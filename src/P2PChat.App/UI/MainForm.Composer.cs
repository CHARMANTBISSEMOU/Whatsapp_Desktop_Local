using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;
using P2PChat.Core.Text;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Composeur type WhatsApp : boutons emoji / pièce jointe / micro-envoi,
    /// panneau d'emojis, saisie, citations, envoi de texte, fichiers et vocaux.
    /// </summary>
    public sealed partial class MainForm
    {
        // ------------------------------------------------------------ géométrie boutons
        private int ComposerPillCenterY()
        {
            Rectangle r = ChatRect();
            return r.Bottom - ComposerHeight() / 2;
        }

        private Rectangle EmojiBtnRect()
        {
            Rectangle r = ChatRect();
            int cy = ComposerPillCenterY();
            return new Rectangle(r.X + WaTheme.S(12), cy - WaTheme.S(19), WaTheme.S(38), WaTheme.S(38));
        }

        private Rectangle AttachBtnRect()
        {
            Rectangle r = ChatRect();
            int cy = ComposerPillCenterY();
            return new Rectangle(r.X + WaTheme.S(56), cy - WaTheme.S(19), WaTheme.S(38), WaTheme.S(38));
        }

        private Rectangle SendBtnRect()
        {
            Rectangle r = ChatRect();
            int cy = ComposerPillCenterY();
            return new Rectangle(r.Right - WaTheme.S(64), cy - WaTheme.S(22), WaTheme.S(44), WaTheme.S(44));
        }

        private Rectangle CloseReplyBtnRect()
        {
            Rectangle b = ReplyBarRect();
            return new Rectangle(b.Right - WaTheme.S(34), b.Y, WaTheme.S(32), b.Height);
        }

        // ------------------------------------------------------------ peinture
        private void PaintComposer(Graphics g)
        {
            Rectangle r = ChatRect();
            int ch = ComposerHeight();
            using (SolidBrush bg = new SolidBrush(WaTheme.ChatHeaderBg))
                g.FillRectangle(bg, r.X, r.Bottom - ch, r.Width, ch);

            Conversation c = Selected();
            if (c != null && c.ReplyTo != null) PaintReplyBar(g, c);

            Rectangle eb = EmojiBtnRect();
            if (_emojiOpen) using (SolidBrush hb = new SolidBrush(WaTheme.Hover)) g.FillEllipse(hb, eb);
            WaIcons.Emoji(g, eb.X + WaTheme.S(6), eb.Y + WaTheme.S(6), WaTheme.S(26),
                _emojiOpen ? WaTheme.Accent : WaTheme.IconGray);

            Rectangle ab = AttachBtnRect();
            if (_recording)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(40, 0xF1, 0x5C, 0x6D))) g.FillEllipse(hb, ab);
                WaIcons.Trash(g, ab.X + WaTheme.S(7), ab.Y + WaTheme.S(7), WaTheme.S(24), WaTheme.RecordRed);
            }
            else
            {
                DrawClip(g, ab.X + WaTheme.S(7), ab.Y + WaTheme.S(7), WaTheme.S(24), WaTheme.IconGray);
            }

            bool hasText = _input != null && _input.Text.Trim().Length > 0;
            Rectangle sb = SendBtnRect();
            using (SolidBrush b = new SolidBrush(_recording ? WaTheme.RecordRed : WaTheme.Accent))
                g.FillEllipse(b, sb);
            if (_recording || hasText)
                WaIcons.Send(g, sb.X + WaTheme.S(10), sb.Y + WaTheme.S(10), WaTheme.S(24), Color.White);
            else
                WaIcons.Mic(g, sb.X + WaTheme.S(10), sb.Y + WaTheme.S(10), WaTheme.S(24), Color.White);

            if (_recording)
            {
                using (Font f = WaTheme.Semi(10f))
                using (SolidBrush tb = new SolidBrush(WaTheme.RecordRed))
                    g.DrawString("● " + WaTheme.FormatDur(_rec.ElapsedSeconds), f, tb, sb.X - WaTheme.S(68), sb.Y + WaTheme.S(12));
            }
        }

        private void PaintReplyBar(Graphics g, Conversation c)
        {
            Rectangle b = ReplyBarRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.Selected)) g.FillRectangle(bg, b);
            using (SolidBrush bar = new SolidBrush(WaTheme.Accent)) g.FillRectangle(bar, b.X, b.Y, WaTheme.S(4), b.Height);
            using (Font f = WaTheme.Semi(9.4f))
            using (SolidBrush tb = new SolidBrush(WaTheme.Accent))
                g.DrawString("Répondre à " + (c.ReplyTo.QuoteAuthor ?? ""), f, tb, b.X + WaTheme.S(12), b.Y + WaTheme.S(6));
            using (Font f = WaTheme.Text(8.8f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                g.DrawString(ClipText(g, c.ReplyTo.QuoteText ?? (c.ReplyTo.Text ?? ""), f, b.Width - WaTheme.S(64)), f, tb, b.X + WaTheme.S(12), b.Y + WaTheme.S(28));
            Rectangle xb = CloseReplyBtnRect();
            WaIcons.Close(g, xb.X + WaTheme.S(8), xb.Y + (xb.Height - WaTheme.S(18)) / 2, WaTheme.S(18), WaTheme.IconGray);
        }

        private static void DrawClip(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = new Pen(c, Math.Max(1f, 1.6f * WaTheme.Dpi)))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath outer = new GraphicsPath())
                using (GraphicsPath inner = new GraphicsPath())
                {
                    outer.StartFigure();
                    outer.AddBezier(x + s * .68f, y + s * .14f, x + s * .88f, y + s * .28f,
                        x + s * .78f, y + s * .46f, x + s * .61f, y + s * .63f);
                    outer.AddLine(x + s * .61f, y + s * .63f, x + s * .43f, y + s * .81f);
                    outer.AddBezier(x + s * .43f, y + s * .81f, x + s * .18f, y + s * .72f,
                        x + s * .22f, y + s * .53f, x + s * .39f, y + s * .36f);
                    outer.AddLine(x + s * .39f, y + s * .36f, x + s * .62f, y + s * .13f);
                    inner.StartFigure();
                    inner.AddBezier(x + s * .63f, y + s * .30f, x + s * .69f, y + s * .34f,
                        x + s * .65f, y + s * .40f, x + s * .56f, y + s * .50f);
                    inner.AddLine(x + s * .56f, y + s * .50f, x + s * .40f, y + s * .66f);
                    g.DrawPath(p, outer);
                    g.DrawPath(p, inner);
                }
            }
        }

        // ------------------------------------------------------------ panneau emojis
        private void PaintEmojiPanel(Graphics g)
        {
            Rectangle p = EmojiRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.ChatHeaderBg)) g.FillRectangle(bg, p);
            using (Pen bo = new Pen(WaTheme.Divider)) g.DrawRectangle(bo, p);

            string[] cats = EmojiCatalog.Categories;
            int tabH = WaTheme.S(38);
            int tabW = p.Width / cats.Length;
            for (int i = 0; i < cats.Length; i++)
            {
                Rectangle t = new Rectangle(p.X + i * tabW, p.Y, tabW, tabH);
                using (SolidBrush b = new SolidBrush(i == _emojiCat ? WaTheme.Selected : WaTheme.ChatHeaderBg))
                    g.FillRectangle(b, t);
                using (Font f = WaTheme.Text(8.6f))
                using (SolidBrush tb = new SolidBrush(i == _emojiCat ? WaTheme.Accent : WaTheme.TextGray))
                {
                    SizeF sz = g.MeasureString(cats[i], f);
                    g.DrawString(cats[i], f, tb, t.X + (t.Width - sz.Width) / 2f, t.Y + (t.Height - sz.Height) / 2f);
                }
            }

            IReadOnlyList<string> list = EmojiCatalog.Get(cats[_emojiCat]);
            int cols = Math.Max(1, (p.Width - WaTheme.S(12)) / WaTheme.S(42));
            int rowH = WaTheme.S(42);
            int gridTop = p.Y + tabH + WaTheme.S(6);
            int visibleH = p.Height - tabH - WaTheme.S(12);
            int maxScroll = Math.Max(0, (int)Math.Ceiling((double)list.Count / cols) * rowH - visibleH);
            if (_emojiScroll > maxScroll) _emojiScroll = maxScroll;

            GraphicsState state = g.Save();
            g.SetClip(new Rectangle(p.X, gridTop, p.Width, visibleH));
            using (Font f = WaTheme.Emoji(16f))
            using (SolidBrush eb = new SolidBrush(Color.Black))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    int row = i / cols, col = i % cols;
                    int ex = p.X + col * WaTheme.S(42) + WaTheme.S(8);
                    int ey = gridTop + row * rowH - _emojiScroll;
                    if (ey + rowH >= gridTop && ey <= gridTop + visibleH)
                        EmojiRasterizer.Draw(g, list[i], f, eb,
                            new RectangleF(ex, ey + WaTheme.S(5), WaTheme.S(30), WaTheme.S(30)));
                }
            }
            g.Restore(state);
        }

        private void EmojiPanelClick(Point p)
        {
            Rectangle r = EmojiRect();
            string[] cats = EmojiCatalog.Categories;
            int tabW = r.Width / cats.Length;
            int tabH = WaTheme.S(38);
            if (p.Y < r.Y + tabH)
            {
                int idx = (p.X - r.X) / tabW;
                if (idx >= 0 && idx < cats.Length) { _emojiCat = idx; _emojiScroll = 0; Invalidate(EmojiRect()); }
                return;
            }
            int cols = Math.Max(1, (r.Width - WaTheme.S(12)) / WaTheme.S(42));
            int rowH = WaTheme.S(42);
            int gridTop = r.Y + tabH + WaTheme.S(6);
            IReadOnlyList<string> list = EmojiCatalog.Get(cats[_emojiCat]);
            int col = (p.X - r.X - WaTheme.S(8)) / WaTheme.S(42);
            int row = (p.Y - gridTop + _emojiScroll) / rowH;
            int i = row * cols + col;
            if (col >= 0 && row >= 0 && i >= 0 && i < list.Count) InsertEmoji(list[i]);
        }

        private void ToggleEmoji()
        {
            _emojiOpen = !_emojiOpen;
            if (_emojiOpen) { _emojiScroll = 0; _chatSearchOpen = false; }
            RelayoutControls();
            RelayoutChat(false);
            Invalidate();
        }

        private void InsertEmoji(string emoji)
        {
            if (_input == null || emoji == null) return;
            Cfg.AddRecentEmoji(emoji);
            int pos = _input.SelectionStart;
            _input.Text = _input.Text.Insert(pos, emoji);
            _input.SelectionStart = pos + emoji.Length;
            _input.Focus();
        }

        private void ToggleChatSearch()
        {
            _chatSearchOpen = !_chatSearchOpen;
            if (_chatSearchOpen) { _emojiOpen = false; }
            _chatSearchQuery = "";
            RelayoutControls();
            RelayoutChat(true);
            Invalidate();
            if (_chatSearchOpen) _chatSearchBox.Focus();
        }

        // ------------------------------------------------------------ saisie
        private void OnInputTextChanged(object sender, EventArgs e)
        {
            RelayoutControls();
            RelayoutChat(false);
            Invalidate();
            Conversation c = Selected();
            if (c != null && !_recording)
            {
                if (IsGroup(c.PeerId)) ForEachGroupMember(c.PeerId, delegate(string member) { _ = _net.SendTypingAsync(member); });
                else _ = _net.SendTypingAsync(c.PeerId);
            }
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                if (_recording) FinishRecording(true);
                else SendCurrentMessage();
            }
            else if (e.KeyCode == Keys.Escape && _recording)
            {
                e.SuppressKeyPress = true;
                FinishRecording(false);
            }
        }

        // ------------------------------------------------------------ envoi texte
        private void SendCurrentMessage()
        {
            Conversation c = Selected();
            if (c == null) return;
            string text = _input.Text ?? "";
            text = text.TrimEnd('\r', '\n');
            if (text.Trim().Length == 0) return;

            ChatMessage m = ChatMessage.NewText(_self.Id, c.PeerId, true, text);
            if (c.ReplyTo != null)
            {
                m.ReplyToId = c.ReplyTo.Id;
                m.QuoteAuthor = c.ReplyTo.QuoteAuthor ?? (c.ReplyTo.IsMine ? "Vous" : c.PeerName);
                m.QuoteText = c.ReplyTo.Text ?? "";
                c.ReplyTo = null;
            }
            _input.Text = "";
            AppendLocalMessage(c, m);
            SendPendingMessage(c, m);
        }

        private async void SendPendingMessage(Conversation conversation, ChatMessage message)
        {
            if (conversation == null || message == null || string.IsNullOrEmpty(message.Id) ||
                !_messageSendInFlight.Add(message.Id)) return;
            bool sent = false;
            try
            {
                if (IsGroup(conversation.PeerId))
                {
                    List<Task<bool>> sends = new List<Task<bool>>();
                    foreach (string member in GroupMembers(conversation.PeerId))
                    {
                        if (member == _self.Id) continue;
                        if (message.Kind == MessageKind.Text)
                            sends.Add(_net.SendGroupTextAsync(member, message));
                        else if (message.Kind == MessageKind.Voice)
                            sends.Add(_net.SendVoiceAsync(member, message));
                        else sends.Add(_net.SendFileAsync(member, message));
                    }
                    bool[] results = await Task.WhenAll(sends);
                    for (int i = 0; i < results.Length; i++) sent |= results[i];
                }
                else if (message.Kind == MessageKind.Text)
                    sent = await _net.SendTextAsync(conversation.PeerId, message);
                else if (message.Kind == MessageKind.Voice)
                    sent = await _net.SendVoiceAsync(conversation.PeerId, message);
                else sent = await _net.SendFileAsync(conversation.PeerId, message);
            }
            catch { sent = false; }
            finally
            {
                _messageSendInFlight.Remove(message.Id);
            }

            if (!sent)
            {
                bool waitingForPeer = !IsGroup(conversation.PeerId) && !IsPeerOnline(conversation.PeerId);
                bool sourceAvailable = message.Kind == MessageKind.Text ||
                    (!string.IsNullOrEmpty(message.LocalPath) && File.Exists(message.LocalPath));
                message.Status = sourceAvailable && (waitingForPeer || !IsGroup(conversation.PeerId))
                    ? DeliveryStatus.Pending : DeliveryStatus.Failed;
                PersistMessage(message);
                Invalidate(StreamRect());
                _toasts.Show(message.Status == DeliveryStatus.Pending
                    ? "Message en attente. Il sera envoyé à la reconnexion."
                    : "Message non envoyé. Réessayez.");
            }
        }

        private bool IsPeerOnline(string peerId)
        {
            foreach (PeerInfo peer in _net.Peers)
                if (peer.Id == peerId) return peer.Online;
            return false;
        }

        private DateTime _lastPendingRetryUtc = DateTime.MinValue;

        private void RetryPendingMessages(string peerId = null, bool force = false)
        {
            if (_closing || IsDisposed) return;
            DateTime now = DateTime.UtcNow;
            if (peerId == null && !force && (now - _lastPendingRetryUtc).TotalSeconds < 8) return;
            if (peerId == null) _lastPendingRetryUtc = now;
            foreach (Conversation conversation in _convs.Values)
            {
                if (IsGroup(conversation.PeerId) || (peerId != null && conversation.PeerId != peerId) ||
                    !IsPeerOnline(conversation.PeerId)) continue;
                for (int i = 0; i < conversation.Messages.Count; i++)
                {
                    ChatMessage message = conversation.Messages[i];
                    if (!message.IsMine || message.Status != DeliveryStatus.Pending) continue;
                    if (message.Kind != MessageKind.Text &&
                        (string.IsNullOrEmpty(message.LocalPath) || !File.Exists(message.LocalPath)))
                    {
                        message.Status = DeliveryStatus.Failed;
                        PersistMessage(message);
                        continue;
                    }
                    SendPendingMessage(conversation, message);
                }
            }
            Invalidate(StreamRect());
        }

        private void AttachFiles()
        {
            Conversation c = Selected();
            if (c == null) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Envoyer un fichier";
                dlg.Multiselect = true;
                dlg.Filter = "Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                foreach (string f in dlg.FileNames) SendFile(f);
            }
        }

        private void SendFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            Conversation c = Selected();
            if (c == null) return;
            string name = Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool image = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp";
            ChatMessage m = new ChatMessage();
            m.Id = Guid.NewGuid().ToString("N");
            m.Kind = image ? MessageKind.Image : MessageKind.File;
            m.SenderId = _self.Id;
            m.PeerId = c.PeerId;
            m.IsMine = true;
            m.TimestampUtc = ChatMessage.NowUtcMs();
            m.Status = DeliveryStatus.Pending;
            m.LocalPath = path;
            m.FileName = name;
            m.FileSize = new FileInfo(path).Length;
            AppendLocalMessage(c, m);
            SendPendingMessage(c, m);
        }

        private void StartRecording()
        {
            Conversation c = Selected();
            if (c == null || _recording) return;
            string dir = Path.Combine(P2PChat.App.Profile.ProfileStore.AppDataDir, "transfers");
            try { Directory.CreateDirectory(dir); } catch { }
            _recPath = Path.Combine(dir, "voice_" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                _rec.Start(_recPath);
                _recording = true;
                Invalidate();
            }
            catch (Exception ex)
            {
                _recPath = null;
                MessageBox.Show(this, ex.Message, "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FinishRecording(bool send)
        {
            if (!_recording) return;
            _recording = false;
            string wave = _rec.EnvelopeB64();
            int dur = _rec.Stop();
            string path = _recPath;
            _recPath = null;

            Conversation c = Selected();
            if (send && c != null && path != null && File.Exists(path))
            {
                ChatMessage m = new ChatMessage();
                m.Id = Guid.NewGuid().ToString("N");
                m.Kind = MessageKind.Voice;
                m.SenderId = _self.Id;
                m.PeerId = c.PeerId;
                m.IsMine = true;
                m.TimestampUtc = ChatMessage.NowUtcMs();
                m.Status = DeliveryStatus.Pending;
                m.LocalPath = path;
                m.Waveform = wave;
                m.VoiceDurationSec = Math.Max(1, dur);
                AppendLocalMessage(c, m);
                SendPendingMessage(c, m);
            }
            else if (path != null)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
            RelayoutControls();
            Invalidate();
        }

        // ------------------------------------------------------------ lecture vocale
        private void StopPlayback()
        {
            _player.Stop();
            Conversation c = Selected();
            if (c != null && c.PlayingId != null) { c.PlayingId = null; Invalidate(StreamRect()); }
        }

        private void OnPlaybackDone(object sender, EventArgs e)
        {
            Conversation c = Selected();
            if (c != null && c.PlayingId != null) { c.PlayingId = null; Invalidate(StreamRect()); }
        }

        private void ToggleVoice(Conversation c, ChatMessage m)
        {
            if (c == null || m == null || string.IsNullOrEmpty(m.LocalPath)) return;
            if (!File.Exists(m.LocalPath)) return;
            if (_player.IsPlaying && c.PlayingId == m.Id)
            {
                if (_player.IsPaused) _player.Play(m.LocalPath);
                else _player.Pause();
            }
            else
            {
                _player.Play(m.LocalPath);
                c.PlayingId = m.Id;
            }
            Invalidate(StreamRect());
        }

        // ------------------------------------------------------------ glisser-déposer
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            if (_selectedId == null) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            foreach (string f in files) SendFile(f);
        }
    }
}
