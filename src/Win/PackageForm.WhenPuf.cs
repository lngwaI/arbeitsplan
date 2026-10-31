using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Puffer-Anzeige statt "Art" ("Puffer · bedient Lauf n (Nummer)") und Tabelle "Lauf | Tage vorher" unter "Wann?"
    // (Abstand je Lauf = RunOffset + Shift[Lauf], Aenderung ueber Package.DragRun "nur dieser Lauf"). Packen/Back-Up/Puffer haben keine Art.
    partial class PackageForm
    {
        readonly DataGridView gridPuf = new DataGridView();
        Label lPufInfo;

        bool IsPacken(PackageStep s) { PackageStep h = StepsView().Head; return s.Source == Package.PackenName && !s.IsBackup && h != null && s != h; }

        bool NoKind(PackageStep s) { return s.IsBackup || s.Source == Package.PackenName || IsPuffer(s); }

        void BuildWhenPuf()
        {
            lPufInfo = Lbl(gWhat, "", 8, 262); lPufInfo.AutoEllipsis = true; Grow(lPufInfo);
            DataGridView g = gridPuf;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = g.AllowUserToResizeRows = g.AllowUserToResizeColumns = false;
            g.RowHeadersVisible = false; g.MultiSelect = false; g.SelectionMode = DataGridViewSelectionMode.CellSelect;
            g.BackgroundColor = SystemColors.Window; g.BorderStyle = BorderStyle.FixedSingle; g.Anchor = TLR;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.RowTemplate.Height = Font.Height + 6; g.ColumnHeadersHeight = Font.Height + 8;
            DataGridViewTextBoxColumn c0 = new DataGridViewTextBoxColumn(), c1 = new DataGridViewTextBoxColumn();
            c0.HeaderText = "Lauf"; c0.ReadOnly = true; c0.Width = 70; c0.SortMode = DataGridViewColumnSortMode.NotSortable;
            c1.HeaderText = "Tage vorher"; c1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c1.SortMode = DataGridViewColumnSortMode.NotSortable;
            g.Columns.Add(c0); g.Columns.Add(c1);
            gWhen.Controls.Add(g);
            tip.SetToolTip(g, "Abstand des Puffers zum Lauf, je Lauf einzeln änderbar (Tage vor dem Lauf, 0 = am Tag des Laufs, negativ = danach). Der Standardabstand oben gilt für alle Läufe ohne eigenen Wert.");
            g.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.ColumnIndex != 1 || e.RowIndex < 0 || cur == null) return;
                SetPufDist(e.RowIndex, Convert.ToString(g[1, e.RowIndex].EditedFormattedValue));
            };
            g.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
        }

        int PufRuns() { PackageStep h = StepsView().Head; return h == null ? 0 : Math.Min(PackageStep.MaxRuns, h.Days(pk.Day1).Count); }

        internal void SetPufDist(int run, string text)
        {
            int v, have; cur.Shift.TryGetValue(run, out have); have += cur.RunOffset;
            if (!int.TryParse((text ?? "").Trim(), out v)) { FillPuf(cur); return; }
            int delta = -v - have;
            if (delta == 0) { FillPuf(cur); return; }
            if (!StepsView().DragRun(cur, run, delta, true)) { FillPuf(cur); Say("Dieser Abstand ist nicht möglich."); return; }
            AfterMove(cur);
        }

        string PufInfo(PackageStep s)
        {
            int n = PufRuns();
            if (!IsJe(s) || n == 0) return "Puffer · bedient den jeweils nächsten Lauf";
            Package p = StepsView(); string a = p.LaufNoOf(1, pk.Day1), b = p.LaufNoOf(n, pk.Day1);
            return "Puffer · bedient " + (n > 1 ? "Lauf 1–" + n : "Lauf 1") + (a.Length > 0 ? " (" + a + (n > 1 && b.Length > 0 ? " – " + b : "") + ")" : "");
        }

        void FillPuf(PackageStep s)
        {
            bool was = loading; loading = true;
            DataGridView g = gridPuf; int n = PufRuns();
            g.Rows.Clear();
            for (int r = 0; r < n; r++) { int v; s.Shift.TryGetValue(r, out v); g.Rows.Add("Lauf " + (r + 1), (-(s.RunOffset + v)).ToString()); }
            g.Height = g.ColumnHeadersHeight + Math.Min(n, 5) * g.RowTemplate.Height + 2;
            loading = was;
        }

        // Anzeige statt "Art" (Puffer) und die Tabelle unter "Wann?"
        int LayoutPufInfo(int y, PackageStep s)
        {
            lPufInfo.Text = IsPuffer(s) ? PufInfo(s) : ""; tip.SetToolTip(lPufInfo, lPufInfo.Text);
            return Row(y, lPufInfo.Text.Length > 0, lPufInfo);
        }

        int LayoutPufTable(int y, PackageStep s)
        {
            bool show = IsJe(s) && s.IsBuffer && PufRuns() > 0;
            gridPuf.Left = 8; gridPuf.Width = gWhen.Width - 16;
            return Row(y, show, gridPuf);
        }
    }
}
