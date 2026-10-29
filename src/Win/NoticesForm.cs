using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Hilfe -> "Benachrichtigungen…": die letzten 50 Meldungen (neueste zuerst), ungelesene fett; "Alle als gelesen markieren".
    class NoticesForm : Form
    {
        readonly DataGridView grid = new DataGridView();
        readonly Label empty = new Label(), head = new Label();
        readonly Button markAll = new Button();
        readonly Func<List<Notice>> load;
        readonly Func<int> markAllRead;
        int unread;
        public int Unread { get { return unread; } }

        public NoticesForm(Func<List<Notice>> load, Func<int> markAllRead)
        {
            this.load = load; this.markAllRead = markAllRead;
            Text = "Benachrichtigungen";
            Ui.Dialog(this, 720, 420, false);
            Theme th = Theme.Current;
            head.Font = new Font(Font, FontStyle.Bold); head.SetBounds(12, 10, 696, 20);
            grid.SetBounds(12, 36, 696, 336);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.ReadOnly = true;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = th.Window; grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = th.Separator; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = th.RowHead; grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.SelectionBackColor = th.ButtonOn; grid.DefaultCellStyle.SelectionForeColor = th.HeadText;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            Col("Zeit", 110); Col("Meldung", 490); Col("Stand", 80);
            empty.Text = "Keine Benachrichtigungen"; empty.TextAlign = ContentAlignment.MiddleCenter; empty.ForeColor = SystemColors.GrayText;
            empty.SetBounds(12, 36, 696, 336); empty.Visible = false;
            markAll.Text = "Alle als gelesen markieren"; markAll.SetBounds(12, 382, 190, 28);
            markAll.Click += delegate { markAllRead(); Fill(); };
            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(608, 382, 100, 28); close.DialogResult = DialogResult.Cancel; CancelButton = close;
            Controls.AddRange(new Control[] { head, empty, grid, markAll, close });
            Theme.Current.Style(this);
            Fill();
        }

        void Col(string title, int w)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = title; c.Width = w; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        void Fill()
        {
            List<Notice> l = load();
            grid.Rows.Clear(); unread = 0;
            foreach (Notice n in l)
            {
                int i = grid.Rows.Add(n.Time.ToString("dd.MM.yyyy HH:mm", Ui.De), n.Text, n.Read ? "gelesen" : "ungelesen");
                if (!n.Read) { unread++; grid.Rows[i].DefaultCellStyle.Font = new Font(Font, FontStyle.Bold); }
            }
            empty.Visible = l.Count == 0; grid.Visible = l.Count > 0; markAll.Enabled = unread > 0;
            head.Text = unread > 0 ? unread + " ungelesen von " + l.Count : l.Count + " Meldung" + (l.Count == 1 ? "" : "en") + ", alle gelesen";
            grid.ClearSelection();
        }
    }
}
