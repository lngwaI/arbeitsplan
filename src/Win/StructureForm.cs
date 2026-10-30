using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen zur Blockstruktur (getrennt vom Tagesgeschaeft): Reihenfolge, Anzahl, Namen, F/S/N-Bezeichnung,
    // Mindesthoehe, "LKZ gemessen?"-Feld, Freeze-Zeile, Schichtmodus (F/S/N, F/S, Tagesaufgabe), Varianten. Zeilen werden nie geloescht (nur neue, noch ungespeicherte lassen sich entfernen).
    class StructureForm : Form
    {
        readonly DataGridView grid = new DataGridView();
        readonly Button bAdd = new Button(), bUp = new Button(), bDown = new Button(), bRemove = new Button(), bOk = new Button(), bCancel = new Button();
        readonly bool canNames, canHeight, canSort;
        public List<RowInfo> Result;

        public StructureForm(List<RowInfo> rows, bool canNames, bool canHeight, bool canSort)
        {
            this.canNames = canNames; this.canHeight = canHeight; this.canSort = canSort;
            Text = "Blockstruktur";
            Ui.Dialog(this, 860, 460);

            Label hint = new Label();
            hint.Text = "Gilt für alle Tage. Neue Zeilen erscheinen am Ende und lassen sich mit \"Hoch\" verschieben. Varianten mit | trennen (z. B. mobil|stationaer).";
            hint.SetBounds(10, 8, 840, 20);

            grid.SetBounds(10, 32, 840, 372);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = SystemColors.Window;
            AddText("Name", 150, !canNames); AddText("F", 40, !canNames); AddText("S", 40, !canNames); AddText("N", 40, !canNames);
            AddText("Höhe (Zeilen)", 90, !canHeight);
            DataGridViewCheckBoxColumn lk = new DataGridViewCheckBoxColumn(); lk.HeaderText = "LKZ-Feld"; lk.Width = 70; lk.ReadOnly = !canNames;
            grid.Columns.Add(lk);
            DataGridViewComboBoxColumn md = new DataGridViewComboBoxColumn(); md.HeaderText = "Schichtmodus"; md.Width = 140; md.ReadOnly = !canNames;
            md.Items.AddRange(ModeNames); md.FlatStyle = FlatStyle.Flat; md.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            grid.Columns.Add(md);
            AddText("Varianten", 170, !canNames);
            DataGridViewCheckBoxColumn fz = new DataGridViewCheckBoxColumn(); fz.HeaderText = "Freeze-Zeile"; fz.Width = 85; fz.ReadOnly = !canNames; fz.ToolTipText = "Zeile für Freeze-Vorgänge (Bag-/Tank-Freeze mit Cycles)";
            grid.Columns.Add(fz);
            foreach (RowInfo r in rows)
            {
                int i = grid.Rows.Add(r.Name, r.Labels[0], r.Labels[1], r.Labels[2], r.MinLines.ToString(), r.HasLkz, ModeNames[(int)r.Mode], r.Variants, r.Freeze);
                grid.Rows[i].Tag = r.Clone();
                LockLabels(i);
            }
            // Schichtmodus sofort uebernehmen: F/S/N-Bezeichnungen ausgeblendeter Schichten sperren
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 6) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex == 6) LockLabels(e.RowIndex); };

            bAdd.Text = "+ Zeile"; bAdd.SetBounds(10, 414, 80, 30); bAdd.Enabled = canNames; bAdd.Click += delegate { AddRow(); };
            bUp.Text = "Hoch"; bUp.SetBounds(96, 414, 70, 30); bUp.Enabled = canSort; bUp.Click += delegate { MoveSel(-1); };
            bDown.Text = "Runter"; bDown.SetBounds(172, 414, 70, 30); bDown.Enabled = canSort; bDown.Click += delegate { MoveSel(1); };
            bRemove.Text = "Neue entfernen"; bRemove.SetBounds(248, 414, 120, 30); bRemove.Enabled = canNames; bRemove.Click += delegate { RemoveNew(); };
            bOk.Text = "Speichern"; bOk.SetBounds(650, 414, 95, 30); bOk.Click += delegate { Commit(); };
            bCancel.Text = "Abbrechen"; bCancel.SetBounds(755, 414, 95, 30); bCancel.DialogResult = DialogResult.Cancel;
            CancelButton = bCancel;

            Controls.AddRange(new Control[] { hint, grid, bAdd, bUp, bDown, bRemove, bOk, bCancel });
        }

        // Reihenfolge = ShiftMode (FSN, FS, Task)
        static readonly string[] ModeNames = { "Früh/Spät/Nacht", "Früh/Spät", "Tagesaufgabe" };

        static ShiftMode ModeOf(object v) { int i = Array.IndexOf(ModeNames, v as string); return i < 0 ? ShiftMode.FSN : (ShiftMode)i; }

        // Bezeichnungen ausgeblendeter Schichten sind gesperrt (grau), bleiben aber erhalten
        void LockLabels(int i)
        {
            if (!canNames) return;   // Spalten sind ohnehin gesperrt
            RowInfo m = new RowInfo(); m.Mode = ModeOf(grid.Rows[i].Cells[6].Value);
            for (int s = 0; s < 3; s++)
            {
                DataGridViewCell c = grid.Rows[i].Cells[1 + s];
                c.ReadOnly = !m.HasShift(s) || m.Mode == ShiftMode.Task;
                c.Style.ForeColor = c.ReadOnly ? SystemColors.GrayText : Color.Empty;
            }
        }

