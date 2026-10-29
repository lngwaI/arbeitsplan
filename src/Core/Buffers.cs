using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    // Ein Puffer eines Blocks (Zeile "Puffer"): Id laufend je Block (p1, p2 ...), freier Name, Typ (aus RowInfo.Variants), mehrzeiliger Text ohne Format.
    // Die angezeigte Nummer ergibt sich aus der Position in Entry.Buffers und wird nie gespeichert.
    public class BufferItem
    {
        public string Id = "", Name = "", Typ = "", Text = "";

        public BufferItem Clone() { return (BufferItem)MemberwiseClone(); }
        public bool SameValue(BufferItem o) { return o != null && Name == o.Name && Typ == o.Typ && Text == o.Text; }
        public string Key() { return Id + "|" + Typ + "|" + Name + "|" + Text + ";"; }
    }

    // Hilfsfunktionen fuer Entry.Buffers: Typen (VariantSet), Migration alter Daten, Rechte, Anzeige, Dreiwege-Merge.
    public static class BufferSet
    {
        // Typen der Puffer als VariantSet-String ("mobil:2|stationaer"); Eingabe fuer CheckEval (Anzahl spielt dort keine Rolle)
        public static string Types(List<BufferItem> l)
        {
            string s = "";
            foreach (BufferItem b in l) if (b.Typ.Length > 0) s = VariantSet.With(s, b.Typ, VariantSet.Count(s, b.Typ) + 1);
            return s;
        }

        public static int Count(List<BufferItem> l, string typ) { int n = 0; foreach (BufferItem b in l) if (b.Typ == typ) n++; return n; }

        static int Num(string id)
        {
            int n;
            return id != null && id.Length > 1 && id[0] == 'p' && int.TryParse(id.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        public static string NewId(params List<BufferItem>[] lists)
        {
            int m = 0;
            foreach (List<BufferItem> l in lists) if (l != null) foreach (BufferItem b in l) m = Math.Max(m, Num(b.Id));
            return "p" + (m + 1).ToString(CultureInfo.InvariantCulture);
        }

        public static BufferItem Add(List<BufferItem> l, string name, string typ, string text)
        {
            BufferItem b = new BufferItem(); b.Id = NewId(l); b.Name = name ?? ""; b.Typ = typ ?? ""; b.Text = text ?? "";
            l.Add(b); return b;
        }

        // Kopien hinten anhaengen (neue Ids)
        public static void AddRange(List<BufferItem> l, List<BufferItem> add)
        {
            foreach (BufferItem a in add) { BufferItem b = a.Clone(); b.Id = NewId(l); l.Add(b); }
        }

        public static List<BufferItem> CloneAll(List<BufferItem> l)
        {
            List<BufferItem> r = new List<BufferItem>();
            foreach (BufferItem b in l) r.Add(b.Clone());
            return r;
        }

        // Migration: alter Wert Entry.Variant ("mobil:2|stationaer") -> je Anzahl ein Puffer ohne Name (3 Eintraege)
        public static List<BufferItem> FromVariant(string v)
        {
            List<BufferItem> l = new List<BufferItem>();
            foreach (KeyValuePair<string, int> kv in VariantSet.Counts(v)) for (int i = 0; i < kv.Value; i++) Add(l, "", kv.Key, "");
            return l;
        }

        // Anzahl je Typ auf variantSet ("mobil:2|stationaer") bringen; vorhandene Puffer mit Name/Text bleiben, andere Typen entfallen
        public static void SetTypes(List<BufferItem> l, string variantSet)
        {
            SortedDictionary<string, int> want = VariantSet.Counts(variantSet);
            l.RemoveAll(delegate(BufferItem b) { return !want.ContainsKey(b.Typ); });   // auch Puffer ohne Typ
            foreach (KeyValuePair<string, int> kv in want) SetCount(l, kv.Key, kv.Value);
        }
        public static void SetTypes(Entry e, string variantSet) { SetTypes(e.Buffers, variantSet); }

        // Anzahl eines Typs setzen: vorhandene Puffer (mit Name/Text) bleiben, ueberzaehlige hinten entfallen, fehlende kommen namenlos ans Ende
        public static void SetCount(List<BufferItem> l, string typ, int n)
        {
            n = Math.Max(0, Math.Min(VariantSet.Max, n));
            int have = Count(l, typ);
            for (int i = l.Count - 1; i >= 0 && have > n; i--) if (l[i].Typ == typ) { l.RemoveAt(i); have--; }
            for (; have < n; have++) Add(l, "", typ, "");
        }

        // Ids eindeutig und nicht leer machen (nach dem Laden)
        public static void Normalize(List<BufferItem> l)
        {
            List<string> seen = new List<string>();
            foreach (BufferItem b in l)
            {
                if (b.Id.Length == 0 || seen.Contains(b.Id)) { b.Id = NewId(l); }
                seen.Add(b.Id);
            }
        }

        // XML (Sicherung, Plan-Datei, Paket-Block): <puffer id="p1" typ="mobil" name="...">Text</puffer>
        public static void WriteXml(System.Xml.XmlWriter w, List<BufferItem> l)
        {
            foreach (BufferItem b in l)
            {
                w.WriteStartElement("puffer");
                w.WriteAttributeString("id", b.Id); w.WriteAttributeString("typ", b.Typ); w.WriteAttributeString("name", b.Name);
                w.WriteString(b.Text);
                w.WriteEndElement();
            }
        }

        // Liest die <puffer>-Elemente unter e; ohne solche wird das alte Attribut "variante" migriert (je Anzahl ein namenloser Puffer).
        public static List<BufferItem> ReadXml(System.Xml.XmlElement e)
        {
            List<BufferItem> l = new List<BufferItem>();
            foreach (System.Xml.XmlNode n in e.ChildNodes)
            {
                System.Xml.XmlElement c = n as System.Xml.XmlElement;
                if (c == null || c.Name != "puffer") continue;
                BufferItem b = new BufferItem();
                b.Id = c.GetAttribute("id"); b.Typ = c.GetAttribute("typ"); b.Name = c.GetAttribute("name"); b.Text = c.InnerText.Replace("\r\n", "\n");
                l.Add(b);
            }
            if (l.Count == 0) return FromVariant(e.GetAttribute("variante"));
            Normalize(l);
            return l;
        }

        public static string Key(List<BufferItem> l) { StringBuilder sb = new StringBuilder(); foreach (BufferItem b in l) sb.Append(b.Key()); return sb.ToString(); }

        // "Puffer 1: Name (mobil)" (ohne Namen "–"), mit Text darunter eingerueckt
        public static List<string> DisplayLines(List<BufferItem> l, bool withText)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < l.Count; i++)
            {
                BufferItem b = l[i];
                r.Add("Puffer " + (i + 1) + ": " + (b.Name.Length > 0 ? b.Name : "–") + (b.Typ.Length > 0 ? " (" + b.Typ + ")" : ""));
                if (withText && b.Text.Length > 0) foreach (string t in b.Text.Split('\n')) r.Add("    " + t);
            }
            return r;
        }

        public static bool Matches(List<BufferItem> l, string q)
        {
            foreach (BufferItem b in l)
                if (b.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || b.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static int IndexOf(List<BufferItem> l, string id) { for (int i = 0; i < l.Count; i++) if (l[i].Id == id) return i; return -1; }
        static BufferItem ById(List<BufferItem> l, string id) { foreach (BufferItem b in l) if (b.Id == id) return b; return null; }

        // Rechte: was aendert n gegenueber old? Hinzufuegen = AddText, Entfernen = DeleteText, Aendern (Name/Typ/Text/Reihenfolge) = EditText
        public static void Diff(List<BufferItem> old, List<BufferItem> n, out bool added, out bool removed, out bool changed)
        {
            added = removed = changed = false;
            foreach (BufferItem b in n) { BufferItem o = ById(old, b.Id); if (o == null) added = true; else if (!o.SameValue(b)) changed = true; }
            foreach (BufferItem o in old) if (ById(n, o.Id) == null) removed = true;
            if (!added && !removed && !changed)
                for (int i = 0; i < n.Count && i < old.Count; i++) if (n[i].Id != old[i].Id) changed = true;
        }

        // Dreiwege je Id. Basis b, eigene Fassung mine, gespeicherter Stand theirs.
        // Neu = dazu, einseitig geloescht = weg (hat die andere Seite denselben Puffer geaendert, bleibt er), einseitig geaendert = diese.
        // Beide aendern denselben Puffer unterschiedlich (clash): gespeicherte Fassung bleibt, die eigene kommt als weiterer Puffer dazu
        // (preferMine: nur die eigene). Gleichzeitig angelegte Puffer mit gleicher Id aber anderem Inhalt werden neu nummeriert.
        public static List<BufferItem> Merge(List<BufferItem> b, List<BufferItem> mine, List<BufferItem> theirs, bool preferMine, out bool clash)
        {
            clash = false;
            List<BufferItem> r = new List<BufferItem>();
            List<BufferItem> extra = new List<BufferItem>();   // eigene Fassungen/Neuanlagen, die zusaetzlich bleiben
            foreach (BufferItem t in theirs)
            {
                BufferItem bb = ById(b, t.Id), mm = ById(mine, t.Id);
                if (bb == null)
                {   // neu bei theirs; gleiche Id bei mine = gleichzeitig angelegt
                    r.Add(t.Clone());
                    if (mm != null && !mm.SameValue(t)) extra.Add(mm.Clone());
                    continue;
                }
                if (mm == null)
                {   // von mir geloescht: weg, ausser theirs hat ihn geaendert
                    if (!t.SameValue(bb)) r.Add(t.Clone());
                    continue;
                }
                if (mm.SameValue(bb) || mm.SameValue(t)) { r.Add(t.Clone()); continue; }
                if (t.SameValue(bb)) { r.Add(mm.Clone()); continue; }
                clash = true;   // beide haben denselben Puffer unterschiedlich geaendert
                if (preferMine) r.Add(mm.Clone());
                else { r.Add(t.Clone()); extra.Add(mm.Clone()); }
            }
            foreach (BufferItem m in mine)
            {
                if (ById(theirs, m.Id) != null) continue;
                BufferItem bb = ById(b, m.Id);
                if (bb == null) { extra.Add(m.Clone()); continue; }          // von mir neu angelegt
                if (!m.SameValue(bb)) extra.Add(m.Clone());                   // von theirs geloescht, von mir geaendert: bleibt
            }
            // Eigene Puffer an ihre Stelle aus meiner Liste setzen (nach dem Vorgaenger, sonst vor dem Nachfolger); Ids bei Doppelung neu vergeben
            foreach (BufferItem x in extra)
            {
                string oid = x.Id;
                if (ById(r, x.Id) != null) x.Id = NewId(b, mine, theirs, r, extra);
                int mi = mine.FindIndex(delegate(BufferItem y) { return y.Id == oid; }), at = -1;
                for (int i = mi - 1; i >= 0 && at < 0; i--) { int ri = IndexOf(r, mine[i].Id); if (ri >= 0) at = ri + 1; }
                for (int i = mi + 1; i < mine.Count && at < 0; i++) { int ri = IndexOf(r, mine[i].Id); if (ri >= 0) at = ri; }
                r.Insert(at < 0 ? r.Count : at, x);
            }
            return r;
        }
    }
}
