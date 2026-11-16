using System;

namespace P2PChat.Core.Protocol
{
    /// <summary>
    /// Types de trames échangées entre pairs (le contenu est chiffré au niveau
    /// de la session TCP, sauf le handshake d'établissement des clés).
    /// </summary>
    public enum WireType : int
    {
        /// <summary>Premier message TCP : PeerInfo + clé publique ECDH du connecté.</summary>
        Handshake = 1,

        /// <summary>Acquittement du handshake : PeerInfo + clé publique ECDH de l'accepteur.</summary>
        HandshakeAck = 2,

        /// <summary>Message texte.</summary>
        TextMessage = 3,

        /// <summary>Message vocal (métadonnées; le son suit via FileChunks).</summary>
        VoiceMessage = 4,

        /// <summary>Annonce de fichier : nom, taille, id du message associé.</summary>
        FileMeta = 5,

        /// <summary>Morceau de fichier (binaire base64 dans le payload JSON).</summary>
        FileChunk = 6,

        /// <summary>Fin d'un transfert de fichier.</summary>
        FileDone = 7,

        /// <summary>Indicateur "en train d'écrire..."</summary>
        Typing = 8,

        /// <summary>Accusé de réception / de lecture d'un message.</summary>
        Receipt = 9,

        /// <summary>Déconnexion propre annoncée.</summary>
        Goodbye = 10,

        /// <summary>Réaction emoji à un message (r vide = retrait de la réaction).</summary>
        Reaction = 11,

        /// <summary>Édition d'un message déjà envoyé (payload {id, txt}).</summary>
        EditMessage = 12,

        /// <summary>Suppression d'un message pour tous les participants (payload {id}).</summary>
        DeleteForAll = 13,

        /// <summary>Métadonnées d'un groupe : {gid, name, members[]}.</summary>
        GroupMeta = 14,

        /// <summary>Message texte destiné à un groupe (payload = message, msg.PeerId = id du groupe).</summary>
        GroupText = 15,

        /// <summary>Statut/story publié : payload = StatusPost {id, snd, txt, bg, ts}.</summary>
        StatusPost = 16,

        /// <summary>Signalisation d'appel audio WebRTC : offre, réponse ou fin.</summary>
        CallSignal = 17
    }
}
