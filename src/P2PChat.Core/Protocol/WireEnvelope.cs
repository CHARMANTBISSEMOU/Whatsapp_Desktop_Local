using System;
using System.Collections.Generic;
using System.Text;
using P2PChat.Core.Json;

namespace P2PChat.Core.Protocol
{
    /// <summary>
    /// Enveloppe de transport réseau : métadonnées + payload JSON.
    /// Après le handshake ECDH, chaque trame TCP chiffrée contient exactement
    /// une enveloppe sérialisée en UTF-8. Le format "Dic" de D est libre
    /// selon le type T (voir NetworkEngine).
    /// </summary>
    public sealed class WireEnvelope
    {
        /// <summary>Version de protocole (1).</summary>
        public int V = 1;

        /// <summary>Type de trame.</summary>
        public WireType T;

        /// <summary>PeerInfo.Id de l'expéditeur de la trame.</summary>
        public string Sid;

        /// <summary>Horodatage (ms epoch UTC).</summary>
        public long Ts;

        /// <summary>Payload JSON ( MiniJson).</summary>
        public string D;

        /// <summary>Crée une enveloppe prête à l'emploi.</summary>
        public static WireEnvelope Create(WireType type, string senderId, string payloadJson)
        {
            WireEnvelope e = new WireEnvelope();
            e.V = 1;
            e.T = type;
            e.Sid = senderId;
            e.Ts = ChatMessage.NowUtcMs();
            e.D = payloadJson == null ? "{}" : payloadJson;
            return e;
        }

        /// <summary>Payload parsé en dictionnaire.</summary>
        public Dictionary<string, object> Payload()
        {
            if (D == null || D.Length == 0)
            {
                return new Dictionary<string, object>();
            }
            return MiniJson.ParseObject(D);
        }

        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["v"] = V;
            d["t"] = (int)T;
            d["sid"] = Sid;
            d["ts"] = Ts;
            d["d"] = D;
            return d;
        }

        /// <summary>Sérialise l'enveloppe en JSON UTF-8 (prête à chiffrer).</summary>
        public byte[] Encode()
        {
            return Encoding.UTF8.GetBytes(MiniJson.Serialize(ToDict()));
        }

        /// <summary>Reconstitue une enveloppe depuis des octets JSON UTF-8.</summary>
        public static WireEnvelope Decode(byte[] raw)
        {
            return FromDict(MiniJson.ParseObject(Encoding.UTF8.GetString(raw)));
        }

        public static WireEnvelope FromDict(Dictionary<string, object> d)
        {
            if (d == null)
            {
                throw new FormatException("Enveloppe invalide (null).");
            }
            WireEnvelope e = new WireEnvelope();
            e.V = (int)MiniJson.AsLong(d, "v", 1);
            e.T = (WireType)MiniJson.AsLong(d, "t", -1);
            e.Sid = MiniJson.AsString(d, "sid");
            e.Ts = MiniJson.AsLong(d, "ts");
            e.D = MiniJson.AsString(d, "d", "{}");
            if (e.T < (WireType)1)
            {
                throw new FormatException("Enveloppe invalide (type inconnu).");
            }
            return e;
        }
    }
}
