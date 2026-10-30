using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Regeln "packen-tag" und "lauffolge" (Package.Check, Packages.Rules.cs). Laufnummern (ZZZZ) steigen mit dem Datum; erlaubt sind Luecken
    // (1 2 4), zwei Nummern am selben Tag (anderer Raum, Regel "gleichtag") und dieselbe Nummer an mehreren Tagen (Lauf ueber Nacht, von Hand im Plan).
    public partial class Package
    {
        // Packen und Back-Up nie am Tag eines Hauptlaufs (auch raumuebergreifend), Packen immer vor dem ersten Lauf
        void CheckPackenTag(List<PackageProblem> res, List<RunDate> plan, int main, DateTime start, bool known)
        {
            List<RunDate> runs = plan.FindAll(delegate(RunDate d) { return d.Step == main; });
            if (runs.Count == 0) return;
            RunDate first = runs[0];
            foreach (RunDate r in runs) if (r.Day < first.Day) first = r;
            foreach (RunDate d in plan)
            {
                if (d.Step < 0 || d.Step >= Steps.Count) continue;
                PackageStep s = Steps[d.Step];
                bool pack = !s.IsBackup && s.Source == PackenName;
                if (!pack && !s.IsBackup) continue;
                RunDate same = runs.Find(delegate(RunDate r) { return r.Day == d.Day; });
                PackageProblem p = null;
                if (same != null)
                {
                    p = Add(res, Severity.Sperre, "packen-tag", StepName(d.Step) + " und Lauf " + (same.Run + 1) + " liegen beide am " + DayLabel(d.Day, start, known) + " (am Lauftag wird nicht gepackt)", d.Step, d.Run);
                    p.Step2 = main; p.Run2 = same.Run;
                }
                else if (pack && d.Day > first.Day)
                {
                    p = Add(res, Severity.Sperre, "packen-tag", StepName(d.Step) + " liegt nach Lauf " + (first.Run + 1) + " (gepackt wird immer vor dem ersten Lauf)", d.Step, d.Run);
                    p.Step2 = main; p.Run2 = first.Run;
                }
                if (p != null) p.Action = "Packen z. B. 2 Tage vor Lauf 1 legen (der Back-Up liegt 1 Tag danach)";
            }
        }

        // Innerhalb der Kampagne: ein Lauf mit hoeherer Nummer darf nie vor einem Lauf mit niedrigerer Nummer liegen (Sperre)
        void CheckLaufFolge(List<PackageProblem> res, List<RunDate> plan, int main)
        {
            List<RunDate> runs = plan.FindAll(delegate(RunDate d) { return d.Step == main; });
            runs.Sort(delegate(RunDate a, RunDate b) { return a.Run.CompareTo(b.Run); });
            RunDate top = null;
            foreach (RunDate r in runs)
            {
                if (top != null && r.Day < top.Day)
                {
                    PackageProblem p = Add(res, Severity.Sperre, "lauffolge", "Lauf " + (r.Run + 1) + " (" + FormatRun(Prefix, Steps[main].StartNo + r.Run) + ") liegt vor Lauf " + (top.Run + 1) + " (" + FormatRun(Prefix, Steps[main].StartNo + top.Run) + ") – Laufnummern steigen mit dem Datum", main, r.Run);
                    p.Step2 = main; p.Run2 = top.Run; p.Action = "Läufe in der Reihenfolge ihrer Nummern legen";
                }
                else top = r;
            }
        }

        // Gegen den Plan (gleiches Praefix) nur ein Hinweis, ein Tippfehler in einer alten Woche darf nicht jeden Start sperren. Es gilt der erste Tag
        // einer Nummer (Lauf ueber Nacht zaehlt nicht als Bruch); dieselbe Nummer ist nie ein Bruch. Bloecke, die "Ueberschreiben" ersetzt, zaehlen nicht.
        void CheckLaufFolgePlan(List<PackageProblem> res, IDataStore store, DateTime start, ApplyMode mode, List<RunDate> plan, int main)
        {
            if (!IsCode4(Prefix)) return;
            Dictionary<int, DateTime> have = PlanRuns(store, Prefix, mode == ApplyMode.Overwrite ? ReplacedBlocks(store, start, plan) : null);
            if (have.Count == 0) return;
            foreach (RunDate r in plan)
            {
                if (r.Step != main) continue;
                int no = Steps[main].StartNo + r.Run;
                DateTime day = start.AddDays(r.Day - 1);
                foreach (KeyValuePair<int, DateTime> h in have)
                {
                    if (h.Key == no || (h.Key < no ? h.Value <= day : h.Value >= day)) continue;
                    Add(res, Severity.Hinweis, "lauffolge", "Lauf " + FormatRun(Prefix, no) + " am " + DateText(day) + " passt nicht zu Lauf " + FormatRun(Prefix, h.Key) + " im Plan (" + DateText(h.Value) + ") – Laufnummern steigen mit dem Datum", main, r.Run).Action = "erste Laufnummer oder Startdatum prüfen; ein Tippfehler im Plan darf bleiben";
                    return;   // eine Meldung genuegt, sonst wiederholt sie sich je Lauf
                }
            }
        }

        // Bloecke (Tag|Zeile), die das Paket im Modus "Ueberschreiben" ersetzt: genau seine Ziele, nie Nachbartage
        HashSet<string> ReplacedBlocks(IDataStore store, DateTime start, List<RunDate> plan)
        {
            HashSet<string> set = new HashSet<string>();
            List<RowInfo> rows = store.GetRows();
            foreach (PackageBlock b in Blocks) set.Add(start.Date.AddDays(b.Day - 1).Ticks + "|" + RowId(rows, b.Row));
            foreach (RunDate d in plan)
            {
                if (d.Step < 0 || d.Step >= Steps.Count) continue;
                PackageStep s = Steps[d.Step];
                int id = RowId(rows, d.Row.Length > 0 ? d.Row : s.Row);
                foreach (int dl in CardsSplit(s, start.DayOfWeek) ? s.CardDeltas() : new List<int>(new[] { 0 }))
                    set.Add(start.Date.AddDays(d.Day - 1 + dl).Ticks + "|" + id);
            }
            return set;
        }

        // Hauptlauf-Nummer (ZZZZ) -> erster Tag im Plan, ohne die Bloecke in skip
        static Dictionary<int, DateTime> PlanRuns(IDataStore store, string product, HashSet<string> skip)
        {
            Dictionary<int, DateTime> seen = new Dictionary<int, DateTime>();
            foreach (SearchHit h in store.Find(product))
            {
                if (skip != null && skip.Contains(h.Day.Date.Ticks + "|" + h.RowId)) continue;
                Entry e = store.Get(h.Day, h.RowId);
                if (e == null) continue;
                foreach (RunRef r in RunNumbers.Find(e, product))
                {
                    int n = int.Parse(r.Run, System.Globalization.CultureInfo.InvariantCulture);
                    DateTime t;
                    if (!seen.TryGetValue(n, out t) || h.Day < t) seen[n] = h.Day;
                }
            }
            return seen;
        }
    }
}
