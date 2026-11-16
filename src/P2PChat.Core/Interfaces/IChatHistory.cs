using System.Collections.Generic;
using P2PChat.Core.Protocol;

namespace P2PChat.Core.Interfaces
{
    /// <summary>
    /// Contrat de persistance de l'historique des conversations,
    /// implémenté par P2PChat.App.Features.ChatHistoryStore.
    /// </summary>
    public interface IChatHistory
    {
        /// <summary>Ajoute un message à la conversation (clé = msg.PeerId) et persiste.</summary>
        void Append(ChatMessage msg);

        /// <summary>Met à jour le message déjà enregistré (édition, réaction, suppression ou accusé).</summary>
        void Update(ChatMessage msg);

        /// <summary>Charge les derniers messages d'une conversation (les plus récents LAST ; ordre chronologique).</summary>
        List<ChatMessage> Load(string peerId, int limit);

        /// <summary>Recherche les messages contenant le texte (insensible à la casse), ordre chronologique.</summary>
        List<ChatMessage> Search(string peerId, string query);

        /// <summary>Efface l'historique d'une conversation.</summary>
        void Clear(string peerId);

        /// <summary>Efface tout l'historique.</summary>
        void ClearAll();
    }
}
