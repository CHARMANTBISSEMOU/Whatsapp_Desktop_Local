using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Theme;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Page « Profil » (entrée 5 du rail) : identité locale, résumé réseau et
    /// accès directs à l'édition du profil et à la page Paramètres.
    /// </summary>
    public sealed partial class MainForm
    {
        private int _hoverProfileBtn = -1;

        private Rectangle ProfileCardRect()
        {
            Rectangle main = ChatRect();
            int w = Math.Min(WaTheme.S(470), main.Width - WaTheme.S(40));
            int h = Math.Min(WaTheme.S(420), main.Height - WaTheme.S(40));
            if (w < WaTheme.S(240)) w = Math.Max(WaTheme.S(240), main.Width);
            if (h < WaTheme.S(240)) h = Math.Max(WaTheme.S(240), main.Height);
            return new Rectangle(main.X + (main.Width - w) / 2,
                main.Y + (main.Height - h) / 2, w, h);
        }

        private Rectangle ProfileButtonRect(int index)
        {
            Rectangle card = ProfileCardRect();
            int pad = WaTheme.S(18);
            int w = (card.Width - pad * 3) / 2;
            int h = WaTheme.S(40);
            return new Rectangle(card.X + pad + index * (w + pad), card.Bottom - h - pad, w, h);
        }

        internal void PaintProfilePage(Graphics g)
        {
            Rectangle side = SidebarRect();
            Rectangle main = ChatRect();
            using (SolidBrush b = new SolidBrush(WaTheme.SidebarBg)) g.FillRectangle(b, side);
            using (SolidBrush b = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(b, main);
            using (Pen p = new Pen(WaTheme.Divider))
                g.DrawLine(p, side.Right - 1, 0, side.Right - 1, ClientSize.Height);

            using (Font f = WaTheme.Semi(19f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
                g.DrawString("Profil", f, b, side.X + WaTheme.S(24), WaTheme.S(20));
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                g.DrawString("Votre identité sur le réseau local", f, b,
                    side.X + WaTheme.S(24), WaTheme.S(58));

            Rectangle card = ProfileCardRect();
            using (GraphicsPath path = WaTheme.RoundRect(card.X, card.Y, card.Width, card.Height, WaTheme.S(16)))
            {
                using (SolidBrush b = new SolidBrush(WaTheme.SidebarBg)) g.FillPath(b, path);
                using (Pen p = new Pen(WaTheme.Divider)) g.DrawPath(p, path);
            }

            int av = WaTheme.S(96);
            Rectangle avr = new Rectangle(card.X + (card.Width - av) / 2, card.Y + WaTheme.S(26), av, av);
            WaTheme.DrawAvatar(g, avr, _self.Pseudo, null);

            string pseudo = string.IsNullOrEmpty(_self.Pseudo) ? "Sans pseudo" : _self.Pseudo;
            int ty = avr.Bottom + WaTheme.S(14);
            using (Font f = WaTheme.Semi(17f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
            {
                SizeF sz = g.MeasureString(pseudo, f);
                g.DrawString(pseudo, f, b, card.X + (card.Width - sz.Width) / 2f, ty);
            }
            string bio = string.IsNullOrEmpty(_self.Bio) ? "Disponible" : _self.Bio;
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
            {
                int maxW = card.Width - WaTheme.S(48);
                if (g.MeasureString(bio, f).Width > maxW) bio = ClipText(g, bio, f, maxW);
                SizeF sz = g.MeasureString(bio, f);
                g.DrawString(bio, f, b, card.X + (card.Width - sz.Width) / 2f, ty + WaTheme.S(30));
            }

            int y = ty + WaTheme.S(66);
            using (Pen p = new Pen(WaTheme.Divider))
                g.DrawLine(p, card.X + WaTheme.S(18), y, card.Right - WaTheme.S(18), y);
            y = ProfileInfoRow(g, card, y + WaTheme.S(14), "Identifiant", _self.Id);
            string diag = _net == null ? "" : _net.DiagnosticSummary;
            y = ProfileInfoRow(g, card, y, "Réseau", diag);

            for (int i = 0; i < 2; i++)
            {
                Rectangle r = ProfileButtonRect(i);
                bool hot = _hoverProfileBtn == i;
                Color fill = i == 0
                    ? (hot ? WaTheme.Blend(WaTheme.Accent, Color.Black, 0.15) : WaTheme.Accent)
                    : (hot ? WaTheme.Hover : WaTheme.SearchBg);
                using (GraphicsPath path = WaTheme.RoundRect(r.X, r.Y, r.Width, r.Height, WaTheme.S(10)))
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                string label = i == 0 ? "Modifier le profil" : "Paramètres";
                using (Font f = WaTheme.Semi(10f))
                using (SolidBrush b = new SolidBrush(i == 0 ? Color.White : WaTheme.TextDark))
                {
                    SizeF sz = g.MeasureString(label, f);
                    g.DrawString(label, f, b, r.X + (r.Width - sz.Width) / 2f,
                        r.Y + (r.Height - sz.Height) / 2f);
                }
            }
        }

        private int ProfileInfoRow(Graphics g, Rectangle card, int y, string label, string value)
        {
            if (string.IsNullOrEmpty(value)) value = "non renseigné";
            using (Font lf = WaTheme.Text(9f))
            using (SolidBrush lb = new SolidBrush(WaTheme.TextGray))
                g.DrawString(label.ToUpperInvariant(), lf, lb, card.X + WaTheme.S(18), y + WaTheme.S(1));
            int vx = card.X + WaTheme.S(112);
            int maxW = card.Right - WaTheme.S(18) - vx;
            string[] lines = value.Replace("\r", "").Split('\n');
            using (Font vf = WaTheme.Text(9.6f))
            using (SolidBrush vb = new SolidBrush(WaTheme.TextDark))
            {
                int shown = 0;
                foreach (string raw in lines)
                {
                    if (shown >= 3) break;
                    string line = raw.Trim();
                    if (line.Length == 0) line = " ";
                    if (g.MeasureString(line, vf).Width > maxW) line = ClipText(g, line, vf, maxW);
                    g.DrawString(line, vf, vb, vx, y);
                    y += WaTheme.S(17);
                    shown++;
                }
            }
            return y + WaTheme.S(8);
        }

        private void ProfileMouseMove(Point p)
        {
            int hit = -1;
            for (int i = 0; i < 2; i++) if (ProfileButtonRect(i).Contains(p)) { hit = i; break; }
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            if (hit != _hoverProfileBtn)
            {
                _hoverProfileBtn = hit;
                Invalidate(ChatRect());
            }
        }

        private void ProfileMouseDown(Point p)
        {
            if (ProfileButtonRect(0).Contains(p)) { ShowProfileEditor(); return; }
            if (ProfileButtonRect(1).Contains(p)) { ActivateSettingsPage(); return; }
        }
    }
}
