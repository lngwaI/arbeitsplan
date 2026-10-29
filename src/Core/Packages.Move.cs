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

        // Fruehester Termin = Tag 1: rutscht ein JeLauf-Schritt (Versatz -1) oder ein Schritt davor, werden alle Schritte nach hinten geschoben;
        // fehlt vorn eine Luecke, wird sie geschlossen (soweit die rohen Tage der Schritte es zulassen). true = geaendert.
        public bool Normalize()
        {
            int min = int.MaxValue;
            foreach (RunDate d in Schedule(null)) min = Math.Min(min, d.Day);
            if (min == int.MaxValue || min == 1) return false;
            int k = 1 - min;
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
    }
}
