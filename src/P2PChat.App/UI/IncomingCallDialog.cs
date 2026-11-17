using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace P2PChat.App.UI
{
    /// <summary>Écran non bloquant visuellement pour décider d'un appel entrant.</summary>
    internal sealed class IncomingCallDialog : Form
    {
        private readonly Label _countdown;
        private readonly Timer _timer;
        private int _remaining;

        internal IncomingCallDialog(string peerName, bool video, int timeoutSeconds)
        {
            _remaining = Math.Max(1, timeoutSeconds);
            Text = video ? "Appel vidéo entrant" : "Appel audio entrant";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(390, 286);
            BackColor = Color.FromArgb(248, 249, 250);

            Label avatar = new Label
            {
                Text = string.IsNullOrWhiteSpace(peerName) ? "?" : peerName.Substring(0, 1).ToUpperInvariant(),
                Font = new Font("Segoe UI", 27f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 168, 132),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(76, 76),
                Location = new Point((ClientSize.Width - 76) / 2, 24)
            };
            using (GraphicsPath circle = new GraphicsPath())
            {
                circle.AddEllipse(0, 0, 76, 76);
                avatar.Region = new Region(circle);
            }
            Label name = new Label
            {
                Text = peerName ?? "Contact",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 35, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 110),
                Size = new Size(346, 30)
            };
            Label kind = new Label
            {
                Text = video ? "Appel vidéo entrant" : "Appel audio entrant",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(90, 110, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 144),
                Size = new Size(346, 24)
            };
            _countdown = new Label
            {
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(0, 145, 115),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 174),
                Size = new Size(346, 22)
            };

            Button decline = new Button
            {
                Text = "Refuser",
                DialogResult = DialogResult.No,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(42, 220),
                Size = new Size(138, 42)
            };
            Button accept = new Button
            {
                Text = "Accepter",
                DialogResult = DialogResult.Yes,
                BackColor = Color.FromArgb(0, 168, 132),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(210, 220),
                Size = new Size(138, 42)
            };
            decline.FlatAppearance.BorderSize = 0;
            accept.FlatAppearance.BorderSize = 0;
            Controls.AddRange(new Control[] { avatar, name, kind, _countdown, decline, accept });
            AcceptButton = accept;
            CancelButton = decline;

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += OnTimerTick;
            Shown += delegate { UpdateCountdown(); _timer.Start(); };
            FormClosed += delegate { _timer.Stop(); _timer.Dispose(); };
        }

        internal void DismissByCaller()
        {
            if (IsDisposed) return;
            DialogResult = DialogResult.Abort;
            Close();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _remaining--;
            if (_remaining <= 0)
            {
                _timer.Stop();
                DialogResult = DialogResult.Retry; // expire = sonnerie sans réponse
                Close();
                return;
            }
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            _countdown.Text = "Sonnerie… fin automatique dans " + _remaining + " s";
        }
    }
}
