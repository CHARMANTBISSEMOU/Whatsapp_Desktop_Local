using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.Net
{
    /// <summary>
    /// Découverte automatique des pairs du réseau local (zéro configuration), v2 :
    ///  - broadcast sur 255.255.255.255 ET sur le directed-broadcast (x.y.z.255)
    ///    de CHAQUE interface IPv4 up (WiFi, Ethernet, plusieurs réseaux...) ;
    ///  - un UdpClient d'envoi par interface (bind sur l'IP locale de l'interface :
    ///    le broadcast part toujours du bon réseau, même avec plusieurs cartes) ;
    ///  - écoute sur 0.0.0.0 avec SO_REUSEADDR avant Bind (plusieurs instances
    ///    du même PC s'entendent) ;
    ///  - re-scan des interfaces toutes les 30 s (changement de réseau WiFi) ;
    ///  - ignore les annonces « self » ; nouveau pair → re-broadcast immédiat.
    /// </summary>
    public sealed class UdpBeacon : IDisposable
    {
        private const int BroadcastPeriodMs = 3000;
        private const int RescanPeriodTicks = 10;   // 10 × 3 s = 30 s

        private readonly PeerInfo _self;
        private readonly int _udpPort;
        private readonly int _tcpPort;

        private readonly object _lifeLock = new object();
        private readonly object _sendLock = new object();
        private readonly object _knownLock = new object();
        private readonly HashSet<string> _knownPeerIds = new HashSet<string>();

        private readonly List<UdpClient> _senders = new List<UdpClient>();
        private readonly List<IPEndPoint> _targets = new List<IPEndPoint>();
        private UdpClient _recvClient;
        private Thread _recvThread;
        private Timer _broadcastTimer;
        private volatile bool _running;
        private bool _disposed;
        private int _tickCount;

        /// <summary>Levé pour CHAQUE annonce reçue d'un pair (sauf nous-mêmes).</summary>
        public event Action<PeerInfo, IPEndPoint, int> PeerSeen;

        /// <summary>Nombre de cibles de broadcast servies (diagnostics).</summary>
        public int InterfaceCount { get { lock (_sendLock) { return _targets.Count; } } }

        public UdpBeacon(PeerInfo self, int udpPort, int tcpPort)
        {
            if (self == null) throw new ArgumentNullException("self");
            _self = self;
            _udpPort = udpPort;
            _tcpPort = tcpPort;
        }

        public void Start()
        {
            lock (_lifeLock)
            {
                if (_disposed) throw new ObjectDisposedException("UdpBeacon");
                if (_running) return;

                _recvClient = new UdpClient();
                _recvClient.Client.SetSocketOption(SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress, true);
                _recvClient.Client.Bind(new IPEndPoint(IPAddress.Any, _udpPort));

                RefreshInterfaces();

                _running = true;
                _broadcastTimer = new Timer(BroadcastTick, null, 0, BroadcastPeriodMs);
                _recvThread = new Thread(ReceiveLoop);
                _recvThread.IsBackground = true;
                _recvThread.Name = "UdpBeacon.Receive";
                _recvThread.Start();
            }
        }

        /// <summary>
        /// Reconstruit la liste des UdpClient d'envoi : un par interface IPv4 up.
        /// Directed broadcast calculé depuis le masque de l'interface :
        /// iface 192.168.1.42/24 → cible 192.168.1.255.
        /// </summary>
        private void RefreshInterfaces()
        {
            lock (_sendLock)
            {
                for (int i = 0; i < _senders.Count; i++)
                {
                    try { _senders[i].Close(); } catch (Exception) { }
                }
                _senders.Clear();
                _targets.Clear();

                // cible globale en premier (route par défaut)
                bool globalAdded = false;

                NetworkInterface[] nics;
                try { nics = NetworkInterface.GetAllNetworkInterfaces(); }
                catch (Exception) { nics = new NetworkInterface[0]; }

                foreach (NetworkInterface nic in nics)
                {
                    try
                    {
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                        if (nic.OperationalStatus != OperationalStatus.Up) continue;
                        IPInterfaceProperties ip = nic.GetIPProperties();
                        if (ip == null) continue;

                        foreach (UnicastIPAddressInformation u in ip.UnicastAddresses)
                        {
                            if (u.Address == null || u.Address.AddressFamily != AddressFamily.InterNetwork)
                            {
                                continue;
                            }
                            if (IPAddress.IsLoopback(u.Address)) continue;
                            if (u.Address.GetAddressBytes()[0] == 169 && u.Address.GetAddressBytes()[1] == 254)
                            {
                                continue; // link-local APIPA : pas de voisin utile
                            }

                            // directed broadcast de cette interface
                            IPAddress bcast = DirectBroadcast(u.Address, u.IPv4Mask);
                            IPEndPoint target;
                            if (bcast != null)
                            {
                                target = new IPEndPoint(bcast, _udpPort);
                            }
                            else if (!globalAdded)
                            {
                                target = new IPEndPoint(IPAddress.Broadcast, _udpPort);
                                globalAdded = true;
                            }
                            else
                            {
                                continue;
                            }

                            try
                            {
                                UdpClient sender = new UdpClient(new IPEndPoint(u.Address, 0));
                                sender.EnableBroadcast = true;
                                _senders.Add(sender);
                                _targets.Add(target);
                            }
                            catch (Exception)
                            {
                                // bind impossible sur cette IP : on tente via broadcast global
                                if (!globalAdded)
                                {
                                    try
                                    {
                                        UdpClient sender = new UdpClient();
                                        sender.EnableBroadcast = true;
                                        _senders.Add(sender);
                                        _targets.Add(new IPEndPoint(IPAddress.Broadcast, _udpPort));
                                        globalAdded = true;
                                    }
                                    catch (Exception) { }
                                }
                            }

                            // pareil pour 255.255.255.255 une fois au moins
                            if (!globalAdded && _senders.Count > 0)
                            {
                                _targets.Add(new IPEndPoint(IPAddress.Broadcast, _udpPort));
                                _senders.Add(_senders[0]); // même socket, cibles multiples
                                globalAdded = true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // interface illisible : on continue avec la suivante
                    }
                }

                // aucun senders (rare) : un broadcast global best-effort
                if (_senders.Count == 0)
                {
                    try
                    {
                        UdpClient sender = new UdpClient();
                        sender.EnableBroadcast = true;
                        _senders.Add(sender);
                        _targets.Add(new IPEndPoint(IPAddress.Broadcast, _udpPort));
                    }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>Broadcast dirigé (ip | ~mask) — null si masque inconnu.</summary>
        private static IPAddress DirectBroadcast(IPAddress addr, IPAddress mask)
        {
            if (mask == null) return null;
            byte[] a = addr.GetAddressBytes();
            byte[] m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4) return null;
            byte[] b = new byte[4];
            for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | (byte)~m[i]);
            return new IPAddress(b);
        }

        private void BroadcastTick(object state)
        {
            if (!_running) return;
            try
            {
                _tickCount++;
                if (_tickCount % RescanPeriodTicks == 0)
                {
                    RefreshInterfaces(); // changement de WiFi / ajout de carte
                }
                BroadcastNow();
            }
            catch (Exception) { }
        }

        /// <summary>Diffuse l'annonce sur toutes les interfaces, tout de suite.</summary>
        public void BroadcastNow()
        {
            byte[] bytes;
            lock (_sendLock)
            {
                if (!_running || _senders.Count == 0) return;
            }
            try
            {
                bytes = Encoding.UTF8.GetBytes(BuildPayload());
            }
            catch (Exception)
            {
                return;
            }
            lock (_sendLock)
            {
                if (!_running) return;
                for (int i = 0; i < _senders.Count && i < _targets.Count; i++)
                {
                    try
                    {
                        _senders[i].Send(bytes, bytes.Length, _targets[i]);
                    }
                    catch (Exception)
                    {
                        // best effort : les autres interfaces portent l'annonce
                    }
                }
            }
        }

        private string BuildPayload()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["peer"] = _self.ToDict();
            d["tcp"] = _tcpPort;
            return MiniJson.Serialize(d);
        }

        private void ReceiveLoop()
        {
            UdpClient rc = _recvClient;
            if (rc == null) return;
            while (_running)
            {
                byte[] data;
                IPEndPoint from;
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    data = rc.Receive(ref remote);
                    from = remote;
                }
                catch (SocketException)
                {
                    if (!_running) break;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (!_running) break;
                    continue;
                }
                if (data == null || data.Length == 0) continue;
                HandleDatagram(data, from);
            }
        }

        private void HandleDatagram(byte[] data, IPEndPoint from)
        {
            PeerInfo peer = null;
            int remoteTcpPort = 0;
            try
            {
                Dictionary<string, object> d = MiniJson.ParseObject(Encoding.UTF8.GetString(data));
                peer = PeerInfo.FromDict(MiniJson.AsDict(d, "peer"));
                long tcp = MiniJson.AsLong(d, "tcp", 0);
                if (tcp > 0 && tcp <= 65535) remoteTcpPort = (int)tcp;
            }
            catch (Exception)
            {
                return; // annonce illisible : ignorée
            }
            if (peer == null || string.IsNullOrEmpty(peer.Id)) return;
            if (string.Equals(peer.Id, _self.Id, StringComparison.Ordinal)) return;
            if (remoteTcpPort <= 0) return;

            // source en 127.x : on mémorise l'IP locale réelle pour TCP loopback
            if (from != null && from.Address != null && IPAddress.IsLoopback(from.Address))
            {
                from = new IPEndPoint(IPAddress.Loopback, from.Port);
            }
            peer.Online = true;

            bool isNew = false;
            lock (_knownLock)
            {
                isNew = _knownPeerIds.Add(peer.Id);
            }

            Action<PeerInfo, IPEndPoint, int> h = PeerSeen;
            if (h != null)
            {
                try { h(peer, from, remoteTcpPort); }
                catch (Exception) { }
            }

            if (isNew)
            {
                BroadcastNow(); // il nous découvre immédiatement aussi
            }
        }

        public void Stop()
        {
            lock (_lifeLock)
            {
                _running = false;
                if (_broadcastTimer != null)
                {
                    try { _broadcastTimer.Dispose(); } catch (Exception) { }
                    _broadcastTimer = null;
                }
                lock (_sendLock)
                {
                    for (int i = 0; i < _senders.Count; i++)
                    {
                        try { _senders[i].Close(); } catch (Exception) { }
                    }
                    _senders.Clear();
                    _targets.Clear();
                }
                UdpClient rc = _recvClient;
                _recvClient = null;
                if (rc != null)
                {
                    try { rc.Close(); } catch (Exception) { }
                }
                Thread t = _recvThread;
                _recvThread = null;
                if (t != null && t.IsAlive)
                {
                    try { t.Join(500); } catch (Exception) { }
                }
            }
        }

        public void Dispose()
        {
            lock (_lifeLock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            Stop();
        }
    }
}
