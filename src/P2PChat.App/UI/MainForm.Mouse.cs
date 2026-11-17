using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Interactions souris de la zone de discussion : entête, recherche,
    /// bulles (lecture vocale, menu), composeur et panneau d'emojis.</summary>
    public sealed partial class MainForm
    {
        private StreamItem HitStreamItem(Point p)
        {
            Conversation c = Selected();
            if (c == null) return default(StreamItem);
            Rectangle st = StreamRect();
            if (!st.Contains(p)) return default(StreamItem);
            List<StreamItem> items = EnsureItems(c, st.Width);
            int off = StreamOffset(c, st);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                StreamItem it = items[i];
                if (it.Kind != 2) continue;
                Rectangle r = new Rectangle(st.X + it.Rect.X, it.Rect.Y + off, it.Rect.Width, it.Rect.Height);
                if (r.Contains(p)) return it;
            }
            return default(StreamItem);
        }

        private Rectangle BubbleScreenRect(StreamItem it)
        {
            Rectangle st = StreamRect();
            int off = StreamOffset(Selected(), st);
            return new Rectangle(st.X + it.Rect.X, it.Rect.Y + off, it.Rect.Width, it.Rect.Height);
        }

        private void ChatMouseMove(Point p)
        {
            if (_activeNav == 2) { StatusMouseMove(p); return; }
            if (_activeNav == 5) { ProfileMouseMove(p); return; }
            if (_activeNav == 6) { Cursor = Cursors.Default; return; }
            if (_activeNav != 0 && _activeNav != 4) return;
            if (_selectedId == null)
            {
                int action = -1;
                for (int i = 0; i < 4; i++) if (EmptyActionRect(i).Contains(p)) { action = i; break; }
                if (action != _hoverEmptyAction) { _hoverEmptyAction = action; Invalidate(ChatRect()); }
                return;
            }
            int hb = -1;
            if (_selectedId != null && ChatHeaderRect().Contains(p))
            {
                if (Inflated(HeaderIconRect(1), 8).Contains(p)) hb = 1;
                else if (Inflated(HeaderIconRect(0), 8).Contains(p)) hb = 0;
                else if (Inflated(HeaderIconRect(2), 8).Contains(p)) hb = 2;
            }
            if (hb != _hoverChatBtn) { _hoverChatBtn = hb; Invalidate(ChatHeaderRect()); }

            Conversation c = Selected();
            string hover = null;
            if (c != null)
            {
                StreamItem it = HitStreamItem(p);
                if (it.Kind == 2 && it.Msg != null) hover = it.Msg.Id;
                Cursor = it.Kind == 2 && it.Msg != null &&
                    (it.Msg.Kind == MessageKind.File || it.Msg.Kind == MessageKind.Image)
                    ? Cursors.Hand : Cursors.Default;
            }
            if (c != null && hover != c.HoverMsgId)
            {
                c.HoverMsgId = hover;
                Invalidate(StreamRect());
            }
        }

        private void ChatMouseDown(Point p, MouseButtons button)
        {
            if (_activeNav == 2) { StatusMouseDown(p); return; }
            if (_activeNav == 6) { SettingsMouseDown(p); return; }
            if (_activeNav != 0 && _activeNav != 4) return;
            Conversation c = Selected();
            if (c == null)
            {
                if (button == MouseButtons.Left)
                    for (int i = 0; i < 4; i++) if (EmptyActionRect(i).Contains(p)) { ActivateEmptyAction(i); return; }
                return;
            }

            if (button == MouseButtons.Right)
            {
                StreamItem it = HitStreamItem(p);
                if (it.Kind == 2 && it.Msg != null) ShowBubbleMenu(it.Msg, PointToScreen(p));
                return;
            }
            if (button != MouseButtons.Left) return;

            if (_emojiOpen)
            {
                if (EmojiRect().Contains(p)) { EmojiPanelClick(p); return; }
                _emojiOpen = false;
                Invalidate();
            }

            if (ChatHeaderRect().Contains(p))
            {
                if (Inflated(HeaderIconRect(2), 8).Contains(p)) { StartAudioCall(); return; }
                if (Inflated(HeaderIconRect(1), 8).Contains(p)) { ToggleChatSearch(); return; }
                if (Inflated(HeaderIconRect(0), 8).Contains(p)) { ShowChatMenu(PointToScreen(p)); return; }
                return;
            }

            if (_chatSearchOpen && ChatSearchBarRect().Contains(p)) { _chatSearchBox.Focus(); return; }

            if (EmojiBtnRect().Contains(p)) { ToggleEmoji(); return; }
            if (AttachBtnRect().Contains(p))
            {
                if (_recording) FinishRecording(false);
                else AttachFiles();
                return;
            }
            if (SendBtnRect().Contains(p))
            {
                if (_recording) { FinishRecording(true); return; }
                if (_input.Text.Trim().Length > 0) SendCurrentMessage();
                else StartRecording();
                return;
            }

            if (c.ReplyTo != null && CloseReplyBtnRect().Contains(p))
            {
                c.ReplyTo = null;
                RelayoutControls();
                RelayoutChat(false);
                Invalidate();
                return;
            }

            StreamItem hit = HitStreamItem(p);
            if (hit.Kind == 2 && hit.Msg != null)
            {
                ChatMessage m = hit.Msg;
                if (m.Kind == MessageKind.Voice) ToggleVoice(c, m);
                else if (m.Kind == MessageKind.File || m.Kind == MessageKind.Image) OpenMessageFile(m);
                else
                {
                    c.HoverMsgId = m.Id;
                    Invalidate(StreamRect());
                }
            }
        }

        private void ChatMouseUp(Point p, MouseButtons button)
        {
            // Aucune action au relâchement ; géré au clic (ChatMouseDown).
        }
    }
}
