using System;
using System.Collections.Generic;

// Pflichtpunkte (Haken je Block, z. B. LKZ, Tagesprobe, Wochen-WFI) und Rundgaenge (Wochenhaken).
// Auswertung rein ueber IDataStore.Get; "heute" wird uebergeben (testbar).
namespace Arbeitsplan.Core
{
    public enum CheckRule { IfOccupied, WeeklyOnce, Workdays }

    // Pflichtpunkt einer Zeile. Key "LKZ" = Entry.Lkz, sonst Eintrag in Entry.Marks.
    // Cond: Komma-Liste; "!v" = entfaellt bei Variante v (Tag; bei WeeklyOnce irgendein Tag der KW), "+v" = bei Variante v automatisch erfuellt.
    // Weekly (nur IfOccupied): einmal je ISO-KW noetig; leere Tage ohne Haken sind dann "nicht noetig" bzw. "Woche offen".
    public class CheckDef
    {
        public const string LkzKey = "LKZ";
        public string Key = "", Name = "", Short = "", Cond = "";
        public int RowId;
        public CheckRule Rule;
        public bool Weekly;

        public CheckDef Clone() { return (CheckDef)MemberwiseClone(); }

        public static CheckDef Lkz(int rowId)
        {
            CheckDef d = new CheckDef(); d.Key = LkzKey; d.Name = "LKZ gemessen"; d.Short = "LKZ"; d.RowId = rowId; d.Rule = CheckRule.IfOccupied; d.Weekly = true;
            return d;
        }
    }

    public class RoundDef
    {
        public string Key = "", Name = "", Short = "";
        public bool Active = true;
        public RoundDef Clone() { return (RoundDef)MemberwiseClone(); }
    }

    public class DutyConfig
    {
        public List<CheckDef> Defs = new List<CheckDef>();
        public List<RoundDef> Rounds = new List<RoundDef>();
        public string Land = "BW";                                  // "BW" oder "BY"
        public bool BufMade;                                        // N2: Puffer "hergestellt" abhaken; Tages-/Wochenprobe folgt daraus (Standard aus, XML puffer-fertig)
        public List<DateTime> ExtraHolidays = new List<DateTime>(), RemovedHolidays = new List<DateTime>();

        public DutyConfig Clone()
        {
            DutyConfig c = new DutyConfig(); c.Land = Land; c.BufMade = BufMade;
            foreach (CheckDef d in Defs) c.Defs.Add(d.Clone());
            foreach (RoundDef r in Rounds) c.Rounds.Add(r.Clone());
            c.ExtraHolidays.AddRange(ExtraHolidays); c.RemovedHolidays.AddRange(RemovedHolidays);
            return c;
        }

        public static DutyConfig Default()
        {
            DutyConfig c = new DutyConfig();
            string[][] r = { new[] { "GMP1", "1. GMP-Rundgang", "GMP1" }, new[] { "GMP2", "2. GMP-Rundgang", "GMP2" }, new[] { "KUEHL", "Kühlraumkontrolle", "KR" },
                             new[] { "EHS", "EHS-Rundgang", "EHS" }, new[] { "WFI", "WFI-Probenahme", "WFI" } };
            foreach (string[] x in r) { RoundDef d = new RoundDef(); d.Key = x[0]; d.Name = x[1]; d.Short = x[2]; c.Rounds.Add(d); }
            return c;
        }

        // Standard fuer die Zeile "Puffer" (neue Datenbestaende): Tagesprobe werktags (mobil = mit Herstellung erledigt),
        // Wochenprobe (WFI) einmal je KW: erledigt durch stationaere Herstellung in der KW (Cond "!stationaer"), sonst manuell abhaken.
        public void AddPufferDefaults(int rowId)
        {
            CheckDef p = new CheckDef(); p.Key = "PROBE"; p.Name = "Tagesprobe (täglich)"; p.Short = "Tagesprobe"; p.RowId = rowId; p.Rule = CheckRule.Workdays; p.Cond = "+mobil";
            CheckDef w = new CheckDef(); w.Key = "WFIW"; w.Name = "Manuelle Probe (wöchentlich)"; w.Short = "Probe wöchentl."; w.RowId = rowId; w.Rule = CheckRule.WeeklyOnce; w.Cond = "!stationaer";
            Defs.Add(p); Defs.Add(w);
        }
    }

    // Nachruesten fuer alte Bestaende (Zeile "Puffer" ohne Pflichtpunkte/Varianten): nur nach Rueckfrage, nur mit PackageAdmin,
    // ergaenzt nur Fehlendes (vorhandene Pflichtpunkte und Varianten bleiben unveraendert).
    public static class PufferSetup
    {
        public const string RowName = "Puffer", Variants = "mobil|stationaer";

