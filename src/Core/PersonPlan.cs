using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    public class PersonShift
    {
        public DateTime Day;
        public int Shift;            // 0 F, 1 S, 2 N
        public string Block;
    }

    // Schichten eines Kuerzels ueber mehrere Tage (nur lesend) samt Warnungen.
    public static class PersonPlan
    {
        // Woerter/Kuerzel getrennt durch Leerraum , ; / (auch fuer Suche, Markierung und Text-loeschen-Pruefung in der UI).
        public static readonly Regex Token = new Regex(@"[^\s,;/]+", RegexOptions.Compiled);
        public static readonly string[] ShiftNames = { "F", "S", "N" };

        static bool Has(string text, string k)
        {
            foreach (Match m in Token.Matches(text ?? "")) if (string.Equals(m.Value, k, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static List<PersonShift> Collect(IDataStore store, List<RowInfo> rows, string kuerzel, DateTime from, int days)
        {
            List<PersonShift> res = new List<PersonShift>();
            if (string.IsNullOrEmpty(kuerzel)) return res;
            for (int d = 0; d < days; d++)
            {
                DateTime day = from.AddDays(d);
                foreach (RowInfo r in rows)
                {
                    Entry e = store.Get(day, r.Id);
                    if (e == null) continue;
                    for (int s = 0; s < 3; s++)
                        if (Has(e.People[s], kuerzel)) { PersonShift p = new PersonShift(); p.Day = day; p.Shift = s; p.Block = r.Name; res.Add(p); }
                }
            }
            return res;
        }

        public static List<string> Warnings(List<PersonShift> list)
        {
            List<string> w = new List<string>();
            List<DateTime> days = new List<DateTime>();
            foreach (PersonShift p in list) if (!days.Contains(p.Day)) days.Add(p.Day);
            foreach (DateTime day in days)
            {
                string ds = day.ToString("ddd dd.MM.", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                bool[] sh = new bool[3]; int cnt = 0;
                for (int s = 0; s < 3; s++)
                {
                    List<string> blocks = new List<string>();
                    foreach (PersonShift p in list) if (p.Day == day && p.Shift == s) blocks.Add(p.Block);
                    if (blocks.Count > 0) { sh[s] = true; cnt++; }
                    if (blocks.Count > 1) w.Add(ds + ": in zwei Blöcken derselben Schicht " + ShiftNames[s] + " (" + string.Join(", ", blocks.ToArray()) + ")");
                }
                if (cnt > 1)
                {
                    List<string> n = new List<string>();
                    for (int s = 0; s < 3; s++) if (sh[s]) n.Add(ShiftNames[s]);
                    w.Add(ds + ": in mehreren Schichten (" + string.Join(" + ", n.ToArray()) + ")");
                }
                bool nightBefore = list.Exists(delegate(PersonShift p) { return p.Day == day.AddDays(-1) && p.Shift == 2; });
                if (sh[0] && nightBefore) w.Add(ds + ": F-Schicht direkt nach N-Schicht vom Vortag");
            }
            return w;
        }
    }
}
