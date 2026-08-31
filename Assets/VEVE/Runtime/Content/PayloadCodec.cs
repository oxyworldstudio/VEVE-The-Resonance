using System;
using System.Collections.Generic;
using System.Text;

namespace VEVE.Content
{
    /// <summary>
    /// Compact "line codec" - key=value, '%' escapes, one per line. Deliberately
    /// dependency-free (works on netstandard 2.0/2.1 where a JSON serializer
    /// would add a package), stable, and human-readable in a serialized .asset.
    /// </summary>
    public static class PayloadCodec
    {
        public static string Encode(IEnumerable<KeyValuePair<string, string>> fields)
        {
            if (fields == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var f in fields)
            {
                sb.Append(f.Key).Append('=').Append(Escape(f.Value)).Append('\n');
            }
            return sb.ToString();
        }

        public static Dictionary<string, string> Decode(string payload)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(payload)) return result;
            foreach (string line in payload.Split('\n'))
            {
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                result[line.Substring(0, eq)] = Unescape(line.Substring(eq + 1));
            }
            return result;
        }

        public static string Escape(string v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            var sb = new StringBuilder(v.Length + 4);
            foreach (char c in v)
            {
                if (c == '%') sb.Append("%25");
                else if (c == '=') sb.Append("%3D");
                else if (c == '\n') sb.Append("%0A");
                else if (c == '|') sb.Append("%7C");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Unescape(string v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            var sb = new StringBuilder(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] == '%' && i + 2 < v.Length)
                {
                    string two = v.Substring(i + 1, 2);
                    if (two == "25") { sb.Append('%'); i += 2; continue; }
                    if (two == "3D") { sb.Append('='); i += 2; continue; }
                    if (two == "0A") { sb.Append('\n'); i += 2; continue; }
                    if (two == "7C") { sb.Append('|'); i += 2; continue; }
                }
                sb.Append(v[i]);
            }
            return sb.ToString();
        }

        public static readonly char[] ListSeparator = { '|' };
    }

    /// <summary>MissionTemplate <-> payload, stable field order, used by the exporter/asset pipeline (C7).</summary>
    public static class MissionPayloadCodec
    {
        public static string Encode(MissionTemplate t)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id", t.id ?? string.Empty),
                new KeyValuePair<string, string>("title", t.title ?? string.Empty),
                new KeyValuePair<string, string>("region", t.regionKey ?? string.Empty),
                new KeyValuePair<string, string>("par", ((int)t.parSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("pairs", ((int)t.enemySquadPairs).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("alert", ((float)t.alertBias).ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("intelw", t.intelObjectiveWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("objs", JoinObjectives(t)),
            };
            return PayloadCodec.Encode(fields);
        }

        public static MissionTemplate Decode(string payload)
        {
            var map = PayloadCodec.Decode(payload);
            return new MissionTemplate
            {
                id = Get(map, "id"),
                title = Get(map, "title"),
                regionKey = Get(map, "region"),
                parSeconds = GetInt(map, "par"),
                enemySquadPairs = GetInt(map, "pairs"),
                alertBias = GetFloat(map, "alert"),
                intelObjectiveWeight = ParseDouble(Get(map, "intelw")),
                objectiveSummary = SplitPayloadList(Get(map, "objs"))
            };
        }

        private const char ListDelimiter = '|';

        private static string Get(Dictionary<string, string> m, string k)
        {
            return m != null && m.TryGetValue(k, out string v) ? v : string.Empty;
        }

        private static int GetInt(Dictionary<string, string> m, string k)
        {
            string raw = Get(m, k);
            return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static float GetFloat(Dictionary<string, string> m, string k)
        {
            string raw = Get(m, k);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static double ParseDouble(string raw)
        {
            return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0.0;
        }

        public static string JoinObjectives(MissionTemplate t)
        {
            string[] objs = t.objectiveSummary;
            if (objs == null || objs.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < objs.Length; i++)
            {
                if (i > 0) sb.Append(ListDelimiter);
                sb.Append(PayloadCodec.Escape(objs[i]));
            }
            return sb.ToString();
        }

        public static string[] SplitPayloadList(string packed)
        {
            if (string.IsNullOrEmpty(packed)) return Array.Empty<string>();
            string[] raw = packed.Split(ListDelimiter);
            var items = new List<string>(raw.Length);
            foreach (string r in raw)
                if (!string.IsNullOrEmpty(r)) items.Add(PayloadCodec.Unescape(r));
            return items.ToArray();
        }
    }
}
