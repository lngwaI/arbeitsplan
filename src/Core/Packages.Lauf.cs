using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // UX-5b: zu welchem Lauf gehoert ein Termin (fuer "Puffer 2: Name · Lauf 3")? Quelle ist der Terminplan (Schedule() bzw. der Plan des
    // Kampagnenstarts), die Nummer ist die Wiederholung des Hauptlaufs + 1; ausgefallene Laeufe lassen eine Luecke.
    public partial class Package
    {
        public int LaufOf(List<RunDate> plan, RunDate rd) { return LaufOf(plan, rd, Day1); }

        // 0-basierter Lauf, den der Termin rd bedient; -1 = kein Bezug (kein Hauptlauf, oder der Termin liegt nach dem letzten Lauf).
        // Hauptlauf-Schritt und Schritte "zu jedem Lauf": der eigene Lauf. Sonst der naechste Lauf am oder nach dem Tag des Termins.
        public int LaufOf(List<RunDate> plan, RunDate rd, DayOfWeek day1)
        {
            int main = HeadIndex(day1);
            if (main < 0 || rd.Step < 0 || rd.Step >= Steps.Count) return -1;
            if (rd.Step == main || Steps[rd.Step].Bind == StepBind.JeLauf) return rd.Run;
            int best = -1, bestDay = int.MaxValue;
            foreach (RunDate m in plan)
                if (m.Step == main && m.Day >= rd.Day && m.Day < bestDay) { best = m.Run; bestDay = m.Day; }
            return best;
        }
    }
}
