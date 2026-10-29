using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    public enum Severity { Sperre, Hinweis }

    // Ergebnis der Regelpruefung: Sperre (Uebernehmen/Apply verweigert, auch PackageAdmin darf nicht umgehen) oder Hinweis.
    // Step/Run: Position im Paket (-1 = ganzes Paket); Code ordnet das Problem einem Eingabefeld zu (Paket-Editor).
    public class PackageProblem
    {
        public Severity Severity;
        public string Code = "", Text = "";
        public int Step = -1, Run = -1;

        public bool Blocks { get { return Severity == Severity.Sperre; } }
        public override string ToString() { return Text; }
    }

    // Regelpruefung eines Pakets (E4). Eine Quelle fuer Paket-Editor (Hinweisliste), Kampagnenstart und Apply.
    public partial class Package
    {
        // Nur das Paket selbst (ohne Plan): Termine aus Schedule()
        public List<PackageProblem> Check() { return Check(null, default(DateTime), ApplyMode.Overwrite, null); }

        public static bool HasBlock(List<PackageProblem> l) { return l.Exists(delegate(PackageProblem p) { return p.Blocks; }); }

        // Text mit Schrittname, fuer Meldungen ausserhalb des Paket-Editors
        public string Describe(PackageProblem p)
        {
            return (p.Step >= 0 && p.Step < Steps.Count && Steps[p.Step].Title.Length > 0 ? Steps[p.Step].Title + ": " : "") + p.Text;
        }

        public string DescribeBlocks(List<PackageProblem> l)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PackageProblem p in l) if (p.Blocks) { if (sb.Length > 0) sb.Append('\n'); sb.Append(Describe(p)); }
            return sb.ToString();
        }

        static PackageProblem Add(List<PackageProblem> res, Severity sev, string code, string text, int step = -1, int run = -1)
        {
            PackageProblem p = new PackageProblem(); p.Severity = sev; p.Code = code; p.Text = text; p.Step = step; p.Run = run;
            res.Add(p); return p;
        }

        // store != null: zusaetzlich gegen den Plan (Hauptlauf am selben Tag/Block, Puffer anderer Produkte). start = Datum von Tag 1,
        // mode = Uebernahme-Modus (Overwrite ersetzt Hauptlaeufe im Plan, kein Konflikt), schedule = Tage/Bloecke je Lauf (null = Schedule()).
        public List<PackageProblem> Check(IDataStore store, DateTime start, ApplyMode mode, List<RunDate> schedule)
        {
            List<PackageProblem> res = new List<PackageProblem>();
            DayOfWeek d1 = store != null && schedule == null ? start.DayOfWeek : Day1;
            List<RunDate> plan = schedule ?? Schedule(null, d1);
            int main = HeadIndex(d1);
            PackageStep head = main >= 0 ? Steps[main] : null;

            int heads = 0; string headNames = "";
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty) continue;
                if (s.Kind == StepKind.Run && s.Bind == StepBind.Frei) { heads++; headNames += (headNames.Length > 0 ? ", " : "") + s.Title; }
                if (Type == PackageType.Einzel && s.Kind != StepKind.Normal)
                    Add(res, Severity.Sperre, "einzel", "Ein Einzelpaket darf keinen Lauf- oder Unterlauf-Schritt enthalten (Laufnummer kommt von Hand).", i);
                if (Type == PackageType.Ereignis && TypeIsSet && s.ManualNo.Length > 0)
                    Add(res, Severity.Sperre, "ereignis", "Ein Sonderereignis hat keine Laufnummer von Hand.", i);
            }
            if (Type == PackageType.Kampagne && head == null) Add(res, Severity.Sperre, "kopf", "Die Kampagne hat keinen Hauptlauf-Schritt.");
            if (heads > 1) Add(res, Severity.Sperre, "kopf", "Die Kampagne hat " + heads + " Hauptlauf-Schritte (" + headNames + ") – es darf nur einen geben (weitere als „Lauf-Schritt mit Laufnummer“ anlegen).");
            if (head != null)
            {
                int n = head.RunCountFor(d1);
                if (n < 1 || n > PackageStep.MaxRuns) Add(res, Severity.Sperre, "laeufe", "Die Laufanzahl (" + n + ") muss zwischen 1 und " + PackageStep.MaxRuns + " liegen.", main);
            }
            string bp = BackupProblem(Steps);
            if (bp != null) Add(res, Severity.Sperre, "backup", bp);

            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty) continue;
                int nRuns = RunCountOf(s);
                if (s.Numbered && s.StartNo + nRuns - 1 > 9999) Add(res, Severity.Sperre, "number", "Laufnummer über 9999 – kleinere erste Laufnummer wählen.", i);
                if (nRuns > 1 && s.IdentStart.Length > 0 && s.IdentStart == PackageStep.IdentAt(s.IdentStart, 1))
                    Add(res, Severity.Sperre, "ident", "Kennung-Start „" + s.IdentStart + "“ hat keine Endziffern – ab Lauf 2 bleibt die Kennung gleich.", i);
                if (s.Source == PackenName && !s.IsBackup && s.ManualNo.Length == 0)
                    Add(res, Severity.Hinweis, "packen", "Packen ohne eigene Nummer im Paket – sie wird beim Kampagnenstart Pflicht (von Hand, 8 Ziffern).", i);
                if (s.RunEvery > 1 && !s.IsBuffer) Add(res, Severity.Hinweis, "every", "„Reicht für " + s.RunEvery + " Läufe“ ist nur für Puffer üblich.", i);
            }

            if (head != null)
            {
                List<RunDate> runs = plan.FindAll(delegate(RunDate d) { return d.Step == main; });
                int first = int.MaxValue;
                foreach (RunDate d in runs) first = Math.Min(first, d.Day);
                CheckSameDay(res, plan, main, runs, start, store != null || Lauf1.HasValue);
                CheckBuffers(res, plan, head.RunCountFor(d1));
                CheckSubRuns(res, plan, main, runs);
                foreach (RunDate d in plan)
                    if (first != int.MaxValue && d.Day < first - 60 && d.Step != main)
                        Add(res, Severity.Hinweis, "frueh", "Mehr als 60 Tage vor Lauf 1 (" + d.Label.Replace("\n", " ") + ").", d.Step, d.Run);
                if (store != null) CheckPlan(res, store, start, mode, plan, main);
            }
            else if (store != null) CheckPlan(res, store, start, mode, plan, -1);
            return res;
        }

        string DayLabel(int day, DateTime start, bool known)
        {
            return known && start != default(DateTime) ? DateText(start.AddDays(day - 1)) : (Lauf1.HasValue ? DateText(Lauf1.Value.AddDays(day - Lauf1Day)) : RelText(day));
        }

        // Zwei Hauptlaeufe im selben Block am selben Tag (Unterlauf und Puffer am selben Tag sind erlaubt)
        void CheckSameDay(List<PackageProblem> res, List<RunDate> plan, int main, List<RunDate> runs, DateTime start, bool known)
        {
            Dictionary<string, RunDate> seen = new Dictionary<string, RunDate>();
            foreach (RunDate d in runs)
            {
                string k = d.Day + "|" + (d.Row.Length > 0 ? d.Row : Steps[main].Row).ToLowerInvariant();
                RunDate o;
                if (seen.TryGetValue(k, out o))
                    Add(res, Severity.Sperre, "gleichtag", "Zwei Hauptläufe am selben Tag (" + DayLabel(d.Day, start, known) + ") im Block „" + (d.Row.Length > 0 ? d.Row : Steps[main].Row) + "“: Lauf " + (o.Run + 1) + " und Lauf " + (d.Run + 1) + ".", main, d.Run);
                else seen[k] = d;
            }
        }

        // Freie Puffer-Schritte: nicht mehr Termine als Laeufe geplant sind (JeLauf und "reicht fuer n Laeufe" erfuellen es von selbst)
        void CheckBuffers(List<PackageProblem> res, List<RunDate> plan, int planned)
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty || !s.IsBuffer || s.Bind != StepBind.Frei) continue;
                int cnt = plan.FindAll(delegate(RunDate d) { return d.Step == i; }).Count;
                if (cnt > planned) Add(res, Severity.Sperre, "puffer", "Mehr Puffer-Termine (" + cnt + ") als Produktläufe (" + planned + ") – „Zu jedem Lauf“ wählen oder die Anzahl verringern.", i);
            }
        }

        // Jeder Hauptlauf braucht je Unterlauf-Schritt einen Unterlauf ("Lauf 4 ohne Unterlauf")
        void CheckSubRuns(List<PackageProblem> res, List<RunDate> plan, int main, List<RunDate> runs)
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty || s.Kind != StepKind.SubRun) continue;
                List<RunDate> subs = plan.FindAll(delegate(RunDate d) { return d.Step == i; });
                List<int> miss = new List<int>();
                foreach (RunDate r in runs) if (!subs.Exists(delegate(RunDate d) { return d.Group == r.Run; })) miss.Add(r.Run);
                if (miss.Count == 0 && subs.Count < runs.Count) miss.Add(runs.Count - 1);   // mehrere Unterlaeufe im selben Lauf-Fenster: Zahl stimmt nicht
                foreach (int r in miss) Add(res, Severity.Sperre, "unterlauf", "Lauf " + (r + 1) + " ohne Unterlauf (Unterläufe: " + subs.Count + ", Hauptläufe: " + runs.Count + ").", i, r);
            }
        }

        // Gegen den Plan: Hauptlauf im selben Block/Tag, Puffer anderer Produkte am selben Tag (Sperre) bzw. Vor-/Folgetag (Hinweis)
        void CheckPlan(List<PackageProblem> res, IDataStore store, DateTime start, ApplyMode mode, List<RunDate> plan, int main)
        {
            List<RowInfo> rows = store.GetRows();
            string mine = Prefix;
            HashSet<string> done = new HashSet<string>();
            foreach (RunDate d in plan)
            {
                if (d.Step < 0 || d.Step >= Steps.Count) continue;
                PackageStep s = Steps[d.Step];
                int id = RowId(rows, d.Row.Length > 0 ? d.Row : s.Row);
                if (id < 0) continue;
                DateTime day = start.AddDays(d.Day - 1);
                if (d.Step == main && mode != ApplyMode.Overwrite)
                {
                    Entry e = store.Get(day, id);
                    if (e != null && RunNumbers.Find(e).Count > 0 && done.Add("h" + day.Ticks + "|" + id))
                        Add(res, Severity.Sperre, "planlauf", "Am " + DateText(day) + " liegt im Block „" + (d.Row.Length > 0 ? d.Row : s.Row) + "“ schon ein Hauptlauf im Plan – zwei Hauptläufe am selben Tag sind nicht möglich.", d.Step, d.Run);
                }
                if (!s.IsBuffer) continue;
                for (int off = -1; off <= 1; off++)
                {
                    Entry e = store.Get(day.AddDays(off), id);
                    if (e == null || e.Buffers.Count == 0) continue;
                    int other = ProductOfBuffers(e, mine);   // 1 = anderes Produkt, 0 = unbekannt, -1 = dasselbe
                    if (other < 0 || (other == 0 && off != 0) || !done.Add("p" + day.Ticks + "|" + id + "|" + off)) continue;
                    string when = off == 0 ? "am selben Tag" : (off < 0 ? "am Vortag" : "am Folgetag");
                    string txt = "Im Block „" + (d.Row.Length > 0 ? d.Row : s.Row) + "“ stehen " + when + " (" + DateText(day.AddDays(off)) + ") schon Puffer"
                        + (other == 1 ? " eines anderen Produkts" : " (Produkt nicht erkennbar, Zuordnung unsicher)")
                        + " – Puffer verschiedener Produkte werden nie am selben Tag, in der Regel mit mindestens 1 Tag Abstand hergestellt.";
                    Add(res, off == 0 && other == 1 ? Severity.Sperre : Severity.Hinweis, "puffer-produkt", txt, d.Step, d.Run);
                }
            }
        }

        // Produkt der Puffer eines Blocks aus Laufnummern in Name/Text: 1 = anderes Praefix als dieses Paket, -1 = nur dieses Paket,
        // 0 = keine Nummer erkennbar (oder eigenes Praefix unbekannt)
        static int ProductOfBuffers(Entry e, string mine)
        {
            bool other = false, same = false;
            foreach (BufferItem b in e.Buffers)
                foreach (RunRef r in RunNumbers.Find(b.Name + "\n" + b.Text, null))
                {
                    if (mine.Length > 0 && r.Product == mine) same = true; else if (mine.Length > 0) other = true;
                }
            return other ? 1 : (same ? -1 : 0);
        }
    }
}
