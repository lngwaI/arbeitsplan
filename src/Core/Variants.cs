using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arbeitsplan.Core
{
    // Herstellungsvarianten eines Blocks (Entry.Types aus den Puffern, Schritt-Typ im Paket) mit Anzahl je Variante, mit "|" getrennt und kanonisch sortiert:
    // "mobil|stationaer" (je 1, altes Format bleibt gueltig), "mobil:2|stationaer" (2x mobil + 1x stationaer). Anzahl 1..Max.
    // Welche Varianten es gibt, legt der Admin an der Zeile fest (RowInfo.Variants), nicht der Code.
    public static class VariantSet
    {
        public const int Max = 9;

        // Variante -> Anzahl (>= 1), nach Namen sortiert; "name:n" mit ungueltigem n zaehlt als 1, doppelte Namen: groesste Anzahl
        public static SortedDictionary<string, int> Counts(string v)
        {
            SortedDictionary<string, int> d = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string t in (v ?? "").Split('|'))
            {
                string x = t.Trim(); int n = 1, c = x.LastIndexOf(':');
                if (c > 0) { if (!int.TryParse(x.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) n = 1; x = x.Substring(0, c).Trim(); }
                if (x.Length == 0) continue;
                n = Math.Max(1, Math.Min(Max, n));
                int o; if (!d.TryGetValue(x, out o) || n > o) d[x] = n;
            }
            return d;
        }

        static string Join(IDictionary<string, int> d)
        {
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, int> kv in d) if (kv.Value > 0) l.Add(kv.Value > 1 ? kv.Key + ":" + kv.Value.ToString(CultureInfo.InvariantCulture) : kv.Key);
            l.Sort(StringComparer.Ordinal);
            return string.Join("|", l.ToArray());
        }

        // Nur die Namen (fuer die Pflichtpunkt-Regeln: Anzahl spielt dort keine Rolle)
        public static List<string> Of(string v) { return new List<string>(Counts(v).Keys); }
        public static bool Contains(string v, string x) { return Counts(v).ContainsKey(x); }
        public static int Count(string v, string x) { int n; return Counts(v).TryGetValue(x, out n) ? n : 0; }

        // Anzahl setzen (0 = Variante weg, hoechstens Max)
        public static string With(string v, string x, int n)
        {
            SortedDictionary<string, int> d = Counts(v);
            n = Math.Max(0, Math.Min(Max, n));
            if (n == 0) d.Remove(x); else d[x] = n;
            return Join(d);
        }

        // "2× mobil + stationaer"
        public static string Display(string v)
        {
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, int> kv in Counts(v)) l.Add(kv.Value > 1 ? kv.Value + "× " + kv.Key : kv.Key);
            return string.Join(" + ", l.ToArray());
        }
    }
}
