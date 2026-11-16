using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using P2PChat.Core.Crypto;
using P2PChat.Core.Interfaces;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.App.Features
{
    /// <summary>
    /// Historique persistant : un fichier JSONL par pair dans
    /// %APPDATA%\P2PChat\historique\. 1 ligne = 1 message.
    /// </summary>
    public sealed class ChatHistoryStore : IChatHistory
    {
        private static readonly object Gate = new object();

        private static string HistoryDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "P2PChat", "historique");
            }
        }

        /// <summary>Assainit un identifiant de pair pour en faire un nom de fichier sûr.</summary>
        private static string SafePeerId(string peerId)
        {
            if (peerId == null) return "sans-id";
            StringBuilder b = new StringBuilder(peerId.Length);
            foreach (char c in peerId)
            {
                if (char.IsLetterOrDigit(c) || c == '-')
                {
                    b.Append(c);
                }
            }
            if (b.Length == 0) return "sans-id";
            return b.ToString();
        }

        private static string PathFor(string peerId)
        {
            Directory.CreateDirectory(HistoryDir);
            return Path.Combine(HistoryDir, SafePeerId(peerId) + ".jsonl");
        }

        public void Append(ChatMessage msg)
        {
            if (msg == null || msg.PeerId == null) return;
            lock (Gate)
            {
                File.AppendAllText(PathFor(msg.PeerId),
                    LocalCrypto.EncryptString(MiniJson.Serialize(msg.ToDict())) + Environment.NewLine);
            }
        }

        public void Update(ChatMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.Id) || string.IsNullOrEmpty(msg.PeerId)) return;
            lock (Gate)
            {
                string file = PathFor(msg.PeerId);
                if (!File.Exists(file)) return;

                string[] lines = File.ReadAllLines(file);
                bool changed = false;
                string replacement = LocalCrypto.EncryptString(MiniJson.Serialize(msg.ToDict()));
                for (int i = 0; i < lines.Length; i++)
                {
                    ChatMessage existing = ParseLine(lines[i]);
                    if (existing != null && existing.Id == msg.Id)
                    {
                        lines[i] = replacement;
                        changed = true;
                    }
                }
                if (!changed) return;

                string temp = file + ".tmp";
                try
                {
                    File.WriteAllLines(temp, lines);
                    File.Replace(temp, file, null);
                }
                catch
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                    throw;
                }
            }
        }

        public List<ChatMessage> Load(string peerId, int limit)
        {
            List<ChatMessage> result = new List<ChatMessage>();
            lock (Gate)
            {
                string file = PathFor(peerId);
                if (!File.Exists(file)) return result;
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch
                {
                    return result;
                }
                for (int i = 0; i < lines.Length; i++)
                {
                    ChatMessage m = ParseLine(lines[i]);
                    if (m != null) result.Add(m);
                }
            }
            if (limit > 0 && result.Count > limit)
            {
                result = result.GetRange(result.Count - limit, limit);
            }
            return result;
        }

        public List<ChatMessage> Search(string peerId, string query)
        {
            List<ChatMessage> result = new List<ChatMessage>();
            lock (Gate)
            {
                string file = PathFor(peerId);
                if (!File.Exists(file)) return result;
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch
                {
                    return result;
                }
                foreach (string line in lines)
                {
                    ChatMessage m = ParseLine(line);
                    if (m == null || m.Text == null) continue;
                    if (m.Text.IndexOf(query == null ? "" : query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(m);
                    }
                }
            }
            return result;
        }

        public void Clear(string peerId)
        {
            lock (Gate)
            {
                string file = PathFor(peerId);
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch
                {
                }
            }
        }

        public void ClearAll()
        {
            lock (Gate)
            {
                try
                {
                    if (Directory.Exists(HistoryDir))
                    {
                        Directory.Delete(HistoryDir, true);
                    }
                }
                catch
                {
                }
            }
        }

        private static ChatMessage ParseLine(string line)
        {
            if (line == null || line.Trim().Length == 0) return null;
            string json = line;
            if (line.StartsWith(LocalCrypto.Magic, StringComparison.Ordinal))
            {
                try { json = LocalCrypto.DecryptString(line); }
                catch { return null; } // ligne chiffrée illisible
            }
            try
            {
                return ChatMessage.FromDict(MiniJson.ParseObject(json));
            }
            catch
            {
                return null; // ligne corrompue : ignorée
            }
        }
    }
}
