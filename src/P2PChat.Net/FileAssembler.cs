using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace P2PChat.Net
{
    /// <summary>
    /// Réassemblage des fichiers reçus par morceaux base64 :
    ///  - Begin(id, fileName, size) : tampon mémoire (cap 100 Mo par transfert) ;
    ///  - Add(id, base64Chunk) : morceaux de 65536 octets côté émission (base64 dans le JSON) ;
    ///  - Complete(id, ts, senderId) : écrit transferDir\{senderId}_{id}-{nomSécurisé}
    ///    et retourne le chemin absolu écrit (null si invalide/incomplet) ;
    ///  - purge des transferts non terminés de plus de 5 minutes.
    /// Thread-safe : un seul lock court autour du dictionnaire, aucune attente sous lock.
    /// </summary>
    public sealed class FileAssembler : IDisposable
    {
        public const long MaxFileBytes = 100L * 1024L * 1024L;
        public const int ChunkBytes = 65536;
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

        private readonly string _dir;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Transfer> _items = new Dictionary<string, Transfer>();
        private bool _disposed;

        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private sealed class Transfer
        {
            internal readonly MemoryStream Buffer = new MemoryStream();
            internal readonly DateTime StartedUtc = DateTime.UtcNow;
            internal string FileName = "";
            internal long Size;
        }

        public FileAssembler(string transferDir)
        {
            if (transferDir == null) throw new ArgumentNullException("transferDir");
            _dir = transferDir;
            try { Directory.CreateDirectory(_dir); } catch (Exception) { }
        }

        /// <summary>Démarre un transfert entrant. False si id invalide/doublon/taille > 100 Mo.</summary>
        public bool Begin(string id, string fileName, long size)
        {
            if (string.IsNullOrEmpty(id) || size < 0 || size > MaxFileBytes) return false;
            lock (_sync)
            {
                if (_disposed) return false;
                if (_items.ContainsKey(id)) return false;
                Transfer t = new Transfer();
                t.FileName = fileName == null ? "" : fileName;
                t.Size = size;
                _items[id] = t;
                return true;
            }
        }

        /// <summary>Ajoute un morceau base64 au transfert id. False si inconnu/illisible/dépassement.</summary>
        public bool Add(string id, string base64Chunk)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(base64Chunk)) return false;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64Chunk); }
            catch (Exception) { return false; }
            lock (_sync)
            {
                if (_disposed) return false;
                Transfer t;
                if (!_items.TryGetValue(id, out t)) return false;
                if (t.Buffer.Length + bytes.Length > MaxFileBytes)
                {
                    _items.Remove(id);
                    return false;
                }
                t.Buffer.Write(bytes, 0, bytes.Length);
                return true;
            }
        }

        /// <summary>
        /// Finalise le transfert id : écrit le fichier sous
        /// {transferDir}\{senderId sécurisé}_{id sécurisé}-{nomSécurisé}
        /// et retourne le chemin absolu (null si inconnu/incomplet/échec disque).
        /// </summary>
        public string Complete(string id, long tsUtcMs, string senderId)
        {
            if (string.IsNullOrEmpty(id)) return null;
            byte[] data;
            string fileName;
            lock (_sync)
            {
                if (_disposed) return null;
                Transfer t;
                if (!_items.TryGetValue(id, out t)) return null;
                _items.Remove(id);
                if (t.Size > 0 && t.Buffer.Length != t.Size) return null; // incomplet → refusé
                if (t.Buffer.Length > MaxFileBytes) return null;
                data = t.Buffer.ToArray();
                fileName = t.FileName;
            }
            return WriteToDisk(id, tsUtcMs, senderId, fileName, data);
        }

        private string WriteToDisk(string id, long tsUtcMs, string senderId, string fileName, byte[] data)
        {
            try
            {
                string safeName = MakeSafeFileName(fileName);
                if (safeName.Length == 0) safeName = "fichier.bin";
                string safeSender = MakeSafeToken(senderId);
                if (safeSender.Length == 0) safeSender = "peer";
                string safeId = MakeSafeToken(id);
                if (safeId.Length == 0) safeId = "transfert";

                string path = Path.Combine(_dir, safeSender + "_" + safeId + "-" + safeName);
                int n = 2;
                while (File.Exists(path) && n < 64)
                {
                    path = Path.Combine(_dir, safeSender + "_" + safeId + "-" + n + "-" + safeName);
                    n++;
                }
                File.WriteAllBytes(path, data);
                if (tsUtcMs > 0)
                {
                    try
                    {
                        DateTime fileTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                            .AddMilliseconds(tsUtcMs);
                        File.SetLastWriteTimeUtc(path, fileTime);
                    }
                    catch (Exception) { }
                }
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Purge les transferts non terminés de plus de 5 minutes (mémoire).</summary>
        public void PurgeStale()
        {
            lock (_sync)
            {
                if (_disposed || _items.Count == 0) return;
                List<string> drop = new List<string>();
                DateTime now = DateTime.UtcNow;
                foreach (KeyValuePair<string, Transfer> kv in _items)
                {
                    if (now - kv.Value.StartedUtc > StaleAfter) drop.Add(kv.Key);
                }
                for (int i = 0; i < drop.Count; i++) _items.Remove(drop[i]);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                _items.Clear();
            }
        }

        private static string MakeSafeToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c < ' ' || Array.IndexOf(invalid, c) >= 0)
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
                if (sb.Length >= 80) break;
            }
            return sb.ToString();
        }

        private static string MakeSafeFileName(string name)
        {
            if (name == null) return "";
            string s = MakeSafeToken(name).Trim();
            while (s.Length > 0 && s[0] == '.')
            {
                s = s.Substring(1); // pas de fichier caché
            }
            if (s.Length == 0) return "";
            string upper = s.ToUpperInvariant();
            int dot = upper.IndexOf('.');
            string baseName = dot > 0 ? upper.Substring(0, dot) : upper;
            if (Array.IndexOf(ReservedNames, baseName) >= 0)
            {
                s = "_" + s; // CON, PRN, COM1… interdits sous Windows
            }
            return s;
        }
    }
}
