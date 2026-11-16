using System;
using System.Collections.Generic;
using System.Drawing;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>Géométrie mesurée d'une bulle (cache de mise en page).</summary>
    public sealed class BubbleLayout
    {
        /// <summary>Largeur totale de la bulle (queue comprise).</summary>
        public int W;
        /// <summary>Hauteur totale de la bulle.</summary>
        public int H;
        /// <summary>Nombre de lignes de texte mesurées.</summary>
        public int Lines;
        /// <summary>Largeur de la dernière ligne de texte.</summary>
        public float LastLineW;
        /// <summary>Hauteur d'une ligne de texte.</summary>
        public float LineH;
        /// <summary>true si la méta (heure+coches) tient sur la dernière ligne.</summary>
        public bool MetaInline;
        /// <summary>true si une puce de date précède ce message.</summary>
        public bool HasDate;
        /// <summary>Libellé de la puce de date.</summary>
        public string DateLabel;
        /// <summary>Largeur de la zone de texte dans la bulle.</summary>
        public int TextW;
        /// <summary>Image chargée pour Kind=Image (mise en cache).</summary>
        public Image Thumb;
        /// <summary>true si le chargement de l'image a échoué.</summary>
        public bool ThumbFailed;
    }

    /// <summary>
    /// État complet d'une conversation : messages, non-lus, brouillon,
    /// « en train d'écrire… », défilement, mise en page des bulles, citation.
    /// </summary>
    public sealed class Conversation
    {
        public string PeerId;
        public string PeerName = "";
        public string PeerBio = "";
        public bool Online;
        public long LastSeenUtc;

        public readonly List<ChatMessage> Messages = new List<ChatMessage>();
        public bool Loaded;
        public int Unread;
        public string Draft = "";

        /// <summary>Le pair est « en train d'écrire… » jusqu'à cette date locale.</summary>
        public DateTime TypingUntil = DateTime.MinValue;
        public string TypingPeerName;
        public bool Typing { get { return DateTime.Now < TypingUntil; } }

        /// <summary>Décalage vertical du fil (px depuis le haut du contenu).</summary>
        public int ScrollY;
        /// <summary>Hauteur totale mesurée du fil.</summary>
        public int ContentH;
        /// <summary>Le fil est-il collé en bas (auto-scroll) ?</summary>
        public bool AtBottom = true;

        /// <summary>Id du message survolé (boutons d'action).</summary>
        public string HoverMsgId;
        /// <summary>Id du vocal en cours de lecture.</summary>
        public string PlayingId;
        /// <summary>Message cité (réponse en préparation).</summary>
        public ChatMessage ReplyTo;

        /// <summary>Cache de mise en page des bulles (clé = message.Id).</summary>
        public readonly Dictionary<string, BubbleLayout> Layouts =
            new Dictionary<string, BubbleLayout>();

        public ChatMessage Last
        {
            get { return Messages.Count == 0 ? null : Messages[Messages.Count - 1]; }
        }

        public ChatMessage Find(string id)
        {
            if (id == null) return null;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].Id == id) return Messages[i];
            }
            return null;
        }

        /// <summary>Invalide la mise en page (changement de thème/police/taille).</summary>
        public void InvalidateLayout()
        {
            Layouts.Clear();
            ContentH = 0;
        }
    }
}
