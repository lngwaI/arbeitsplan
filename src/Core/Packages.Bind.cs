using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbeitsplan.Core
{
    // Frei: eigener Rhythmus (Tage, Wiederholung); JeLauf: folgt den Tagen des Hauptlaufs (Package.Head) plus RunOffset.
    public enum StepBind { Frei, JeLauf }

    // Zeitmodell v7 (Paketformat 7): Bezug zum Hauptlauf, Ausfall einzelner Laeufe, Verschieben ab einem Lauf,
    // Kennung als Start + Ueberschreibungen, Nummer von Hand. Rechnung: Package.Schedule (Packages.Time.cs).
    public partial class PackageStep
    {
        public StepBind Bind;
        public int RunOffset;               // JeLauf: Tage zum Hauptlauf-Tag (-30..30, Puffer -1 = ein Tag vor jedem Lauf)
        public int RunEvery = 1;            // JeLauf: nur jeder n-te stattfindende Lauf (1 = jeder)
        public bool NumberRuns;             // JeLauf, Art Schritt: "Titel 1, 2, 3" (aus alten Paketen); sonst ohne Nummer
        public string ManualNo = "";        // Packen/Einzelpaket: Nummer von Hand (8 Ziffern), Plantext "<Titel> (Nr. …)"
        public List<int> Skip = new List<int>();   // nur Hauptlauf: ausgefallene Laeufe (0-basiert, sortiert); Nummer/Kennung bleiben Luecke
        public Dictionary<int, int> Move = new Dictionary<int, int>();   // Lauf (0-basiert) -> Tage, gilt ab diesem Lauf fuer alle folgenden
        public string IdentStart = "";      // Kennung des ersten Laufs, z. B. "PA 40010001"; Endziffern +1 je geplantem Lauf
        public Dictionary<int, string> IdentOver = new Dictionary<int, string>();   // Lauf (0-basiert) -> Text von Hand; "-" = ausgesetzt
        public const string IdentOff = "-";
        public const int MaxOffset = 30;

        public static bool IsManualNo(string s)
        {
            if (s == null || s.Length != 8) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        public bool Skips(int run) { return Skip.Contains(run); }

        public void SetSkip(int run, bool on)
        {
            if (run < 0 || run >= MaxRuns) return;
            if (on && !Skip.Contains(run)) { Skip.Add(run); Skip.Sort(); }
            else if (!on) Skip.Remove(run);
        }

        // Kennung eines Laufs (Index des Hauptlaufs): Ueberschreibung > Start + Lauf > alte Kommaliste. "" = keine.
        public string IdentFor(int run)
        {
            string o;
            if (IdentOver.TryGetValue(run, out o)) return o == IdentOff ? "" : o;
            if (IdentStart.Length > 0) return IdentAt(IdentStart, run);
            string[] per = Ident.Split(',');
            return run < per.Length ? per[run].Trim() : "";
        }

        // Endziffern des Texts + run, Stellenzahl bleibt ("PA 40010001", 2 -> "PA 40010003"); ohne Endziffern unveraendert.
        public static string IdentAt(string start, int run)
        {
            start = (start ?? "").Trim();
            if (start.Length == 0 || run == 0) return start;
            int b = start.Length;
            while (b > 0 && start[b - 1] >= '0' && start[b - 1] <= '9') b--;
            int w = start.Length - b;
            long n;
            if (w == 0 || w > 17 || !long.TryParse(start.Substring(b), NumberStyles.None, CultureInfo.InvariantCulture, out n)) return start;
            return start.Substring(0, b) + (n + run).ToString("D" + w, CultureInfo.InvariantCulture);
        }

        // Kennungen als Kommaliste fuer die bisherige Eingabezeile (Start/Ueberschreibungen aufgeloest); runs = Zahl der Laeufe.
        public string IdentList(int runs)
        {
            if (IdentStart.Length == 0 && IdentOver.Count == 0) return Ident;
            List<string> l = new List<string>();
            for (int i = 0; i < Math.Max(1, runs); i++) l.Add(IdentFor(i));
            return string.Join(", ", l.ToArray()).TrimEnd(' ', ',');
        }

        // Alte Kommaliste setzen (loescht Start und Ueberschreibungen)
        public void SetIdentList(string list)
        {
            Ident = (list ?? "").Trim(); IdentStart = ""; IdentOver.Clear();
        }

        public int IdentCount(int runs) { string l = IdentList(runs).Trim(); return l.Length == 0 ? 0 : l.Split(',').Length; }

        // Migration: alte Kommaliste -> Start + Ueberschreibungen (Ergebnis je Lauf bleibt exakt gleich)
        internal void IdentToStart(int runs)
        {
            if (Ident.Trim().Length == 0 || IdentStart.Length > 0 || IdentOver.Count > 0) return;
            string[] per = Ident.Split(',');
            string first = per[0].Trim();
            IdentStart = first;
            for (int i = 0; i < Math.Max(1, runs); i++)
            {
                string old = i < per.Length ? per[i].Trim() : "", ext = first.Length > 0 ? IdentAt(first, i) : "";
                if (old != ext) IdentOver[i] = old.Length > 0 ? old : IdentOff;
            }
            Ident = "";
        }

        // Ganzen Schritt um k Tage nach hinten/vorn schieben, ohne Wochentagsmuster zu drehen (Ursprung wandert: Package.ShiftOrigin)
        internal void ShiftDays(int k, DayOfWeek day1)
        {
            if (Repeat == RepeatMode.Weekdays && (WeekDays & 127) != 0) Day = BaseDays(day1)[0];
            if (Repeat == RepeatMode.DayList)
            {
                List<int> l = ParseDays(DayList);
                for (int i = 0; i < l.Count; i++) l[i] += k;
                DayList = JoinDays(l);
            }
            else { Day += k; if (Until > 0) Until = Math.Max(1, Until + k); }
        }

        // Fruehester roher Tag (vor Versatz) fuer die Pruefung, wie weit der Plan nach vorn geschlossen werden darf
        internal int RawFirst(DayOfWeek day1)
        {
            List<int> b = BaseDays(day1); int m = b[0];
            foreach (int d in b) m = Math.Min(m, d);
            return m;
        }

        internal void CopyBind(PackageStep o)
        {
            Skip = new List<int>(o.Skip);
            Move = new Dictionary<int, int>(o.Move);
            IdentOver = new Dictionary<int, string>(o.IdentOver);
        }
    }
}
