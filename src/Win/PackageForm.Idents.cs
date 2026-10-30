using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Kennung je Lauf (Startwert + Tabelle Lauf | Datum | Laufnummer | Kennung | faellt aus) und Materialvorlage fuer Notizen.
    // Die Kennung wird je geplanter Laufposition hochgezaehlt; ein ausgefallener Lauf laesst eine Luecke (Folgelaeufe ruecken nicht nach);
    // jede Zelle bleibt von Hand aenderbar (IdentOver, "-" = ausgesetzt).
    partial class PackageForm
    {
        readonly DataGridView gridId = new DataGridView();
        readonly Button bTemplate = new Button();
        readonly ContextMenuStrip menuId = new ContextMenuStrip();
        Font fIdBold, fIdStrike;
        bool showIdents;
        const int ColRun = 0, ColDate = 1, ColNo = 2, ColId = 3, ColSkip = 4;
        const string Dash = "—";

        void BuildIdents()
        {
            DataGridView g = gridId;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = g.AllowUserToResizeRows = g.AllowUserToResizeColumns = false;
            g.RowHeadersVisible = false; g.MultiSelect = false; g.SelectionMode = DataGridViewSelectionMode.CellSelect;
            g.BackgroundColor = SystemColors.Window; g.BorderStyle = BorderStyle.FixedSingle; g.Anchor = TLR;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.RowTemplate.Height = Font.Height + 6; g.ColumnHeadersHeight = Font.Height + 8; g.Height = 3 * g.RowTemplate.Height;
            fIdBold = new Font(Font, FontStyle.Bold); fIdStrike = new Font(Font, FontStyle.Strikeout);
            string[] heads = { "Lauf", "Datum", "Laufnummer", "Kennung", "fällt aus" };
            for (int i = 0; i < 5; i++)
            {
                DataGridViewColumn c = i == ColSkip ? (DataGridViewColumn)new DataGridViewCheckBoxColumn() : new DataGridViewTextBoxColumn();
                c.HeaderText = heads[i]; c.SortMode = DataGridViewColumnSortMode.NotSortable; c.ReadOnly = i != ColId && i != ColSkip;
                c.AutoSizeMode = i == ColId ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None;
                c.Width = i == ColRun ? 44 : i == ColDate ? 150 : i == ColNo ? 90 : 70;
                g.Columns.Add(c);
            }
            gExtra.Controls.Add(g);
            tip.SetToolTip(g, "Kennung je Lauf. Eintippen = von Hand (orange), Entf = aussetzen (—), Rechtsklick = auf Automatik zurück. „fällt aus“: der Lauf entfällt samt Kennung, Folgeläufe behalten ihre Nummern.");
            menuId.Items.Add("auf Automatik zurück", null, delegate { ResetIdent(gridId.CurrentCell != null ? gridId.CurrentCell.RowIndex : -1); });
            g.CellMouseDown += delegate(object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex != ColId) return;
                g.CurrentCell = g[e.ColumnIndex, e.RowIndex];
                menuId.Items[0].Enabled = cur != null && cur.IdentOver.ContainsKey(e.RowIndex);
                menuId.Show(Cursor.Position);
            };
            g.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e) { if (e.ColumnIndex == ColId && e.RowIndex >= 0) SetIdent(e.RowIndex, Convert.ToString(g[ColId, e.RowIndex].EditedFormattedValue)); };
            g.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Delete || g.CurrentCell == null || g.CurrentCell.ColumnIndex != ColId || g.IsCurrentCellInEditMode) return;
                SetIdent(g.CurrentCell.RowIndex, ""); e.Handled = true;
            };
            g.CurrentCellDirtyStateChanged += delegate { if (g.CurrentCell is DataGridViewCheckBoxCell && g.IsCurrentCellDirty) g.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            g.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (loading || e.ColumnIndex != ColSkip || e.RowIndex < 0 || cur == null) return;
                PackageStep h = StepsView().Head;
                if (h == null) return;
                h.SetSkip(e.RowIndex, true.Equals(g[ColSkip, e.RowIndex].Value));
                Changed(); RefreshIdents();
            };
            g.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };

            Btn(bTemplate, "Materialvorlage einfügen", 8, 0, 190, delegate { InsertTemplate(); });
            gExtra.Controls.Add(bTemplate);
            tip.SetToolTip(bTemplate, "Fügt die Notiz-Vorlage des Puffer-Bausteins (Einstellungen → Equipment & Bausteine) als Notizen ein; je Zeile eine Notiz.");
        }

        static string AutoIdent(PackageStep s, int run) { return s.IdentStart.Length > 0 ? PackageStep.IdentAt(s.IdentStart, run) : ""; }

        // Text von Hand: leer/"—" = aussetzen, gleich der Automatik = Ueberschreibung entfernen
        internal void SetIdent(int run, string text)
        {
            if (cur == null || run < 0) return;
            string t = (text ?? "").Trim(), auto = AutoIdent(cur, run);
            if (t.Length == 0 || t == Dash || t == PackageStep.IdentOff) { if (auto.Length == 0) cur.IdentOver.Remove(run); else cur.IdentOver[run] = PackageStep.IdentOff; }
            else if (t == auto) cur.IdentOver.Remove(run);
            else cur.IdentOver[run] = t;
            Changed(); RefreshIdents();
        }

        internal void ResetIdent(int run)
        {
            if (cur == null || run < 0 || !cur.IdentOver.Remove(run)) return;
            Changed(); RefreshIdents();
        }

        // Tabelle neu aufbauen (Schrittwechsel, Laufanzahl) bzw. nur Werte/Formate nachziehen
        void FillIdents()
        {
            PackageStep s = cur; int n = s != null ? StepsView().RunCountOf(s) : 0;
            showIdents = s != null && IdentVisible(s) && ((n > 1 && s.Kind == StepKind.Run) || s.IdentStart.Length > 0 || s.IdentOver.Count > 0);
            bool was = loading; loading = true;
            if (gridId.Rows.Count != (showIdents ? n : 0))
            {
                gridId.Rows.Clear();
                for (int i = 0; showIdents && i < n; i++) gridId.Rows.Add();
            }
            gridId.Height = gridId.ColumnHeadersHeight + Math.Min(Math.Max(n, 1), 6) * gridId.RowTemplate.Height + 4;
            loading = was;
            RefreshIdents();
        }

        void RefreshIdents()
        {
            PackageStep s = cur; if (s == null || !showIdents) return;
            Package p = StepsView(); PackageStep h = p.Head; int si = steps.IndexOf(s);
            bool main = h != null && (s == h || s.Bind == StepBind.JeLauf);
            List<RunDate> plan = Plan(); List<int> hd = h != null ? h.Days(p.Day1) : new List<int>();
            bool was = loading; loading = true;
            for (int r = 0; r < gridId.Rows.Count; r++)
            {
                DataGridViewRow row = gridId.Rows[r];
                bool skip = main && h.Skips(r);
                int day = -1;
                foreach (RunDate d in plan) if (d.Step == si && d.Run == r) { day = d.Day; break; }
                if (day < 0 && main && r < hd.Count) day = hd[r] + (s == h ? 0 : s.RunOffset);
                string no = s.Numbered ? s.RunNo(prefix, r) : main ? h.RunNo(prefix, r) : "";
                string over; bool isOver = s.IdentOver.TryGetValue(r, out over);
                string id = isOver ? (over == PackageStep.IdentOff ? Dash : over) : AutoIdent(s, r);
                row.Cells[ColRun].Value = (r + 1).ToString();
                row.Cells[ColDate].Value = day > 0 ? StripHead(day, true) : "";
                row.Cells[ColNo].Value = no; row.Cells[ColId].Value = id; row.Cells[ColSkip].Value = skip;
                row.Cells[ColSkip].ReadOnly = !main;
                Color fore = skip ?Section.HelpColor : Color.Empty;
                foreach (DataGridViewCell c in row.Cells) { c.Style.ForeColor = fore; c.Style.SelectionForeColor = fore; c.Style.Font = null; }
                if (skip) row.Cells[ColNo].Style.Font = fIdStrike;
                if (isOver && !skip) { Color o = Color.DarkOrange; row.Cells[ColId].Style.ForeColor = o; row.Cells[ColId].Style.SelectionForeColor = o; row.Cells[ColId].Style.Font = fIdBold; }
            }
            loading = was;
        }

        // Vorlage des Puffer-Bausteins als Notizen ans Ende des Notizfelds (vorhandene Zeilen nicht doppelt)
        internal void InsertTemplate()
        {
            if (cur == null) return;
            string t = cat.NoteTemplateFor(cur); if (t.Length == 0) return;
            List<string> have = cur.NoteTexts();
            foreach (string l in t.Replace("\r", "").Split('\n')) { string x = l.Trim(); if (x.Length > 0 && !have.Contains(x)) have.Add(x); }
            tNote.Text = string.Join("\r\n", have.ToArray());
        }
    }
}
