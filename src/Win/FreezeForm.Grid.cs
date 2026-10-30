using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Cycle-Tabelle des Freeze-Dialogs: Cycle | Anlage | geplant | tatsaechlicher Start (editierbar, leer = berechnet) | Ende | Verzoegerung | zuruecksetzen.
    // Jede Eingabe rechnet sofort ueber FreezeCalc neu: ein korrigierter Start zieht die Folge-Cycles derselben Anlage nach.
    partial class FreezeForm
    {
        readonly DataGridView grid = new DataGridView();
        string msg = "";   // Hinweis zur letzten Eingabe (z. B. Zeit nicht lesbar)
        Font fBold, fItal;
        bool selCleared;

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (fBold != null) fBold.Dispose(); if (fItal != null) fItal.Dispose(); }
            base.Dispose(disposing);
        }
        const int ColStart = 3, ColReset = 6;

        void BuildGrid(int y)
        {
            grid.SetBounds(LblX, y, 756, 306);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.AllowUserToResizeColumns = false;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.ScrollBars = ScrollBars.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight = 22;
            grid.RowTemplate.Height = 20; grid.BackgroundColor = SystemColors.Window; grid.BorderStyle = BorderStyle.FixedSingle; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 241, 246); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(45, 55, 75);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 236, 190); grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.GridColor = Color.FromArgb(214, 219, 228);
            AddCol("Zyklus", 60, true); AddCol("Anlage", 70, true); AddCol("geplanter Start", 150, true); AddCol("tatsächlicher Start", 160, false); AddCol("Ende", 130, true); AddCol("Verzögerung", 90, true);
            DataGridViewLinkColumn lk = new DataGridViewLinkColumn(); lk.HeaderText = ""; lk.Width = 96; lk.SortMode = DataGridViewColumnSortMode.NotSortable; lk.ReadOnly = true;
            lk.LinkColor = lk.VisitedLinkColor = Color.FromArgb(0, 105, 180); grid.Columns.Add(lk);
            grid.Columns[ColStart].HeaderCell.ToolTipText = "Zeit eintragen (z. B. 02:30 oder 05.03. 02:30). Leer = berechnet.";
            grid.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e) { if (!loading && e.RowIndex >= 0 && e.ColumnIndex == ColStart) IstEdited(e.RowIndex); };
            grid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (ch.ReadOnly || e.RowIndex < 0 || e.ColumnIndex != ColReset || !it.Ist.ContainsKey(e.RowIndex + 1)) return;
                it.Ist.Remove(e.RowIndex + 1); msg = ""; Recalc();
            };
            Controls.Add(grid); grid.TabIndex = tab++;
            grid.ReadOnly = ch.ReadOnly;
        }

        void AddCol(string title, int w, bool ro)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = title; c.Width = w; c.ReadOnly = ro; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        static string Stamp(DateTime d) { return d.ToString("ddd dd.MM. HH:mm", Ui.De); }

        // Dialogfelder lesen, alle Cycles neu rechnen und die Tabelle (ohne Neuaufbau der Zeilen) bzw. die Hinweise aktualisieren
        void Recalc()
        {
            if (loading) return;
            Collect();
            List<FreezeCycle> cy = FreezeCalc.Cycles(it);
            Font plain = grid.Font;
            if (fBold == null) { fBold = new Font(plain, FontStyle.Bold); fItal = new Font(plain, FontStyle.Italic); }
            Font bold = fBold, ital = fItal;
            loading = true;
            try
            {
                int n = it.Art == FreezeArt.Bag ? cy.Count : 0;
                while (grid.Rows.Count < n) grid.Rows.Add();
                while (grid.Rows.Count > n) grid.Rows.RemoveAt(grid.Rows.Count - 1);
                for (int i = 0; i < n; i++)
                {
                    FreezeCycle c = cy[i]; DataGridViewRow r = grid.Rows[i];
                    r.DefaultCellStyle.BackColor = FreezeStyle.AnlageTint(c.Anlage);
                    r.Cells[0].Value = c.Label;
                    r.Cells[1].Value = "FA" + c.Anlage; r.Cells[1].Style.ForeColor = FreezeStyle.Anlage(c.Anlage); r.Cells[1].Style.Font = bold;
                    r.Cells[2].Value = Stamp(c.Planned) + (c.Estimated ? "  (geschätzt)" : ""); r.Cells[2].Style.Font = c.Estimated ? ital : plain;
                    r.Cells[ColStart].Value = Stamp(c.Actual);
                    r.Cells[ColStart].Style.Font = c.HasIst ? bold : plain; r.Cells[ColStart].Style.ForeColor = c.HasIst ? Color.Black : Color.FromArgb(105, 112, 128);
                    r.Cells[4].Value = Stamp(c.End);
                    r.Cells[5].Value = FreezeCalc.DelayText(c.DelayMin); r.Cells[5].Style.ForeColor = FreezeStyle.Delay; r.Cells[5].Style.Font = bold;
                    r.Cells[ColReset].Value = c.HasIst && !ch.ReadOnly ? "zurücksetzen" : "";
                }
            }
            finally { loading = false; }
            if (!selCleared) { grid.ClearSelection(); grid.CurrentCell = null; selCleared = grid.Rows.Count > 0; }   // keine Vorauswahl
            if (it.Art == FreezeArt.Tank && it.Valid) lbEndV.Text = Stamp(cy[0].End);
            List<string> hints = FreezeCalc.Hints(it);
            if (msg.Length > 0) { lbHint.ForeColor = Color.FromArgb(185, 30, 30); lbHint.Text = msg; }
            else if (hints.Count > 0) { lbHint.ForeColor = Color.FromArgb(170, 80, 0); lbHint.Text = "Hinweis: " + string.Join("\n", hints.ToArray()); }
            else { lbHint.ForeColor = Section.HelpColor; lbHint.Text = it.Art == FreezeArt.Bag ? "Zyklus n läuft auf Anlage ((n − 1) mod 3) + 1. Gespeichert werden nur Ihre Eingaben; alle weiteren Zeiten werden berechnet." : "Der Tank-Freeze hat einen Start und eine Dauer."; }
        }

        // Eingabe in "tatsaechlicher Start": leer oder gleich dem berechneten Wert = kein Ist; sonst Ist setzen (Folge-Cycles derselben Anlage folgen)
        void IstEdited(int row)
        {
            int n = row + 1;
            string s = (grid[ColStart, row].Value ?? "").ToString().Trim();
            FreezeItem probe = it.Clone(); probe.Ist.Remove(n);
            DateTime calc = FreezeCalc.Actual(probe, n), t;
            msg = "";
            if (s.Length == 0) it.Ist.Remove(n);
            else if (!ParseTime(s, calc, out t)) msg = "Die Zeit \"" + s + "\" ist nicht lesbar. Bitte so eintragen: 02:30 oder 05.03. 02:30.";
            else if (t == calc) it.Ist.Remove(n);
            else it.Ist[n] = t;
            Recalc();
        }

        static readonly Regex Full = new Regex(@"^\s*(?:[^\d\s]+\s+)?(\d{1,2})\.(\d{1,2})\.(?:(\d{2,4}))?\s+(\d{1,2}):(\d{2})\s*$", RegexOptions.Compiled);
        static readonly Regex TimeOnly = new Regex(@"^\s*(\d{1,2})[:.](\d{2})\s*$", RegexOptions.Compiled);

        // "HH:mm" (Tag wie berechnet), "TT.MM. HH:mm" (Jahr: das naechstliegende), "TT.MM.JJJJ HH:mm"; auch mit Wochentag davor
        public static bool ParseTime(string s, DateTime calc, out DateTime t)
        {
            t = DateTime.MinValue;
            try
            {
                Match m = TimeOnly.Match(s);
                if (m.Success) { t = calc.Date.AddHours(int.Parse(m.Groups[1].Value)).AddMinutes(int.Parse(m.Groups[2].Value)); return int.Parse(m.Groups[1].Value) < 24 && int.Parse(m.Groups[2].Value) < 60; }
                m = Full.Match(s);
                if (!m.Success) return false;
                int d = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value), h = int.Parse(m.Groups[4].Value), mi = int.Parse(m.Groups[5].Value);
                if (h > 23 || mi > 59) return false;
                if (m.Groups[3].Success) { int y = int.Parse(m.Groups[3].Value); if (y < 100) y += 2000; t = new DateTime(y, mo, d, h, mi, 0); return true; }
                double best = double.MaxValue;
                for (int y = calc.Year - 1; y <= calc.Year + 1; y++)
                {
                    if (d > DateTime.DaysInMonth(y, mo)) continue;
                    DateTime c = new DateTime(y, mo, d, h, mi, 0); double dist = Math.Abs((c - calc).TotalDays);
                    if (dist < best) { best = dist; t = c; }
                }
                return best < double.MaxValue;
            }
            catch (ArgumentException) { return false; }   // z. B. Monat 13
            catch (FormatException) { return false; }
        }

