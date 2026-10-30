using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    // Anzeigetexte zum Zeitmodell: Termine relativ zu Lauf 1 (nie Tagesnummern oder Minus-Tage), Datumsbereich und Laeufe eines Schritts.
    public partial class Package
    {
        // Datum eines Paket-Tags: aus dem Datum von Tag 1 (tag1) oder aus Lauf1; null = unbekannt
        public DateTime? DateOf(int day, DateTime? tag1 = null)
        {
            if (tag1.HasValue) return tag1.Value.Date.AddDays(day - 1);
            if (Lauf1.HasValue) return Lauf1.Value.Date.AddDays(day - Lauf1Day);
            return null;
        }

        static string DateText(DateTime d, bool year = true)
        {
            return PackageStep.DayNames[((int)d.DayOfWeek + 6) % 7] + " " + d.ToString(year ? "dd.MM.yyyy" : "dd.MM.", CultureInfo.InvariantCulture);
        }

        static string DayWord(int n) { return n + (n == 1 ? " Tag" : " Tage"); }

        // "Fr 12.03.2027 · 3 Tage vor Lauf 1", "Mo 15.03.2027 · Lauf 1", "1 Tag nach Lauf 1"; ohne bekanntes Datum nur der Bezug.
        public string RelText(int day, DateTime? tag1 = null)
        {
            List<int> runs = new List<int>();   // Tage der stattfindenden Hauptlaeufe, Nummer = Index der geplanten Laeufe
            List<int> nos = new List<int>();
            PackageStep h = Head;
            if (h != null)
            {
                List<int> d = h.Days(Day1);
                for (int i = 0; i < d.Count; i++) if (!h.Skips(i)) { runs.Add(d[i]); nos.Add(i + 1); }
            }
            string rel = RelOf(day, runs, nos);
            DateTime? dt = DateOf(day, tag1);
            return dt.HasValue ? DateText(dt.Value) + " · " + rel : rel;
        }

        // Nur der Bezug ("3 Tage vor Lauf 1") zu den stattfindenden Hauptlaeufen eines tatsaechlichen Plans (Kampagnenstart: mit Verschiebungen)
        public string RelText(int day, List<RunDate> plan)
        {
            List<RunDate> main = new List<RunDate>();
            int hi = Steps.IndexOf(Head);
            foreach (RunDate d in plan) if (d.Step == hi) main.Add(d);
            main.Sort(delegate(RunDate a, RunDate b) { return a.Run.CompareTo(b.Run); });
            List<int> runs = new List<int>(), nos = new List<int>();
            foreach (RunDate d in main) { runs.Add(d.Day); nos.Add(d.Run + 1); }
            return RelOf(day, runs, nos);
        }

        static string RelOf(int day, List<int> runs, List<int> nos)
        {
            if (runs.Count == 0)
            {
                int rd = day - 1;
                return rd == 0 ? "Beginn" : DayWord(Math.Abs(rd)) + (rd > 0 ? " nach" : " vor") + " Beginn";
            }
            int k = 0;   // letzter Lauf am oder vor dem Tag, sonst der erste
            for (int i = 0; i < runs.Count; i++) if (runs[i] <= day) k = i;
            int diff = day - runs[k];
            string lauf = "Lauf " + nos[k];
            return diff == 0 ? lauf : DayWord(Math.Abs(diff)) + (diff > 0 ? " nach " : " vor ") + lauf;
        }

        List<int> DaysOfStep(PackageStep step)
        {
            int si = Steps.IndexOf(step);
            List<int> days = new List<int>();
            foreach (RunDate d in Schedule(null)) if (d.Step == si) days.Add(d.Day);
            days.Sort();
            return days;
        }

        // "Fr 12.03.–So 21.03.2027" (ein Tag: "Fr 12.03.2027"); ohne bekanntes Datum "3 Tage vor Lauf 1 – 1 Tag nach Lauf 1"
        public string DateRangeText(PackageStep step, DateTime? tag1 = null)
        {
            List<int> days = DaysOfStep(step);
            if (days.Count == 0) return "";
            int a = days[0], b = days[days.Count - 1];
            DateTime? da = DateOf(a, tag1), db = DateOf(b, tag1);
            if (!da.HasValue || !db.HasValue) return a == b ? RelText(a) : RelText(a) + " – " + RelText(b);
            if (a == b) return DateText(da.Value);
            return DateText(da.Value, da.Value.Year != db.Value.Year) + "–" + DateText(db.Value);
        }

        // "Lauf 1–10 · 10 Termine", bei Luecken "Lauf 1, 2, 4 · 3 Termine"; freie Schritte nur "n Termine"
        public string RunsText(PackageStep step)
        {
            int si = Steps.IndexOf(step);
            List<int> runs = new List<int>();
            List<RunDate> plan = Schedule(null);
            foreach (RunDate d in plan) if (d.Step == si) runs.Add(d.Run + 1);
            int n = runs.Count;
            if (n == 0) return "kein Termin";
            string t = n + (n == 1 ? " Termin" : " Termine");
            PackageStep h = Head;
            if (h == null || (step != h && step.Bind != StepBind.JeLauf))
            {
                if (h != null && step != h && step.IsBuffer)   // UX-5c: eigener Rhythmus, "fuer Lauf 1–3"
                {
                    List<int> fl = new List<int>();
                    foreach (RunDate d in plan) if (d.Step == si) { int l = LaufOf(plan, d) + 1; if (l > 0 && !fl.Contains(l)) fl.Add(l); }
                    fl.Sort();
                    if (fl.Count > 0) t += " · für Lauf " + RangeText(fl);
                }
                return t;
            }
            return "Lauf " + RangeText(runs) + " · " + t;
        }

        // "1–4", "1, 2, 4": aufeinanderfolgende ab drei zusammengefasst
        static string RangeText(List<int> runs)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < runs.Count; )
            {
                int j = i;
                while (j + 1 < runs.Count && runs[j + 1] == runs[j] + 1) j++;
                if (i > 0) sb.Append(", ");
                if (j - i >= 2) { sb.Append(runs[i]).Append('–').Append(runs[j]); i = j + 1; }
                else { sb.Append(runs[i]); i++; }
            }
            return sb.ToString();
        }
    }
}
