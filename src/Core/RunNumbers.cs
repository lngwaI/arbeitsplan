using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    // Laufnummer XXYYZZZZ (8 Ziffern) im Blockinhalt: Produktcode = erste 4 Ziffern, Lauf = letzte 4.
    public class RunRef
    {
        public string Product, Run;
    }

    public static class RunNumbers
    {
        static readonly Regex Pattern = new Regex(@"(?<!\d)(\d{4})(\d{4})(?!\d)", RegexOptions.Compiled);

        // Alle Laufnummern des Textes (ohne Doppelte, Reihenfolge des Auftretens).
        public static List<RunRef> Find(string content)
        {
            List<RunRef> res = new List<RunRef>();
            if (string.IsNullOrEmpty(content)) return res;
            foreach (Match m in Pattern.Matches(content))
            {
                string p = m.Groups[1].Value, r = m.Groups[2].Value;
                if (!res.Exists(delegate(RunRef x) { return x.Product == p && x.Run == r; })) { RunRef n = new RunRef(); n.Product = p; n.Run = r; res.Add(n); }
            }
            return res;
        }

        // Stabiler Index (0..count-1) aus dem Produktcode, z. B. fuer eine Farbpalette.
        public static int ProductIndex(string product, int count)
        {
            int h = 17;
            foreach (char c in product) h = h * 31 + c;
            return (h & 0x7fffffff) % count;
        }

        // "Produkt 1357 · Lauf 0030, 0031" je Produkt eine Zeile.
        public static string Describe(List<RunRef> runs)
        {
            List<string> prods = new List<string>();
            foreach (RunRef r in runs) if (!prods.Contains(r.Product)) prods.Add(r.Product);
            List<string> lines = new List<string>();
            foreach (string p in prods)
            {
                List<string> nos = new List<string>();
                foreach (RunRef r in runs) if (r.Product == p) nos.Add(r.Run);
                lines.Add("Produkt " + p + " · Lauf " + string.Join(", ", nos.ToArray()));
            }
            return string.Join("\n", lines.ToArray());
        }
    }
}
