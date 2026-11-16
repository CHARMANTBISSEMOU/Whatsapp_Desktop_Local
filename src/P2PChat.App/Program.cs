using System;
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
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
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
