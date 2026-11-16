using System;
using System.Collections.Generic;
using P2PChat.Core.Json;

namespace P2PChat.Core.Protocol
{
    /// <summary>Type de contenu d'un message.</summary>
    public enum MessageKind : int
    {
        Text = 0,
        Voice = 1,
        File = 2,
        Image = 3
    }

    /// <summary>
    /// Statut d'acheminement d'un message (les fameuses coches de WhatsApp).
    /// Uniquement significatif pour les messages envoyés par soi-même (IsMine).
    /// </summary>
    public enum DeliveryStatus : int
    {
        Pending = 0,   // prêt, pas encore envoyé
        Sent = 1,      // ✓
        Delivered = 2, // ✓✓
        Read = 3,      // ✓✓ bleues
        Failed = 4
    }

    /// <summary>
    /// Un message de conversation complet : texte, vocal, fichier ou image.
    /// Sert à la fois pour le transport réseau et la persistance de l'historique.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>Identifiant unique du message (GUID "N").</summary>
        public string Id;

        /// <summary>Type de contenu.</summary>
        public MessageKind Kind;

        /// <summary>PeerInfo.Id de l'expéditeur.</summary>
        public string SenderId;

        /// <summary>PeerInfo.Id du CORRESPONDANT (le "peer" avec qui on discute).</summary>
        public string PeerId;

        /// <summary>true si ce message a été envoyé par soi-même.</summary>
        public bool IsMine;

        /// <summary>Texte du message (ou légende pour vocal/fichier - facultatif).</summary>
        public string Text;

        /// <summary>Horodatage (ms epoch UTC).</summary>
        public long TimestampUtc;

        /// <summary>Id du message auquel celui-ci répond (WhatsApp "reply") - facultatif.</summary>
        public string ReplyToId;

        /// <summary>Auteur affiché du message cité (snapshot local, non réseau).</summary>
        public string QuoteAuthor;

        /// <summary>Extrait cité affiché dans la bulle (snapshot local, non réseau).</summary>
        public string QuoteText;

        /// <summary>Réaction emoji actuelle du pair sur CE message ("" = aucune).</summary>
        public string Reaction;

        /// <summary>Enveloppe de la waveform (30 barres 0-255, base64) pour les vocaux.</summary>
        public string Waveform;

        /// <summary>true si le message a été supprimé localement ("Supprimer pour moi").</summary>
        public bool Deleted;

        /// <summary>Chemin local du vocal/fichier (diffère selon la machine !).</summary>
        public string LocalPath;

        /// <summary>Nom de fichier tel qu'envoyé.</summary>
        public string FileName;

        /// <summary>Taille du fichier en octets.</summary>
        public long FileSize;

        /// <summary>Durée du vocal en secondes.</summary>
        public int VoiceDurationSec;

        /// <summary>Statut d'acheminement (coches).</summary>
        public DeliveryStatus Status;

        /// <summary>Horodatage courant en ms epoch UTC.</summary>
        public static long NowUtcMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        /// <summary>Crée un message texte tout prêt.</summary>
        public static ChatMessage NewText(string senderId, string peerId, bool mine, string text)
        {
            ChatMessage m = new ChatMessage();
            m.Id = Guid.NewGuid().ToString("N");
            m.Kind = MessageKind.Text;
            m.SenderId = senderId;
            m.PeerId = peerId;
            m.IsMine = mine;
            m.Text = text == null ? "" : text;
            m.TimestampUtc = NowUtcMs();
            m.Status = DeliveryStatus.Pending;
            return m;
        }

        public ChatMessage Clone()
        {
            ChatMessage m = new ChatMessage();
            m.Id = Id;
            m.Kind = Kind;
            m.SenderId = SenderId;
            m.PeerId = PeerId;
            m.IsMine = IsMine;
            m.Text = Text;
            m.TimestampUtc = TimestampUtc;
            m.ReplyToId = ReplyToId;
            m.QuoteAuthor = QuoteAuthor;
            m.QuoteText = QuoteText;
            m.Reaction = Reaction;
            m.Waveform = Waveform;
            m.Deleted = Deleted;
            m.LocalPath = LocalPath;
            m.FileName = FileName;
            m.FileSize = FileSize;
            m.VoiceDurationSec = VoiceDurationSec;
            m.Status = Status;
            return m;
        }

        /// <summary>Dictionnaire sérialisable (réseau + historique).</summary>
        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["id"] = Id;
            d["k"] = (int)Kind;
            d["snd"] = SenderId;
            d["peer"] = PeerId;
            d["mine"] = IsMine;
            d["txt"] = Text;
            d["ts"] = TimestampUtc;
            if (ReplyToId != null) d["rep"] = ReplyToId;
            if (QuoteAuthor != null) d["qa"] = QuoteAuthor;
            if (QuoteText != null) d["qt"] = QuoteText;
            if (Reaction != null) d["re"] = Reaction;
            if (Waveform != null) d["wf"] = Waveform;
            if (Deleted) d["del"] = true;
            if (LocalPath != null) d["path"] = LocalPath;
            if (FileName != null) d["fn"] = FileName;
            d["fs"] = FileSize;
            d["dur"] = VoiceDurationSec;
            d["st"] = (int)Status;
            return d;
        }

        /// <summary>Recrée un message depuis le dictionnaire JSON. Null si invalide.</summary>
        public static ChatMessage FromDict(Dictionary<string, object> d)
        {
            if (d == null)
            {
                return null;
            }
            ChatMessage m = new ChatMessage();
            m.Id = MiniJson.AsString(d, "id");
            m.Kind = (MessageKind)MiniJson.AsLong(d, "k", (long)MessageKind.Text);
            m.SenderId = MiniJson.AsString(d, "snd");
            m.PeerId = MiniJson.AsString(d, "peer");
            m.IsMine = MiniJson.AsBool(d, "mine");
            m.Text = MiniJson.AsString(d, "txt");
            m.TimestampUtc = MiniJson.AsLong(d, "ts");
            m.ReplyToId = MiniJson.AsString(d, "rep");
            m.QuoteAuthor = MiniJson.AsString(d, "qa");
            m.QuoteText = MiniJson.AsString(d, "qt");
            m.Reaction = MiniJson.AsString(d, "re", "");
            m.Waveform = MiniJson.AsString(d, "wf");
            m.Deleted = MiniJson.AsBool(d, "del", false);
            m.LocalPath = MiniJson.AsString(d, "path");
            m.FileName = MiniJson.AsString(d, "fn");
            m.FileSize = MiniJson.AsLong(d, "fs");
            m.VoiceDurationSec = (int)MiniJson.AsLong(d, "dur");
            m.Status = (DeliveryStatus)MiniJson.AsLong(d, "st", (long)DeliveryStatus.Sent);
            if (m.Id == null || m.Id.Length == 0)
            {
                return null;
            }
            return m;
        }
    }
}
