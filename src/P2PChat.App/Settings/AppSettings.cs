using System;
using System.Collections.Generic;
using System.IO;
using P2PChat.Core.Json;
using P2PChat.Core.Crypto;
using P2PChat.App.Theme;

namespace P2PChat.App.Settings
{
    /// <summary>État par correspondant (comme l'app réelle) : épinglé, silencié, non-lu manuel.</summary>
    public sealed class PeerLocal
    {
        public bool Pinned;
        public bool Muted;
        public bool MarkedUnread;
    public bool Archived;
    }

public sealed class ContactLocal
{
    public string Alias = "";
    public string LastKnownName = "";
}

    /// <summary>Groupe local : nom + identifiants des membres (ids de pair).</summary>
    public sealed class GroupLocal
    {
        public string Name = "";
        public List<string> Members = new List<string>();
    }

    /// <summary>Statut (story) local : texte + fond coloré, expire après 24 h.</summary>
    public sealed class StatusItem
    {
        public string Id = "";
        public string SenderId = "";
        public string SenderName = "";
        public string Text = "";
        public int Bg;
        public long Ts;
        public bool Mine;
    }

    /// <summary>
    /// Paramètres persistants de l'application (%APPDATA%\P2PChat\settings.json) :
    /// thème, taille de police, notifications, accusés de lecture, blocages,
    /// état par discussion (épinglé/sourdine/non-lu), émos récents, géométrie.
    /// </summary>
    public sealed class AppSettings
    {
        public int ThemeModeInt = (int)WaThemeMode.System;
        public int FontStepInt;
        public bool NotifSound = true;
        public bool NotifFlash = true;
        public bool NotifPreview = true;
        public bool ReadReceipts = true;
        public List<string> BlockedIds = new List<string>();
        public List<string> RecentEmojis = new List<string>();
        public readonly Dictionary<string, PeerLocal> Peers = new Dictionary<string, PeerLocal>();
        public readonly Dictionary<string, ContactLocal> Contacts = new Dictionary<string, ContactLocal>();
        public readonly Dictionary<string, GroupLocal> Groups = new Dictionary<string, GroupLocal>();
        public readonly List<StatusItem> Statuses = new List<StatusItem>();
        public int SidebarWidth;
        public int WindowW;
        public int WindowH;
        public bool WindowMax;

