using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    // Wiederholung eines Schritts: alle x Tage (Standard, alte Pakete), an bestimmten Wochentagen oder an bestimmten Tagen der Kampagne.
    public enum RepeatMode { Interval, Weekdays, DayList }

    // Wiederholmodi, Versatz je Lauf (Ausnahme aus dem Kalender) und Ansatz (Puffer mit Tank oder Flasche/Gebinde).
    public partial class PackageStep
    {
        public RepeatMode Repeat;                // XML "wiederholung" (fehlt = alle x Tage)
        public int WeekDays;                     // Bitmaske Mo=1, Di=2, Mi=4, Do=8, Fr=16, Sa=32, So=64
        public string DayList = "";              // Tage der Kampagne, z. B. "1, 4, 9"
        public int Until;                        // > 0: "bis Tag n" statt Anzahl (alle x Tage, Wochentage)
        public Dictionary<int, int> Shift = new Dictionary<int, int>();   // Lauf (0-basiert) -> Tage Versatz
        public const int MaxRuns = 99;
        public static readonly string[] DayNames = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };

        // Ansatz (fest im Code, siehe AnsatzFor): "" = Tank (wie bisher), "gebinde" = Flasche/Gebinde mit Bezeichnung + Menge + Einheit (kein Tank
        // noetig). Gilt fuer Puffer-Schritte (UsesAnsatz). Das fruehere "ohne" (ohne Gebinde) wird beim Laden zu "gebinde".
        public string Ansatz = "", Gebinde = "", Menge = "", Einheit = "";
        public const string AnsatzGebinde = "gebinde";

        // Moegliche Ansaetze je Puffer-Typ, Schluessel -> Anzeige: mobil = Mobiler Tank | Flasche/Gebinde; stationaer = nur stationaerer Tank (keine Wahl);
        // sonst (alter Puffer ohne eindeutigen Typ) Tank | Flasche/Gebinde
        public static List<KeyValuePair<string, string>> AnsatzFor(string typ)
        {
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            l.Add(new KeyValuePair<string, string>("", typ == "mobil" ? "Mobiler Tank" : (typ == "stationaer" ? "Stationärer Tank" : "Tank (Vorlagetank/WFI-Tank)")));
            if (typ != "stationaer") l.Add(new KeyValuePair<string, string>(AnsatzGebinde, "Flasche/Gebinde"));
            return l;
        }

        // Puffer-Typ des Schritts, wenn er eindeutig ist; sonst ""
        public string AnsatzTyp
        {
            get
            {
                List<string> v = VariantSet.Of(Variant);
                if (Puffers.Count > 0) { v = new List<string>(); foreach (PufferSpec p in Puffers) if (!v.Contains(p.Typ)) v.Add(p.Typ); }
                return v.Count == 1 ? v[0] : "";
            }
        }

        // Auswahl fuer diesen Schritt: alte stationaere Schritte mit Gebinde behalten ihren Eintrag, bis er bewusst geaendert wird
        public List<KeyValuePair<string, string>> AnsatzChoices()
        {
            List<KeyValuePair<string, string>> l = AnsatzFor(AnsatzTyp);
            if (Ansatz.Length > 0 && !l.Exists(delegate(KeyValuePair<string, string> k) { return k.Key == Ansatz; })) l.Add(new KeyValuePair<string, string>(Ansatz, "Flasche/Gebinde"));
            return l;
        }

        static int Bit(int dayIndex) { return 1 << (((dayIndex % 7) + 7) % 7); }   // 0 = Mo
        static int MonIndex(DayOfWeek d) { return ((int)d + 6) % 7; }

        // Tage (1 = Starttag) aller Laeufe ohne Versatz; day1 = Wochentag von Tag 1
        internal List<int> BaseDays(DayOfWeek day1)
        {
            List<int> r = new List<int>();
            int cap = Until > 0 ? MaxRuns : Math.Max(1, Math.Min(MaxRuns, Count));
            if (Repeat == RepeatMode.DayList) r = ParseDays(DayList);
            else if (Repeat == RepeatMode.Weekdays && (WeekDays & 127) != 0)
            {
                for (int d = Math.Max(1, Day); r.Count < cap && d < Day + 7 * MaxRuns && (Until <= 0 || d <= Until); d++)
                    if ((WeekDays & Bit(MonIndex(day1) + d - 1)) != 0) r.Add(d);
            }
            else
            {
                int iv = Math.Max(1, Interval);
                for (int i = 0; i < cap; i++) { int d = Day + i * iv; if (Until > 0 && d > Until && i > 0) break; r.Add(d); }
            }
            if (r.Count == 0) r.Add(Day);
            if (r.Count > MaxRuns) r.RemoveRange(MaxRuns, r.Count - MaxRuns);
            return r;
        }

        // Tag je Lauf mit Versatz (nie vor Tag 1): Move gilt ab dem Lauf fuer alle folgenden, Shift nur fuer den einen Lauf
        public List<int> Days(DayOfWeek day1 = DayOfWeek.Monday)
        {
            List<int> r = BaseDays(day1);
            int cum = 0;
            for (int i = 0; i < r.Count; i++)
            {
                int m, s; if (Move.TryGetValue(i, out m)) cum += m;
                Shift.TryGetValue(i, out s);
                if (cum + s != 0) r[i] = Math.Max(1, r[i] + cum + s);
            }
            return r;
        }

        // Zahl der Wiederholungen; setzen = "nach n Laeufen" (Ende per Anzahl, kein "bis Tag"); Tagesliste wird gekuerzt/verlaengert
        public int RunCount
        {
            get { return BaseDays(DayOfWeek.Monday).Count; }
            set
            {
                value = Math.Max(1, Math.Min(MaxRuns, value));
                Count = value; Until = 0;
                if (Repeat != RepeatMode.DayList) return;
                List<int> l = ParseDays(DayList);
                if (l.Count == 0) return;
                int gap = l.Count > 1 ? Math.Max(1, l[l.Count - 1] - l[l.Count - 2]) : 1;
                while (l.Count < value) l.Add(l[l.Count - 1] + gap);
                if (l.Count > value) l.RemoveRange(value, l.Count - value);
                DayList = JoinDays(l);
            }
        }
        public int RunCountFor(DayOfWeek day1) { return BaseDays(day1).Count; }
        public int FirstDay { get { List<int> d = Days(); int m = d[0]; foreach (int x in d) m = Math.Min(m, x); return m; } }

        public static List<int> ParseDays(string s)
        {
            List<int> r = new List<int>();
            foreach (string t in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int d;
                if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d) && d >= 1 && d <= 999 && !r.Contains(d)) r.Add(d);
            }
            r.Sort();
            return r;
        }

        internal static string JoinDays(List<int> l) { List<string> s = new List<string>(); foreach (int d in l) s.Add(d.ToString(CultureInfo.InvariantCulture)); return string.Join(", ", s.ToArray()); }

        // Ganzen Schritt um delta Tage verschieben (alle Laeufe, Ausnahmen bleiben relativ). false = wuerde vor Tag 1 fallen.
        public bool MoveAll(int delta, DayOfWeek day1 = DayOfWeek.Monday)
        {
            if (delta == 0) return false;
            foreach (int d in Days(day1)) if (d + delta < 1) return false;
            if (Repeat == RepeatMode.DayList)
            {
                List<int> l = ParseDays(DayList);
                for (int i = 0; i < l.Count; i++) l[i] += delta;
                DayList = JoinDays(l);
            }
            else if (Repeat == RepeatMode.Weekdays && (WeekDays & 127) != 0)
            {   // Muster wandert mit (Mo/Mi/Fr + 1 Tag = Di/Do/Sa); Tag auf den ersten Lauf setzen, damit er nicht vor Tag 1 rutscht
                Day = BaseDays(day1)[0];
                int m = 0;
                for (int i = 0; i < 7; i++) if ((WeekDays & (1 << i)) != 0) m |= Bit(i + delta);
                WeekDays = m;
            }
            if (Repeat != RepeatMode.DayList) { Day = Math.Max(1, Day + delta); if (Until > 0) Until = Math.Max(1, Until + delta); }
            return true;
        }

        // Nur einen Lauf verschieben (Ausnahme "Tag-Versatz je Lauf"); bei nur einem Lauf = ganzer Schritt. false = vor Tag 1.
        public bool MoveRun(int run, int delta, DayOfWeek day1 = DayOfWeek.Monday)
        {
            List<int> d = Days(day1);
            if (delta == 0 || run < 0 || run >= d.Count || d[run] + delta < 1) return false;
            if (d.Count == 1) return MoveAll(delta, day1);
            int s; Shift.TryGetValue(run, out s); s += delta;
            if (s == 0) Shift.Remove(run); else Shift[run] = s;
            return true;
        }

        public bool Shifted(int run) { return Shift.ContainsKey(run); }

        // Kurzfassung, z. B. "4× alle 6 Tage", "Mo, Mi, Fr bis Tag 14", "Tage 1, 4, 9", "einmal"
