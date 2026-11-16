using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using P2PChat.App.Theme;

namespace P2PChat.App.UI
{
    /// <summary>Rail de navigation persistante : discussions actives, autres entrées en attente.</summary>
    public sealed partial class MainForm
    {
        internal int NavigationRailWidth() { return WaTheme.S(64); }

        internal Rectangle NavigationRailRect()
        {
            return new Rectangle(0, 0, NavigationRailWidth(), ClientSize.Height);
        }

        private Rectangle NavigationButtonRect(int index)
        {
            int size = WaTheme.S(42);
            if (index == 5)
                return new Rectangle((NavigationRailWidth() - size) / 2,
                    ClientSize.Height - WaTheme.S(58), size, size);
            if (index == 6)
                return new Rectangle((NavigationRailWidth() - size) / 2,
                    ClientSize.Height - WaTheme.S(112), size, size);
            int y;
            switch (index)
            {
                case 0: y = 10; break;
                case 1: y = 66; break;
                case 2: y = 122; break;
                case 3: y = 178; break;
                case 4: y = 246; break;
                default: y = 246; break;
            }
            return new Rectangle((NavigationRailWidth() - size) / 2, WaTheme.S(y), size, size);
        }

        private int NavigationHitTest(Point p)
        {
            for (int i = 0; i < 5; i++)
                if (NavigationButtonRect(i).Contains(p)) return i;
            if (NavigationButtonRect(5).Contains(p)) return 5;
            if (NavigationButtonRect(6).Contains(p)) return 6;
            return -1;
        }

        private Rectangle StatusEditButtonRect()
        {
            Rectangle side = SidebarRect();
            return new Rectangle(side.X + WaTheme.S(16), WaTheme.S(92), side.Width - WaTheme.S(32), WaTheme.S(34));
        }

        private string NavigationLabel(int index)
        {
            switch (index)
            {
                case 0: return "Discussions";
                case 1: return "Appels";
                case 2: return "Statuts";
                case 3: return "Communautés";
                case 4: return "Archives";
                case 6: return "Paramètres";
                default: return "Profil";
            }
        }

        private void PaintNavigationRail(Graphics g)
        {
            Rectangle rail = NavigationRailRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.Dark ? Color.FromArgb(17, 27, 33) : Color.FromArgb(247, 247, 247)))
                g.FillRectangle(bg, rail);
            using (Pen border = new Pen(WaTheme.Divider))
                g.DrawLine(border, rail.Right - 1, rail.Top, rail.Right - 1, rail.Bottom);

            for (int i = 0; i < 5; i++)
            {
                Rectangle r = NavigationButtonRect(i);
                bool active = _activeNav == i;
                if (active || _hoverNav == i)
                {
                    using (SolidBrush b = new SolidBrush(active ? WaTheme.Selected : WaTheme.Hover))
                        g.FillEllipse(b, r);
                }
                Color c = active ? WaTheme.TextDark : WaTheme.IconGray;
                int inset = WaTheme.S(11);
                if (i == 0) DrawChatIcon(g, r, c);
                else if (i == 1) WaIcons.Phone(g, r.X + inset, r.Y + inset, r.Width - inset * 2, c);
                else if (i == 2) DrawStatusIcon(g, r, c);
                else if (i == 3) DrawPeopleIcon(g, r, c);
                else DrawArchiveIcon(g, r, c);
                if (i == 0 && UnreadCount() > 0)
                {
                    int d = WaTheme.S(18);
                    Rectangle badge = new Rectangle(r.Right - d / 2, r.Top - d / 4, d, d);
                    using (SolidBrush green = new SolidBrush(WaTheme.BrandGreen)) g.FillEllipse(green, badge);
                    using (Font f = WaTheme.Semi(7.5f))
                    using (SolidBrush white = new SolidBrush(Color.White))
                    {
                        string count = UnreadCount() > 9 ? "9+" : UnreadCount().ToString();
                        SizeF sz = g.MeasureString(count, f);
                        g.DrawString(count, f, white, badge.X + (badge.Width - sz.Width) / 2, badge.Y + (badge.Height - sz.Height) / 2 - 1);
                    }
                }
            }

