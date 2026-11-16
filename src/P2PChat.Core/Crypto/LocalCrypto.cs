using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace P2PChat.Core.Crypto
{
    /// <summary>
    /// Chiffrement des données locales (historique, profil, paramètres).
    /// Clé maîtresse aléatoire 32 o (%APPDATA%\P2PChat\secrets.key) ; chiffrement
    /// AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC) via SessionCipher.
    /// </summary>
    public static class LocalCrypto
    {
        public const string Magic = "P2PENC1:";

        private static readonly object Gate = new object();
        private static byte[] _key;
        private static string _storageDirectory;

        /// <summary>Configure le dossier de données avant le premier chiffrement local.</summary>
        public static void ConfigureStorageDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Dossier invalide.", "directory");
            lock (Gate)
            {
                if (_key != null) throw new InvalidOperationException("Le chiffrement local est déjà initialisé.");
                _storageDirectory = directory;
            }
        }

        private static string KeyPath
        {
            get
            {
                string root = _storageDirectory;
                if (string.IsNullOrEmpty(root))
                {
                    root = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "P2PChat");
                }
                return Path.Combine(root, "secrets.key");
            }
        }

        public static byte[] MasterKey()
        {
            lock (Gate)
            {
                if (_key != null && _key.Length == 32) return _key;
                try
                {
                    string p = KeyPath;
                    if (File.Exists(p))
                    {
                        _key = Convert.FromBase64String(File.ReadAllText(p).Trim());
                        if (_key.Length == 32) return _key;
                    }
                }
                catch { }
                byte[] k = new byte[32];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(k);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(KeyPath));
                    File.WriteAllText(KeyPath, Convert.ToBase64String(k));
                }
                catch { }
                _key = k;
                return _key;
            }
        }

        public static byte[] Encrypt(byte[] plain)
        {
            using (SessionCipher c = SessionCipher.FromSharedSecret(MasterKey()))
                return c.Encrypt(plain);
        }

        public static byte[] Decrypt(byte[] frame)
        {
            using (SessionCipher c = SessionCipher.FromSharedSecret(MasterKey()))
                return c.Decrypt(frame);
        }

        public static string EncryptString(string text)
        {
            return Magic + Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(text ?? "")));
        }

        public static string DecryptString(string stored)
        {
            if (stored == null) return null;
            if (stored.StartsWith(Magic, StringComparison.Ordinal))
            {
                stored = stored.Substring(Magic.Length);
            }
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(stored)));
        }
    }
}
