using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Kampagne/Ablauf planen (wann, welcher Block, was, welches Equipment, Ausweich, Notiz),
    // als Datei speichern/laden und in die Woche uebernehmen.
    class PackageForm : Form
    {
        readonly IDataStore store;
        readonly IEquipmentStatusProvider equip;
        readonly bool canApply;
        readonly TextBox tName = new TextBox();
        readonly DateTimePicker dtStart = new DateTimePicker();
        readonly ComboBox cbMode = new ComboBox();
        readonly DataGridView grid = new DataGridView();
        readonly Label info = new Label();
        List<PackageBlock> blocks = new List<PackageBlock>();
        public bool Applied;

        public PackageForm(IDataStore store, IEquipmentStatusProvider equip, DateTime weekStart, Package initial, bool canApply)
        {
            this.store = store; this.equip = equip; this.canApply = canApply;
            Text = "Paket-Editor";
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 560);
            MinimumSize = new Size(900, 420);

            Label l1 = new Label(); l1.Text = "Name:"; l1.SetBounds(10, 14, 45, 20);
            tName.SetBounds(58, 10, 260, 22);
            Label l2 = new Label(); l2.Text = "Start (Tag 1):"; l2.SetBounds(335, 14, 85, 20);
            dtStart.Format = DateTimePickerFormat.Short; dtStart.Value = weekStart; dtStart.SetBounds(422, 10, 110, 22);
            Label l3 = new Label(); l3.Text = "Übernehmen:"; l3.SetBounds(550, 14, 80, 20);
            cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.SetBounds(632, 10, 260, 22);
            cbMode.Items.AddRange(new object[] { "Nur leere Blöcke füllen", "Vorhandenes überschreiben", "An vorhandenen Text anhängen" });
            cbMode.SelectedIndex = 0;
            info.SetBounds(10, 38, 1020, 18);

            grid.SetBounds(10, 60, 1020, 440);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AllowUserToAddRows = false; grid.RowHeadersVisible = false; grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.BackgroundColor = SystemColors.Window;
            AddText("Tag", 45);
            DataGridViewComboBoxColumn rc = new DataGridViewComboBoxColumn(); rc.HeaderText = "Block"; rc.Width = 120; rc.FlatStyle = FlatStyle.Flat;
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) rc.Items.Add(r.Name);
            grid.Columns.Add(rc);
            AddText("Tätigkeit / Lauf", 170); AddText("Equipment", 210); AddBtn(); AddText("Ausweich-Equipment", 170); AddBtn(); AddText("Notiz", 190);
            grid.CellContentClick += OnCellClick;

            int y = 510;
            Button bAdd = Btn("+ Schritt", 10, y, 90), bDel = Btn("Entfernen", 106, y, 90);
            Button bOpen = Btn("Öffnen…", 220, y, 90), bSave = Btn("Speichern…", 316, y, 100);
            Button bApply = Btn("In Plan übernehmen", 760, y, 160), bClose = Btn("Schließen", 930, y, 100);
            bApply.Enabled = canApply;
            bAdd.Click += delegate { AddStep(new PackageStep()); };
            bDel.Click += delegate { if (grid.CurrentRow != null) grid.Rows.Remove(grid.CurrentRow); };
            bOpen.Click += delegate { OpenFile(); };
            bSave.Click += delegate { SaveFile(); };
            bApply.Click += delegate { DoApply(); };
            bClose.Click += delegate { Close(); };

            Controls.AddRange(new Control[] { l1, tName, l2, dtStart, l3, cbMode, info, grid, bAdd, bDel, bOpen, bSave, bApply, bClose });
            foreach (Control c in new Control[] { bAdd, bDel, bOpen, bSave, bApply, bClose }) c.Anchor = AnchorStyles.Bottom | (c.Left > 500 ? AnchorStyles.Right : AnchorStyles.Left);
            LoadPackage(initial ?? new Package());
            if (initial == null) AddStep(new PackageStep());
        }

        static Button Btn(string text, int x, int y, int w) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 30); return b; }

        void AddText(string title, int width)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = title; c.Width = width; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        void AddBtn()
        {
            DataGridViewButtonColumn c = new DataGridViewButtonColumn(); c.HeaderText = ""; c.Text = "Wählen…"; c.UseColumnTextForButtonValue = true; c.Width = 70;
            grid.Columns.Add(c);
        }

        // Spalten: 0 Tag, 1 Block, 2 Taetigkeit, 3 Equipment, 4 [Waehlen], 5 Ausweich, 6 [Waehlen], 7 Notiz
        void AddStep(PackageStep s)
        {
            int i = grid.Rows.Add(s.Day.ToString(), null, s.Text, s.Equipment, null, s.Fallback, null, s.Note);
            DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell)grid.Rows[i].Cells[1];
            if (s.Row.Length > 0) { if (!cell.Items.Contains(s.Row)) cell.Items.Add(s.Row); cell.Value = s.Row; }
        }

        void LoadPackage(Package p)
        {
            tName.Text = p.Name;
            grid.Rows.Clear();
            foreach (PackageStep s in p.Steps) AddStep(s);
            blocks = p.Blocks;
            info.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
        }

        static string S(object o) { return o == null ? "" : o.ToString().Trim(); }

        Package Collect()
        {
            grid.EndEdit();
            Package p = new Package(); p.Name = tName.Text.Trim();
            foreach (DataGridViewRow r in grid.Rows)
            {
                PackageStep s = new PackageStep();
                int d; s.Day = int.TryParse(S(r.Cells[0].Value), out d) && d >= 1 ? d : 1;
                s.Row = S(r.Cells[1].Value); s.Text = S(r.Cells[2].Value); s.Equipment = S(r.Cells[3].Value);
                s.Fallback = S(r.Cells[5].Value); s.Note = S(r.Cells[7].Value);
                if (!s.IsEmpty) p.Steps.Add(s);
            }
            p.Blocks = blocks;
            return p;
        }

        void OnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 4 && e.ColumnIndex != 6)) return;
            DataGridViewCell target = grid.Rows[e.RowIndex].Cells[e.ColumnIndex - 1];
            using (EquipmentPicker pk = new EquipmentPicker(equip.ListNumbers(), S(target.Value)))
                if (pk.ShowDialog(this) == DialogResult.OK) target.Value = pk.Result;
        }

        void OpenFile()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg|Alle Dateien|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { LoadPackage(Package.Load(d.FileName)); }
                catch (Exception ex) { MessageBox.Show("Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void SaveFile()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = tName.Text.Trim().Length > 0 ? tName.Text.Trim() : "Paket";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Collect().Save(d.FileName); }
                catch (Exception ex) { MessageBox.Show("Paket konnte nicht gespeichert werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void DoApply()
        {
            Package p = Collect();
            if (p.Steps.Count == 0 && p.Blocks.Count == 0) { MessageBox.Show("Das Paket ist leer.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (PackageStep s in p.Steps)
                if (s.Row.Length == 0) { MessageBox.Show("Bitte bei jedem Schritt einen Block wählen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string[] modes = { "nur leere Blöcke gefüllt", "vorhandene Blöcke überschrieben", "Text an vorhandene Blöcke angehängt" };
            if (MessageBox.Show("Paket \"" + p.Name + "\" ab " + dtStart.Value.ToString("dd.MM.yyyy") + " in den Plan übernehmen (" + modes[cbMode.SelectedIndex] + ")?",
                "In Plan übernehmen", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                string msg = p.Apply(store, dtStart.Value.Date, (ApplyMode)cbMode.SelectedIndex);
                Applied = true;
                MessageBox.Show(msg, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    // Auswahl bekannter Equipment-Nummern plus freie Eingabe (z. B. Ausweich-Equipment, das nicht in der Liste steht).
    class EquipmentPicker : Form
    {
        readonly CheckedListBox list = new CheckedListBox();
        readonly TextBox own = new TextBox();
        public string Result = "";

        public EquipmentPicker(List<string> known, string current)
        {
            Text = "Equipment wählen";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 440);

            List<string> extras = new List<string>();
            foreach (string t in current.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string v = t.Trim();
                if (v.Length > 0 && !known.Contains(v) && !extras.Contains(v)) extras.Add(v);
            }
            Label l1 = new Label(); l1.Text = "Aus der Equipment-Liste:"; l1.SetBounds(10, 8, 340, 18);
            list.SetBounds(10, 28, 340, 280); list.CheckOnClick = true;
            foreach (string k in known) list.Items.Add(k, Array.IndexOf(current.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries), k) >= 0 || ContainsToken(current, k));
            Label l2 = new Label(); l2.Text = "Eigene Eingabe (Komma getrennt):"; l2.SetBounds(10, 316, 340, 18);
            own.SetBounds(10, 336, 340, 22); own.Text = string.Join(", ", extras.ToArray());
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(150, 396, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(255, 396, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, own, ok, cancel });
        }

        static bool ContainsToken(string csv, string k)
        {
            foreach (string t in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) if (t.Trim() == k) return true;
            return false;
        }

        void Commit()
        {
            List<string> res = new List<string>();
            foreach (object o in list.CheckedItems) res.Add(o.ToString());
            foreach (string t in own.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string v = t.Trim();
                if (v.Length > 0 && !res.Contains(v)) res.Add(v);
            }
            Result = string.Join(", ", res.ToArray());
            DialogResult = DialogResult.OK;
        }
    }
}
