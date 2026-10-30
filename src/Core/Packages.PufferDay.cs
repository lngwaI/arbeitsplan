using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // UX-5c: eigener Zeitpunkt je Puffer-Karte (PufferSpec.OffsetDays) bei "zu jedem Lauf": Puffer 1 am Lauftag-2, Puffer 2 am Lauftag-1.
    // Schedule() liefert weiter einen Termin je Schritt und Lauf (Tag des Schritt-Versatzes); die Karten mit abweichendem Versatz werden
    // erst in BuildTargets/Apply auf ihren Tag gelegt (CardDelta = Tage gegenueber dem Termin des Schritts). Ohne eigene Versaetze aendert sich nichts.
    public partial class PackageStep
    {
        public const int MaxCardOffset = 7;

        // Versatz zum Lauf, den die Karte tatsaechlich hat (eigener, sonst der des Schritts)
        public int CardOffset(PufferSpec sp) { return Bind == StepBind.JeLauf && sp.OffsetDays.HasValue ? sp.OffsetDays.Value : RunOffset; }

        // Tage gegenueber dem Termin des Schritts (0 = wie der Schritt)
        public int CardDelta(PufferSpec sp) { return CardOffset(sp) - RunOffset; }

        // Mindestens eine Karte liegt an einem anderen Tag als der Schritt
        public bool HasCardOffsets
        {
            get
            {
                if (Bind != StepBind.JeLauf) return false;
                foreach (PufferSpec sp in Puffers) if (CardDelta(sp) != 0) return true;
                return false;
            }
        }

        // Verschiedene Tage-Abstaende der Karten gegenueber dem Termin des Schritts, aufsteigend; ohne eigene Versaetze nur 0
        public List<int> CardDeltas()
        {
            List<int> l = new List<int>();
            foreach (PufferSpec sp in Puffers) { int d = CardDelta(sp); if (!l.Contains(d)) l.Add(d); }
            if (l.Count == 0) l.Add(0);
            l.Sort();
            return l;
        }

        // Puffer dieses Laufs, die am Tag "Termin + delta" liegen (Reihenfolge der Karten bleibt)
        public List<BufferItem> BuffersAt(int run, string prefix, int lauf, int delta)
        {
            List<BufferItem> all = BuffersFor(run, prefix, lauf);
            if (Puffers.Count == 0) return all;
            List<BufferItem> res = new List<BufferItem>();
            for (int k = 0; k < Puffers.Count && k < all.Count; k++) if (CardDelta(Puffers[k]) == delta) res.Add(all[k]);
            return res;
        }
    }

    public partial class Package
    {
        // Der Schritt hat Karten an verschiedenen Tagen (und es gibt einen Hauptlauf, an dem er haengt)
        public bool CardsSplit(PackageStep s, DayOfWeek day1) { return s.IsBuffer && s.HasCardOffsets && HeadIndex(day1) >= 0; }

        // N7: Tage, um die die frueheste Karte vor dem Termin des Schritts liegt (0 = keine davor). Eine Quelle fuer Normalize, Kalender,
        // Kampagnen-Vorschau und Pruefung (frueheste Karte = Termin + MinCardDelta).
        public int MinCardDelta(PackageStep s, DayOfWeek day1) { return CardsSplit(s, day1) ? Math.Min(0, s.CardDeltas()[0]) : 0; }

        // Frueheste Karte eines Termins vor Tag 1 (= vor dem Kampagnen-Start)?
        public bool CardBeforeStart(RunDate d, DayOfWeek day1)
        {
            return d.Step >= 0 && d.Step < Steps.Count && Steps[d.Step].IsBuffer && d.Day + MinCardDelta(Steps[d.Step], day1) < 1;
        }

        // Regel "pufvor" (Sperre): eine Puffer-Karte laege vor dem Kampagnen-Start; beim Uebernehmen wuerde ein Block ausserhalb
        // des gezeigten Zeitraums geschrieben (bei "Überschreiben" sogar ersetzt). Je Schritt eine Meldung.
        void CheckPufferBefore(List<PackageProblem> res, List<RunDate> plan, DayOfWeek day1)
        {
            HashSet<int> done = new HashSet<int>();
            foreach (RunDate d in plan)
            {
                if (!CardBeforeStart(d, day1) || !done.Add(d.Step)) continue;
                PackageStep s = Steps[d.Step];
                List<string> who = new List<string>();
                foreach (int dl in CardsSplit(s, day1) ? s.CardDeltas() : new List<int>(new[] { 0 }))
                    if (d.Day + dl < 1) who.Add(CardsSplit(s, day1) ? CardNames(s, dl) : "alle Puffer");
                int days = 1 - (d.Day + MinCardDelta(s, day1));
                PackageProblem p = Add(res, Severity.Sperre, "pufvor", "Puffer „" + StepName(d.Step) + "“ (" + string.Join(", ", who.ToArray()) + ", Lauf " + (d.Run + 1) + ") läge "
                    + days + (days == 1 ? " Tag" : " Tage") + " vor dem Kampagnen-Start – beim Übernehmen würde ein Block außerhalb des gezeigten Zeitraums geschrieben", d.Step, d.Run);
                p.Action = "eigenen Versatz der Karte verkleinern oder Lauf 1 (Startdatum) entsprechend später legen";
            }
        }

        // Namen der Puffer (Karten) im Plantext fuer Tooltips: "Puffer 1, Puffer 2" je Tag-Abstand delta
        public string CardNames(PackageStep s, int delta)
        {
            List<string> n = new List<string>();
            for (int k = 0; k < s.Puffers.Count; k++) if (s.CardDelta(s.Puffers[k]) == delta) n.Add("Puffer " + (k + 1));
            return string.Join(", ", n.ToArray());
        }

        // Puffer-Termine, die nach dem Lauf liegen, den sie bedienen (Hinweis "pufnach"): Schritte "zu jedem Lauf" mit Versatz > 0
        // (je Karte), freie Termine ohne folgenden Lauf. Je Schritt ein Hinweis.
        void CheckPufferAfter(List<PackageProblem> res, List<RunDate> plan, DayOfWeek day1)
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty || !s.IsBuffer) continue;
                if (s.Bind == StepBind.JeLauf)
                {
                    if (HeadIndex(day1) < 0 || i == HeadIndex(day1)) continue;
                    List<string> late = new List<string>();
                    if (s.Puffers.Count == 0) { if (s.RunOffset > 0) late.Add("alle Puffer"); }
                    else for (int k = 0; k < s.Puffers.Count; k++) if (s.CardOffset(s.Puffers[k]) > 0) late.Add("Puffer " + (k + 1));
                    foreach (KeyValuePair<int, int> kv in s.Shift) if (s.RunOffset + kv.Value > 0) late.Add("für Lauf " + (kv.Key + 1));
                    if (late.Count > 0)
                        Add(res, Severity.Hinweis, "pufnach", "Puffer „" + StepName(i) + "“ (" + string.Join(", ", late.ToArray()) + ") liegt nach dem Lauf, den er bedient", i).Action = "Versatz auf „vor dem Lauf“ oder „am Tag des Laufs“ stellen";
                    continue;
                }
                foreach (RunDate d in plan)
                {
                    if (d.Step != i || LaufOf(plan, d, day1) >= 0) continue;
                    Add(res, Severity.Hinweis, "pufnach", "Puffer „" + StepName(i) + "“ (Termin " + (d.Run + 1) + ") liegt nach dem letzten Lauf und bedient keinen Lauf", i, d.Run).Action = "Termin früher legen oder „Ende bis Lauf“ wählen";
                }
            }
        }
    }
}
