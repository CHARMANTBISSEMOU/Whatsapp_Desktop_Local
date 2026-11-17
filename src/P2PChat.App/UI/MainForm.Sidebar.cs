using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Controls;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Barre latérale : entête, recherche, chips de filtre, liste des discussions.</summary>
    public sealed partial class MainForm
    {
        private string _pressedRowId;
        private int _hoverChip = -1;
        private int _hoverHeaderBtn = -1; // 0 = nouvelle discussion, 1 = menu

        // ---------------------------------------------------------------- peinture
        private void PaintSidebar(Graphics g)
        {
            Rectangle side = SidebarRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.SidebarBg))
            {
                g.FillRectangle(bg, side);
            }

            PaintSidebarHeader(g);
            PaintSearchAndChips(g);
            PaintChatList(g);
        }

        private Rectangle HeaderBtnRect(int idx)
        {
            int s = WaTheme.S(24), pad = WaTheme.S(10);
            int right = SidebarRect().Right - pad - s;
            return new Rectangle(right - idx * (s + pad), WaTheme.S((HeaderH - 24) / 2), s, s);
        }

        private void PaintSidebarHeader(Graphics g)
        {
            Rectangle h = SidebarHeaderRect();
            using (Font f = WaTheme.Semi(18f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                g.DrawString(_activeNav == 4 ? "Archivées" : (_activeNav == 1 ? "Appels" : "Discussions"), f, tb, h.X + WaTheme.S(24), h.Y + WaTheme.S(16));

            Rectangle bNew = HeaderBtnRect(0);
            Rectangle bMenu = HeaderBtnRect(1);
            int d = WaTheme.S(42);
            Rectangle circle = new Rectangle(bNew.X - (d - bNew.Width) / 2,
                bNew.Y - (d - bNew.Height) / 2, d, d);
            using (SolidBrush green = new SolidBrush(WaTheme.Accent)) g.FillEllipse(green, circle);
            WaIcons.Plus(g, bNew.X, bNew.Y, bNew.Width, Color.White);
            WaIcons.MenuDots(g, bMenu.X, bMenu.Y, bMenu.Height, WaTheme.IconGray);
            if (_hoverHeaderBtn == 0)
            {
                using (Pen pe = new Pen(WaTheme.BrandGreen, WaTheme.S(2)))
                    g.DrawEllipse(pe, circle.X - WaTheme.S(2), circle.Y - WaTheme.S(2), circle.Width + WaTheme.S(4), circle.Height + WaTheme.S(4));
            }
            if (_hoverHeaderBtn == 1)
            {
                using (SolidBrush hb = new SolidBrush(WaTheme.Hover))
                {
                    g.FillEllipse(hb, bMenu.X - WaTheme.S(7), bMenu.Y - WaTheme.S(7),
                        bMenu.Width + WaTheme.S(14), bMenu.Height + WaTheme.S(14));
                }
                WaIcons.MenuDots(g, bMenu.X, bMenu.Y, bMenu.Height, WaTheme.IconGray);
            }
        }

        private void PaintSearchAndChips(Graphics g)
        {
            Rectangle side = SidebarRect();
            // pilule de recherche (le TextBox réel est posé dessus)
            Rectangle pill = new Rectangle(side.X + WaTheme.S(12), WaTheme.S(HeaderH) + WaTheme.S(7),
                side.Width - WaTheme.S(24), WaTheme.S(42));
            using (SolidBrush b = new SolidBrush(WaTheme.SearchBg))
            using (GraphicsPath p = WaTheme.RoundRect(pill.X, pill.Y, pill.Width, pill.Height, WaTheme.S(9)))
            {
                g.FillPath(b, p);
            }
            if (_searchBox.Text.Length == 0)
            {
                WaIcons.Search(g, pill.X + WaTheme.S(14), pill.Y + WaTheme.S(9), WaTheme.S(16), WaTheme.IconGray);
                using (Font f = WaTheme.Text(10f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    string ph = "Rechercher ou démarrer une discussion";
                    g.DrawString(ClipText(g, ph, f, pill.Width - WaTheme.S(44)), f, tb,
                        pill.X + WaTheme.S(36), pill.Y + WaTheme.S(9));
                }
            }
            else
            {
                WaIcons.Back(g, pill.X + WaTheme.S(13), pill.Y + WaTheme.S(9), WaTheme.S(16), WaTheme.IconGray);
            }

            // chips de filtre : Toutes / Non lues / Favoris (comme WhatsApp)
            int cy = WaTheme.S(HeaderH) + WaTheme.S(74);
            int cx = side.X + WaTheme.S(14);
            int unreadCount = UnreadCount();
            string[] labels = new string[]
            {
                "Toutes",
                unreadCount > 0 ? "Non lues " + unreadCount : "Non lues",
                "Favoris"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                bool on;
                if (i == 0) on = !_unreadFilter && !_favoritesFilter;
                else if (i == 1) on = _unreadFilter;
                else on = _favoritesFilter;
                int w = WaTheme.S(20 + labels[i].Length * 8);
                Rectangle chip = new Rectangle(cx, cy, w, WaTheme.S(28));
                using (SolidBrush b = new SolidBrush(on ? WaTheme.Accent : WaTheme.SearchBg))
                using (GraphicsPath p = WaTheme.RoundRect(chip.X, chip.Y, chip.Width, chip.Height, WaTheme.S(14)))
                {
                    g.FillPath(b, p);
                }
                if (_hoverChip == i && !on)
                {
                    using (SolidBrush b = new SolidBrush(WaTheme.Hover))
                    using (GraphicsPath p = WaTheme.RoundRect(chip.X, chip.Y, chip.Width, chip.Height, WaTheme.S(14)))
                    {
                        g.FillPath(b, p);
                    }
                }
                using (Font f = WaTheme.Text(9.5f))
                using (SolidBrush tb = new SolidBrush(on ? Color.White : WaTheme.TextDark))
                {
                    SizeF sz = g.MeasureString(labels[i], f);
                    g.DrawString(labels[i], f, tb, chip.X + (chip.Width - sz.Width) / 2f,
                        chip.Y + (chip.Height - sz.Height) / 2f);
                }
                cx += w + WaTheme.S(8);
            }
        }

        private Rectangle RowRect(int index)
        {
            Rectangle lr = ListRect();
            return new Rectangle(lr.X, lr.Y + index * WaTheme.S(RowH) - _listScroll,
                lr.Width, WaTheme.S(RowH));
        }

        private void PaintChatList(Graphics g)
        {
            Rectangle lr = ListRect();
            GraphicsState clip = g.Save();
            g.SetClip(lr);

            List<Conversation> list = VisibleConversations();
            if (list.Count == 0)
            {
                using (Font f = WaTheme.Text(10.5f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    string msg = _activeNav == 4
                        ? "Aucune discussion archivée"
                        : (_convs.Count == 0 ? "Aucune discussion pour l'instant" : "Aucun résultat");
                    SizeF sz = g.MeasureString(msg, f);
                    g.DrawString(msg, f, tb, lr.X + (lr.Width - sz.Width) / 2f, lr.Y + WaTheme.S(60));
                    if (_convs.Count == 0)
                    {
                        string hint = "Les autres ordinateurs du réseau local\napparaîtront ici automatiquement.";
                        using (Font f2 = WaTheme.Text(9f))
                        {
                            sz = g.MeasureString(hint, f2);
                            g.DrawString(hint, f2, tb, lr.X + (lr.Width - sz.Width) / 2f, lr.Y + WaTheme.S(86));
                        }
                    }
                }
                g.Restore(clip);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                Rectangle row = RowRect(i);
                if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue;
                PaintChatRow(g, list[i], row);
            }

            // séparateur fin en bas de chaque ligne
            using (Pen pen = new Pen(WaTheme.Divider))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Rectangle row = RowRect(i);
                    if (row.Bottom < lr.Y || row.Y > lr.Bottom) continue;
                    g.DrawLine(pen, row.X + WaTheme.S(84), row.Bottom - 1, row.Right, row.Bottom - 1);
                }
            }

            g.Restore(clip);

            // barre de défilement fine
            int max = ListScrollMax();
            if (max > 0)
            {
                int trackH = lr.Height;
                int thumbH = Math.Max(WaTheme.S(30), trackH * trackH / (list.Count * WaTheme.S(RowH)));
                int thumbY = lr.Y + (trackH - thumbH) * _listScroll / max;
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 128, 128, 128)))
                using (GraphicsPath p = WaTheme.RoundRect(lr.Right - WaTheme.S(8), thumbY, WaTheme.S(5), thumbH, WaTheme.S(2.5f)))
                {
                    g.FillPath(sb, p);
                }
            }
        }

        private void PaintChatRow(Graphics g, Conversation c, Rectangle row)
        {
            bool selected = c.PeerId == _selectedId;
            bool hovered = c.PeerId == _hoverRowId && !selected;
            if (selected || hovered)
            {
                using (SolidBrush b = new SolidBrush(selected ? WaTheme.Selected : WaTheme.Hover))
                {
                    g.FillRectangle(b, row);
                }
            }

            Settings.PeerLocal local = Cfg.Peer(c.PeerId);
            int unread = c.Unread + (local.MarkedUnread && c.Unread == 0 ? 1 : 0);

            int av = WaTheme.S(58);
            WaTheme.DrawAvatar(g,
                new Rectangle(row.X + WaTheme.S(13), row.Y + (row.Height - av) / 2, av, av),
                c.PeerName, null, c.Online);

            int tx = row.X + WaTheme.S(84);
            int nameMaxW = row.Width - WaTheme.S(84) - WaTheme.S(90);

            // heure (à droite, verte si non-lus)
            string time = "";
            ChatMessage last = c.Last;
            if (last != null) time = WaTheme.FormatListTime(last.TimestampUtc);
            if (time.Length > 0)
            {
                using (Font f = WaTheme.Text(8.6f))
                using (SolidBrush tb = new SolidBrush(unread > 0 ? WaTheme.Accent : WaTheme.TextGray))
                {
                    SizeF timeSz = g.MeasureString(time, f);
                    g.DrawString(time, f, tb, row.Right - timeSz.Width - WaTheme.S(16),
                        row.Y + WaTheme.S(14));
                }
            }

            // nom
            using (Font f = WaTheme.Semi(11.2f))
            {
                string name = ClipText(g, c.PeerName, f, nameMaxW);
                using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                {
                    g.DrawString(name, f, tb, tx, row.Y + WaTheme.S(12));
                }
            }

            // badges (épinglé / muet) sous l'heure
            float bx = row.Right - WaTheme.S(20);
            if (local.Pinned)
            {
                bx -= WaTheme.S(18);
                WaIcons.Pin(g, bx, row.Y + WaTheme.S(15), WaTheme.S(14), WaTheme.TextGray);
            }
            if (local.Muted)
            {
                bx -= WaTheme.S(18);
                WaIcons.BellMuted(g, bx, row.Y + WaTheme.S(15), WaTheme.S(14), WaTheme.TextGray);
            }

            // badge non-lus
            if (unread > 0)
            {
                string n = unread.ToString();
                using (Font f = WaTheme.Semi(8.2f))
                {
                    SizeF ns = g.MeasureString(n, f);
                    int bw = Math.Max(WaTheme.S(20), (int)ns.Width + WaTheme.S(10));
                    int bh = WaTheme.S(20);
                    Rectangle br = new Rectangle(row.Right - bw - WaTheme.S(14),
                        row.Bottom - WaTheme.S(30), bw, bh);
                    using (SolidBrush bb = new SolidBrush(WaTheme.BrandGreen))
                    {
                        g.FillEllipse(bb, br);
                    }
                    using (SolidBrush wb = new SolidBrush(Color.FromArgb(0x11, 0x1B, 0x21)))
                    {
                        g.DrawString(n, f, wb, br.X + (br.Width - ns.Width) / 2f,
                            br.Y + (br.Height - ns.Height) / 2f);
                    }
                }
            }

            // aperçu
            int prevY = row.Y + WaTheme.S(38);
            float px = tx;
            if (c.Typing)
            {
                using (Font f = WaTheme.Text(9.8f))
                using (SolidBrush tb = new SolidBrush(WaTheme.Accent))
                {
                    g.DrawString("en train d'écrire…", f, tb, px, prevY);
                }
            }
            else if (last != null)
            {
                if (last.IsMine)
                {
                    WaTheme.DrawChecks(g, px, prevY + WaTheme.S(4), last.Status);
                    px += WaTheme.S(20);
                }
                using (Font f = WaTheme.Text(9.8f))
                using (SolidBrush tb = new SolidBrush(local.MarkedUnread ? WaTheme.TextDark : WaTheme.TextGray))
                {
                    int maxW = row.Right - (int)px - WaTheme.S(40) - (unread > 0 ? WaTheme.S(30) : 0);
                    string prev = ClipText(g, PreviewText(c), f, maxW);
                    g.DrawString(prev, f, tb, px, prevY);
                }
            }
            else
            {
                using (Font f = WaTheme.Text(9.8f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    string bio = string.IsNullOrEmpty(c.PeerBio) ? "Démarrer une conversation" : c.PeerBio;
                    g.DrawString(ClipText(g, bio, f, row.Right - tx - WaTheme.S(30)), f, tb, px, prevY);
                }
            }
        }

        /// <summary>Aperçu de la ligne (WhatsApp : « Vous : », Photo, Message vocal…).</summary>
        internal string PreviewText(Conversation c)
        {
            ChatMessage last = c.Last;
            if (last == null) return "";
            if (last.Deleted) return "Ce message a été supprimé";
            string body;
            switch (last.Kind)
            {
                case MessageKind.Voice: body = "Message vocal"; break;
                case MessageKind.Image:
                    body = string.IsNullOrEmpty(last.Text) ? "Photo" : "Photo · " + last.Text;
                    break;
                case MessageKind.File:
                    body = string.IsNullOrEmpty(last.Text)
                        ? (last.FileName ?? "Fichier")
                        : last.FileName + " · " + last.Text;
                    break;
                default:
                    body = (last.Text ?? "").Replace("\r", " ").Replace("\n", " ");
                    break;
            }
            return last.IsMine ? "Vous : " + body : body;
        }

        /// <summary>Rogne un texte à la largeur max (ajoute « … » si besoin).</summary>
        internal static string ClipText(Graphics g, string text, Font f, int maxW)
        {
            if (text == null) return "";
            if (maxW <= 0) return "";
            if (g.MeasureString(text, f).Width <= maxW) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (g.MeasureString(text.Substring(0, mid) + "…", f).Width <= maxW) lo = mid;
                else hi = mid - 1;
            }
            return lo <= 0 ? "" : text.Substring(0, lo) + "…";
        }

        // ---------------------------------------------------------------- souris sidebar
        private int RowIndexAt(Point p)
        {
            Rectangle lr = ListRect();
            if (!lr.Contains(p)) return -1;
            int idx = (p.Y - lr.Y + _listScroll) / WaTheme.S(RowH);
            List<Conversation> list = VisibleConversations();
            return idx >= 0 && idx < list.Count ? idx : -1;
        }

        private void SidebarMouseMove(Point p)
        {
            // entête : boutons
            int hb = -1;
            if (SidebarHeaderRect().Contains(p))
            {
                Rectangle bNew = HeaderBtnRect(0);
                Rectangle bMenu = HeaderBtnRect(1);
                if (Inflated(bNew, 8).Contains(p)) hb = 0;
                else if (Inflated(bMenu, 8).Contains(p)) hb = 1;
            }
            if (hb != _hoverHeaderBtn)
            {
                _hoverHeaderBtn = hb;
                Invalidate(SidebarHeaderRect());
            }

            // chips
            int chip = ChipIndexAt(p);
            if (chip != _hoverChip)
            {
                _hoverChip = chip;
                Invalidate(new Rectangle(SidebarRect().X, WaTheme.S(HeaderH) + WaTheme.S(68),
                    SidebarRect().Width, WaTheme.S(42)));
            }

            // lignes
            int idx = RowIndexAt(p);
            string id = null;
            if (idx >= 0)
            {
                List<Conversation> list = VisibleConversations();
                id = list[idx].PeerId;
            }
            if (id != _hoverRowId)
            {
                _hoverRowId = id;
                Invalidate(ListRect());
            }
        }

        private static Rectangle Inflated(Rectangle r, int px)
        {
            r.Inflate(WaTheme.S(px), WaTheme.S(px));
            return r;
        }

        private int UnreadCount()
        {
            int n = 0;
            foreach (KeyValuePair<string, Conversation> kv in _convs)
            {
                if (kv.Value.Unread > 0 || Cfg.Peer(kv.Key).MarkedUnread) n++;
            }
            return n;
        }

        private int ChipIndexAt(Point p)
        {
            Rectangle side = SidebarRect();
            int cy = WaTheme.S(HeaderH) + WaTheme.S(74);
            int cx = side.X + WaTheme.S(14);
            int unreadCount = UnreadCount();
            string[] labels = new string[]
            {
                "Toutes",
                unreadCount > 0 ? "Non lues " + unreadCount : "Non lues",
                "Favoris"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                int w = WaTheme.S(20 + labels[i].Length * 8);
                Rectangle chip = new Rectangle(cx, cy, w, WaTheme.S(28));
                if (chip.Contains(p)) return i;
                cx += w + WaTheme.S(8);
            }
            return -1;
        }

        private void SidebarMouseDown(Point p, MouseButtons button)
        {
            if (_activeNav == 1) { CallsPageMouseDown(p, button); return; }
            if (_activeNav == 2) { StatusMouseDown(p); return; }
            if (_activeNav == 6) { SettingsMouseDown(p); return; }
            if (_activeNav != 0 && _activeNav != 4) return;
            int hb = _hoverHeaderBtn;
            if (hb == 0)
            {
                ShowNewChatMenu(PointToScreen(p));
                return;
            }
            if (hb == 1)
            {
                Rectangle b = HeaderBtnRect(1);
                ShowSidebarMenu(PointToScreen(new Point(b.Left, b.Bottom + 4)));
                return;
            }
            int chip = ChipIndexAt(p);
            if (chip >= 0)
            {
                if (chip == 0) { _unreadFilter = false; _favoritesFilter = false; }
                else if (chip == 1) { _unreadFilter = true; _favoritesFilter = false; }
                else { _unreadFilter = false; _favoritesFilter = true; }
                _listScroll = 0;
                Invalidate(SidebarRect());
                return;
            }
            int idx = RowIndexAt(p);
            if (idx >= 0)
            {
                List<Conversation> list = VisibleConversations();
                _pressedRowId = list[idx].PeerId;
            }
        }

        private void SidebarMouseUp(Point p, MouseButtons button)
        {
            if (_activeNav == 1) { CallsPageMouseUp(p, button); return; }
            if (_activeNav != 0 && _activeNav != 4) return;
            int idx = RowIndexAt(p);
            if (idx < 0)
            {
                _pressedRowId = null;
                return;
            }
            List<Conversation> list = VisibleConversations();
            Conversation c = list[idx];
            if (button == MouseButtons.Left && _pressedRowId == c.PeerId)
            {
                OpenConversation(c.PeerId);
            }
            else if (button == MouseButtons.Right)
            {
                ShowRowMenu(c, PointToScreen(p));
            }
            _pressedRowId = null;
        }
    }
}
