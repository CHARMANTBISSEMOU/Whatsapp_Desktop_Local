using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using P2PChat.App.Features;
using P2PChat.App.Profile;
using P2PChat.App.Settings;
using P2PChat.App.UI;
using P2PChat.Core.Interfaces;
using P2PChat.Core.Protocol;
using P2PChat.Net;

namespace P2PChat.App
{
    /// <summary>
    /// Point d'entrée : charge le profil, affiche la connexion, démarre le moteur
    /// réseau P2P (découverte UDP + serveur TCP chiffré) puis la fenêtre principale.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Mutex profileMutex = null;
            bool ownsProfileMutex = false;
            try
            {
                string profileName = GetProfileName(args);
                if (profileName == null)
                {
                    bool createdNew;
                    profileMutex = new Mutex(true, @"Local\P2PChat.DefaultProfile", out createdNew);
                    ownsProfileMutex = createdNew;

                    // Un lancement sans argument garde le profil habituel si
                    // aucune instance n'est ouverte. Les instances suivantes
                    // reçoivent automatiquement une identité indépendante.
                    // Le contrôle de processus couvre aussi une ancienne version
                    // déjà ouverte, qui ne connaîtrait pas encore le mutex.
                    if (!createdNew || HasOtherP2PChatProcess())
                    {
                        if (ownsProfileMutex)
                        {
                            profileMutex.ReleaseMutex();
                            ownsProfileMutex = false;
                        }
                        profileMutex.Dispose();
                        profileMutex = null;
                        profileName = AcquireIsolatedProfileMutex(out profileMutex);
                        ownsProfileMutex = true;
                    }
                }

                ProfileStore.ConfigureInstance(profileName);
                Run();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"),
                        DateTime.Now.ToString("s") + " | " + ex.ToString() + Environment.NewLine + Environment.NewLine);
                }
                catch { }
                MessageBox.Show("Erreur fatale : " + ex.Message, "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (profileMutex != null)
                {
                    if (ownsProfileMutex)
                    {
                        try { profileMutex.ReleaseMutex(); } catch (ApplicationException) { }
                    }
                    profileMutex.Dispose();
                }
            }
        }

        private static bool HasOtherP2PChatProcess()
        {
            int currentId = Process.GetCurrentProcess().Id;
            Process[] processes;
            try { processes = Process.GetProcessesByName("p2p"); }
            catch { return false; }
            foreach (Process process in processes)
            {
                using (process)
                {
                    if (process.Id != currentId) return true;
                }
            }
            return false;
        }

        private static string AcquireIsolatedProfileMutex(out Mutex acquired)
        {
            for (int slot = 2; ; slot++)
            {
                bool createdNew;
                Mutex candidate = new Mutex(true, @"Local\P2PChat.Instance." + slot, out createdNew);
                if (createdNew)
                {
                    acquired = candidate;
                    return "instance-" + slot;
                }
                candidate.Dispose();
            }
        }

        private static string GetProfileName(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    throw new ArgumentException("Utilisation : p2p.exe --profile <nom> (lettres, chiffres, - ou _).");
                return args[i + 1];
            }
            return null;
        }

        private static void Run()
        {
            ProfileStore profile = ProfileStore.Load();

            // 1) Écran de connexion (dev-ui) : modifie Self.Pseudo / Self.Bio, OK pour continuer.
            using (LoginScreen login = new LoginScreen(profile.Self))
            {
                if (login.ShowDialog() != DialogResult.OK)
                {
                    return; // l'utilisateur a annulé
                }
            }
            profile.Save();

            // 2) Moteur réseau P2P : zéro configuration, tout est automatique.
            string transfers = Path.Combine(ProfileStore.AppDataDir, "transfers");
            Directory.CreateDirectory(transfers);

            IChatNetwork net = new NetworkEngine(
                profile.Self.SanitizedClone(),
                54545,                   // port UDP découverte
                54546,                   // port TCP sessions
                transfers,               // dossier des fichiers reçus
                WindowsFormsSynchronizationContext.Current);

            using (net)
            {
                IChatHistory history = new ChatHistoryStore();
                net.Start();
                Application.Run(new MainForm(net, history, profile.Self, AppSettings.Load()));
            }
        }
    }
}
