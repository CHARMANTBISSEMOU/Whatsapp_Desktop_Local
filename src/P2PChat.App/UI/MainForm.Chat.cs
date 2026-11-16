using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Zone de discussion : entête, fil de messages (bulles), recherche, état vide.</summary>
    public sealed partial class MainForm
    {
        // ---------------------------------------------------------------- peinture globale
        private void PaintChat(Graphics g)
        {
            Conversation c = Selected();
            if (c == null) return;
            Rectangle chat = ChatRect();

            PaintStream(g, c);
            PaintChatHeader(g, c);
        }

        private void PaintChatHeader(Graphics g, Conversation c)
        {
            Rectangle h = ChatHeaderRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.ChatHeaderBg))
            {
                g.FillRectangle(bg, h);
            }

            int av = WaTheme.S(40);
            Rectangle avR = new Rectangle(h.X + WaTheme.S(16), h.Y + (h.Height - av) / 2, av, av);
            WaTheme.DrawAvatar(g, avR, c.PeerName, null);

            // nom
            int tx = avR.Right + WaTheme.S(15);
            using (Font f = WaTheme.Semi(11.4f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
            {
                g.DrawString(ClipText(g, c.PeerName, f, h.Width - WaTheme.S(180)), f, tb, tx, h.Y + WaTheme.S(11));
            }

            // sous-titre : en train d'écrire… / en ligne / dernier accès
            string sub;
            Color subColor = WaTheme.TextGray;
            if (c.Typing && IsGroup(c.PeerId))
            {
                sub = (string.IsNullOrEmpty(c.TypingPeerName) ? "Un membre" : c.TypingPeerName) + " écrit…";
                subColor = WaTheme.Accent;
            }
            else if (IsGroup(c.PeerId))
            {
                sub = GroupMemberCount(c.PeerId).ToString() + " membres";
            }
            else if (c.Typing)
            {
                sub = "en train d'écrire…";
                subColor = WaTheme.Accent;
            }
            else if (c.Online)
            {
                sub = "en ligne";
            }
            else
            {
                sub = "hors ligne";
            }
            using (Font f = WaTheme.Text(9.2f))
            using (SolidBrush tb = new SolidBrush(subColor))
            {
                g.DrawString(sub, f, tb, tx, h.Y + WaTheme.S(32));
            }

            // boutons droite : recherche + menu
            Rectangle bSearch = HeaderIconRect(1);
            Rectangle bMenu = HeaderIconRect(0);
            Rectangle bCall = HeaderIconRect(2);
            Color ic = WaTheme.IconGray;
            if (_hoverChatBtn == 1)
            {
                using (SolidBrush hb = new SolidBrush(WaTheme.Hover))
                {
                    g.FillEllipse(hb, bSearch.X - WaTheme.S(8), bSearch.Y - WaTheme.S(8),
                        bSearch.Width + WaTheme.S(16), bSearch.Height + WaTheme.S(16));
                }
            }
            if (_hoverChatBtn == 0)
            {
                using (SolidBrush hb = new SolidBrush(WaTheme.Hover))
                {
                    g.FillEllipse(hb, bMenu.X - WaTheme.S(8), bMenu.Y - WaTheme.S(8),
                        bMenu.Width + WaTheme.S(16), bMenu.Height + WaTheme.S(16));
                }
            }
            if (_hoverChatBtn == 2 && !IsGroup(c.PeerId))
            {
                using (SolidBrush hb = new SolidBrush(WaTheme.Hover))
                    g.FillEllipse(hb, bCall.X - WaTheme.S(8), bCall.Y - WaTheme.S(8),
                        bCall.Width + WaTheme.S(16), bCall.Height + WaTheme.S(16));
            }
            WaIcons.Search(g, bSearch.X, bSearch.Y, bSearch.Width, ic);
            WaIcons.MenuDots(g, bMenu.X, bMenu.Y, bMenu.Height, ic);
            using (Font callFont = new Font("Segoe UI Symbol", WaTheme.S(17f), FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush callBrush = new SolidBrush(IsGroup(c.PeerId) ? WaTheme.TextGray : ic))
                g.DrawString("☎", callFont, callBrush, bCall.X - WaTheme.S(1), bCall.Y - WaTheme.S(1));
        }

        /// <summary>Position des icônes droites de l'entête de discussion (0 = le plus à droite).</summary>
        internal Rectangle HeaderIconRect(int idx)
        {
            Rectangle h = ChatHeaderRect();
            int s = WaTheme.S(22), pad = WaTheme.S(12);
            int right = h.Right - pad - s;
            return new Rectangle(right - idx * (s + pad), h.Y + (h.Height - s) / 2, s, s);
        }

        private void PaintChatSearchBar(Graphics g)
        {
            Rectangle r = ChatSearchBarRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.ChatHeaderBg))
            {
                g.FillRectangle(bg, r);
            }
            Rectangle pill = new Rectangle(r.X + WaTheme.S(16), r.Y + WaTheme.S(5),
                r.Width - WaTheme.S(80), r.Height - WaTheme.S(10));
            using (SolidBrush b = new SolidBrush(WaTheme.SearchBg))
            using (GraphicsPath p = WaTheme.RoundRect(pill.X, pill.Y, pill.Width, pill.Height, WaTheme.S(8)))
            {
                g.FillPath(b, p);
            }
            WaIcons.Search(g, pill.X + WaTheme.S(12), pill.Y + WaTheme.S(8), WaTheme.S(16), WaTheme.IconGray);
            // bouton fermer
            Rectangle x = new Rectangle(r.Right - WaTheme.S(52), r.Y + WaTheme.S(10), WaTheme.S(24), WaTheme.S(24));
            WaIcons.Close(g, x.X + WaTheme.S(4), x.Y + WaTheme.S(4), WaTheme.S(16), WaTheme.IconGray);
        }

        // ---------------------------------------------------------------- fil : items
        /// <summary>Élément du fil : bandeau chiffré, puce de date ou bulle.</summary>
        private struct StreamItem
        {
            public int Kind;          // 0 = chiffrement, 1 = date, 2 = bulle
            public Rectangle Rect;    // en coordonnées « contenu » (0 = haut du fil)
            public string Label;
            public ChatMessage Msg;
        }

        private List<StreamItem> _items;
        private string _itemsConvId;
        private int _itemsWidth;
        private int _itemsMsgCount = -1;

        /// <summary>Construit (et met en cache) la liste des éléments du fil.</summary>
        private List<StreamItem> EnsureItems(Conversation c, int streamW)
        {
            if (_items != null && _itemsConvId == c.PeerId && _itemsWidth == streamW &&
                _itemsMsgCount == c.Messages.Count && c.ContentH > 0 &&
                _layoutsVersion == c.Layouts.Count)
            {
                return _items;
            }
            using (Graphics g = CreateGraphics())
            {
                _items = BuildItems(g, c, streamW);
            }
            _itemsConvId = c.PeerId;
            _itemsWidth = streamW;
            _itemsMsgCount = c.Messages.Count;
            _layoutsVersion = c.Layouts.Count;
            int total = 0;
            foreach (StreamItem it in _items) total = Math.Max(total, it.Rect.Bottom);
            total += WaTheme.S(14);
            c.ContentH = total;
            return _items;
        }

        private int _layoutsVersion = -1;

        private void MeasureStream(Conversation c)
        {
            EnsureItems(c, StreamRect().Width);
        }

        private List<StreamItem> BuildItems(Graphics g, Conversation c, int streamW)
        {
            var items = new List<StreamItem>();
            int y = WaTheme.S(8);

            // bandeau chiffrement
            StreamItem eb;
            eb.Kind = 0;
            eb.Label = null;
            eb.Msg = null;
            eb.Rect = new Rectangle(0, y, streamW, WaTheme.S(64));
            items.Add(eb);
            y += WaTheme.S(64) + WaTheme.S(10);

            long lastDay = -1;
            foreach (ChatMessage m in c.Messages)
            {
                long day = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMilliseconds(m.TimestampUtc).ToLocalTime().Date.Ticks;
                if (day != lastDay)
                {
                    StreamItem di;
                    di.Kind = 1;
                    di.Label = WaTheme.FormatDateHeader(m.TimestampUtc);
                    di.Msg = null;
                    di.Rect = new Rectangle(0, y, streamW, WaTheme.S(34));
                    items.Add(di);
                    y += WaTheme.S(34) + WaTheme.S(6);
                    lastDay = day;
                }

                BubbleLayout lay;
                if (!c.Layouts.TryGetValue(m.Id, out lay))
                {
                    lay = MeasureBubble(g, m, streamW);
                    c.Layouts[m.Id] = lay;
                }
                bool reaction = !string.IsNullOrEmpty(m.Reaction);
                int h = lay.H + (reaction ? WaTheme.S(14) : 0);

                StreamItem bi;
                bi.Kind = 2;
                bi.Label = null;
                bi.Msg = m;
                int x = m.IsMine
                    ? streamW - lay.W - WaTheme.S(68)
                    : WaTheme.S(62);
                x = Math.Max(WaTheme.S(10), x);
                bi.Rect = new Rectangle(x, y, lay.W, h);
                items.Add(bi);
                y += h + WaTheme.S(4);
            }
            return items;
        }

        /// <summary>Décale les coordonnées « contenu » vers l'écran.</summary>
        private int StreamOffset(Conversation c, Rectangle st)
        {
            if (c.ContentH <= st.Height) return st.Bottom - c.ContentH;
            return st.Top - _chatScroll;
        }

        // ---------------------------------------------------------------- fil : mesure des bulles
        private const int PadX = 9, PadTop = 6, PadBot = 8;

        private bool IsEmojiOnly(string t)
        {
            if (string.IsNullOrEmpty(t) || t.Length > 8) return false;
            foreach (char ch in t)
            {
                if (ch < 0x2000 && ch != ' ' && ch != '\n') return false;
            }
            return true;
        }

        private BubbleLayout MeasureBubble(Graphics g, ChatMessage m, int streamW)
        {
            BubbleLayout lay = new BubbleLayout();
            Conversation activeConversation = Selected();
            int authorH = activeConversation != null && IsGroup(activeConversation.PeerId) && !m.IsMine
                ? WaTheme.S(15) : 0;
            int maxW = Math.Min(streamW - WaTheme.S(120), WaTheme.S(560));
            int maxTextW = maxW - WaTheme.S(PadX * 2);

            string meta = WaTheme.FormatTime(m.TimestampUtc);
            int metaW;
            using (Font mf = WaTheme.Text(7.4f))
            {
                metaW = (int)g.MeasureString(meta, mf).Width + WaTheme.S(6) +
                        WaTheme.ChecksWidth(m.IsMine ? m.Status : DeliveryStatus.Sent);
            }
            int quoteH = 0;
            if (m.ReplyToId != null || m.QuoteText != null) quoteH = WaTheme.S(46);

            bool emojiOnly = m.Kind == MessageKind.Text && IsEmojiOnly(m.Text) && m.ReplyToId == null;
            using (Font f = emojiOnly ? WaTheme.Emoji(WaTheme.MsgEmojiPt) : WaTheme.Text(WaTheme.MsgPt))
            {
                lay.LineH = g.MeasureString("Ag", f).Height;
                string text = m.Deleted ? "Ce message a été supprimé" : (m.Text ?? "");
                if (m.Kind == MessageKind.Text || m.Deleted)
                {
                    if (text.Length == 0)
                    {
                        lay.W = WaTheme.S(84);
                        lay.H = WaTheme.S(PadTop + PadBot) + WaTheme.S(16) + quoteH + authorH;
                        lay.TextW = WaTheme.S(40);
                        lay.Lines = 1;
                        lay.MetaInline = false;
                        return lay;
                    }
                    SizeF sz = g.MeasureString(text, f, maxTextW);
                    lay.Lines = Math.Max(1, (int)Math.Ceiling(sz.Height / lay.LineH));
                    bool single = lay.Lines == 1 && (int)Math.Ceiling(sz.Width) <= maxTextW;
                    if (single && sz.Width + metaW + WaTheme.S(6) <= maxTextW)
                    {
                        lay.MetaInline = true;
                        lay.TextW = (int)Math.Ceiling(sz.Width);
                        lay.W = lay.TextW + metaW + WaTheme.S(PadX * 2 + 6);
                        lay.LastLineW = lay.TextW;
                        lay.H = quoteH + WaTheme.S(PadTop) + (int)Math.Ceiling(lay.LineH) + WaTheme.S(PadBot) + authorH;
                    }
                    else
                    {
                        lay.MetaInline = false;
                        lay.TextW = Math.Min(maxTextW, (int)Math.Ceiling(sz.Width));
                        lay.W = lay.TextW + WaTheme.S(PadX * 2);
                        string[] lines = text.Split('\n');
                        string lastLine = lines[lines.Length - 1].TrimEnd('\r');
                        lay.LastLineW = Math.Min(lay.TextW, g.MeasureString(lastLine, f).Width);
                        lay.H = quoteH + WaTheme.S(PadTop) + (int)Math.Ceiling(sz.Height) + WaTheme.S(14 + PadBot) + authorH;
                    }
                    lay.W = Math.Max(lay.W, WaTheme.S(84));
                    return lay;
                }
                if (m.Kind == MessageKind.Voice)
                {
                    lay.W = Math.Min(maxW, WaTheme.S(310));
                    lay.TextW = lay.W - WaTheme.S(PadX * 2);
                    lay.H = WaTheme.S(PadTop + PadBot) + WaTheme.S(44) + quoteH + WaTheme.S(16) + authorH;
                    return lay;
                }
                if (m.Kind == MessageKind.Image)
                {
                    int tw = Math.Min(maxW, WaTheme.S(330));
                    int th = WaTheme.S(200);
                    Image img = GetThumb(lay, m);
                    if (img != null)
                    {
                        double ratio = Math.Min((double)tw / img.Width, (double)th / img.Height);
                        tw = Math.Max(WaTheme.S(120), (int)(img.Width * ratio));
                        th = Math.Max(WaTheme.S(90), (int)(img.Height * ratio));
                    }
                    int capH = 0;
                    if (!string.IsNullOrEmpty(m.Text))
                    {
                        using (Font cf = WaTheme.Text(9.6f))
                        {
                            SizeF cs = g.MeasureString(m.Text, cf, tw - WaTheme.S(12));
                            capH = (int)Math.Ceiling(cs.Height) + WaTheme.S(4);
                        }
                    }
                    lay.W = tw;
                    lay.TextW = tw;
                    lay.H = th + capH + WaTheme.S(22) + authorH;
                    return lay;
                }
                // fichier
                lay.W = Math.Min(maxW, WaTheme.S(330));
                lay.TextW = lay.W - WaTheme.S(PadX * 2);
                lay.H = WaTheme.S(PadTop + PadBot) + WaTheme.S(48) + quoteH + WaTheme.S(18) + authorH;
                return lay;
            }
        }

        /// <summary>Charge (et met en cache) la miniature d'une image.</summary>
        private Image GetThumb(BubbleLayout lay, ChatMessage m)
        {
            if (lay.Thumb != null) return lay.Thumb;
            if (lay.ThumbFailed || string.IsNullOrEmpty(m.LocalPath)) return null;
            try
            {
                if (!File.Exists(m.LocalPath)) { lay.ThumbFailed = true; return null; }
                using (var fs = new FileStream(m.LocalPath, FileMode.Open, FileAccess.Read))
                {
                    lay.Thumb = Image.FromStream(fs);
                }
                return lay.Thumb;
            }
            catch
            {
                lay.ThumbFailed = true;
                return null;
            }
        }

        // ---------------------------------------------------------------- fil : peinture
        private int _hoverChatBtn = -1;
        private string _hoverBubbleId;

        private void PaintStream(Graphics g, Conversation c)
        {
            Rectangle st = StreamRect();
            GraphicsState clip = g.Save();
            g.SetClip(st);

            WaWallpaper.PaintRegion(g, st, _chatScroll);

            List<StreamItem> items = EnsureItems(c, st.Width);
            int off = StreamOffset(c, st);
            foreach (StreamItem it in items)
            {
                int y = it.Rect.Y + off;
                if (y > st.Bottom || y + it.Rect.Height < st.Top) continue;
                Rectangle r = new Rectangle(st.X + it.Rect.X, y, it.Rect.Width, it.Rect.Height);
                if (it.Kind == 0) PaintEncryptBanner(g, r);
                else if (it.Kind == 1) PaintDateChip(g, r, it.Label);
                else
                {
                    BubbleLayout lay;
                    if (c.Layouts.TryGetValue(it.Msg.Id, out lay))
                    {
                        PaintBubble(g, c, it.Msg, new Rectangle(r.X, r.Y, lay.W, lay.H), lay);
                    }
                }
            }

            // indicateur « en train d'écrire… » (bulle entrante avec 3 points animés)
            if (c.Typing)
            {
                int bw = WaTheme.S(64), bh = WaTheme.S(36);
                int by = st.Bottom - bh - WaTheme.S(16);
                int bx = st.X + WaTheme.S(62);
                using (SolidBrush bb = new SolidBrush(WaTheme.BubbleOther))
                using (GraphicsPath p = WaTheme.RoundRectCoins(bx, by, bw, bh,
                    0, WaTheme.S(9), WaTheme.S(9), WaTheme.S(9)))
                {
                    g.FillPath(bb, p);
                }
                PaintTail(g, bx, by, false);
                int phase = (Environment.TickCount / 300) % 3;
                for (int i = 0; i < 3; i++)
                {
                    int dy = i == phase ? -WaTheme.S(4) : 0;
                    int d = WaTheme.S(7);
                    int dx = bx + WaTheme.S(15) + i * WaTheme.S(14);
                    using (SolidBrush db = new SolidBrush(WaTheme.TextGray))
                    {
                        g.FillEllipse(db, dx, by + (bh - d) / 2 + dy, d, d);
                    }
                }
            }

            // barre de défilement fine
            int max = ChatScrollMax(c);
            if (max > 0)
            {
                int trackH = st.Height;
                int thumbH = Math.Max(WaTheme.S(36), trackH * trackH / Math.Max(1, c.ContentH));
                int thumbY = st.Y + (trackH - thumbH) * _chatScroll / max;
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                using (GraphicsPath p = WaTheme.RoundRect(st.Right - WaTheme.S(9), thumbY,
                    WaTheme.S(5), thumbH, WaTheme.S(2.5f)))
                {
                    g.FillPath(sb, p);
                }
            }

            g.Restore(clip);
        }

        private void PaintDateChip(Graphics g, Rectangle r, string label)
        {
            using (Font f = WaTheme.Text(8.4f))
            {
                SizeF sz = g.MeasureString(label, f);
                int w = (int)sz.Width + WaTheme.S(24);
                int h = WaTheme.S(26);
                int x = r.X + (r.Width - w) / 2;
                int y = r.Y + WaTheme.S(4);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(14, 0, 0, 0)))
                using (GraphicsPath sp = WaTheme.RoundRect(x + 1, y + 2, w, h, WaTheme.S(13)))
                {
                    g.FillPath(sh, sp);
                }
                using (SolidBrush bb = new SolidBrush(WaTheme.ChipBg))
                using (GraphicsPath p = WaTheme.RoundRect(x, y, w, h, WaTheme.S(13)))
                {
                    g.FillPath(bb, p);
                }
                using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                {
                    g.DrawString(label, f, tb, x + (w - sz.Width) / 2f, y + (h - sz.Height) / 2f);
                }
            }
        }

        private void PaintEncryptBanner(Graphics g, Rectangle r)
        {
            using (Font f = WaTheme.Text(8.6f))
            {
                string line1 = "Les messages sont chiffrés de bout en bout.";
                string line2 = "Personne en dehors de cette conversation ne peut les lire.";
                SizeF s1 = g.MeasureString(line1, f);
                SizeF s2 = g.MeasureString(line2, f);
                int w = (int)Math.Max(s1.Width, s2.Width) + WaTheme.S(56);
                int h = WaTheme.S(56);
                int x = r.X + (r.Width - w) / 2;
                int y = r.Y;
                using (SolidBrush bb = new SolidBrush(WaTheme.EncryptBg))
                using (GraphicsPath p = WaTheme.RoundRect(x, y, w, h, WaTheme.S(9)))
                {
                    g.FillPath(bb, p);
                }
                WaIcons.Lock(g, x + w / 2f - WaTheme.S(6), y + WaTheme.S(7), WaTheme.S(12),
                    Color.FromArgb(0x54, 0x65, 0x6F));
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(0x54, 0x65, 0x6F)))
                {
                    g.DrawString(line1, f, tb, x + (w - s1.Width) / 2f, y + WaTheme.S(21));
                    g.DrawString(line2, f, tb, x + (w - s2.Width) / 2f, y + WaTheme.S(36));
                }
            }
        }

        /// <summary>Queue de bulle WhatsApp (petit triangle en haut à gauche/droite).</summary>
        private void PaintTail(Graphics g, int bx, int by, bool mine)
        {
            using (SolidBrush bb = new SolidBrush(mine ? WaTheme.BubbleMine : WaTheme.BubbleOther))
            {
                if (mine)
                {
                    g.FillPolygon(bb, new PointF[]
                    {
                        new PointF(bx, by),
                        new PointF(bx + WaTheme.S(8), by),
                        new PointF(bx, by + WaTheme.S(12)),
                    });
                }
                else
                {
                    g.FillPolygon(bb, new PointF[]
                    {
                        new PointF(bx, by),
                        new PointF(bx - WaTheme.S(8), by),
                        new PointF(bx, by + WaTheme.S(12)),
                    });
                }
            }
        }

    }
}
