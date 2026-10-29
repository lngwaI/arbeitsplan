using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arbeitsplan.Core
{
    public enum PackageType { Kampagne, Einzel, Ereignis }

    // Zeitmodell (Paketformat 7): ein Hauptlauf (Head) gibt den Takt vor, JeLauf-Schritte folgen ihm, freie Schritte haben ein eigenes
    // Raster. Schedule() ist die einzige Rechenquelle fuer Termine (Vorschau, Kalender, Kampagnenstart, Uebernahme).
    public partial class Package
    {
        PackageType? typeSet;
        public DateTime? Lauf1;             // optional: Datum von Lauf 1 (Vorschau, Vorschlag beim Kampagnenstart); XML "lauf1"

        // Pakettyp; ohne ausdrueckliche Angabe: mit Hauptlauf-Schritt Kampagne, sonst Ereignis (z. B. TFF Flush, Zwischenreinigung)
        public PackageType Type
        {
            get { return typeSet.HasValue ? typeSet.Value : (Head != null ? PackageType.Kampagne : PackageType.Ereignis); }
            set { typeSet = value; }
        }

        internal bool TypeIsSet { get { return typeSet.HasValue; } }

        int HeadIndex(DayOfWeek day1)
        {
            int main = -1;   // erster Hauptlauf-Schritt mit den meisten Wiederholungen
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.Kind == StepKind.Run && s.Bind == StepBind.Frei && !s.IsEmpty && (main < 0 || s.RunCountFor(day1) > Steps[main].RunCountFor(day1))) main = i;
            }
            return main;
        }

        // Der eine Hauptlauf-Schritt (Art Lauf, frei); null = kein Hauptlauf
        public PackageStep Head { get { int i = HeadIndex(Day1); return i < 0 ? null : Steps[i]; } }

        // Geplante Laeufe (inklusive ausgefallener); setzen: Anzahl des Hauptlaufs (Ende "nach n", kein "bis Tag")
        public int RunCount
        {
            get { PackageStep h = Head; return h != null ? h.RunCountFor(Day1) : 0; }
            set { PackageStep h = Head; if (h != null) h.RunCount = value; }
        }

        // Laufanzahl aendern (Kampagnenstart): Hauptlauf bekommt n Laeufe, eigene Wiederholungen "fuer alle Laeufe" folgen, Ausfaelle ab n entfallen
        public void SetRunCount(int n)
        {
            PackageStep h = Head;
            if (h == null) return;
            n = Math.Max(1, Math.Min(PackageStep.MaxRuns, n));
            int old = RunCount;
            if (n == old) return;
            h.RunCount = n;
            foreach (PackageStep s in Steps)
                if (s != h && s.Bind == StepBind.Frei && s.Until == 0 && s.Repeat != RepeatMode.DayList && old > 1 && s.Count == old && !s.IsBackup) { s.Count = n; DropRunsFrom(s, n); }
            DropRunsFrom(h, n);
        }

        // Ausfaelle, Verschiebungen und Kennungs-Texte von Hand ab Lauf n (0-basiert) entfernen (der Lauf existiert nicht mehr)
        static void DropRunsFrom(PackageStep s, int n)
        {
            s.Skip.RemoveAll(delegate(int r) { return r >= n; });
            foreach (Dictionary<int, int> m in new[] { s.Move, s.Shift })
                foreach (int k in new List<int>(m.Keys)) if (k >= n) m.Remove(k);
            foreach (int k in new List<int>(s.IdentOver.Keys)) if (k >= n) s.IdentOver.Remove(k);
        }

        // Schritte, deren Laufnummer beim Kampagnenstart von Hand kommt (Pflicht, kein Vorschlag): Packen; Einzelpaket ohne Packen: der erste Schritt
        public List<PackageStep> HandNoSteps()
        {
            List<PackageStep> l = new List<PackageStep>();
            foreach (PackageStep s in Steps) if (s.Source == PackenName && !s.IsBackup && !s.IsEmpty) l.Add(s);
            if (l.Count == 0 && Type == PackageType.Einzel)
                foreach (PackageStep s in Steps) if (!s.IsBackup && !s.IsEmpty) { l.Add(s); break; }
            return l;
        }

        // Paket-Tag (1-basiert) von Lauf 1
        public int Lauf1Day { get { PackageStep h = Head; return h != null ? h.Days(Day1)[0] : 1; } }

        // Datum von Tag 1, wenn Lauf 1 am angegebenen Datum stattfinden soll
        public DateTime StartFor(DateTime lauf1) { return lauf1.Date.AddDays(1 - Lauf1Day); }

        // Laeufe eines Schritts: bei JeLauf die des Hauptlaufs
        public int RunCountOf(PackageStep s)
        {
            PackageStep h = Head;
            return s.Bind == StepBind.JeLauf && h != null && s != h ? h.RunCountFor(Day1) : s.RunCountFor(Day1);
        }

        // Alle Termine aller Schritte (Tag 1 = Starttag). gaps = Abstaende zwischen den Hauptlaeufen (z. B. aus einer frueheren Kampagne),
        // fehlende = Paket-Abstand. Alles, was zu einem Hauptlauf gehoert, wandert mit ihm. Ausgefallene Laeufe (Skip) haben keinen Termin,
        // ihre JeLauf-Schritte entfallen; die Nummern der uebrigen Laeufe bleiben (Luecke).
        public List<RunDate> Schedule() { return Schedule(null); }
        public List<RunDate> Schedule(List<int> gaps) { return Schedule(gaps, Day1); }

        public List<RunDate> Schedule(List<int> gaps, DayOfWeek day1)
        {
            int main = HeadIndex(day1);
            List<int> mainDay = new List<int>(), shift = new List<int>();
            if (main >= 0)
            {
                mainDay = Steps[main].Days(day1);
                for (int r = 0; r < mainDay.Count; r++)
                {
                    int plan = r == 0 ? 0 : mainDay[r] - mainDay[r - 1];   // geplanter Abstand (alle x Tage, Wochentage, Tagesliste)
                    shift.Add(r == 0 ? 0 : shift[r - 1] + (gaps != null && r - 1 < gaps.Count ? gaps[r - 1] : plan) - plan);
                }
            }
            List<RunDate> res = new List<RunDate>();
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty) continue;
                if (main >= 0 && i != main && s.Bind == StepBind.JeLauf)
                {
                    int ord = 0;   // zaehlt nur stattfindende Laeufe ("reicht fuer 2 Laeufe")
                    for (int r = 0; r < mainDay.Count; r++)
                    {
                        if (Steps[main].Skips(r)) continue;
                        if (ord++ % Math.Max(1, s.RunEvery) != 0) continue;
                        RunDate d = new RunDate(); d.Step = i; d.Run = r; d.Group = r; d.Day = mainDay[r] + shift[r] + s.RunOffset;
                        d.Label = s.Label(Prefix, r); d.Row = s.Row;
                        res.Add(d);
                    }
                    continue;
                }
                List<int> days = s.Days(day1);
                for (int r = 0; r < days.Count; r++)
                {
                    if (i == main && s.Skips(r)) continue;
                    RunDate d = new RunDate(); d.Step = i; d.Run = r; d.Day = days[r];
                    d.Label = s.Label(Prefix, r); d.Row = s.Row;
                    for (int k = 0; k < mainDay.Count; k++) if (mainDay[k] <= d.Day) d.Group = k;
                    if (i == main) d.Group = r;
                    if (d.Group >= 0) d.Day += shift[d.Group];
                    res.Add(d);
                }
            }
            return res;
        }

        // ---------- Kopie ----------

        public Package Clone()
        {
            Package p = (Package)MemberwiseClone();
            p.Steps = new List<PackageStep>();
            foreach (PackageStep s in Steps) p.Steps.Add(s.Clone());
            p.Blocks = new List<PackageBlock>();
            foreach (PackageBlock b in Blocks) { PackageBlock n = new PackageBlock(); n.Day = b.Day; n.Row = b.Row; n.Data = b.Data.Clone(); p.Blocks.Add(n); }
            return p;
        }
    }
}
