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
        public int LaufRef;   // Nummer des Laufs, den dieser Puffer bedient (UX-5b, setzt Package.Apply; 0 = unbekannt, alte Daten)
        public string LaufNo = "";   // volle Laufnummer des bedienten Laufs (8 Ziffern, XML "laufnr", setzt Package.Apply; "" = unbekannt). Nie in Name/Text: RunNumbers.Find zaehlte sie als Lauf
        public bool Made;     // hergestellt (N2, nur mit DutyConfig.BufMade sichtbar/wirksam; XML fertig="1", alte Daten = false)

        public BufferItem Clone() { return (BufferItem)MemberwiseClone(); }
        public bool SameCore(BufferItem o) { return o != null && Name == o.Name && Typ == o.Typ && Text == o.Text && LaufRef == o.LaufRef && LaufNo == o.LaufNo; }   // ohne Made
        public bool SameValue(BufferItem o) { return SameCore(o) && Made == o.Made; }
        public string Key() { return Id + "|" + Typ + "|" + Name + "|" + Text + (LaufRef > 0 ? "|L" + LaufRef : "") + (LaufNo.Length > 0 ? "|N" + LaufNo : "") + (Made ? "|M" : "") + ";"; }

        // Kopfzeile wie im Plan: "Name · für 12340003" (ohne Nummer "· Lauf 3", ohne Namen "–", ohne Lauf-Angabe nur "Puffer 2: Name")
        public string HeadText() { return (Name.Length > 0 ? Name : "–") + LaufText; }
        public string LaufText { get { return LaufNo.Length > 0 ? " · für " + LaufNo : (LaufRef > 0 ? " · Lauf " + LaufRef : ""); } }
    }

    // Hilfsfunktionen fuer Entry.Buffers: Typen (VariantSet), Migration alter Daten, Rechte, Anzeige, Dreiwege-Merge.
    public static class BufferSet
    {
        // Marker in BufferItem.Text (UX-5f): steht anstelle des Leerzeichens vor der Notiz einer Equipment-Zeile ("TFF: H1-T-0123<Marker>Notiz").
        // Plain ergibt den bisherigen Text; Alt-Daten ohne Marker bleiben unveraendert (Private-Use-Zeichen, in XML erlaubt).
        public const char NoteMark = '';

        public static string Plain(string text) { return text != null && text.IndexOf(NoteMark) >= 0 ? text.Replace(NoteMark, ' ') : text; }

        // Anzeige: die Notiz steht kursiv, eingerueckt in einer eigenen Zeile unter dem Geraet; italic[i] gehoert zu Zeichen i des Ergebnisses
        public static string ForDisplay(string text, out bool[] italic)
        {
            if (text.IndexOf(NoteMark) < 0) { italic = new bool[text.Length]; return text; }
            StringBuilder sb = new StringBuilder(); List<bool> it = new List<bool>(); bool inNote = false;
            foreach (char ch in text)
            {
                if (ch == NoteMark) { sb.Append("\n  "); it.Add(false); it.Add(false); it.Add(false); inNote = true; continue; }
                if (ch == '\n') inNote = false;
                sb.Append(ch); it.Add(inNote);
            }
            italic = it.ToArray();
            return sb.ToString();
        }

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
                if (b.LaufRef > 0) w.WriteAttributeString("lauf", b.LaufRef.ToString(CultureInfo.InvariantCulture));
                if (b.LaufNo.Length > 0) w.WriteAttributeString("laufnr", b.LaufNo);
                if (b.Made) w.WriteAttributeString("fertig", "1");
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
                int lr; if (int.TryParse(c.GetAttribute("lauf"), NumberStyles.Integer, CultureInfo.InvariantCulture, out lr) && lr > 0 && lr < 100000) b.LaufRef = lr;
                if (PackageStep.IsManualNo(c.GetAttribute("laufnr"))) b.LaufNo = c.GetAttribute("laufnr");
                b.Made = c.GetAttribute("fertig") == "1";
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
                r.Add("Puffer " + (i + 1) + ": " + (b.Name.Length > 0 ? b.Name : "–") + (b.Typ.Length > 0 ? " (" + b.Typ + ")" : "") + (b.Made ? " – hergestellt" : ""));
                if (withText && b.Text.Length > 0) foreach (string t in Plain(b.Text).Split('\n')) r.Add("    " + t);
            }
            return r;
        }

        public static bool Matches(List<BufferItem> l, string q)
        {
            foreach (BufferItem b in l)
                if (b.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || Plain(b.Text).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
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

        static BufferItem WithMade(BufferItem core, BufferItem bb, BufferItem mm, BufferItem t)
        {
            BufferItem x = core.Clone(); x.Made = mm.Made != bb.Made ? mm.Made : t.Made; return x;
        }

        // Nur "hergestellt" geaendert (Rest des Blocks gleich)? "Puffer 2 hergestellt" / "Puffer 2: hergestellt zurückgenommen", sonst "".
        public static string MadeChange(Entry b, Entry n)
        {
            if (b.Content != n.Content || b.Lkz != n.Lkz || b.Buffers.Count != n.Buffers.Count) return "";
            string r = "";
            for (int i = 0; i < n.Buffers.Count; i++)
            {
                BufferItem o = ById(b.Buffers, n.Buffers[i].Id);
                if (o == null || !o.SameCore(n.Buffers[i])) return "";
                if (o.Made != n.Buffers[i].Made) r += (r.Length > 0 ? ", " : "") + "Puffer " + (i + 1) + (n.Buffers[i].Made ? " hergestellt" : ": hergestellt zurückgenommen");
            }
            return r;
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
                // "hergestellt" wird getrennt gemerged (einseitig geaendert gewinnt, beide gleich = gleich); der uebrige Inhalt wie bisher
                if (mm.SameCore(bb) || mm.SameCore(t)) { r.Add(WithMade(t, bb, mm, t)); continue; }
                if (t.SameCore(bb)) { r.Add(WithMade(mm, bb, mm, t)); continue; }
                clash = true;   // beide haben denselben Puffer unterschiedlich geaendert
                if (preferMine) r.Add(WithMade(mm, bb, mm, t));
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
