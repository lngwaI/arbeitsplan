using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Migration Paketformat <= 6 -> 7 (beim Laden). Der Hauptlauf bleibt der Schritt, den der alte Schedule() gewaehlt hat. Ein Schritt wird nur dann
    // JeLauf, wenn seine Tage fuer JEDEN Starttag-Wochentag genau Hauptlauf-Tage + konstanter Versatz sind und jeder Termin in der Gruppe seines
    // Hauptlaufs liegt; dann ist Schedule() vor und nach der Migration identisch (auch mit Rhythmus aus frueheren Kampagnen). Im Zweifel bleibt er frei.
    public partial class Package
    {
        public void Migrate()
        {
            int hi = HeadIndex(DayOfWeek.Monday);
            if (hi >= 0)
                for (int i = 0; i < Steps.Count; i++)
                {
                    PackageStep s = Steps[i];
                    if (i == hi || s.Bind == StepBind.JeLauf || s.IsEmpty || s.IsBackup || HasBackup(Steps, s)) continue;
                    if (s.Kind == StepKind.Normal && s.RunCountFor(DayOfWeek.Monday) < 2) continue;
                    int off;
                    if (!FollowsHead(Steps[hi], s, out off)) continue;
                    s.Bind = StepBind.JeLauf; s.RunOffset = off; s.NumberRuns = s.Kind == StepKind.Normal && s.RunCountFor(DayOfWeek.Monday) > 1;
                }
            foreach (PackageStep s in Steps) s.IdentToStart(RunCountOf(s));
        }

        static int GroupOf(List<int> mainDay, int day)
        {
            int g = -1;
            for (int k = 0; k < mainDay.Count; k++) if (mainDay[k] <= day) g = k;
            return g;
        }

        static bool FollowsHead(PackageStep h, PackageStep s, out int off)
        {
            off = 0;
            for (int w = 0; w < 7; w++)
            {
                List<int> hd = h.Days((DayOfWeek)w), sd = s.Days((DayOfWeek)w);
                if (hd.Count != sd.Count) return false;
                int o = sd[0] - hd[0];
                if (w == 0) off = o; else if (o != off) return false;
                if (Math.Abs(o) > PackageStep.MaxOffset) return false;
                for (int r = 0; r < hd.Count; r++)
                    if (sd[r] - hd[r] != o || GroupOf(hd, sd[r]) != r) return false;
            }
            return true;
        }
    }
}
