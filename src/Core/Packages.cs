using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Equipment einer Position in einem Schritt. Bei "abwechselnd" wird die ID-Liste reihum verteilt
    // (Lauf 1 -> A, Lauf 2 -> B, Lauf 3 -> wieder A ...). "Fallback" ist das Ausweich-Equipment fuer Unvorhergesehenes.
    public class EquipAssign
    {
        public string Short = "";                                   // Position, fuer die die Zeile angelegt wurde
        public List<EquipChoice> Choices = new List<EquipChoice>();  // gewaehlte Geraete (bei "abwechselnd" auch von Positionen derselben Austausch-Gruppe)
        public bool Alternate;
        public string Fallback = "";
        public string Note = "";

        public EquipAssign Clone()
        {
            EquipAssign a = (EquipAssign)MemberwiseClone();
            a.Choices = new List<EquipChoice>();
            foreach (EquipChoice c in Choices) a.Choices.Add(c.Clone());
            return a;
        }

        public EquipChoice ChoiceFor(int run)
        {
            if (Choices.Count == 0) return null;
            return Alternate ? Choices[run % Choices.Count] : Choices[0];
        }

        // Eigene Zeile: "<Kurzname>: <ID> (Ausweich: <ID>) <Notiz>"; ohne ID steht die Notiz (z. B. Schlauch-Bezeichnung) hinter dem Namen.
        // Bei Geraeten einer anderen Position der Gruppe (Freezetank 2 statt 1) steht deren Kurzname.
        public string Line(int run)
        {
            EquipChoice c = ChoiceFor(run);
            string id = c != null ? c.Id : "";
            if (id.Length == 0 && Note.Length == 0 && Fallback.Length == 0) return "";
            StringBuilder sb = new StringBuilder(c != null && c.Short.Length > 0 ? c.Short : Short).Append(':');
            if (id.Length > 0) sb.Append(' ').Append(id);
            if (Fallback.Length > 0) sb.Append(" (Ausweich: ").Append(Fallback).Append(')');
            if (Note.Length > 0) sb.Append(' ').Append(Note);
            return sb.ToString();
        }
    }

    // Ablaufschritt einer Kampagne: wann (Tag relativ zum Start), in welchem Block, was, mit welchem Equipment; optional wiederholt.
    public class PackageStep
    {
        public string Title = "";           // z. B. "Lauf", "Packen"
        public string Source = "";          // Name des Bausteins, aus dem der Schritt stammt
        public string Row = "";
        public int Day = 1;                 // 1 = erster Tag
        public int Count = 1;               // Wiederholungen (z. B. 6 Laeufe)
        public int Interval = 1;            // Abstand in Tagen
        public int StartNo = 1;             // Nummer des ersten Laufs ("Lauf 1")
        public string Text = "";
        public string Note = "";
        public List<EquipAssign> Equipment = new List<EquipAssign>();

        public PackageStep Clone()
        {
            PackageStep s = (PackageStep)MemberwiseClone();
            s.Equipment = new List<EquipAssign>();
            foreach (EquipAssign a in Equipment) s.Equipment.Add(a.Clone());
            return s;
        }

        public bool IsEmpty
        {
            get
            {
                if (Title.Length > 0 || Text.Length > 0 || Note.Length > 0) return false;
                foreach (EquipAssign a in Equipment) if (a.Line(0).Length > 0) return false;
                return true;
            }
        }

        public string Compose(int run)
        {
            List<string> lines = new List<string>();
            if (Title.Length > 0) lines.Add(Count > 1 ? Title + " " + (StartNo + run) : Title);
            if (Text.Length > 0) lines.Add(Text);
            foreach (EquipAssign a in Equipment) { string l = a.Line(run); if (l.Length > 0) lines.Add(l); }
            if (Note.Length > 0) lines.Add("Notiz: " + Note);
            return string.Join("\n", lines.ToArray());
        }

        // Alle Laeufe: Tag (1-basiert) und Text.
        public List<KeyValuePair<int, string>> Runs()
        {
            List<KeyValuePair<int, string>> res = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < Math.Max(1, Count); i++) res.Add(new KeyValuePair<int, string>(Day + i * Math.Max(1, Interval), Compose(i)));
            return res;
        }
    }

    // Kompletter Block (Export einer Woche): Inhalt mit Formatierung, Kuerzel, LKZ.
    public class PackageBlock
    {
        public int Day = 1;
        public string Row = "";
        public Entry Data = new Entry();
    }

    public enum ApplyMode { FillEmpty, Overwrite, Append }

    public class Package
    {
        public string Name = "";
        public List<PackageStep> Steps = new List<PackageStep>();
        public List<PackageBlock> Blocks = new List<PackageBlock>();

        // ---------- Datei (XML, UTF-8: Umlaute und Sonderzeichen bleiben erhalten) ----------

        public void Save(string path)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = true; st.Encoding = new UTF8Encoding(false);
            using (XmlWriter w = XmlWriter.Create(path, st))
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("name", Name); w.WriteAttributeString("version", "2");
                foreach (PackageStep s in Steps)
                {
                    w.WriteStartElement("Schritt");
                    w.WriteAttributeString("titel", s.Title); w.WriteAttributeString("quelle", s.Source); w.WriteAttributeString("block", s.Row);
                    w.WriteAttributeString("tag", I(s.Day)); w.WriteAttributeString("anzahl", I(s.Count)); w.WriteAttributeString("abstand", I(s.Interval)); w.WriteAttributeString("startnr", I(s.StartNo));
                    w.WriteAttributeString("text", s.Text); w.WriteAttributeString("notiz", s.Note);
                    foreach (EquipAssign a in s.Equipment)
                    {
                        w.WriteStartElement("Equipment");
                        w.WriteAttributeString("kurz", a.Short);
                        w.WriteAttributeString("abwechselnd", a.Alternate ? "1" : "0"); w.WriteAttributeString("ausweich", a.Fallback); w.WriteAttributeString("notiz", a.Note);
                        foreach (EquipChoice ch in a.Choices)   // je Geraet ein Element: beliebige Zeichen in Kurzname/ID moeglich
                        {
                            w.WriteStartElement("Geraet"); w.WriteAttributeString("kurz", ch.Short); w.WriteAttributeString("id", ch.Id); w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                foreach (PackageBlock b in Blocks)
                {
                    w.WriteStartElement("Block");
                    w.WriteAttributeString("tag", I(b.Day)); w.WriteAttributeString("zeile", b.Row); w.WriteAttributeString("lkz", b.Data.Lkz ? "1" : "0");
                    w.WriteElementString("Inhalt", b.Data.Content);
                    w.WriteStartElement("Kuerzel");
                    w.WriteAttributeString("f", b.Data.People[0]); w.WriteAttributeString("s", b.Data.People[1]); w.WriteAttributeString("n", b.Data.People[2]);
                    w.WriteEndElement();
                    foreach (FormatSpan f in b.Data.Spans)
                    {
                        w.WriteStartElement("Format");
                        w.WriteAttributeString("start", I(f.Start)); w.WriteAttributeString("laenge", I(f.Length));
                        w.WriteAttributeString("farbe", I(f.Rgb)); w.WriteAttributeString("groesse", f.Size.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("fett", f.Bold ? "1" : "0"); w.WriteAttributeString("kursiv", f.Italic ? "1" : "0");
                        w.WriteAttributeString("schrift", f.Font ?? "");
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }

        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        static int ToInt(string s, int def) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }

        static IEnumerable<XmlElement> Elements(XmlNode n)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null) yield return e; }
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static Package Load(string path)
        {
            // Gehaertet: keine DTD/externen Entitaeten, keine Aufloesung externer Quellen, Groessenlimit (10 MB).
            if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("Paketdatei ist zu groß.");
            XmlReaderSettings rs = new XmlReaderSettings();
            rs.DtdProcessing = DtdProcessing.Prohibit;
            rs.XmlResolver = null;
            rs.MaxCharactersInDocument = 20 * 1000 * 1000;
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (XmlReader xr = XmlReader.Create(path, rs)) doc.Load(xr);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Paket") throw new InvalidDataException("Keine gültige Paketdatei.");
            Package p = new Package();
            p.Name = root.GetAttribute("name");
            foreach (XmlElement e in Elements(root))
            {
                if (e.Name == "Schritt")
                {
                    PackageStep s = new PackageStep();
                    s.Title = e.GetAttribute("titel"); s.Source = e.GetAttribute("quelle"); s.Row = e.GetAttribute("block");
                    s.Day = Clamp(ToInt(e.GetAttribute("tag"), 1), 1, 366); s.Count = Clamp(ToInt(e.GetAttribute("anzahl"), 1), 1, 99);
                    s.Interval = Clamp(ToInt(e.GetAttribute("abstand"), 1), 1, 30); s.StartNo = Clamp(ToInt(e.GetAttribute("startnr"), 1), 0, 999);
                    s.Text = e.GetAttribute("text"); s.Note = e.GetAttribute("notiz");
                    foreach (XmlElement q in Elements(e))
                        if (q.Name == "Equipment")
                        {
                            EquipAssign a = new EquipAssign();
                            a.Short = q.GetAttribute("kurz");
                            foreach (XmlElement g in Elements(q))
                                if (g.Name == "Geraet") a.Choices.Add(new EquipChoice(g.GetAttribute("kurz"), g.GetAttribute("id")));
                            if (a.Choices.Count == 0)   // aeltere Pakete: "wahl" (Kurz|Id;...) bzw. "ids" (Id|Id|...)
                            {
                                foreach (string w in q.GetAttribute("wahl").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = w.Split('|');
                                    a.Choices.Add(kv.Length > 1 ? new EquipChoice(kv[0], kv[1]) : new EquipChoice(a.Short, w));
                                }
                                if (a.Choices.Count == 0)
                                    foreach (string id in q.GetAttribute("ids").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) a.Choices.Add(new EquipChoice(a.Short, id));
                            }
                            a.Alternate = q.GetAttribute("abwechselnd") == "1"; a.Fallback = q.GetAttribute("ausweich"); a.Note = q.GetAttribute("notiz");
                            s.Equipment.Add(a);
                        }
                    p.Steps.Add(s);
                }
                else if (e.Name == "Block")
                {
                    PackageBlock b = new PackageBlock();
                    b.Day = Clamp(ToInt(e.GetAttribute("tag"), 1), 1, 366); b.Row = e.GetAttribute("zeile"); b.Data.Lkz = e.GetAttribute("lkz") == "1";
                    foreach (XmlElement c in Elements(e))
                    {
                        if (c.Name == "Inhalt") b.Data.Content = c.InnerText.Replace("\r\n", "\n");
                        else if (c.Name == "Kuerzel") { b.Data.People[0] = c.GetAttribute("f"); b.Data.People[1] = c.GetAttribute("s"); b.Data.People[2] = c.GetAttribute("n"); }
                        else if (c.Name == "Format")
                        {
                            FormatSpan f = new FormatSpan();
                            f.Start = Clamp(ToInt(c.GetAttribute("start"), 0), 0, 1000000); f.Length = Clamp(ToInt(c.GetAttribute("laenge"), 0), 0, 1000000); f.Rgb = ToInt(c.GetAttribute("farbe"), 0) & 0xFFFFFF;
                            float.TryParse(c.GetAttribute("groesse"), NumberStyles.Float, CultureInfo.InvariantCulture, out f.Size);
                            if (!(f.Size >= 6f && f.Size <= 72f)) f.Size = 0;   // NaN/unsinnig: Standardgroesse
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

        // Vorschau/Uebernahme: je Ziel (Tag, Zeile) der neue Block. Schritte desselben Ziels werden zusammengefasst.
        class Target { public DateTime Day; public int RowId; public Entry Data; }

        List<Target> BuildTargets(List<RowInfo> rows, DateTime start, out int unknownRow)
        {
            unknownRow = 0;
            Dictionary<string, Target> map = new Dictionary<string, Target>();
            List<Target> order = new List<Target>();
            foreach (PackageBlock b in Blocks)
            {
                int id = RowId(rows, b.Row);
                if (id < 0) { unknownRow++; continue; }
                DateTime day = start.AddDays(b.Day - 1);
                string k = day.ToString("yyyyMMdd") + "|" + id;
                Target t = new Target(); t.Day = day; t.RowId = id; t.Data = b.Data.Clone();
                if (!map.ContainsKey(k)) order.Add(t); else order[order.FindIndex(delegate(Target x) { return x == map[k]; })] = t;
                map[k] = t;
            }
            foreach (PackageStep s in Steps)
            {
                if (s.IsEmpty) continue;
                int id = RowId(rows, s.Row);
                if (id < 0) { unknownRow++; continue; }
                foreach (KeyValuePair<int, string> run in s.Runs())
                {
                    DateTime day = start.AddDays(run.Key - 1);
                    string k = day.ToString("yyyyMMdd") + "|" + id;
                    Target t;
                    if (!map.TryGetValue(k, out t)) { t = new Target(); t.Day = day; t.RowId = id; t.Data = new Entry(); map[k] = t; order.Add(t); }
                    t.Data.Content = t.Data.Content.Length > 0 ? t.Data.Content + "\n\n" + run.Value : run.Value;
                }
            }
            return order;
        }

        // Ergebnis der Uebernahme (Meldung fuer den Nutzer).
        public string Apply(IDataStore store, DateTime start, ApplyMode mode)
        {
            List<RowInfo> rows = store.GetRows();
            int written = 0, skipped = 0, unknownRow, conflicts = 0;
            foreach (Target t in BuildTargets(rows, start, out unknownRow))
            {
                Entry n = t.Data;
                Entry cur = store.Get(t.Day, t.RowId);
                Entry result;
                if (cur == null || cur.IsEmpty) result = n.Clone();
                else if (mode == ApplyMode.FillEmpty) { skipped++; continue; }
                else if (mode == ApplyMode.Overwrite) result = n.Clone();
                else
                {   // anhaengen: Text nach dem vorhandenen, Formatierung der neuen Textteile verschieben
                    result = cur.Clone();
                    if (n.Content.Length > 0)
                    {
                        int shift = result.Content.Length > 0 ? result.Content.Length + 2 : 0;
                        result.Content = result.Content.Length > 0 ? result.Content + "\n\n" + n.Content : n.Content;
                        foreach (FormatSpan f in n.Spans) { FormatSpan c = f.Clone(); c.Start += shift; result.Spans.Add(c); }
                    }
                    for (int i = 0; i < 3; i++) if (n.People[i].Length > 0) result.People[i] = result.People[i].Length > 0 ? result.People[i] + ", " + n.People[i] : n.People[i];
                    result.Lkz = result.Lkz || n.Lkz;
                }
                result.Version = cur != null ? cur.Version : 0;
                if (store.Save(t.Day, t.RowId, result)) written++;
                else
                {
                    Entry again = store.Get(t.Day, t.RowId);
                    result.Version = again != null ? again.Version : 0;
                    if (store.Save(t.Day, t.RowId, result)) written++; else conflicts++;
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
}
