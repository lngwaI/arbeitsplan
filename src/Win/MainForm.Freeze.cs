using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Freeze-Vorgaenge eines Blocks (Zeile mit RowInfo.Freeze): "+ Freeze" bzw. Klick auf einen Vorgang oeffnet FreezeForm.
    // Gespeichert wird wie ein Haken (Change): sofort mit Protokoll, auch ohne Bearbeitungsmodus; die Aenderung wird dreiwege
    // (Basis = Stand beim Oeffnen) in den inzwischen gespeicherten Stand eingearbeitet (FreezeSet.Merge), fremde Aenderungen bleiben erhalten.
    partial class MainForm
    {
#if TESTHOOKS
        Func<FreezeChoice, bool> freezeTest;   // Selbsttest: statt des Dialogs (true = OK)
#endif

        void InitFreeze(ContextMenuStrip vm)
        {
            ToolStripItem add = new ToolStripMenuItem("Freeze hinzufügen…", null, delegate
            {
                CellData c = view.Selected;
                if (c != null && rowInfo[c.RowIndex].Freeze) EditFreeze(c, "", c.Day);
            });
            vm.Items.Add(add);
            vm.Opening += delegate { CellData c = view.Selected; add.Visible = c != null && c.RowIndex < rowInfo.Count && rowInfo[c.RowIndex].Freeze; };
        }

        // id "" = neuer Vorgang; origin = Tag des Blocks, in dem der Vorgang gespeichert ist (bei einer Fortsetzung der fruehere Tag)
        void EditFreeze(CellData c, string id, DateTime origin)
        {
            bool isNew = id.Length == 0, canWrite = me.Has(Perm.EditText) || me.Has(Perm.AddText);
            if (isNew && !canWrite)
            {
                if (!quiet) MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (editCell != null && !EndEdit(true)) return;   // laufende Bearbeitung zuerst speichern
            CellData t = origin.Date == c.Day.Date ? c : CellFor(c.RowIndex, origin);
            FreshCur(t);
            FreezeItem cur = isNew ? null : t.Cur.Freezes.Find(delegate(FreezeItem f) { return f.Id == id; });
            if (!isNew && cur == null)
            {
                if (!quiet) MessageBox.Show("Dieser Freeze-Vorgang wurde inzwischen von einem anderen PC entfernt.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Rebuild(false);
                return;
            }
            FreezeChoice ch = new FreezeChoice();
            ch.Cfg = store.GetCatalog().FreezeDefaults; ch.IsNew = isNew; ch.ReadOnly = !canWrite;
            ch.Where = BlockTitle(t) + " · " + Ui.DayText(t.Day);
            ch.Mains = RunsAround(t.Day); ch.Tanks = FreezeTanks();
            if (isNew)
            {
                FreezeItem f = new FreezeItem(); f.DauerMin = ch.Cfg.DauerMin; f.AbstandMin = ch.Cfg.AbstandMin; f.Cycles = ch.Cfg.Cycles; f.Offset2Min = ch.Cfg.Offset2Min; f.Offset3Min = ch.Cfg.Offset3Min; f.Start1to3[0] = t.Day.AddHours(6);
                ch.Item = f;
            }
            else ch.Item = cur.Clone();
            string laufNr = ch.Item.MainRef.Length > 0 ? ch.Item.MainRef : ch.Mains.Count > 0 ? ch.Mains[0] : "";
            ch.SpareTank = FreezeSet.SpareTank(store, rowInfo, laufNr, t.Day, store.GetCatalog());
            if (!AskFreeze(ch) || ch.ReadOnly) return;

            List<FreezeItem> baseList = FreezeSet.CloneAll(t.Cur.Freezes), mine = FreezeSet.CloneAll(baseList);
            int at = isNew ? -1 : mine.FindIndex(delegate(FreezeItem f) { return f.Id == id; });
            if (ch.Action == FreezeChoice.Act.Remove) { if (at >= 0) mine.RemoveAt(at); }
            else if (at >= 0) { ch.Item.Id = id; mine[at] = ch.Item; }
            else FreezeSet.Add(mine, ch.Item);
            ChangeFreezes(t, baseList, mine);
            Rebuild(false);   // Fortsetzungen in anderen Tagen neu berechnen
        }

        bool AskFreeze(FreezeChoice ch)
        {
#if TESTHOOKS
            if (freezeTest != null) return freezeTest(ch);
#endif
            using (FreezeForm f = new FreezeForm(ch)) return f.ShowDialog(this) == DialogResult.OK;
        }

        // Block der Zeile row am Tag day: sichtbare Zelle, sonst eine Hilfszelle (Vorgang aus einem Tag vor der angezeigten Woche)
        CellData CellFor(int row, DateTime day)
        {
            foreach (CellData x in view.Cells) if (x.RowIndex == row && x.Day.Date == day.Date) return x;
            CellData d = new CellData(); d.Day = day.Date; d.RowIndex = row; d.Cur = store.Get(day.Date, rowInfo[row].Id) ?? new Entry(); d.Show(d.Cur);
            return d;
        }

        // Laufnummern (8 Stellen) im Plan +-3 Tage um day, naechste zuerst
        List<string> RunsAround(DateTime day)
        {
            List<string> r = new List<string>();
            for (int k = 0; k <= 3; k++)
                for (int s = 0; s < (k == 0 ? 1 : 2); s++)
                {
                    DateTime d = day.AddDays(s == 0 ? -k : k);
                    foreach (RowInfo ri in rowInfo)
                    {
                        Entry e = store.Get(d, ri.Id);
                        if (e != null) foreach (RunRef x in RunNumbers.Find(e)) if (!r.Contains(x.Product + x.Run)) r.Add(x.Product + x.Run);
                    }
                }
            return r;
        }

        // Freezetanks aus dem Katalog: Positionen der Austausch-Gruppe "Freezetanks" bzw. mit "Freezetank" im Namen
        List<string> FreezeTanks()
        {
            List<string> r = new List<string>();
            Catalog cat = store.GetCatalog();
            EquipGroup g = cat.Groups.Find(delegate(EquipGroup x) { return x.Name.IndexOf("Freezetank", StringComparison.OrdinalIgnoreCase) >= 0; });
            foreach (EquipPosition p in cat.Positions)
                if ((g != null && g.Shorts.Contains(p.Short)) || p.Name.StartsWith("Freezetank", StringComparison.OrdinalIgnoreCase))
                    foreach (string id in p.Ids) if (!r.Contains(id)) r.Add(id);
            return r;
        }

        // Rechte (wie bei Puffern): Neu AddText (oder EditText), Entfernen DeleteText, Aendern inkl. tatsaechlichem Start EditText. null = erlaubt.
        string FreezeRightMissing(List<FreezeItem> old, List<FreezeItem> now)
        {
            bool add, rem, chg; FreezeSet.Diff(old, now, out add, out rem, out chg);
            if (rem && !me.Has(Perm.DeleteText)) return "Text löschen";
            if (chg && !me.Has(Perm.EditText)) return "Text ändern";
            if (add && !me.Has(Perm.AddText) && !me.Has(Perm.EditText)) return "Text hinzufügen";
            return null;
        }

        // Speichern ueber Change: Rechte, Konfliktweg, Protokoll. Hat ein anderer PC den Block inzwischen geaendert, wird die Aenderung dreiwege eingearbeitet.
        void ChangeFreezes(CellData c, List<FreezeItem> baseList, List<FreezeItem> mine)
        {
            if (FreezeSet.Key(mine) == FreezeSet.Key(baseList)) return;
            string miss = FreezeRightMissing(baseList, mine);
            if (miss != null)
            {
                if (!quiet) MessageBox.Show("Für diese Änderung fehlt das Recht \"" + miss + "\".", "Keine Berechtigung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Func<List<FreezeItem>, List<FreezeItem>> merged = delegate(List<FreezeItem> theirs) { bool clash; return FreezeSet.Merge(baseList, mine, theirs, true, out clash); };
            Change(c, delegate(CellData x) { },   // nie im Bearbeitungsmodus (EditFreeze beendet ihn zuerst)
                delegate(Entry e) { return FreezeSet.Key(merged(e.Freezes)) == FreezeSet.Key(e.Freezes); },
                delegate(Entry e) { e.Freezes = merged(e.Freezes); });
        }
    }
}
