using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Kampagne planen. Links der Ablauf (Schritte aus Bausteinen wie Packen, Lauf, TFF Flush), rechts die Details
    // mit allen Equipment-Positionen (Prozesstank, Pumpe, TFF-Skid ...), Wiederholung/Abwechseln, Ausweich-Equipment und Vorschau.
    class PackageForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        readonly IDataStore store;
        readonly bool canApply, canAdmin;
        readonly List<string> rowNames = new List<string>();
        Catalog cat;
        readonly List<PackageStep> steps = new List<PackageStep>();
        List<PackageBlock> blocks = new List<PackageBlock>();

        readonly TextBox tName = new TextBox();
        readonly DateTimePicker dtStart = new DateTimePicker();
        readonly ComboBox cbMode = new ComboBox();
        readonly ListBox lbSteps = new ListBox();
        readonly TextBox tTitle = new TextBox(), tNote = new TextBox(), tPreview = new TextBox();
        readonly ComboBox cbRow = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        readonly Panel equipPanel = new Panel();
        readonly Label info = new Label();
        readonly ContextMenuStrip menuBau = new ContextMenuStrip(), menuPos = new ContextMenuStrip();
        PackageStep cur;
        bool loading;
        public bool Applied;

        public PackageForm(IDataStore store, IEquipmentStatusProvider equip, DateTime weekStart, Package initial, bool canApply, bool canAdmin)
        {
            this.store = store; this.canApply = canApply; this.canAdmin = canAdmin;
            Text = "Paket-Editor";
            Font = Theme.Current.UiFont();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1190, 700);
            MinimumSize = new Size(1190, 700);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

            cat = store.GetCatalog();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) rowNames.Add(r.Name);

            Add(L("Name:", 10, 14)); tName.SetBounds(58, 10, 260, 22); Controls.Add(tName);
            Add(L("Start (Tag 1):", 335, 14)); dtStart.Format = DateTimePickerFormat.Short; dtStart.Value = weekStart; dtStart.SetBounds(422, 10, 110, 22); Controls.Add(dtStart);
            dtStart.ValueChanged += delegate { UpdatePreview(); };
            Add(L("Übernehmen:", 550, 14)); cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.SetBounds(632, 10, 260, 22); Controls.Add(cbMode);
            cbMode.Items.AddRange(new object[] { "Nur leere Blöcke füllen", "Vorhandenes überschreiben", "An vorhandenen Text anhängen" });
            cbMode.SelectedIndex = 0;
            info.SetBounds(10, 38, 1170, 18); Controls.Add(info);

            // links: Ablauf
            Add(L("Ablauf (Schritte):", 10, 62));
            lbSteps.SetBounds(10, 82, 255, 500);
            lbSteps.SelectedIndexChanged += delegate { Bind(lbSteps.SelectedIndex >= 0 ? steps[lbSteps.SelectedIndex] : null); };
            Controls.Add(lbSteps);
            Button bAdd = Btn("+ Baustein ▾", 10, 590, 94, delegate { ShowBausteinMenu(); });
            Button bDup = Btn("Duplizieren", 108, 590, 80, delegate { Duplicate(); });
            Button bDel = Btn("Entfernen", 192, 590, 73, delegate { RemoveStep(); });
            Controls.AddRange(new Control[] { bAdd, bDup, bDel });

            // rechts: Details
            int x = 285;
            Add(L("Titel:", x, 62)); tTitle.SetBounds(x + 38, 58, 190, 22); Controls.Add(tTitle);
            Add(L("Block (Raum):", x + 250, 62)); cbRow.DropDownStyle = ComboBoxStyle.DropDownList; cbRow.SetBounds(x + 335, 58, 170, 22); cbRow.Items.AddRange(rowNames.ToArray()); Controls.Add(cbRow);
            Add(L("Tag:", x + 525, 62)); Num(nDay, x + 560, 58, 55, 1, 366); Controls.Add(nDay);
            Add(L("Wiederholen:", x, 96)); Num(nCount, x + 80, 92, 55, 1, 99); Controls.Add(nCount);
            Add(L("mal, alle", x + 142, 96)); Num(nInterval, x + 200, 92, 55, 1, 30); Controls.Add(nInterval);
            Add(L("Tag(e), erste Nummer:", x + 262, 96)); Num(nStartNo, x + 402, 92, 55, 0, 999); Controls.Add(nStartNo);
            Add(L("(z. B. 3 Läufe → Lauf 1, 2, 3 an aufeinanderfolgenden Tagen)", x + 466, 96));

            Add(L("Equipment (Position – Gerät – Wechsel – Ausweich/Backup – Notiz):", x, 130));
            equipPanel.SetBounds(x, 152, 895, 262); equipPanel.AutoScroll = true; equipPanel.BorderStyle = BorderStyle.FixedSingle; Controls.Add(equipPanel);
            Button bPos = Btn("+ Position ▾", x, 420, 120, delegate { menuPos.Show(Control.MousePosition); });
            Controls.Add(bPos);
            Add(L("Notiz zum Schritt (optional):", x, 456)); tNote.SetBounds(x, 476, 895, 22); Controls.Add(tNote);
            Add(L("Vorschau – so erscheint es im Plan:", x, 508));
            tPreview.Multiline = true; tPreview.ReadOnly = true; tPreview.ScrollBars = ScrollBars.Vertical; tPreview.BackColor = Color.White; tPreview.SetBounds(x, 528, 895, 90); Controls.Add(tPreview);
            Ui.SimpleMenu(tNote); Ui.SimpleMenu(tTitle);

            tTitle.TextChanged += delegate { if (!loading && cur != null) { cur.Title = tTitle.Text.Trim(); Changed(); } };
            cbRow.SelectedIndexChanged += delegate { if (!loading && cur != null) { cur.Row = cbRow.Text; Changed(); } };
            nDay.ValueChanged += delegate { if (!loading && cur != null) { cur.Day = (int)nDay.Value; Changed(); } };
            nCount.ValueChanged += delegate { if (!loading && cur != null) { cur.Count = (int)nCount.Value; Changed(); } };
            nInterval.ValueChanged += delegate { if (!loading && cur != null) { cur.Interval = (int)nInterval.Value; Changed(); } };
            nStartNo.ValueChanged += delegate { if (!loading && cur != null) { cur.StartNo = (int)nStartNo.Value; Changed(); } };
            tNote.TextChanged += delegate { if (!loading && cur != null) { cur.Note = tNote.Text.Trim(); Changed(); } };

            // unten
            int y = 650;
            Button bOpen = Btn("Öffnen…", 10, y, 90, delegate { OpenFile(); });
            Button bSave = Btn("Speichern…", 106, y, 100, delegate { SaveFile(); });
            Button bSetup = Btn("Einrichten…", 212, y, 100, delegate { Setup(); });
            bSetup.Visible = canAdmin;
            Button bApply = Btn("In Plan übernehmen", 920, y, 150, delegate { DoApply(); }); bApply.Enabled = canApply;
            Button bClose = Btn("Schließen", 1080, y, 100, delegate { Close(); });
            Controls.AddRange(new Control[] { bOpen, bSave, bSetup, bApply, bClose });

            BuildMenus();
            LoadPackage(initial);
        }

        public void SelectStep(int i) { if (i >= 0 && i < lbSteps.Items.Count) lbSteps.SelectedIndex = i; }

        void Add(Control c) { Controls.Add(c); }
        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 28); b.Click += click; return b; }
        static void Num(NumericUpDown n, int x, int y, int w, int min, int max) { n.SetBounds(x, y, w, 22); n.Minimum = min; n.Maximum = max; n.Value = Math.Max(min, 1); }

        // ---------- Menues (Bausteine, Positionen) ----------

        void BuildMenus()
        {
            menuBau.Items.Clear(); menuPos.Items.Clear();
            foreach (Baustein b in cat.Bausteine)
                if (!b.Special) { Baustein bb = b; menuBau.Items.Add(b.Name, null, delegate { AddFromBaustein(bb); }); }
            bool special = false;
            foreach (Baustein b in cat.Bausteine)
                if (b.Special)
                {
                    if (!special) { menuBau.Items.Add(new ToolStripSeparator()); special = true; }
                    Baustein bb = b; menuBau.Items.Add(b.Name + "  (Sonderereignis)", null, delegate { AddFromBaustein(bb); });
                }
            menuBau.Items.Add(new ToolStripSeparator());
            menuBau.Items.Add("Leerer Schritt", null, delegate { AddStep(new PackageStep()); });
            foreach (EquipPosition p in cat.Positions) { EquipPosition pp = p; menuPos.Items.Add(p.Name + "  (" + p.Short + ")", null, delegate { AddPosition(pp.Short); }); }
        }

        void ShowBausteinMenu() { menuBau.Show(Control.MousePosition); }

        // ---------- Schritte ----------

        int NextDay()
        {
            int d = 0;
            foreach (PackageStep s in steps) d = Math.Max(d, s.Day + (Math.Max(1, s.Count) - 1) * Math.Max(1, s.Interval));
            return d + 1;
        }

        void AddFromBaustein(Baustein b)
        {
            if (b.Unique && steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; }) && !canAdmin)
            {
                MessageBox.Show("\"" + b.Name + "\" ist in jeder Kampagne nur einmal vorgesehen. Ein weiterer Schritt braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            PackageStep s2 = new PackageStep();
            s2.Title = b.Name; s2.Source = b.Name; s2.Row = b.Row; s2.Text = b.Text; s2.Day = steps.Count == 0 ? 1 : NextDay();
            foreach (string sh in b.Positions) { EquipAssign a = new EquipAssign(); a.Short = sh; s2.Equipment.Add(a); }
            AddStep(s2);
        }

        void AddStep(PackageStep s)
        {
            steps.Add(s);
            lbSteps.Items.Add(Describe(s));
            lbSteps.SelectedIndex = lbSteps.Items.Count - 1;
        }

        void Duplicate()
        {
            if (cur == null) return;
            PackageStep c = cur.Clone();
            c.Day = NextDay();
            AddStep(c);
        }

        void RemoveStep()
        {
            int i = lbSteps.SelectedIndex;
            if (i < 0) return;
            Baustein b = cat.Baustein_(steps[i].Source);
            if (b != null && b.Unique && !canAdmin)
            {
                MessageBox.Show("\"" + b.Name + "\" ist ein Standard-Schritt der Kampagne. Entfernen braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            steps.RemoveAt(i); lbSteps.Items.RemoveAt(i);
            if (lbSteps.Items.Count > 0) lbSteps.SelectedIndex = Math.Min(i, lbSteps.Items.Count - 1); else Bind(null);
        }

        string Describe(PackageStep s)
        {
            string days = s.Count > 1 ? s.Day + "–" + (s.Day + (s.Count - 1) * s.Interval) : s.Day.ToString();
            return "Tag " + days + " · " + (s.Row.Length > 0 ? s.Row : "?") + " · " + (s.Title.Length > 0 ? s.Title : "(ohne Titel)") + (s.Count > 1 ? " ×" + s.Count : "");
        }

        void Changed()
        {
            int i = lbSteps.SelectedIndex;
            if (i >= 0 && cur != null) { loading = true; lbSteps.Items[i] = Describe(cur); loading = false; }
            UpdatePreview();
        }

        // ---------- Details ----------

        void Bind(PackageStep s)
        {
            if (loading) return;
            cur = s;
            loading = true;
            foreach (Control c in new List<Control>(equipPanel.Controls.OfType())) { equipPanel.Controls.Remove(c); c.Dispose(); }
            bool on = s != null;
            tTitle.Enabled = cbRow.Enabled = nDay.Enabled = nCount.Enabled = nInterval.Enabled = nStartNo.Enabled = tNote.Enabled = on;
            if (on)
            {
                tTitle.Text = s.Title;
                cbRow.SelectedIndex = cbRow.Items.IndexOf(s.Row);
                nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, s.Day));
                nCount.Value = Math.Max(1, Math.Min(99, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
                nStartNo.Value = Math.Max(0, Math.Min(999, s.StartNo));
                tNote.Text = s.Note;
                int y = 4;
                if (s.Equipment.Count == 0) { Label l = L("Keine Equipment-Positionen. Mit \"+ Position\" hinzufügen.", 6, 8); equipPanel.Controls.Add(l); }
                foreach (EquipAssign a in s.Equipment) { EquipRow r = new EquipRow(a, cat.Position(a.Short), delegate { UpdatePreview(); }, RemoveAssign); r.Top = y; equipPanel.Controls.Add(r); y += r.Height + 2; }
            }
            else { tTitle.Text = ""; tNote.Text = ""; }
            loading = false;
            UpdatePreview();
        }

        void AddPosition(string shortName)
        {
            if (cur == null) return;
            EquipAssign a = new EquipAssign(); a.Short = shortName;
            cur.Equipment.Add(a);
            Bind(cur);
        }

        void RemoveAssign(EquipAssign a) { if (cur == null) return; cur.Equipment.Remove(a); Bind(cur); }

        void UpdatePreview()
        {
            if (cur == null) { tPreview.Text = ""; return; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int run = 0;
            foreach (KeyValuePair<int, string> r in cur.Runs())
            {
                sb.Append("— ").Append(cur.Row.Length > 0 ? cur.Row : "?").Append(", ").Append(dtStart.Value.AddDays(r.Key - 1).ToString("ddd dd.MM.", De)).Append(" —\r\n");
                sb.Append(r.Value.Replace("\n", "\r\n")).Append("\r\n");
                if (++run >= 6 && cur.Count > 6) { sb.Append("… (").Append(cur.Count - 6).Append(" weitere Wiederholungen)\r\n"); break; }
            }
            tPreview.Text = sb.ToString();
        }

        // ---------- Paket laden/speichern/uebernehmen ----------

        void LoadPackage(Package p)
        {
            steps.Clear(); lbSteps.Items.Clear();
            blocks = new List<PackageBlock>();
            if (p == null)
            {
                tName.Text = "";
                foreach (Baustein b in cat.Bausteine) if (b.DefaultInCampaign) AddFromBaustein(b);   // z. B. "Packen" ist standardmaessig dabei
            }
            else
            {
                tName.Text = p.Name; blocks = p.Blocks;
                foreach (PackageStep s in p.Steps) { steps.Add(s); lbSteps.Items.Add(Describe(s)); }
                if (steps.Count > 0) lbSteps.SelectedIndex = 0;
            }
            if (steps.Count == 0) Bind(null);
            info.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
        }

        Package Collect()
        {
            Package p = new Package(); p.Name = tName.Text.Trim();
            foreach (PackageStep s in steps) if (!s.IsEmpty) p.Steps.Add(s);
            p.Blocks = blocks;
            return p;
        }

        void Setup()
        {
            using (CatalogForm f = new CatalogForm(cat, rowNames))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); }
                catch (Exception ex) { MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message, "Einrichten", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                cat = store.GetCatalog();
                BuildMenus();
                Bind(cur);
            }
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
                if (s.Row.Length == 0) { MessageBox.Show("Bitte bei jedem Schritt einen Block (Raum) wählen: " + (s.Title.Length > 0 ? s.Title : "Schritt ohne Titel"), "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
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

        // ---------- Zeile fuer eine Equipment-Position ----------

        class EquipRow : Panel
        {
            readonly EquipAssign a;
            readonly EquipPosition pos;
            readonly Action changed;
            readonly ComboBox cbId = new ComboBox(), cbFb = new ComboBox();
            readonly CheckBox chkAlt = new CheckBox();
            readonly Button btnAlt = new Button();
            bool loading;

            public EquipRow(EquipAssign a, EquipPosition pos, Action changed, Action<EquipAssign> remove)
            {
                this.a = a; this.pos = pos; this.changed = changed;
                Height = 28; Width = 870;
                Label name = new Label(); name.Text = pos != null && pos.Name != a.Short ? pos.Name + " (" + a.Short + ")" : a.Short; name.SetBounds(2, 5, 220, 20); name.AutoEllipsis = true;
                Controls.Add(name);

                List<string> ids = pos != null ? pos.Ids : new List<string>();
                string ph = pos != null ? pos.Placeholder() : "";
                cbId.DropDownStyle = ComboBoxStyle.DropDown; cbId.SetBounds(226, 3, 150, 22);   // frei eintippbar (Notfall-/Ausweichgeraet)
                cbId.Items.AddRange(ids.ToArray()); if (ph.Length > 0) cbId.Items.Add(ph);
                cbFb.DropDownStyle = ComboBoxStyle.DropDown; cbFb.SetBounds(522, 3, 140, 22); cbFb.Items.AddRange(ids.ToArray());
                chkAlt.Text = "abwechselnd"; chkAlt.SetBounds(382, 4, 100, 20);
                btnAlt.Text = "…"; btnAlt.SetBounds(486, 3, 30, 22);
                TextBox note = new TextBox(); note.SetBounds(668, 3, 170, 22); Ui.SimpleMenu(note);
                Button x = new Button(); x.Text = "x"; x.SetBounds(844, 3, 24, 22); x.Click += delegate { remove(a); };
                Controls.AddRange(new Control[] { cbId, chkAlt, btnAlt, cbFb, note, x });
                new ToolTip().SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange).");
                new ToolTip().SetToolTip(chkAlt, "Läufe wechseln reihum zwischen den gewählten Geräten (Lauf 1 → A, Lauf 2 → B, Lauf 3 → wieder A …).");
                new ToolTip().SetToolTip(cbFb, "Ausweich-Equipment (Backup) für Unvorhergesehenes.");

                loading = true;
                chkAlt.Checked = a.Alternate; cbFb.Text = a.Fallback; note.Text = a.Note;
                Refill();
                loading = false;

                cbId.TextChanged += delegate { if (loading || chkAlt.Checked) return; a.Ids.Clear(); if (cbId.Text.Trim().Length > 0) a.Ids.Add(cbId.Text.Trim()); Warn(); changed(); };
                cbFb.TextChanged += delegate { if (!loading) { a.Fallback = cbFb.Text.Trim(); changed(); } };
                note.TextChanged += delegate { if (!loading) { a.Note = note.Text.Trim(); changed(); } };
                chkAlt.CheckedChanged += delegate
                {
                    if (loading) return;
                    a.Alternate = chkAlt.Checked;
                    if (a.Alternate && a.Ids.Count < 2) PickAlternatives();
                    if (!a.Alternate && a.Ids.Count > 1) a.Ids.RemoveRange(1, a.Ids.Count - 1);
                    loading = true; Refill(); loading = false; changed();
                };
                btnAlt.Click += delegate { PickAlternatives(); loading = true; Refill(); loading = false; changed(); };
            }

            void Refill()
            {
                cbId.Enabled = !chkAlt.Checked; btnAlt.Enabled = chkAlt.Checked;
                cbId.Text = chkAlt.Checked ? string.Join(" / ", a.Ids.ToArray()) : (a.Ids.Count > 0 ? a.Ids[0] : "");
                Warn();
            }

            void Warn()
            {
                string t = cbId.Text.Trim();
                bool known = t.Length == 0 || chkAlt.Checked || (pos != null && (pos.Ids.Contains(t) || t == pos.Placeholder()));
                cbId.ForeColor = known ? SystemColors.WindowText : Color.DarkOrange;
            }

            void PickAlternatives()
            {
                using (AlternativesPicker p = new AlternativesPicker(pos != null ? pos.Ids : new List<string>(), a.Ids))
                    if (p.ShowDialog(this) == DialogResult.OK) { a.Ids = p.Result; if (a.Ids.Count < 2) { a.Alternate = false; loading = true; chkAlt.Checked = false; loading = false; } }
                    else if (a.Ids.Count < 2) { a.Alternate = false; loading = true; chkAlt.Checked = false; loading = false; }
            }
        }
    }

    // Reihum-Verteilung: Geraete ankreuzen (Reihenfolge wie in der Liste); eigene Eintraege sind moeglich.
    class AlternativesPicker : Form
    {
        readonly CheckedListBox list = new CheckedListBox();
        readonly TextBox own = new TextBox();
        public List<string> Result = new List<string>();

        public AlternativesPicker(List<string> known, List<string> current)
        {
            Text = "Abwechselnd verwenden";
            Font = Theme.Current.UiFont();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(330, 370);
            Label l1 = new Label(); l1.Text = "Geräte ankreuzen, die sich abwechseln (A, B, A, B …):"; l1.SetBounds(10, 8, 310, 32);
            list.SetBounds(10, 42, 310, 200); list.CheckOnClick = true;
            List<string> extras = new List<string>();
            foreach (string c in current) if (!known.Contains(c)) extras.Add(c);
            foreach (string k in known) list.Items.Add(k, current.Contains(k));
            Label l2 = new Label(); l2.Text = "Weitere (Komma getrennt):"; l2.SetBounds(10, 252, 310, 18);
            own.SetBounds(10, 272, 310, 22); own.Text = string.Join(", ", extras.ToArray()); Ui.SimpleMenu(own);
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(120, 326, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(225, 326, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, own, ok, cancel });
        }

        void Commit()
        {
            Result = new List<string>();
            foreach (object o in list.CheckedItems) Result.Add(o.ToString());
            foreach (string t in own.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) { string v = t.Trim(); if (v.Length > 0 && !Result.Contains(v)) Result.Add(v); }
            DialogResult = DialogResult.OK;
        }
    }

    static class ControlExt
    {
        public static IEnumerable<Control> OfType(this Control.ControlCollection c) { foreach (Control x in c) yield return x; }
    }
}
