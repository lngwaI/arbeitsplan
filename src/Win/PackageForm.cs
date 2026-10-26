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

        readonly TextBox tName = new TextBox(), tProduct = new TextBox(), tSubNos = new TextBox();
        readonly ComboBox cbKind = new ComboBox();
        readonly DateTimePicker dtStart = new DateTimePicker();
        readonly ComboBox cbMode = new ComboBox();
        readonly ListBox lbSteps = new ListBox();
        readonly TextBox tTitle = new TextBox(), tNote = new TextBox(), tPreview = new TextBox();
        readonly ComboBox cbRow = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        readonly Panel equipPanel = new Panel();
        readonly Label info = new Label(), rowHint = new Label();
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
            Add(L("Produktcode:", 910, 14)); tProduct.SetBounds(992, 10, 50, 22); tProduct.MaxLength = 4; Controls.Add(tProduct);
            Add(L("(XXYY der Laufnummer)", 1048, 14));
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
            rowHint.Text = "Block weicht vom Standard ab (Sonderaktion)"; rowHint.ForeColor = Color.DarkOrange; rowHint.AutoSize = true; rowHint.Location = new Point(x + 625, 62); rowHint.Visible = false; Controls.Add(rowHint);
            Add(L("Wiederholen:", x, 96)); Num(nCount, x + 80, 92, 55, 1, 99); Controls.Add(nCount);
            Add(L("mal, alle", x + 142, 96)); Num(nInterval, x + 200, 92, 55, 1, 30); Controls.Add(nInterval);
            Add(L("Tag(e), erste Nummer:", x + 262, 96)); Num(nStartNo, x + 402, 92, 60, 0, 9999); Controls.Add(nStartNo);
            Add(L("Art:", x + 475, 96)); cbKind.DropDownStyle = ComboBoxStyle.DropDownList; cbKind.SetBounds(x + 505, 92, 230, 22); Controls.Add(cbKind);
            cbKind.Items.AddRange(new object[] { "Schritt (Nummer 1, 2, 3 …)", "Hauptlauf (Laufnummer)", "Unterlauf (Splitten/Einfrieren)" });
            Add(L("Unterlauf-Nr. je Lauf:", x, 130)); tSubNos.SetBounds(x + 140, 126, 300, 22); Controls.Add(tSubNos);
            Add(L("Komma = nächster Lauf, mehrere mit \"/\"; 4 Ziffern = mit Produktcode", x + 450, 130));

            Add(L("Equipment (Position – Gerät – Wechsel – Ausweich/Backup – Notiz):", x, 158));
            equipPanel.SetBounds(x, 178, 895, 236); equipPanel.AutoScroll = true; equipPanel.BorderStyle = BorderStyle.FixedSingle; Controls.Add(equipPanel);
            Button bPos = Btn("+ Position ▾", x, 420, 120, delegate { menuPos.Show(Control.MousePosition); });
            Controls.Add(bPos);
            Add(L("Notiz zum Schritt (optional):", x, 456)); tNote.SetBounds(x, 476, 895, 22); Controls.Add(tNote);
            Add(L("Vorschau – so erscheint es im Plan:", x, 508));
            tPreview.Multiline = true; tPreview.ReadOnly = true; tPreview.ScrollBars = ScrollBars.Vertical; tPreview.BackColor = Color.White; tPreview.SetBounds(x, 528, 895, 90); Controls.Add(tPreview);
            Ui.SimpleMenu(tNote); Ui.SimpleMenu(tTitle); Ui.SimpleMenu(tSubNos); Ui.SimpleMenu(tProduct);
            cbKind.SelectedIndexChanged += delegate { tSubNos.Enabled = cur != null && cbKind.SelectedIndex == 2; if (!loading && cur != null) { cur.Kind = (StepKind)cbKind.SelectedIndex; Changed(); } };
            tSubNos.TextChanged += delegate { if (!loading && cur != null) { cur.SubNos = tSubNos.Text.Trim(); Changed(); } };
            tProduct.TextChanged += delegate { if (!loading) { for (int i = 0; i < steps.Count; i++) lbSteps.Items[i] = Describe(steps[i]); UpdatePreview(); } };

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
            if (b.Name == "Lauf") s2.Kind = StepKind.Run; else if (b.Name == "Splitten") s2.Kind = StepKind.SubRun;   // Vorbelegung, im Schritt aenderbar
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
            return "Tag " + days + " · " + (s.Row.Length > 0 ? s.Row : "?") + " · " + (s.Kind != StepKind.Normal ? s.Label(Product, 0) : (s.Title.Length > 0 ? s.Title : "(ohne Titel)")) + (s.Count > 1 ? " ×" + s.Count : "");
        }

        string Product { get { return tProduct.Text.Trim(); } }

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
            tTitle.Enabled = cbRow.Enabled = nDay.Enabled = nCount.Enabled = nInterval.Enabled = nStartNo.Enabled = tNote.Enabled = cbKind.Enabled = on;
            tSubNos.Enabled = on && s.Kind == StepKind.SubRun;
            if (on)
            {
                tTitle.Text = s.Title;
                cbKind.SelectedIndex = (int)s.Kind; tSubNos.Text = s.SubNos;
                cbRow.SelectedIndex = cbRow.Items.IndexOf(s.Row);
                nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, s.Day));
                nCount.Value = Math.Max(1, Math.Min(99, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
                nStartNo.Value = Math.Max(0, Math.Min(9999, s.StartNo));
                tNote.Text = s.Note;
                int y = 4;
                if (s.Equipment.Count == 0) { Label l = L("Keine Equipment-Positionen. Mit \"+ Position\" hinzufügen.", 6, 8); equipPanel.Controls.Add(l); }
                foreach (EquipAssign a in s.Equipment) { EquipRow r = new EquipRow(a, cat, delegate { UpdatePreview(); }, RemoveAssign); r.Top = y; equipPanel.Controls.Add(r); y += r.Height + 2; }
            }
            else { tTitle.Text = ""; tNote.Text = ""; tSubNos.Text = ""; cbKind.SelectedIndex = -1; }
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

        void UpdateRowHint()
        {
            Baustein b = cur != null ? cat.Baustein_(cur.Source) : null;
            rowHint.Visible = b != null && b.Row.Length > 0 && cur.Row.Length > 0 && cur.Row != b.Row;   // z. B. Packen in Suite 2
        }

        void UpdatePreview()
        {
            UpdateRowHint();
            if (cur == null) { tPreview.Text = ""; return; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int run = 0;
            foreach (KeyValuePair<int, string> r in cur.Runs(Product))
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
            loading = true; tProduct.Text = p != null ? p.ProductCode : ""; loading = false;
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

        // null = Produktcode ungueltig (Meldung wurde gezeigt). Schritte als Kopie: der Kampagnenstart nummeriert um.
        Package Collect()
        {
            if (Product.Length > 0 && !Package.IsCode4(Product))
            {
                MessageBox.Show("Der Produktcode muss aus genau 4 Ziffern bestehen (z. B. 1357) oder leer sein.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tProduct.Focus(); return null;
            }
            Package p = new Package(); p.Name = tName.Text.Trim(); p.ProductCode = Product;
            foreach (PackageStep s in steps) if (!s.IsEmpty) p.Steps.Add(s.Clone());
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
                Package p = Collect();
                if (p == null || d.ShowDialog(this) != DialogResult.OK) return;
                try { p.Save(d.FileName); }
                catch (Exception ex) { MessageBox.Show("Paket konnte nicht gespeichert werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void DoApply()
        {
            Package p = Collect();
            if (p == null) return;
            if (p.Steps.Count == 0 && p.Blocks.Count == 0) { MessageBox.Show("Das Paket ist leer.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (PackageStep s in p.Steps)
                if (s.Row.Length == 0) { MessageBox.Show("Bitte bei jedem Schritt einen Block (Raum) wählen: " + (s.Title.Length > 0 ? s.Title : "Schritt ohne Titel"), "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string[] modes = { "nur leere Blöcke füllen", "vorhandene Blöcke überschreiben", "Text an vorhandene Blöcke anhängen" };
            // Kampagnenstart: Startdatum, erste Laufnummer, Termin je Lauf (einzeln verschiebbar)
            DateTime start; List<RunDate> schedule;
            using (CampaignForm cf = new CampaignForm(p, store, dtStart.Value.Date, modes[cbMode.SelectedIndex]))
            {
                if (cf.ShowDialog(this) != DialogResult.OK) return;
                start = cf.Start; schedule = cf.Schedule;
            }
            try
            {
                string msg = p.Apply(store, start, (ApplyMode)cbMode.SelectedIndex, schedule);
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
            readonly Catalog cat;
            readonly Action changed;
            readonly ComboBox cbId = new ComboBox(), cbFb = new ComboBox();
            readonly CheckBox chkAlt = new CheckBox();
            readonly Button btnAlt = new Button();
            bool loading;

            public EquipRow(EquipAssign a, Catalog cat, Action changed, Action<EquipAssign> remove)
            {
                this.a = a; this.cat = cat; this.pos = cat.Position(a.Short); this.changed = changed;
                Height = 28; Width = 870;
                Label name = new Label(); name.Text = pos != null && pos.Name != a.Short ? pos.Name + " (" + a.Short + ")" : a.Short; name.SetBounds(2, 5, 220, 20); name.AutoEllipsis = true;
                Controls.Add(name);

                List<string> own = pos != null ? pos.Ids : new List<string>();
                string ph = pos != null ? pos.Placeholder() : "";
                cbId.DropDownStyle = ComboBoxStyle.DropDown; cbId.SetBounds(226, 3, 150, 22);   // frei eintippbar (Notfall-/Ausweichgeraet)
                cbId.Items.AddRange(own.ToArray()); if (ph.Length > 0) cbId.Items.Add(ph);
                cbFb.DropDownStyle = ComboBoxStyle.DropDown; cbFb.SetBounds(522, 3, 140, 22);
                foreach (EquipChoice c in cat.Interchangeable(a.Short)) cbFb.Items.Add(c.Short == a.Short ? c.Id : c.Short + " " + c.Id);
                chkAlt.Text = "abwechselnd"; chkAlt.SetBounds(382, 4, 100, 20);
                bool rot = pos != null && pos.AllowRotation;      // nur wo der Admin Rotation hinterlegt hat (z. B. Abfüllskid)
                chkAlt.Visible = btnAlt.Visible = rot || a.Alternate;   // bereits gesetzte Rotation (Import) bleibt sicht- und aenderbar, nichts wird still veraendert
                btnAlt.Text = "…"; btnAlt.SetBounds(486, 3, 30, 22);
                TextBox note = new TextBox(); note.SetBounds(668, 3, 170, 22); Ui.SimpleMenu(note);
                Button x = new Button(); x.Text = "x"; x.SetBounds(844, 3, 24, 22); x.Click += delegate { remove(a); };
                Controls.AddRange(new Control[] { cbId, chkAlt, btnAlt, cbFb, note, x });
                ToolTip tip = new ToolTip();
                tip.SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange).");
                tip.SetToolTip(chkAlt, "Läufe wechseln reihum zwischen den gewählten Geräten (Lauf 1 → A, Lauf 2 → B, Lauf 3 → wieder A …). Zur Auswahl stehen alle Geräte, die laut Admin gleich eingesetzt werden können.");
                tip.SetToolTip(cbFb, "Ausweich-Equipment (Backup) für Unvorhergesehenes.");

                loading = true;
                chkAlt.Checked = a.Alternate; cbFb.Text = a.Fallback; note.Text = a.Note;
                Refill();
                loading = false;

                cbId.TextChanged += delegate
                {
                    if (loading || chkAlt.Checked) return;
                    a.Choices.Clear();
                    if (cbId.Text.Trim().Length > 0) a.Choices.Add(new EquipChoice(a.Short, cbId.Text.Trim()));
                    Warn(); changed();
                };
                cbFb.TextChanged += delegate { if (!loading) { a.Fallback = cbFb.Text.Trim(); changed(); } };
                note.TextChanged += delegate { if (!loading) { a.Note = note.Text.Trim(); changed(); } };
                chkAlt.CheckedChanged += delegate
                {
                    if (loading) return;
                    a.Alternate = chkAlt.Checked;
                    if (a.Alternate && a.Choices.Count < 2) PickAlternatives();
                    if (!a.Alternate && a.Choices.Count > 1) a.Choices.RemoveRange(1, a.Choices.Count - 1);
                    loading = true; Refill(); loading = false; changed();
                };
                btnAlt.Click += delegate { PickAlternatives(); loading = true; Refill(); loading = false; changed(); };
            }

            string Display(EquipChoice c) { return c.Short == a.Short ? c.Id : c.ToString(); }

            void Refill()
            {
                cbId.Enabled = !chkAlt.Checked; btnAlt.Enabled = chkAlt.Checked;
                if (chkAlt.Checked) { List<string> d = new List<string>(); foreach (EquipChoice c in a.Choices) d.Add(Display(c)); cbId.Text = string.Join(" / ", d.ToArray()); }
                else cbId.Text = a.Choices.Count > 0 ? a.Choices[0].Id : "";
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
                using (AlternativesPicker p = new AlternativesPicker(cat.Interchangeable(a.Short), a.Choices, a.Short))
                    if (p.ShowDialog(this) == DialogResult.OK) a.Choices = p.Result;
                if (a.Choices.Count < 2) { a.Alternate = false; loading = true; chkAlt.Checked = false; loading = false; }
            }
        }
    }

    // Reihum-Verteilung: Geraete ankreuzen, die sich abwechseln. Angeboten werden alle Geraete, die laut Admin-Hinterlegung
    // (Austausch-Gruppen) gleich eingesetzt werden koennen; eigene Eintraege sind moeglich.
    class AlternativesPicker : Form
    {
        readonly CheckedListBox list = new CheckedListBox();
        readonly TextBox own = new TextBox();
        readonly List<EquipChoice> offered;
        readonly string ownShort;
        public List<EquipChoice> Result = new List<EquipChoice>();

        public AlternativesPicker(List<EquipChoice> offered, List<EquipChoice> current, string ownShort)
        {
            this.offered = offered; this.ownShort = ownShort;
            Text = "Abwechselnd verwenden";
            Font = Theme.Current.UiFont();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 400);
            Label l1 = new Label(); l1.Text = "Geräte ankreuzen, die sich abwechseln (A, B, A, B …). Angeboten werden alle Geräte, die laut Admin gleich eingesetzt werden können:"; l1.SetBounds(10, 8, 340, 48);
            list.SetBounds(10, 60, 340, 220); list.CheckOnClick = true;
            List<string> extras = new List<string>();
            foreach (EquipChoice c in offered) list.Items.Add(c.ToString(), current.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; }));
            foreach (EquipChoice c in current) if (!offered.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; })) extras.Add(c.Id);
            Label l2 = new Label(); l2.Text = "Weitere (Komma getrennt):"; l2.SetBounds(10, 290, 340, 18);
            own.SetBounds(10, 310, 340, 22); own.Text = string.Join(", ", extras.ToArray()); Ui.SimpleMenu(own);
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(150, 356, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(255, 356, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, own, ok, cancel });
        }

        void Commit()
        {
            Result = new List<EquipChoice>();
            for (int i = 0; i < offered.Count; i++) if (list.GetItemChecked(i)) Result.Add(offered[i].Clone());
            foreach (string t in own.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string v = t.Trim();
                if (v.Length > 0 && !Result.Exists(delegate(EquipChoice x) { return x.Id == v; })) Result.Add(new EquipChoice(ownShort, v));
            }
            DialogResult = DialogResult.OK;
        }
    }

    static class ControlExt
    {
        public static IEnumerable<Control> OfType(this Control.ControlCollection c) { foreach (Control x in c) yield return x; }
    }
}
