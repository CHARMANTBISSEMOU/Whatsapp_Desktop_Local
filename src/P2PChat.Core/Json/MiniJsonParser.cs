using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace P2PChat.Core.Json
{
    /// <summary>MiniJson - Partie 2 : analyse (parser) JSON.</summary>
    public static partial class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException("json");
            int i = 0;
            object v = ParseValue(json, ref i, 0);
            SkipWs(json, ref i);
            if (i != json.Length) throw new FormatException("JSON : contenu après la fin.");
            return v;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static object ParseValue(string s, ref int i, int depth)
        {
            if (depth > 128) throw new FormatException("JSON : imbrication trop profonde.");
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON : fin inattendue.");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return ParseNumber(s, ref i);
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
            {
                throw new FormatException("JSON : mot clé attendu à " + i);
            }
            i += word.Length;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i, int depth)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON : clé attendue.");
                string k = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON : ':' attendu.");
                i++;
                object v = ParseValue(s, ref i, depth + 1);
                d[k] = v;
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON : fin inattendue (objet).");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("JSON : ',' ou '}' attendu.");
            }
        }

        private static List<object> ParseArray(string s, ref int i, int depth)
        {
            List<object> list = new List<object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                object v = ParseValue(s, ref i, depth + 1);
                list.Add(v);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON : fin inattendue (tableau).");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("JSON : ',' ou ']' attendu.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("JSON : chaîne non fermée.");
                char c = s[i];
                if (c == '"') { i++; return sb.ToString(); }
                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length) throw new FormatException("JSON : échappement incomplet.");
                    char e = s[i];
                    switch (e)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case 'b': sb.Append('\b'); i++; break;
                        case 'f': sb.Append('\f'); i++; break;
                        case 'u':
                            if (i + 4 >= s.Length) throw new FormatException("JSON : \\u incomplet.");
                            int code = int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            i += 5;
                            sb.Append((char)code);
                            break;
                        default: throw new FormatException("JSON : échappement inconnu.");
                    }
                }
                else { sb.Append(c); i++; }
            }
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            string tok = s.Substring(start, i - start);
            long l;
            if (tok.IndexOf('.') < 0 && tok.IndexOf('e') < 0 && tok.IndexOf('E') < 0 &&
                long.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
            {
                return l;
            }
            double d;
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
            throw new FormatException("JSON : nombre invalide.");
        }
    }
}
