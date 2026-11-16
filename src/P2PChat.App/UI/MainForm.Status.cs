using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Settings;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Page « Statuts » (stories façon WhatsApp) : publication d'un statut texte
    /// sur fond coloré, expiration après 24 h, propagation P2P aux pairs en ligne,
    /// liste à gauche (mon statut + pairs), visionneuse plein panneau à droite.
    /// </summary>
    public sealed partial class MainForm
    {
        private string _selectedStatusId;
        private int _hoverStatusRow = -2; // -2 aucun, -1 = mon statut, >= 0 = ligne pair

        private static readonly int[] StatusPalette = new int[]
        {
            unchecked((int)0xFF128C7E), unchecked((int)0xFF0B6E8A), unchecked((int)0xFF5B7CFA),
            unchecked((int)0xFF8A2BE2), unchecked((int)0xFFE77C40), unchecked((int)0xFFD35400),
            unchecked((int)0xFFC2416B), unchecked((int)0xFF2E3440)
        };

        /// <summary>Un statut est frais s'il a moins de 24 h.</summary>
        private static bool StatusFresh(StatusItem it)
        {
            if (it == null) return false;
            long cutoff = DateTime.UtcNow.AddHours(-24).Ticks / TimeSpan.TicksPerMillisecond;
            return it.Ts >= cutoff;
        }

        /// <summary>Mes statuts, du plus récent au plus ancien.</summary>
        internal List<StatusItem> MyStatuses()
        {
            List<StatusItem> list = new List<StatusItem>();
            foreach (StatusItem it in Cfg.Statuses)
            {
                if (it.Mine && StatusFresh(it)) list.Add(it);
            }
            list.Sort(delegate(StatusItem a, StatusItem b) { return b.Ts.CompareTo(a.Ts); });
            return list;
        }

        /// <summary>Statuts des pairs, du plus récent au plus ancien.</summary>
        internal List<StatusItem> PeerStatuses()
        {
            List<StatusItem> list = new List<StatusItem>();
            foreach (StatusItem it in Cfg.Statuses)
            {
                if (!it.Mine && StatusFresh(it) && !Cfg.IsBlocked(it.SenderId)) list.Add(it);
            }
            list.Sort(delegate(StatusItem a, StatusItem b) { return b.Ts.CompareTo(a.Ts); });
            return list;
        }

        private StatusItem SelectedStatus()
        {
            if (_selectedStatusId == null) return null;
            foreach (StatusItem it in Cfg.Statuses)
            {
                if (it.Id == _selectedStatusId && StatusFresh(it)) return it;
            }
            return null;
        }

        private void PurgeStaleStatuses()
        {
            Cfg.Statuses.RemoveAll(delegate(StatusItem x) { return !StatusFresh(x); });
        }

        // ------------------------------------------------------------ géométrie
        private Rectangle StatusMyRowRect()
        {
            Rectangle side = SidebarRect();
            int y = side.Y + WaTheme.S(HeaderH) + WaTheme.S(12);
            return new Rectangle(side.X + WaTheme.S(12), y, side.Width - WaTheme.S(24), WaTheme.S(64));
        }

        private Rectangle StatusPeerRowRect(int index)
        {
            Rectangle side = SidebarRect();
            int y = StatusMyRowRect().Bottom + WaTheme.S(30) + index * WaTheme.S(64);
            return new Rectangle(side.X + WaTheme.S(12), y, side.Width - WaTheme.S(24), WaTheme.S(64));
        }

        /// <summary>Bouton « + » de l'entête : publie un nouveau statut.</summary>
        private Rectangle StatusNewButtonRect()
        {
            Rectangle side = SidebarRect();
            int s = WaTheme.S(32);
            return new Rectangle(side.Right - s - WaTheme.S(18), WaTheme.S(20), s, s);
        }

        private int StatusHitTest(Point p)
        {
            if (StatusMyRowRect().Contains(p)) return -1;
            List<StatusItem> peers = PeerStatuses();
            for (int i = 0; i < peers.Count; i++)
            {
                if (StatusPeerRowRect(i).Contains(p)) return i;
            }
            return -2;
        }

        // ------------------------------------------------------------ souris
        private void StatusMouseMove(Point p)
        {
            if (StatusNewButtonRect().Contains(p))
            {
                Cursor = Cursors.Hand;
                if (_hoverStatusRow != -3) { _hoverStatusRow = -3; Invalidate(SidebarRect()); }
                return;
            }
            int hit = StatusHitTest(p);
            if (hit != _hoverStatusRow)
            {
                _hoverStatusRow = hit;
                Cursor = hit >= -1 ? Cursors.Hand : Cursors.Default;
                Invalidate(SidebarRect());
            }
        }

        private void StatusMouseDown(Point p)
        {
            if (StatusNewButtonRect().Contains(p)) { CreateMyStatus(); return; }
            int hit = StatusHitTest(p);
            if (hit == -1)
            {
                List<StatusItem> mine = MyStatuses();
                if (mine.Count == 0) CreateMyStatus();
                else
                {
                    _selectedStatusId = mine[0].Id;
                    Invalidate();
                }
                return;
            }
            if (hit >= 0)
            {
                List<StatusItem> peers = PeerStatuses();
                if (hit < peers.Count)
                {
                    _selectedStatusId = peers[hit].Id;
                    Invalidate();
                }
            }
        }

        // ------------------------------------------------------------ peinture
        internal void PaintStatusPage(Graphics g)
        {
            Rectangle side = SidebarRect();
            using (SolidBrush sb = new SolidBrush(WaTheme.SidebarBg)) g.FillRectangle(sb, side);

            using (Font f = WaTheme.Semi(18f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                g.DrawString("Statuts", f, tb, side.X + WaTheme.S(24), side.Y + WaTheme.S(22));
            using (Font f = WaTheme.Text(9.4f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                g.DrawString("Disparaissent après 24 h", f, tb, side.X + WaTheme.S(24), side.Y + WaTheme.S(50));

            Rectangle nb = StatusNewButtonRect();
            using (SolidBrush bgb = new SolidBrush(_hoverStatusRow == -3 ? WaTheme.Selected : WaTheme.SearchBg))
                g.FillEllipse(bgb, nb);
            using (Pen pen = new Pen(WaTheme.Accent, WaTheme.S(2.2f)))
            {
                int cx = nb.X + nb.Width / 2, cy = nb.Y + nb.Height / 2, a = WaTheme.S(7);
                g.DrawLine(pen, cx - a, cy, cx + a, cy);
                g.DrawLine(pen, cx, cy - a, cx, cy + a);
            }

            List<StatusItem> mine = MyStatuses();
            Rectangle my = StatusMyRowRect();
            if (_hoverStatusRow == -1)
            {
                using (SolidBrush hb = new SolidBrush(WaTheme.Hover))
                using (GraphicsPath p = WaTheme.RoundRect(my.X, my.Y, my.Width, my.Height, WaTheme.S(10)))
                    g.FillPath(hb, p);
            }
            int av = WaTheme.S(48);
            Rectangle avr = new Rectangle(my.X + WaTheme.S(10), my.Y + (my.Height - av) / 2, av, av);
            if (mine.Count > 0)
            {
                using (Pen ring = new Pen(WaTheme.Accent, WaTheme.S(2.6f)))
                    g.DrawEllipse(ring, avr.X - WaTheme.S(3), avr.Y - WaTheme.S(3),
                        avr.Width + WaTheme.S(6), avr.Height + WaTheme.S(6));
            }
            WaTheme.DrawAvatar(g, avr, _self.Pseudo, null, false);
            using (Font f = WaTheme.Semi(10.4f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                g.DrawString(mine.Count == 0 ? "Mon statut" : "Mon statut · " + WaTheme.FormatListTime(mine[0].Ts),
                    f, tb, avr.Right + WaTheme.S(12), my.Y + WaTheme.S(11));
            using (Font f = WaTheme.Text(9f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                g.DrawString(mine.Count == 0 ? "Appuyez pour publier un statut" : mine.Count + " statut(s) publié(s)",
                    f, tb, avr.Right + WaTheme.S(12), my.Y + WaTheme.S(34));

            List<StatusItem> peers = PeerStatuses();
            using (Font f = WaTheme.Semi(8.6f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                g.DrawString(peers.Count == 0 ? "AUCUN STATUT RÉCENT" : "RÉCENTS", f, tb,
                    side.X + WaTheme.S(24), StatusMyRowRect().Bottom + WaTheme.S(9));

            for (int i = 0; i < peers.Count; i++)
            {
                Rectangle r = StatusPeerRowRect(i);
                if (r.Bottom > side.Bottom) break;
                if (_hoverStatusRow == i || peers[i].Id == _selectedStatusId)
                {
                    using (SolidBrush hb = new SolidBrush(peers[i].Id == _selectedStatusId ? WaTheme.Selected : WaTheme.Hover))
                    using (GraphicsPath p = WaTheme.RoundRect(r.X, r.Y, r.Width, r.Height, WaTheme.S(10)))
                        g.FillPath(hb, p);
                }
                Rectangle pav = new Rectangle(r.X + WaTheme.S(10), r.Y + (r.Height - av) / 2, av, av);
                using (Pen ring = new Pen(WaTheme.Accent, WaTheme.S(2.4f)))
                    g.DrawEllipse(ring, pav.X - WaTheme.S(3), pav.Y - WaTheme.S(3),
                        pav.Width + WaTheme.S(6), pav.Height + WaTheme.S(6));
                WaTheme.DrawAvatar(g, pav, peers[i].SenderName, null, false);
                using (Font f = WaTheme.Semi(10.2f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                    g.DrawString(ClipText(g, peers[i].SenderName, f, r.Width - WaTheme.S(100)), f, tb,
                        pav.Right + WaTheme.S(12), r.Y + WaTheme.S(11));
                using (Font f = WaTheme.Text(9f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    string preview = peers[i].Text == null ? "" : peers[i].Text.Replace("\r", " ").Replace("\n", " ");
                    g.DrawString(ClipText(g, preview + " · " + WaTheme.FormatListTime(peers[i].Ts), f, r.Width - WaTheme.S(100)),
                        f, tb, pav.Right + WaTheme.S(12), r.Y + WaTheme.S(34));
                }
            }

            using (Pen sep = new Pen(WaTheme.Divider))
                g.DrawLine(sep, side.Right - 1, 0, side.Right - 1, ClientSize.Height);

            PaintStatusViewer(g);
        }

        private void PaintStatusViewer(Graphics g)
        {
            Rectangle main = ChatRect();
            using (SolidBrush mb = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(mb, main);
            StatusItem sel = SelectedStatus();
            if (sel == null)
            {
                int cx = main.X + main.Width / 2, cy = main.Y + main.Height / 2;
                using (SolidBrush cb = new SolidBrush(WaTheme.ChatHeaderBg))
                    g.FillEllipse(cb, cx - WaTheme.S(40), cy - WaTheme.S(112), WaTheme.S(80), WaTheme.S(80));
                DrawStatusIcon(g, new Rectangle(cx - WaTheme.S(22), cy - WaTheme.S(94), WaTheme.S(44), WaTheme.S(44)), WaTheme.TextGray);
                using (Font f = WaTheme.Semi(15f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                {
                    string t = "Aucun statut sélectionné";
                    SizeF sz = g.MeasureString(t, f);
                    g.DrawString(t, f, tb, cx - sz.Width / 2f, cy - WaTheme.S(6));
                }
                using (Font f = WaTheme.Text(9.6f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    string t = "Sélectionnez un statut à gauche, ou appuyez sur « Mon statut » pour publier.";
                    SizeF sz = g.MeasureString(t, f);
                    g.DrawString(t, f, tb, cx - sz.Width / 2f, cy + WaTheme.S(22));
                }
                return;
            }

            Rectangle card = new Rectangle(main.X + WaTheme.S(24), main.Y + WaTheme.S(24),
                main.Width - WaTheme.S(48), main.Height - WaTheme.S(48));
            if (card.Width < WaTheme.S(60) || card.Height < WaTheme.S(60)) return;
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(sel.Bg)))
            using (GraphicsPath p = WaTheme.RoundRect(card.X, card.Y, card.Width, card.Height, WaTheme.S(14)))
                g.FillPath(cb, p);

            using (SolidBrush bar = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                g.FillRectangle(bar, card.X + WaTheme.S(2), card.Y + WaTheme.S(2), card.Width - WaTheme.S(4), WaTheme.S(44));
            int av = WaTheme.S(32);
            WaTheme.DrawAvatar(g, new Rectangle(card.X + WaTheme.S(12), card.Y + WaTheme.S(9), av, av),
                sel.Mine ? _self.Pseudo : sel.SenderName, null, false);
            using (Font f = WaTheme.Semi(10.6f))
            using (SolidBrush tb = new SolidBrush(Color.White))
                g.DrawString((sel.Mine ? "Mon statut" : sel.SenderName) + " · " + WaTheme.FormatListTime(sel.Ts),
                    f, tb, card.X + WaTheme.S(56), card.Y + WaTheme.S(15));

            using (Font f = WaTheme.Semi(24f + WaTheme.FontStep * 3f))
            using (SolidBrush tb = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(sel.Text ?? "", f, tb,
                    new RectangleF(card.X + WaTheme.S(30), card.Y + WaTheme.S(50),
                        card.Width - WaTheme.S(60), card.Height - WaTheme.S(106)), sf);
            }
            if (sel.Mine)
            {
                using (Font f = WaTheme.Text(8.6f))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.DrawString("Publié · visible par les pairs du réseau local", f, tb,
                        card.X + WaTheme.S(14), card.Bottom - WaTheme.S(26));
            }
        }

        // ------------------------------------------------------------ publication
        internal void CreateMyStatus()
        {
            string text; int bg;
            if (!PromptCreateStatus(out text, out bg)) return;
            PublishStatus(text, bg);
        }

        private bool PromptCreateStatus(out string text, out int bg)
        {
            text = null;
            bg = StatusPalette[0];
            Form f = new Form();
            f.Text = "Nouveau statut";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(436, 360);

            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.TextAlign = HorizontalAlignment.Center;
            tb.Font = new Font("Segoe UI", 15f);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Bounds = new Rectangle(20, 18, 396, 172);
            f.Controls.Add(tb);

            Label lb = new Label();
            lb.Text = "Couleur de fond";
            lb.AutoSize = true;
            lb.Location = new Point(20, 206);
            f.Controls.Add(lb);

            int[] chosen = new int[] { 0 };
            Button[] swatches = new Button[StatusPalette.Length];
            System.Action mark = null;
            for (int i = 0; i < StatusPalette.Length; i++)
            {
                int idx = i;
                Button b = new Button();
                b.Bounds = new Rectangle(20 + i * 46, 230, 36, 36);
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 3;
                b.BackColor = Color.FromArgb(StatusPalette[i]);
                b.FlatAppearance.BorderColor = SystemColors.Control;
                b.Click += delegate { chosen[0] = idx; if (mark != null) mark(); };
                f.Controls.Add(b);
                swatches[i] = b;
            }
            mark = delegate
            {
                for (int j = 0; j < swatches.Length; j++)
                {
                    swatches[j].FlatAppearance.BorderColor = j == chosen[0] ? Color.Black : SystemColors.Control;
                }
            };
            mark();

            Button ok = new Button();
            ok.Text = "PUBLIER";
            ok.DialogResult = DialogResult.OK;
            ok.Bounds = new Rectangle(302, 308, 114, 34);
            Button cancel = new Button();
            cancel.Text = "Annuler";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Bounds = new Rectangle(180, 308, 110, 34);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;

            if (f.ShowDialog(this) != DialogResult.OK) return false;
            string t = tb.Text == null ? "" : tb.Text.Trim();
            if (t.Length == 0)
            {
                _toasts.Show("Saisissez le texte de votre statut.");
                return false;
            }
            text = t;
            bg = StatusPalette[chosen[0]];
            return true;
        }

        private void PublishStatus(string text, int bg)
        {
            StatusItem it = new StatusItem();
            it.Id = Guid.NewGuid().ToString("N");
            it.SenderId = _self.Id;
            it.SenderName = _self.Pseudo;
            it.Text = text;
            it.Bg = bg;
            it.Ts = ChatMessage.NowUtcMs();
            it.Mine = true;
            Cfg.Statuses.Add(it);
            PurgeStaleStatuses();
            Cfg.Save();

            StatusPost post = new StatusPost();
            post.Id = it.Id;
            post.SenderId = it.SenderId;
            post.Text = it.Text;
            post.Bg = it.Bg;
            post.TimestampUtc = it.Ts;
            List<PeerInfo> snapshot = new List<PeerInfo>(_net.Peers);
            foreach (PeerInfo p in snapshot)
            {
                if (p.Id == _self.Id || Cfg.IsBlocked(p.Id)) continue;
                _net.SendStatusAsync(p.Id, post);
            }

            _selectedStatusId = it.Id;
            Invalidate();
            _toasts.Show("Statut publié");
        }

        /// <summary>Statut reçu d'un pair : mémorisation + notification.</summary>
        private void OnStatusReceived(string senderId, StatusPost st)
        {
            if (st == null || Cfg.IsBlocked(senderId)) return;
            foreach (StatusItem x in Cfg.Statuses)
            {
                if (x.Id == st.Id) return; // doublon
            }
            StatusItem it = new StatusItem();
            it.Id = st.Id;
            it.SenderId = senderId;
            PeerInfo p = FindPeer(senderId);
            it.SenderName = p != null && !string.IsNullOrEmpty(p.Pseudo) ? p.Pseudo : "Pair";
            it.Text = st.Text;
            it.Bg = st.Bg;
            it.Ts = st.TimestampUtc;
            it.Mine = false;
            Cfg.Statuses.Add(it);
            PurgeStaleStatuses();
            Cfg.Save();
            if (_activeNav == 2) Invalidate();
            else _toasts.Show(it.SenderName + " a publié un statut");
        }
    }
}