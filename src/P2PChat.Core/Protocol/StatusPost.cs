using System;
using System.Collections.Generic;
using P2PChat.Core.Json;

namespace P2PChat.Core.Protocol
{
    /// <summary>
    /// Statut façon WhatsApp (story) : texte sur fond coloré, expire après 24 h.
    /// Transporté par WireType.StatusPost ; persistance locale chiffrée
    /// (AppSettings.Statuses).
    /// </summary>
    public sealed class StatusPost
    {
        /// <summary>Identifiant unique (GUID "N").</summary>
        public string Id;

        /// <summary>PeerInfo.Id de l'auteur.</summary>
        public string SenderId;

        /// <summary>Texte du statut.</summary>
        public string Text;

        /// <summary>Couleur de fond (ARGB).</summary>
        public int Bg;

        /// <summary>Publication (ms epoch UTC).</summary>
        public long TimestampUtc;

        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["id"] = Id;
            d["snd"] = SenderId;
            d["txt"] = Text ?? "";
            d["bg"] = (long)Bg;
            d["ts"] = TimestampUtc;
            return d;
        }

        public static StatusPost FromDict(Dictionary<string, object> d)
        {
            if (d == null) return null;
            StatusPost s = new StatusPost();
            s.Id = MiniJson.AsString(d, "id");
            s.SenderId = MiniJson.AsString(d, "snd");
            s.Text = MiniJson.AsString(d, "txt");
            s.Bg = (int)MiniJson.AsLong(d, "bg");
            s.TimestampUtc = MiniJson.AsLong(d, "ts");
            if (string.IsNullOrEmpty(s.Id)) return null;
            return s;
        }
    }
}