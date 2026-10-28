using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Aenderungen anderer PCs sichtbar machen (Idee von Claude): nach dem Nachladen werden geaenderte Bloecke
    // ChangeHighlight.Seconds lang dezent markiert (nur Ansicht, nicht gespeichert); eigene Aenderungen nicht.
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
