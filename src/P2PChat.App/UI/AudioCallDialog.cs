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
        internal event Action HangupRequested;
        internal event Action MuteToggled;
        internal bool Muted { get; private set; }

        internal AudioCallDialog(string peerName, bool incoming)
        {
            Text = incoming ? "Appel audio entrant" : "Appel audio";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(330, 190);
            BackColor = Color.FromArgb(248, 249, 250);

            Label name = new Label
            {
                Text = peerName ?? "Contact",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 25),
                Size = new Size(290, 34)
            };
            _state = new Label
            {
                Text = incoming ? "Appel audio entrant" : "Appel en cours…",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(100, 115, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 65),
                Size = new Size(290, 26)
            };
            _mute = new Button { Text = "Couper micro", Location = new Point(45, 125), Size = new Size(110, 36) };
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
                Location = new Point(175, 125),
                Size = new Size(110, 36)
            };
            _hangup.FlatAppearance.BorderSize = 0;
            _hangup.Click += delegate { RaiseHangup(); };
            Controls.Add(name);
            Controls.Add(_state);
            Controls.Add(_mute);
            Controls.Add(_hangup);
        }

        internal void SetCallState(string value) { _state.Text = value ?? ""; }
        internal void SetIncomingAccepted()
        {
            Text = "Appel audio";
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