        public static RowInfo Row(List<RowInfo> rows)
        {
            foreach (RowInfo r in rows) if (string.Equals(r.Name.Trim(), RowName, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        // Standard-Pflichtpunkte, deren Schluessel es noch nicht gibt
        static List<CheckDef> MissingDefs(DutyConfig cfg, int rowId)
        {
            DutyConfig t = new DutyConfig(); t.AddPufferDefaults(rowId);
            return t.Defs.FindAll(delegate(CheckDef d) { return !cfg.Defs.Exists(delegate(CheckDef x) { return x.Key == d.Key; }); });
        }

        public static bool Needed(List<RowInfo> rows, DutyConfig cfg)
        {
            RowInfo r = Row(rows);
            return r != null && (r.Variants.Length == 0 || MissingDefs(cfg, r.Id).Count > 0);
        }

        // Richtet im Bestand ein, was fehlt; false = nichts zu tun oder kein Recht. Exception = nicht speicherbar.
        public static bool Apply(IDataStore s, User u)
        {
            if (u == null || !u.Has(Perm.PackageAdmin)) return false;
            List<RowInfo> rows = s.GetRows();
            DutyConfig cfg = s.GetDuties();
            if (!Needed(rows, cfg)) return false;
            RowInfo r = Row(rows);
            List<CheckDef> add = MissingDefs(cfg, r.Id);
            if (r.Variants.Length == 0) { r.Variants = Variants; s.SaveStructure(rows); }
            if (add.Count > 0) { cfg.Defs.AddRange(add); s.SaveDuties(cfg); }
            return true;
        }
    }

    // Wochenhaken (Rundgang) je ISO-KW; Version wie bei Entry (Konflikterkennung).
    public class WeekMark
    {
        public DateTime Monday;
        public string Key = "";
        public bool Done;
        public int Version;
        public DateTime At;              // wann abgehakt (MinValue = unbekannt, alte Daten)
        public string By = "";           // Kuerzel (sonst Login) dessen, der abgehakt hat
        public WeekMark Clone() { return (WeekMark)MemberwiseClone(); }
    }

    public static class IsoWeek
    {
        public static DateTime Monday(DateTime d) { d = d.Date; return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }
        // ISO 8601: KW des Donnerstags dieser Woche
        public static int Number(DateTime d) { return (Monday(d).AddDays(3).DayOfYear - 1) / 7 + 1; }
    }

    public static class Holidays
    {
        // Ostersonntag (Gauss, Form nach Meeus)
        public static DateTime Easter(int y)
        {
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int n = h + l - 7 * m + 114;
            return new DateTime(y, n / 31, n % 31 + 1);
        }

        public static bool Is(DateTime day, DutyConfig cfg)
        {
            day = day.Date;
            if (cfg != null && cfg.RemovedHolidays.Contains(day)) return false;
            if (cfg != null && cfg.ExtraHolidays.Contains(day)) return true;
            int o = (day - Easter(day.Year)).Days;
            if (o == -2 || o == 1 || o == 39 || o == 50 || o == 60) return true;
            int md = day.Month * 100 + day.Day;
            if (md == 101 || md == 106 || md == 501 || md == 1003 || md == 1101 || md == 1225 || md == 1226) return true;
            return md == 815 && cfg != null && cfg.Land == "BY";
        }
    }

    // VariantSet (Herstellungsvarianten mit Anzahl): Variants.cs

    public enum DayState { NA, Done, Auto, Missing, Due, NotNeeded, WeekOpen }

    public static class CheckEval
    {
        static Entry At(IDataStore s, DateTime day, int row) { return s.Get(day, row) ?? new Entry(); }

        public static bool IsSet(Entry e, CheckDef d) { return d.Key == CheckDef.LkzKey ? e.Lkz : e.Marks.Contains(d.Key); }

        // Variante v mit Praefix p ("!" oder "+") in Cond?
        public static bool Cond(CheckDef d, char p, string v)
        {
            foreach (string t in d.Cond.Split(','))
            {
                string x = t.Trim();
                if (x.Length > 1 && x[0] == p && string.Equals(x.Substring(1), v, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Entfaellt ("!"): irgendeine Variante des Tages traegt die Bedingung (z. B. stationaer, auch zusammen mit mobil)
        static bool Has(CheckDef d, char p, string variant)
        {
            foreach (string v in VariantSet.Of(variant)) if (Cond(d, p, v)) return true;
            return false;
        }

        // Automatisch ("+"): ALLE Varianten des Tages sind automatisch (mobil allein ja, mobil + stationaer nein)
        static bool Auto(CheckDef d, string variant)
        {
            List<string> l = VariantSet.Of(variant);
            if (l.Count == 0) return false;
            foreach (string v in l) if (!Cond(d, '+', v)) return false;
            return true;
        }

        // "+": automatisch erfuellt; mit BufMade nur, wenn ein Puffer dieses Typs hergestellt ist (EIN mobiler genuegt, auch neben stationaeren)
        static bool AutoNow(CheckDef d, Entry e, bool made) { return made ? MadeRules.AnyMade(e, d, '+') : Auto(d, e.Types); }

        // Wochenprobe ("!"): erledigt durch Variante in der KW; mit BufMade nur durch einen hergestellten Puffer
        static bool WeekSkip(Entry[] w, CheckDef d, bool made)
        {
            if (!made) return AnyNA(w, d);
            foreach (Entry e in w) if (MadeRules.AnyMade(e, d, '!')) return true;
            return false;
        }

        static Entry[] LoadWeek(IDataStore s, CheckDef d, DateTime monday)
        {
            Entry[] w = new Entry[7];
            for (int i = 0; i < 7; i++) w[i] = At(s, monday.AddDays(i), d.RowId);
            return w;
        }

        static bool AnySet(Entry[] w, CheckDef d) { foreach (Entry e in w) if (IsSet(e, d)) return true; return false; }
        static bool AnyNA(Entry[] w, CheckDef d) { foreach (Entry e in w) if (Has(d, '!', e.Types)) return true; return false; }

        public static DayState Day(IDataStore s, CheckDef d, DateTime day, DateTime today, DutyConfig cfg)
        {
            day = day.Date; today = today.Date;
            DateTime mon = IsoWeek.Monday(day);
            Entry[] w = LoadWeek(s, d, mon);
            Entry e = w[(day - mon).Days];
            bool set = IsSet(e, d);
            DayState open = day <= today ? DayState.Missing : DayState.Due;
            bool made = cfg != null && cfg.BufMade;
            switch (d.Rule)
            {
                case CheckRule.WeeklyOnce:
                    if (WeekSkip(w, d, made)) return DayState.Auto;   // Variante mit "!" (stationaer) in der KW = Wochenprobe damit erledigt (mit BufMade: hergestellt)
                    if (set) return DayState.Done;
                    return AnySet(w, d) ? DayState.NotNeeded : DayState.WeekOpen;
                case CheckRule.Workdays:
                    if (Has(d, '!', e.Types)) return DayState.NA;
                    if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday || Holidays.Is(day, cfg))
                    {   // Wochenende/Feiertag: nur faellig, wenn dort hergestellt wird und nicht alles automatisch ist (z. B. stationaer)
                        if (set) return DayState.Done;
                        if (e.Types.Length == 0) return DayState.NA;
                        return AutoNow(d, e, made) ? DayState.Auto : open;
                    }
                    if (AutoNow(d, e, made)) return DayState.Auto;
                    return set ? DayState.Done : open;
                default:   // IfOccupied
                    if (Has(d, '!', e.Types)) return DayState.NA;
                    if (set) return DayState.Done;
                    if (AutoNow(d, e, made)) return DayState.Auto;
                    if (e.HasText) return open;
                    if (!d.Weekly) return DayState.NotNeeded;
                    return AnySet(w, d) ? DayState.NotNeeded : DayState.WeekOpen;
            }
        }

        // Wochenpflicht (Weekly, z. B. LKZ) ohne Haken in der KW, deren Sonntag erreicht ist (<= heute): dringend
        public static bool Overdue(IDataStore s, CheckDef d, DateTime anyDay, DateTime today)
        {
            DateTime mon = IsoWeek.Monday(anyDay);
            if (!d.Weekly || mon.AddDays(6) > today.Date) return false;
            Entry[] w = LoadWeek(s, d, mon);
            return !AnySet(w, d) && !AnyNA(w, d);
        }

        // Zusammenfassung der KW: Missing > WeekOpen > Due > Done > NotNeeded > NA.
        // WeeklyOnce ohne Haken: Missing, wenn der Sonntag <= heute, sonst WeekOpen.
        public static DayState Week(IDataStore s, CheckDef d, DateTime anyDay, DateTime today, DutyConfig cfg)
        {
            DateTime mon = IsoWeek.Monday(anyDay);
            if (d.Rule == CheckRule.WeeklyOnce)
            {
                Entry[] w = LoadWeek(s, d, mon);
                if (WeekSkip(w, d, cfg != null && cfg.BufMade)) return DayState.Done;
                if (AnySet(w, d)) return DayState.Done;
                return mon.AddDays(6) <= today.Date ? DayState.Missing : DayState.WeekOpen;
            }
            DayState[] order = { DayState.Missing, DayState.WeekOpen, DayState.Due, DayState.Done, DayState.Auto, DayState.NotNeeded, DayState.NA };
            int best = order.Length - 1;
            for (int i = 0; i < 7; i++)
            {
                DayState x = Day(s, d, mon.AddDays(i), today, cfg);
                best = Math.Min(best, Array.IndexOf(order, x));
            }
            return order[best] == DayState.Auto ? DayState.Done : order[best];
        }
    }
}