#if TESTHOOKS
        // Selbsttest: Haken "Freeze-Zeile" lesen/setzen, Speichern ohne Anzeige
        public bool TestFreeze(int i) { object v = grid.Rows[i].Cells[8].Value; return v is bool && (bool)v; }
        public void TestSetFreeze(int i, bool on) { grid.Rows[i].Cells[8].Value = on; }
        public List<RowInfo> TestCommit() { Commit(); return Result; }
#endif

        void AddText(string title, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = title; c.Width = width; c.ReadOnly = readOnly; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        void AddRow()
        {
            RowInfo r = new RowInfo(); r.Id = 0;
            int i = grid.Rows.Add("", r.Labels[0], r.Labels[1], r.Labels[2], r.MinLines.ToString(), false, ModeNames[0], "", false);
            grid.Rows[i].Tag = r;
            grid.CurrentCell = grid.Rows[i].Cells[0];
        }

        void RemoveNew()
        {
            if (grid.CurrentRow == null) return;
            RowInfo r = (RowInfo)grid.CurrentRow.Tag;
            if (r.Id > 0) { MessageBox.Show("Bestehende Zeilen können nicht gelöscht werden.", "Blockstruktur", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            grid.Rows.Remove(grid.CurrentRow);
        }

        void MoveSel(int delta)
        {
            if (grid.CurrentRow == null) return;
            int i = grid.CurrentRow.Index, j = i + delta;
            if (j < 0 || j >= grid.Rows.Count) return;
            object[] a = Values(i), b = Values(j);
            object ta = grid.Rows[i].Tag, tb = grid.Rows[j].Tag;
            for (int c = 0; c < a.Length; c++) { grid.Rows[i].Cells[c].Value = b[c]; grid.Rows[j].Cells[c].Value = a[c]; }
            grid.Rows[i].Tag = tb; grid.Rows[j].Tag = ta;
            grid.CurrentCell = grid.Rows[j].Cells[0];
        }

        object[] Values(int i)
        {
            object[] v = new object[grid.Columns.Count];
            for (int c = 0; c < v.Length; c++) v[c] = grid.Rows[i].Cells[c].Value;
            return v;
        }

        static string S(object o) { return o == null ? "" : o.ToString().Trim(); }

        void Commit()
        {
            grid.EndEdit();
            List<RowInfo> res = new List<RowInfo>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                RowInfo r = ((RowInfo)row.Tag).Clone();
                if (canNames)
                {
                    r.Name = S(row.Cells[0].Value);
                    for (int s = 0; s < 3; s++) r.Labels[s] = S(row.Cells[1 + s].Value);
                    r.HasLkz = row.Cells[5].Value is bool && (bool)row.Cells[5].Value;
                    r.Mode = ModeOf(row.Cells[6].Value);
                    List<string> v = new List<string>();
                    foreach (string x in S(row.Cells[7].Value).Split('|')) if (x.Trim().Length > 0 && !v.Contains(x.Trim())) v.Add(x.Trim());
                    r.Variants = string.Join("|", v.ToArray());
                    r.Freeze = row.Cells[8].Value is bool && (bool)row.Cells[8].Value;
                }
                if (canHeight)
                {
                    int h;
                    if (!int.TryParse(S(row.Cells[4].Value), out h) || h < 1 || h > 30)
                    {
                        MessageBox.Show("Höhe: bitte eine Zahl von 1 bis 30 eingeben.", "Blockstruktur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    r.MinLines = h;
                }
                res.Add(r);
            }
            Result = res;
            DialogResult = DialogResult.OK;
        }
    }
}
