using System;
using System.Collections.Generic;
using System.Globalization;

namespace P2PChat.Core.Json
{
    /// <summary>MiniJson - Partie 3 : lecture sécurisée de dictionnaires et conversions.</summary>
    public static partial class MiniJson
    {
        /// <summary>Analyse et caste en dictionnaire. FormatException si ce n'est pas un objet.</summary>
        public static Dictionary<string, object> ParseObject(string json)
        {
            object v = Deserialize(json);
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d == null) throw new FormatException("JSON : objet attendu.");
            return d;
        }

        /// <summary>Sous-dictionnaire (null si absent ou pas un objet).</summary>
        public static Dictionary<string, object> AsDict(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        /// <summary>Sous-liste (null si absente ou pas un tableau).</summary>
        public static List<object> AsList(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as List<object>;
            return null;
        }

        public static string AsString(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsString(v, def);
            return def;
        }

        public static string AsString(Dictionary<string, object> d, string key)
        {
            return AsString(d, key, null);
        }

        /// <summary>Représentation texte d'une valeur brute (null si la valeur est null).</summary>
        public static string AsString(object v)
        {
            return AsString(v, null);
        }

        public static string AsString(object v, string def)
        {
            if (v == null) return def;
            if (v is string) return (string)v;
            if (v is long) return ((long)v).ToString(CultureInfo.InvariantCulture);
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            if (v is bool) return ((bool)v) ? "true" : "false";
            return v.ToString();
        }

        public static long AsLong(Dictionary<string, object> d, string key, long def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsLong(v, def);
            return def;
        }

        public static long AsLong(Dictionary<string, object> d, string key)
        {
            return AsLong(d, key, 0);
        }

        /// <summary>Conversion en long d'une valeur brute (0 si non numérique).</summary>
        public static long AsLong(object v)
        {
            return AsLong(v, 0);
        }

        public static long AsLong(object v, long def)
        {
            if (v is long) return (long)v;
            if (v is int) return (int)v;
            if (v is double) return (long)(double)v;
            return def;
        }

        public static double AsDouble(Dictionary<string, object> d, string key, double def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsDouble(v, def);
            return def;
        }

        public static double AsDouble(object v, double def)
        {
            if (v is double) return (double)v;
            if (v is long) return (long)v;
            return def;
        }

        public static bool AsBool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsBool(v, def);
            return def;
        }

        public static bool AsBool(Dictionary<string, object> d, string key)
        {
            return AsBool(d, key, false);
        }

        public static bool AsBool(object v, bool def)
        {
            if (v is bool) return (bool)v;
            if (v is long) return ((long)v) != 0;
            return def;
        }
    }
}
