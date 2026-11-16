using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Suite de la zone de discussion : peinture des bulles (texte, vocal, fichier,
    /// image), citations, réactions, marques de lecture, et état vide type WhatsApp.
    /// </summary>
    public sealed partial class MainForm
    {
        private int _hoverEmptyAction = -1;

        // ================================================================ état vide
        private void PaintEmptyState(Graphics g)
        {
            Rectangle chat = ChatRect();
            using (SolidBrush b = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(b, chat);

            int cx = chat.X + chat.Width / 2;
            int cy = chat.Y + chat.Height / 2;
            using (Font f = WaTheme.Semi(20f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
            {
                string t = "Votre espace de messagerie";
                SizeF sz = g.MeasureString(t, f);
                g.DrawString(t, f, tb, cx - sz.Width / 2f, cy - WaTheme.S(136));
            }
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
            {
                string t = "Échangez directement et de façon chiffrée sur votre réseau local.";
                SizeF sz = g.MeasureString(t, f);
                g.DrawString(t, f, tb, cx - sz.Width / 2f, cy - WaTheme.S(102));
            }

            string[] labels = { "Envoyer un document", "Ajouter un contact", "Inviter quelqu’un", "Assistant P2P" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle card = EmptyActionRect(i);
                int d = WaTheme.S(58);
                Rectangle circle = new Rectangle(card.X + (card.Width - d) / 2, card.Y, d, d);
                using (SolidBrush bg = new SolidBrush(_hoverEmptyAction == i ? WaTheme.Selected : WaTheme.ChatHeaderBg))
                    g.FillEllipse(bg, circle);
                int inset = WaTheme.S(18);
                Color icon = i == 3 ? Color.FromArgb(145, 72, 206) : WaTheme.TextDark;
                if (i == 0) WaIcons.Doc(g, circle.X + inset, circle.Y + inset, d - inset * 2, icon);
                else if (i == 1) WaIcons.Person(g, circle.X + inset, circle.Y + inset, d - inset * 2, icon);
                else if (i == 2) DrawInviteIcon(g, circle, icon);
                else DrawAssistantIcon(g, circle, icon);
                using (Font f = WaTheme.Text(9.5f))
                using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                {
                    SizeF sz = g.MeasureString(labels[i], f);
                    g.DrawString(labels[i], f, tb, card.X + (card.Width - sz.Width) / 2f, circle.Bottom + WaTheme.S(12));
                }
            }
        }

        private Rectangle EmptyActionRect(int index)
        {
            Rectangle chat = ChatRect();
            int cy = chat.Y + chat.Height / 2;
            if (chat.Width >= WaTheme.S(620))
            {
                int cardW = WaTheme.S(136), gap = WaTheme.S(10);
                int left = chat.X + (chat.Width - (cardW * 4 + gap * 3)) / 2;
                return new Rectangle(left + index * (cardW + gap), cy - WaTheme.S(10), cardW, WaTheme.S(112));
            }
            int smallW = WaTheme.S(136), smallGap = WaTheme.S(12);
            int startX = chat.X + (chat.Width - (smallW * 2 + smallGap)) / 2;
            int col = index % 2, row = index / 2;
            return new Rectangle(startX + col * (smallW + smallGap), cy - WaTheme.S(52) + row * WaTheme.S(116), smallW, WaTheme.S(112));
        }

        private static void DrawInviteIcon(Graphics g, Rectangle circle, Color c)
        {
            int x = circle.X + circle.Width / 4, y = circle.Y + circle.Height / 3;
            int w = circle.Width / 2, h = circle.Height / 3;
            using (Pen p = new Pen(c, WaTheme.S(2)))
            {
                g.DrawRectangle(p, x, y, w, h);
                g.DrawLine(p, x, y, x + w / 2, y + h / 2);
                g.DrawLine(p, x + w, y, x + w / 2, y + h / 2);
            }
        }

        private static void DrawAssistantIcon(Graphics g, Rectangle circle, Color c)
        {
            float cx = circle.X + circle.Width / 2f, cy = circle.Y + circle.Height / 2f;
            using (Pen p = new Pen(c, WaTheme.S(2.2f)))
            {
                g.DrawLine(p, cx, cy - WaTheme.S(15), cx, cy + WaTheme.S(15));
                g.DrawLine(p, cx - WaTheme.S(15), cy, cx + WaTheme.S(15), cy);
                g.DrawLine(p, cx - WaTheme.S(10), cy - WaTheme.S(10), cx + WaTheme.S(10), cy + WaTheme.S(10));
                g.DrawLine(p, cx + WaTheme.S(10), cy - WaTheme.S(10), cx - WaTheme.S(10), cy + WaTheme.S(10));
            }
        }

        private void ActivateEmptyAction(int index)
        {
            if (index == 0) _toasts.Show("Ouvrez une discussion pour envoyer un document.");
            else if (index == 1) PromptConnect();
            else if (index == 2)
            {
                try { System.Windows.Forms.Clipboard.SetText("Installez P2PChat et connectez-vous au même réseau local pour discuter."); } catch { }
                _toasts.Show("Invitation copiée.");
            }
            else if (index == 3) _toasts.Show("L’assistant P2P sera ajouté dans une prochaine version.");
        }

        // ================================================================ bulles
        private void PaintBubble(Graphics g, Conversation c, ChatMessage m, Rectangle rect, BubbleLayout lay)
        {
            bool mine = m.IsMine;
            int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
            int r = WaTheme.S(8);
            int authorH = IsGroup(c.PeerId) && !mine ? WaTheme.S(15) : 0;

            if (m.Kind == MessageKind.Image && lay.Thumb != null)
            {
                if (authorH > 0)
                {
                    using (Font f = WaTheme.Semi(8.8f))
                    using (SolidBrush b = new SolidBrush(WaTheme.Accent))
                        g.DrawString(SenderName(m.SenderId), f, b, x + WaTheme.S(PadX), y + WaTheme.S(3));
                }
                PaintImageBubble(g, m, mine, x, y + authorH, w, h - authorH);
            }
            else
            {
                Color bg = mine ? WaTheme.BubbleMine : WaTheme.BubbleOther;
                using (GraphicsPath p = mine ? WaTheme.RoundRectCoins(x, y, w, h, r, 0, r, r)
                                             : WaTheme.RoundRectCoins(x, y, w, h, 0, r, r, r))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);
                PaintTail(g, x, y, mine);

                int padX = WaTheme.S(PadX), padT = WaTheme.S(PadTop);
                int ix = x + padX, iw = Math.Max(1, w - padX * 2);
                int iy = y + padT;

                if (authorH > 0)
                {
                    using (Font f = WaTheme.Semi(8.8f))
                    using (SolidBrush b = new SolidBrush(WaTheme.Accent))
                        g.DrawString(SenderName(m.SenderId), f, b, ix, iy);
                    iy += authorH;
                }

                if (m.ReplyToId != null || m.QuoteText != null)
                    iy += PaintQuote(g, m, mine, ix, iy, iw) + WaTheme.S(4);

                if (m.Deleted)
                {
                    using (Font f = WaTheme.Text(WaTheme.MsgPt))
                    using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                        g.DrawString("Ce message a été supprimé", f, b, ix, iy + WaTheme.S(2));
                }
                else if (m.Kind == MessageKind.Voice)
                    PaintVoiceContent(g, c, m, mine, ix, iy, iw);
                else if (m.Kind == MessageKind.File)
                    PaintFileContent(g, m, mine, ix, iy, iw);
                else
                    PaintTextContent(g, m, mine, ix, iy, iw);

                PaintMeta(g, m, mine, x, y, w, h);
            }

            if (!string.IsNullOrEmpty(m.Reaction))
                PaintReaction(g, m.Reaction, mine, x, y, h);
        }

        private void PaintTextContent(Graphics g, ChatMessage m, bool mine, int ix, int iy, int iw)
        {
            string text = m.Text ?? "";
            using (Font f = WaTheme.Text(WaTheme.MsgPt))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
                g.DrawString(text, f, b, new RectangleF(ix, iy, iw, 2000));
        }

        private void PaintVoiceContent(Graphics g, Conversation c, ChatMessage m, bool mine, int ix, int iy, int iw)
        {
            int ps = WaTheme.S(36);
            Rectangle btn = new Rectangle(ix, iy + WaTheme.S(4), ps, ps);
            bool playing = _player.IsPlaying && c.PlayingId == m.Id;

            Color ic = mine ? WaTheme.MetaMine : WaTheme.IconGray;
            using (SolidBrush cb = new SolidBrush(mine ? Color.FromArgb(55, 0x78, 0x8C, 0x7C) : Color.FromArgb(48, 0x54, 0x65, 0x6F)))
                g.FillEllipse(cb, btn);
            if (playing && !_player.IsPaused)
                WaIcons.Pause(g, btn.X + WaTheme.S(11), btn.Y + WaTheme.S(10), WaTheme.S(16), ic);
            else
                WaIcons.Play(g, btn.X + WaTheme.S(12), btn.Y + WaTheme.S(10), WaTheme.S(16), ic);

            int wx = btn.Right + WaTheme.S(6);
            int wmax = Math.Max(WaTheme.S(30), iw - ps - WaTheme.S(18));
            int bars = 30, gap = WaTheme.S(2);
            int totalGap = (bars - 1) * gap;
            int barW = Math.Max(1, (wmax - totalGap) / bars);
            int used = barW * bars + totalGap;
            int mid = iy + ps / 2 + WaTheme.S(4);
            int barH = WaTheme.S(30);
            float[] amps = DecodeWave(m.Waveform);
            int played = 0;
            if (playing && _player.LengthMs > 0)
                played = (int)((long)bars * _player.ProgressMs / _player.LengthMs);

            for (int i = 0; i < bars; i++)
            {
                float amp = amps == null ? 0.3f + 0.6f * (float)Math.Sin(i * 0.6f) : amps[i];
                int bh = Math.Max(WaTheme.S(4), (int)(amp * barH));
                int bx = wx + i * (barW + gap);
                using (SolidBrush bb = new SolidBrush(i < played ? WaTheme.Accent : ic))
                    g.FillRectangle(bb, bx, mid - bh / 2, barW, bh);
            }

            using (Font f = WaTheme.Text(7.6f))
            using (SolidBrush b = new SolidBrush(ic))
                g.DrawString(WaTheme.FormatDur(m.VoiceDurationSec), f, b, wx + used + WaTheme.S(4), mid - WaTheme.S(7));
        }

        private void PaintFileContent(Graphics g, ChatMessage m, bool mine, int ix, int iy, int iw)
        {
            Color ic = mine ? WaTheme.MetaMine : WaTheme.IconGray;
            WaIcons.Doc(g, ix + WaTheme.S(2), iy + WaTheme.S(2), WaTheme.S(24), ic);
            int tx = ix + WaTheme.S(34);
            using (Font f = WaTheme.Semi(9.8f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
                g.DrawString(ClipText(g, m.FileName ?? "Fichier", f, iw - WaTheme.S(36)), f, b, tx, iy + WaTheme.S(2));
            using (Font f = WaTheme.Text(8.4f))
            using (SolidBrush b = new SolidBrush(ic))
                g.DrawString(WaTheme.FormatSize(m.FileSize), f, b, tx, iy + WaTheme.S(26));
        }

        private void PaintImageBubble(Graphics g, ChatMessage m, bool mine, int x, int y, int w, int h)
        {
            Image img = GetImage(m);
            int capH = string.IsNullOrEmpty(m.Text) ? 0 : WaTheme.S(24);
            int imgH = Math.Max(1, h - capH);
            GraphicsState state = g.Save();
            using (GraphicsPath p = WaTheme.RoundRect(x, y, w, imgH, WaTheme.S(8)))
            {
                g.SetClip(p, CombineMode.Replace);
                if (img != null) g.DrawImage(img, x, y, w, imgH);
                else using (SolidBrush bb = new SolidBrush(WaTheme.BubbleOther)) g.FillRectangle(bb, x, y, w, imgH);
            }
            g.Restore(state);
            if (capH > 0)
            {
                using (Font f = WaTheme.Text(9.4f))
                using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
                    g.DrawString(ClipText(g, m.Text, f, w - WaTheme.S(16)), f, b, x + WaTheme.S(10), y + imgH + WaTheme.S(2));
            }
            PaintMeta(g, m, mine, x, y + imgH - WaTheme.S(2), w, h - imgH + WaTheme.S(2));
        }

        private Image GetImage(ChatMessage m)
        {
            if (m == null) return null;
            Conversation c = Selected();
            BubbleLayout lay;
            if (c != null && c.Layouts.TryGetValue(m.Id, out lay)) return lay.Thumb;
            return null;
        }

        private int PaintQuote(Graphics g, ChatMessage m, bool mine, int ix, int iy, int iw)
        {
            int qh = WaTheme.S(42);
            Color qbg = mine ? WaTheme.QuoteMine : WaTheme.QuoteOther;
            using (GraphicsPath p = WaTheme.RoundRect(ix, iy, iw, qh, WaTheme.S(4)))
            using (SolidBrush b = new SolidBrush(qbg))
                g.FillPath(b, p);
            using (SolidBrush bar = new SolidBrush(WaTheme.Accent))
                g.FillRectangle(bar, ix, iy, WaTheme.S(3), qh);
            using (Font f = WaTheme.Semi(8.8f))
            using (SolidBrush b = new SolidBrush(WaTheme.Accent))
                g.DrawString(m.QuoteAuthor ?? "", f, b, ix + WaTheme.S(10), iy + WaTheme.S(4));
            using (Font f = WaTheme.Text(8.2f))
            using (SolidBrush b = new SolidBrush(mine ? WaTheme.MetaMine : WaTheme.TextGray))
                g.DrawString(ClipText(g, m.QuoteText ?? "", f, iw - WaTheme.S(20)), f, b, ix + WaTheme.S(10), iy + WaTheme.S(20));
            return qh;
        }

        private void PaintMeta(Graphics g, ChatMessage m, bool mine, float bx, float by, float bw, float bh)
        {
            string time = WaTheme.FormatTime(m.TimestampUtc);
            Color c = mine ? WaTheme.MetaMine : WaTheme.MetaOther;
            using (Font f = WaTheme.Text(7.4f))
            {
                float timeW = g.MeasureString(time, f).Width;
                float checksW = m.IsMine ? WaTheme.ChecksWidth(m.Status) : 0;
                float tx = bx + bw - WaTheme.S(PadX) - timeW - (m.IsMine ? WaTheme.S(3) + checksW : 0);
                float ty = by + bh - WaTheme.S(13);
                using (SolidBrush b = new SolidBrush(c))
                    g.DrawString(time, f, b, tx, ty);
                if (m.IsMine)
                    WaTheme.DrawChecks(g, tx + timeW + WaTheme.S(3), ty + WaTheme.S(4), m.Status);
            }
        }

        private void PaintReaction(Graphics g, string emoji, bool mine, int x, int y, int h)
        {
            int rr = WaTheme.S(20);
            Rectangle chip = new Rectangle(mine ? x - WaTheme.S(14) : x + WaTheme.S(6), y + h + WaTheme.S(2), WaTheme.S(26), rr);
            using (GraphicsPath p = WaTheme.RoundRect(chip.X, chip.Y, chip.Width, chip.Height, WaTheme.S(10)))
            using (SolidBrush b = new SolidBrush(WaTheme.Selected))
                g.FillPath(b, p);
            using (Font f = WaTheme.Emoji(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
                g.DrawString(emoji, f, b, chip.X + WaTheme.S(4), chip.Y + WaTheme.S(1));
        }

        private float[] DecodeWave(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(b64);
                float[] a = new float[Math.Min(30, data.Length)];
                for (int i = 0; i < a.Length; i++) a[i] = Math.Max(0.06f, data[i] / 255f);
                return a;
            }
            catch { return null; }
        }
    }
}
