using System;
using System.Collections.Generic;
using P2PChat.App.Features;
using P2PChat.App.Settings;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Branchement du moteur réseau P2P à l'interface : réception des pairs,
    /// messages, accusés, « en train d'écrire », réactions et notifications.</summary>
    public sealed partial class MainForm
    {
        private void WireNetwork()
        {
            AllowDrop = true;
            _net.PeerListChanged += OnPeerListChanged;
            _net.PeerAppeared += OnPeerAppeared;
            _net.MessageReceived += OnMessageReceived;
            _net.MessageStatusChanged += OnMessageStatusChanged;
            _net.TypingChanged += OnTypingChanged;
            _net.ReactionReceived += OnReactionReceived;
            _net.MessageEdited += OnMessageEdited;
            _net.MessageDeletedForAll += OnMessageDeletedForAll;
            _net.GroupMetaReceived += OnGroupMetaReceived;
            _net.StatusReceived += OnStatusReceived;
            _net.StatusViewed += OnStatusViewed;
            _net.CallSignalReceived += OnCallSignalReceived;
        }

        private void OnPeerListChanged(object sender, EventArgs e)
        {
            if (_closing || IsDisposed) return;
            LoadConversationsFromPeers();
            RetryPendingMessages(null, true);
            RetryStatusViewedReceipts();
            RebuildOrder();
            Invalidate(SidebarRect());
            if (_activeNav == 1) Invalidate(ChatRect());
        }

        private void OnPeerAppeared(object sender, PeerInfo p)
        {
            WaSounds.PlayPeer();
            OnPeerListChanged(sender, EventArgs.Empty);
        }

        private void AppendLocalMessage(Conversation c, ChatMessage m)
        {
            if (!IsGroup(c.PeerId) && Cfg.Peer(c.PeerId).Archived)
            {
                Cfg.Peer(c.PeerId).Archived = false;
                Cfg.Save();
                _activeNav = 0;
                RelayoutControls();
            }
            c.Messages.Add(m);
            try { _history.Append(m); } catch { }
            c.Draft = "";
            RebuildOrder();
            RelayoutChat(true);
            RelayoutControls();
            Invalidate();
            WaSounds.PlaySent();
        }

        private void PersistMessage(ChatMessage message)
        {
            try { _history.Update(message); } catch { }
        }

        private void MarkConversationRead(Conversation c)
        {
            if (c == null) return;
            if (c.Unread != 0) { c.Unread = 0; Invalidate(SidebarRect()); }
            if (!Cfg.ReadReceipts) return;
            for (int i = 0; i < c.Messages.Count; i++)
            {
                ChatMessage m = c.Messages[i];
                if (!m.IsMine && m.Status != DeliveryStatus.Read)
                {
                    m.Status = DeliveryStatus.Read;
                    PersistMessage(m);
                    _net.MarkReadAsync(IsGroup(c.PeerId) ? m.SenderId : c.PeerId, m.Id);
                }
            }
        }

        private void OnMessageReceived(object sender, ChatMessage msg)
        {
            if (msg == null) return;
            if (Cfg.IsBlocked(msg.SenderId ?? msg.PeerId)) return;
            Conversation c = Conv(msg.PeerId);
            if (IsGroup(msg.PeerId))
            {
                GroupLocal group;
                if (!Cfg.Groups.TryGetValue(msg.PeerId, out group) || group == null ||
                    group.Members == null || !group.Members.Contains(msg.SenderId)) return;
                if (c == null)
                {
                    c = new Conversation();
                    c.PeerId = msg.PeerId;
                    c.PeerName = string.IsNullOrEmpty(group.Name) ? "Groupe" : group.Name;
                    _convs[msg.PeerId] = c;
                }
            }
            if (c == null)
            {
                PeerInfo p = FindPeer(msg.PeerId);
                if (p == null)
                {
                    p = new PeerInfo();
                    p.Id = msg.PeerId;
                    p.Pseudo = "Inconnu";
                }
                c = CreateConversation(p);
            }
            if (FindLocal(c, msg.Id) != null) return;
            if (!msg.Deleted)
            {
                c.Messages.Add(msg);
                try { _history.Append(msg); } catch { }
            }
            c.Online = true;

            if (_selectedId == c.PeerId)
            {
                MarkConversationRead(c);
                RelayoutChat(true);
                Invalidate();
            }
            else
            {
                c.Unread++;
                RebuildOrder();
                _toasts.MessageReceived(Cfg, c.PeerId, c.PeerName, PreviewText(c), null, Focused);
                Invalidate(SidebarRect());
            }
        }

        private void OnMessageStatusChanged(object sender, ChatMessage m)
        {
            if (m == null) return;
            Conversation c = Conv(m.PeerId);
            ChatMessage local = c == null ? null : FindLocal(c, m.Id);
            if (local == null)
            {
                foreach (KeyValuePair<string, Conversation> kv in _convs)
                {
                    if (!IsGroup(kv.Key) || !GroupMembers(kv.Key).Contains(m.PeerId)) continue;
                    local = FindLocal(kv.Value, m.Id);
                    if (local != null) { c = kv.Value; break; }
                }
            }
            if (local != null)
            {
                local.Status = m.Status;
                PersistMessage(local);
                Invalidate();
            }
        }

        private void OnTypingChanged(object sender, string peerId)
        {
            Conversation c = Conv(peerId);
            if (c != null)
            {
                c.TypingPeerName = null;
                c.TypingUntil = DateTime.Now.AddSeconds(4);
                Invalidate(SidebarRect());
                Invalidate(ChatHeaderRect());
                return;
            }
            foreach (KeyValuePair<string, Conversation> kv in _convs)
            {
                if (!IsGroup(kv.Key) || !GroupMembers(kv.Key).Contains(peerId)) continue;
                kv.Value.TypingPeerName = SenderName(peerId);
                kv.Value.TypingUntil = DateTime.Now.AddSeconds(4);
                Invalidate(SidebarRect());
                if (_selectedId == kv.Key) Invalidate(ChatHeaderRect());
            }
        }

        private void OnReactionReceived(string peerId, string messageId, string emoji)
        {
            Conversation c = FindConversationForMessage(peerId, messageId, false);
            if (c == null) return;
            ChatMessage m = FindLocal(c, messageId);
            if (m == null) return;
            m.Reaction = emoji;
            PersistMessage(m);
            Invalidate();
        }

        private void OnMessageEdited(string peerId, string messageId, string newText)
        {
            Conversation c = FindConversationForMessage(peerId, messageId, true);
            if (c == null) return;
            ChatMessage m = FindLocal(c, messageId);
            if (m == null) return;
            if (newText != null)
            {
                m.Text = newText;
                m.Deleted = false;
                PersistMessage(m);
                c.InvalidateLayout();
                RelayoutChat(true);
                Invalidate();
            }
        }

        private void OnMessageDeletedForAll(string peerId, string messageId)
        {
            Conversation c = FindConversationForMessage(peerId, messageId, true);
            if (c == null) return;
            ChatMessage m = FindLocal(c, messageId);
            if (m == null) return;
            m.Deleted = true;
            m.Text = "";
            m.Reaction = null;
            PersistMessage(m);
            c.InvalidateLayout();
            RelayoutChat(true);
            Invalidate();
        }

        private Conversation FindConversationForMessage(string peerId, string messageId, bool requireAuthor)
        {
            Conversation direct = Conv(peerId);
            ChatMessage directMessage = direct == null ? null : FindLocal(direct, messageId);
            if (directMessage != null && (!requireAuthor || directMessage.SenderId == peerId)) return direct;
            foreach (KeyValuePair<string, Conversation> kv in _convs)
            {
                if (!IsGroup(kv.Key) || !GroupMembers(kv.Key).Contains(peerId)) continue;
                ChatMessage m = FindLocal(kv.Value, messageId);
                if (m != null && (!requireAuthor || m.SenderId == peerId)) return kv.Value;
            }
            return null;
        }
    }
}
