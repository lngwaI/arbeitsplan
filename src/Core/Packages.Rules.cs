using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    public enum Severity { Sperre, Hinweis, Info }   // Info: nur zur Kenntnis (kein Fehler, macht die Pruefung nicht "auffaellig")

    // Ergebnis der Regelpruefung: Sperre (Uebernehmen/Apply verweigert, auch PackageAdmin darf nicht umgehen) oder Hinweis.
    // Step/Run: Position im Paket (-1 = ganzes Paket); Code ordnet das Problem einem Eingabefeld zu (Paket-Editor).
    public class PackageProblem
    {
        public Severity Severity;
        public string Code = "", Text = "";
        public int Step = -1, Run = -1;
        public int Step2 = -1, Run2 = -1;   // zweiter Beteiligter im Paket (z. B. der andere Lauf); -1 = keiner / liegt im Plan
        public string Action = "";          // kurze Handlungsanweisung; Text nennt Ursache und Beteiligte

        public bool Blocks { get { return Severity == Severity.Sperre; } }
        public bool IsInfo { get { return Severity == Severity.Info; } }
        public string Full { get { return Action.Length > 0 ? Text + " – " + Action : Text; } }
        public override string ToString() { return Full; }

        // Kurzname der Regel (Gruppenkopf in der Hinweisliste)
        public static string RuleTitle(string code)
        {
            switch (code)
            {
                case "gleichtag": return "Zwei Hauptläufe am selben Tag und Raum";
                case "planlauf": return "Hauptlauf liegt schon im Plan";
                case "puffer": return "Mehr Puffer-Termine als Produktläufe";
                case "pufrhythmus": return "Puffer-Rhythmus endet vor dem letzten Lauf";
                case "pufnach": return "Puffer liegt nach dem Lauf, den er bedient";
                case "pufdoppelt": return "Gerät in zwei Puffern am selben Tag";
                case "puffer-produkt": return "Puffer verschiedener Produkte zu nah beieinander";
                case "unterlauf": return "Unterlauf fehlt";
                case "frueh": return "Termin sehr früh vor Lauf 1";
                case "pufvor": return "Puffer vor dem Kampagnen-Start";
                case "packen-tag": return "Packen oder Back-Up am Tag eines Laufs";
                case "lauffolge": return "Laufnummern steigen nicht mit dem Datum";
                case "pufkeine": return "Kampagne ohne Puffer";
                default: return "";   // ohne Titel: nie gruppiert
            }
        }
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
            return (p.Step >= 0 && p.Step < Steps.Count && Steps[p.Step].Title.Length > 0 ? Steps[p.Step].Title + ": " : "") + p.Full;
        }

        public string DescribeBlocks(List<PackageProblem> l)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PackageProblem p in l) if (p.Blocks) { if (sb.Length > 0) sb.Append('\n'); sb.Append(Describe(p)); }   // Describe: Text + Handlung
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
                    Add(res, Severity.Info, "packen", "Packen hat noch keine eigene Nummer – der Kampagnenstart fragt sie ab.", i).Action = "oben unter „Nr. für Packen“ eintragen (8 Ziffern, z. B. 12340001)";
                if (s.IsBuffer && Math.Max(s.PufferCount, s.Puffers.Count) > 9) Add(res, Severity.Sperre, "pufzahl", "Mehr als 9 Puffer je Termin sind nicht möglich (" + Math.Max(s.PufferCount, s.Puffers.Count) + ").", i);
                if (s.IsBuffer && s.Puffers.Count == 0 && s.BufferNames().Length > Math.Max(1, s.PufferCount)) Add(res, Severity.Hinweis, "pufnamen", "Es sind mehr Puffer-Namen als Puffer eingetragen – die überzähligen Namen werden nicht verwendet.", i);
                if (s.RunEvery > 1 && !s.IsBuffer) Add(res, Severity.Hinweis, "every", "„Reicht für " + s.RunEvery + " Läufe“ ist nur für Puffer üblich.", i);
            }

            CheckPufferDup(res, plan, start, store != null || Lauf1.HasValue);
            CheckPufferBefore(res, plan, d1);   // N7: Puffer-Karte vor dem Kampagnen-Start (Packages.PufferDay.cs)

            if (head != null)
            {
                List<RunDate> runs = plan.FindAll(delegate(RunDate d) { return d.Step == main; });
                int first = int.MaxValue;
                foreach (RunDate d in runs) first = Math.Min(first, d.Day);
                CheckSameDay(res, plan, main, runs, start, store != null || Lauf1.HasValue);
                CheckBuffers(res, plan, head.RunCountFor(d1), main);
                CheckPufferAfter(res, plan, d1);
                CheckSubRuns(res, plan, main, runs);
                CheckPackenTag(res, plan, main, start, store != null || Lauf1.HasValue);   // Packages.RulesPacken.cs
                CheckLaufFolge(res, plan, main);
                if (store != null) CheckLaufFolgePlan(res, store, start, mode, plan, main);
                if (Type == PackageType.Kampagne && !Steps.Exists(delegate(PackageStep x) { return !x.IsEmpty && (x.IsBuffer || x.UsesAnsatz); }))
                    Add(res, Severity.Hinweis, "pufkeine", "Die Kampagne hat keinen Puffer-Schritt").Action = "Puffer hinzufügen, falls das Produkt welche braucht";
                foreach (RunDate d in plan)
                    if (first != int.MaxValue && d.Day < first - 60 && d.Step != main)
                        Add(res, Severity.Hinweis, "frueh", StepName(d.Step) + " (" + d.Label.Replace("\n", " ") + ") liegt mehr als 60 Tage vor Lauf 1", d.Step, d.Run).Action = "Tag prüfen";
                if (store != null) CheckPlan(res, store, start, mode, plan, main);
            }
            else if (store != null) CheckPlan(res, store, start, mode, plan, -1);
            return res;
        }

        string StepName(int i) { return Steps[i].Title.Length > 0 ? Steps[i].Title : "Schritt " + (i + 1); }

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
                {
                    PackageProblem p = Add(res, Severity.Sperre, "gleichtag", "Lauf " + (o.Run + 1) + " und Lauf " + (d.Run + 1) + " liegen beide am " + DayLabel(d.Day, start, known) + " in " + (d.Row.Length > 0 ? d.Row : Steps[main].Row) + " (zwei Hauptläufe am selben Tag nicht erlaubt)", main, d.Run);
                    p.Step2 = main; p.Run2 = o.Run; p.Action = "einen der beiden Läufe auf einen anderen Tag oder Raum legen";
                    Hand(p, main, d.Run, main, o.Run);
                }
                else seen[k] = d;
            }
        }

        // Von Hand gesetzter Lauf (PackageStep.Pinned): der Verstoss bleibt sichtbar, sperrt aber nicht
        PackageProblem Hand(PackageProblem p, int step, int run, int step2 = -1, int run2 = -1)
        {
            bool pin = (step >= 0 && step < Steps.Count && Steps[step].ByHand(run)) || (step2 >= 0 && step2 < Steps.Count && Steps[step2].ByHand(run2));
            if (pin) { p.Severity = Severity.Hinweis; p.Text += " – von Hand gesetzt"; }
            return p;
        }

        // Freie Puffer-Schritte: nicht mehr Termine als Laeufe geplant sind (JeLauf und "reicht fuer n Laeufe" erfuellen es von selbst).
        // Tagesliste (Rhythmus) ist nur ein Hinweis; deckt sie den letzten Lauf nicht ab, ebenfalls (Kampagnenstart mit mehr Laeufen).
        void CheckBuffers(List<PackageProblem> res, List<RunDate> plan, int planned, int main)
        {
            List<int> runDays = new List<int>();
            foreach (RunDate d in plan) if (d.Step == main) runDays.Add(d.Day);
            runDays.Sort();
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty || !s.IsBuffer || s.Bind != StepBind.Frei) continue;
                List<RunDate> mine = plan.FindAll(delegate(RunDate d) { return d.Step == i; });
                if (mine.Count > planned)
                    Add(res, s.Repeat == RepeatMode.DayList ? Severity.Hinweis : Severity.Sperre, "puffer", "Pufferläufe (" + mine.Count + ") von Puffer „" + StepName(i) + "“ übersteigen Produktläufe (" + planned + ")", i).Action = "„Zu jedem Lauf“ wählen oder die Anzahl verringern";
                if (s.Repeat == RepeatMode.DayList && mine.Count > 0 && runDays.Count > 1)
                {
                    int last = runDays[runDays.Count - 1], prev = runDays[runDays.Count - 2];
                    if (!mine.Exists(delegate(RunDate d) { return d.Day > prev && d.Day <= last; }))
                        Add(res, Severity.Hinweis, "pufrhythmus", "Die festen Tage von Puffer „" + StepName(i) + "“ reichen nicht bis zum letzten Lauf (Tag " + last + ")", i).Action = "Tage ergänzen (Puffer-Rhythmus) – bei mehr Läufen wächst der Rhythmus nicht mit";
                }
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
                foreach (int r in miss)
                {
                    PackageProblem p = Add(res, Severity.Sperre, "unterlauf", "Lauf " + (r + 1) + " ohne Unterlauf (Schritt „" + StepName(i) + "“: " + subs.Count + " Unterläufe für " + runs.Count + " Hauptläufe)", i, r);
                    p.Action = "Unterlauf-Nummer für Lauf " + (r + 1) + " ergänzen oder Wiederholungen angleichen";
                }
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
                    List<RunRef> have = e != null ? RunNumbers.Find(e) : null;
                    if (have != null && have.Count > 0 && done.Add("h" + day.Ticks + "|" + id))
                        Add(res, Severity.Sperre, "planlauf", "Lauf " + (d.Run + 1) + " dieses Pakets und Lauf " + have[0].Run + " (schon im Plan) liegen beide am " + DateText(day) + " in " + (d.Row.Length > 0 ? d.Row : s.Row) + " (zwei Hauptläufe am selben Tag nicht erlaubt)", d.Step, d.Run).Action = "Startdatum oder Raum ändern oder „Überschreiben“ wählen";
                }
                if (!s.IsBuffer) continue;
                foreach (int cd in CardsSplit(s, start.DayOfWeek) ? s.CardDeltas() : new List<int>(new[] { 0 }))   // Karten mit eigenem Versatz liegen an anderen Tagen
                for (int off = -1; off <= 1; off++)
                {
                    DateTime cday = day.AddDays(cd);
                    Entry e = store.Get(cday.AddDays(off), id);
                    if (e == null || e.Buffers.Count == 0) continue;
                    int other = ProductOfBuffers(e, mine);   // 1 = anderes Produkt, 0 = unbekannt, -1 = dasselbe
                    if (other < 0 || (other == 0 && off != 0) || !done.Add("p" + cday.Ticks + "|" + id + "|" + off)) continue;
                    string when = off == 0 ? "am selben Tag" : (off < 0 ? "am Vortag" : "am Folgetag");
                    string oname = "", oprod = "";
                    foreach (BufferItem b in e.Buffers)
                        foreach (string pr in BufferProducts(b))
                            if (oname.Length == 0 && pr != mine) { oname = b.Name; oprod = pr; }
                    if (oname.Length == 0 && e.Buffers.Count > 0) oname = e.Buffers[0].Name;
                    string txt = "Puffer „" + StepName(d.Step) + "“ (Produkt " + ProductCode + ") am " + DateText(cday) + " in " + (d.Row.Length > 0 ? d.Row : s.Row)
                        + " und im Plan schon Puffer „" + oname + "“" + (other == 1 ? " (Produkt " + oprod + ")" : " (Produkt nicht erkennbar, Zuordnung unsicher)") + " " + when;
                    Add(res, off == 0 && other == 1 ? Severity.Sperre : Severity.Hinweis, "puffer-produkt", txt, d.Step, d.Run).Action = "Puffer verschiedener Produkte nie am selben Tag, möglichst mit 1 Tag Abstand – Termin verschieben";
                }
            }
        }

        // Produkt der Puffer eines Blocks aus LaufNo und Laufnummern in Name/Text: 1 = anderes Praefix als dieses Paket, -1 = nur dieses Paket,
        // 0 = keine Nummer erkennbar (oder eigenes Praefix unbekannt)
        static int ProductOfBuffers(Entry e, string mine)
        {
            bool other = false, same = false;
            foreach (BufferItem b in e.Buffers)
                foreach (string pr in BufferProducts(b))
                {
                    if (mine.Length > 0 && pr == mine) same = true; else if (mine.Length > 0) other = true;
                }
            return other ? 1 : (same ? -1 : 0);
        }

        // Praefixe (XXYY), die ein Puffer erkennen laesst: bedienter Lauf (LaufNo), sonst Laufnummern in Name/Text (alte Daten)
        static List<string> BufferProducts(BufferItem b)
        {
            List<string> l = new List<string>();
            if (b.LaufNo.Length == 8) l.Add(b.LaufNo.Substring(0, 4));
            foreach (RunRef r in RunNumbers.Find(b.Name + "\n" + b.Text, null)) if (!l.Contains(r.Product)) l.Add(r.Product);
            return l;
        }
    }
}
