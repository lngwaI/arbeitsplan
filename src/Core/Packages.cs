using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Kampagnen-Schritt: wann (Tag relativ zum Start, 1 = erster Tag), in welchem Block, was, mit welchem Equipment.
    public class PackageStep
    {
        public int Day = 1;
        public string Row = "";
        public string Text = "";
        public string Equipment = "";       // Komma-getrennt, Auswahl und/oder eigene Eingabe
        public string Fallback = "";        // Ausweich-Equipment
        public string Note = "";

        public bool IsEmpty { get { return Text.Length == 0 && Equipment.Length == 0 && Fallback.Length == 0 && Note.Length == 0; } }

        // Text, wie er im Block erscheint; Equipment-Nummern werden dort automatisch nach Status eingefaerbt.
        public string Compose()
        {
            StringBuilder sb = new StringBuilder();
            if (Text.Length > 0) sb.Append(Text);
            if (Equipment.Length > 0) { if (sb.Length > 0) sb.Append('\n'); sb.Append("Equipment: ").Append(Equipment); }
            if (Fallback.Length > 0) { if (sb.Length > 0) sb.Append('\n'); sb.Append("Ausweich: ").Append(Fallback); }
            if (Note.Length > 0) { if (sb.Length > 0) sb.Append('\n'); sb.Append("Notiz: ").Append(Note); }
            return sb.ToString();
        }
    }

    // Kompletter Block (Export einer Woche): Inhalt mit Formatierung, Kuerzel, LKZ.
    public class PackageBlock
    {
        public int Day = 1;
        public string Row = "";
        public Entry Data = new Entry();
    }

    public class Package
    {
        public string Name = "";
        public List<PackageStep> Steps = new List<PackageStep>();
        public List<PackageBlock> Blocks = new List<PackageBlock>();

        // ---------- Datei (XML) ----------

        public void Save(string path)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = true; st.Encoding = new UTF8Encoding(false);
            using (XmlWriter w = XmlWriter.Create(path, st))
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("name", Name); w.WriteAttributeString("version", "1");
                foreach (PackageStep s in Steps)
                {
                    w.WriteStartElement("Schritt");
                    w.WriteAttributeString("tag", s.Day.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("block", s.Row);
                    w.WriteAttributeString("text", s.Text); w.WriteAttributeString("equipment", s.Equipment);
                    w.WriteAttributeString("ausweich", s.Fallback); w.WriteAttributeString("notiz", s.Note);
                    w.WriteEndElement();
                }
                foreach (PackageBlock b in Blocks)
                {
                    w.WriteStartElement("Block");
                    w.WriteAttributeString("tag", b.Day.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("zeile", b.Row);
                    w.WriteAttributeString("lkz", b.Data.Lkz ? "1" : "0");
                    w.WriteElementString("Inhalt", b.Data.Content);
                    w.WriteStartElement("Kuerzel");
                    w.WriteAttributeString("f", b.Data.People[0]); w.WriteAttributeString("s", b.Data.People[1]); w.WriteAttributeString("n", b.Data.People[2]);
                    w.WriteEndElement();
                    foreach (FormatSpan f in b.Data.Spans)
                    {
                        w.WriteStartElement("Format");
                        w.WriteAttributeString("start", f.Start.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("laenge", f.Length.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("farbe", f.Rgb.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("groesse", f.Size.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("fett", f.Bold ? "1" : "0"); w.WriteAttributeString("kursiv", f.Italic ? "1" : "0");
                        w.WriteAttributeString("schrift", f.Font ?? "");
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }

        public static Package Load(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Paket") throw new InvalidDataException("Keine gueltige Paketdatei.");
            Package p = new Package();
            p.Name = root.GetAttribute("name");
            foreach (XmlElement e in root.ChildNodes.OfType())
            {
                if (e.Name == "Schritt")
                {
                    PackageStep s = new PackageStep();
                    s.Day = Math.Max(1, ToInt(e.GetAttribute("tag"), 1)); s.Row = e.GetAttribute("block"); s.Text = e.GetAttribute("text");
                    s.Equipment = e.GetAttribute("equipment"); s.Fallback = e.GetAttribute("ausweich"); s.Note = e.GetAttribute("notiz");
                    p.Steps.Add(s);
                }
                else if (e.Name == "Block")
                {
                    PackageBlock b = new PackageBlock();
                    b.Day = Math.Max(1, ToInt(e.GetAttribute("tag"), 1)); b.Row = e.GetAttribute("zeile"); b.Data.Lkz = e.GetAttribute("lkz") == "1";
                    foreach (XmlElement c in e.ChildNodes.OfType())
                    {
                        if (c.Name == "Inhalt") b.Data.Content = c.InnerText.Replace("\r\n", "\n");
                        else if (c.Name == "Kuerzel") { b.Data.People[0] = c.GetAttribute("f"); b.Data.People[1] = c.GetAttribute("s"); b.Data.People[2] = c.GetAttribute("n"); }
                        else if (c.Name == "Format")
                        {
                            FormatSpan f = new FormatSpan();
                            f.Start = ToInt(c.GetAttribute("start"), 0); f.Length = ToInt(c.GetAttribute("laenge"), 0); f.Rgb = ToInt(c.GetAttribute("farbe"), 0);
                            float.TryParse(c.GetAttribute("groesse"), NumberStyles.Float, CultureInfo.InvariantCulture, out f.Size);
                            f.Bold = c.GetAttribute("fett") == "1"; f.Italic = c.GetAttribute("kursiv") == "1";
                            f.Font = c.GetAttribute("schrift").Length > 0 ? c.GetAttribute("schrift") : null;
                            b.Data.Spans.Add(f);
                        }
                    }
                    p.Blocks.Add(b);
                }
            }
            return p;
        }

        static int ToInt(string s, int def) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }

        // ---------- Aus einer Woche erstellen / in eine Woche uebernehmen ----------

        public static Package FromWeek(IDataStore store, DateTime start, string name)
        {
            Package p = new Package(); p.Name = name;
            foreach (RowInfo r in store.GetRows())
                for (int d = 0; d < 7; d++)
                {
                    Entry e = store.Get(start.AddDays(d), r.Id);
                    if (e == null || (e.IsEmpty && !e.Lkz)) continue;
                    PackageBlock b = new PackageBlock(); b.Day = d + 1; b.Row = r.Name; b.Data = e; b.Data.Version = 0;
                    p.Blocks.Add(b);
                }
            return p;
        }

        // Ergebnis der Uebernahme (Meldung fuer den Nutzer).
        public string Apply(IDataStore store, DateTime start, ApplyMode mode)
        {
            List<RowInfo> rows = store.GetRows();
            int written = 0, skipped = 0, unknownRow = 0, conflicts = 0;
            // Ziel (Tag|Zeile) -> neuer Block; Schritte desselben Ziels werden zusammengefasst
            Dictionary<string, Entry> targets = new Dictionary<string, Entry>();
            Dictionary<string, KeyValuePair<DateTime, int>> keys = new Dictionary<string, KeyValuePair<DateTime, int>>();
            List<string> order = new List<string>();

            foreach (PackageBlock b in Blocks)
            {
                int id = RowId(rows, b.Row);
                if (id < 0) { unknownRow++; continue; }
                DateTime day = start.AddDays(b.Day - 1);
                string k = day.ToString("yyyyMMdd") + "|" + id;
                targets[k] = b.Data.Clone(); keys[k] = new KeyValuePair<DateTime, int>(day, id); if (!order.Contains(k)) order.Add(k);
            }
            foreach (PackageStep s in Steps)
            {
                if (s.IsEmpty) continue;
                int id = RowId(rows, s.Row);
                if (id < 0) { unknownRow++; continue; }
                DateTime day = start.AddDays(s.Day - 1);
                string k = day.ToString("yyyyMMdd") + "|" + id;
                Entry t;
                if (!targets.TryGetValue(k, out t)) { t = new Entry(); targets[k] = t; keys[k] = new KeyValuePair<DateTime, int>(day, id); order.Add(k); }
                string add = s.Compose();
                t.Content = t.Content.Length > 0 ? t.Content + "\n\n" + add : add;
            }

            foreach (string k in order)
            {
                Entry n = targets[k];
                DateTime day = keys[k].Key; int id = keys[k].Value;
                Entry cur = store.Get(day, id);
                Entry result;
                if (cur == null || cur.IsEmpty) result = n.Clone();
                else if (mode == ApplyMode.FillEmpty) { skipped++; continue; }
                else if (mode == ApplyMode.Overwrite) result = n.Clone();
                else
                {   // anhaengen: Text nach dem vorhandenen, Formatierung der neuen Textteile verschieben
                    result = cur.Clone();
                    if (n.Content.Length > 0)
                    {
                        int shift = result.Content.Length + 2;
                        result.Content = result.Content.Length > 0 ? result.Content + "\n\n" + n.Content : n.Content;
                        if (result.Content == n.Content) shift = 0;
                        foreach (FormatSpan f in n.Spans) { FormatSpan c = f.Clone(); c.Start += shift; result.Spans.Add(c); }
                    }
                    for (int i = 0; i < 3; i++) if (n.People[i].Length > 0) result.People[i] = result.People[i].Length > 0 ? result.People[i] + ", " + n.People[i] : n.People[i];
                    result.Lkz = result.Lkz || n.Lkz;
                }
                result.Version = cur != null ? cur.Version : 0;
                if (store.Save(day, id, result)) written++;
                else
                {
                    Entry again = store.Get(day, id);
                    result.Version = again != null ? again.Version : 0;
                    if (store.Save(day, id, result)) written++; else conflicts++;
                }
            }
            string msg = written + " Block/Blöcke geschrieben";
            if (skipped > 0) msg += ", " + skipped + " übersprungen (bereits belegt)";
            if (unknownRow > 0) msg += ", " + unknownRow + " mit unbekanntem Blocknamen ignoriert";
            if (conflicts > 0) msg += ", " + conflicts + " wegen Konflikt nicht gespeichert";
            return msg + ".";
        }

        static int RowId(List<RowInfo> rows, string name)
        {
            foreach (RowInfo r in rows) if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) return r.Id;
            return -1;
        }
    }

    public enum ApplyMode { FillEmpty, Overwrite, Append }

    static class XmlExt
    {
        public static IEnumerable<XmlElement> OfType(this XmlNodeList list)
        {
            foreach (XmlNode n in list) { XmlElement e = n as XmlElement; if (e != null) yield return e; }
        }
    }
}
