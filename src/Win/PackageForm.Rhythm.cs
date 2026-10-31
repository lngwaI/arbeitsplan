using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Puffer-Rhythmus-Fenster (PufferRhythmForm.cs): Aufruf aus der Kalenderleiste und aus "Wann?" eines Puffers.
    partial class PackageForm
    {
        readonly Button bRhythm = new Button(), bRhythmWhen = new Button();
        const string RhythmTip = "Tage aller Puffer in einer Tabelle setzen oder per Muster (mob, mob/stat, stat, frei) einfüllen";

        void InitRhythm()
        {
            bRhythm.Text = "Puffer-Rhythmus…"; bRhythmWhen.Text = "Puffer-Rhythmus…"; bRhythmWhen.Tag = "keep"; bRhythm.Tag = "keep"; bRhythm.TabStop = false;
            bRhythm.Font = Font; bRhythmWhen.Font = Font;
            bRhythm.Click += delegate { ShowRhythm(); }; bRhythmWhen.Click += delegate { ShowRhythm(); };
            tip.SetToolTip(bRhythm, RhythmTip); tip.SetToolTip(bRhythmWhen, RhythmTip);
        }

        PufferRhythmForm BuildRhythm()
        {
            return RhythmFor(StepsView(), Plan(), delegate(int day) { return StripHead(day, true); }, cat.Baustein_("Puffer mobil") != null, cat.Baustein_("Puffer stationär") != null);
        }

        // Rhythmus-Fenster fuer ein Paket mit seinem Plan (Editor und Kampagnenstart); newMob/newStat: neue Puffer-Schritte anlegbar
        internal static PufferRhythmForm RhythmFor(Package p, List<RunDate> plan, Func<int, string> headText, bool newMob, bool newStat)
        {
            PackageStep head = p.Head; List<PackageStep> steps = p.Steps;
            HashSet<int> runs = new HashSet<int>(); int last = 1, maxDay = 1;
            foreach (RunDate d in plan) { maxDay = Math.Max(maxDay, d.Day); if (head != null && d.Step == steps.IndexOf(head)) { runs.Add(d.Day); last = Math.Max(last, d.Day); } }
            if (head == null) last = maxDay;
            List<PufferRhythmForm.RRow> rows = new List<PufferRhythmForm.RRow>();
            foreach (PackageStep s in OrderOf(p))
            {
                if (s.IsEmpty || !(s.IsBuffer || s.UsesAnsatz)) continue;
                int si = steps.IndexOf(s);
                PufferRhythmForm.RRow r = new PufferRhythmForm.RRow(); r.Step = s; r.Name = TitleOf(s);
                List<string> v = VariantSet.Of(s.PufferVariant());
                r.Mob = v.Contains("mobil"); r.Stat = v.Contains("stationaer"); r.Typ = r.Mob || r.Stat ? VarText(s.PufferVariant()) : "Ansatz";
                foreach (RunDate d in plan) if (d.Step == si) r.Orig.Add(d.Day);
                r.Days = new HashSet<int>(r.Orig);
                rows.Add(r);
            }
            return new PufferRhythmForm(rows, last, Math.Max(last, maxDay) + 7, headText, runs, newMob, newStat);
        }

        void ShowRhythm()
        {
            using (PufferRhythmForm f = BuildRhythm())
                if (f.ShowDialog(big != null ? (IWin32Window)big : this) == DialogResult.OK) ApplyRhythm(f.Rows);   // aus der grossen Vorschau: deren Fenster ist Besitzer
        }

        // Je geaenderter Puffer feste Tage (Frei + Tagesliste). Neue Puffer-Schritte erst jetzt (Abbrechen aendert nichts).
        // shortcut: Rhythmus sind feste Tage, mehr Läufe beim Kampagnenstart verlängern ihn nicht; bei Bedarf Muster im Paket speichern
        void ApplyRhythm(List<PufferRhythmForm.RRow> rows)
        {
            PackageStep last = null;
            foreach (PufferRhythmForm.RRow r in rows)
            {
                if (!r.Changed || r.Days.Count == 0) continue;
                PackageStep s = r.Step;
                if (r.IsNew)
                {
                    Baustein b = cat.Baustein_(r.Mob ? "Puffer mobil" : "Puffer stationär");
                    if (b == null) continue;
                    s = FromBaustein(b); if (s.Row.Length == 0) s.Row = b.Row;
                    steps.Add(s);
                }
                SetPufferDays(s, r.Days);
                last = s;
            }
            if (last != null) AfterMove(last);
        }

        // Feste Tage eines Puffers (Frei + Tagesliste): gemeinsam fuer Rhythmus-Fenster, Palette und Kampagnenstart
        internal static void SetPufferDays(PackageStep s, ICollection<int> days)
        {
            List<int> d = new List<int>(days); d.Sort();
            s.Bind = StepBind.Frei; s.RunOffset = 0; s.RunEvery = 1; s.Repeat = RepeatMode.DayList; s.DayList = PackageStep.JoinDays(d); s.Day = d[0];
            s.Shift.Clear(); s.Move.Clear(); s.Skip.Clear(); s.Until = 0;
        }
    }
}
