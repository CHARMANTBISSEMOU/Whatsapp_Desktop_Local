using System;
using System.Drawing;
using System.Windows.Forms;

namespace P2PChat.App.UI
{
    internal sealed class AudioCallDialog : Form
    {
        private readonly Label _state;
        private readonly Button _mute;
        private readonly Button _hangup;
        private readonly PictureBox _mainVideo;
        private readonly PictureBox _thumbnailVideo;
        private readonly Panel _thumbnail;
        private readonly Label _mainCaption;
        private readonly Label _thumbnailCaption;
        private readonly Label _mainPlaceholder;
        private readonly Label _thumbnailPlaceholder;
        private readonly Label _videoStatus;
        private readonly Panel _videoStage;
        private readonly Panel _controls;
        private readonly string _peerName;
        private readonly bool _video;
        private Bitmap _remoteFrame;
        private Bitmap _localFrame;
        private bool _localIsMain;
        private bool _closed;

        internal event Action HangupRequested;
        internal event Action MuteToggled;
        internal bool Muted { get; private set; }

        internal AudioCallDialog(string peerName, bool incoming, bool video)
        {
            _peerName = string.IsNullOrWhiteSpace(peerName) ? "Contact" : peerName;
            _video = video;
            Text = incoming ? (video ? "Appel vidéo entrant" : "Appel audio entrant") : (video ? "Appel vidéo" : "Appel audio");
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = video;
            BackColor = Color.FromArgb(248, 249, 250);
            MinimizeBox = true;

            if (video)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                ClientSize = new Size(980, 700);
                MinimumSize = new Size(640, 480);

                Label name = new Label
                {
                    Text = _peerName,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(16, 8),
                    Size = new Size(460, 38),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left
                };

                _videoStage = new Panel
                {
                    BackColor = Color.FromArgb(12, 18, 22),
                    Location = new Point(14, 52),
                    Size = new Size(ClientSize.Width - 28, ClientSize.Height - 142),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };
                _mainVideo = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(12, 18, 22),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Cursor = Cursors.Default
                };
                _mainPlaceholder = new Label
                {
                    Text = "Connexion vidéo…",
                    ForeColor = Color.FromArgb(205, 215, 220),
                    BackColor = Color.FromArgb(12, 18, 22),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                _mainCaption = MakeVideoCaption();
                _videoStage.Controls.Add(_mainVideo);
                _videoStage.Controls.Add(_mainPlaceholder);
                _videoStage.Controls.Add(_mainCaption);

                _thumbnail = new Panel
                {
                    BackColor = Color.FromArgb(12, 18, 22),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                _thumbnailVideo = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(12, 18, 22),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Cursor = Cursors.Hand
                };
                _thumbnailPlaceholder = new Label
                {
                    Text = "Votre caméra démarre…",
                    ForeColor = Color.FromArgb(205, 215, 220),
                    BackColor = Color.FromArgb(12, 18, 22),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Cursor = Cursors.Hand
                };
                _thumbnailCaption = MakeVideoCaption();
                _thumbnail.Controls.Add(_thumbnailVideo);
                _thumbnail.Controls.Add(_thumbnailPlaceholder);
                _thumbnail.Controls.Add(_thumbnailCaption);
                _thumbnail.Click += OnThumbnailClick;
                _thumbnailVideo.Click += OnThumbnailClick;
                _thumbnailPlaceholder.Click += OnThumbnailClick;
                _thumbnailCaption.Click += OnThumbnailClick;

                _controls = new Panel
                {
                    BackColor = Color.FromArgb(248, 249, 250),
                    Location = new Point(0, ClientSize.Height - 82),
                    Size = new Size(ClientSize.Width, 82),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };
                _videoStatus = new Label
                {
                    Text = "Connexion vidéo…",
                    ForeColor = Color.FromArgb(100, 115, 125),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(16, 8),
                    Size = new Size(320, 20),
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom
                };
                _state = new Label
                {
                    Text = incoming ? "Appel vidéo entrant" : "Appel en cours…",
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Color.FromArgb(100, 115, 125),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(16, 31),
                    Size = new Size(320, 24),
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom
                };
                _mute = new Button { Text = "Couper micro", Size = new Size(124, 36), Anchor = AnchorStyles.Bottom };
                _hangup = MakeHangupButton(incoming);
                _controls.Controls.AddRange(new Control[] { _videoStatus, _state, _mute, _hangup });
                Controls.Add(name);
                Controls.Add(_videoStage);
                _videoStage.Controls.Add(_thumbnail);
                Controls.Add(_controls);
                _videoStage.Resize += delegate { LayoutThumbnail(); };
                _controls.Resize += delegate { LayoutVideoButtons(); };
                Resize += delegate { LayoutThumbnail(); LayoutVideoButtons(); };
                LayoutThumbnail();
                LayoutVideoButtons();
            }
            else
            {
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                ClientSize = new Size(330, 190);
                Label name = new Label
                {
                    Text = _peerName,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(20, 25), Size = new Size(290, 34)
                };
                _state = new Label
                {
                    Text = incoming ? "Appel audio entrant" : "Appel en cours…",
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Color.FromArgb(100, 115, 125),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(20, 65), Size = new Size(290, 26)
                };
                _mute = new Button { Text = "Couper micro", Location = new Point(45, 125), Size = new Size(110, 36) };
                _hangup = MakeHangupButton(incoming);
                _hangup.Location = new Point(175, 125);
                Controls.AddRange(new Control[] { name, _state, _mute, _hangup });
                _mainVideo = null;
                _thumbnailVideo = null;
                _thumbnail = null;
                _mainCaption = null;
                _thumbnailCaption = null;
                _mainPlaceholder = null;
                _thumbnailPlaceholder = null;
                _videoStatus = null;
                _videoStage = null;
                _controls = null;
            }

