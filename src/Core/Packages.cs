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

    // Ablaufschritt einer Kampagne: wann (Tag relativ zum Start), in welchem Block, was, mit welchem Equipment; optional wiederholt
    // (Wiederholmodi, Versatz je Lauf, Ansatz: Packages.Repeat.cs).
    public partial class PackageStep
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
        public string Ident = "";           // alte Kennung je Lauf, Komma-getrennt (z. B. "PA 40010001, PA 40010017"); neu: IdentStart/IdentOver (Packages.Bind.cs); ganz unten rot, gross, fett
        public string Variant = "";         // Puffer-Schritt: Typ(en) der erzeugten Puffer (VariantSet, z. B. "mobil"), "" = kein Puffer
        public List<EquipAssign> Equipment = new List<EquipAssign>();

        // Format der Kennungszeile (rot, gross, fett).
        public static FormatSpan IdentSpan(int start, int length)
        {
            FormatSpan f = new FormatSpan(); f.Start = start; f.Length = length; f.Rgb = 0xFF0000; f.Size = 14f; f.Bold = true;
            return f;
        }

        public PackageStep Clone()
        {
            PackageStep s = (PackageStep)MemberwiseClone();
            s.Shift = new Dictionary<int, int>(Shift);
            s.CopyBind(this);
            s.Equipment = new List<EquipAssign>();
            foreach (EquipAssign a in Equipment) s.Equipment.Add(a.Clone());
            return s;
        }

        public bool IsEmpty
        {
            get
            {
                if (Title.Length > 0 || Text.Length > 0 || Note.Length > 0 || Ident.Trim().Length > 0 || IdentStart.Length > 0 || IdentOver.Count > 0 || ManualNo.Length > 0 || Kind == StepKind.SubRun) return false;
                foreach (EquipAssign a in Equipment) if (a.Line(0).Length > 0) return false;
                return true;
            }
        }

        public bool Numbered { get { return Kind != StepKind.Normal || (Bind == StepBind.JeLauf ? NumberRuns : RunCount > 1); } }

        // Laufnummer: mit Laufnummern-Praefix (XXYY, Package.Prefix) 8-stellig XXYYZZZZ (z. B. 13570030), sonst wie bisher "3".
        public string RunNo(string prefix, int run)
        {
            return Kind != StepKind.Normal ? Package.FormatRun(prefix, StartNo + run) : (StartNo + run).ToString(CultureInfo.InvariantCulture);
        }

        // Kurzbezeichnung fuer Tabellen (z. B. "Lauf 13570030").
        public string Label(string prefix, int run)
        {
            string t = Title.Length > 0 ? Title : (Kind == StepKind.SubRun ? "Unterlauf" : "Schritt");
            return Numbered ? t + " " + RunNo(prefix, run) : t;
        }

        public string Compose(int run, string prefix) { return string.Join("\n", Lines(run, prefix).ToArray()); }

        // Puffer-Schritt (Variante gesetzt): erzeugt Puffer statt Inhaltstext
        public bool IsBuffer { get { return Variant.Trim().Length > 0 && VariantSet.Of(Variant).Count > 0; } }

        // Puffer dieses Laufs (Ids vergibt der Block beim Anhaengen): Name = Titelzeile, Typ = Variante, Text = die uebrigen Zeilen
        // (Ansatz, Equipment, Notiz). "mobil|stationaer" = zwei Puffer gleichen Namens, "mobil:2" = zwei mobile.
        public List<BufferItem> BuffersFor(int run, string prefix)
        {
            List<string> lines = Lines(run, prefix);
            string name = "";
            if (Title.Length > 0 && lines.Count > 0) { name = lines[0]; lines.RemoveAt(0); }
            List<BufferItem> res = new List<BufferItem>();
            foreach (KeyValuePair<string, int> kv in VariantSet.Counts(Variant))
                for (int i = 0; i < kv.Value; i++) BufferSet.Add(res, name, kv.Key, string.Join("\n", lines.ToArray()));
            return res;
        }

        List<string> Lines(int run, string prefix)
        {
            string product = prefix;
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
            else if (Numbered && Title.Length > 0) lines.Add(Title + " " + RunNo(product, run));
            else if (ManualNo.Length > 0) lines.Add((Title.Length > 0 ? Title + " " : "") + "(Nr. " + ManualNo + ")");   // Klammer: zaehlt nicht als Kampagnenlauf (RunNumbers)
            else if (Title.Length > 0) lines.Add(Title);
            if (Text.Length > 0) lines.Add(Text);
            if (AnsatzLine().Length > 0) lines.Add(AnsatzLine());   // z. B. "Gebinde: Flasche 5 L"
            foreach (EquipAssign a in Equipment) { string l = a.Line(run); if (l.Length > 0) lines.Add(l); }
            lines.AddRange(NoteTexts());   // Notizzeilen ohne Praefix (im Plan kursiv, siehe EntryFor)
            if (IdentFor(run).Length > 0) lines.Add(IdentFor(run));   // immer die letzte Zeile (Format in BuildTargets)
            return lines;
        }

        // Block, wie ihn dieser Lauf allein im Plan ergibt (Plantext, rote Kennung, Herstellungsvariante), z. B. fuer die Vorschau.
        // Notizen des Schritts: je Zeile (Enter im Notizfeld) eine Notiz
        public List<string> NoteTexts()
        {
            List<string> res = new List<string>();
            foreach (string n in Note.Replace("\r", "").Split('\n')) if (n.Trim().Length > 0) res.Add(n.Trim());
            return res;
        }

        public Entry EntryFor(int run, string prefix)
        {
            Entry e = new Entry();
            if (IsBuffer) { e.Buffers = BuffersFor(run, prefix); return e; }
            List<string> lines = Lines(run, prefix);
            e.Content = string.Join("\n", lines.ToArray());
            string idt = IdentFor(run);
            int nn = NoteTexts().Count;
            if (nn > 0)
            {
                int first = 0, before = lines.Count - nn - (idt.Length > 0 ? 1 : 0);   // Zeilen des Inhalts vor der ersten Notiz (Text kann mehrzeilig sein)
                for (int k = 0; k < before; k++) first += lines[k].Split('\n').Length;
                Notes.SetNote(e, first, first + nn - 1, true);
            }
            if (idt.Length > 0) e.Spans.Add(IdentSpan(e.Content.Length - idt.Length, idt.Length));
            return e;
        }
    }

    // Ein Lauf eines Schritts mit Tag (1 = Starttag); im Kampagnenstart-Dialog einzeln verschiebbar.
    public class RunDate
    {
        public int Step, Run, Day;          // Step = Index in Package.Steps, Run = Wiederholung (0-basiert)
        public int Group = -1;              // zugehoeriger Hauptlauf (Wiederholung des Lauf-Schritts), -1 = davor (z. B. Packen)
        public string Label = "", Row = ""; // Row: Block (Raum), im Kampagnenstart je Lauf aenderbar

        // Eindeutiger Schluessel (Schritt, Lauf), z. B. fuer Woerterbuecher.
        public long Key() { return KeyOf(Step, Run); }
        public static long KeyOf(int step, int run) { return (long)step * 100000 + run; }
    }

    // Kompletter Block (Export einer Woche): Inhalt mit Formatierung, Kuerzel, LKZ.
    public class PackageBlock
    {
        public int Day = 1;
        public string Row = "";
        public Entry Data = new Entry();
    }

    public enum ApplyMode { FillEmpty, Overwrite, Append }

    // Laufnummern, Produktcode und fruehere Kampagnen: Packages.Runs.cs; Datei (XML): Packages.Xml.cs
    public partial class Package
    {
        public string Name = "";
        public string ProductCode = "";     // frei (z. B. "AB1", alt "1357"), siehe IsProductCode; leer = ohne Produkt
        public List<PackageStep> Steps = new List<PackageStep>();
        public List<PackageBlock> Blocks = new List<PackageBlock>();

        // ---------- Aus einer Woche erstellen / in eine Woche uebernehmen ----------

        public static Package FromWeek(IDataStore store, DateTime start, string name)
        {
            Package p = new Package(); p.Name = name;
            foreach (RowInfo r in store.GetRows())
                for (int d = 0; d < 7; d++)
                {
                    Entry e = store.Get(start.AddDays(d), r.Id);
                    if (e == null || (e.IsEmpty && !e.HasNotes && !e.Lkz && e.Buffers.Count == 0)) continue;
                    PackageBlock b = new PackageBlock(); b.Day = d + 1; b.Row = r.Name; b.Data = e; b.Data.Version = 0;
                    p.Blocks.Add(b);
                }
            return p;
        }

        // Vorschau/Uebernahme: je Ziel (Tag, Zeile) der neue Block. Schritte desselben Ziels werden zusammengefasst.
        class Target { public DateTime Day; public int RowId; public Entry Data; }

        List<Target> BuildTargets(List<RowInfo> rows, DateTime start, List<RunDate> schedule, out int unknownRow)
        {
            unknownRow = 0;
            Dictionary<string, Target> map = new Dictionary<string, Target>();
            List<Target> order = new List<Target>();
            foreach (PackageBlock b in Blocks)
            {
                int id = RowId(rows, b.Row);
                if (id < 0) { unknownRow++; continue; }
                DateTime day = start.AddDays(b.Day - 1);
                string k = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + id;
                Target t = new Target(); t.Day = day; t.RowId = id; t.Data = b.Data.Clone();
                if (!map.ContainsKey(k)) order.Add(t); else order[order.FindIndex(delegate(Target x) { return x == map[k]; })] = t;
                map[k] = t;
            }
            List<RunDate> plan = schedule != null ? new List<RunDate>(schedule) : Schedule(null, start.DayOfWeek);   // Schedule() ist die einzige Rechenquelle
            plan.Sort(delegate(RunDate a, RunDate b) { return a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Run.CompareTo(b.Run); });
            foreach (RunDate rd in plan)
            {
                if (rd.Step < 0 || rd.Step >= Steps.Count || Steps[rd.Step].IsEmpty) continue;
                PackageStep s = Steps[rd.Step];
                int id = RowId(rows, rd.Row.Length > 0 ? rd.Row : s.Row);   // Raumwechsel je Lauf; Equipment bleibt
                if (id < 0) { unknownRow++; continue; }
                DateTime day = start.AddDays(rd.Day - 1);
                string k = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + id;
                Target t;
                if (!map.TryGetValue(k, out t)) { t = new Target(); t.Day = day; t.RowId = id; t.Data = new Entry(); map[k] = t; order.Add(t); }
                if (s.IsBuffer) { BufferSet.AddRange(t.Data.Buffers, s.BuffersFor(rd.Run, Prefix)); continue; }   // mehrere Puffer am Tag: anhaengen
                AppendText(t.Data, s.EntryFor(rd.Run, Prefix));   // Notizzeilen und Kennung (Format) kommen mit
            }
            return order;
        }

        // Block fuer SaveManyWithAudit; mit Protokolleintrag, wenn der Nutzer bekannt ist (Paket-Uebernahme erscheint im Verlauf)
        static SaveItem Item(List<RowInfo> rows, Target t, Entry result, User by)
        {
            SaveItem it = new SaveItem(); it.Day = t.Day; it.RowId = t.RowId; it.Data = result;
            if (by != null) foreach (RowInfo r in rows) if (r.Id == t.RowId) { it.Audit = Audit.New(t.Day, r, by); break; }
            return it;
        }

        // Ergebnis aus dem aktuellen Stand; null = ueberspringen (Modus FillEmpty und Block belegt)
        static SaveItem Prepare(IDataStore store, List<RowInfo> rows, Target t, ApplyMode mode, User by)
        {
            Entry cur = store.Get(t.Day, t.RowId);
            Entry result = Merge(cur, t.Data, mode);
            if (result == null) return null;
            result.Version = cur != null ? cur.Version : 0;
            return Item(rows, t, result, by);
        }

        // Text und Formate von add hinter dst haengen (Leerzeile dazwischen), Formate verschoben
        static void AppendText(Entry dst, Entry add)
        {
            if (add.Content.Length == 0) return;
            int shift = dst.Content.Length > 0 ? dst.Content.Length + 2 : 0;
            dst.Content = dst.Content.Length > 0 ? dst.Content + "\n\n" + add.Content : add.Content;
            foreach (FormatSpan f in add.Spans) { FormatSpan c = f.Clone(); c.Start += shift; dst.Spans.Add(c); }
        }

        // Notizzeilen aus old ans Ende von dst (nicht, wenn dst dieselbe Zeile schon hat)
        static void KeepNotes(Entry dst, Entry old)
        {
            List<string> have = Notes.NoteTexts(dst), keep = new List<string>();
            foreach (string t in Notes.NoteTexts(old)) if (!have.Contains(t)) keep.Add(t);
            if (keep.Count == 0) return;
            int first = Notes.Lines(dst.Content).Count;
            dst.Content = dst.Content.Length > 0 ? dst.Content + "\n" + string.Join("\n", keep.ToArray()) : string.Join("\n", keep.ToArray());
            Notes.SetNote(dst, first, first + keep.Count - 1, true);
        }

        // Neuer Blockinhalt aus vorhandenem (cur) und Paket-Block (n); null = ueberspringen. Kuerzel und LKZ-Haken des vorhandenen Blocks bleiben erhalten.
        // Ein Block mit nur LKZ-Haken zaehlt als leer (IsEmpty), verliert den Haken aber nicht.
        internal static Entry Merge(Entry cur, Entry n, ApplyMode mode)
        {
            Entry result;
            if (cur == null) return n.Clone();
            bool free = cur.IsEmpty && cur.Buffers.Count == 0;   // ein Block mit Puffern gilt als belegt; nur Notizen zaehlen als leer
            if (free || mode == ApplyMode.Overwrite)
            {
                result = n.Clone();
                for (int i = 0; i < 3; i++) if (result.People[i].Length == 0) result.People[i] = cur.People[i];
                if (n.Buffers.Count == 0) result.Buffers = BufferSet.CloneAll(cur.Buffers);   // ohne Puffer im Paket bleiben die vorhandenen
                KeepNotes(result, cur);   // vorhandene Notizen bleiben (ans Ende)
            }
            else if (mode == ApplyMode.FillEmpty) return null;
            else
            {   // anhaengen: Text nach dem vorhandenen, Formatierung der neuen Textteile verschieben
                result = cur.Clone();
                AppendText(result, n);
                for (int i = 0; i < 3; i++) if (n.People[i].Length > 0) result.People[i] = result.People[i].Length > 0 ? result.People[i] + ", " + n.People[i] : n.People[i];
                BufferSet.AddRange(result.Buffers, n.Buffers);   // Puffer hinten anhaengen (neue Ids)
            }
            result.Lkz = cur.Lkz || n.Lkz;
            return result;
        }

        // Ergebnis der Uebernahme (Meldung fuer den Nutzer). schedule: Tage je Lauf (aus Schedule(), ggf. verschoben); null = Paket-Rhythmus.
        public string Apply(IDataStore store, DateTime start, ApplyMode mode, List<RunDate> schedule, User by = null)
        {
            List<string> belongs = store.GetCatalog().CheckPackage(this);   // harter Fehler: Zubehoer eines anderen Hauptgeraets
            if (belongs.Count > 0) throw new InvalidOperationException("Equipment passt nicht zusammen:\n" + string.Join("\n", belongs.ToArray()));
            string bp = BackupProblem(Steps);   // Back-Up ohne Packen
            if (bp != null) throw new InvalidOperationException(bp);
            if (schedule == null) SyncBackups(Steps);   // Back-Up immer einen Tag nach dem Packen
            List<PackageProblem> rules = Check(store, start, mode, schedule);   // Regeln (Packages.Rules.cs): Sperre gilt fuer alle, auch PackageAdmin
            if (HasBlock(rules)) throw new InvalidOperationException("Gesperrt, Regel verletzt:\n" + DescribeBlocks(rules));
            List<RowInfo> rows = store.GetRows();
            int written = 0, skipped = 0, unknownRow, conflicts = 0;
            // Alle Bloecke in EINEM Schreibvorgang (Versionspruefung je Block)
            List<SaveItem> batch = new List<SaveItem>(); List<Target> src = new List<Target>();
            foreach (Target t in BuildTargets(rows, start, schedule, out unknownRow))
            {
                SaveItem it = Prepare(store, rows, t, mode, by);
                if (it == null) { skipped++; continue; }
                batch.Add(it); src.Add(t);
            }
            if (batch.Count > 0) written += store.SaveManyWithAudit(batch);
            // Konflikte: Ergebnis aus dem neuen Stand mit derselben Modus-Logik neu berechnen, zweiter Durchgang nur fuer diese
            List<SaveItem> again = new List<SaveItem>();
            for (int i = 0; i < batch.Count; i++)
            {
                if (batch[i].Saved) continue;
                SaveItem it = Prepare(store, rows, src[i], mode, by);
                if (it == null) skipped++; else again.Add(it);
            }
            if (again.Count > 0) { int w = store.SaveManyWithAudit(again); written += w; conflicts += again.Count - w; }
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
            PackageStep mi = Step(p, "Mischen", "Mischen", 4, StepKind.Run, 4); mi.Bind = StepBind.JeLauf; mi.RunOffset = 1;   // folgt dem Hauptlauf (genau ein Hauptlauf-Schritt, Packages.Rules.cs)
            Eq(mi, "MT", "H2-T-0210"); Eq(mi, "ST1", "H2-T-0211"); Eq(mi, "ST2", "H2-T-0212"); Eq(mi, "MS", "H2-X-0210"); Eq(mi, "EV", "H2-X-0201"); Eq(mi, "Pumpe", "H1-P-0102-05");
            PackageStep b1 = Step(p, "Backfill", "Backfill", 6, StepKind.Run, 4); b1.Text = "Tag 1 von 2"; b1.Bind = StepBind.JeLauf; b1.RunOffset = 3;
            PackageStep b2 = Step(p, "Backfill", "Backfill", 7, StepKind.Run, 4); b2.Text = "Tag 2 von 2"; b2.Bind = StepBind.JeLauf; b2.RunOffset = 4;
            Seen.Add(p);
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