#if TESTHOOKS
        public string RepeatText()
        {
            string end = Until > 0 ? " bis Tag " + Until : " (" + RunCount + "×)";
            string t;
            if (Repeat == RepeatMode.DayList) t = "Tage " + JoinDays(ParseDays(DayList));
            else if (Repeat == RepeatMode.Weekdays) t = (WeekDays & 127) == 0 ? "Wochentage (keine gewählt)" : WeekDayText(WeekDays).Replace(",", ", ") + end;
            else if (RunCount <= 1) t = "einmal";
            else t = Until > 0 ? "alle " + Math.Max(1, Interval) + (Interval == 1 ? " Tag" : " Tage") + end : RunCount + "× alle " + Math.Max(1, Interval) + (Interval == 1 ? " Tag" : " Tage");
            return Shift.Count > 0 ? t + " · " + Shift.Count + (Shift.Count == 1 ? " Ausnahme" : " Ausnahmen") : t;
        }
#endif

        // ---------- Ansatz ----------

        public bool UsesAnsatz { get { return Ansatz.Length > 0 || Source == PufferSetup.RowName || Row == PufferSetup.RowName; } }

        // Plantext-Zeile: "Gebinde: Flasche 5 L"; Tank = "" (Equipment-Zeilen wie bisher)
        public string AnsatzLine()
        {
            if (Ansatz.Length == 0) return "";
            string g = (Gebinde.Trim() + " " + (Menge.Trim() + " " + Einheit.Trim()).Trim()).Trim();
            return "Gebinde: " + (g.Length > 0 ? g : "Flasche");
        }

        // Tank-Positionen (Positionsname enthaelt "tank") ohne Geraet; nur bei Puffer-Schritten mit Ansatz Tank, sonst leer (kein Hinweis)
        public List<string> MissingTanks(Catalog cat)
        {
            List<string> r = new List<string>();
            if (!UsesAnsatz || Ansatz.Length > 0) return r;
            foreach (EquipAssign a in Equipment)
                if (IsTank(cat, a.Short) && a.ChoiceFor(0) == null && !r.Contains(a.Short)) r.Add(a.Short);
            return r;
        }

        public static bool IsTank(Catalog cat, string shortName)
        {
            EquipPosition p = cat != null ? cat.Position(shortName) : null;
            return p != null && p.Name.IndexOf("tank", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------- Datei (Packages.Xml.cs) ----------

        public static string WeekDayText(int mask)
        {
            List<string> l = new List<string>();
            for (int i = 0; i < 7; i++) if ((mask & (1 << i)) != 0) l.Add(DayNames[i]);
            return string.Join(",", l.ToArray());
        }

        public static int ParseWeekDays(string s)
        {
            int m = 0;
            foreach (string t in (s ?? "").Split(','))
            {
                int i = Array.IndexOf(DayNames, t.Trim());
                if (i >= 0) m |= 1 << i;
            }
            return m;
        }

        public string ShiftText() { return MapText(Shift); }

        public void ParseShift(string s) { ParseMap(s, Shift); }

        // "Lauf:Tage;Lauf:Tage" (Lauf 0-basiert), fuer Shift und Move
        public static string MapText(Dictionary<int, int> map)
        {
            List<int> keys = new List<int>(map.Keys); keys.Sort();
            StringBuilder sb = new StringBuilder();
            foreach (int k in keys) sb.Append(sb.Length > 0 ? ";" : "").Append(k.ToString(CultureInfo.InvariantCulture)).Append(':').Append(map[k].ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static void ParseMap(string s, Dictionary<int, int> map)
        {
            map.Clear();
            foreach (string t in (s ?? "").Split(';'))
            {
                string[] kv = t.Split(':'); int k, v;
                if (kv.Length == 2 && int.TryParse(kv[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)
                    && k >= 0 && k < MaxRuns && v != 0 && Math.Abs(v) <= 366) map[k] = v;
            }
        }
    }

    // Reihenfolge im Paket-Editor: nach erstem Tag; am selben Tag gilt die vom Nutzer gesetzte Reihenfolge (Index in der Schrittliste).
    // Verschieben ueber eine Tagesgrenze uebernimmt den Tag des Ziel-Nachbarn; die Anzeige springt dadurch nie um.
    public static class StepOrder
    {
        public static List<PackageStep> Sorted(List<PackageStep> steps)
        {
            List<PackageStep> o = new List<PackageStep>(steps);
            Dictionary<PackageStep, int> first = new Dictionary<PackageStep, int>();
            foreach (PackageStep s in steps) first[s] = s.FirstDay;
            o.Sort(delegate(PackageStep a, PackageStep b) { return first[a] != first[b] ? first[a].CompareTo(first[b]) : steps.IndexOf(a).CompareTo(steps.IndexOf(b)); });
            return o;
        }

        // Schritt s an Position "to" der sortierten Liste bringen (Pfeile, Ziehen, auch ans Ende). false = nichts geaendert.
        public static bool MoveTo(List<PackageStep> steps, PackageStep s, int to)
        {
            List<PackageStep> o = Sorted(steps);
            int from = o.IndexOf(s);
            if (from < 0 || o.Count < 2) return false;
            to = Math.Max(0, Math.Min(o.Count - 1, to));
            if (to == from) return false;
            PackageStep n = o[to];
            int delta = n.FirstDay - s.FirstDay;
            if (delta != 0 && !s.MoveAll(delta)) return false;
            steps.Remove(s);
            int k = steps.IndexOf(n);
            steps.Insert(from < to ? k + 1 : k, s);
            return true;
        }
    }
}
