using System;
using System.IO;
using P2PChat.Core.Crypto;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.App.Profile
{
    /// <summary>
    /// Profil local persistant : %APPDATA%\P2PChat\profile.json.
    /// Contient l'identifiant permanent, le pseudo et la bio ("statut").
    /// L'identifiant ne change jamais, même après changement de pseudo.
    /// </summary>
    public sealed class ProfileStore
    {
        /// <summary>L'identité publique locale.</summary>
        public PeerInfo Self = new PeerInfo();

        /// <summary>Dossier de données de l'application.</summary>
        public static string AppDataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "P2PChat");
            }
        }

        /// <summary>Charge le profil ; en crée un neuf au premier lancement.</summary>
        public static ProfileStore Load()
        {
            ProfileStore p = new ProfileStore();
            try
            {
                string file = Path.Combine(AppDataDir, "profile.json");
                if (File.Exists(file))
                {
                    string raw = File.ReadAllText(file);
                    if (raw.StartsWith(LocalCrypto.Magic, StringComparison.Ordinal))
                    {
                        try { raw = LocalCrypto.DecryptString(raw); }
                        catch { raw = "{}"; }
                    }
                    PeerInfo info = PeerInfo.FromDict(MiniJson.ParseObject(raw));
                    if (info != null && info.Pseudo != null && info.Pseudo.Length > 0)
                    {
                        p.Self = info;
                    }
                }
            }
            catch
            {
                // Profil illisible : on repart d'une identité neuve.
                p.Self = new PeerInfo();
            }
            if (p.Self.Id == null || p.Self.Id.Length == 0)
            {
                p.Self = new PeerInfo();
                p.Self.Id = Guid.NewGuid().ToString("N");
                p.Self.Bio = "Disponible";
            }
            return p;
        }

        /// <summary>Enregistre le profil sur le disque. Appelé après la fenêtre de connexion.</summary>
        public void Save()
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(Path.Combine(AppDataDir, "profile.json"),
                LocalCrypto.EncryptString(MiniJson.Serialize(Self.ToDict())));
        }
    }
}
