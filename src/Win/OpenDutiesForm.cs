using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Woche -> "Offene Pflichten dieser Woche…" (Idee von Claude, Lesen genuegt): ruhige Liste offener LKZ, Tagesproben/WFI
    // und Rundgaenge der angezeigten KW (optional mit der naechsten); "Springe dorthin" schliesst und setzt Navigation + Auswahl.
    class OpenDutiesForm : Form
    {
        readonly DataGridView grid = new DataGridView();
        readonly CheckBox next = new CheckBox();
        readonly Label empty = new Label(), head = new Label();
        readonly Func<int, List<OpenDuty>> load;   // Anzahl KWs -> Liste
        List<OpenDuty> items = new List<OpenDuty>();
        public OpenDuty Target;                    // gewaehlter Eintrag (DialogResult.OK)

        public OpenDutiesForm(string title, Func<int, List<OpenDuty>> load)
        {
            this.load = load;
            Text = "Offene Pflichten";
            Ui.Dialog(this, 640, 440, false);
            Theme th = Theme.Current;
            head.Text = title; head.Font = new Font(Font, FontStyle.Bold); head.SetBounds(12, 10, 420, 20);
            next.Text = "Nächste KW einbeziehen"; next.AutoSize = true; next.Location = new Point(440, 10);
            next.CheckedChanged += delegate { Fill(); };

            grid.SetBounds(12, 38, 616, 350);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.ReadOnly = true;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = th.Window; grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = th.Separator; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = th.RowHead; grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.SelectionBackColor = th.ButtonOn; grid.DefaultCellStyle.SelectionForeColor = th.HeadText;
            Col("Wann", 80); Col("Bereich", 130); Col("Pflicht", 170); Col("Stand", 80);
            DataGridViewButtonColumn go = new DataGridViewButtonColumn(); go.HeaderText = ""; go.Text = "Springe dorthin"; go.UseColumnTextForButtonValue = true;
            go.Width = 130; go.FlatStyle = FlatStyle.Flat; grid.Columns.Add(go);
            grid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex == 4) Jump(e.RowIndex); };
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Jump(e.RowIndex); };

            empty.Text = "Alles erledigt ✓"; empty.Font = new Font(Font.FontFamily, 14f); empty.ForeColor = th.DoneText;
            empty.TextAlign = ContentAlignment.MiddleCenter; empty.SetBounds(12, 38, 616, 350); empty.Visible = false;

            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(528, 400, 100, 28); close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            th.StyleButton(close);
            Controls.AddRange(new Control[] { head, next, empty, grid, close });
            Fill();
        }

        void Col(string title, int w)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = title; c.Width = w; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        void Fill()
        {
            items = load(next.Checked ? 2 : 1);
            grid.Rows.Clear();
            foreach (OpenDuty o in items)
            {
                int i = grid.Rows.Add(o.When, o.Where, o.What, o.State);
                if (o.Overdue) grid.Rows[i].Cells[3].Style.ForeColor = Theme.Current.CheckMissingText;
            }
            empty.Visible = items.Count == 0; grid.Visible = items.Count > 0;
        }

        public int Count { get { return items.Count; } }

        void Jump(int row)
        {
            Target = items[row];
            DialogResult = DialogResult.OK;
        }
    }
}