            using (Pen sep = new Pen(WaTheme.Divider))
                g.DrawLine(sep, WaTheme.S(14), WaTheme.S(300), NavigationRailWidth() - WaTheme.S(14), WaTheme.S(300));

            Rectangle profile = NavigationButtonRect(5);
            WaTheme.DrawAvatar(g, new Rectangle(profile.X + WaTheme.S(5), profile.Y + WaTheme.S(5),
                profile.Width - WaTheme.S(10), profile.Height - WaTheme.S(10)), _self.Pseudo, null);

            Rectangle gear = NavigationButtonRect(6);
            bool gearActive = _activeNav == 6;
            if (gearActive || _hoverNav == 6)
            {
                using (SolidBrush b = new SolidBrush(gearActive ? WaTheme.Selected : WaTheme.Hover))
                    g.FillEllipse(b, gear);
            }
            DrawGearIcon(g, gear, gearActive ? WaTheme.TextDark : WaTheme.IconGray);
        }

        /// <summary>Engrenage dessiné à la main (aucune ressource externe).</summary>
        private static void DrawGearIcon(Graphics g, Rectangle r, Color c)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float ring = r.Width * .17f;
            using (Pen p = new Pen(c, WaTheme.S(2f)))
            {
                g.DrawEllipse(p, cx - ring, cy - ring, ring * 2, ring * 2);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * System.Math.PI / 4.0;
                    float ca = (float)System.Math.Cos(a), sa = (float)System.Math.Sin(a);
                    g.DrawLine(p, cx + ca * (ring + WaTheme.S(2)), cy + sa * (ring + WaTheme.S(2)),
                        cx + ca * (ring + WaTheme.S(6)), cy + sa * (ring + WaTheme.S(6)));
                }
            }
        }

        private void PaintNavigationPage(Graphics g)
        {
            Rectangle side = SidebarRect(), main = ChatRect();
            using (SolidBrush sb = new SolidBrush(WaTheme.SidebarBg)) g.FillRectangle(sb, side);
            using (SolidBrush mb = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(mb, main);
            using (Pen sep = new Pen(WaTheme.Divider)) g.DrawLine(sep, side.Right - 1, 0, side.Right - 1, ClientSize.Height);

            string title = NavigationLabel(_activeNav);
            using (Font f = WaTheme.Semi(19f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark)) g.DrawString(title, f, b, side.X + WaTheme.S(24), WaTheme.S(20));

            string sub;
            string heading;
            string body;
            switch (_activeNav)
            {
                case 1:
                    sub = "Restez en contact avec vos proches";
                    heading = "Les appels arrivent bientôt";
                    body = "Les appels audio et vidéo seront disponibles ici.";
                    break;
                case 2:
                    sub = "Statuts de profil des pairs du réseau local";
                    heading = "Statuts de profil";
                    body = "Les stories avec photos, vidéos et expiration restent à développer.";
                    break;
                case 3:
                    sub = "Retrouvez vos groupes au même endroit";
                    heading = "Les communautés arrivent bientôt";
                    body = "Créez une communauté pour organiser vos groupes.";
                    break;
                case 4:
                    sub = "Vos discussions mises de côté";
                    heading = "Aucune discussion archivée";
                    body = "Les discussions archivées apparaîtront ici.";
                    break;
                default:
                    sub = "Votre identité sur le réseau local";
                    heading = string.IsNullOrEmpty(_self.Pseudo) ? "Votre profil" : _self.Pseudo;
                    body = string.IsNullOrEmpty(_self.Bio) ? "Disponible" : _self.Bio;
                    break;
            }
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextGray)) g.DrawString(sub, f, b, side.X + WaTheme.S(24), WaTheme.S(58));

            if (_activeNav == 2)
            {
                Rectangle edit = StatusEditButtonRect();
                using (SolidBrush b = new SolidBrush(WaTheme.SearchBg))
                using (GraphicsPath p = WaTheme.RoundRect(edit.X, edit.Y, edit.Width, edit.Height, WaTheme.S(8))) g.FillPath(b, p);
                using (Font f = WaTheme.Semi(9.4f))
                using (SolidBrush b = new SolidBrush(WaTheme.Accent)) g.DrawString("Modifier mon statut", f, b, edit.X + WaTheme.S(12), edit.Y + WaTheme.S(8));
                int rowY = edit.Bottom + WaTheme.S(10);
                foreach (KeyValuePair<string, Conversation> kv in _convs)
                {
                    Conversation peer = kv.Value;
                    if (IsGroup(kv.Key) || string.IsNullOrWhiteSpace(peer.PeerBio) || rowY + WaTheme.S(64) > ClientSize.Height) continue;
                    int av = WaTheme.S(42);
                    WaTheme.DrawAvatar(g, new Rectangle(side.X + WaTheme.S(16), rowY, av, av), peer.PeerName, null, peer.Online);
                    using (Font nf = WaTheme.Semi(9.7f))
                    using (SolidBrush nb = new SolidBrush(WaTheme.TextDark)) g.DrawString(peer.PeerName, nf, nb, side.X + WaTheme.S(70), rowY + WaTheme.S(2));
                    using (Font bf = WaTheme.Text(8.8f))
                    using (SolidBrush bb = new SolidBrush(WaTheme.TextGray)) g.DrawString(ClipText(g, peer.PeerBio, bf, side.Width - WaTheme.S(90)), bf, bb, side.X + WaTheme.S(70), rowY + WaTheme.S(22));
                    using (Pen line = new Pen(WaTheme.Divider)) g.DrawLine(line, side.X + WaTheme.S(70), rowY + av + WaTheme.S(8), side.Right, rowY + av + WaTheme.S(8));
                    rowY += WaTheme.S(64);
                }
            }

            int cx = main.X + main.Width / 2, cy = main.Y + main.Height / 2;
            int d = WaTheme.S(68);
            if (_activeNav == 5)
                WaTheme.DrawAvatar(g, new Rectangle(cx - d / 2, cy - WaTheme.S(126), d, d), _self.Pseudo, null);
            else
            {
                using (SolidBrush iconBg = new SolidBrush(WaTheme.ChatHeaderBg)) g.FillEllipse(iconBg, cx - d / 2, cy - WaTheme.S(126), d, d);
                Rectangle iconRect = new Rectangle(cx - d / 2 + WaTheme.S(20), cy - WaTheme.S(106), WaTheme.S(28), WaTheme.S(28));
                if (_activeNav == 1) WaIcons.Phone(g, iconRect.X, iconRect.Y, iconRect.Width, WaTheme.IconGray);
                else if (_activeNav == 2) DrawStatusIcon(g, new Rectangle(cx - d / 2 + WaTheme.S(17), cy - WaTheme.S(109), WaTheme.S(34), WaTheme.S(34)), WaTheme.IconGray);
                else if (_activeNav == 3) DrawPeopleIcon(g, new Rectangle(cx - d / 2 + WaTheme.S(17), cy - WaTheme.S(109), WaTheme.S(34), WaTheme.S(34)), WaTheme.IconGray);
                else DrawArchiveIcon(g, new Rectangle(cx - d / 2 + WaTheme.S(17), cy - WaTheme.S(109), WaTheme.S(34), WaTheme.S(34)), WaTheme.IconGray);
            }

            using (Font f = WaTheme.Semi(17f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
            {
                SizeF sz = g.MeasureString(heading, f);
                g.DrawString(heading, f, b, cx - sz.Width / 2f, cy - WaTheme.S(42));
            }
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
            {
                SizeF sz = g.MeasureString(body, f);
                g.DrawString(body, f, b, cx - sz.Width / 2f, cy - WaTheme.S(10));
            }
            if (_activeNav == 1 || _activeNav == 3)
            {
                using (Font f = WaTheme.Text(8.5f))
                using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                {
                    const string note = "Interface prête — fonctions à connecter selon SPECIFICATION_INTERFACE_FONCTIONNELLE.md";
                    SizeF sz = g.MeasureString(note, f);
                    g.DrawString(note, f, b, cx - sz.Width / 2f, cy + WaTheme.S(22));
                }
            }
        }

        private static void DrawChatIcon(Graphics g, Rectangle r, Color c)
        {
            float x = r.X + r.Width * .27f, y = r.Y + r.Height * .27f;
            float w = r.Width * .46f, h = r.Height * .38f;
            using (Pen p = new Pen(c, WaTheme.S(2.2f)))
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(x, y, w, h, 180, 90); path.AddArc(x + w - h, y, h, h, 270, 90);
                path.AddArc(x + w - h, y + h - h, h, h, 0, 90); path.AddLine(x + w, y + h * .7f, x + w * .62f, y + h * .7f);
                path.AddLine(x + w * .62f, y + h * .7f, x + w * .45f, y + h); path.AddLine(x + w * .45f, y + h, x + w * .45f, y + h * .7f);
                path.AddLine(x + w * .45f, y + h * .7f, x, y + h * .7f); path.CloseFigure();
                g.DrawPath(p, path);
            }
        }

        private static void DrawStatusIcon(Graphics g, Rectangle r, Color c)
        {
            float s = r.Width * .48f, x = r.X + (r.Width - s) / 2f, y = r.Y + (r.Height - s) / 2f;
            using (Pen p = new Pen(c, WaTheme.S(2)))
            {
                g.DrawEllipse(p, x, y, s, s);
                g.DrawArc(p, x - WaTheme.S(4), y - WaTheme.S(4), s + WaTheme.S(8), s + WaTheme.S(8), 205, 280);
                g.DrawEllipse(p, x + s * .35f, y + s * .35f, s * .3f, s * .3f);
            }
        }

        private static void DrawPeopleIcon(Graphics g, Rectangle r, Color c)
        {
            float x = r.X + r.Width * .5f, y = r.Y + r.Height * .35f;
            using (Pen p = new Pen(c, WaTheme.S(2.2f)))
            {
                g.DrawEllipse(p, x - WaTheme.S(5), y - WaTheme.S(8), WaTheme.S(10), WaTheme.S(10));
                g.DrawArc(p, x - WaTheme.S(11), y + WaTheme.S(1), WaTheme.S(22), WaTheme.S(17), 190, 160);
                g.DrawArc(p, x - WaTheme.S(18), y + WaTheme.S(4), WaTheme.S(14), WaTheme.S(12), 200, 140);
                g.DrawArc(p, x + WaTheme.S(4), y + WaTheme.S(4), WaTheme.S(14), WaTheme.S(12), 200, 140);
            }
        }

        private static void DrawArchiveIcon(Graphics g, Rectangle r, Color c)
        {
            float x = r.X + r.Width * .25f, y = r.Y + r.Height * .34f;
            float w = r.Width * .5f, h = r.Height * .38f;
            using (Pen p = new Pen(c, WaTheme.S(2.2f)))
            {
                g.DrawRectangle(p, x, y + WaTheme.S(5), w, h - WaTheme.S(5));
                g.DrawRectangle(p, x - WaTheme.S(2), y, w + WaTheme.S(4), WaTheme.S(6));
                g.DrawLine(p, x + w * .35f, y + WaTheme.S(12), x + w * .65f, y + WaTheme.S(12));
            }
        }
    }
}
