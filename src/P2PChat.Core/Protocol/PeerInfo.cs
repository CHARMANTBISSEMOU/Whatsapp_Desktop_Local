using System.Collections.Generic;
using P2PChat.Core.Json;

namespace P2PChat.Core.Protocol
{
    /// <summary>
    /// Identité publique d'un pair (le même objet décrit aussi "soi-même").
    /// Sérialisable en JSON via MiniJson (dictionnaire de primitives).
    /// </summary>
    public sealed class PeerInfo
    {
        /// <summary>Identifiant permanent (GUID). Généré une fois, stocké dans le profil.</summary>
        public string Id;

        /// <summary>Pseudo affiché dans WhatsApp comme nom.</summary>
        public string Pseudo;

        /// <summary>Petite phrase de statut ("Disponible", etc.) - facultatif.</summary>
        public string Bio;

        /// <summary>Horodatage de la dernière mise à jour (ms epoch UTC).</summary>
        public long UpdatedUtc;

        /// <summary>true si le pair est actuellement joignable sur le réseau local.</summary>
        public bool Online;

        public PeerInfo()
        {
            Id = "";
            Pseudo = "";
            Bio = "";
        }

        /// <summary>Copie complète.</summary>
        public PeerInfo Clone()
        {
            PeerInfo p = new PeerInfo();
            p.Id = Id;
            p.Pseudo = Pseudo;
            p.Bio = Bio;
            p.UpdatedUtc = UpdatedUtc;
            p.Online = Online;
            return p;
        }

        /// <summary>Renvoie une copie propre pour envoi réseau (toujours Online=true).</summary>
        public PeerInfo SanitizedClone()
        {
            PeerInfo p = Clone();
            p.Online = true;
            return p;
        }

        /// <summary>Dictionnaire sérialisable via MiniJson.</summary>
        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["id"] = Id;
            d["pseudo"] = Pseudo;
            d["bio"] = Bio;
            d["upd"] = UpdatedUtc;
            d["on"] = Online;
            return d;
        }

        /// <summary>Recrée un PeerInfo depuis le dictionnaire JSON reçu. Retourne null si invalide.</summary>
        public static PeerInfo FromDict(Dictionary<string, object> d)
        {
            if (d == null)
            {
                return null;
            }
            PeerInfo p = new PeerInfo();
            p.Id = MiniJson.AsString(d, "id");
            p.Pseudo = MiniJson.AsString(d, "pseudo");
            p.Bio = MiniJson.AsString(d, "bio");
            p.UpdatedUtc = MiniJson.AsLong(d, "upd");
            p.Online = MiniJson.AsBool(d, "on");
            if (p.Id == null || p.Id.Length == 0)
            {
                return null;
            }
            return p;
        }
    }
}
