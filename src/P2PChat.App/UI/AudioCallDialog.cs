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
        private readonly PictureBox _remoteVideo;
        private readonly Label _videoStatus;
        private readonly bool _video;
        internal event Action HangupRequested;
        internal event Action MuteToggled;
        internal bool Muted { get; private set; }

        internal AudioCallDialog(string peerName, bool incoming, bool video)
        {
            _video = video;
            Text = incoming ? (video ? "Appel vidéo entrant" : "Appel audio entrant") : (video ? "Appel vidéo" : "Appel audio");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = video ? new Size(520, 435) : new Size(330, 190);
            BackColor = Color.FromArgb(248, 249, 250);

            Label name = new Label
            {
                Text = peerName ?? "Contact",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, video ? 12 : 25),
                Size = new Size(video ? 480 : 290, 34)
            };
            _remoteVideo = video ? new PictureBox
            {
                BackColor = Color.FromArgb(15, 25, 30),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 54),
                Size = new Size(480, 270)
            } : null;
            _videoStatus = video ? new Label
            {
                Text = "Connexion vidéo…",
                ForeColor = Color.FromArgb(100, 115, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 326), Size = new Size(480, 22)
            } : null;
            _state = new Label
            {
                Text = incoming ? "Appel audio entrant" : "Appel en cours…",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(100, 115, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, video ? 350 : 65),
                Size = new Size(video ? 480 : 290, 26)
            };
            int buttonY = video ? 390 : 125;
            _mute = new Button { Text = "Couper micro", Location = new Point(video ? 135 : 45, buttonY), Size = new Size(110, 36) };
            _mute.Click += delegate
            {
                Muted = !Muted;
                _mute.Text = Muted ? "Réactiver micro" : "Couper micro";
                Action h = MuteToggled;
                if (h != null) h();
            };
            _hangup = new Button
            {
                Text = incoming ? "Refuser" : "Raccrocher",
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(video ? 275 : 175, buttonY),
                Size = new Size(110, 36)
            };
            _hangup.FlatAppearance.BorderSize = 0;
            _hangup.Click += delegate { RaiseHangup(); };
            Controls.Add(name);
            if (_remoteVideo != null) Controls.Add(_remoteVideo);
            if (_videoStatus != null) Controls.Add(_videoStatus);
            Controls.Add(_state);
            Controls.Add(_mute);
            Controls.Add(_hangup);
        }

        internal void SetRemoteFrame(Bitmap frame)
        {
            if (_remoteVideo == null || _remoteVideo.IsDisposed) { frame.Dispose(); return; }
            Image previous = _remoteVideo.Image;
            _remoteVideo.Image = frame;
            if (previous != null) previous.Dispose();
        }

        internal void SetVideoStatus(string value)
        { if (_videoStatus != null) _videoStatus.Text = value ?? ""; }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_remoteVideo != null && _remoteVideo.Image != null)
            { Image image = _remoteVideo.Image; _remoteVideo.Image = null; image.Dispose(); }
            base.OnFormClosed(e);
        }

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
    }
}
