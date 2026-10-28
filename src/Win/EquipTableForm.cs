using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Tabellenansicht der Equipment-Stammdaten: eine Zeile je Geraet, die Eigenschaften der Geraeteart als Spalten (alle Arten = Gesamtliste).
    // Sortieren per Klick auf die Spaltenkoepfe, Filter per Text; Felder mit Namensende "(Link)" sind anklickbar. Doppelklick waehlt das Geraet.
    class EquipTableForm : Form
    {
        const string All = "(alle Geräte)";
        readonly EquipMaster m;
        readonly ComboBox cbClass = new ComboBox();
        readonly TextBox filter = new TextBox();
        readonly CheckBox cbMaint = new CheckBox();
        readonly DataGridView grid = new DataGridView();
        readonly Label count = new Label();
        public string Picked;   // per Doppelklick gewaehltes Geraet

        public EquipTableForm(EquipMaster master, string cls)
        {
            m = master;
            Text = "Equipment-Tabelle";
            Ui.Dialog(this, 980, 560);
            Label l1 = new Label(); l1.Text = "Geräteart:"; l1.AutoSize = true; l1.Location = new Point(8, 11); Controls.Add(l1);
            cbClass.DropDownStyle = ComboBoxStyle.DropDownList; cbClass.SetBounds(70, 8, 200, 22); Controls.Add(cbClass);
            cbClass.Items.Add(All); foreach (EquipClass c in m.Classes) cbClass.Items.Add(c.Name);
            cbClass.SelectedItem = cls != null && cbClass.Items.Contains(cls) ? cls : All;
            Label l2 = new Label(); l2.Text = "Filter:"; l2.AutoSize = true; l2.Location = new Point(290, 11); Controls.Add(l2);
            filter.SetBounds(330, 8, 180, 22); Ui.SimpleMenu(filter); Controls.Add(filter);
            cbMaint.Text = "nur Geräte in Wartung"; cbMaint.AutoSize = true; cbMaint.Location = new Point(530, 10); Controls.Add(cbMaint);
            count.AutoSize = true; count.Location = new Point(700, 11); count.ForeColor = SystemColors.GrayText; Controls.Add(count);

            grid.SetBounds(8, 40, 956, 480); grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false;
            grid.BackgroundColor = SystemColors.Window; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(grid.Columns[e.ColumnIndex] is DataGridViewLinkColumn)) return;
                OpenLink(this, Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
            };
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex] is DataGridViewLinkColumn) return;
                Picked = Convert.ToString(grid.Rows[e.RowIndex].Cells[0].Value); DialogResult = DialogResult.OK;
            };
            Controls.Add(grid);
            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(884, 526, 80, 26); close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; close.DialogResult = DialogResult.Cancel;
            CancelButton = close; Controls.Add(close);
            Label hint = Ui.Hint("Doppelklick auf eine Zeile öffnet das Gerät in den Stammdaten. Felder mit „(Link)“ am Namen sind Links.", 8, 531, 800);
            hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; Controls.Add(hint);

            cbClass.SelectedIndexChanged += delegate { Fill(); };
            filter.TextChanged += delegate { Fill(); };
            cbMaint.CheckedChanged += delegate { Fill(); };
            Fill();
        }

        // Felder in der Reihenfolge: Vorlage der Art(en), danach weitere Namen, die an Geraeten vorkommen
        List<string> Fields(List<EquipDevice> devs, string cls)
        {
            List<string> f = new List<string>();
            foreach (EquipClass c in m.Classes) if (cls == All || c.Name == cls) foreach (string n in c.Fields) if (!f.Contains(n)) f.Add(n);
            foreach (EquipDevice d in devs) foreach (EquipProp p in d.Props) if (!f.Contains(p.Name)) f.Add(p.Name);
            return f;
        }

        static string Prop(EquipDevice d, string name)
        {
            foreach (EquipProp p in d.Props) if (p.Name == name) return p.Value;
            return "";
        }

        string Groups(EquipDevice d)
        {
            List<string> g = new List<string>();
            foreach (EquipSeries s in m.SeriesOf(d.Id)) if (s.Class.Length == 0 || s.Class == d.Class) g.Add(s.Name);
            return string.Join(", ", g.ToArray());
        }

        bool Hit(EquipDevice d, string q)
        {
            if (q.Length == 0) return true;
            if (Contains(d.Id, q) || Contains(d.Class, q) || Contains(d.Note, q) || Contains(d.Parent, q) || Contains(Groups(d), q)) return true;
            foreach (EquipProp p in d.Props) if (Contains(p.Value, q)) return true;
            return false;
        }

        static bool Contains(string s, string q) { return s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        void Fill()
        {
            string cls = Convert.ToString(cbClass.SelectedItem), q = filter.Text.Trim();
            List<EquipDevice> devs = new List<EquipDevice>();
            foreach (EquipDevice d in m.Devices)
                if ((cls == All || d.Class == cls) && (!cbMaint.Checked || !d.Active) && Hit(d, q)) devs.Add(d);
            List<string> fields = Fields(cls == All ? m.Devices : devs, cls);

            grid.SuspendLayout();
            grid.Columns.Clear();
            AddCol("ID", false); AddCol("Geräteart", false); AddCol("Gruppe", false); AddCol("Gehört zu", false); AddCol("Zustand", false);
            foreach (string f in fields) AddCol(f, EquipClass.IsLink(f));
            AddCol("Notiz", false);
            foreach (EquipDevice d in devs)
            {
                List<object> row = new List<object>();
                row.Add(d.Id); row.Add(d.Class); row.Add(Groups(d)); row.Add(d.Parent); row.Add(d.Active ? "verfügbar" : "in Wartung");
                foreach (string f in fields) row.Add(Prop(d, f));
                row.Add(d.Note.Replace("\r", "").Replace("\n", " "));
                int i = grid.Rows.Add(row.ToArray());
                grid.Rows[i].Tag = d;
                if (!d.Active) grid.Rows[i].DefaultCellStyle.ForeColor = Color.Firebrick;
                grid.Rows[i].Cells[0].ToolTipText = m.Info(d.Id);
            }
            grid.ResumeLayout();
            count.Text = devs.Count + " von " + m.Devices.Count + " Geräten";
        }

        void AddCol(string name, bool link)
        {
            DataGridViewColumn c = link ? (DataGridViewColumn)new DataGridViewLinkColumn() : new DataGridViewTextBoxColumn();
            c.HeaderText = name; c.SortMode = DataGridViewColumnSortMode.Automatic;
            if (name == "Notiz") c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(c);
        }

        // Nur Web-Adressen, Laufwerks- und Netzpfade; ausfuehrbare Dateitypen werden nie geoeffnet.
        public static bool OpenLink(IWin32Window owner, string url)
        {
            url = (url ?? "").Trim();
            if (url.Length == 0) return false;
            bool web = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool path = url.StartsWith("\\\\") || (url.Length > 2 && url[1] == ':' && (url[2] == '\\' || url[2] == '/')) || url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
            string low = url.ToLowerInvariant();
            foreach (string x in new[] { ".exe", ".bat", ".cmd", ".com", ".scr", ".ps1", ".vbs", ".js", ".msi", ".lnk", ".hta", ".jar" })
                if (!web && low.EndsWith(x)) path = false;
            if (!web && !path)
            {
                MessageBox.Show(owner, "Dieser Link kann nicht geöffnet werden (erlaubt: http(s)://, Laufwerks- oder Netzpfad, keine Programme):\n\n" + url, "Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            try { Process.Start(url); return true; }
            catch (Exception ex) { MessageBox.Show(owner, "Der Link konnte nicht geöffnet werden:\n" + ex.Message, "Link", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        }

#if TESTHOOKS
        public int TestRows { get { return grid.Rows.Count; } }
        public int TestColumns { get { return grid.Columns.Count; } }
        public bool TestHasLinkColumn { get { foreach (DataGridViewColumn c in grid.Columns) if (c is DataGridViewLinkColumn) return true; return false; } }
        public void TestFilter(string cls, string q) { cbClass.SelectedItem = cls; filter.Text = q; }
        public string TestCell(int row, string header) { foreach (DataGridViewColumn c in grid.Columns) if (c.HeaderText == header) return Convert.ToString(grid.Rows[row].Cells[c.Index].Value); return null; }
#endif
    }
}
