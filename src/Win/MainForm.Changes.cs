using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Eigenideen von Claude (docs/Eigenideen.md):
    // - Aenderungen anderer PCs sichtbar machen: nach dem Nachladen werden geaenderte Bloecke
    //   ChangeHighlight.Seconds lang dezent markiert (nur Ansicht, nicht gespeichert); eigene Aenderungen nicht.
    // - Offene Pflichten der angezeigten KW mit "Springe dorthin".
    partial class MainForm
    {
        readonly List<ChangedBlock> changed = new List<ChangedBlock>();
        Timer changeTimer;

        // Angezeigter Stand (Version je Block) vor dem Nachladen
        List<ChangedBlock> ShownVersions()
        {
            List<ChangedBlock> l = new List<ChangedBlock>();
            foreach (CellData c in view.Cells)
            {
                if (c.RowIndex >= rowInfo.Count) continue;
                ChangedBlock b = new ChangedBlock(); b.Day = c.Day; b.RowId = rowInfo[c.RowIndex].Id; b.Version = c.Cur.Version;
                l.Add(b);
            }
            return l;
        }

        // Nach dem Uebernehmen: neue Treffer merken und markieren; Rueckgabe = Text fuer die Statusleiste ("" = nichts)
        string NoteChanges(List<ChangedBlock> shown)
        {
            DateTime now = DateTime.Now;
            List<ChangedBlock> add = ChangeHighlight.Find(shown, store, me.Login, now);
            ChangeHighlight.Update(changed, add, now);
            MarkChanged();
            view.Invalidate();
            if (changed.Count > 0)
            {
                if (changeTimer == null)
                {
                    changeTimer = new Timer(); changeTimer.Interval = 1000;
                    changeTimer.Tick += delegate
                    {
                        if (ChangeHighlight.Update(changed, new List<ChangedBlock>(), DateTime.Now)) { MarkChanged(); view.Invalidate(); }
                        if (changed.Count == 0) changeTimer.Stop();
                    };
                    FormClosed += delegate { changeTimer.Dispose(); };
                }
                changeTimer.Start();
            }
            return ChangeHighlight.Status(add, rowInfo);
        }

        // ---------- Offene Pflichten (Idee von Claude; Lesen genuegt) ----------

        // KW der Ansicht: die mit den meisten angezeigten Tagen (Fenster ab Samstag -> folgende KW)
        DateTime ShownWeek { get { return IsoWeek.Monday(start.AddDays(3)); } }

        void ShowOpenDuties()
        {
            if (!EndEdit(true)) return;
            DateTime mon = ShownWeek;
            OpenDuty t;
            using (OpenDutiesForm f = new OpenDutiesForm("Offen in " + PlanView.WeekTitle(mon), delegate(int w) { return OpenDuties.List(store, store.GetDuties(), store.GetRows(), mon, w, DateTime.Today); }))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Target == null) return;
                t = f.Target;
            }
            JumpTo(t);
        }

        // Navigation + Auswahl; Rundgang: Leiste der KW aufklappen und nach oben
        void JumpTo(OpenDuty t)
        {
            if (!EndEdit(true)) return;
            if (t.Day < start || t.Day > start.AddDays(6)) { start = MondayOf(t.Day); Rebuild(true); }
            if (t.RowId < 0)
            {
                RoundGroup g = view.Rounds.Find(delegate(RoundGroup x) { return x.Monday == IsoWeek.Monday(t.Day); });
                if (g != null && g.Collapsed) ToggleRoundFold(g);
                canvas.AutoScrollPosition = new System.Drawing.Point(0, 0);
                return;
            }
            foreach (CellData c in view.Cells)
                if (c.Day == t.Day && rowInfo[c.RowIndex].Id == t.RowId)
                {
                    if (view.Collapsed[c.RowIndex]) { fold[t.RowId] = false; ApplyFold(); view.Relayout(); }
                    view.Selected = c; view.Invalidate();
                    canvas.AutoScrollPosition = new System.Drawing.Point(0, Math.Max(0, c.R.Y - 4));
                    break;
                }
        }

        // Markierung auf die angezeigten Bloecke uebertragen (auch nach jedem Rebuild)
        void MarkChanged()
        {
            foreach (CellData c in view.Cells)
            {
                int id = c.RowIndex < rowInfo.Count ? rowInfo[c.RowIndex].Id : -1;
                c.Changed = changed.Exists(delegate(ChangedBlock x) { return x.Day == c.Day && x.RowId == id; });
            }
        }
    }
}
