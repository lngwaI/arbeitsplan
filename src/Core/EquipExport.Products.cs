using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    public static partial class EquipExport
    {
        public const string AllProducts = "(alle Produkte)";

        // Erkennung der Nummern: Schema (EquipmentMatcher.Pattern) plus Geraete aus Stammdaten und Positionen.
        class IdMatcher
        {
            readonly Regex rx;
            readonly Dictionary<string, string> canon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // Schreibweise der Liste
            readonly HashSet<string> shorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // "<Kurzname>: <Wert>" mit genau einem Wort als Wert, optional "(Ausweich: <Wert>)"; mehrere Woerter sind freier Text
            static readonly Regex LineRx = new Regex(@"^\s*([^:\n]{1,30}):[ \t]+(\S+)(?:[ \t]+\(Ausweich:[ \t]+([^\s)]+)\))?\s*$", RegexOptions.Compiled);

            public IdMatcher(Catalog cat)
            {
                foreach (EquipDevice d in cat.Master.Devices) AddId(d.Id);
                foreach (EquipPosition p in cat.Positions) { if (p.Short.Length > 0) shorts.Add(p.Short); foreach (string id in p.Ids) AddId(id); }
                List<string> ids = new List<string>(canon.Keys);
                ids.Sort(delegate(string a, string b) { return b.Length != a.Length ? b.Length - a.Length : string.CompareOrdinal(a, b); });
                string list = string.Join("|", ids.ConvertAll<string>(Regex.Escape).ToArray());
                // Wortgrenzen auch gegen "-": "TFF1" trifft nicht in "TFF1-2". Das Schema bleibt Gross/klein-genau.
                rx = new Regex((ids.Count > 0 ? @"(?<![\w-])(?i:" + list + @")(?![\w-])|" : "") + EquipmentMatcher.Pattern, RegexOptions.CultureInvariant);
            }

            void AddId(string id)
            {
                id = (id ?? "").Trim();
                if (id.Length >= 2 && !canon.ContainsKey(id)) canon[id] = id;   // ein Zeichen wuerde ueberall treffen
            }

            public List<string> Find(string txt)
            {
                List<string> r = new List<string>(); string c;
                foreach (Match m in rx.Matches(txt)) r.Add(canon.TryGetValue(m.Value, out c) ? c : m.Value);
                return r;
            }

            // Werte aus Geraetezeilen bekannter Positionen, die nichts treffen; nur mit Ziffer (sonst meist Notiz wie "Reserve")
            public void Unknown(string lines, List<string> res)
            {
                foreach (string line in (lines ?? "").Split('\n'))
                {
                    Match m = LineRx.Match(line);
                    if (!m.Success || !shorts.Contains(m.Groups[1].Value.Trim())) continue;
                    AddUnknown(m.Groups[2].Value, res);
                    if (m.Groups[3].Success) AddUnknown(m.Groups[3].Value, res);
                }
            }

            void AddUnknown(string v, List<string> res)
            {
                bool digit = false;
                foreach (char ch in v) if (char.IsDigit(ch)) digit = true;
                if (digit && !rx.IsMatch(v) && !res.Exists(delegate(string x) { return string.Equals(x, v, StringComparison.OrdinalIgnoreCase); })) res.Add(v);
            }
        }

        // Zuordnung Laufnummern-Praefix -> Produkt aus den Paketen im Paketordner und den Produkten des Katalogs.
        public class ProductMap
        {
            readonly Dictionary<string, string> codes = new Dictionary<string, string>();   // Praefix -> Produktcode
            readonly Catalog cat;

            public ProductMap(string folder, Catalog cat)
            {
                this.cat = cat;
                string[] files = new string[0];
                try { files = Directory.GetFiles(folder ?? "", "*.apkg"); } catch (Exception) { }   // Ordner fehlt: Produkte nur aus dem Katalog
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    Package p;
                    try { p = Package.Load(f); } catch (Exception) { continue; }   // nicht lesbare Pakete ueberspringen
                    if (p.Prefix.Length > 0 && p.ProductCode.Length > 0 && !codes.ContainsKey(p.Prefix)) codes[p.Prefix] = p.ProductCode;
                }
                foreach (ProductProfile pr in cat.Products) if (Package.IsCode4(pr.Code) && !codes.ContainsKey(pr.Code)) codes[pr.Code] = pr.Code;
            }

            public void Apply(CampaignSpan c)
            {
                string code;
                if (!codes.TryGetValue(c.Prefix, out code)) return;
                ProductProfile p = cat.Product(code);
                c.ProductCode = code; c.ProductName = p != null ? p.Name : "";
            }

            // Eintraege des Produktfelds: "(alle Produkte)", "Name (Code)", Codes aus Paketen ohne Katalog-Eintrag
            public List<string> Entries()
            {
                List<string> r = new List<string>();
                foreach (ProductProfile p in cat.Products) if (p.Code.Length > 0) r.Add(p.Name.Length > 0 ? p.Name + " (" + p.Code + ")" : p.Code);
                foreach (string code in codes.Values) if (cat.Product(code) == null && !r.Contains(code)) r.Add(code);
                r.Sort(StringComparer.CurrentCultureIgnoreCase);
                r.Insert(0, AllProducts);
                return r;
            }

            // Praefixe zur Eingabe (Eintrag, Code, Name oder 4-stelliges Praefix); null = alle Produkte; leere Liste: why nennt den Grund
            List<string> Prefixes(string input, out string why)
            {
                why = "";
                string t = (input ?? "").Trim();
                if (t.Length == 0 || t == AllProducts) return null;
                Match m = Regex.Match(t, @"\(([^()]+)\)\s*$");
                string code = m.Success ? m.Groups[1].Value.Trim() : t;
                List<string> res = new List<string>();
                if (Package.IsCode4(code)) res.Add(code);
                bool known = false;
                foreach (ProductProfile p in cat.Products)
                    if (Same(p.Code, code) || Same(p.Name, code)) { known = true; AddPrefixes(p.Code, res); }
                AddPrefixes(code, res);
                if (res.Count == 0)
                    why = known ? "Für „" + t + "“ liegt kein Paket mit Laufnummern-Präfix im Paketordner" : "Kein Produkt „" + t + "“ gefunden (Name, Code oder 4-stelliges Präfix eingeben)";
                return res;
            }

            void AddPrefixes(string code, List<string> res)
            {
                foreach (KeyValuePair<string, string> kv in codes) if (Same(kv.Value, code) && !res.Contains(kv.Key)) res.Add(kv.Key);
            }

            static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

            // Kampagnen zur Produkt-Eingabe, mit Produkt. Leere Liste: why nennt den Grund.
            public List<CampaignSpan> Campaigns(IDataStore store, DateTime from, DateTime to, string input, out string why)
            {
                List<string> prefixes = Prefixes(input, out why);
                List<CampaignSpan> res = new List<CampaignSpan>();
                if (prefixes == null) res = EquipExport.Campaigns(store, from, to, "");
                else foreach (string px in prefixes) res.AddRange(EquipExport.Campaigns(store, from, to, px));
                SortSpans(res);
                foreach (CampaignSpan c in res) Apply(c);
                if (res.Count == 0 && why.Length == 0)
                    why = prefixes == null ? "Keine Laufnummern im Plan zwischen " + from.ToString("dd.MM.yyyy") + " und " + to.ToString("dd.MM.yyyy")
                        : "Keine Läufe im Plan für „" + input.Trim() + "“ (Präfix " + string.Join(", ", prefixes.ToArray()) + ")";
                return res;
            }
        }
    }
}
