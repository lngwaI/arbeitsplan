using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Matrizen: (1) "Passt zu" zwischen zwei Geraetearten (z. B. Filtergehaeuse x Skid), Klick schaltet das Paar um, Notiz je Paar;
    // (2) Produkt x Geraet (aus den Produktprofilen, nur Ansicht). Arbeitet direkt auf den uebergebenen Stammdaten (Speichern im Eltern-Dialog).
    class EquipMatrixForm : Form
    {
        readonly Catalog cat;
        readonly EquipMaster m;
        readonly bool edit;
        readonly TabControl tabs = new TabControl();
        readonly ComboBox cbRows = new ComboBox(), cbCols = new ComboBox(), cbProdRows = new ComboBox();
        readonly DataGridView grid = new DataGridView(), prod = new DataGridView();
        readonly TextBox note = new TextBox();
        readonly Button btnNote = new Button();
        readonly Label info = new Label(), prodInfo = new Label();
        bool loading;

        public EquipMatrixForm(Catalog catalog, bool canEdit, string rowsCls, string colsCls)
        {
            cat = catalog; m = cat.Master; edit = canEdit;
            Text = "Equipment-Matrix";
            Ui.Dialog(this, 900, 560);
            tabs.SetBounds(6, 6, 880, 510); Controls.Add(tabs);
            TabPage t1 = new TabPage("Passt zu (Art × Art)"), t2 = new TabPage("Produkt × Gerät");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2);

            t1.Controls.Add(L("Zeilen (Geräteart):", 8, 12)); Drop(cbRows, 116, 9, 170); t1.Controls.Add(cbRows);
            t1.Controls.Add(L("Spalten (Geräteart):", 310, 12)); Drop(cbCols, 428, 9, 170); t1.Controls.Add(cbCols);
            info.SetBounds(620, 6, 250, 32); info.ForeColor = SystemColors.GrayText; t1.Controls.Add(info);
            SetupGrid(grid, 8, 44, 856, 340); t1.Controls.Add(grid);
            t1.Controls.Add(L("Notiz zum markierten Paar:", 8, 396));
            note.SetBounds(8, 414, 560, 22); Ui.SimpleMenu(note); t1.Controls.Add(note);
            btnNote.Text = "Notiz übernehmen"; btnNote.SetBounds(576, 413, 120, 24); t1.Controls.Add(btnNote);
            t1.Controls.Add(Ui.Hint("Klick auf eine Zelle: „✓“ = passt zu. „(✓)“ gilt über eine Regel für eine ganze Art oder Gruppe und wird dort geändert.", 8, 448, 850));

            t2.Controls.Add(L("Geräteart (Zeilen):", 8, 12)); Drop(cbProdRows, 116, 9, 170); t2.Controls.Add(cbProdRows);
            prodInfo.SetBounds(310, 6, 550, 32); prodInfo.ForeColor = SystemColors.GrayText; t2.Controls.Add(prodInfo);
            SetupGrid(prod, 8, 44, 856, 420); t2.Controls.Add(prod);

            foreach (EquipClass c in m.Classes) { cbRows.Items.Add(c.Name); cbCols.Items.Add(c.Name); cbProdRows.Items.Add(c.Name); }
            Sel(cbRows, rowsCls, 0); Sel(cbCols, colsCls, 1); Sel(cbProdRows, rowsCls, 0);

            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(806, 522, 80, 26); close.DialogResult = DialogResult.Cancel; CancelButton = close; Controls.Add(close);

            cbRows.SelectedIndexChanged += delegate { FillPairs(); }; cbCols.SelectedIndexChanged += delegate { FillPairs(); };
            cbProdRows.SelectedIndexChanged += delegate { FillProducts(); };
            grid.CellClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex > 0) ClickPair(e.RowIndex, e.ColumnIndex); };
            grid.SelectionChanged += delegate { ShowNote(); };
            btnNote.Click += delegate { SaveNote(); };
            FillPairs(); FillProducts();
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }
        static void Drop(ComboBox c, int x, int y, int w) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.SetBounds(x, y, w, 22); }
        static void Sel(ComboBox c, string v, int fallback) { if (v != null && c.Items.Contains(v)) c.SelectedItem = v; else if (c.Items.Count > 0) c.SelectedIndex = Math.Min(fallback, c.Items.Count - 1); }

        static void SetupGrid(DataGridView g, int x, int y, int w, int h)
        {
            g.SetBounds(x, y, w, h); g.ReadOnly = true; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false; g.RowHeadersVisible = false;
            g.BackgroundColor = SystemColors.Window; g.SelectionMode = DataGridViewSelectionMode.CellSelect; g.MultiSelect = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells; g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        List<EquipDevice> Of(string cls) { return m.Devices.FindAll(delegate(EquipDevice d) { return d.Class == cls; }); }

        void FillPairs()
        {
            string rc = Convert.ToString(cbRows.SelectedItem), cc = Convert.ToString(cbCols.SelectedItem);
            loading = true;
            grid.Columns.Clear(); grid.Rows.Clear();
            List<EquipDevice> cols = Of(cc), rows = Of(rc);
            DataGridViewColumn first = new DataGridViewTextBoxColumn(); first.HeaderText = rc + " \\ " + cc; first.Frozen = true; grid.Columns.Add(first);
            foreach (EquipDevice c in cols) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = c.Id });
            foreach (EquipDevice r in rows)
            {
                int i = grid.Rows.Add(); grid.Rows[i].Cells[0].Value = r.Id; grid.Rows[i].Cells[0].Style.BackColor = SystemColors.Control;
                for (int j = 0; j < cols.Count; j++) PaintPair(i, j + 1, r.Id, cols[j].Id);
            }
            loading = false;
            info.Text = rows.Count + " × " + cols.Count + " Geräte" + (edit ? "" : " (nur Ansicht)");
            ShowNote();
        }

        string Direct(string a, string b)   // "✓", "(✓)" (nur ueber Art/Gruppe), "" (nein)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return "–";
            if (!m.HasPair(a, b)) return "";
            foreach (EquipLink l in m.Links)
            {
                if (string.Equals(l.From, a, StringComparison.OrdinalIgnoreCase) && l.To.Exists(delegate(string t) { return string.Equals(t, b, StringComparison.OrdinalIgnoreCase); })) return "✓";
                if (string.Equals(l.From, b, StringComparison.OrdinalIgnoreCase) && l.To.Exists(delegate(string t) { return string.Equals(t, a, StringComparison.OrdinalIgnoreCase); })) return "✓";
            }
            return "(✓)";
        }

        void PaintPair(int row, int col, string a, string b)
        {
            DataGridViewCell c = grid.Rows[row].Cells[col];
            string v = Direct(a, b);
            c.Value = v;
            c.Style.BackColor = v == "✓" ? Color.FromArgb(198, 239, 206) : v == "(✓)" ? Color.FromArgb(235, 245, 235) : v == "–" ? SystemColors.Control : SystemColors.Window;
            string n = m.PairNote(a, b);
            c.ToolTipText = n.Length > 0 ? n : (v == "(✓)" ? "über Regel für Art/Gruppe" : "");
            if (n.Length > 0 && v == "✓") c.Value = "✓ *";
        }

        string IdAt(int row, int col) { return col == 0 ? Convert.ToString(grid.Rows[row].Cells[0].Value) : grid.Columns[col].HeaderText; }

        void ClickPair(int row, int col)
        {
            if (loading) return;
            string a = IdAt(row, 0), b = IdAt(row, col);
            string v = Convert.ToString(grid.Rows[row].Cells[col].Value);
            if (!edit || v == "–" || v == "(✓)") { ShowNote(); return; }
            m.SetPair(a, b, v.Length == 0);
            PaintPair(row, col, a, b);
            ShowNote();
        }

        bool Sel2(out string a, out string b)
        {
            a = b = null;
            DataGridViewCell c = grid.CurrentCell;
            if (c == null || c.ColumnIndex < 1) return false;
            a = IdAt(c.RowIndex, 0); b = IdAt(c.RowIndex, c.ColumnIndex);
            return true;
        }

        void ShowNote()
        {
            string a, b;
            bool ok = Sel2(out a, out b) && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase) && m.HasPair(a, b);
            note.Enabled = btnNote.Enabled = edit && ok;
            note.Text = ok ? m.PairNote(a, b) : "";
        }

        void SaveNote()
        {
            string a, b;
            if (!Sel2(out a, out b) || !m.HasPair(a, b)) return;
            m.SetPairNote(a, b, note.Text.Trim());
            PaintPair(grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex, a, b);
        }

        void FillProducts()
        {
            string rc = Convert.ToString(cbProdRows.SelectedItem);
            prod.Columns.Clear(); prod.Rows.Clear();
            DataGridViewColumn first = new DataGridViewTextBoxColumn(); first.HeaderText = rc; first.Frozen = true; prod.Columns.Add(first);
            foreach (ProductProfile p in cat.Products) prod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = p.Code + (p.Name.Length > 0 ? "\n" + p.Name : "") });
            foreach (EquipDevice d in Of(rc))
            {
                List<string> ok = cat.ProductsAllowing(d.Id);
                List<object> row = new List<object>(); row.Add(d.Id);
                foreach (ProductProfile p in cat.Products) row.Add(ok.Contains(p.Code) ? "✓" : "");
                int i = prod.Rows.Add(row.ToArray());
                for (int j = 0; j < cat.Products.Count; j++) if (ok.Contains(cat.Products[j].Code)) prod.Rows[i].Cells[j + 1].Style.BackColor = Color.FromArgb(198, 239, 206);
            }
            prodInfo.Text = cat.Products.Count == 0 ? "Noch keine Produkte angelegt (Einstellungen → Produkte && Equipment)."
                : "Zugelassen = Gerät steht an einer Position des Produkts (eigene Liste) oder im globalen Standard. Pflege unter Produkte && Equipment.";
        }

#if TESTHOOKS
        public string TestCell(string a, string b) { return Direct(a, b); }
        public void TestClick(string a, string b)
        {
            int r = -1, c = -1;
            for (int i = 0; i < grid.Rows.Count; i++) if (IdAt(i, 0) == a) r = i;
            for (int j = 1; j < grid.Columns.Count; j++) if (grid.Columns[j].HeaderText == b) c = j;
            if (r >= 0 && c >= 0) { grid.CurrentCell = grid.Rows[r].Cells[c]; ClickPair(r, c); }
        }
        public void TestNote(string text) { note.Text = text; SaveNote(); }
        public string TestProduct(string id, string code) { for (int i = 0; i < prod.Rows.Count; i++) if (Convert.ToString(prod.Rows[i].Cells[0].Value) == id) for (int j = 1; j < prod.Columns.Count; j++) if (prod.Columns[j].HeaderText.StartsWith(code)) return Convert.ToString(prod.Rows[i].Cells[j].Value); return null; }
        public void TestTab(int i) { tabs.SelectedIndex = i; }
#endif
    }
}
