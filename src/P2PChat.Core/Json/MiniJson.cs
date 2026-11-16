using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace P2PChat.Core.Json
{
    /// <summary>
    /// Mini sérialiseur/analyseur JSON maison - ZÉRO dépendance externe.
    /// Partie 1 : sérialisation + lecture sécurisée de dictionnaires.
    /// Partie 2 (MiniJsonParser.cs) : analyse.
    /// </summary>
    public static partial class MiniJson
    {
        // ---------------- Sérialisation ----------------

        public static string Serialize(object value)
        {
            StringBuilder sb = new StringBuilder(256);
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is char) { WriteString(sb, ((char)v).ToString()); return; }
            if (v is bool) { sb.Append(((bool)v) ? "true" : "false"); return; }
            if (v is double || v is float || v is decimal)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (v is long || v is int || v is short || v is byte || v is sbyte ||
                v is ushort || v is uint || v is ulong)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            IDictionary dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    Write(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            IEnumerable en = v as IEnumerable;
            if (en != null)
            {
                sb.Append('[');
                bool f = true;
                foreach (object x in en)
                {
                    if (!f) sb.Append(',');
                    f = false;
                    Write(sb, x);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
