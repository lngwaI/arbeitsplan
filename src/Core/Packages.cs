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
        public bool ParaAfter;                                      // Leerzeile nach dieser Zeile im Plantext (Paket-Editor: Rechtsklick "Leerzeile danach")

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
        public string Line(int run) { return BufferSet.Plain(LineM(run)); }

        // Wie Line, aber mit NoteMark (BufferSet) statt des Leerzeichens vor der Notiz: Der Notiz-Teil wird im Plan kursiv gezeigt (UX-5f).
        public string LineM(int run)
        {
            EquipChoice c = ChoiceFor(run);
            string id = c != null ? c.Id : "";
            if (id.Length == 0 && Note.Length == 0 && Fallback.Length == 0) return "";
            StringBuilder sb = new StringBuilder(c != null && c.Short.Length > 0 ? c.Short : Short).Append(':');
            if (id.Length > 0) sb.Append(' ').Append(id);
            if (Fallback.Length > 0) sb.Append(" (Ausweich: ").Append(Fallback).Append(')');
            if (Note.Length > 0) sb.Append(BufferSet.NoteMark).Append(Note);
            return sb.ToString();
        }
    }

    // Normal: Schritt (bei Wiederholung "Titel 1, 2, 3"); Run: Hauptlauf mit voller Laufnummer; SubRun: Unterlaeufe (Splitten/Einfrieren) eines Hauptlaufs.
    public enum StepKind { Normal, Run, SubRun }

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

        // Pufferpaket (Unterpaket): kleines wiederverwendbares Paket nur aus Puffer-Schritten; im Paket-Editor als Kopie in eine Kampagne einsetzbar
        public bool IsBufferPackage
        {
            get
            {
                if (Steps.Count == 0 || Blocks.Count > 0) return false;
                foreach (PackageStep s in Steps) if (!s.IsBuffer) return false;
                return true;
            }
        }

        // Kopie des Puffer-Schritts fuer ein Pufferpaket: ohne Bezug zu Hauptlauf und Rhythmus (Tag 1, ein Termin)
        public static PackageStep ForBufferPackage(PackageStep s)
        {
            PackageStep c = s.Clone();
            c.Day = 1; c.Count = 1; c.Interval = 1; c.Bind = StepBind.Frei; c.RunOffset = 0; c.RunEvery = 1;
            c.Repeat = RepeatMode.Interval; c.WeekDays = 0; c.DayList = ""; c.Until = 0;
            c.Shift.Clear(); c.Move.Clear(); c.Skip.Clear(); c.IdentOver.Clear();
            return c;
        }

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
                bool split = CardsSplit(s, start.DayOfWeek);   // UX-5c: Puffer-Karten mit eigenem Versatz liegen an eigenen Tagen
                foreach (int dl in split ? s.CardDeltas() : new List<int>(new[] { 0 }))
                {
                    DateTime day = start.AddDays(rd.Day - 1 + dl);
                    string k = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + id;
                    Target t;
                    if (!map.TryGetValue(k, out t)) { t = new Target(); t.Day = day; t.RowId = id; t.Data = new Entry(); map[k] = t; order.Add(t); }
                    if (s.IsBuffer) { int lauf = LaufOf(plan, rd, start.DayOfWeek) + 1; BufferSet.AddRange(t.Data.Buffers, WithLauf(WithCode(split ? s.BuffersAt(rd.Run, Prefix, lauf, dl) : s.BuffersFor(rd.Run, Prefix, lauf)), lauf, start.DayOfWeek)); }   // mehrere Puffer am Tag: anhaengen
                    else if (s.Freeze != null && rows.Exists(delegate(RowInfo r) { return r.Id == id && r.Freeze; }))   // Vorgang nur in Freeze-Zeilen sichtbar; sonst bleibt es Text (nichts geht unsichtbar verloren)
                    {
                        FreezeSet.Add(t.Data.Freezes, FreezeFor(s, rd.Run, LaufOf(plan, rd, start.DayOfWeek) + 1, day, start.DayOfWeek));
                        AppendText(t.Data, FreezeRest(s, rd.Run));
                    }
                    else AppendText(t.Data, s.EntryFor(rd.Run, Prefix));   // Notizzeilen und Kennung (Format) kommen mit
                }
            }
            return order;
        }

        // UX-6: Puffer heissen im Plan "<Produktcode> - <Name>" (einzige Stelle; ohne Code oder Namen unveraendert, kein doppelter Praefix)
        public static string PlanBufferName(string code, string name)
        {
            code = (code ?? "").Trim();
            if (code.Length == 0 || string.IsNullOrEmpty(name)) return name ?? "";
            if (name.StartsWith(code, StringComparison.OrdinalIgnoreCase) && (name.Length == code.Length || " -:".IndexOf(name[code.Length]) >= 0)) return name;   // Code schon vorne (auch "PRD-1 Loesung", "prd-1: x")
            return code + " - " + name;
        }

        // Puffernamen mit Produktcode wie im Plan; auch die Vorschau im Paket-Editor nutzt es (Vorschau = Plan)
        public List<BufferItem> WithCode(List<BufferItem> l) { foreach (BufferItem b in l) b.Name = PlanBufferName(ProductCode, b.Name); return l; }

