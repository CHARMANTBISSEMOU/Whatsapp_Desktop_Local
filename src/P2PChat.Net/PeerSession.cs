using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using P2PChat.Core.Crypto;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.Net
{
    /// <summary>
    /// UNE connexion TCP chiffrée avec UN pair.
    ///
    /// Protocole EXACT :
    ///  [client] envoie d'abord une trame NON chiffrée [len4 LE][WireEnvelope(T=Handshake).Encode()]
    ///   où D = MiniJson de {"self":PeerInfo.ToDict(),"pub":CryptoSuite.ToBase64(cléPubliqueEphémère)} ;
    ///  [serveur] répond pareil avec T=HandshakeAck ;
    ///  chaque côté dérive SessionCipher.FromSharedSecret(
    ///    CryptoSuite.DeriveSharedSecret(maPaire, CryptoSuite.FromBase64(pubDistante))) ;
    ///  ENSUITE TOUTES les trames = [len4 LE][cipher.Encrypt(envelope.Encode())].
    ///
    /// Réception : boucle dédiée (read 4 octets → WireFraming.ValidateLength → read len
    /// → Decrypt → WireEnvelope.Decode). Handshake non terminé en 5 s → close.
    /// Écriture : lock (une seule écriture à la fois, jamais d'attente sous lock).
    /// </summary>
    public sealed class PeerSession : IDisposable
    {
        public const int HandshakeTimeoutMs = 5000;

        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly PeerInfo _self;
        private readonly bool _isClient;
        private readonly object _writeLock = new object();
        private readonly object _lifeLock = new object();

        private EcdhKeyPair _keyPair;
        private byte[] _remotePubB;
        private volatile SessionCipher _cipher;
        private PeerInfo _remotePeer;
        private TaskCompletionSource<bool> _handshakeDone;
        private volatile bool _handshakeComplete;
        private volatile bool _closed;
        private Timer _handshakeTimer;

        /// <summary>Après handshake uniquement : toute trame décryptée (jamais Handshake/HandshakeAck).</summary>
        public event Action<WireEnvelope> EnvelopeReceived;

        /// <summary>Levé après le handshake (côté client : Ack reçu ; côté serveur : Ack envoyé).</summary>
        public event Action<PeerInfo> PeerIdentified;

        /// <summary>Levé exactement UNE fois quand la connexion se ferme (timeout compris).</summary>
        public event Action Disconnected;

        /// <summary>Levé si le handshake n'a pas abouti dans les 5 s (la session se ferme ensuite).</summary>
        public event Action HandshakeTimeout;

        public PeerSession(TcpClient connectedClient, bool isClient, PeerInfo self)
        {
            if (connectedClient == null) throw new ArgumentNullException("connectedClient");
            if (self == null) throw new ArgumentNullException("self");
            _tcp = connectedClient;
            _isClient = isClient;
            _self = self;
            _stream = connectedClient.GetStream();
        }

        public PeerInfo RemotePeer { get { return _remotePeer; } }
        public bool IsClient { get { return _isClient; } }
        public bool IsClosed { get { return _closed; } }
        public bool IsHandshakeComplete { get { return _handshakeComplete; } }

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                try { return _tcp.Client.RemoteEndPoint as IPEndPoint; }
                catch (Exception) { return null; }
            }
        }

        public void Start()
        {
            lock (_lifeLock)
            {
                if (_closed) return;
                if (_handshakeDone != null) return; // déjà démarrée
                _handshakeDone = new TaskCompletionSource<bool>();
                _handshakeTimer = new Timer(OnHandshakeTimeout, null, HandshakeTimeoutMs, Timeout.Infinite);
                Thread t = new Thread(Run);
                t.IsBackground = true;
                t.Name = _isClient ? "PeerSession.Client" : "PeerSession.Server";
                t.Start();
            }
        }

        private void OnHandshakeTimeout(object state)
        {
            bool timeout = false;
            lock (_lifeLock)
            {
                if (!_closed && !_handshakeComplete) timeout = true;
            }
            if (!timeout) return;
            Action h = HandshakeTimeout;
            if (h != null)
            {
                try { h(); } catch (Exception) { }
            }
            Close();
        }

        private void Run()
        {
            try
            {
                if (_isClient)
                {
                    // CLIENT : trame initiale EN CLAIR puis attente du HandshakeAck
                    SendPlainFrame(BuildHandshake(WireType.Handshake));
                    WireEnvelope first = WireEnvelope.Decode(ReadFrame());
                    if (first.T != WireType.HandshakeAck) return;
                    if (!ReadRemoteFrom(first)) return;
                    if (!DeriveCipher()) return;
                }
                else
                {
                    // SERVEUR : lit le Handshake puis répond EN CLAIR avec l'Ack
                    WireEnvelope first = WireEnvelope.Decode(ReadFrame());
                    if (first.T != WireType.Handshake) return;
                    if (!ReadRemoteFrom(first)) return;
                    _keyPair = CryptoSuite.CreateKeyPair();
                    if (!DeriveCipher()) return;
                    SendPlainFrame(BuildAckEnvelope());
                }

                MarkHandshakeComplete();
                ReceiveLoop();
            }
            catch (Exception)
            {
                // fin de flux, MAC invalide, timeout… : fermeture propre ci-dessous
            }
            finally
            {
                CloseInternal();
            }
        }

        private WireEnvelope BuildHandshake(WireType t)
        {
            _keyPair = CryptoSuite.CreateKeyPair();
            return BuildSelfPubEnvelope(t);
        }

        private WireEnvelope BuildAckEnvelope()
        {
            // _keyPair déjà créée côté serveur
            return BuildSelfPubEnvelope(WireType.HandshakeAck);
        }

        private WireEnvelope BuildSelfPubEnvelope(WireType t)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["self"] = _self.ToDict();
            d["pub"] = CryptoSuite.ToBase64(_keyPair.PublicKeyB);
            return WireEnvelope.Create(t, _self.Id, MiniJson.Serialize(d));
        }

        private bool ReadRemoteFrom(WireEnvelope env)
        {
            try
            {
                Dictionary<string, object> d = env.Payload();
                PeerInfo p = PeerInfo.FromDict(MiniJson.AsDict(d, "self"));
                string pub = MiniJson.AsString(d, "pub");
                if (p == null || string.IsNullOrEmpty(pub)) return false;
                if (string.Equals(p.Id, _self.Id, StringComparison.Ordinal)) return false; // anti-boucle
                _remotePeer = p;
                _remotePubB = CryptoSuite.FromBase64(pub);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool DeriveCipher()
        {
            try
            {
                byte[] secret = CryptoSuite.DeriveSharedSecret(_keyPair, _remotePubB);
                _cipher = SessionCipher.FromSharedSecret(secret);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void MarkHandshakeComplete()
        {
            _handshakeComplete = true;
            TaskCompletionSource<bool> tcs = _handshakeDone;
            if (tcs != null)
            {
                try { tcs.TrySetResult(true); } catch (Exception) { }
            }
            Timer t = _handshakeTimer;
            _handshakeTimer = null;
            if (t != null)
            {
                try { t.Dispose(); } catch (Exception) { }
            }
            Action<PeerInfo> h = PeerIdentified;
            PeerInfo p = _remotePeer;
            if (h != null && p != null)
            {
                try { h(p); } catch (Exception) { }
            }
        }

        private void ReceiveLoop()
        {
            SessionCipher cipher = _cipher;
            while (!_closed)
            {
                byte[] frame = ReadFrame();
                byte[] plain = cipher.Decrypt(frame);
                WireEnvelope env = WireEnvelope.Decode(plain);
                Action<WireEnvelope> h = EnvelopeReceived;
                if (h != null)
                {
                    try { h(env); } catch (Exception) { }
                }
            }
        }

        private byte[] ReadFrame()
        {
            byte[] prefix = new byte[WireFraming.PrefixBytes];
            if (!ReadExact(prefix, 0, prefix.Length))
            {
                throw new EndOfStreamException("Flux TCP fermé.");
            }
            int len = WireFraming.ReadPrefix(prefix, 0);
            WireFraming.ValidateLength(len);
            byte[] data = new byte[len];
            if (!ReadExact(data, 0, len))
            {
                throw new EndOfStreamException("Flux TCP fermé.");
            }
            return data;
        }

        private bool ReadExact(byte[] buffer, int offset, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = _stream.Read(buffer, offset + got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        private void SendPlainFrame(WireEnvelope env)
        {
            WriteFrame(env.Encode());
        }

        /// <summary>Écrit [len4 LE][payload] sous lock (une seule écriture à la fois).</summary>
        private void WriteFrame(byte[] payload)
        {
            WireFraming.ValidateLength(payload.Length);
            byte[] prefix = new byte[WireFraming.PrefixBytes];
            WireFraming.WritePrefix(prefix, payload.Length);
            lock (_writeLock)
            {
                if (_closed) throw new ObjectDisposedException("PeerSession");
                _stream.Write(prefix, 0, prefix.Length);
                _stream.Write(payload, 0, payload.Length);
                _stream.Flush();
            }
        }

        /// <summary>
        /// Envoie une enveloppe UNE FOIS le handshake terminé (AES-256-CBC + HMAC).
        /// Retourne false si impossible (fermée, avant handshake, I/O KO).
        /// </summary>
        public bool SendEnvelope(WireEnvelope env)
        {
            if (env == null || _closed || !_handshakeComplete) return false;
            SessionCipher cipher = _cipher;
            if (cipher == null) return false;
            try
            {
                byte[] plain = env.Encode();
                lock (_writeLock)
                {
                    if (_closed) return false;
                    byte[] enc = cipher.Encrypt(plain);
                    WireFraming.ValidateLength(enc.Length);
                    byte[] prefix = new byte[WireFraming.PrefixBytes];
                    WireFraming.WritePrefix(prefix, enc.Length);
                    _stream.Write(prefix, 0, prefix.Length);
                    _stream.Write(enc, 0, enc.Length);
                    _stream.Flush();
                }
                return true;
            }
            catch (Exception)
            {
                Close();
                return false;
            }
        }

        /// <summary>Attend la fin du handshake (false si timeout/fermeture).</summary>
        public async Task<bool> WaitHandshakeAsync(int timeoutMs)
        {
            if (_closed) return false;
            if (_handshakeComplete) return true;
            TaskCompletionSource<bool> tcs = _handshakeDone;
            int waited = 0;
            while (tcs == null && waited < 300)
            {
                // Start() imminent : léger délai avant d'abandonner
                await Task.Delay(20).ConfigureAwait(false);
                if (_closed) return false;
                if (_handshakeComplete) return true;
                tcs = _handshakeDone;
                waited += 20;
            }
            if (_closed) return false;
            if (_handshakeComplete) return true;
            if (tcs == null) return false;
            Task done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (done != tcs.Task) return false;
            return _handshakeComplete && !_closed;
        }

        public void Close() { CloseInternal(); }

        public void Dispose() { CloseInternal(); }

        private void CloseInternal()
        {
            bool fire = false;
            lock (_lifeLock)
            {
                if (!_closed)
                {
                    _closed = true;
                    fire = true;
                }
            }
            if (!fire) return;

            Timer t = _handshakeTimer;
            _handshakeTimer = null;
            if (t != null)
            {
                try { t.Dispose(); } catch (Exception) { }
            }

            TaskCompletionSource<bool> tcs = _handshakeDone;
            if (tcs != null)
            {
                try { tcs.TrySetResult(false); } catch (Exception) { }
            }

            try { _stream.Close(); } catch (Exception) { }
            try { _tcp.Close(); } catch (Exception) { }

            SessionCipher c = _cipher;
            _cipher = null;
            if (c != null)
            {
                try { c.Dispose(); } catch (Exception) { }
            }

            Action h = Disconnected;
            if (h != null)
            {
                try { h(); } catch (Exception) { }
            }
        }
    }
}
