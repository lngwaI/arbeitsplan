using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen zur Blockstruktur (getrennt vom Tagesgeschaeft): Reihenfolge, Anzahl, Namen, F/S/N-Bezeichnung,
    // Mindesthoehe, "LKZ gemessen?"-Feld. Zeilen werden nie geloescht (nur neue, noch ungespeicherte lassen sich entfernen).
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
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 460);

            Label hint = new Label();
            hint.Text = "Gilt für alle Tage. Neue Zeilen erscheinen am Ende und lassen sich mit \"Hoch\" verschieben.";
            hint.SetBounds(10, 8, 680, 20);

            grid.SetBounds(10, 32, 680, 372);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = SystemColors.Window;
            AddText("Name", 190, !canNames); AddText("F", 40, !canNames); AddText("S", 40, !canNames); AddText("N", 40, !canNames);
            AddText("Höhe (Zeilen)", 100, !canHeight);
            DataGridViewCheckBoxColumn lk = new DataGridViewCheckBoxColumn(); lk.HeaderText = "LKZ-Feld"; lk.Width = 80; lk.ReadOnly = !canNames;
            grid.Columns.Add(lk);
            foreach (RowInfo r in rows)
            {
                int i = grid.Rows.Add(r.Name, r.Labels[0], r.Labels[1], r.Labels[2], r.MinLines.ToString(), r.HasLkz);
                grid.Rows[i].Tag = r.Clone();
            }

            bAdd.Text = "+ Zeile"; bAdd.SetBounds(10, 414, 80, 30); bAdd.Enabled = canNames; bAdd.Click += delegate { AddRow(); };
            bUp.Text = "Hoch"; bUp.SetBounds(96, 414, 70, 30); bUp.Enabled = canSort; bUp.Click += delegate { MoveSel(-1); };
            bDown.Text = "Runter"; bDown.SetBounds(172, 414, 70, 30); bDown.Enabled = canSort; bDown.Click += delegate { MoveSel(1); };
            bRemove.Text = "Neue entfernen"; bRemove.SetBounds(248, 414, 120, 30); bRemove.Enabled = canNames; bRemove.Click += delegate { RemoveNew(); };
            bOk.Text = "Speichern"; bOk.SetBounds(490, 414, 95, 30); bOk.Click += delegate { Commit(); };
            bCancel.Text = "Abbrechen"; bCancel.SetBounds(595, 414, 95, 30); bCancel.DialogResult = DialogResult.Cancel;
            CancelButton = bCancel;

            Controls.AddRange(new Control[] { hint, grid, bAdd, bUp, bDown, bRemove, bOk, bCancel });
        }

        void AddText(string title, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = title; c.Width = width; c.ReadOnly = readOnly; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        void AddRow()
        {
            RowInfo r = new RowInfo(); r.Id = 0;
            int i = grid.Rows.Add("", r.Labels[0], r.Labels[1], r.Labels[2], r.MinLines.ToString(), false);
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
