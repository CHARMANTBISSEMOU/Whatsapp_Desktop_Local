using System;
using System.Collections.Generic;
using P2PChat.Core.Json;
using P2PChat.Core.Protocol;

namespace P2PChat.Tests
{
    /// <summary>
    /// Harnais de tests console : chaque test affiche PASS/ECHEC.
    /// Code retour 0 si tout passe, 1 sinon. dev-core enrichira ce harnais
    /// (chiffrement, réseau) via Tests\CryptoTests.cs et Tests\NetTests.cs.
    /// </summary>
    internal static class Program
    {
        private static int _failCount;

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                Console.WriteLine("PASS  " + name);
            }
            else
            {
                Console.WriteLine("ECHEC " + name);
                _failCount++;
            }
        }

        internal static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== P2PChat.Tests ===");
            TestMiniJson();
            TestModels();
            TestFraming();
            CryptoTests.Run(Check);
            Console.WriteLine();
            if (_failCount == 0)
            {
                Console.WriteLine("TOUS LES TESTS SONT PASSES.");
                return 0;
            }
            Console.WriteLine(_failCount + " ECHEC(S).");
            return 1;
        }

        private static void TestMiniJson()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["nom"] = "Charles";
            d["age"] = 20L;
            d["ok"] = true;
            d["rien"] = null;
            List<object> l = new List<object>();
            l.Add(1L);
            l.Add("deux");
            l.Add(false);
            d["list"] = l;

            string js = MiniJson.Serialize(d);
            Dictionary<string, object> back = MiniJson.ParseObject(js);
            Check("MiniJson : texte", MiniJson.AsString(back, "nom") == "Charles");
            Check("MiniJson : entier", MiniJson.AsLong(back, "age") == 20);
            Check("MiniJson : booleen", MiniJson.AsBool(back, "ok"));
            Check("MiniJson : null", back.ContainsKey("rien") && back["rien"] == null);
            List<object> l2 = MiniJson.AsList(back, "list");
            Check("MiniJson : liste",
                l2 != null && l2.Count == 3 &&
                MiniJson.AsLong(l2[0]) == 1 &&
                MiniJson.AsString(l2[1]) == "deux" &&
                MiniJson.AsBool(l2[2], true) == false);

            string emoji = "salut \uD83D\uDE00\uD83C\uDF89";
            Dictionary<string, object> ed = new Dictionary<string, object>();
            ed["e"] = emoji;
            string ejs = MiniJson.Serialize(ed);
            Check("MiniJson : emojis Unicode", MiniJson.AsString(MiniJson.ParseObject(ejs), "e") == emoji);

            string esc = "ligne1\nligne2 \"quoted\" back\\slash\ttab";
            Dictionary<string, object> sdd = new Dictionary<string, object>();
            sdd["s"] = esc;
            string sjs = MiniJson.Serialize(sdd);
            Check("MiniJson : echappements", MiniJson.AsString(MiniJson.ParseObject(sjs), "s") == esc);

            Check("MiniJson : Nombre negatif", Equals(MiniJson.Deserialize("-42"), -42L));
            object dbl = MiniJson.Deserialize("3.5");
            Check("MiniJson : flottant", dbl is double && Math.Abs((double)dbl - 3.5) < 0.0001);
            Check("MiniJson : JSON invalide rejete", Faile(() => MiniJson.ParseObject("{ert")));
        }

        private static bool Faile(Action a)
        {
            try
            {
                a();
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static void TestModels()
        {
            PeerInfo p = new PeerInfo();
            p.Id = "abc123";
            p.Pseudo = "Charles";
            p.Bio = "Disponible";
            p.UpdatedUtc = 123456L;
            p.Online = true;
            PeerInfo p2 = PeerInfo.FromDict(MiniJson.ParseObject(MiniJson.Serialize(p.ToDict())));
            Check("PeerInfo : roundtrip", p2 != null && p2.Pseudo == "Charles" && p2.Id == "abc123" && p2.Online);
            Check("PeerInfo : invalide rejete", PeerInfo.FromDict(new Dictionary<string, object>()) == null);

            ChatMessage m = ChatMessage.NewText("me", "peer1", true, "Coucou \uD83D\uDE00");
            ChatMessage m2 = ChatMessage.FromDict(MiniJson.ParseObject(MiniJson.Serialize(m.ToDict())));
            Check("ChatMessage : roundtrip", m2 != null && m2.Id == m.Id && m2.Text == "Coucou \uD83D\uDE00" && m2.IsMine);

            WireEnvelope env = WireEnvelope.Create(WireType.TextMessage, "me",
                MiniJson.Serialize(m.ToDict()));
            WireEnvelope env2 = WireEnvelope.Decode(env.Encode());
            Check("WireEnvelope : roundtrip", env2.T == WireType.TextMessage && env2.Sid == "me");
            ChatMessage m3 = ChatMessage.FromDict(env2.Payload());
            Check("WireEnvelope : payload message", m3 != null && m3.Id == m.Id);
        }

        private static void TestFraming()
        {
            byte[] buf = new byte[4];
            WireFraming.WritePrefix(buf, 70000);
            Check("WireFraming : prefix le", WireFraming.ReadPrefix(buf, 0) == 70000);
            Check("WireFraming : taille refusee", Faile(delegate { WireFraming.ValidateLength(0); })
                && Faile(delegate { WireFraming.ValidateLength(9 * 1024 * 1024); }));
        }
    }
}
