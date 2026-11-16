using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using P2PChat.App.Settings;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Discussions de groupe : création (nom + membres), fan-out des messages,
    /// réception des métadonnées, persistance et affichage des expéditeurs.
    /// Un groupe est identifié par "grp_" + GUID ; les messages portent PeerId = id du groupe.
    /// </summary>
    public sealed partial class MainForm
    {
        internal const string GroupPrefix = "grp_";

        internal static bool IsGroup(string peerId)
        {
            return peerId != null && peerId.StartsWith(GroupPrefix, StringComparison.Ordinal);
        }

        /// <summary>Membres d'un groupe connu localement (jamais null ; vide si inconnu).</summary>
        internal List<string> GroupMembers(string gid)
        {
            GroupLocal g;
            if (gid != null && Cfg.Groups.TryGetValue(gid, out g)) return g.Members;
            return new List<string>();
        }

        internal int GroupMemberCount(string gid)
        {
            return GroupMembers(gid).Count;
        }

        /// <summary>Applique une action d'envoi à chaque membre du groupe (sauf soi).</summary>
        internal void ForEachGroupMember(string gid, Action<string> send)
        {
            foreach (string memberId in GroupMembers(gid))
            {
                if (memberId == _self.Id) continue;
                send(memberId);
            }
        }

        /// <summary>Nom affiché d'un expéditeur (id de pair).</summary>
        internal string SenderName(string senderId)
        {
            if (string.IsNullOrEmpty(senderId) || senderId == _self.Id) return "Vous";
            Conversation c = Conv(senderId);
            if (c != null && !string.IsNullOrEmpty(c.PeerName)) return c.PeerName;
            PeerInfo p = FindPeer(senderId);
            if (p != null && !string.IsNullOrEmpty(p.Pseudo)) return p.Pseudo;
            return "Membre";
        }

        /// <summary>Recrée les conversations des groupes mémorisés au démarrage.</summary>
        internal void LoadGroups()
        {
            foreach (KeyValuePair<string, GroupLocal> kv in Cfg.Groups)
            {
                Conversation c = Conv(kv.Key);
                if (c == null)
                {
                    c = new Conversation();
                    c.PeerId = kv.Key;
                    c.PeerName = string.IsNullOrEmpty(kv.Value.Name) ? "Groupe" : kv.Value.Name;
                    _convs[kv.Key] = c;
                }
                else if (!string.IsNullOrEmpty(kv.Value.Name)) c.PeerName = kv.Value.Name;
            }
        }

        /// <summary>Boîte de dialogue « Nouveau groupe » : nom + choix des membres en ligne.</summary>
        private bool PromptGroup(out string name, out List<string> members)
        {
            name = null;
            members = new List<string>();

            List<PeerInfo> candidates = new List<PeerInfo>();
            foreach (PeerInfo p in _net.Peers)
            {
                if (p.Id == _self.Id) continue;
                if (Cfg.IsBlocked(p.Id)) continue;
                candidates.Add(p);
            }
            if (candidates.Count == 0)
            {
                _toasts.Show("Aucun pair en ligne pour créer un groupe.");
                return false;
            }

            Form f = new Form();
            f.Text = "Nouveau groupe";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(360, 420);

            Label lb = new Label();
            lb.Text = "Nom du groupe";
            lb.AutoSize = true;
            lb.Location = new Point(16, 14);
            f.Controls.Add(lb);

            TextBox tb = new TextBox();
            tb.Bounds = new Rectangle(16, 38, 328, 24);
            f.Controls.Add(tb);

            Label lm = new Label();
            lm.Text = "Membres";
            lm.AutoSize = true;
            lm.Location = new Point(16, 74);
            f.Controls.Add(lm);

            CheckedListBox clb = new CheckedListBox();
            clb.CheckOnClick = true;
            clb.Bounds = new Rectangle(16, 98, 328, 256);
            for (int i = 0; i < candidates.Count; i++)
            {
                clb.Items.Add(candidates[i].Pseudo, false);
            }
            f.Controls.Add(clb);

            Button ok = new Button();
            ok.Text = "CRÉER";
            ok.DialogResult = DialogResult.OK;
            ok.Bounds = new Rectangle(220, 372, 124, 36);
            f.Controls.Add(ok);
            f.AcceptButton = ok;

            if (f.ShowDialog(this) != DialogResult.OK) return false;
            string n = tb.Text.Trim();
            if (n.Length < 3)
            {
                _toasts.Show("Le nom du groupe doit contenir au moins 3 caractères.");
                return false;
            }
            List<string> sel = new List<string>();
            for (int i = 0; i < clb.Items.Count; i++)
            {
                if (clb.GetItemChecked(i)) sel.Add(candidates[i].Id);
            }
            if (sel.Count == 0)
            {
                _toasts.Show("Sélectionnez au moins un membre.");
                return false;
            }
            name = n;
            members = sel;
            return true;
        }

        /// <summary>Crée un groupe, l'annonce aux membres et ouvre la conversation.</summary>
        private void CreateGroup()
        {
            string name;
            List<string> selected;
            if (!PromptGroup(out name, out selected)) return;

            string gid = GroupPrefix + Guid.NewGuid().ToString("N");
            List<string> members = new List<string>(selected);
            members.Add(_self.Id);

            GroupLocal g = new GroupLocal();
            g.Name = name;
            g.Members = members;
            Cfg.Groups[gid] = g;
            Cfg.Save();

            foreach (string memberId in selected)
            {
                _net.SendGroupMetaAsync(memberId, gid, name, members);
            }

            Conversation c = new Conversation();
            c.PeerId = gid;
            c.PeerName = name;
            _convs[gid] = c;
            RebuildOrder();
            OpenConversation(gid);
            _toasts.Show("Groupe « " + name + " » créé.");
        }

        /// <summary>Fan-out d'un message texte vers tous les membres du groupe.</summary>
        private void SendGroupMessage(Conversation c, ChatMessage m)
        {
            ForEachGroupMember(c.PeerId, delegate(string memberId)
            {
                _net.SendGroupTextAsync(memberId, m);
            });
        }

        /// <summary>Réception des métadonnées d'un groupe : conversation + persistance.</summary>
        private void OnGroupMetaReceived(string senderId, string gid, string name, List<string> members)
        {
            if (string.IsNullOrEmpty(gid) || !IsGroup(gid) || members == null ||
                !members.Contains(senderId) || !members.Contains(_self.Id) || members.Count > 64) return;
            GroupLocal g;
            if (!Cfg.Groups.TryGetValue(gid, out g))
            {
                g = new GroupLocal();
                Cfg.Groups[gid] = g;
            }
            if (!string.IsNullOrEmpty(name)) g.Name = name;
            if (members != null && members.Count > 0)
            {
                g.Members = new List<string>(members);
                if (!g.Members.Contains(senderId)) g.Members.Add(senderId);
            }
            Cfg.Save();

            Conversation c = Conv(gid);
            if (c == null)
            {
                c = new Conversation();
                c.PeerId = gid;
                _convs[gid] = c;
            }
            if (!string.IsNullOrEmpty(g.Name)) c.PeerName = g.Name;
            RebuildOrder();
            Invalidate(SidebarRect());
        }

        private void SendGroupTransfer(Conversation c, ChatMessage message, bool voice)
        {
            if (c == null || message == null) return;
            ForEachGroupMember(c.PeerId, delegate(string member)
            {
                if (voice) _net.SendVoiceAsync(member, message);
                else _net.SendFileAsync(member, message);
            });
        }
    }
}
