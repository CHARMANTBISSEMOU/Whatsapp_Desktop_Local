using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using P2PChat.App.Theme;

namespace P2PChat.App.Features
{
    /// <summary>
    /// Notifications in-app façon WhatsApp Web : toast sombre arrondi en bas à
    /// gauche (avatar + nom + aperçu), + clignotement de la barre des tâches,
    /// respect des Paramètres (son / clignotement / aperçu).
    /// </summary>
    public sealed class Toasts : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        private readonly Form _host;
        private readonly System.Windows.Forms.Timer _life;
        private string _peerId;
        private string _title;
        private string _body;
        private Image _avatar;

        /// <summary>Clic sur le toast : ouvrir la discussion peerId.</summary>
        public event Action<string> Activated;

        public Toasts(Form mainHost)
        {
            _host = mainHost;
            _life = new System.Windows.Forms.Timer();
            _life.Interval = 3500;
            _life.Tick += OnLifeTick;
        }

        /// <summary>Nouveau message : son + flash + toast (selon Paramètres).</summary>
        public void MessageReceived(Settings.AppSettings cfg, string peerId,
            string peerName, string preview, Image avatar, bool formFocused)
        {
            if (cfg != null && !string.IsNullOrEmpty(peerId) && cfg.IsBlocked(peerId)) return;
            if (!formFocused)
            {
                if (cfg == null || cfg.NotifSound) WaSounds.PlayIncoming();
                if (cfg == null || cfg.NotifFlash)
                {
                    try
                    {
                        Form f = _host;
                        if (f != null && !f.IsDisposed && !f.Focused) FlashWindow(f.Handle, true);
                    }
                    catch (Exception) { }
                }
            }
            if (cfg == null || cfg.NotifPreview)
            {
                ShowToast(peerId, peerName, preview, avatar);
            }
        }

        /// <summary>Toast informatif simple (une ligne, sans action associée).</summary>
        public void Show(string message)
        {
            ShowToast(null, null, message, null);
        }

        private void ShowToast(string peerId, string title, string body, Image avatar)
        {
            if (_host == null || _host.IsDisposed) return;
            if (_host.InvokeRequired)
            {
                try
                {
                    _host.BeginInvoke((MethodInvoker)delegate
                    {
                        ShowToast(peerId, title, body, avatar);
                    });
                }
                catch (Exception) { }
                return;
            }
            _peerId = peerId;
            _title = title;
            _body = body;
            if (_avatar != null) { try { _avatar.Dispose(); } catch (Exception) { } }
            _avatar = avatar == null ? null : new Bitmap(avatar, new Size(WaTheme.S(40), WaTheme.S(40)));
            _life.Stop();
            _life.Start();
            InvalidateHost();
        }

        private void OnLifeTick(object sender, EventArgs e)
        {
            _life.Stop();
            _peerId = null;
            _title = null;
            _body = null;
            if (_avatar != null) { try { _avatar.Dispose(); } catch (Exception) { } }
            _avatar = null;
            InvalidateHost();
        }

        private void InvalidateHost()
        {
            try { if (_host != null && !_host.IsDisposed) _host.Invalidate(); }
            catch (Exception) { }
        }

        /// <summary>Rayon du toast en bas à gauche de la zone client de l'hôte.</summary>
        public Rectangle CurrentBounds
        {
            get
            {
                if (_body == null) return Rectangle.Empty;
                int w = WaTheme.S(360), h = WaTheme.S(60);
                return new Rectangle(WaTheme.S(16),
                    _host.ClientSize.Height - h - WaTheme.S(16), w, h);
            }
        }

        /// <summary>Peint le toast (appelé par MainForm.OnPaint). Vrai si dessiné.</summary>
        public bool Paint(Graphics g)
        {
            if (_body == null || _host == null) return false;
            Rectangle r = CurrentBounds;
            if (r.Width <= 0) return false;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            using (GraphicsPath sp = WaTheme.RoundRect(r.X + 2, r.Y + 3, r.Width, r.Height, WaTheme.S(9)))
            {
                g.FillPath(sh, sp);
            }
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(0xF0, 0x14, 0x1B, 0x21)))
            using (GraphicsPath path = WaTheme.RoundRect(r.X, r.Y, r.Width, r.Height, WaTheme.S(8)))
            {
                g.FillPath(bg, path);
            }
            int ax = r.X + WaTheme.S(12), ay = r.Y + WaTheme.S(10);
            if (_title != null && _avatar != null)
            {
                WaTheme.DrawRoundImage(g, _avatar, new Rectangle(ax, ay, WaTheme.S(40), WaTheme.S(40)));
            }
            int tx = r.X + WaTheme.S(74);
            if (_title == null) tx = r.X + WaTheme.S(16);
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush gray = new SolidBrush(Color.FromArgb(0x86, 0x96, 0xA0)))
            {
                if (_title != null)
                {
                    using (Font f = WaTheme.Semi(10.5f))
                    {
                        g.DrawString(_title, f, white, tx, r.Y + WaTheme.S(11));
                    }
                    using (Font f = WaTheme.Text(9.5f))
                    {
                        string body = _body == null ? "" : _body;
                        if (body.Length > 64) body = body.Substring(0, 61) + "...";
                        g.DrawString(body, f, gray, tx, r.Y + WaTheme.S(31));
                    }
                }
                else if (_body != null)
                {
                    using (Font f = WaTheme.Text(9.8f))
                    {
                        g.DrawString(_body, f, white, tx, r.Y + WaTheme.S(20));
                    }
                }
            }
            return true;
        }

        /// <summary>Le clic est-il sur le toast actuel ?</summary>
        public bool Hit(Point p)
        {
            if (_body == null) return false;
            return CurrentBounds.Contains(p);
        }

        /// <summary>Traite un clic sur le toast : ouvre la discussion liée.</summary>
        public void Click()
        {
            string id = _peerId;
            _life.Stop();
            _peerId = null;
            _title = null;
            _body = null;
            if (_avatar != null) { try { _avatar.Dispose(); } catch (Exception) { } }
            _avatar = null;
            InvalidateHost();
            if (id != null)
            {
                Action<string> h = Activated;
                if (h != null) h(id);
            }
        }

        public void Dispose()
        {
            _life.Stop();
            _life.Dispose();
            if (_avatar != null) { try { _avatar.Dispose(); } catch (Exception) { } }
            _avatar = null;
        }
    }
}
