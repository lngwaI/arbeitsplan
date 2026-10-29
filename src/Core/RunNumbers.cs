using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    // Laufnummer XXYYZZZZ (8 Ziffern) im Blockinhalt: Product = Praefix XXYY (erste 4 Ziffern, Package.RunPrefix; vom freien Produktcode unabhaengig), Lauf = letzte 4.
    public class RunRef
    {
        public string Product, Run;
    }

    public static class RunNumbers
    {
        // Der eine Parser fuer Anzeige und Kampagnen-Vorschlag (Package.PastRuns). Gewertet je Zeile:
        // "Lauf <8 Ziffern>" an beliebiger Stelle, sonst eine Hauptlauf-Zeile "<Titel> <8 Ziffern>" (Nummer am Zeilenende,
        // z. B. "Mischen 13570030"). Ignoriert: Unterlauf-Zeilen ("Unterlauf … (aus …)"), Kennungszeilen ("PA 40010001":
        // davor nur 1-4 Grossbuchstaben) und Datumszahlen (JJJJMMTT/TTMMJJJJ) ausser direkt nach "Lauf".
        static readonly Regex LaufRx = new Regex(@"\bLauf\s+(\d{8})(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex TailRx = new Regex(@"^(.*\S)\s+(\d{8})$", RegexOptions.Compiled);
        static readonly Regex IdentRx = new Regex(@"^[A-ZÄÖÜ]{1,4}$", RegexOptions.Compiled);

        // Alle Laufnummern des Textes (ohne Doppelte, Reihenfolge des Auftretens).
        public static List<RunRef> Find(string content) { return Find(content, null); }

        // Wie Find(Text), Notizzeilen des Blocks zaehlen nicht.
        public static List<RunRef> Find(Entry e, string product = null) { return Find(Notes.ContentText(e), product); }

        // product: nur Laeufe mit diesem Praefix (XXYY), null = alle.
        public static List<RunRef> Find(string content, string product)
        {
            List<RunRef> res = new List<RunRef>();
            if (string.IsNullOrEmpty(content)) return res;
            foreach (string raw in content.Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length < 8 || l.StartsWith("Unterlauf", StringComparison.OrdinalIgnoreCase)) continue;
                bool any = false;
                foreach (Match m in LaufRx.Matches(l)) { Add(res, m.Groups[1].Value, product); any = true; }
                if (any) continue;
                Match t = TailRx.Match(l);
                if (t.Success && !IdentRx.IsMatch(t.Groups[1].Value) && !IsDate(t.Groups[2].Value)) Add(res, t.Groups[2].Value, product);
            }
            return res;
        }

        static void Add(List<RunRef> res, string no, string product)
        {
            string p = no.Substring(0, 4), r = no.Substring(4);
            if (product != null && p != product) return;
            if (!res.Exists(delegate(RunRef x) { return x.Product == p && x.Run == r; })) { RunRef n = new RunRef(); n.Product = p; n.Run = r; res.Add(n); }
        }

        static bool IsDate(string d8)
        {
            DateTime t;
            return DateTime.TryParseExact(d8, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && t.Year >= 2000 && t.Year < 2100
                || DateTime.TryParseExact(d8, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && t.Year >= 2000 && t.Year < 2100;
        }

        // Stabiler Index (0..count-1) aus dem Produktcode, z. B. fuer eine Farbpalette.
        public static int ProductIndex(string product, int count)
        {
            int h = 17;
            foreach (char c in product) h = h * 31 + c;
            return (h & 0x7fffffff) % count;
        }

        // "Läufe 1357xxxx: 0030, 0031" je Praefix eine Zeile.
        public static string Describe(List<RunRef> runs)
        {
            List<string> prods = new List<string>();
            foreach (RunRef r in runs) if (!prods.Contains(r.Product)) prods.Add(r.Product);
            List<string> lines = new List<string>();
            foreach (string p in prods)
            {
                List<string> nos = new List<string>();
                foreach (RunRef r in runs) if (r.Product == p) nos.Add(r.Run);
                lines.Add("Läufe " + p + "xxxx: " + string.Join(", ", nos.ToArray()));
            }
            return string.Join("\n", lines.ToArray());
        }
    }
}