        public WaThemeMode ThemeMode
        {
            get { return (WaThemeMode)ThemeModeInt; }
            set { ThemeModeInt = (int)value; }
        }

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "P2PChat", "settings.json");
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                string file = FilePath;
                if (!File.Exists(file)) return s;
                string raw = File.ReadAllText(file);
                if (raw.StartsWith(LocalCrypto.Magic, StringComparison.Ordinal))
                {
                    try { raw = LocalCrypto.DecryptString(raw); }
                    catch { raw = "{}"; }
                }
                Dictionary<string, object> d = MiniJson.ParseObject(raw);
                s.ThemeModeInt = (int)MiniJson.AsLong(d, "theme", (long)WaThemeMode.System);
                if (s.ThemeModeInt < 0 || s.ThemeModeInt > 2) s.ThemeModeInt = 0;
                s.FontStepInt = (int)MiniJson.AsLong(d, "font");
                if (s.FontStepInt < 0 || s.FontStepInt > 3) s.FontStepInt = 0;
                s.NotifSound = MiniJson.AsBool(d, "nsound", true);
                s.NotifFlash = MiniJson.AsBool(d, "nflash", true);
                s.NotifPreview = MiniJson.AsBool(d, "nprev", true);
                s.ReadReceipts = MiniJson.AsBool(d, "receipts", true);
                s.SidebarWidth = (int)MiniJson.AsLong(d, "sideW");
                s.WindowW = (int)MiniJson.AsLong(d, "winW");
                s.WindowH = (int)MiniJson.AsLong(d, "winH");
                s.WindowMax = MiniJson.AsBool(d, "winMax");
                LoadStringList(MiniJson.AsList(d, "blocked"), s.BlockedIds);
                LoadStringList(MiniJson.AsList(d, "recent"), s.RecentEmojis);
                Dictionary<string, object> peers = MiniJson.AsDict(d, "peers");
                if (peers != null)
                {
                    foreach (KeyValuePair<string, object> kv in peers)
                    {
                        Dictionary<string, object> pd = kv.Value as Dictionary<string, object>;
                        if (pd == null) continue;
                        PeerLocal p = new PeerLocal();
                        p.Pinned = MiniJson.AsBool(pd, "pin");
                        p.Muted = MiniJson.AsBool(pd, "mute");
                        p.MarkedUnread = MiniJson.AsBool(pd, "unread");
                        p.Archived = MiniJson.AsBool(pd, "archived");
                        s.Peers[kv.Key] = p;
                    }
                }
                Dictionary<string, object> contacts = MiniJson.AsDict(d, "contacts");
                if (contacts != null)
                {
                    foreach (KeyValuePair<string, object> kv in contacts)
                    {
                        Dictionary<string, object> cd = kv.Value as Dictionary<string, object>;
                        if (cd == null) continue;
                        ContactLocal c = new ContactLocal();
                        c.Alias = MiniJson.AsString(cd, "alias", "");
                        c.LastKnownName = MiniJson.AsString(cd, "name", "");
                        s.Contacts[kv.Key] = c;
                    }
                }
                Dictionary<string, object> groups = MiniJson.AsDict(d, "groups");
                if (groups != null)
                {
                    foreach (KeyValuePair<string, object> kv in groups)
                    {
                        Dictionary<string, object> gd = kv.Value as Dictionary<string, object>;
                        if (gd == null) continue;
                        GroupLocal g = new GroupLocal();
                        g.Name = MiniJson.AsString(gd, "name");
                        if (g.Name == null) g.Name = "";
                        List<object> gm = MiniJson.AsList(gd, "members");
                        if (gm != null)
                        {
                            foreach (object o in gm)
                            {
                                string id = MiniJson.AsString(o);
                                if (!string.IsNullOrEmpty(id)) g.Members.Add(id);
                            }
                        }
                        s.Groups[kv.Key] = g;
                    }
                }
                List<object> stList = MiniJson.AsList(d, "statuses");
                if (stList != null)
                {
                    long cutoff = DateTime.UtcNow.AddHours(-24).Ticks / TimeSpan.TicksPerMillisecond;
                    foreach (object o in stList)
                    {
                        Dictionary<string, object> sd = o as Dictionary<string, object>;
                        if (sd == null) continue;
                        StatusItem it = new StatusItem();
                        it.Id = MiniJson.AsString(sd, "id");
                        it.SenderId = MiniJson.AsString(sd, "snd");
                        it.SenderName = MiniJson.AsString(sd, "name");
                        it.Text = MiniJson.AsString(sd, "txt");
                        it.Bg = (int)MiniJson.AsLong(sd, "bg");
                        it.Ts = MiniJson.AsLong(sd, "ts");
                        it.Mine = MiniJson.AsBool(sd, "mine", false);
                        if (string.IsNullOrEmpty(it.Id) || it.Ts < cutoff) continue; // expiré (24 h)
                        s.Statuses.Add(it);
                    }
                }
            }
            catch (Exception)
            {
                // paramètres illisibles : valeurs par défaut
            }
            return s;
        }

        private static void LoadStringList(List<object> src, List<string> dst)
        {
            if (src == null) return;
            dst.Clear();
            foreach (object o in src)
            {
                string v = MiniJson.AsString(o);
                if (v != null) dst.Add(v);
            }
        }

        public void Save()
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["theme"] = (long)ThemeModeInt;
                d["font"] = (long)FontStepInt;
                d["nsound"] = NotifSound;
                d["nflash"] = NotifFlash;
                d["nprev"] = NotifPreview;
                d["receipts"] = ReadReceipts;
                d["sideW"] = (long)SidebarWidth;
                d["winW"] = (long)WindowW;
                d["winH"] = (long)WindowH;
                d["winMax"] = WindowMax;
                List<object> blocked = new List<object>();
                foreach (string b in BlockedIds) blocked.Add(b);
                d["blocked"] = blocked;
                List<object> recent = new List<object>();
                foreach (string b in RecentEmojis) recent.Add(b);
                d["recent"] = recent;
                Dictionary<string, object> peers = new Dictionary<string, object>();
                foreach (KeyValuePair<string, PeerLocal> kv in Peers)
                {
                    Dictionary<string, object> pd = new Dictionary<string, object>();
                    pd["pin"] = kv.Value.Pinned;
                    pd["mute"] = kv.Value.Muted;
                    pd["unread"] = kv.Value.MarkedUnread;
                    pd["archived"] = kv.Value.Archived;
                    peers[kv.Key] = pd;
                }
                d["peers"] = peers;
                Dictionary<string, object> contacts = new Dictionary<string, object>();
                foreach (KeyValuePair<string, ContactLocal> kv in Contacts)
                {
                    Dictionary<string, object> cd = new Dictionary<string, object>();
                    cd["alias"] = kv.Value.Alias;
                    cd["name"] = kv.Value.LastKnownName;
                    contacts[kv.Key] = cd;
                }
                d["contacts"] = contacts;
                Dictionary<string, object> groups = new Dictionary<string, object>();
                foreach (KeyValuePair<string, GroupLocal> kv in Groups)
                {
                    Dictionary<string, object> gd = new Dictionary<string, object>();
                    gd["name"] = kv.Value.Name;
                    List<object> gm = new List<object>();
                    foreach (string m in kv.Value.Members) gm.Add(m);
                    gd["members"] = gm;
                    groups[kv.Key] = gd;
                }
                d["groups"] = groups;
                List<object> statuses = new List<object>();
                foreach (StatusItem it in Statuses)
                {
                    Dictionary<string, object> sd = new Dictionary<string, object>();
                    sd["id"] = it.Id;
                    sd["snd"] = it.SenderId;
                    sd["name"] = it.SenderName;
                    sd["txt"] = it.Text;
                    sd["bg"] = (long)it.Bg;
                    sd["ts"] = it.Ts;
                    sd["mine"] = it.Mine;
                    statuses.Add(sd);
                }
                d["statuses"] = statuses;

                string file = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, LocalCrypto.EncryptString(MiniJson.Serialize(d)));
            }
            catch (Exception)
            {
                // écriture impossible : silencieux
            }
        }

        /// <summary>État local d'un pair (jamais null : créé à la demande).</summary>
        public PeerLocal Peer(string id)
        {
            if (id == null) id = "";
            PeerLocal p;
            lock (Peers)
            {
                if (!Peers.TryGetValue(id, out p))
                {
                    p = new PeerLocal();
                    Peers[id] = p;
                }
            }
            return p;
        }

        public bool IsBlocked(string id)
        {
            return id != null && BlockedIds.Contains(id);
        }

        public ContactLocal Contact(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "";
            ContactLocal c;
            if (!Contacts.TryGetValue(id, out c))
            {
                c = new ContactLocal();
                Contacts[id] = c;
            }
            return c;
        }

        public string ContactName(string id, string networkName)
        {
            ContactLocal c;
            if (id != null && Contacts.TryGetValue(id, out c) && !string.IsNullOrWhiteSpace(c.Alias)) return c.Alias.Trim();
            if (id != null && Contacts.TryGetValue(id, out c) && !string.IsNullOrWhiteSpace(c.LastKnownName)) return c.LastKnownName.Trim();
            return string.IsNullOrWhiteSpace(networkName) ? "Inconnu" : networkName.Trim();
        }

        /// <summary>Mémorise un emoji récent (sans doublon, 24 max).</summary>
        public void AddRecentEmoji(string emoji)
        {
            if (emoji == null || emoji.Length == 0) return;
            RecentEmojis.Remove(emoji);
            RecentEmojis.Insert(0, emoji);
            while (RecentEmojis.Count > 24) RecentEmojis.RemoveAt(RecentEmojis.Count - 1);
        }
    }
}
