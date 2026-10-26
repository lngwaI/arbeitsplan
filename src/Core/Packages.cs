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

    // Normal: Schritt (bei Wiederholung "Titel 1, 2, 3"); Run: Hauptlauf mit voller Laufnummer; SubRun: Unterlaeufe (Splitten/Einfrieren) eines Hauptlaufs.
    public enum StepKind { Normal, Run, SubRun }

    // Ablaufschritt einer Kampagne: wann (Tag relativ zum Start), in welchem Block, was, mit welchem Equipment; optional wiederholt.
    public class PackageStep
    {
        public string Title = "";           // z. B. "Lauf", "Packen"
        public string Source = "";          // Name des Bausteins, aus dem der Schritt stammt
        public string Row = "";
        public int Day = 1;                 // 1 = erster Tag
        public int Count = 1;               // Wiederholungen (z. B. 6 Laeufe)
        public int Interval = 1;            // Abstand in Tagen
        public int StartNo = 1;             // Nummer des ersten Laufs ("Lauf 1"); mit Produktcode die 4 hinteren Stellen (ZZZZ)
        public StepKind Kind;
        public string SubNos = "";          // nur SubRun: Unterlauf-Nummern je Lauf, Komma-getrennt; mehrere je Lauf mit "/"
        public string Text = "";
        public string Note = "";
        public string Ident = "";           // Kennung je Lauf, Komma-getrennt (z. B. "PA 40010001, PA 40010017"); ganz unten rot, gross, fett
        public List<EquipAssign> Equipment = new List<EquipAssign>();

        public string IdentFor(int run)
        {
            string[] per = Ident.Split(',');
            return run < per.Length ? per[run].Trim() : "";
        }

        // Format der Kennungszeile (rot, gross, fett).
        public static FormatSpan IdentSpan(int start, int length)
        {
            FormatSpan f = new FormatSpan(); f.Start = start; f.Length = length; f.Rgb = 0xFF0000; f.Size = 14f; f.Bold = true;
            return f;
        }

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
                if (Title.Length > 0 || Text.Length > 0 || Note.Length > 0 || Ident.Trim().Length > 0 || Kind == StepKind.SubRun) return false;
                foreach (EquipAssign a in Equipment) if (a.Line(0).Length > 0) return false;
                return true;
            }
        }

        public bool Numbered { get { return Kind != StepKind.Normal || Count > 1; } }

        // Laufnummer: mit Produktcode 8-stellig XXYYZZZZ (z. B. 13570030), sonst wie bisher "3".
        public string RunNo(string product, int run)
        {
            int no = StartNo + run;
            return Kind != StepKind.Normal && Package.IsCode4(product) ? product + no.ToString("0000", CultureInfo.InvariantCulture) : no.ToString(CultureInfo.InvariantCulture);
        }

        // Kurzbezeichnung fuer Tabellen (z. B. "Lauf 13570030").
        public string Label(string product, int run)
        {
            string t = Title.Length > 0 ? Title : (Kind == StepKind.SubRun ? "Unterlauf" : "Schritt");
            return Numbered ? t + " " + RunNo(product, run) : t;
        }

        public string Compose(int run, string product)
        {
            List<string> lines = new List<string>();
            if (Kind == StepKind.SubRun)
            {
                string main = RunNo(product, run);
                if (Title.Length > 0) lines.Add(Title);
                string[] per = SubNos.Split(',');
                bool any = false;
                foreach (string part in (run < per.Length ? per[run] : "").Split('/'))
                {
                    string n = part.Trim();
                    if (n.Length == 0) continue;
                    if (Package.IsCode4(n) && Package.IsCode4(product)) n = product + n;   // "4186" -> "13574186"
                    lines.Add("Unterlauf " + n + " (aus " + main + ")"); any = true;
                }
                if (!any) lines.Add("Unterlauf (aus " + main + ")");
            }
            else if (Title.Length > 0) lines.Add(Numbered ? Title + " " + RunNo(product, run) : Title);
            if (Text.Length > 0) lines.Add(Text);
            foreach (EquipAssign a in Equipment) { string l = a.Line(run); if (l.Length > 0) lines.Add(l); }
            if (Note.Length > 0) lines.Add("Notiz: " + Note);
            if (IdentFor(run).Length > 0) lines.Add(IdentFor(run));   // immer die letzte Zeile (Format in BuildTargets)
            return string.Join("\n", lines.ToArray());
        }

        // Alle Laeufe: Tag (1-basiert, Paket-Rhythmus) und Text.
        public List<KeyValuePair<int, string>> Runs(string product)
        {
            List<KeyValuePair<int, string>> res = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < Math.Max(1, Count); i++) res.Add(new KeyValuePair<int, string>(Day + i * Math.Max(1, Interval), Compose(i, product)));
            return res;
        }
    }

    // Ein Lauf eines Schritts mit Tag (1 = Starttag); im Kampagnenstart-Dialog einzeln verschiebbar.
    public class RunDate
    {
        public int Step, Run, Day;          // Step = Index in Package.Steps, Run = Wiederholung (0-basiert)
        public int Group = -1;              // zugehoeriger Hauptlauf (Wiederholung des Lauf-Schritts), -1 = davor (z. B. Packen)
        public string Label = "", Row = ""; // Row: Block (Raum), im Kampagnenstart je Lauf aenderbar
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
        public string ProductCode = "";     // XXYY der Laufnummer (Paket = Produkt); leer = Laeufe ohne Produktnummer
        public List<PackageStep> Steps = new List<PackageStep>();
        public List<PackageBlock> Blocks = new List<PackageBlock>();

        public static bool IsCode4(string s)
        {
            if (s == null || s.Length != 4) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        // ---------- Kampagnenstart: Laufnummern und Termine ----------

        // Kleinste Startnummer der Haupt-/Unterlauf-Schritte, -1 = keine.
        public int FirstRunNo
        {
            get
            {
                int m = -1;
                foreach (PackageStep s in Steps) if (s.Kind != StepKind.Normal && (m < 0 || s.StartNo < m)) m = s.StartNo;
                return m;
            }
        }

        // Laufnummern so verschieben, dass der erste Lauf "first" hat (Abstaende zwischen den Schritten bleiben).
        public void Renumber(int first)
        {
            int d = first - FirstRunNo;
            if (FirstRunNo < 0 || d == 0) return;
            foreach (PackageStep s in Steps) if (s.Kind != StepKind.Normal) s.StartNo = Clamp(s.StartNo + d, 0, 9999);
        }

        // Alle Laeufe aller Schritte mit Tag laut Paket-Rhythmus (Tag, Abstand).
        public List<RunDate> Schedule() { return Schedule(null); }

        // Wie Schedule(); gaps = Abstaende zwischen den Hauptlaeufen (z. B. aus einer frueheren Kampagne), fehlende = Paket-Abstand.
        // Alles, was zu einem Hauptlauf gehoert (Schritte ab seinem Tag bis zum naechsten), wandert mit ihm.
        public List<RunDate> Schedule(List<int> gaps)
        {
            int main = -1;   // Lauf-Schritt: erster Hauptlauf-Schritt mit den meisten Wiederholungen
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Kind == StepKind.Run && !Steps[i].IsEmpty && (main < 0 || Steps[i].Count > Steps[main].Count)) main = i;
            List<int> mainDay = new List<int>(), shift = new List<int>();
            if (main >= 0)
            {
                PackageStep m = Steps[main]; int iv = Math.Max(1, m.Interval);
                for (int r = 0; r < Math.Max(1, m.Count); r++)
                {
                    mainDay.Add(m.Day + r * iv);
                    shift.Add(r == 0 ? 0 : shift[r - 1] + (gaps != null && r - 1 < gaps.Count ? gaps[r - 1] : iv) - iv);
                }
            }
            List<RunDate> res = new List<RunDate>();
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty) continue;
                for (int r = 0; r < Math.Max(1, s.Count); r++)
                {
                    RunDate d = new RunDate(); d.Step = i; d.Run = r; d.Day = s.Day + r * Math.Max(1, s.Interval);
                    d.Label = s.Label(ProductCode, r); d.Row = s.Row;
                    for (int k = 0; k < mainDay.Count; k++) if (mainDay[k] <= d.Day) d.Group = k;
                    if (i == main) d.Group = r;
                    if (d.Group >= 0) d.Day += shift[d.Group];
                    res.Add(d);
                }
            }
            return res;
        }

        // Hauptlauf-Nummern (ZZZZ) desselben Produkts im Plan mit erstem Datum, nach Nummer sortiert. Unterlauf-Zeilen zaehlen nicht.
        public static List<KeyValuePair<int, DateTime>> PastRuns(IDataStore store, string product)
        {
            List<KeyValuePair<int, DateTime>> res = new List<KeyValuePair<int, DateTime>>();
            if (!IsCode4(product)) return res;
            Dictionary<int, DateTime> seen = new Dictionary<int, DateTime>();
            System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex("(?<![0-9])" + product + "([0-9]{4})(?![0-9])");
            foreach (SearchHit h in store.Find(product))
            {
                Entry e = store.Get(h.Day, h.RowId);
                if (e == null) continue;
                foreach (string line in e.Content.Split('\n'))
                {
                    if (line.TrimStart().StartsWith("Unterlauf", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(line))
                    {
                        int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        DateTime t;
                        if (!seen.TryGetValue(n, out t) || h.Day < t) seen[n] = h.Day;
                    }
                }
            }
            res.AddRange(seen);
            res.Sort(delegate(KeyValuePair<int, DateTime> a, KeyValuePair<int, DateTime> b) { return a.Key.CompareTo(b.Key); });
            return res;
        }

        // Abstaende (Tage) zwischen aufeinanderfolgenden Laeufen der letzten Kampagne (fortlaufende Nummern bis zur hoechsten,
        // hoechstens 30 Tage auseinander), aeltester zuerst. Leer = keine frueheren Laeufe.
        public static List<int> PastGaps(IDataStore store, string product)
        {
            List<KeyValuePair<int, DateTime>> runs = PastRuns(store, product);
            List<int> gaps = new List<int>();
            for (int i = runs.Count - 1; i > 0; i--)
            {
                int g = (runs[i].Value - runs[i - 1].Value).Days;
                if (runs[i - 1].Key != runs[i].Key - 1 || g < 1 || g > 30) break;
                gaps.Insert(0, g);
            }
            return gaps;
        }

        // Fruehere Kampagnen desselben Produkts im Plan: hoechste Hauptlauf-Nummer (ZZZZ) mit Datum und haeufigster Abstand
        // zwischen aufeinanderfolgenden Nummern (0 = unbekannt). false = nichts gefunden.
        public static bool LastRun(IDataStore store, string product, out int no, out DateTime day, out int interval)
        {
            no = -1; day = DateTime.MinValue; interval = 0;
            List<KeyValuePair<int, DateTime>> runs = PastRuns(store, product);
            if (runs.Count == 0) return false;
            no = runs[runs.Count - 1].Key; day = runs[runs.Count - 1].Value;
            Dictionary<int, int> gaps = new Dictionary<int, int>();
            for (int i = 1; i < runs.Count; i++)
                if (runs[i].Key == runs[i - 1].Key + 1 && runs[i].Value > runs[i - 1].Value)
                {
                    int g = (runs[i].Value - runs[i - 1].Value).Days, c;
                    gaps.TryGetValue(g, out c); gaps[g] = c + 1;
                }
            int best = 0;
            foreach (KeyValuePair<int, int> kv in gaps) if (kv.Value > best) { best = kv.Value; interval = kv.Key; }
            return true;
        }

        // ---------- Datei (XML, UTF-8: Umlaute und Sonderzeichen bleiben erhalten) ----------

        public void Save(string path)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = true; st.Encoding = new UTF8Encoding(false);
            using (XmlWriter w = XmlWriter.Create(path, st))
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("name", Name); w.WriteAttributeString("version", "3");
                if (ProductCode.Length > 0) w.WriteAttributeString("produkt", ProductCode);
                foreach (PackageStep s in Steps)
                {
                    w.WriteStartElement("Schritt");
                    w.WriteAttributeString("titel", s.Title); w.WriteAttributeString("quelle", s.Source); w.WriteAttributeString("block", s.Row);
                    w.WriteAttributeString("tag", I(s.Day)); w.WriteAttributeString("anzahl", I(s.Count)); w.WriteAttributeString("abstand", I(s.Interval)); w.WriteAttributeString("startnr", I(s.StartNo));
                    if (s.Kind != StepKind.Normal) w.WriteAttributeString("art", s.Kind == StepKind.Run ? "lauf" : "unterlauf");
                    if (s.SubNos.Length > 0) w.WriteAttributeString("unterlaeufe", s.SubNos);
                    w.WriteAttributeString("text", s.Text); w.WriteAttributeString("notiz", s.Note);
                    if (s.Ident.Length > 0) w.WriteAttributeString("kennung", s.Ident);   // fehlt bei aelteren Paketen
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
            p.ProductCode = IsCode4(root.GetAttribute("produkt")) ? root.GetAttribute("produkt") : "";   // fehlt bei aelteren Paketen
            foreach (XmlElement e in Elements(root))
            {
                if (e.Name == "Schritt")
                {
                    PackageStep s = new PackageStep();
                    s.Title = e.GetAttribute("titel"); s.Source = e.GetAttribute("quelle"); s.Row = e.GetAttribute("block");
                    s.Day = Clamp(ToInt(e.GetAttribute("tag"), 1), 1, 366); s.Count = Clamp(ToInt(e.GetAttribute("anzahl"), 1), 1, 99);
                    s.Interval = Clamp(ToInt(e.GetAttribute("abstand"), 1), 1, 30); s.StartNo = Clamp(ToInt(e.GetAttribute("startnr"), 1), 0, 9999);
                    string art = e.GetAttribute("art");
                    s.Kind = art == "lauf" ? StepKind.Run : (art == "unterlauf" ? StepKind.SubRun : StepKind.Normal);
                    s.SubNos = e.GetAttribute("unterlaeufe");
                    s.Text = e.GetAttribute("text"); s.Note = e.GetAttribute("notiz"); s.Ident = e.GetAttribute("kennung");
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

        List<Target> BuildTargets(List<RowInfo> rows, DateTime start, List<RunDate> schedule, out int unknownRow)
        {
            Dictionary<long, RunDate> over = new Dictionary<long, RunDate>();   // (Schritt, Lauf) -> Tag/Block aus dem Kampagnenstart-Dialog
            if (schedule != null) foreach (RunDate d in schedule) over[(long)d.Step * 100000 + d.Run] = d;
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
            for (int si = 0; si < Steps.Count; si++)
            {
                PackageStep s = Steps[si];
                if (s.IsEmpty) continue;
                List<KeyValuePair<int, string>> runs = s.Runs(ProductCode);
                for (int ri = 0; ri < runs.Count; ri++)
                {
                    KeyValuePair<int, string> run = runs[ri];
                    RunDate rd;
                    bool has = over.TryGetValue((long)si * 100000 + ri, out rd);
                    int id = RowId(rows, has && rd.Row.Length > 0 ? rd.Row : s.Row);   // Raumwechsel je Lauf; Equipment bleibt
                    if (id < 0) { unknownRow++; continue; }
                    DateTime day = start.AddDays((has ? rd.Day : run.Key) - 1);
                    string k = day.ToString("yyyyMMdd") + "|" + id;
                    Target t;
                    if (!map.TryGetValue(k, out t)) { t = new Target(); t.Day = day; t.RowId = id; t.Data = new Entry(); map[k] = t; order.Add(t); }
                    t.Data.Content = t.Data.Content.Length > 0 ? t.Data.Content + "\n\n" + run.Value : run.Value;
                    string idt = s.IdentFor(ri);
                    if (idt.Length > 0) t.Data.Spans.Add(PackageStep.IdentSpan(t.Data.Content.Length - idt.Length, idt.Length));
                }
            }
            return order;
        }

        // Speichern mit Protokolleintrag, wenn der Nutzer bekannt ist (Paket-Uebernahme erscheint im Verlauf)
        static bool SaveAudited(IDataStore store, List<RowInfo> rows, Target t, Entry result, User by)
        {
            if (by == null) return store.Save(t.Day, t.RowId, result);
            foreach (RowInfo r in rows) if (r.Id == t.RowId) return Audit.Save(store, t.Day, r, by, result);
            return store.Save(t.Day, t.RowId, result);
        }

        // Ergebnis der Uebernahme (Meldung fuer den Nutzer). schedule: Tage je Lauf (aus Schedule(), ggf. verschoben); null = Paket-Rhythmus.
        public string Apply(IDataStore store, DateTime start, ApplyMode mode, List<RunDate> schedule, User by = null)
        {
            List<RowInfo> rows = store.GetRows();
            int written = 0, skipped = 0, unknownRow, conflicts = 0;
            foreach (Target t in BuildTargets(rows, start, schedule, out unknownRow))
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
                if (SaveAudited(store, rows, t, result, by)) written++;
                else
                {
                    Entry again = store.Get(t.Day, t.RowId);
                    result.Version = again != null ? again.Version : 0;
                    if (SaveAudited(store, rows, t, result, by)) written++; else conflicts++;
                }
            }
            string msg = written + " Block/Blöcke geschrieben";
            if (skipped > 0) msg += ", " + skipped + " übersprungen (bereits belegt)";
            if (unknownRow > 0) msg += ", " + unknownRow + " mit unbekanntem Blocknamen ignoriert";
            if (conflicts > 0) msg += ", " + conflicts + " wegen Konflikt nicht gespeichert";
            return msg + ".";
        }

#if TESTHOOKS
        // Testpaket (erfundene Daten, Produkt 1234): Packen, Packen Backup, 4 Laeufe im Rhythmus Lauf, Mischen, Ruhe,
        // 2 Tage Backfill, Ruhe. Lauf mit Auftrag/Netzwerk-ID (= Laufnummer), Equipment und roter PA-Kennung je Lauf.
        public static Package SampleCampaign(string name)
        {
            Package p = new Package(); p.Name = name; p.ProductCode = "1234";
            PackageStep pk = Step(p, "Packen", "Packen", 1, StepKind.Normal, 1);
            pk.Text = "Auftrag/Netzwerk-ID: siehe Lauf\nDMO-Hinweis: Packanweisung im DMO beachten";
            Eq(pk, "TFF", "H1-X-0101"); Eq(pk, "Pumpe", "H1-P-0101-05"); Eq(pk, "VS", "H1-F-0103-04");
            Step(p, "Packen Backup", "", 2, StepKind.Normal, 1).Text = "Reservetag, falls Packen nicht abgeschlossen";
            PackageStep lf = Step(p, "Lauf – Auftrag/Netzwerk-ID", "Lauf", 3, StepKind.Run, 4);
            Eq(lf, "Pumpe", "H1-P-0101-05");
            EquipAssign tff = Eq(lf, "TFF", "H1-X-0101"); tff.Choices.Add(new EquipChoice("TFF", "H1-X-0102")); tff.Alternate = true; tff.Fallback = "H1-X-0103";
            Eq(lf, "Reds", "H1-T-0131"); Eq(lf, "EV", "H2-X-0201"); Eq(lf, "EVT", "H2-T-0201"); Eq(lf, "PT", "H1-T-0102");
            Eq(lf, "RT", "H1-T-0112"); Eq(lf, "VS", "H1-F-0103-04").Note = "(nur eins vorhanden)"; Eq(lf, "ST1", "H2-T-0211"); Eq(lf, "ST2", "H2-T-0212");
            lf.Ident = "PA 40010001, PA 40010017, PA 40010032, PA 40010048";
            PackageStep mi = Step(p, "Mischen", "Mischen", 4, StepKind.Run, 4);
            Eq(mi, "MT", "H2-T-0210"); Eq(mi, "ST1", "H2-T-0211"); Eq(mi, "ST2", "H2-T-0212"); Eq(mi, "MS", "H2-X-0210"); Eq(mi, "EV", "H2-X-0201"); Eq(mi, "Pumpe", "H1-P-0102-05");
            Step(p, "Backfill", "Backfill", 6, StepKind.Run, 4).Text = "Tag 1 von 2";
            Step(p, "Backfill", "Backfill", 7, StepKind.Run, 4).Text = "Tag 2 von 2";
            return p;
        }

        static PackageStep Step(Package p, string title, string source, int day, StepKind kind, int count)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Source = source; s.Row = "Suite 1"; s.Day = day; s.Kind = kind;
            s.Count = count; s.Interval = 6; s.StartNo = 1;
            p.Steps.Add(s); return s;
        }

        static EquipAssign Eq(PackageStep s, string sh, string id)
        {
            EquipAssign a = new EquipAssign(); a.Short = sh; a.Choices.Add(new EquipChoice(sh, id)); s.Equipment.Add(a); return a;
        }
#endif

        static int RowId(List<RowInfo> rows, string name)
        {
            foreach (RowInfo r in rows) if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) return r.Id;
            return -1;
        }
    }
}
