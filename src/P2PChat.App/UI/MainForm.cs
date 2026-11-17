using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using P2PChat.App.Audio;
using P2PChat.App.Controls;
using P2PChat.App.Features;
using P2PChat.App.Profile;
using P2PChat.App.Settings;
using P2PChat.App.Theme;
using P2PChat.Core.Interfaces;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Fenêtre principale « WhatsApp Desktop » : tout est dessiné à la main
    /// (sidebar + fil de discussion + composeur) avec les tokens exacts de la
    /// vraie application. Fichiers partiels :
    ///   MainForm.Layout   — géométrie des zones
    ///   MainForm.Sidebar  — liste des discussions (peinture + souris)
    ///   MainForm.Chat     — fil de messages, bulles, volets (peinture + souris)
    ///   MainForm.Composer — saisie, emojis, pièces jointes, vocal
    ///   MainForm.Menus    — menus contextuels et boîtes de dialogue
    ///   MainForm.Net      — branchement du moteur réseau
    /// </summary>
    public sealed partial class MainForm : Form
    {
        // ---------------------------------------------------------------- services
        private readonly IChatNetwork _net;
        private readonly IChatHistory _history;
        private readonly PeerInfo _self;
        internal readonly AppSettings Cfg;
        private readonly Toasts _toasts;
        private readonly VoiceRecorder _rec = new VoiceRecorder();
        private readonly VoicePlayer _player = new VoicePlayer();

        // ---------------------------------------------------------------- données
        private readonly Dictionary<string, Conversation> _convs =
            new Dictionary<string, Conversation>();
        private readonly HashSet<string> _textSendInFlight = new HashSet<string>();
        private readonly HashSet<string> _selectedMessageIds = new HashSet<string>();
        private bool _selectMessagesMode;
        private readonly List<string> _order = new List<string>();
        private string _selectedId;
        private string _hoverRowId;
        private string _searchQuery = "";
        private bool _unreadFilter;
        private bool _favoritesFilter;

        // ---------------------------------------------------------------- contrôles réels
        private TextBox _searchBox;   // recherche sidebar
        private TextBox _input;       // saisie du message
        private Panel _inputHost;     // pilule blanche du composeur

        // ---------------------------------------------------------------- états UI
        private readonly System.Windows.Forms.Timer _uiTimer = new System.Windows.Forms.Timer();
        private bool _emojiOpen;
        private int _emojiCat;
        private int _emojiScroll;
        private string _chatSearchQuery = "";
        private bool _chatSearchOpen;
        private TextBox _chatSearchBox;
        private int _listScroll;      // px, liste des discussions
        private int _chatScroll;      // px, fil de la discussion active
        private bool _dragSplitter;
        private int _dragSplitterStartX;
        private int _dragSplitterStartW;
        private Point _lastMouse = new Point(-999, -999);
        private string _hoverActionMsg;   // id du message dont le menu d'action est ouvert
        private int _hoverVoiceProg = -1; // progression survolée (0..1000)
        private string _hoverVoiceId;
        private bool _recording;
        private string _recPath;
        private readonly List<int> _recWave = new List<int>();
        private DateTime _lastTypingSent = DateTime.MinValue;
        private bool _closing;
        private int _sidebarW = 340;
        private int _activeNav;
        private int _hoverNav = -1;

        // ---------------------------------------------------------------- constantes de mise en page (px @96dpi)
        internal const int HeaderH = 72;
        internal const int RowH = 92;
        internal const int ComposerMinH = 62;
        internal const int EmojiH = 330;
        internal const int ReplyH = 58;

        public MainForm(IChatNetwork net, IChatHistory history, PeerInfo self, AppSettings cfg)
        {
            _net = net;
            _history = history;
            _self = self;
            Cfg = cfg ?? AppSettings.Load();

            WaTheme.ApplyMode(Cfg.ThemeMode);
            WaTheme.FontStep = Cfg.FontStepInt;

            Text = "P2PChat";
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            UpdateStyles();
            BackColor = WaTheme.SidebarBg;
            MinimumSize = new Size(900, 560);

            try
            {
                string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whatsapp.ico");
                if (File.Exists(ico)) Icon = new Icon(ico);
            }
            catch { }

            // géométrie mémorisée
            int w = Cfg.WindowW >= 900 ? Cfg.WindowW : 1180;
            int h = Cfg.WindowH >= 560 ? Cfg.WindowH : 760;
            ClientSize = new Size(w, h);
            StartPosition = FormStartPosition.CenterScreen;
            if (Cfg.WindowMax) WindowState = FormWindowState.Maximized;
            if (Cfg.SidebarWidth >= 280 && Cfg.SidebarWidth <= w - 420) _sidebarW = Cfg.SidebarWidth;

            _toasts = new Toasts(this);
            _toasts.Activated += OpenConversation;

            BuildControls();
            WireNetwork();

            _uiTimer.Interval = 200;
            _uiTimer.Tick += OnUiTick;
            _uiTimer.Start();

            _player.PlaybackDone += OnPlaybackDone;

            LoadConversationsFromPeers();
            LoadGroups();
            RebuildOrder();
        }

        // ---------------------------------------------------------------- contrôles
        private void BuildControls()
        {
            // --- recherche sidebar (vrai TextBox pour l'IME/le caret)
            _searchBox = new TextBox();
            _searchBox.BorderStyle = BorderStyle.None;
            _searchBox.Font = WaTheme.Text(10f);
            _searchBox.BackColor = WaTheme.SearchBg;
            _searchBox.ForeColor = WaTheme.TextDark;
            _searchBox.Font = WaTheme.Text(10f + Cfg.FontStepInt * 0.45f);
            _searchBox.TextChanged += delegate
            {
                _searchQuery = _searchBox.Text.Trim();
                _listScroll = 0;
                Invalidate(SidebarRect());
            };
            Controls.Add(_searchBox);

            // --- recherche dans la discussion
            _chatSearchBox = new TextBox();
            _chatSearchBox.BorderStyle = BorderStyle.None;
            _chatSearchBox.Font = WaTheme.Text(10f);
            _chatSearchBox.BackColor = WaTheme.SearchBg;
            _chatSearchBox.ForeColor = WaTheme.TextDark;
            _chatSearchBox.Font = WaTheme.Text(10f + Cfg.FontStepInt * 0.45f);
            _chatSearchBox.Visible = false;
            _chatSearchBox.TextChanged += delegate
            {
                _chatSearchQuery = _chatSearchBox.Text;
                RelayoutChat(true);
                Invalidate(ChatRect());
            };
            Controls.Add(_chatSearchBox);

            // --- pilule de saisie + TextBox multiligne
            _inputHost = new Panel();
            _inputHost.BackColor = WaTheme.ComposerPill;
            Controls.Add(_inputHost);

            _input = new TextBox();
            _input.Multiline = true;
            _input.BorderStyle = BorderStyle.None;
            _input.ScrollBars = ScrollBars.Vertical;
            _input.Font = WaTheme.Text(10.6f);
            _input.BackColor = WaTheme.ComposerPill;
            _input.ForeColor = WaTheme.TextDark;
            _input.Font = WaTheme.Text(10.6f + Cfg.FontStepInt * 0.75f);
            _input.TextChanged += OnInputTextChanged;
            _input.KeyDown += OnInputKeyDown;
            _inputHost.Controls.Add(_input);

            RelayoutControls();
        }

        /// <summary>Repositionne les vrais contrôles selon la géométrie courante.</summary>
        private void RelayoutControls()
        {
            if (_searchBox == null || _inputHost == null || _input == null || _chatSearchBox == null)
            {
                return; // contrôles pas encore construits (pendant le constructeur / OnResize précoce)
            }
            Rectangle side = SidebarRect();
            _searchBox.Bounds = new Rectangle(
                side.X + WaTheme.S(60), side.Y + HeaderH + WaTheme.S(15),
                side.Width - WaTheme.S(74), WaTheme.S(20));

            int composerH = ComposerHeight();
            Rectangle chat = ChatRect();
            int pillH = composerH - WaTheme.S(16);
            _inputHost.Bounds = new Rectangle(
                chat.X + WaTheme.S(120), chat.Bottom - composerH + WaTheme.S(7),
                chat.Width - WaTheme.S(240), pillH);
            _input.Bounds = new Rectangle(
                WaTheme.S(14), WaTheme.S(9),
                _inputHost.Width - WaTheme.S(26), _inputHost.Height - WaTheme.S(14));

            Rectangle sr = ChatSearchBarRect();
            _chatSearchBox.Bounds = new Rectangle(sr.X + WaTheme.S(40), sr.Y + WaTheme.S(11),
                sr.Width - WaTheme.S(60), WaTheme.S(20));
            _chatSearchBox.Visible = _activeNav == 0 && _chatSearchOpen && _selectedId != null;
            _searchBox.Visible = _activeNav == 0 || _activeNav == 1 || _activeNav == 4;
            _inputHost.Visible = (_activeNav == 0 || _activeNav == 4) && _selectedId != null;
            if (_chatSearchOpen) _chatSearchBox.BringToFront();
        }

        // ---------------------------------------------------------------- ticks
        private void OnUiTick(object sender, EventArgs e)
        {
            bool repaint = false;

            // « en train d'écrire… » expire
            Conversation c = Selected();
            if (c != null && !c.Typing && c.TypingUntil != DateTime.MinValue)
            {
                c.TypingUntil = DateTime.MinValue;
                repaint = true;
            }

            // progression du vocal en lecture
            if (_player.IsPlaying && !_player.IsPaused) repaint = true;

            // enregistrement : onde + chrono
            if (_recording)
            {
                _recWave.Add(_rec.PeakLevel);
                while (_recWave.Count > 400) _recWave.RemoveAt(0);
                if (_rec.ElapsedSeconds >= 120) FinishRecording(true); // sécurité 2 min
                repaint = true;
            }

            if (repaint) Invalidate();
        }

        // ---------------------------------------------------------------- helpers
        internal Conversation Selected()
        {
            if (_selectedId == null) return null;
            Conversation c;
            return _convs.TryGetValue(_selectedId, out c) ? c : null;
        }

        internal Conversation Conv(string peerId)
        {
            Conversation c;
            return _convs.TryGetValue(peerId, out c) ? c : null;
        }

        /// <summary>Ouvre (ou crée) la conversation avec ce pair.</summary>
        internal void OpenConversation(string peerId)
        {
            if (peerId == null) return;
            Conversation c = Conv(peerId);
            if (c == null)
            {
                PeerInfo p = FindPeer(peerId);
                if (p == null) return;
                c = CreateConversation(p);
            }
            if (_selectedId == peerId)
            {
                MarkConversationRead(c);
                return;
            }

            // sauvegarde le brouillon de la conversation quittée
            Conversation old = Selected();
            if (old != null) old.Draft = _input.Text;

            _selectedId = peerId;
            _emojiOpen = false;
            _chatSearchOpen = false;
            _chatSearchQuery = "";
            if (_recording) FinishRecording(false);
            StopPlayback();

            _input.Text = c.Draft ?? "";
            _input.SelectionStart = _input.Text.Length;

            LoadMessages(c);
            RetryPendingTextMessages(peerId);
            MarkConversationRead(c);
            RebuildOrder();
            RelayoutControls();
            RelayoutChat(false);
            Invalidate();
        }

        private PeerInfo FindPeer(string peerId)
        {
            foreach (PeerInfo p in _net.Peers)
            {
                if (p.Id == peerId) return p;
            }
            return null;
        }

        private Conversation CreateConversation(PeerInfo p)
        {
            Conversation c = new Conversation();
            c.PeerId = p.Id;
            c.PeerName = Cfg.ContactName(p.Id, p.Pseudo);
            c.PeerBio = p.Bio;
            c.Online = p.Online;
            _convs[p.Id] = c;
            return c;
        }

        /// <summary>Crée les conversations pour tous les pairs connus (découverte).</summary>
        private void LoadConversationsFromPeers()
        {
            foreach (PeerInfo p in _net.Peers)
            {
                if (p.Id == _self.Id) continue;
                Conversation c = Conv(p.Id);
                if (c == null) c = CreateConversation(p);
                ContactLocal contact = Cfg.Contact(p.Id);
                if (!string.IsNullOrEmpty(p.Pseudo)) contact.LastKnownName = p.Pseudo;
                c.PeerName = Cfg.ContactName(p.Id, p.Pseudo);
                c.PeerBio = p.Bio;
                c.Online = p.Online;
            }
            foreach (KeyValuePair<string, ContactLocal> kv in Cfg.Contacts)
            {
                if (string.IsNullOrEmpty(kv.Key) || Conv(kv.Key) != null) continue;
                PeerInfo offline = new PeerInfo();
                offline.Id = kv.Key;
                offline.Pseudo = Cfg.ContactName(kv.Key, kv.Value.LastKnownName);
                offline.Bio = "Hors ligne";
                CreateConversation(offline);
            }
        }

        /// <summary>Charge l'historique disque d'une conversation (une seule fois).</summary>
        private void LoadMessages(Conversation c)
        {
            if (c.Loaded) return;
            c.Loaded = true;
            try
            {
                List<ChatMessage> old = _history.Load(c.PeerId, 500);
                if (old != null)
                {
                    foreach (ChatMessage m in old)
                    {
                        if (!m.Deleted && FindLocal(c, m.Id) == null) c.Messages.Add(m);
                    }
                    c.Messages.Sort(delegate(ChatMessage a, ChatMessage b)
                    {
                        return a.TimestampUtc.CompareTo(b.TimestampUtc);
                    });
                }
            }
            catch { }
        }

        private static ChatMessage FindLocal(Conversation c, string id)
        {
            for (int i = 0; i < c.Messages.Count; i++)
            {
                if (c.Messages[i].Id == id) return c.Messages[i];
            }
            return null;
        }

        // ---------------------------------------------------------------- cycle de vie
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _closing = true;
            if (_audioCall != null) EndAudioCall(true, null);
            Conversation old = Selected();
            if (old != null) old.Draft = _input.Text;
            try
            {
                Cfg.SidebarWidth = _sidebarW;
                if (WindowState == FormWindowState.Maximized)
                {
                    Cfg.WindowMax = true;
                    Cfg.WindowW = RestoreBounds.Width;
                    Cfg.WindowH = RestoreBounds.Height;
                }
                else
                {
                    Cfg.WindowMax = false;
                    Cfg.WindowW = ClientSize.Width;
                    Cfg.WindowH = ClientSize.Height;
                }
                Cfg.Save();
            }
            catch { }
            try { _uiTimer.Stop(); } catch { }
            try { if (_recording) _rec.Cancel(); } catch { }
            try { _rec.Dispose(); } catch { }
            try { _player.Dispose(); } catch { }
            try { _toasts.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_sidebarW > ClientSize.Width - NavigationRailWidth() - 420)
            {
                _sidebarW = Math.Max(280, ClientSize.Width - NavigationRailWidth() - 420);
            }
            RelayoutControls();
            Conversation c = Selected();
            if (c != null) c.InvalidateLayout();
            RelayoutChat(false);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            PaintNavigationRail(g);
            if (_activeNav == 0 || _activeNav == 4)
            {
                PaintSidebar(g);
                PaintSplitter(g);
                if (_selectedId != null)
                {
                    PaintChat(g);
                    PaintComposer(g);
                    if (_emojiOpen) PaintEmojiPanel(g);
                    if (_chatSearchOpen) PaintChatSearchBar(g);
                }
                else PaintEmptyState(g);
            }
            else if (_activeNav == 1) PaintCallsPage(g);
            else if (_activeNav == 2) PaintStatusPage(g);
            else if (_activeNav == 6) PaintSettingsPage(g);
            else if (_activeNav == 5) PaintProfilePage(g);
            else PaintNavigationPage(g);
            _toasts.Paint(g);
        }

        // ---------------------------------------------------------------- souris
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.Location;
            if (_dragSplitter)
            {
                int w = _dragSplitterStartW + (p.X - _dragSplitterStartX);
                _sidebarW = Math.Max(280, Math.Min(ClientSize.Width - NavigationRailWidth() - 420, w));
                RelayoutControls();
                Conversation c = Selected();
                if (c != null) c.InvalidateLayout();
                RelayoutChat(false);
                Invalidate();
                return;
            }
            if (_lastMouse == p) return;
            _lastMouse = p;

            Rectangle side = SidebarRect();
            if (!NavigationRailRect().Contains(p) && _hoverNav != -1)
            {
                _hoverNav = -1;
                Invalidate(NavigationRailRect());
            }
            if (NavigationRailRect().Contains(p))
            {
                int nav = NavigationHitTest(p);
                if (nav != _hoverNav) { _hoverNav = nav; Invalidate(NavigationRailRect()); }
                Cursor = nav >= 0 ? Cursors.Hand : Cursors.Default;
            }
            else if (side.Contains(p))
            {
                if (_activeNav == 1) CallsPageMouseMove(p);
                else
                {
                    SidebarMouseMove(p);
                    Cursor = SplitterHit(p) ? Cursors.SizeWE : Cursors.Default;
                }
            }
            else if (SplitterHit(p))
            {
                Cursor = Cursors.SizeWE;
            }
            else
            {
                if (_hoverRowId != null)
                {
                    _hoverRowId = null;
                    Invalidate(side);
                }
                if (_activeNav == 1) CallsPageMouseMove(p);
                else ChatMouseMove(p);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point p = e.Location;
            if (_toasts.Hit(p)) return;
            if (SplitterHit(p) && e.Button == MouseButtons.Left)
            {
                _dragSplitter = true;
                _dragSplitterStartX = p.X;
                _dragSplitterStartW = _sidebarW;
                return;
            }
            if (NavigationRailRect().Contains(p) && e.Button == MouseButtons.Left)
            {
                int nav = NavigationHitTest(p);
                if (nav >= 0)
                {
                    _activeNav = nav;
                    _unreadFilter = false;
                    _favoritesFilter = false;
                    _listScroll = 0;
                    if (nav == 1)
                    {
                        _searchQuery = "";
                        _searchBox.Clear();
                    }
                    RelayoutControls();
                    Invalidate(NavigationRailRect());
                    Invalidate();
                }
            }
            else if (_activeNav == 5 && e.Button == MouseButtons.Left) ProfileMouseDown(p);
            else if (SidebarRect().Contains(p)) SidebarMouseDown(p, e.Button);
            else if (_activeNav == 1) CallsPageMouseDown(p, e.Button);
            else ChatMouseDown(p, e.Button);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragSplitter)
            {
                _dragSplitter = false;
                Cursor = Cursors.Default;
                return;
            }
            Point p = e.Location;
            if (_toasts.Hit(p))
            {
                _toasts.Click();
                return;
            }
            if (NavigationRailRect().Contains(p)) return;
            if (SidebarRect().Contains(p)) SidebarMouseUp(p, e.Button);
            else if (_activeNav == 1) CallsPageMouseUp(p, e.Button);
            else ChatMouseUp(p, e.Button);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _lastMouse = new Point(-999, -999);
            if (_activeNav == 1 && (_hoverCallPeerId != null || _hoverCallButton))
            {
                _hoverCallPeerId = null;
                _hoverCallButton = false;
                Invalidate();
            }
            if (_hoverRowId != null)
            {
                _hoverRowId = null;
                Invalidate(SidebarRect());
            }
            Conversation c = Selected();
            if (c != null && c.HoverMsgId != null)
            {
                c.HoverMsgId = null;
                Invalidate(ChatRect());
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Point p = PointToClient(Cursor.Position);
            int delta = e.Delta;
            if (SidebarRect().Contains(p))
            {
                int max = _activeNav == 1 ? CallsListScrollMax() : ListScrollMax();
                _listScroll = Math.Max(0, Math.Min(max, _listScroll - delta / 2));
                Invalidate(SidebarRect());
            }
            else if (_selectedId != null && ChatRect().Contains(p))
            {
                if (_emojiOpen && EmojiRect().Contains(p))
                {
                    _emojiScroll = Math.Max(0, _emojiScroll - delta / 2);
                    Invalidate(EmojiRect());
                    return;
                }
                Conversation c = Selected();
                if (c != null)
                {
                    RelayoutChat(false);
                    int max = ChatScrollMax(c);
                    int oldScroll = _chatScroll;
                    _chatScroll = Math.Max(0, Math.Min(max, _chatScroll - delta / 2));
                    c.AtBottom = _chatScroll >= max - 2;
                    if (oldScroll != _chatScroll) Invalidate(ChatRect());
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _selectMessagesMode)
            {
                _selectedMessageIds.Clear();
                _selectMessagesMode = false;
                Invalidate(StreamRect());
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------------------------------------------------------- thème
        internal void ApplyThemeChange()
        {
            WaTheme.ApplyMode(Cfg.ThemeMode);
            WaTheme.FontStep = Cfg.FontStepInt;
            BackColor = WaTheme.SidebarBg;
            _searchBox.BackColor = WaTheme.SearchBg;
            _searchBox.ForeColor = WaTheme.TextDark;
            _searchBox.Font = WaTheme.Text(10f + Cfg.FontStepInt * 0.45f);
            _chatSearchBox.BackColor = WaTheme.SearchBg;
            _chatSearchBox.ForeColor = WaTheme.TextDark;
            _chatSearchBox.Font = WaTheme.Text(10f + Cfg.FontStepInt * 0.45f);
            _inputHost.BackColor = WaTheme.ComposerPill;
            _input.BackColor = WaTheme.ComposerPill;
            _input.ForeColor = WaTheme.TextDark;
            _input.Font = WaTheme.Text(10.6f + Cfg.FontStepInt * 0.75f);
            WaWallpaper.Invalidate();
            foreach (KeyValuePair<string, Conversation> kv in _convs)
            {
                kv.Value.InvalidateLayout();
            }
            RelayoutChat(false);
            Invalidate();
        }
    }
}
