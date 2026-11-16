using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Géométrie des zones de la fenêtre principale (tout en px @96dpi mis à l'échelle).</summary>
    public sealed partial class MainForm
    {
        // ---------------------------------------------------------------- zones
        internal Rectangle SidebarRect()
        {
            int rail = NavigationRailWidth();
            return new Rectangle(rail, 0, Math.Min(_sidebarW, Math.Max(0, ClientSize.Width - rail)), ClientSize.Height);
        }

        internal Rectangle ChatRect()
        {
            Rectangle side = SidebarRect();
            return new Rectangle(side.Right, 0, Math.Max(0, ClientSize.Width - side.Right), ClientSize.Height);
        }

        internal bool SplitterHit(Point p)
        {
            int sx = ChatRect().X;
            return p.X >= sx - WaTheme.S(3) && p.X <= sx + WaTheme.S(3);
        }

        private void PaintSplitter(Graphics g)
        {
            int sx = ChatRect().X;
            using (SolidBrush b = new SolidBrush(WaTheme.Divider))
            {
                g.FillRectangle(b, sx - 1, 0, 1, ClientSize.Height);
            }
        }

        // ---------------------------------------------------------------- sidebar
        internal Rectangle SidebarHeaderRect()
        {
            Rectangle side = SidebarRect();
            return new Rectangle(side.X, 0, side.Width, WaTheme.S(HeaderH));
        }

        /// <summary>Zone visible de la liste des discussions (sous recherche + chips).</summary>
        internal Rectangle ListRect()
        {
            int top = WaTheme.S(HeaderH) + WaTheme.S(49) + WaTheme.S(58);
            Rectangle side = SidebarRect();
            return new Rectangle(side.X, top, side.Width, ClientSize.Height - top);
        }

        /// <summary>Conversations affichées (ordre + filtres recherche / non-lus).</summary>
        internal List<Conversation> VisibleConversations()
        {
            var list = new List<Conversation>();
            foreach (string id in _order)
            {
                Conversation c;
                if (!_convs.TryGetValue(id, out c)) continue;
                bool archived = Cfg.Peer(id).Archived;
                if (_activeNav == 4 ? !archived : archived) continue;
                if (_unreadFilter && c.Unread <= 0 && !Cfg.Peer(id).MarkedUnread) continue;
                if (_favoritesFilter && !Cfg.Peer(id).Pinned) continue;
                if (_searchQuery.Length > 0)
                {
                    string hay = (c.PeerName + " " + PreviewText(c)).ToLowerInvariant();
                    if (!hay.Contains(_searchQuery.ToLowerInvariant())) continue;
                }
                list.Add(c);
            }
            return list;
        }

        internal int ListScrollMax()
        {
            int content = VisibleConversations().Count * WaTheme.S(RowH);
            return Math.Max(0, content - ListRect().Height);
        }

        /// <summary>Reconstruit l'ordre : épinglées d'abord, puis dernière activité.</summary>
        internal void RebuildOrder()
        {
            _order.Clear();
            foreach (KeyValuePair<string, Conversation> kv in _convs) _order.Add(kv.Key);
            _order.Sort(delegate(string a, string b)
            {
                Conversation ca = _convs[a], cb = _convs[b];
                bool pa = Cfg.Peer(a).Pinned, pb = Cfg.Peer(b).Pinned;
                if (pa != pb) return pa ? -1 : 1;
                return LastActivity(cb).CompareTo(LastActivity(ca));
            });
        }

        private static long LastActivity(Conversation c)
        {
            ChatMessage last = c.Last;
            return last != null ? last.TimestampUtc : c.LastSeenUtc;
        }

        // ---------------------------------------------------------------- discussion
        internal Rectangle ChatHeaderRect()
        {
            Rectangle r = ChatRect();
            return new Rectangle(r.X, 0, r.Width, WaTheme.S(HeaderH));
        }

        internal Rectangle ChatSearchBarRect()
        {
            Rectangle r = ChatRect();
            return new Rectangle(r.X, WaTheme.S(HeaderH), r.Width, WaTheme.S(44));
        }

        /// <summary>Zone du fil de messages (entre entête et composeur).</summary>
        internal Rectangle StreamRect()
        {
            Rectangle r = ChatRect();
            int top = WaTheme.S(HeaderH) + (_chatSearchOpen ? WaTheme.S(44) : 0);
            int bottom = r.Bottom - ComposerHeight() - (_emojiOpen ? WaTheme.S(EmojiH) : 0);
            return new Rectangle(r.X, top, r.Width, Math.Max(0, bottom - top));
        }

        /// <summary>Hauteur du composeur (grandit avec le texte : 62..170 + barre de citation).</summary>
        internal int ComposerHeight()
        {
            int h = WaTheme.S(ComposerMinH);
            if (_input != null)
            {
                int lines = _input.GetLineFromCharIndex(_input.TextLength) + 1;
                if (lines > 1)
                {
                    h = Math.Min(WaTheme.S(170), WaTheme.S(ComposerMinH) + (lines - 1) * WaTheme.S(20));
                }
            }
            Conversation c = Selected();
            if (c != null && c.ReplyTo != null) h += WaTheme.S(ReplyH);
            return h;
        }

        internal Rectangle ComposerRect()
        {
            Rectangle r = ChatRect();
            int h = ComposerHeight() + (_emojiOpen ? WaTheme.S(EmojiH) : 0);
            return new Rectangle(r.X, r.Bottom - h, r.Width, h);
        }

        internal Rectangle EmojiRect()
        {
            Rectangle r = ChatRect();
            int ch = ComposerHeight();
            return new Rectangle(r.X + WaTheme.S(16), r.Bottom - ch - WaTheme.S(EmojiH),
                Math.Min(WaTheme.S(420), r.Width - WaTheme.S(32)), WaTheme.S(EmojiH));
        }

        /// <summary>Barre de citation au-dessus de la pilule de saisie.</summary>
        internal Rectangle ReplyBarRect()
        {
            Rectangle r = ChatRect();
            int ch = ComposerHeight();
            return new Rectangle(r.X + WaTheme.S(52), r.Bottom - ch + WaTheme.S(2),
                r.Width - WaTheme.S(104), WaTheme.S(ReplyH) - WaTheme.S(8));
        }

        internal int ChatScrollMax(Conversation c)
        {
            return Math.Max(0, c.ContentH - StreamRect().Height);
        }

        /// <summary>
        /// Recalcule la hauteur du fil ; scrollToBottom=true colle en bas
        /// (sinon conserve la position, collée si déjà en bas).
        /// </summary>
        internal void RelayoutChat(bool scrollToBottom)
        {
            Conversation c = Selected();
            if (c == null) return;
            bool wasBottom = c.AtBottom || scrollToBottom;
            MeasureStream(c);
            int max = ChatScrollMax(c);
            if (wasBottom)
            {
                _chatScroll = max;
                c.AtBottom = true;
            }
            else
            {
                _chatScroll = Math.Max(0, Math.Min(max, _chatScroll));
            }
        }
    }
}
