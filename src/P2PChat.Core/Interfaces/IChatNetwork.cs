using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using P2PChat.Core.Protocol;

namespace P2PChat.Core.Interfaces
{
    /// <summary>
    /// Contrat du moteur réseau, implémenté par P2PChat.Net.NetworkEngine et
    /// consommé par l'interface graphique. Les méthodes retournent false en cas
    /// d'échec (pair injoignable) sans lever d'exception pour l'UI.
    /// 
    /// IMPORTANCE (UI) : les événements peuvent être levés sur des threads réseau.
    /// NetworkEngine fait pivoter les événements sur le SynchronizationContext fourni
    /// (s'il n'est pas null) ; sinon l'UI DOIT utiliser Control.BeginInvoke.
    /// </summary>
    public interface IChatNetwork : IDisposable
    {
        /// <summary>Mon identité publique (celle du profil local).</summary>
        PeerInfo Self { get; }

        /// <summary>Instantané des pairs actuellement en ligne.</summary>
        IReadOnlyList<PeerInfo> Peers { get; }

        /// <summary>Un pair vient d'apparaître (event arg = copie du PeerInfo).</summary>
        event EventHandler<PeerInfo> PeerAppeared;

        /// <summary>
        /// La liste des pairs a changé (apparition, disparition, mise à jour de pseudo/bio
        /// ou changement de statut en ligne). L'UI doit rafraîchir sa liste.
        /// </summary>
        event EventHandler PeerListChanged;

        /// <summary>
        /// Message entrant prêt à afficher : texte, vocal (LocalPath déjà sauvegardé
        /// sur le disque) ou fichier/image (idem). IsMine=false, PeerId=expéditeur.
        /// </summary>
        event EventHandler<ChatMessage> MessageReceived;

        /// <summary>
        /// Mise à jour du statut d'un message : l'objet ne contient de fiable que
        /// Id, PeerId et Status (l'UI retrouve sa bulle par Id).
        /// </summary>
        event EventHandler<ChatMessage> MessageStatusChanged;

        /// <summary>
        /// Le pair (event arg = PeerInfo.Id) est "en train d'écrire...".
        /// L'UI gère son propre timeout d'affichage (4 s conseillé).
        /// </summary>
        event EventHandler<string> TypingChanged;

        /// <summary>
        /// Le pair a réagi à un message : (peerId, messageId, emoji).
        /// Emoji vide = retrait de la réaction.
        /// </summary>
        event Action<string, string, string> ReactionReceived;

        /// <summary>Un pair a modifié (texte) un message : (peerId, messageId, nouveauTexte).</summary>
        event Action<string, string, string> MessageEdited;

        /// <summary>Un pair a supprimé un message pour tous : (peerId, messageId).</summary>
        event Action<string, string> MessageDeletedForAll;

        /// <summary>Un pair annonce/met à jour un groupe : (expéditeur, idGroupe, nom, membres).</summary>
        event Action<string, string, string, List<string>> GroupMetaReceived;

        /// <summary>Statut/story reçu d'un pair : (expéditeur, statut).</summary>
        event Action<string, StatusPost> StatusReceived;

        /// <summary>Démarre la découverte UDP et le serveur TCP. Appeler une seule fois.</summary>
        void Start();

        /// <summary>Envoie un message texte. Le drapeau Status est mis à jour par le moteur.</summary>
        Task<bool> SendTextAsync(string peerId, ChatMessage msg);

        /// <summary>Envoie un message vocal (Kind=Voice, LocalPath = fichier WAV local, VoiceDurationSec).</summary>
        Task<bool> SendVoiceAsync(string peerId, ChatMessage msg);

        /// <summary>
        /// Envoie un fichier (Kind=File ou Image selon l'extension : png/jpg/jpeg/gif/bmp = Image).
        /// Découpage en morceaux + réassemblage gérés par le moteur.
        /// </summary>
        Task<bool> SendFileAsync(string peerId, ChatMessage msg);

        /// <summary>Annonce "je tape..." (bridé à 1 envoi / 2,5 s par le moteur).</summary>
        Task<bool> SendTypingAsync(string peerId);

        /// <summary>Réagit (emoji) à un message ; emoji vide = retire la réaction.</summary>
        Task<bool> SendReactionAsync(string peerId, string messageId, string emoji);

        /// <summary>Modifie un message déjà envoyé (texte).</summary>
        Task<bool> SendEditAsync(string peerId, string messageId, string newText);

        /// <summary>Supprime un message pour tous les participants.</summary>
        Task<bool> SendDeleteForAllAsync(string peerId, string messageId);

        /// <summary>Envoie un message texte de groupe à un membre (fan-out orchestré par l'UI).</summary>
        Task<bool> SendGroupTextAsync(string memberId, ChatMessage msg);

        /// <summary>Annonce les métadonnées d'un groupe à un membre.</summary>
        Task<bool> SendGroupMetaAsync(string memberId, string groupId, string groupName, List<string> memberIds);

        /// <summary>Envoie un statut (story) à un pair.</summary>
        Task<bool> SendStatusAsync(string peerId, StatusPost status);

        /// <summary>Marque un message reçu comme lu (envoie l'accusé ✓✓ bleu à l'expéditeur).</summary>
        Task MarkReadAsync(string peerId, string messageId);

        /// <summary>Déconnexion propre d'avec un pair.</summary>
        Task<bool> SendGoodbyeAsync(string peerId);

        /// <summary>
        /// Connexion directe « manuelle » à un pair : ip:portTcp ou nom d'hôte.
        /// Permet de discuter même si le broadcast UDP est bloqué par le pare-feu.
        /// Retourne l'id du pair rejoint, ou null en cas d'échec.
        /// </summary>
        Task<string> ConnectToAsync(string host, int tcpPort);

        /// <summary>Résumé réseau pour le panneau Paramètres (IP locale, ports, pairs).</summary>
        string DiagnosticSummary { get; }

        /// <summary>Rafraîchit les interfaces réseau et relance la découverte immédiatement.</summary>
        void AnnounceNow();
    }
}
