using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2PChat.Net
{
    /// <summary>
    /// Accepteur TCP : TcpListener(IPAddress.Any, portTcp), backlog 16,
    /// try/catch global sur la boucle d'acceptation.
    /// Si le port demandé est déjà pris (2e instance sur le même PC), bascule
    /// silencieusement sur un port éphémère : Exposes ActualPort pour annoncer
    /// le port réellement écouté dans le beacon UDP.
    /// </summary>
    public sealed class TcpHost : IDisposable
    {
        private readonly int _requestedPort;
        private readonly object _lifeLock = new object();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private bool _disposed;

        /// <summary>Connexion acceptée : TcpClient déjà connecté + endpoint distant.</summary>
        public event Action<TcpClient, IPEndPoint> ClientAccepted;

        public TcpHost(int tcpPort)
        {
            _requestedPort = tcpPort;
        }

        /// <summary>Port réellement écouté (peut différer du demandé si déjà occupé).</summary>
        public int ActualPort { get; private set; }

        public void Start()
        {
            lock (_lifeLock)
            {
                if (_disposed) throw new ObjectDisposedException("TcpHost");
                if (_running) return;

                TcpListener l = null;
                try
                {
                    l = new TcpListener(IPAddress.Any, _requestedPort);
                    l.Start(16);
                    ActualPort = _requestedPort;
                }
                catch (Exception)
                {
                    // port occupé (autre instance) : port éphémère à la place
                    if (l != null) { try { l.Stop(); } catch (Exception) { } }
                    l = new TcpListener(IPAddress.Any, 0);
                    l.Start(16);
                    ActualPort = ((IPEndPoint)l.LocalEndpoint).Port;
                }

                _listener = l;
                _running = true;
                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Name = "TcpHost.Accept";
                _acceptThread.Start();
            }
        }

        public void Stop()
        {
            lock (_lifeLock)
            {
                _running = false;
                TcpListener l = _listener;
                _listener = null;
                if (l != null) { try { l.Stop(); } catch (Exception) { } }
                Thread t = _acceptThread;
                _acceptThread = null;
                if (t != null && t.IsAlive)
                {
                    try { t.Join(500); } catch (Exception) { }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            lock (_lifeLock) { _disposed = true; }
        }

        private void AcceptLoop()
        {
            TcpListener l = _listener;
            while (_running && l != null)
            {
                TcpClient client = null;
                try
                {
                    client = l.AcceptTcpClient();
                    IPEndPoint ep = client.Client.RemoteEndPoint as IPEndPoint;
                    if (ep == null)
                    {
                        CloseQuietly(client);
                        continue;
                    }
                    try { client.NoDelay = true; } catch (Exception) { }
                    Action<TcpClient, IPEndPoint> h = ClientAccepted;
                    if (h == null)
                    {
                        CloseQuietly(client);
                        continue;
                    }
                    h(client, ep);
                }
                catch (Exception)
                {
                    CloseQuietly(client);
                    if (!_running) break;
                    Thread.Sleep(100);
                }
            }
        }

        private static void CloseQuietly(TcpClient c)
        {
            if (c == null) return;
            try { c.Close(); } catch (Exception) { }
        }
    }
}