            _mute.Click += delegate
            {
                Muted = !Muted;
                _mute.Text = Muted ? "Réactiver micro" : "Couper micro";
                Action h = MuteToggled;
                if (h != null) h();
            };
            _hangup.Click += delegate { RaiseHangup(); };
        }

        private static Label MakeVideoCaption()
        {
            return new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(155, 0, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(8, 4, 8, 4),
                Location = new Point(12, 12)
            };
        }

        private Button MakeHangupButton(bool incoming)
        {
            Button button = new Button
            {
                Text = incoming ? "Refuser" : "Raccrocher",
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(124, 36)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void LayoutThumbnail()
        {
            if (_thumbnail == null || _videoStage == null) return;
            int width = Math.Min(300, Math.Max(170, _videoStage.ClientSize.Width / 4));
            int height = Math.Max(96, width * 9 / 16);
            if (height > _videoStage.ClientSize.Height / 2)
            {
                height = Math.Max(82, _videoStage.ClientSize.Height / 2);
                width = height * 16 / 9;
            }
            _thumbnail.SetBounds(Math.Max(8, _videoStage.ClientSize.Width - width - 16),
                Math.Max(8, _videoStage.ClientSize.Height - height - 16), width, height);
            _thumbnail.BringToFront();
        }

        private void LayoutVideoButtons()
        {
            if (_controls == null) return;
            int gap = 12;
            int pairWidth = _mute.Width + gap + _hangup.Width;
            int x = Math.Max(16, (_controls.ClientSize.Width - pairWidth) / 2);
            int y = 18;
            _mute.Location = new Point(x, y);
            _hangup.Location = new Point(x + _mute.Width + gap, y);
        }

        private void OnThumbnailClick(object sender, EventArgs e)
        {
            _localIsMain = !_localIsMain;
            RefreshVideoViews();
        }

        internal void SetRemoteFrame(Bitmap frame)
        {
            if (_mainVideo == null || _closed || IsDisposed || !IsHandleCreated) { frame.Dispose(); return; }
            Bitmap previous = _remoteFrame;
            _remoteFrame = frame;
            RefreshVideoViews();
            if (previous != null && previous != _localFrame) previous.Dispose();
        }

        internal void SetLocalFrame(Bitmap frame)
        {
            if (_thumbnailVideo == null || _closed || IsDisposed || !IsHandleCreated) { frame.Dispose(); return; }
            Bitmap previous = _localFrame;
            _localFrame = frame;
            RefreshVideoViews();
            if (previous != null && previous != _remoteFrame) previous.Dispose();
        }

        private void RefreshVideoViews()
        {
            if (_mainVideo == null || _thumbnailVideo == null || IsDisposed) return;
            Bitmap large = _localIsMain ? _localFrame : _remoteFrame;
            Bitmap small = _localIsMain ? _remoteFrame : _localFrame;
            _mainVideo.Image = large;
            _thumbnailVideo.Image = small;
            _mainPlaceholder.Text = large == null
                ? (_localIsMain ? "Votre caméra démarre…" : "En attente de la vidéo du contact…") : "";
            _thumbnailPlaceholder.Text = small == null
                ? (_localIsMain ? "Vidéo du contact en attente…" : "Votre caméra démarre…") : "";
            _mainPlaceholder.Visible = large == null;
            _thumbnailPlaceholder.Visible = small == null;
            _mainCaption.Text = _localIsMain ? "Vous" : _peerName;
            _thumbnailCaption.Text = _localIsMain ? _peerName : "Vous";
            _mainCaption.BringToFront();
            _thumbnailCaption.BringToFront();
            _thumbnail.BringToFront();
        }

        internal void SetVideoStatus(string value)
        { if (_videoStatus != null) _videoStatus.Text = value ?? ""; }

        internal void SetCallState(string value) { _state.Text = value ?? ""; }

        internal void SetIncomingAccepted()
        {
            Text = _video ? "Appel vidéo" : "Appel audio";
            _state.Text = "Connexion en cours…";
            _hangup.Text = "Raccrocher";
        }

        private void RaiseHangup()
        {
            Action h = HangupRequested;
            if (h != null) h();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) RaiseHangup();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            if (_mainVideo != null) _mainVideo.Image = null;
            if (_thumbnailVideo != null) _thumbnailVideo.Image = null;
            if (_remoteFrame != null) { _remoteFrame.Dispose(); _remoteFrame = null; }
            if (_localFrame != null) { _localFrame.Dispose(); _localFrame = null; }
            base.OnFormClosed(e);
        }
    }
}