#if TESTHOOKS
        // Testpaket (erfundene Daten, Produkt 1234, Typ Kampagne, Lauf 1 ohne festes Datum): Packen, Packen Backup, Hauptlauf 4x im Rhythmus, Mischen und
        // 2 Tage Backfill je Lauf (JeLauf). Lauf mit Auftrag/Netzwerk-ID (= Laufnummer), Equipment und roter PA-Kennung (Start + Endziffern) je Lauf.
        public static Package SampleCampaign(string name)
        {
            Package p = new Package(); p.Name = name; p.ProductCode = "1234"; p.Type = PackageType.Kampagne;
            PackageStep pk = Step(p, "Packen", "Packen", 1, StepKind.Normal, 1);
            pk.Text = "Auftrag/Netzwerk-ID: siehe Lauf\nDMO-Hinweis: Packanweisung im DMO beachten";
            Eq(pk, "TFF", "H1-X-0101"); Eq(pk, "Pumpe", "H1-P-0101-05"); Eq(pk, "VS", "H1-F-0103-04");
            Step(p, "Packen Backup", "", 2, StepKind.Normal, 1).Text = "Reservetag, falls Packen nicht abgeschlossen";
            PackageStep lf = Step(p, "Lauf – Auftrag/Netzwerk-ID", "Lauf", 3, StepKind.Run, 4);
            Eq(lf, "Pumpe", "H1-P-0101-05");
            EquipAssign tff = Eq(lf, "TFF", "H1-X-0101"); tff.Choices.Add(new EquipChoice("TFF", "H1-X-0102")); tff.Alternate = true; tff.Fallback = "H1-X-0103";
            Eq(lf, "Reds", "H1-T-0131"); Eq(lf, "EV", "H2-X-0201"); Eq(lf, "EVT", "H2-T-0201"); Eq(lf, "PT", "H1-T-0102");
            Eq(lf, "RT", "H1-T-0112"); Eq(lf, "VS", "H1-F-0103-04").Note = "(nur eins vorhanden)"; Eq(lf, "ST1", "H2-T-0211"); Eq(lf, "ST2", "H2-T-0212");
            lf.IdentStart = "PA 40010001";   // Kennungstart: Endziffern +1 je Lauf
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

        // PB: Testkampagne plus Puffer-Schritt mit 3 mobilen Puffern (Namen), 2 mobilen Tanks und einer Flasche je Puffer; Tag 2 (--makepkg PB...)
        public static Package SampleBuffers(string name)
        {
            Package p = SampleCampaign(name);
            PackageStep pu = new PackageStep(); pu.Title = "Puffer mobil"; pu.Source = "Puffer mobil"; pu.Row = "Puffer"; pu.Day = 2; pu.Variant = "mobil"; pu.PufferCount = 3; pu.PufferNames = "Lösung A|Lösung B";
            for (int i = 1; i <= 2; i++) { EquipAssign a = new EquipAssign(); a.Short = "MobT"; a.Note = "Tank " + i; pu.Equipment.Add(a); }
            EquipAssign fl = new EquipAssign(); fl.Short = "Flasche"; fl.Note = "5 L"; pu.Equipment.Add(fl);
            p.Steps.Add(pu);
            return p;
        }
#endif

    }
}
