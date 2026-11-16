using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using P2PChat.Core.Interfaces;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.Net
{
    /// <summary>
    /// Moteur réseau P2P chiffré (ECDH P-256 puis AES-256-CBC/HMAC par session TCP) :
    /// découverte 100% automatique (beacon UDP broadcast), UNE session "chiffrée" par pair,
    /// transferts de fichiers par morceaux base64 de 65536 octets, timeout de présence 12 s,
    /// purge des transferts > 5 min, événements pivotés sur le SynchronizationContext
    /// fourni quand il n'est pas null (sinon appel direct sur le thread réseau).
    /// Locks courts, jamais d'await sous lock, C# 7.3 / .NET Framework 4.8, zéro NuGet.
    /// </summary>
    public sealed class NetworkEngine : IChatNetwork
    {
        private const long PresenceTimeoutMs = 12000;  // 12 s sans beacon → Offline
        private const int ConnectTimeoutMs = 4000;
        private const int HandshakeWaitMs = 7000;       // > timeout handshake (5 s) de la session
        private const int CleanupPeriodMs = 3000;
        private const long TypingThrottleMs = 2500;      // bridge : 1 envoi / 2,5 s par pair
        private const long TransferStaleMs = 300000;    // purge des transferts > 5 min

        private readonly PeerInfo _self;
        private readonly int _udpPort;
        private readonly int _tcpPort;
        private readonly SynchronizationContext _uiContext;
        private readonly FileAssembler _assembler;

        private readonly object _sync = new object(); // protège _states et _unidentified
        private readonly Dictionary<string, PeerState> _states = new Dictionary<string, PeerState>(); // clé = peer.id
        private readonly List<PeerSession> _unidentified = new List<PeerSession>(); // sessions entrantes avant identification
        private readonly object _transferLock = new object();
        private readonly Dictionary<string, IncomingMeta> _incoming = new Dictionary<string, IncomingMeta>(); // clé = peerId|id

        private TcpHost _host;
        private UdpBeacon _beacon;
        private string _discoveryError;
        private Timer _cleanupTimer;
        private volatile bool _started;
        private volatile bool _disposed;

        private static readonly Task<bool> TaskTrue = Task.FromResult(true);
        private static readonly Task<bool> TaskFalse = Task.FromResult(false);
        private static readonly Task TaskDone = Task.FromResult<object>(null);

        /// <summary>État interne d'un pair connu (par discovery beacon OU par session entrante).</summary>
        private sealed class PeerState
        {
            internal readonly object Sync = new object();
            internal readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1); // séquenceur d'envois du pair
            internal PeerInfo Info;
            internal IPAddress Host;
            internal int RemoteTcpPort;
            internal long LastSeenUtcMs;
            internal long LastTypingSentUtcMs;
            internal PeerSession Session;
            internal bool Connecting;
            internal bool Online;
        }

        /// <summary>Métadonnées d'un transfert entrant (reconstitué à FileDone).</summary>
        private sealed class IncomingMeta
        {
            internal readonly long StartedUtcMs = ChatMessage.NowUtcMs();
            internal string Id;
            internal string FileName;
            internal long FileSize;
            internal int Dur;
            internal int Kind;
            internal long Ts;
            internal string Text;
            internal string Waveform;
            internal string ReplyToId;
            internal string QuoteAuthor;
            internal string QuoteText;
            internal string GroupId;
        }

        public NetworkEngine(PeerInfo self, int udpPort, int tcpPort, string transferDir,
            SynchronizationContext uiContext)
        {
            if (self == null) throw new ArgumentNullException("self");
            _self = self;
            _udpPort = udpPort;
            _tcpPort = tcpPort;
            _assembler = new FileAssembler(transferDir);
            _uiContext = uiContext;
        }

        public PeerInfo Self { get { return _self; } }

        public IReadOnlyList<PeerInfo> Peers
        {
            get
            {
                List<PeerInfo> list = new List<PeerInfo>();
                lock (_sync)
                {
                    foreach (KeyValuePair<string, PeerState> kv in _states)
                    {
                        PeerState st = kv.Value;
                        if (!st.Online || st.Info == null) continue;
                        PeerInfo p = st.Info.Clone();
                        p.Online = true;
                        list.Add(p);
                    }
                }
                return list;
            }
        }

        public event EventHandler<PeerInfo> PeerAppeared;
        public event EventHandler PeerListChanged;
        public event EventHandler<ChatMessage> MessageReceived;
        public event EventHandler<ChatMessage> MessageStatusChanged;
        public event EventHandler<string> TypingChanged;
        public event Action<string, string, string> ReactionReceived;
        public event Action<string, string, string> MessageEdited;
        public event Action<string, string> MessageDeletedForAll;
        public event Action<string, string, string, List<string>> GroupMetaReceived;
        public event Action<string, StatusPost> StatusReceived;
        public event Action<string, CallSignal> CallSignalReceived;

        /// <summary>Démarre la découverte UDP + le serveur TCP + le périphérique de nettoyage.</summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException("NetworkEngine");
            if (_started) return;
            _started = true;

            _host = new TcpHost(_tcpPort);
            _host.ClientAccepted += OnClientAccepted;
            _host.Start(); // accepte AVANT d'annoncer notre présence

            UdpBeacon beacon = new UdpBeacon(_self, _udpPort, _host.ActualPort);
            try
            {
                beacon.PeerSeen += OnPeerSeen;
                beacon.Start();
                _beacon = beacon;
                _discoveryError = null;
            }
            catch (Exception ex)
            {
                try { beacon.Dispose(); } catch (Exception) { }
                _beacon = null; // UDP indisponible : le TCP reste utilisable si on nous contacte
                _discoveryError = ex.Message;
            }

            _cleanupTimer = new Timer(CleanupTick, null, CleanupPeriodMs, CleanupPeriodMs);
        }

        /// <summary>Tous les événements passent par uiContext.Post si != null, sinon appel direct.</summary>
        private void PostUi(SendOrPostCallback cb)
        {
            SynchronizationContext ctx = _uiContext;
            if (ctx != null)
            {
                try { ctx.Post(cb, null); } catch (Exception) { }
            }
            else
            {
                cb(null);
            }
        }

        private void RaisePeerAppeared(PeerInfo info)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                EventHandler<PeerInfo> h = PeerAppeared;
                if (h != null) h(this, info);
            });
        }

        private void RaisePeerListChanged()
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                EventHandler h = PeerListChanged;
                if (h != null) h(this, EventArgs.Empty);
            });
        }

        private void RaiseMessageReceived(ChatMessage msg)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                EventHandler<ChatMessage> h = MessageReceived;
                if (h != null) h(this, msg);
            });
        }

        private void RaiseMessageStatusChanged(ChatMessage status)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                EventHandler<ChatMessage> h = MessageStatusChanged;
                if (h != null) h(this, status);
            });
        }

        private void RaiseTypingChanged(string peerId)
        {
            if (_disposed) return;
            string arg = peerId;
            PostUi(delegate
            {
                if (_disposed) return;
                EventHandler<string> h = TypingChanged;
                if (h != null) h(this, arg);
            });
        }

        private void RaiseStatusChanged(string messageId, string peerId, DeliveryStatus status)
        {
            ChatMessage m = new ChatMessage();
            m.Id = messageId;
            m.PeerId = peerId;
            m.SenderId = _self.Id; // c'est MON message
            m.IsMine = true;
            m.Status = status;
            RaiseMessageStatusChanged(m);
        }

        private PeerState LookupState(string peerId)
        {
            if (string.IsNullOrEmpty(peerId)) return null;
            lock (_sync)
            {
                PeerState st;
                return _states.TryGetValue(peerId, out st) ? st : null;
            }
        }

        private void HookSession(PeerSession s)
        {
            PeerSession captured = s;
            s.PeerIdentified += delegate(PeerInfo p) { OnSessionIdentified(captured, p); };
            s.EnvelopeReceived += delegate(WireEnvelope e) { OnEnvelopeReceived(captured, e); };
            s.Disconnected += delegate { OnSessionDisconnected(captured); };
        }

        private void OnClientAccepted(TcpClient client, IPEndPoint ep)
        {
            if (_disposed)
            {
                try { client.Close(); } catch (Exception) { }
                return;
            }
            PeerSession s = new PeerSession(client, false, _self); // rôle serveur
            HookSession(s);
            lock (_sync)
            {
                _unidentified.Add(s);
            }
            s.Start();
        }

        /// <summary>
        /// Beacon UDP reçu : ajout/mise à jour du pair (adresse, port TCP, dernier contact),
        /// PeerAppeared + PeerListChanged si nouveau, connexion TCP si pas encore de session.
        /// </summary>
        private void OnPeerSeen(PeerInfo peer, IPEndPoint from, int remoteTcpPort)
        {
            if (_disposed || peer == null || string.IsNullOrEmpty(peer.Id)) return;
            if (string.Equals(peer.Id, _self.Id, StringComparison.Ordinal)) return;

            PeerState st = null;
            lock (_sync)
            {
                PeerState found;
                if (!_states.TryGetValue(peer.Id, out found))
                {
                    found = new PeerState();
                    _states[peer.Id] = found;
                }
                st = found;
            }

            bool isNew = (st.Info == null);
            bool listChanged = false;
            PeerInfo snapshot;

            lock (st.Sync)
            {
                st.LastSeenUtcMs = ChatMessage.NowUtcMs();
                if (from != null) st.Host = from.Address;
                if (remoteTcpPort > 0) st.RemoteTcpPort = remoteTcpPort;

                PeerInfo fresh = peer.Clone();
                fresh.Online = true;
                PeerInfo cur = st.Info;
                bool infoChanged = cur == null
                    || !string.Equals(cur.Pseudo, fresh.Pseudo, StringComparison.Ordinal)
                    || !string.Equals(cur.Bio, fresh.Bio, StringComparison.Ordinal)
                    || fresh.UpdatedUtc > cur.UpdatedUtc
                    || !cur.Online;
                if (infoChanged) st.Info = fresh;

                if (!st.Online)
                {
                    st.Online = true;
                    listChanged = true;
                }
                if (infoChanged) listChanged = true;

                snapshot = st.Info.Clone();
                snapshot.Online = true;
            }

            if (isNew)
            {
                RaisePeerAppeared(snapshot);
                RaisePeerListChanged();
            }
            else if (listChanged)
            {
                RaisePeerListChanged();
            }

            // connexion TCP automatique si pas encore de session
            if (st.Session == null)
            {
                Task unused = EnsureSessionAsync(st);
            }
        }

        private static void ClearSessionIfCurrent(PeerState st, PeerSession s)
        {
            lock (st.Sync)
            {
                if (st.Session == s) st.Session = null;
            }
        }

        /// <summary>
        /// Retourne une session prête (handshake OK) vers ce pair, en connectant si besoin
        /// (timeout connect 4 s, attente handshake 7 s). Null si injoignable.
        /// Jamais d'attente sous lock.
        /// </summary>
        private async Task<PeerSession> EnsureSessionAsync(PeerState st)
        {
            if (_disposed) return null;

            PeerSession s = st.Session;
            if (s != null && !s.IsClosed)
            {
                bool ok = await s.WaitHandshakeAsync(HandshakeWaitMs).ConfigureAwait(false);
                if (ok && s.IsHandshakeComplete && !s.IsClosed) return s;
                ClearSessionIfCurrent(st, s);
            }

            bool creator = false;
            IPAddress host = null;
            int port = 0;
            lock (st.Sync)
            {
                PeerSession cur = st.Session;
                if (cur != null && !cur.IsClosed)
                {
                    s = cur;
                }
                else
                {
                    s = null;
                    if (!st.Connecting)
                    {
                        creator = true;
                        st.Connecting = true;
                        host = st.Host;
                        port = st.RemoteTcpPort;
                    }
                }
            }

            if (!creator)
            {
                if (s == null) return null; // une autre tâche connecte déjà
                bool ok2 = await s.WaitHandshakeAsync(HandshakeWaitMs).ConfigureAwait(false);
                return (ok2 && s.IsHandshakeComplete && !s.IsClosed) ? s : null;
            }

            if (host == null || port <= 0)
            {
                lock (st.Sync) { st.Connecting = false; }
                return null; // adresse encore inconnue : le beacon la fournira sous peu
            }

            TcpClient tcp = null;
            PeerSession created = null;
            try
            {
                tcp = new TcpClient();
                tcp.NoDelay = true;
                Task connectTask = tcp.ConnectAsync(host, port);
                bool connected = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs))
                    .ConfigureAwait(false) == connectTask;
                if (connectTask.IsFaulted || connectTask.IsCanceled)
                {
                    AggregateException observed = connectTask.Exception; // observe l'échec
                }
                if (!connected || !tcp.Connected)
                {
                    try { tcp.Close(); } catch (Exception) { }
                    return null;
                }
                created = new PeerSession(tcp, true, _self); // rôle client
                HookSession(created);
                lock (st.Sync)
                {
                    PeerSession cur = st.Session;
                    if (cur == null || cur.IsClosed)
                    {
                        st.Session = created;
                    }
                    // sinon : une session entrante a été adoptée entre-temps ;
                    // l'arbitrage anti-doublon (OnSessionIdentified) tranchera.
                }
                created.Start();
                bool done = await created.WaitHandshakeAsync(HandshakeWaitMs).ConfigureAwait(false);
                if (done && created.IsHandshakeComplete && !created.IsClosed) return created;
                ClearSessionIfCurrent(st, created);
                created.Close();
                return null;
            }
            catch (Exception)
            {
                if (created != null)
                {
                    ClearSessionIfCurrent(st, created);
                    created.Close();
                }
                else if (tcp != null)
                {
                    try { tcp.Close(); } catch (Exception) { }
                }
                return null;
            }
            finally
            {
                lock (st.Sync) { st.Connecting = false; }
            }
        }

        /// <summary>
        /// Handshake terminé sur une session. Applique la règle anti-doublon :
        /// une seule session vivante par peer.id. Si deux connexions concurrentes
        /// existent (les deux pairs initient), celle dont l'INITIATEUR a le plus
        /// petit id gagne (décision identique des deux côtés) ; à égalité, l'entrant
        /// déjà lié garde sa session et la NOUVELLE connexion est fermée.
        /// </summary>
        private void OnSessionIdentified(PeerSession session, PeerInfo peer)
        {
            if (session == null) return;
            lock (_sync) { _unidentified.Remove(session); }
            if (peer == null || string.IsNullOrEmpty(peer.Id)) return;

            if (string.Equals(peer.Id, _self.Id, StringComparison.Ordinal))
            {
                session.Close(); // anti-boucle : un pair qui annonce notre id est rejeté
                return;
            }
            if (session.IsClosed) return; // déjà morte : rien à adopter

            PeerState st;
            bool newBySession = false;
            lock (_sync)
            {
                PeerState found;
                if (!_states.TryGetValue(peer.Id, out found))
                {
                    found = new PeerState();
                    PeerInfo baseInfo = peer.Clone();
                    baseInfo.Online = false;
                    found.Info = baseInfo;
                    IPEndPoint rep = session.RemoteEndPoint;
                    if (rep != null) found.Host = rep.Address;
                    _states[peer.Id] = found;
                    newBySession = true;
                }
                st = found;
            }

            PeerSession toClose = null;
            PeerInfo appearedSnapshot = null;
            bool listChanged = false;

            lock (st.Sync)
            {
                PeerSession cur = st.Session;
                if (cur == session)
                {
                    // session sortante déjà enregistrée : rien à arbitrer
                }
                else if (cur == null || cur.IsClosed)
                {
                    st.Session = session;
                }
                else
                {
                    string curInit = cur.IsClient ? _self.Id : peer.Id;
                    string newInit = session.IsClient ? _self.Id : peer.Id;
                    if (string.CompareOrdinal(newInit, curInit) < 0)
                    {
                        st.Session = session; // la nouvelle connexion gagne
                        toClose = cur;
                    }
                    else
                    {
                        toClose = session; // déjà lié (même peer.id) : la nouvelle connexion ferme
                    }
                }

                if (st.Session == session)
                {
                    PeerInfo fresh = peer.Clone();
                    PeerInfo old = st.Info;
                    bool newer = old == null
                        || old.Pseudo.Length == 0
                        || fresh.UpdatedUtc >= old.UpdatedUtc;
                    if (old != null && fresh.UpdatedUtc == 0 && old.UpdatedUtc > 0) newer = false;
                    if (newer) st.Info = fresh;
                    if (!st.Online)
                    {
                        st.Online = true;
                        if (st.Info != null) st.Info.Online = true;
                        listChanged = true;
                    }
                    if (newBySession)
                    {
                        appearedSnapshot = st.Info.Clone();
                        appearedSnapshot.Online = true;
                        listChanged = true;
                    }
                }
            }

            if (toClose != null) toClose.Close();
            if (appearedSnapshot != null) RaisePeerAppeared(appearedSnapshot);
            if (listChanged) RaisePeerListChanged();
        }

        private void OnSessionDisconnected(PeerSession session)
        {
            if (session == null) return;
            PeerInfo remote = session.RemotePeer;
            lock (_sync) { _unidentified.Remove(session); }
            if (remote == null || string.IsNullOrEmpty(remote.Id)) return;

            PeerState st = LookupState(remote.Id);
            bool changed = false;
            if (st != null)
            {
                lock (st.Sync)
                {
                    if (st.Session == session)
                    {
                        st.Session = null;
                        if (st.Online)
                        {
                            st.Online = false; // TCP fermé → Offline
                            if (st.Info != null) st.Info.Online = false;
                            changed = true;
                        }
                    }
                }
            }
            if (changed) RaisePeerListChanged();
        }

        /// <summary>Timeout de présence 12 s / TCP mort → Online=false ; purge transferts 5 min.</summary>
        private void CleanupTick(object state)
        {
            if (_disposed) return;
            long now = ChatMessage.NowUtcMs();
            bool changed = false;

            lock (_sync)
            {
                foreach (KeyValuePair<string, PeerState> kv in _states)
                {
                    PeerState st = kv.Value;
                    lock (st.Sync)
                    {
                        PeerSession s = st.Session;
                        bool beaconStale = st.LastSeenUtcMs > 0
                            && (now - st.LastSeenUtcMs > PresenceTimeoutMs);
                        bool tcpClosed = (s != null && s.IsClosed);
                        if (st.Online && (beaconStale || tcpClosed))
                        {
                            st.Online = false;
                            if (st.Info != null) st.Info.Online = false;
                            if (tcpClosed) st.Session = null;
                            changed = true;
                        }
                    }
                }
            }

            if (changed) RaisePeerListChanged();

            try { _assembler.PurgeStale(); } catch (Exception) { }

            lock (_transferLock)
            {
                if (_incoming.Count > 0)
                {
                    List<string> drop = new List<string>();
                    foreach (KeyValuePair<string, IncomingMeta> kv in _incoming)
                    {
                        if (now - kv.Value.StartedUtcMs > TransferStaleMs) drop.Add(kv.Key);
                    }
                    for (int i = 0; i < drop.Count; i++) _incoming.Remove(drop[i]);
                }
            }
        }

        private void OnEnvelopeReceived(PeerSession session, WireEnvelope env)
        {
            if (_disposed || session == null || env == null) return;
            try
            {
                PeerInfo remote = session.RemotePeer;
                if (remote == null || string.IsNullOrEmpty(remote.Id)) return;
                if (string.Equals(remote.Id, _self.Id, StringComparison.Ordinal)) return;
                string peerId = remote.Id;

                switch (env.T)
                {
                    case WireType.TextMessage:
                    case WireType.VoiceMessage: // toléré comme texte (vrais vocaux → FileMeta/chunks)
                        HandleText(session, peerId, env);
                        break;
                    case WireType.FileMeta:
                        HandleFileMeta(peerId, env);
                        break;
                    case WireType.FileChunk:
                        HandleFileChunk(env);
                        break;
                    case WireType.FileDone:
                        HandleFileDone(session, peerId, env);
                        break;
                    case WireType.Typing:
                        RaiseTypingChanged(peerId);
                        break;
                    case WireType.Reaction:
                        HandleReaction(peerId, env);
                        break;
                    case WireType.EditMessage:
                        HandleEdit(peerId, env);
                        break;
                    case WireType.DeleteForAll:
                        HandleDeleteForAll(peerId, env);
                        break;
                    case WireType.GroupMeta:
                        HandleGroupMeta(peerId, env);
                        break;
                    case WireType.GroupText:
                        HandleGroupText(session, peerId, env);
                        break;
                    case WireType.StatusPost:
                        HandleStatus(peerId, env);
                        break;
                    case WireType.CallSignal:
                        HandleCallSignal(peerId, env);
                        break;
                    case WireType.Receipt:
                        HandleReceipt(peerId, env);
                        break;
                    case WireType.Goodbye:
                        HandleGoodbye(session, peerId);
                        break;
                    default:
                        break; // Handshake/HandshakeAck ne passent jamais ici
                }
            }
            catch (Exception)
            {
                // trame incohérente : ignorée, la session reste ouverte
            }
        }

        private void HandleCallSignal(string peerId, WireEnvelope env)
        {
            CallSignal signal = CallSignal.FromDict(env.Payload());
            if (signal == null || string.IsNullOrEmpty(signal.CallId) ||
                (signal.Action != "offer" && signal.Action != "answer" &&
                 signal.Action != "hangup" && signal.Action != "reject")) return;
            Action<string, CallSignal> handler = CallSignalReceived;
            if (handler != null) PostUi(delegate { handler(peerId, signal); });
        }

        private void HandleText(PeerSession session, string peerId, WireEnvelope env)
        {
            ChatMessage msg = ChatMessage.FromDict(env.Payload());
            if (msg == null || string.IsNullOrEmpty(msg.Id)) return;
            msg.IsMine = false;
            msg.PeerId = peerId;
            msg.SenderId = peerId;
            msg.Status = DeliveryStatus.Delivered;
            RaiseMessageReceived(msg);
            SendReceipt(session, msg.Id, 2); // accusé ✓✓ automatique
        }

        private void HandleFileMeta(string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;
            long fs = MiniJson.AsLong(d, "fs", 0);
            string fn = MiniJson.AsString(d, "fn");
            long dur = MiniJson.AsLong(d, "dur", 0);
            long kind = MiniJson.AsLong(d, "k", -1);
            long ts = MiniJson.AsLong(d, "ts", 0);
            if (ts <= 0) ts = ChatMessage.NowUtcMs();
            string txt = MiniJson.AsString(d, "txt");
            string wf = MiniJson.AsString(d, "wf");
            string rep = MiniJson.AsString(d, "rep");
            string qa = MiniJson.AsString(d, "qa");
            string qt = MiniJson.AsString(d, "qt");
            string gid = MiniJson.AsString(d, "gid");
            if (gid != null && (!gid.StartsWith("grp_", StringComparison.Ordinal) || gid.Length > 80)) return;
            if (!_assembler.Begin(id, fn, fs)) return; // refusé (trop gros, doublon…)
            IncomingMeta meta = new IncomingMeta();
            meta.Id = id;
            meta.FileName = fn == null ? "" : fn;
            meta.FileSize = fs;
            meta.Dur = (int)dur;
            meta.Kind = (int)kind;
            meta.Ts = ts;
            meta.Text = txt == null ? "" : txt;
            meta.Waveform = wf;
            meta.ReplyToId = rep;
            meta.QuoteAuthor = qa;
            meta.QuoteText = qt;
            meta.GroupId = gid;
            lock (_transferLock)
            {
                _incoming[peerId + "|" + id] = meta;
            }
        }

        private void HandleFileChunk(WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            string ch = MiniJson.AsString(d, "ch");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(ch)) return;
            _assembler.Add(id, ch);
        }

        private void HandleFileDone(PeerSession session, string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;

            IncomingMeta meta = null;
            lock (_transferLock)
            {
                IncomingMeta found;
                if (_incoming.TryGetValue(peerId + "|" + id, out found))
                {
                    _incoming.Remove(peerId + "|" + id);
                    meta = found;
                }
            }
            if (meta == null) return;

            string path = _assembler.Complete(id, meta.Ts, peerId);
            if (path == null) return;

            ChatMessage msg = new ChatMessage();
            msg.Id = id;
            if (meta.Kind >= (int)MessageKind.Text && meta.Kind <= (int)MessageKind.Image)
            {
                msg.Kind = (MessageKind)meta.Kind;
            }
            else
            {
                msg.Kind = DetectKind(meta.FileName, meta.Dur);
            }
            msg.SenderId = peerId;
            msg.PeerId = string.IsNullOrEmpty(meta.GroupId) ? peerId : meta.GroupId;
            msg.IsMine = false;
            msg.Text = meta.Text;
            msg.TimestampUtc = meta.Ts;
            msg.FileName = meta.FileName;
            msg.FileSize = meta.FileSize;
            msg.LocalPath = path;
            msg.VoiceDurationSec = meta.Dur;
            msg.Waveform = meta.Waveform;
            msg.ReplyToId = meta.ReplyToId;
            msg.QuoteAuthor = meta.QuoteAuthor;
            msg.QuoteText = meta.QuoteText;
            msg.Status = DeliveryStatus.Delivered;
            RaiseMessageReceived(msg);
            SendReceipt(session, id, 2);
        }

        /// <summary>Kind d'un fichier reçu : Image si png/jpg/jpeg/gif/bmp, sinon File.</summary>
        private static MessageKind DetectKind(string fileName, int dur)
        {
            string ext = Path.GetExtension(fileName == null ? "" : fileName).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp")
            {
                return MessageKind.Image;
            }
            if (dur > 0) return MessageKind.Voice;
            return MessageKind.File;
        }

        /// <summary>Réaction d'un pair sur un de mes messages : {id, r}.</summary>
        private void HandleReaction(string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;
            string emoji = MiniJson.AsString(d, "r", "");
            RaiseReactionReceived(peerId, id, emoji);
        }

        private void RaiseReactionReceived(string peerId, string messageId, string emoji)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                Action<string, string, string> h = ReactionReceived;
                if (h != null) h(peerId, messageId, emoji);
            });
        }

        /// <summary>Édition d'un message : {id, txt}.</summary>
        private void HandleEdit(string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;
            string txt = MiniJson.AsString(d, "txt", "");
            RaiseMessageEdited(peerId, id, txt);
        }

        private void RaiseMessageEdited(string peerId, string messageId, string newText)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                Action<string, string, string> h = MessageEdited;
                if (h != null) h(peerId, messageId, newText);
            });
        }

        /// <summary>Suppression pour tous : {id}.</summary>
        private void HandleDeleteForAll(string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;
            RaiseMessageDeletedForAll(peerId, id);
        }

        private void RaiseMessageDeletedForAll(string peerId, string messageId)
        {
            if (_disposed) return;
            PostUi(delegate
            {
                if (_disposed) return;
                Action<string, string> h = MessageDeletedForAll;
                if (h != null) h(peerId, messageId);
            });
        }

        /// <summary>Message texte de groupe : PeerId (id du groupe) est préservé, SenderId = expéditeur réel.</summary>
        private void HandleGroupText(PeerSession session, string senderId, WireEnvelope env)
        {
            ChatMessage msg = ChatMessage.FromDict(env.Payload());
            if (msg == null || string.IsNullOrEmpty(msg.Id)) return;
            if (string.IsNullOrEmpty(msg.PeerId) || !msg.PeerId.StartsWith("grp_", StringComparison.Ordinal)) return;
            msg.IsMine = false;
            msg.SenderId = senderId;
            msg.Status = DeliveryStatus.Delivered;
            RaiseMessageReceived(msg);
            SendReceipt(session, msg.Id, 2); // accusé ✓✓ automatique
        }

        /// <summary>Annonce de groupe : {gid, name, members[]}.</summary>
        private void HandleGroupMeta(string senderId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string gid = MiniJson.AsString(d, "gid");
            if (string.IsNullOrEmpty(gid) || !gid.StartsWith("grp_", StringComparison.Ordinal) || gid.Length > 80) return;
            string name = MiniJson.AsString(d, "name");
            List<string> members = new List<string>();
            List<object> arr = MiniJson.AsList(d, "members");
            if (arr != null)
            {
                foreach (object o in arr)
                {
                    string s = MiniJson.AsString(o);
                    if (!string.IsNullOrEmpty(s) && !members.Contains(s) && members.Count < 64) members.Add(s);
                }
            }
            if (!members.Contains(senderId) || !members.Contains(_self.Id)) return;
            RaiseGroupMetaReceived(senderId, gid, name == null ? "" : name, members);
        }

        private void RaiseGroupMetaReceived(string senderId, string gid, string name, List<string> members)
        {
            if (_disposed) return;
            List<string> snapshot = new List<string>(members);
            PostUi(delegate
            {
                if (_disposed) return;
                Action<string, string, string, List<string>> h = GroupMetaReceived;
                if (h != null) h(senderId, gid, name, snapshot);
            });
        }

        /// <summary>Statut/story reçu : {id, snd, txt, bg, ts}.</summary>
        private void HandleStatus(string senderId, WireEnvelope env)
        {
            StatusPost st = StatusPost.FromDict(env.Payload());
            if (st == null || string.IsNullOrEmpty(st.Id)) return;
            st.SenderId = senderId;
            RaiseStatusReceived(senderId, st);
        }

        private void RaiseStatusReceived(string senderId, StatusPost st)
        {
            if (_disposed || st == null) return;
            StatusPost snapshot = st;
            PostUi(delegate
            {
                if (_disposed) return;
                Action<string, StatusPost> h = StatusReceived;
                if (h != null) h(senderId, snapshot);
            });
        }

        private void HandleReceipt(string peerId, WireEnvelope env)
        {
            Dictionary<string, object> d = env.Payload();
            string id = MiniJson.AsString(d, "id");
            if (string.IsNullOrEmpty(id)) return;
            long st = MiniJson.AsLong(d, "st", 0);
            if (st < 1 || st > 4) return;
            ChatMessage m = new ChatMessage();
            m.Id = id;
            m.PeerId = peerId;
            m.SenderId = _self.Id; // c'était mon message à l'origine
            m.IsMine = true;
            m.Status = (DeliveryStatus)st;
            RaiseMessageStatusChanged(m); // clone : Id + PeerId + Status + SenderId
        }

        private void HandleGoodbye(PeerSession session, string peerId)
        {
            PeerState st = LookupState(peerId);
            if (st != null)
            {
                lock (st.Sync)
                {
                    if (st.Session == session) st.Session = null;
                }
            }
            session.Close();
            if (st != null)
            {
                bool changed = false;
                lock (st.Sync)
                {
                    if (st.Online)
                    {
                        st.Online = false; // Goodbye → Offline
                        if (st.Info != null) st.Info.Online = false;
                        changed = true;
                    }
                }
                if (changed) RaisePeerListChanged();
            }
        }

        private void SendReceipt(PeerSession session, string messageId, int status)
        {
            if (session == null || string.IsNullOrEmpty(messageId)) return;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["id"] = messageId;
            d["st"] = status;
            WireEnvelope env = WireEnvelope.Create(WireType.Receipt, _self.Id, MiniJson.Serialize(d));
            try { session.SendEnvelope(env); } catch (Exception) { }
        }

        public Task<bool> SendTextAsync(string peerId, ChatMessage msg)
        {
            return SendTextCoreAsync(peerId, msg, WireType.TextMessage);
        }

        /// <summary>Message texte de groupe à un membre (PeerId du message = id du groupe).</summary>
        public Task<bool> SendGroupTextAsync(string memberId, ChatMessage msg)
        {
            return SendTextCoreAsync(memberId, msg, WireType.GroupText);
        }

        private async Task<bool> SendTextCoreAsync(string peerId, ChatMessage msg, WireType type)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || msg == null || string.IsNullOrEmpty(msg.Id))
            {
                return false;
            }
            PeerState st = LookupState(peerId);
            if (st == null) return false;

            await st.Gate.WaitAsync().ConfigureAwait(false); // séquenceur du pair
            try
            {
                PeerSession s = await EnsureSessionAsync(st).ConfigureAwait(false);
                if (s == null) return false;
                string payload = MiniJson.Serialize(msg.ToDict());
                WireEnvelope env = WireEnvelope.Create(type, _self.Id, payload);
                bool ok = s.SendEnvelope(env);
                if (ok) RaiseStatusChanged(msg.Id, peerId, DeliveryStatus.Sent);
                return ok;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                st.Gate.Release();
            }
        }

        public Task<bool> SendVoiceAsync(string peerId, ChatMessage msg)
        {
            return SendTransferAsync(peerId, msg, true);
        }

        public Task<bool> SendFileAsync(string peerId, ChatMessage msg)
        {
            return SendTransferAsync(peerId, msg, false);
        }

        /// <summary>
        /// Envoie un vocal/fichier : FileMeta {id,fn,fs,dur?,k,ts} puis des morceaux
        /// de 65536 octets (base64 dans le JSON) puis FileDone {id}.
        /// </summary>
        private async Task<bool> SendTransferAsync(string peerId, ChatMessage msg, bool voice)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || msg == null || string.IsNullOrEmpty(msg.Id))
            {
                return false;
            }
            string path = msg.LocalPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            PeerState st = LookupState(peerId);
            if (st == null) return false;

            await st.Gate.WaitAsync().ConfigureAwait(false); // séquenceur du pair
            try
            {
                PeerSession s = await EnsureSessionAsync(st).ConfigureAwait(false);
                if (s == null) return false;

                string fileName = !string.IsNullOrEmpty(msg.FileName) ? msg.FileName : Path.GetFileName(path);
                long size = 0;
                try { size = new FileInfo(path).Length; } catch (Exception) { }
                MessageKind kind = voice ? MessageKind.Voice : KindForFileName(fileName);

                Dictionary<string, object> meta = new Dictionary<string, object>();
                meta["id"] = msg.Id;
                meta["fn"] = fileName;
                meta["fs"] = size;
                meta["k"] = (int)kind;
                meta["ts"] = msg.TimestampUtc > 0 ? msg.TimestampUtc : ChatMessage.NowUtcMs();
                if (voice) meta["dur"] = msg.VoiceDurationSec;
                if (!string.IsNullOrEmpty(msg.Text)) meta["txt"] = msg.Text;
                if (!string.IsNullOrEmpty(msg.Waveform)) meta["wf"] = msg.Waveform;
                if (!string.IsNullOrEmpty(msg.ReplyToId)) meta["rep"] = msg.ReplyToId;
                if (!string.IsNullOrEmpty(msg.QuoteAuthor)) meta["qa"] = msg.QuoteAuthor;
                if (!string.IsNullOrEmpty(msg.QuoteText)) meta["qt"] = msg.QuoteText;
                if (msg.PeerId != null && msg.PeerId.StartsWith("grp_", StringComparison.Ordinal)) meta["gid"] = msg.PeerId;
                if (!s.SendEnvelope(WireEnvelope.Create(WireType.FileMeta, _self.Id, MiniJson.Serialize(meta))))
                {
                    return false;
                }

                byte[] chunkBuf = new byte[FileAssembler.ChunkBytes];
                using (FileStream fileStream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    int n = fileStream.Read(chunkBuf, 0, chunkBuf.Length);
                    while (n > 0)
                    {
                        Dictionary<string, object> chunk = new Dictionary<string, object>();
                        chunk["id"] = msg.Id;
                        chunk["ch"] = Convert.ToBase64String(chunkBuf, 0, n);
                        if (!s.SendEnvelope(WireEnvelope.Create(
                            WireType.FileChunk, _self.Id, MiniJson.Serialize(chunk))))
                        {
                            return false;
                        }
                        n = fileStream.Read(chunkBuf, 0, chunkBuf.Length);
                    }
                }

                Dictionary<string, object> done = new Dictionary<string, object>();
                done["id"] = msg.Id;
                if (!s.SendEnvelope(WireEnvelope.Create(WireType.FileDone, _self.Id, MiniJson.Serialize(done))))
                {
                    return false;
                }

                RaiseStatusChanged(msg.Id, peerId, DeliveryStatus.Sent);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                st.Gate.Release();
            }
        }

        private static MessageKind KindForFileName(string fileName)
        {
            string ext = Path.GetExtension(fileName == null ? "" : fileName).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp")
            {
                return MessageKind.Image;
            }
            return MessageKind.File;
        }

        public Task<bool> SendTypingAsync(string peerId)
        {
            if (_disposed || string.IsNullOrEmpty(peerId)) return TaskFalse;
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            bool send = false;
            lock (st.Sync)
            {
                long now = ChatMessage.NowUtcMs();
                if (now - st.LastTypingSentUtcMs >= TypingThrottleMs)
                {
                    st.LastTypingSentUtcMs = now;
                    send = true;
                }
            }
            if (!send) return TaskTrue; // bridé : 1 / 2,5 s et par pair
            PeerSession s = st.Session;
            if (s == null || s.IsClosed || !s.IsHandshakeComplete) return TaskTrue; // pas de session : ignoré
            WireEnvelope env = WireEnvelope.Create(WireType.Typing, _self.Id, "{}");
            return Task.FromResult(s.SendEnvelope(env));
        }

        /// <summary>Marque un message reçu comme lu : envoie Receipt {id, st=3} (✓✓ bleu).</summary>
        public Task MarkReadAsync(string peerId, string messageId)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(messageId)) return TaskDone;
            PeerState st = LookupState(peerId);
            if (st == null) return TaskDone;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return;
                SendReceipt(s, messageId, 3);
            });
        }

        public Task<bool> SendGoodbyeAsync(string peerId)
        {
            if (_disposed || string.IsNullOrEmpty(peerId)) return TaskFalse;
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed) return true; // rien à faire
                try
                {
                    s.SendEnvelope(WireEnvelope.Create(WireType.Goodbye, _self.Id, "{}"));
                }
                catch (Exception) { }
                HandleLocalGoodbye(st, s);
                return true;
            });
        }

        private void HandleLocalGoodbye(PeerState st, PeerSession s)
        {
            lock (st.Sync)
            {
                if (st.Session == s) st.Session = null;
                if (st.Online)
                {
                    st.Online = false;
                    if (st.Info != null) st.Info.Online = false;
                }
            }
            s.Close();
            RaisePeerListChanged();
        }

        /// <summary>Réagit à un message (emoji vide = retire la réaction).</summary>
        public Task<bool> SendReactionAsync(string peerId, string messageId, string emoji)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(messageId))
            {
                return TaskFalse;
            }
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return false;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["id"] = messageId;
                d["r"] = emoji == null ? "" : emoji;
                WireEnvelope env = WireEnvelope.Create(WireType.Reaction, _self.Id,
                    MiniJson.Serialize(d));
                try { return s.SendEnvelope(env); }
                catch (Exception) { return false; }
            });
        }

        /// <summary>Modifie un message déjà envoyé (texte).</summary>
        public Task<bool> SendEditAsync(string peerId, string messageId, string newText)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(messageId))
            {
                return TaskFalse;
            }
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return false;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["id"] = messageId;
                d["txt"] = newText == null ? "" : newText;
                WireEnvelope env = WireEnvelope.Create(WireType.EditMessage, _self.Id,
                    MiniJson.Serialize(d));
                try { return s.SendEnvelope(env); }
                catch (Exception) { return false; }
            });
        }

        /// <summary>Supprime un message pour tous les participants.</summary>
        public Task<bool> SendDeleteForAllAsync(string peerId, string messageId)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || string.IsNullOrEmpty(messageId))
            {
                return TaskFalse;
            }
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return false;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["id"] = messageId;
                WireEnvelope env = WireEnvelope.Create(WireType.DeleteForAll, _self.Id,
                    MiniJson.Serialize(d));
                try { return s.SendEnvelope(env); }
                catch (Exception) { return false; }
            });
        }

        /// <summary>Annonce les métadonnées d'un groupe à un membre.</summary>
        public Task<bool> SendGroupMetaAsync(string memberId, string groupId, string groupName, List<string> memberIds)
        {
            if (_disposed || string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(groupId))
            {
                return TaskFalse;
            }
            PeerState st = LookupState(memberId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return false;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["gid"] = groupId;
                d["name"] = groupName == null ? "" : groupName;
                List<object> members = new List<object>();
                if (memberIds != null)
                {
                    foreach (string m in memberIds) members.Add(m);
                }
                d["members"] = members;
                WireEnvelope env = WireEnvelope.Create(WireType.GroupMeta, _self.Id,
                    MiniJson.Serialize(d));
                try { return s.SendEnvelope(env); }
                catch (Exception) { return false; }
            });
        }

        /// <summary>Envoie un statut (story) à un pair.</summary>
        public Task<bool> SendStatusAsync(string peerId, StatusPost status)
        {
            if (_disposed || string.IsNullOrEmpty(peerId) || status == null) return TaskFalse;
            PeerState st = LookupState(peerId);
            if (st == null) return TaskFalse;
            return Task.Run(delegate
            {
                PeerSession s = st.Session;
                if (s == null || s.IsClosed || !s.IsHandshakeComplete) return false;
                WireEnvelope env = WireEnvelope.Create(WireType.StatusPost, _self.Id,
                    MiniJson.Serialize(status.ToDict()));
                try { return s.SendEnvelope(env); }
                catch (Exception) { return false; }
            });
        }

        public async Task<bool> SendCallSignalAsync(string peerId, CallSignal signal)
        {
            if (string.IsNullOrEmpty(peerId) || signal == null || string.IsNullOrEmpty(signal.CallId)) return false;
            PeerState st;
            lock (_sync) { if (!_states.TryGetValue(peerId, out st)) return false; }
            try
            {
                PeerSession s = await EnsureSessionAsync(st).ConfigureAwait(false);
                return s != null && s.SendEnvelope(WireEnvelope.Create(
                    WireType.CallSignal, _self.Id, MiniJson.Serialize(signal.ToDict())));
            }
            catch (Exception) { return false; }
        }

        /// <summary>Nouvelle vague de découverte immédiate (bouton « réessayer »).</summary>
        public void AnnounceNow()
        {
            UdpBeacon b = _beacon;
            if (b != null)
            {
                try { b.RefreshAndAnnounce(); } catch (Exception) { }
            }
        }

        /// <summary>Retire une session qui n'a pas complété son identification.</summary>
        private void ClearUnregistered(PeerSession s)
        {
            lock (_sync)
            {
                _unidentified.Remove(s);
            }
        }

        /// <summary>
        /// Connexion directe « Joindre via IP » : résout l'hôte, se connecte en TCP,
        /// poignée de main chiffrée puis renvoie l'id du pair (null si échec).
        /// </summary>
        public async Task<string> ConnectToAsync(string host, int tcpPort)
        {
            if (_disposed || string.IsNullOrEmpty(host) || tcpPort <= 0 || tcpPort > 65535)
            {
                return null;
            }
            IPAddress ip = null;
            IPAddress tryIp;
            if (IPAddress.TryParse(host, out tryIp))
            {
                ip = tryIp;
            }
            else
            {
                try
                {
                    foreach (IPAddress a in await Dns.GetHostAddressesAsync(host).ConfigureAwait(false))
                    {
                        if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                    }
                }
                catch (Exception) { }
            }
            if (ip == null) return null;

            TcpClient tcp = null;
            PeerSession session = null;
            try
            {
                tcp = new TcpClient();
                tcp.NoDelay = true;
                Task connectTask = tcp.ConnectAsync(ip, tcpPort);
                bool connected = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs))
                    .ConfigureAwait(false) == connectTask;
                if (connectTask.IsFaulted || connectTask.IsCanceled)
                {
                    AggregateException observed = connectTask.Exception;
                }
                if (!connected || !tcp.Connected)
                {
                    try { tcp.Close(); } catch (Exception) { }
                    return null;
                }
                session = new PeerSession(tcp, true, _self);
                HookSession(session);
                session.Start();
                bool ok = await session.WaitHandshakeAsync(HandshakeWaitMs).ConfigureAwait(false);
                if (!ok || session.IsClosed || session.RemotePeer == null)
                {
                    ClearUnregistered(session);
                    session.Close();
                    return null;
                }
                return session.RemotePeer.Id;
            }
            catch (Exception)
            {
                if (session != null)
                {
                    ClearUnregistered(session);
                    try { session.Close(); } catch (Exception) { }
                }
                else if (tcp != null)
                {
                    try { tcp.Close(); } catch (Exception) { }
                }
                return null;
            }
        }

        /// <summary>Résumé réseau du panneau Paramètres → Réseau.</summary>
        public string DiagnosticSummary
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("IP locales : ");
                try
                {
                    bool first = true;
                    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up) continue;
                        IPInterfaceProperties props = nic.GetIPProperties();
                        if (props == null) continue;
                        foreach (UnicastIPAddressInformation u in props.UnicastAddresses)
                        {
                            if (u.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                            if (IPAddress.IsLoopback(u.Address)) continue;
                            if (!first) sb.Append(", ");
                            sb.Append(u.Address);
                            first = false;
                        }
                    }
                    if (first) sb.Append("aucune");
                }
                catch (Exception) { sb.Append("?"); }
                sb.AppendLine();
                sb.Append("Découverte UDP : port ").Append(_udpPort);
                UdpBeacon b = _beacon;
                sb.AppendLine(b != null
                    ? " — " + b.InterfaceCount + " interface(s) servie(s)"
                    : " — désactivée");
                if (!string.IsNullOrEmpty(_discoveryError))
                    sb.Append("Erreur de découverte : ").AppendLine(_discoveryError);
                sb.Append("Serveur TCP : port ");
                sb.Append(_host != null ? _host.ActualPort.ToString() : "?");
                sb.AppendLine();
                sb.Append("Pairs en ligne : ").Append(((System.Collections.ICollection)Peers).Count).AppendLine();
                sb.Append("Chiffrement : ECDH P-256 + AES-256 + HMAC-SHA256 (par session)");
                return sb.ToString();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _started = false;

            Timer t = _cleanupTimer;
            _cleanupTimer = null;
            if (t != null)
            {
                try { t.Dispose(); } catch (Exception) { }
            }

            if (_beacon != null)
            {
                try { _beacon.Dispose(); } catch (Exception) { }
            }
            if (_host != null)
            {
                try { _host.Dispose(); } catch (Exception) { }
            }

            List<PeerSession> all = new List<PeerSession>();
            lock (_sync)
            {
                for (int i = 0; i < _unidentified.Count; i++) all.Add(_unidentified[i]);
                _unidentified.Clear();
                foreach (KeyValuePair<string, PeerState> kv in _states)
                {
                    PeerSession s = kv.Value.Session;
                    if (s != null && !all.Contains(s)) all.Add(s);
                    kv.Value.Session = null;
                }
                _states.Clear();
            }
            lock (_transferLock) { _incoming.Clear(); }

            for (int i = 0; i < all.Count; i++)
            {
                try { all[i].Close(); } catch (Exception) { }
            }
            try { _assembler.Dispose(); } catch (Exception) { }
        }
    }
}
