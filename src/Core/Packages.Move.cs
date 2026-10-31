using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Tag 1 ist immer der fruehste Termin des Pakets; Schritte vor Lauf 1 (Packen, Puffer) schieben den Ursprung nach vorn.
    // Wochentagsmuster bleiben dabei an ihren Wochentagen (Day1 wandert mit dem Ursprung).
    public partial class Package
    {
        // Ursprung um k Tage verschieben: alle Tage +k (k < 0 = Luecke vorn schliessen), Day1 wandert gegenlaeufig. Datum von Lauf 1 bleibt.
        internal void ShiftOrigin(int k)
        {
            if (k == 0) return;
            foreach (PackageStep s in Steps) s.ShiftDays(k, Day1);
            Day1 = (DayOfWeek)((((int)Day1 - k) % 7 + 7) % 7);
        }

        public const string Day1Hint = "Lauf 1 liegt nicht auf einem gewählten Wochentag";

        // Wochentag von Tag 1 so setzen, dass Lauf 1 am Datum lauf1 liegt (Wochentagsmuster bleiben auf echten Wochentagen).
        // Liegt auf dem Datum kein Lauf (Muster ohne diesen Wochentag), wird der naechste passende Tag gesucht und zurueckgegeben
        // (Day1Hint zeigen); sonst lauf1 unveraendert.
        public DateTime SyncDay1(DateTime lauf1)
        {
            lauf1 = lauf1.Date;
            if (Day1Converges(lauf1)) return lauf1;
            DayOfWeek keep = Day1;
            for (int k = 1; k <= 7; k++) if (Day1Converges(lauf1.AddDays(k))) return lauf1.AddDays(k);
            Day1 = keep;
            return lauf1;
        }

        bool Day1Converges(DateTime l1)
        {
            for (int k = 0; k < 8; k++)
            {
                DayOfWeek d = StartFor(l1).DayOfWeek;
                if (Day1 == d) return true;
                Day1 = d;
            }
            return false;
        }

        // Fruehester Termin = Tag 1: rutscht ein JeLauf-Schritt (Versatz -1) oder ein Schritt davor, werden alle Schritte nach hinten geschoben;
        // fehlt vorn eine Luecke, wird sie geschlossen (soweit die rohen Tage der Schritte es zulassen). true = geaendert.
        // closeGap = false: nur Termine vor Tag 1 korrigieren, eine Luecke vorn bleibt (Ziehen im Kalender soll nicht zurueckspringen)
        public bool Normalize(bool closeGap = true)
        {
            int min = int.MaxValue;
            foreach (RunDate d in Schedule(null))   // N7: Puffer-Karten mit eigenem Versatz liegen frueher als der Termin des Schritts
                min = Math.Min(min, d.Day + (d.Step >= 0 && d.Step < Steps.Count ? MinCardDelta(Steps[d.Step], Day1) : 0));
            if (min == int.MaxValue || min == 1) return false;
            int k = 1 - min;
            if (k < 0 && !closeGap) return false;
            if (k < 0)
            {
                int slack = int.MaxValue;
                PackageStep h = Head;
                foreach (PackageStep s in Steps)
                    if (!s.IsEmpty && (s.Bind == StepBind.Frei || h == null)) slack = Math.Min(slack, s.RawFirst(Day1) - 1);
                k = -Math.Max(0, Math.Min(-k, slack == int.MaxValue ? 0 : slack));
            }
            if (k == 0) return false;
            ShiftOrigin(k);
            return true;
        }

        // Schritt um delta Tage verschieben, auch vor Tag 1 (dann wandern alle anderen nach hinten). JeLauf: Versatz zum Hauptlauf.
        // false = nicht moeglich (Versatz > 30 Tage).
        public bool MoveStep(PackageStep s, int delta)
        {
            if (delta == 0) return false;
            if (s.Bind == StepBind.JeLauf && Head != null && s != Head)
            {
                int o = s.RunOffset + delta;
                if (Math.Abs(o) > PackageStep.MaxOffset) return false;
                foreach (int v in s.Shift.Values) if (Math.Abs(o + v) > PackageStep.MaxOffset) return false;   // auch der Abstand je Lauf
                s.RunOffset = o; Normalize(); return true;
            }
            List<int> d = s.Days(Day1);
            int first = d[0];
            foreach (int x in d) first = Math.Min(first, x);
            if (first + delta < 1) ShiftOrigin(1 - (first + delta));
            bool ok = s.MoveAll(delta, Day1);
            Normalize();
            return ok;
        }

        // Kalender: Lauf "run" von s um delta Tage ziehen. Standard = dieser und alle folgenden Laeufe (Move ab diesem Lauf; beim ersten Lauf
        // der ganze Schritt), onlyRun = nur dieser Lauf (Shift). JeLauf-Schritte folgen dem Hauptlauf: sonst der Versatz zum Lauf, einzelne Laeufe
        // mit eigenem Abstand zu diesem Lauf. false = nicht moeglich (vor Tag 1, Versatz zu gross).
        public bool DragRun(PackageStep s, int run, int delta, bool onlyRun)
        {
            if (!DragRunRaw(s, run, delta, onlyRun)) return false;
            Normalize(false);   // JeLauf-Schritte mit negativem Versatz (Puffer -1) duerfen nie vor Tag 1 landen
            return true;
        }

        bool DragRunRaw(PackageStep s, int run, int delta, bool onlyRun)
        {
            if (delta == 0) return false;
            if (s.Bind == StepBind.JeLauf && Head != null && s != Head)
            {
                if (!onlyRun) return MoveStep(s, delta);
                if (run < 0 || run >= Head.Days(Day1).Count) return false;
                int v; s.Shift.TryGetValue(run, out v); v += delta;
                if (Math.Abs(s.RunOffset + v) > PackageStep.MaxOffset) return false;
                if (v == 0) s.Shift.Remove(run); else s.Shift[run] = v;
                return true;
            }
            List<int> d = s.Days(Day1);
            if (run < 0 || run >= d.Count) return false;
            if (onlyRun) return s.MoveRun(run, delta, Day1);
            if (run > 0 && d.Count > 1) return s.MoveFrom(run, delta, Day1);
            int first = d[0];
            foreach (int x in d) first = Math.Min(first, x);
            if (first + delta < 1) ShiftOrigin(1 - (first + delta));
            return s.MoveAll(delta, Day1);
        }
    }

    public partial class PackageStep
    {
        // Ab Lauf "run" (0-basiert) alle folgenden Laeufe um delta Tage verschieben (Move, XML "versatzab"); der erste Lauf = ganzer Schritt.
        // Der Lauf gilt danach als fest (Pinned). false = vor Tag 1 oder Versatz zu gross.
        public bool MoveFrom(int run, int delta, DayOfWeek day1 = DayOfWeek.Monday)
        {
            List<int> d = Days(day1);
            if (delta == 0 || run < 0 || run >= d.Count) return false;
            if (run == 0) return MoveAll(delta, day1);
            for (int i = run; i < d.Count; i++) if (d[i] + delta < 1) return false;
            int m; Move.TryGetValue(run, out m); m += delta;
            if (Math.Abs(m) > 366) return false;
            if (m == 0) Move.Remove(run); else Move[run] = m;
            return true;
        }

        // Lauf ist von Hand verschoben (Ausnahme oder Move-Ansatz): im Kalender mit Schloss, bleibt bei Regelaenderungen an seinem Tag
        public bool Pinned(int run) { return Shift.ContainsKey(run) || Move.ContainsKey(run); }

        // Zurueck auf die Regel: Move/Shift dieses Laufs entfernen. false = war nicht verschoben.
        public bool ResetRun(int run) { bool a = Shift.Remove(run), b = Move.Remove(run); return a || b; }

        public bool ResetMoves() { bool any = Shift.Count + Move.Count > 0; Shift.Clear(); Move.Clear(); return any; }
    }
}