#if TESTHOOKS
        // Selbsttest: Eingabe wie im Dialog (Zelle "tatsaechlicher Start" des Cycles n setzen und uebernehmen), Tabellenwerte lesen
        public void TestSetIst(int n, string text) { grid[ColStart, n - 1].Value = text; IstEdited(n - 1); }
        public string TestCell(int n, int col) { return grid.Rows.Count >= n ? (grid[col, n - 1].Value ?? "").ToString() : ""; }
        public int TestRows { get { return grid.Rows.Count; } }
        public string TestHint { get { return lbHint.Text; } }
        public void TestClickReset(int n) { if (it.Ist.ContainsKey(n)) { it.Ist.Remove(n); msg = ""; Recalc(); } }
        public FreezeItem TestItem { get { Collect(); return it; } }
        public void TestSetArt(bool bag) { rBag.Checked = bag; rTank.Checked = !bag; }
        public bool TestBagVisible { get { return bagMode; } }   // Control.Visible ist ohne angezeigtes Fenster immer false
        public bool TestTankVisible { get { return !bagMode; } }
        public string TestEnd { get { return lbEndV.Text; } }
        public void TestSetStart(DateTime c1) { dC1.Value = c1; }
        public void TestSetGap(decimal h) { nGap.Value = h; }
        public void TestSetMain(string s) { cMain.Text = s; }
        public string TestCheck() { Collect(); return Check(); }
#endif
    }
}
