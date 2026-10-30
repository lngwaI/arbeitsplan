using System;
using System.Collections.Generic;
using System.Globalization;

// "Offene Pflichten" (Idee von Claude): was in einer KW (optional der naechsten) noch fehlt oder faellig ist –
// LKZ, Tagesprobe, Wochen-WFI und weitere Pflichtpunkte, Rundgaenge. Rein ueber IDataStore + CheckEval, "heute" wird uebergeben.
namespace Arbeitsplan.Core
{
    public class OpenDuty
    {
        public DateTime Day;                // Tag; bei KW-Eintraegen der Montag
        public bool Week;                   // gilt fuer die ganze KW (kein bestimmter Tag)
        public int RowId = -1;              // -1 = Rundgang
        public int Order;                   // Zeilenposition (Rundgaenge -1)
        public string Where = "", What = "", State = "";   // Raum bzw. "Rundgang", Pflichtpunkt, "fehlt"/"fällig"/"KW offen"
        public bool Overdue;                // fehlt (Tag <= heute bzw. KW vorbei)

        static readonly string[] DayNames = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
        public string When
        {
            get { return Week ? "KW" + IsoWeek.Number(Day) : DayNames[(int)Day.DayOfWeek] + " " + Day.ToString("dd.MM.", CultureInfo.InvariantCulture); }
        }
    }

    public static class OpenDuties
    {
        // Pflichtpunkte einer Zeile; LKZ kommt aus der Blockstruktur (HasLkz), wenn nicht eigens definiert
        public static List<CheckDef> DefsFor(RowInfo r, DutyConfig cfg)
        {
            List<CheckDef> l = cfg.Defs.FindAll(delegate(CheckDef d) { return d.RowId == r.Id; });
            if (r.HasLkz && !l.Exists(delegate(CheckDef d) { return d.Key == CheckDef.LkzKey; })) l.Insert(0, CheckDef.Lkz(r.Id));
            return l;
        }

        // weeks ISO-KWs ab der KW von anyDay; sortiert nach Tag (KW-Eintraege zuerst), dann Zeile
        public static List<OpenDuty> List(IDataStore s, DutyConfig cfg, List<RowInfo> rows, DateTime anyDay, int weeks, DateTime today)
        {
            List<OpenDuty> res = new List<OpenDuty>();
            today = today.Date;
            for (int w = 0; w < weeks; w++)
            {
                DateTime mon = IsoWeek.Monday(anyDay).AddDays(7 * w);
                for (int ri = 0; ri < rows.Count; ri++)
                    foreach (CheckDef d in DefsFor(rows[ri], cfg))
                    {
                        string name = MadeRules.Display(d, cfg.BufMade).Name; if (name.Length == 0) name = d.Short;
                        if (d.Rule == CheckRule.WeeklyOnce)
                        {
                            DayState ws = CheckEval.Week(s, d, mon, today, cfg);
                            if (ws == DayState.Missing || ws == DayState.WeekOpen) res.Add(Item(mon, true, rows[ri], ri, name, ws == DayState.Missing ? "fehlt" : "KW offen", ws == DayState.Missing));
                            continue;
                        }
                        bool any = false, weekOpen = false;
                        for (int i = 0; i < 7; i++)
                        {
                            DayState st = CheckEval.Day(s, d, mon.AddDays(i), today, cfg);
                            if (st == DayState.Missing || st == DayState.Due) { res.Add(Item(mon.AddDays(i), false, rows[ri], ri, name, st == DayState.Missing ? "fehlt" : "fällig", st == DayState.Missing)); any = true; }
                            else if (st == DayState.WeekOpen) weekOpen = true;
                        }
                        if (weekOpen && !any) res.Add(Item(mon, true, rows[ri], ri, name, "KW offen", mon.AddDays(6) < today));
                    }
                List<WeekMark> marks = s.GetWeekMarks(mon);
                foreach (RoundDef r in cfg.Rounds)
                {
                    if (!r.Active || marks.Exists(delegate(WeekMark m) { return m.Key == r.Key && m.Done; })) continue;
                    OpenDuty o = new OpenDuty(); o.Day = mon; o.Week = true; o.Order = -1; o.Where = "Rundgang"; o.What = MadeRules.Display(r, cfg.BufMade).Name;
                    o.Overdue = mon.AddDays(6) < today; o.State = o.Overdue ? "fehlt" : "offen";
                    res.Add(o);
                }
            }
            res.Sort(delegate(OpenDuty a, OpenDuty b)
            {
                int c = a.Day.CompareTo(b.Day);
                if (c == 0) c = b.Week.CompareTo(a.Week);
                if (c == 0) c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.CompareOrdinal(a.What, b.What);
            });
            return res;
        }

        static OpenDuty Item(DateTime day, bool week, RowInfo r, int order, string what, string state, bool overdue)
        {
            OpenDuty o = new OpenDuty(); o.Day = day; o.Week = week; o.RowId = r.Id; o.Order = order;
            o.Where = r.Name.Length > 0 ? r.Name : "(Zeile " + (order + 1) + ")"; o.What = what; o.State = state; o.Overdue = overdue;
            return o;
        }
    }
}
