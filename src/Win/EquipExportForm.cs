using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Woche -> "Equipment-Liste exportieren…": erkannte Equipment-Nummern von Tag / Woche / Zeitraum, jede einmal, getrennt durch Leerzeilen.
    class EquipExportForm : Form
    {
        readonly IDataStore store;
        readonly EquipExport.ProductMap map;
        CampaignCalendar cal;
        string campWhy = "";                            // Grund, wenn die Kampagnenliste leer ist
        readonly RadioButton rDay = new RadioButton(), rWeek = new RadioButton(), rRange = new RadioButton(), rCamp = new RadioButton();
        readonly DateTimePicker dDay = new DateTimePicker(), dFrom = new DateTimePicker(), dTo = new DateTimePicker();
        readonly ComboBox camps = new ComboBox();
        readonly ComboBox prefix = new ComboBox();      // Produkt: Liste oder Name, Code, 4-stelliges Praefix tippen
        readonly NumericUpDown before = new NumericUpDown(), after = new NumericUpDown();
        readonly TextBox preview = new TextBox();
        readonly Label count = new Label(), unknownL = new Label();
        readonly CheckBox withCount = new CheckBox();   // mit Anzahl: "ID<TAB>Anzahl" je Zeile
        readonly ToolTip tips = new ToolTip();
        DateTime ra, rb;                                // gerade gezeigter Zeitraum (Kampagne: inkl. Puffer mit Lauf-Angabe)
        readonly DateTime weekStart;
        string text = "";
        readonly Timer campTimer = new Timer();   // Produktfeld entprellt (300 ms)
        Font bold;
        bool loading;

        public EquipExportForm(IDataStore store, DateTime weekStart, DateTime? selectedDay, string packageFolder = null)
        {
            this.store = store; this.weekStart = weekStart.Date;
            map = new EquipExport.ProductMap(packageFolder, store.GetCatalog());
            Text = "Equipment-Liste exportieren";
            Ui.Dialog(this, 610, 528, false);
            Theme th = Theme.Current;

            rDay.Text = "Tag"; rDay.AutoSize = true; rDay.Location = new Point(14, 14);
            rWeek.Text = "Woche"; rWeek.AutoSize = true; rWeek.Location = new Point(14, 42);
            rRange.Text = "Zeitraum"; rRange.AutoSize = true; rRange.Location = new Point(14, 70);
            rCamp.Text = "Kampagne"; rCamp.AutoSize = true; rCamp.Location = new Point(14, 98);
            camps.DropDownStyle = ComboBoxStyle.DropDownList; camps.DisplayMember = "Label"; camps.SetBounds(110, 96, 484, 24);
            Label lp = new Label(); lp.Text = "Produktcode"; lp.AutoSize = true; lp.Location = new Point(14, 130);
            prefix.SetBounds(110, 126, 190, 24); prefix.DropDownStyle = ComboBoxStyle.DropDown; prefix.DropDownWidth = 300;
            prefix.Items.AddRange(map.Entries().ToArray()); prefix.SelectedIndex = 0;
            prefix.AutoCompleteMode = AutoCompleteMode.Suggest; prefix.AutoCompleteSource = AutoCompleteSource.ListItems;
            Label lv = new Label(); lv.Text = "Vorlauf (Tage)"; lv.AutoSize = true; lv.Location = new Point(316, 130);
            before.SetBounds(404, 126, 42, 24); before.Minimum = 0; before.Maximum = 30; before.Value = 2;
            Label ln = new Label(); ln.Text = "Nachlauf (Tage)"; ln.AutoSize = true; ln.Location = new Point(456, 130);
            after.SetBounds(552, 126, 42, 24); after.Minimum = 0; after.Maximum = 30; after.Value = 0;
            Label bis = new Label(); bis.Text = "bis"; bis.AutoSize = true; bis.Location = new Point(244, 73);
            foreach (DateTimePicker d in new[] { dDay, dFrom, dTo }) { d.Format = DateTimePickerFormat.Custom; d.CustomFormat = "dd.MM.yyyy"; d.Width = 120; }   // TT.MM.JJJJ unabhaengig von der Windows-Region
            dDay.Location = new Point(110, 11); dFrom.Location = new Point(110, 68); dTo.Location = new Point(276, 68);
            DateTime day = selectedDay.HasValue ? selectedDay.Value.Date : this.weekStart;
            dDay.Value = day; dFrom.Value = this.weekStart; dTo.Value = this.weekStart.AddDays(6);
            Label wk = new Label(); wk.Text = this.weekStart.ToString("dd.MM.yyyy") + " – " + this.weekStart.AddDays(6).ToString("dd.MM.yyyy");
            wk.AutoSize = true; wk.Location = new Point(110, 44); wk.ForeColor = th.HeadText;
            if (selectedDay.HasValue) rDay.Checked = true; else rWeek.Checked = true;
            EventHandler upd = delegate { Update_(); };
            rDay.CheckedChanged += upd; rWeek.CheckedChanged += upd; rRange.CheckedChanged += upd; rCamp.CheckedChanged += upd; camps.SelectedIndexChanged += upd; before.ValueChanged += upd; after.ValueChanged += upd;
            campTimer.Interval = 300;
            campTimer.Tick += delegate { campTimer.Stop(); LoadCamps(); Update_(); };
            prefix.TextChanged += delegate { campTimer.Stop(); campTimer.Start(); };
            dDay.ValueChanged += upd; dFrom.ValueChanged += upd; dTo.ValueChanged += upd;

            count.AutoSize = true; count.Location = new Point(14, 160); bold = new Font(Font, FontStyle.Bold); count.Font = bold;
            preview.Multiline = true; preview.ReadOnly = true; preview.ScrollBars = ScrollBars.Vertical; preview.BackColor = th.Window;
            withCount.Text = "mit Anzahl (Nummer, Tab, Anzahl)"; withCount.AutoSize = true; withCount.Location = new Point(14, 182);
            tips.SetToolTip(withCount, "Aus: jede Nummer einmal, durch Leerzeilen getrennt.\nAn: eine Zeile je Nummer \"Nummer<Tab>Anzahl\"; Anzahl = Blöcke (Tag/Zeile), in denen die Nummer vorkommt.");
            withCount.CheckedChanged += upd;
            preview.SetBounds(14, 208, 580, 214);
            unknownL.AutoSize = false; unknownL.AutoEllipsis = true; unknownL.SetBounds(14, 426, 580, 20); unknownL.ForeColor = th.CheckMissingText;

            Button clip = new Button(); clip.Text = "In Zwischenablage"; clip.SetBounds(14, 458, 140, 28);
            Button save = new Button(); save.Text = "Als .txt speichern…"; save.SetBounds(160, 458, 140, 28);
            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(494, 458, 100, 28); close.DialogResult = DialogResult.Cancel;
            Button calBtn = new Button(); calBtn.Text = "Kalender…"; calBtn.SetBounds(306, 458, 100, 28);
            calBtn.Click += delegate { OpenCalendar(); };
            tips.SetToolTip(calBtn, "Kampagnen im Monatsüberblick: Klick wählt eine Kampagne, Ziehen über Tage setzt den Zeitraum.");
            th.StyleButton(calBtn);
            CancelButton = close; AcceptButton = clip;   // Enter = kopieren (Hauptzweck: Liste in die Zwischenablage), Esc = schliessen
            clip.Click += delegate { Copy(); };
            save.Click += delegate { Save(); };
            th.StyleButton(clip); th.StyleButton(save); th.StyleButton(close);
            Controls.AddRange(new Control[] { rDay, rWeek, rRange, rCamp, camps, lp, prefix, lv, before, ln, after, bis, dDay, wk, dFrom, dTo, count, withCount, preview, unknownL, clip, save, calBtn, close });
            Control[] tab = { rDay, dDay, rWeek, rRange, dFrom, dTo, rCamp, camps, prefix, before, after, withCount, preview, clip, save, calBtn, close };   // Tab-Reihenfolge = Lesereihenfolge
            for (int i = 0; i < tab.Length; i++) tab[i].TabIndex = i;
            LoadCamps();
            Update_();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { campTimer.Stop(); campTimer.Dispose(); tips.Dispose(); if (bold != null) bold.Dispose(); }
            base.Dispose(disposing);
        }

        void LoadCamps()
        {
            loading = true;
            try { LoadCampsCore(); } finally { loading = false; }
        }

        void LoadCampsCore()
        {
            camps.Items.Clear();
            foreach (EquipExport.CampaignSpan c in map.Campaigns(store, weekStart.AddDays(-180), weekStart.AddDays(365), prefix.Text, out campWhy)) camps.Items.Add(c);
            int sel = 0;
            for (int i = 0; i < camps.Items.Count; i++)
            { EquipExport.CampaignSpan c = (EquipExport.CampaignSpan)camps.Items[i]; if (c.First <= weekStart.AddDays(6) && c.Last >= weekStart) { sel = i; break; } }
            if (camps.Items.Count > 0) camps.SelectedIndex = sel;
        }

        EquipExport.CampaignSpan Camp { get { return camps.SelectedItem as EquipExport.CampaignSpan; } }

        // Zeitraum und Nummern (mit Anzahl) der aktuellen Auswahl; Kampagne: Puffer mit Lauf-Angabe vor dem Vorlauf zaehlen mit (from = frueheste Stelle)
        List<KeyValuePair<string, int>> Compute(out DateTime from, out DateTime to, List<string> unknown)
        {
            if (rDay.Checked) { from = to = dDay.Value.Date; }
            else if (rCamp.Checked)
            {
                EquipExport.CampaignSpan c = Camp;
                if (c != null) return EquipExport.CountCampaign(store, c, (int)before.Value, (int)after.Value, out from, out to, unknown);
                from = weekStart; to = weekStart.AddDays(-1);
            }
            else if (rWeek.Checked) { from = weekStart; to = weekStart.AddDays(6); }
            else { from = dFrom.Value.Date; to = dTo.Value.Date; }
            return to < from ? new List<KeyValuePair<string, int>>() : EquipExport.Count(store, from, to, unknown);
        }

        void Update_()
        {
            if (loading) return;   // Neubefuellen der Kampagnenliste: Aufrufer aktualisiert danach einmal
            dDay.Enabled = rDay.Checked; dFrom.Enabled = dTo.Enabled = rRange.Checked;
            camps.Enabled = prefix.Enabled = before.Enabled = after.Enabled = rCamp.Checked;
            List<string> unknown = new List<string>();
            List<KeyValuePair<string, int>> ids = Compute(out ra, out rb, unknown);
            DateTime a = ra, b = rb;
            text = withCount.Checked ? EquipExport.FormatCounts(ids) : EquipExport.Format(EquipExport.Ids(ids));
            preview.Text = text;
            string range = rCamp.Checked && Camp != null ? " · " + a.ToString("dd.MM.") + " – " + b.ToString("dd.MM.yyyy") : "";
            count.Text = rCamp.Checked && Camp == null ? campWhy : b < a ? "Zeitraum: „von“ liegt nach „bis“"
                : ids.Count == 0 ? "Keine Equipment-Nummern in diesem Zeitraum" + range : ids.Count + " Equipment-Nummern" + range;
            unknownL.Text = unknown.Count == 0 ? "" : unknown.Count + (unknown.Count == 1 ? " Eintrag" : " Einträge") + " nicht erkannt: " + string.Join(", ", unknown.ToArray());
            tips.SetToolTip(unknownL, unknown.Count == 0 ? "" : unknownL.Text + "\nDiese Werte stehen nicht in der Geräteliste und kommen nicht in den Export.");
        }

        CampaignCalendar MakeCalendar()
        {
            List<EquipExport.CampaignSpan> all = EquipExport.Campaigns(store, weekStart.AddDays(-180), weekStart.AddDays(365), "");
            foreach (EquipExport.CampaignSpan c in all) map.Apply(c);
            return new CampaignCalendar(store, all, (int)before.Value, (int)after.Value, weekStart, PickCampaign, PickRange);
        }

        void OpenCalendar()
        {
            if (cal == null || cal.IsDisposed) { cal = MakeCalendar(); cal.Show(this); } else cal.Activate();
        }

        // Kalender: Kampagne waehlen; passt sie nicht zum Produktfeld, wird das Feld geleert
        void PickCampaign(EquipExport.CampaignSpan c)
        {
            loading = true;
            try
            {
                campTimer.Stop();
                rCamp.Checked = true;
                int i = FindCamp(c);
                if (i < 0) { prefix.SelectedIndex = 0; campTimer.Stop(); LoadCampsCore(); i = FindCamp(c); }
                if (i >= 0) camps.SelectedIndex = i;
            }
            finally { loading = false; }
            Update_();
        }

        int FindCamp(EquipExport.CampaignSpan c)
        {
            for (int i = 0; i < camps.Items.Count; i++)
            {
                EquipExport.CampaignSpan x = (EquipExport.CampaignSpan)camps.Items[i];
                if (x.Prefix == c.Prefix && x.FirstNo <= c.LastNo && x.LastNo >= c.FirstNo) return i;
            }
            return -1;
        }

        void PickRange(DateTime a, DateTime b)
        {
            loading = true;
            try { rRange.Checked = true; dFrom.Value = a; dTo.Value = b; }
            finally { loading = false; }
            Update_();
        }

#if TESTHOOKS
        public void TestProduct(string t) { prefix.Text = t; campTimer.Stop(); LoadCamps(); Update_(); }
        public void TestCampMode() { rCamp.Checked = true; }
        public List<string> TestCamps() { List<string> r = new List<string>(); foreach (EquipExport.CampaignSpan c in camps.Items) r.Add(c.Label); return r; }
        public List<string> TestProductItems() { List<string> r = new List<string>(); foreach (object o in prefix.Items) r.Add(o.ToString()); return r; }
        public string TestPreview { get { return preview.Text; } }
        public string TestCount { get { return count.Text; } }
        public string TestUnknown { get { return unknownL.Text; } }
        public string TestSelected { get { return rCamp.Checked && Camp != null ? Camp.Label : ""; } }
        public string TestRangeText { get { return rRange.Checked ? dFrom.Value.Date.ToString("yyyy-MM-dd") + ".." + dTo.Value.Date.ToString("yyyy-MM-dd") : ""; } }
        public CampaignCalendar TestCalendar() { return MakeCalendar(); }
#endif

        void Copy()
        {
            if (text.Length == 0) return;
            try { Clipboard.SetText(text); }
            catch (Exception ex) { MessageBox.Show("Kopieren fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void Save()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                DateTime a = ra, b = rb;
                d.Filter = "Textdatei (*.txt)|*.txt"; d.FileName = "Equipment_" + a.ToString("yyyy-MM-dd") + (b > a ? "_bis_" + b.ToString("yyyy-MM-dd") : "");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, text, new UTF8Encoding(false)); }
                catch (Exception ex) { MessageBox.Show("Speichern fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
