using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using P2PChat.App.Controls;
using P2PChat.App.Settings;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Menus contextuels type WhatsApp (entête, ligne de discussion, bulle,
    /// nouvelle discussion) et leurs actions (épingler, sourdine, bloquer, répondre…).</summary>
    public sealed partial class MainForm
    {
        private void ShowNewChatMenu(Point screenPt)
        {
            WaMenu m = new WaMenu();
            m.Add("Créer un groupe", CreateGroup, WaIcons.Person);
            m.Add("Ajouter un contact", PromptConnect, WaIcons.Network);
            m.AddSep();
            m.Add("Actualiser la liste", delegate { _net.AnnounceNow(); _toasts.Show("Liste actualisée"); }, WaIcons.Network);
            m.AddSep();
            m.Add("À propos", delegate { _toasts.Show("P2PChat — messagerie P2P locale (projet scolaire)"); }, WaIcons.Info);
            m.ShowAt(screenPt, 0);
        }

        private void ShowSidebarMenu(Point screenPt)
        {
            WaMenu m = new WaMenu();
            m.Add("Modifier mon profil", ShowProfileEditor, WaIcons.Person);
            m.Add("Paramètres", ActivateSettingsPage, WaIcons.Info);
            m.AddSep();
            bool dark = Cfg.ThemeModeInt == 2;
            m.Add(dark ? "Thème clair" : "Thème sombre", delegate
            {
                Cfg.ThemeModeInt = dark ? 0 : 2;
                Cfg.Save();
                ApplyThemeChange();
            }, WaIcons.Sun);
            m.Add("Actualiser la liste", delegate { _net.AnnounceNow(); _toasts.Show("Liste actualisée"); }, WaIcons.Network);
            m.AddSep();
            m.Add("À propos", delegate { _toasts.Show("P2PChat — messagerie P2P locale (projet scolaire)"); }, WaIcons.Info);
            m.ShowAt(screenPt, 0);
        }

        private void ShowProfileEditor()
        {
            Form f = new Form();
            f.Text = "Mon profil";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(410, 220);
            Label nameLabel = new Label { Text = "Nom affiché", AutoSize = true, Location = new Point(18, 18) };
            TextBox name = new TextBox { Bounds = new Rectangle(18, 40, 374, 26), Text = _self.Pseudo ?? "" };
            Label bioLabel = new Label { Text = "Statut", AutoSize = true, Location = new Point(18, 82) };
            TextBox bio = new TextBox { Bounds = new Rectangle(18, 104, 374, 26), Text = _self.Bio ?? "" };
            Button save = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, Bounds = new Rectangle(276, 160, 116, 34) };
            f.Controls.AddRange(new Control[] { nameLabel, name, bioLabel, bio, save });
            f.AcceptButton = save;
            if (f.ShowDialog(this) != DialogResult.OK) return;
            string pseudo = name.Text.Trim();
            if (pseudo.Length < 3)
            {
                MessageBox.Show(this, "Le nom doit contenir au moins 3 caractères.", "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _self.Pseudo = pseudo;
            _self.Bio = bio.Text.Trim();
            _self.UpdatedUtc = ChatMessage.NowUtcMs();
            if (_net.Self != null)
            {
                _net.Self.Pseudo = _self.Pseudo;
                _net.Self.Bio = _self.Bio;
                _net.Self.UpdatedUtc = _self.UpdatedUtc;
            }
            new P2PChat.App.Profile.ProfileStore { Self = _self }.Save();
            _net.AnnounceNow();
            Invalidate();
        }

        private void ShowSettingsDialog()
        {
            Form f = new Form();
            f.Text = "Paramètres P2PChat";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(520, 430);

            Label appearance = new Label { Text = "APPARENCE", AutoSize = true, Location = new Point(18, 18) };
            Label themeLabel = new Label { Text = "Thème", AutoSize = true, Location = new Point(18, 48) };
            ComboBox theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(180, 44, 310, 26) };
            theme.Items.AddRange(new object[] { "Système", "Clair", "Sombre" });
            theme.SelectedIndex = Math.Max(0, Math.Min(2, Cfg.ThemeModeInt));
            Label fontLabel = new Label { Text = "Taille du texte", AutoSize = true, Location = new Point(18, 82) };
            NumericUpDown font = new NumericUpDown { Minimum = 0, Maximum = 3, Value = Cfg.FontStepInt, Bounds = new Rectangle(180, 78, 70, 26) };

            Label notifications = new Label { Text = "NOTIFICATIONS", AutoSize = true, Location = new Point(18, 122) };
            CheckBox sound = new CheckBox { Text = "Sons", Checked = Cfg.NotifSound, AutoSize = true, Location = new Point(18, 152) };
            CheckBox flash = new CheckBox { Text = "Clignoter dans la barre des tâches", Checked = Cfg.NotifFlash, AutoSize = true, Location = new Point(180, 152) };
            CheckBox preview = new CheckBox { Text = "Afficher l’aperçu des messages", Checked = Cfg.NotifPreview, AutoSize = true, Location = new Point(18, 181) };

            Label privacy = new Label { Text = "CONFIDENTIALITÉ", AutoSize = true, Location = new Point(18, 218) };
            CheckBox receipts = new CheckBox { Text = "Envoyer les accusés de lecture", Checked = Cfg.ReadReceipts, AutoSize = true, Location = new Point(18, 248) };
            Label network = new Label { Text = "RÉSEAU", AutoSize = true, Location = new Point(18, 284) };
            TextBox diagnostics = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Bounds = new Rectangle(18, 310, 370, 76), Text = _net.DiagnosticSummary };
            Button copy = new Button { Text = "Copier", Bounds = new Rectangle(398, 310, 92, 30) };
            copy.Click += delegate { try { Clipboard.SetText(diagnostics.Text); _toasts.Show("Diagnostic réseau copié."); } catch { } };

            Button ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, Bounds = new Rectangle(374, 392, 116, 30) };
            Button cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(250, 392, 110, 30) };
            f.Controls.AddRange(new Control[] { appearance, themeLabel, theme, fontLabel, font, notifications,
                sound, flash, preview, privacy, receipts, network, diagnostics, copy, ok, cancel });
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            if (f.ShowDialog(this) != DialogResult.OK) return;

            Cfg.ThemeModeInt = theme.SelectedIndex;
            Cfg.FontStepInt = (int)font.Value;
            Cfg.NotifSound = sound.Checked;
            Cfg.NotifFlash = flash.Checked;
            Cfg.NotifPreview = preview.Checked;
            Cfg.ReadReceipts = receipts.Checked;
            Cfg.Save();
            WaTheme.ApplyMode(Cfg.ThemeMode);
            ApplyThemeChange();
            RelayoutChat(false);
            Invalidate();
        }

        private void ShowRowMenu(Conversation c, Point screenPt)
        {
            if (c == null) return;
            Settings.PeerLocal pl = Cfg.Peer(c.PeerId);
            WaMenu m = new WaMenu();
            m.Add(pl.Pinned ? "Désépingler" : "Épingler", delegate { pl.Pinned = !pl.Pinned; Cfg.Save(); RebuildOrder(); Invalidate(SidebarRect()); }, WaIcons.Pin);
            m.Add(pl.Muted ? "Réactiver les notifications" : "Mettre en sourdine", delegate { pl.Muted = !pl.Muted; Cfg.Save(); Invalidate(SidebarRect()); }, WaIcons.BellMuted);
            m.Add("Marquer comme non lu", delegate { pl.MarkedUnread = true; Cfg.Save(); RebuildOrder(); Invalidate(SidebarRect()); }, WaIcons.React);
            if (!IsGroup(c.PeerId))
            {
                m.Add("Renommer le contact", delegate { RenameContact(c); }, WaIcons.Person);
                m.Add(pl.Archived ? "Désarchiver la discussion" : "Archiver la discussion",
                    delegate { ToggleArchive(c); }, WaIcons.Archive);
            }
            m.Add(Cfg.IsBlocked(c.PeerId) ? "Débloquer" : "Bloquer", delegate { ToggleBlock(c); }, WaIcons.Block);
            m.AddSep();
            m.Add("Supprimer la discussion", delegate { ClearChat(c); }, WaIcons.Trash);
            m.ShowAt(screenPt, 0);
        }

        private void ShowChatMenu(Point screenPt)
        {
            Conversation c = Selected();
            if (c == null) return;
            WaMenu m = new WaMenu();
            m.Add("Rechercher", delegate { ToggleChatSearch(); }, WaIcons.Search);
            m.Add("Médias et fichiers", ShowMediaGallery, WaIcons.Photo);
            m.Add(Cfg.IsBlocked(c.PeerId) ? "Débloquer" : "Bloquer", delegate { ToggleBlock(c); }, WaIcons.Block);
            m.AddSep();
            m.Add("Supprimer la discussion", delegate { ClearChat(c); }, WaIcons.Trash);
            m.ShowAt(screenPt, 0);
        }

        private void ShowMediaGallery()
        {
            Conversation c = Selected();
            if (c == null) return;
            LoadMessages(c);
            Form f = new Form();
            f.Text = "Médias et fichiers — " + c.PeerName;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(680, 430);
            ListView list = new ListView();
            list.View = View.Details;
            list.FullRowSelect = true;
            list.GridLines = true;
            list.Bounds = new Rectangle(12, 12, 656, 370);
            list.Columns.Add("Type", 110);
            list.Columns.Add("Nom", 300);
            list.Columns.Add("Taille", 100);
            list.Columns.Add("Date", 120);
            foreach (ChatMessage message in c.Messages)
            {
                if (message.Kind != MessageKind.Image && message.Kind != MessageKind.File && message.Kind != MessageKind.Voice) continue;
                string kind = message.Kind == MessageKind.Image ? "Image" : (message.Kind == MessageKind.Voice ? "Vocal" : "Document");
                string name = message.Kind == MessageKind.Voice ? (message.FileName ?? "Message vocal") : (message.FileName ?? kind);
                ListViewItem item = new ListViewItem(kind);
                item.SubItems.Add(name);
                item.SubItems.Add(WaTheme.FormatSize(message.FileSize));
                item.SubItems.Add(WaTheme.FormatListTime(message.TimestampUtc));
                item.Tag = message.LocalPath;
                list.Items.Add(item);
            }
            list.DoubleClick += delegate
            {
                if (list.SelectedItems.Count == 0) return;
                string path = list.SelectedItems[0].Tag as string;
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                {
                    MessageBox.Show(f, "Ce fichier n’est pas disponible sur cet appareil.", "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { System.Diagnostics.Process.Start(path); }
                catch (Exception ex) { MessageBox.Show(f, ex.Message, "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            Label hint = new Label { Text = "Double-cliquez sur un élément pour l’ouvrir.", AutoSize = true, Location = new Point(14, 394) };
            f.Controls.Add(list);
            f.Controls.Add(hint);
            f.ShowDialog(this);
        }

        private void ShowBubbleMenu(ChatMessage msg, Point screenPt)
        {
            if (msg == null) return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = WaTheme.Text(9.8f);
            menu.BackColor = WaTheme.MenuBg;
            menu.ForeColor = WaTheme.TextDark;
            menu.Items.Add("Répondre", null, delegate { SetReply(msg); });
            if (msg.Kind == MessageKind.File || msg.Kind == MessageKind.Image)
                menu.Items.Add("Ouvrir le fichier", null, delegate { OpenMessageFile(msg); });
            if (msg.Kind == MessageKind.Text && !string.IsNullOrEmpty(msg.Text))
                menu.Items.Add("Copier le texte", null, delegate { CopyMessageText(msg); });
            else if ((msg.Kind == MessageKind.File || msg.Kind == MessageKind.Image || msg.Kind == MessageKind.Voice) && !string.IsNullOrEmpty(msg.LocalPath))
                menu.Items.Add("Copier le fichier", null, delegate { CopyMessageFile(msg); });
            if (msg.IsMine && msg.Kind == MessageKind.Text)
                menu.Items.Add("Modifier", null, delegate { EditMessage(msg); });
            menu.Items.Add("Réagir 👍", null, delegate { React(msg, "👍"); });
            menu.Items.Add("Réagir ❤️", null, delegate { React(msg, "❤️"); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Supprimer pour moi", null, delegate
            {
                msg.Deleted = true;
                PersistMessage(msg);
                Invalidate();
            });
            if (msg.IsMine)
                menu.Items.Add("Supprimer pour tous", null, delegate { DeleteForAll(msg); });
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(this, PointToClient(screenPt));
        }

        private void CopyMessageText(ChatMessage msg)
        {
            try
            {
                Clipboard.SetText(msg.Text ?? "");
                _toasts.Show("Texte copié.");
            }
            catch (Exception ex) { _toasts.Show("Impossible de copier : " + ex.Message); }
        }

        private void CopyMessageFile(ChatMessage msg)
        {
            try
            {
                if (msg == null || string.IsNullOrEmpty(msg.LocalPath) || !System.IO.File.Exists(msg.LocalPath))
                    throw new System.IO.FileNotFoundException("Le fichier n’est plus disponible.");
                StringCollection paths = new StringCollection();
                paths.Add(msg.LocalPath);
                Clipboard.SetFileDropList(paths);
                _toasts.Show("Fichier copié.");
            }
            catch (Exception ex) { _toasts.Show("Impossible de copier : " + ex.Message); }
        }

        private void OpenMessageFile(ChatMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.LocalPath) || !System.IO.File.Exists(msg.LocalPath))
            {
                MessageBox.Show(this, "Le fichier n’est pas disponible sur cet appareil.", "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(new ProcessStartInfo(msg.LocalPath) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Windows n’a pas pu ouvrir ce fichier : " + ex.Message, "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------ actions
        private void ToggleBlock(Conversation c)
        {
            if (c == null) return;
            if (Cfg.IsBlocked(c.PeerId)) Cfg.BlockedIds.Remove(c.PeerId);
            else Cfg.BlockedIds.Add(c.PeerId);
            Cfg.Save();
            Invalidate();
        }

        private void ToggleArchive(Conversation c)
        {
            if (c == null || IsGroup(c.PeerId)) return;
            Settings.PeerLocal local = Cfg.Peer(c.PeerId);
            local.Archived = !local.Archived;
            Cfg.Save();
            if (local.Archived && _selectedId == c.PeerId)
            {
                _selectedId = null;
                _chatSearchOpen = false;
            }
            RebuildOrder();
            RelayoutControls();
            Invalidate();
            _toasts.Show(local.Archived ? "Discussion archivée." : "Discussion désarchivée.");
        }

        private void RenameContact(Conversation c)
        {
            if (c == null || IsGroup(c.PeerId)) return;
            ContactLocal contact = Cfg.Contact(c.PeerId);
            string value = PromptText("Renommer le contact", "Nom affiché", contact.Alias);
            if (value == null) return;
            contact.Alias = value.Trim();
            if (contact.Alias.Length == 0) contact.Alias = contact.LastKnownName;
            c.PeerName = Cfg.ContactName(c.PeerId, contact.LastKnownName);
            Cfg.Save();
            RebuildOrder();
            Invalidate();
        }

        private void ClearChat(Conversation c)
        {
            if (c == null) return;
            try { _history.Clear(c.PeerId); } catch { }
            c.Messages.Clear();
            c.Layouts.Clear();
            c.ContentH = 0;
            RelayoutChat(true);
            RebuildOrder();
            Invalidate();
        }

        private void SetReply(ChatMessage msg)
        {
            Conversation c = Selected();
            if (c == null || msg == null) return;
            c.ReplyTo = msg;
            RelayoutControls();
            RelayoutChat(false);
            Invalidate();
            _input.Focus();
        }

        private void React(ChatMessage msg, string emoji)
        {
            if (msg == null) return;
            string next = msg.Reaction == emoji ? "" : emoji;
            msg.Reaction = next;
            PersistMessage(msg);
            if (IsGroup(msg.PeerId))
                ForEachGroupMember(msg.PeerId, delegate(string member) { _net.SendReactionAsync(member, msg.Id, next); });
            else _net.SendReactionAsync(msg.PeerId, msg.Id, next);
            Invalidate();
        }

        private void EditMessage(ChatMessage msg)
        {
            Conversation c = Selected();
            if (c == null || msg == null) return;
            string cur = msg.Text ?? "";
            string next = PromptText("Modifier le message", "Nouveau texte", cur);
            if (next == null || next.Trim().Length == 0 || next == cur) return;
            msg.Text = next;
            msg.Deleted = false;
            PersistMessage(msg);
            c.InvalidateLayout();
            RelayoutChat(true);
            Invalidate();
            if (IsGroup(msg.PeerId))
                ForEachGroupMember(msg.PeerId, delegate(string member) { _net.SendEditAsync(member, msg.Id, next); });
            else _net.SendEditAsync(msg.PeerId, msg.Id, next);
        }

        private void DeleteForAll(ChatMessage msg)
        {
            Conversation c = Selected();
            if (msg == null) return;
            msg.Deleted = true;
            msg.Text = "";
            msg.Reaction = null;
            PersistMessage(msg);
            if (c != null) { c.InvalidateLayout(); RelayoutChat(true); }
            Invalidate();
            if (IsGroup(msg.PeerId))
                ForEachGroupMember(msg.PeerId, delegate(string member) { _net.SendDeleteForAllAsync(member, msg.Id); });
            else _net.SendDeleteForAllAsync(msg.PeerId, msg.Id);
        }

        private async void PromptConnect()
        {
            string value = PromptText("Ajouter un ordinateur", "Adresse IP du PC (ex. 192.168.1.20 ou 192.168.1.20:54546)");
            if (string.IsNullOrEmpty(value)) return;
            value = value.Trim();
            string host = value;
            int port = 54546;
            int idx = value.LastIndexOf(':');
            if (idx > 0 && value.IndexOf(':') == idx)
            {
                int parsed;
                if (!int.TryParse(value.Substring(idx + 1), out parsed) || parsed < 1 || parsed > 65535)
                {
                    MessageBox.Show(this, "Le port doit être compris entre 1 et 65535.",
                        "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                port = parsed;
                host = value.Substring(0, idx).Trim();
            }

            string peerId = await _net.ConnectToAsync(host, port);
            if (string.IsNullOrEmpty(peerId))
            {
                MessageBox.Show(this,
                    "Connexion impossible. Vérifiez l'adresse, que P2PChat est ouvert sur l'autre PC et que le pare-feu autorise l'application sur le réseau privé.",
                    "P2PChat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoadConversationsFromPeers();
            RebuildOrder();
            OpenConversation(peerId);
            _toasts.Show("Ordinateur connecté.");
        }

        private string PromptText(string title, string label, string initial = null)
        {
            Form f = new Form();
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(380, 120);
            Label lb = new Label();
            lb.Text = label;
            lb.AutoSize = true;
            lb.Location = new Point(16, 14);
            TextBox tb = new TextBox();
            tb.Bounds = new Rectangle(16, 40, 348, 24);
            if (initial != null) { tb.Text = initial; tb.SelectAll(); }
            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Bounds = new Rectangle(150, 78, 90, 30);
            f.Controls.Add(lb);
            f.Controls.Add(tb);
            f.Controls.Add(ok);
            f.AcceptButton = ok;
            return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
        }
    }
}
