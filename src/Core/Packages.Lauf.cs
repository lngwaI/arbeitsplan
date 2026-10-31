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

        // Volle Laufnummer (8 Ziffern) des bedienten Laufs (lauf = Nummer im Hauptlauf, 1 = erster); "" = unbekannt (kein Praefix, kein Lauf)
        public string LaufNoOf(int lauf, DayOfWeek day1)
        {
            int main = HeadIndex(day1);
            if (main < 0 || lauf < 1 || !IsCode4(Prefix)) return "";
            string no = FormatRun(Prefix, Steps[main].StartNo + lauf - 1);
            return no.Length == 8 ? no : "";
        }

        // Vorgang aus der Freeze-Vorlage am Tag day: Uhrzeit aus der Vorgabe, sonst offen; Hauptlauf = bedienter Lauf (lauf, 1 = erster)
        public FreezeItem FreezeFor(PackageStep s, int run, int lauf, DateTime day, DayOfWeek day1)
        {
            FreezeItem f = s.Freeze.Clone();
            f.TimeOpen = s.Freeze.Start1to3[0] == DateTime.MinValue;
            f.Start1to3[0] = f.TimeOpen ? day.Date : day.Date + s.Freeze.Start1to3[0].TimeOfDay;
            f.MainRef = LaufNoOf(lauf, day1); f.SubNo = s.SubNoFor(run, Prefix);
            return f;
        }

        // Was neben dem Vorgang als Text im Block steht (Text, Equipment, Notizen): der Titel steckt schon im Vorgang
        public Entry FreezeRest(PackageStep s, int run)
        {
            PackageStep c = s.Clone(); c.Title = "";
            return c.EntryFor(run, Prefix);
        }

        // Puffer dem bedienten Lauf zuordnen (Plan und Vorschau)
        public List<BufferItem> WithLauf(List<BufferItem> l, int lauf, DayOfWeek day1)
        {
            string no = LaufNoOf(lauf, day1);
            foreach (BufferItem b in l) { b.LaufRef = Math.Max(0, lauf); b.LaufNo = no; }
            return l;
        }
    }
}
