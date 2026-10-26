using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Muster (Ablauf) eines Produkts in vier Ebenen – 1 Produkt (Name, Produktcode), 2 Ablauf (Schritte nach Tag),
    // 3 Schritt-Details (Was?/Wann?/Wo?/Zusaetze/Equipment des gewaehlten Schritts), 4 Vorschau & Pruefung (Kalenderleiste,
    // Plantext, Pruefhinweise). Die Kopfzeile zeigt, wo man ist. Startdatum und Uebernahme-Modus fragt erst der Kampagnenstart.
    partial class PackageForm : Form
    {
        readonly IDataStore store;
        readonly bool canApply, canAdmin;
        readonly DateTime weekStart;
        readonly List<string> rowNames = new List<string>();
        Catalog cat;
        readonly List<PackageStep> steps = new List<PackageStep>();
        List<PackageBlock> blocks = new List<PackageBlock>();
        PackageStep cur;
        bool loading, dirty;
        string file;            // Datei des Musters, null = noch nicht gespeichert
        public bool Applied;
        public User By;         // angemeldeter Nutzer (fuer das Aenderungsprotokoll bei der Uebernahme)

        readonly Crumbs crumbs = new Crumbs();
        readonly Panel left = new Panel();
        readonly TextBox tName = new TextBox(), tProduct = new TextBox();
        readonly Label lRuns = new Label(), lBlocks = new Label();
        readonly ListView lv = new ListView();
        readonly Button bAdd = new Button(), bUp = new Button(), bDown = new Button(), bDup = new Button(), bDel = new Button();
        readonly ToolTip tip = new ToolTip();
        Section secProduct;

        int IH { get { return Font.Height + 8; } }     // Hoehe der Eingabefelder
        int RowH { get { return IH + 6; } }
        const AnchorStyles TLR = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        public PackageForm(IDataStore store, IEquipmentStatusProvider equip, DateTime weekStart, Package initial, bool canApply, bool canAdmin)
        {
            this.store = store; this.canApply = canApply; this.canAdmin = canAdmin; this.weekStart = weekStart;
            Font = Theme.Current.UiFont();
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(1190, 700);
            MinimumSize = new Size(900, 600);
            cat = store.GetCatalog();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) rowNames.Add(r.Name);

            Control details = BuildDetails();
            BuildLeft();
            Control preview = BuildPreview();
            Panel bar = BuildBar();
            crumbs.Height = Font.Height * 2 + 8; crumbs.TabStop = false; crumbs.PartClicked += CrumbClicked;
            Controls.Add(details); Controls.Add(left); Controls.Add(preview); Controls.Add(bar); Controls.Add(crumbs);   // zuletzt hinzugefuegt = zuerst angedockt
            left.TabIndex = 0; details.TabIndex = 1; preview.TabIndex = 2; bar.TabIndex = 3;
            Theme.Current.Style(this);
            LoadPackage(initial);
            ActiveControl = lv;   // Start im Ablauf, nicht mit markiertem Namen
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle wa = Screen.FromControl(this).WorkingArea;   // kleiner Bildschirm (z. B. 1024x768): Fenster passt sich an, Bereiche scrollen
            if (Width <= wa.Width && Height <= wa.Height) return;
            Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
            Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
        }

        protected override void OnResize(EventArgs e)
        {
            left.Width = Math.Max(330, Math.Min(400, ClientSize.Width * 34 / 100));
            base.OnResize(e);
        }

        Label Lbl(Control parent, string text, int x, int w)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = false; l.TextAlign = ContentAlignment.MiddleLeft; l.SetBounds(x, 0, w, IH); l.UseMnemonic = false;
            parent.Controls.Add(l); return l;
        }

        Button Btn(Button b, string text, int x, int y, int w, EventHandler click) { b.Text = text; b.SetBounds(x, y, w, Theme.Current.ButtonH - 2); b.Click += click; return b; }

        // ---------- 1 Produkt, 2 Ablauf ----------

        void BuildLeft()
        {
            left.Dock = DockStyle.Left; left.Width = 400; left.Padding = new Padding(8, 8, 0, 8); left.Tag = "keep";
            int w = 360, lw = 96;
            secProduct = new Section("1   Produkt", "XXYY = Produkt, ZZZZ = Laufnummer (z. B. 13570030)", true, w);
            int y = secProduct.ContentTop;
            Lbl(secProduct, "Name:", 8, lw).Top = y;
            tName.SetBounds(8 + lw, y, w - 16 - lw, IH); tName.Anchor = TLR;
            y += RowH;
            Lbl(secProduct, "Produktcode:", 8, lw).Top = y;
            tProduct.SetBounds(8 + lw, y, 52, IH); tProduct.MaxLength = 4;
            lRuns.SetBounds(8 + lw + 60, y, w - 16 - lw - 60, IH); lRuns.Anchor = TLR; lRuns.TextAlign = ContentAlignment.MiddleLeft; lRuns.AutoEllipsis = true;
            secProduct.Controls.AddRange(new Control[] { tName, tProduct, lRuns });
            secProduct.Height = y + RowH + 4; secProduct.Dock = DockStyle.Top;
            Ui.SimpleMenu(tName); Ui.SimpleMenu(tProduct);
            tip.SetToolTip(tProduct, "4 Ziffern XXYY. Die Läufe heißen dann XXYYZZZZ (ZZZZ = Laufnummer). Leer = Läufe ohne Produktnummer.");
            tName.TextChanged += delegate { Changed(); };
            tProduct.TextChanged += delegate { Changed(); };

            Section flow = new Section("2   Ablauf", "Schritte nach Tag sortiert. Anklicken = rechts bearbeiten.", true, w);
            lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.ShowItemToolTips = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable; lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Nr", 30); lv.Columns.Add("Tag", 46); lv.Columns.Add("Raum", 76); lv.Columns.Add("Schritt", 110); lv.Columns.Add("Wiederholung", 96);
            lv.SelectedIndexChanged += delegate { if (!loading && lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag != cur) Bind((PackageStep)lv.SelectedItems[0].Tag); };
            lv.MouseUp += delegate { if (lv.SelectedItems.Count == 0) FillList(); };   // Klick ins Leere: Auswahl bleibt beim aktuellen Schritt
            lv.Resize += delegate { FitColumns(); };

            int bh = Theme.Current.ButtonH - 2;
            Panel btns = new Panel(); btns.Tag = "keep"; btns.Dock = DockStyle.Bottom; btns.Height = 2 * bh + 12; btns.Width = w - 16;
            Btn(bAdd, "+ Schritt hinzufügen…", 0, 6, w - 16, delegate { AddStepDialog(); }).Anchor = TLR;
            bAdd.Font = new Font(Font, FontStyle.Bold);
            Btn(bUp, "▲", 0, bh + 10, 34, delegate { MoveStep(-1); });
            Btn(bDown, "▼", 38, bh + 10, 34, delegate { MoveStep(1); });
            Btn(bDup, "Duplizieren", 78, bh + 10, 100, delegate { Duplicate(); });
            Btn(bDel, "Entfernen…", 184, bh + 10, 100, delegate { RemoveStep(); });
            btns.Controls.AddRange(new Control[] { bAdd, bUp, bDown, bDup, bDel });
            tip.SetToolTip(bAdd, "Baustein (Packen, Lauf, Splitten …) oder leeren Schritt hinzufügen");
            tip.SetToolTip(bUp, "Schritt nach oben (früher); übernimmt dabei ggf. den Tag des Vorgängers");
            tip.SetToolTip(bDown, "Schritt nach unten (später); übernimmt dabei ggf. den Tag des Nachfolgers");
            tip.SetToolTip(bDup, "Gewählten Schritt kopieren (Strg+D)");
            tip.SetToolTip(bDel, "Gewählten Schritt entfernen (Entf)");
            lBlocks.Dock = DockStyle.Bottom; lBlocks.Height = 2 * Font.Height + 4; lBlocks.ForeColor = Section.HelpColor; lBlocks.Visible = false;
            flow.Controls.Add(lv); flow.Controls.Add(lBlocks); flow.Controls.Add(btns);
            flow.Dock = DockStyle.Fill;

            Panel gap = new Panel(); gap.Tag = "keep"; gap.Dock = DockStyle.Top; gap.Height = 8;
            left.Controls.Add(flow); left.Controls.Add(gap); left.Controls.Add(secProduct);
            secProduct.TabIndex = 0; flow.TabIndex = 1;
        }

        void FitColumns()
        {
            int rest = lv.ClientSize.Width;
            for (int i = 0; i < lv.Columns.Count; i++) if (i != 3) rest -= lv.Columns[i].Width;
            lv.Columns[3].Width = Math.Max(40, rest);
        }

        Panel BuildBar()
        {
            Panel bar = new Panel(); bar.Dock = DockStyle.Bottom; bar.Height = Theme.Current.ButtonH + 14;
            int y = 7;
            Button bOpen = Btn(new Button(), "Öffnen…", 8, y, 96, delegate { OpenFile(); });
            Button bSave = Btn(new Button(), "Speichern", 110, y, 96, delegate { SaveFile(false); });
            Button bSaveAs = Btn(new Button(), "Speichern unter…", 212, y, 130, delegate { SaveFile(true); });
            Button bSetup = Btn(new Button(), "Katalog einrichten…", 360, y, 150, delegate { Setup(); });
            bSetup.Visible = canAdmin;
            Button bClose = Btn(new Button(), "Schließen", 0, y, 100, delegate { Close(); });
            Button bApply = Btn(new Button(), "In Plan übernehmen…", 0, y, 170, delegate { DoApply(); });
            bApply.Enabled = canApply; bApply.Font = new Font(Font, FontStyle.Bold);
            bClose.Left = ClientSize.Width - 108; bApply.Left = bClose.Left - 176;
            bClose.Anchor = bApply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Width = ClientSize.Width;
            bar.Controls.AddRange(new Control[] { bOpen, bSave, bSaveAs, bSetup, bApply, bClose });
            tip.SetToolTip(bSave, "Muster als Datei (.apkg) speichern (Strg+S)");
            tip.SetToolTip(bSetup, "Equipment-Positionen und Bausteine einrichten (Recht \"Pakete einrichten\")");
            tip.SetToolTip(bApply, "Kampagne starten: Startdatum, erste Laufnummer und Umgang mit vorhandenen Einträgen wählen");
            return bar;
        }

        // ---------- Schritte ----------

        // Schritte nach Tag (bei gleichem Tag in der gespeicherten Reihenfolge); Nummer im Editor = Position in dieser Liste.
        List<PackageStep> Ordered()
        {
            List<PackageStep> o = new List<PackageStep>(steps);
            o.Sort(delegate(PackageStep a, PackageStep b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : steps.IndexOf(a).CompareTo(steps.IndexOf(b)); });
            return o;
        }

        int No(PackageStep s) { return Ordered().IndexOf(s) + 1; }
        string Product { get { return tProduct.Text.Trim(); } }
        static string TitleOf(PackageStep s) { return s.Title.Length > 0 ? s.Title : "(ohne Titel)"; }
        static string DaysOf(PackageStep s) { return s.Count > 1 ? s.Day + "–" + (s.Day + (s.Count - 1) * Math.Max(1, s.Interval)) : s.Day.ToString(); }
        static string RepeatOf(PackageStep s) { return s.Count > 1 ? s.Count + "× alle " + s.Interval + (s.Interval == 1 ? " Tag" : " Tage") : "einmal"; }

        void FillList()
        {
            bool was = loading; loading = true;
            List<PackageStep> o = Ordered();
            lv.BeginUpdate();
            while (lv.Items.Count > o.Count) lv.Items.RemoveAt(lv.Items.Count - 1);
            for (int i = 0; i < o.Count; i++)
            {
                PackageStep s = o[i];
                string[] t = { (i + 1).ToString(), DaysOf(s), s.Row.Length > 0 ? s.Row : "– fehlt –", TitleOf(s), RepeatOf(s) };
                if (i >= lv.Items.Count) { ListViewItem n = new ListViewItem(t); n.UseItemStyleForSubItems = false; lv.Items.Add(n); }
                ListViewItem it = lv.Items[i];
                for (int k = 0; k < t.Length; k++) if (it.SubItems[k].Text != t[k]) it.SubItems[k].Text = t[k];
                it.SubItems[2].ForeColor = s.Row.Length > 0 ? SystemColors.WindowText : Color.Firebrick;
                it.Tag = s;
                it.ToolTipText = "Tag " + DaysOf(s) + " · " + (s.Row.Length > 0 ? s.Row : "Raum fehlt") + " · " + TitleOf(s) + " · " + RepeatOf(s);
                if (it.Selected != (s == cur)) it.Selected = s == cur;
                if (s == cur) it.EnsureVisible();
            }
            lv.EndUpdate();
            FitColumns();
            loading = was;
        }

        public void SelectStep(int i) { List<PackageStep> o = Ordered(); if (i >= 0 && i < o.Count) Bind(o[i]); }

        int NextDay()
        {
            int d = 0;
            foreach (PackageStep s in steps) d = Math.Max(d, s.Day + (Math.Max(1, s.Count) - 1) * Math.Max(1, s.Interval));
            return d + 1;
        }

        // "Packen" o. Ae. ist nur einmal je Kampagne vorgesehen; ein weiterer braucht PackageAdmin.
        bool UniqueBlocked(string source)
        {
            Baustein b = cat.Baustein_(source);
            if (b == null || !b.Unique || canAdmin || !steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; })) return false;
            MessageBox.Show(this, "\"" + b.Name + "\" ist in jeder Kampagne nur einmal vorgesehen. Ein weiterer Schritt braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        PackageStep FromBaustein(Baustein b)
        {
            PackageStep s = new PackageStep();
            s.Title = b.Name; s.Source = b.Name; s.Row = b.Row; s.Text = b.Text; s.Day = steps.Count == 0 ? 1 : NextDay();
            if (b.Name == "Lauf") s.Kind = StepKind.Run; else if (b.Name == "Splitten") s.Kind = StepKind.SubRun;   // Vorbelegung, im Schritt aenderbar
            foreach (string sh in b.Positions) { EquipAssign a = new EquipAssign(); a.Short = sh; s.Equipment.Add(a); }
            return s;
        }

        void AddStepDialog()
        {
            using (BausteinPicker p = new BausteinPicker(cat, steps))
            {
                if (p.ShowDialog(this) != DialogResult.OK) return;
                if (p.EmptyStep) { PackageStep s = new PackageStep(); s.Day = steps.Count == 0 ? 1 : NextDay(); AddStep(s); }
                else if (!UniqueBlocked(p.Result.Name)) AddStep(FromBaustein(p.Result));
            }
        }

        void AddStep(PackageStep s) { steps.Add(s); dirty = true; Bind(s); }

        void Duplicate()
        {
            if (cur == null || UniqueBlocked(cur.Source)) return;
            PackageStep c = cur.Clone();
            c.Day = NextDay();
            AddStep(c);
        }

        // In der Liste nach oben/unten: bei anderem Tag uebernimmt der Schritt den Tag des Nachbarn und steht davor/dahinter.
        void MoveStep(int dir)
        {
            List<PackageStep> o = Ordered();
            int j = o.IndexOf(cur) + dir;
            if (cur == null || j < 0 || j >= o.Count) return;
            PackageStep n = o[j];
            cur.Day = n.Day;
            steps.Remove(cur);
            int k = steps.IndexOf(n);
            steps.Insert(dir < 0 ? k : k + 1, cur);
            dirty = true;
            Bind(cur);
        }

        void RemoveStep() { RemoveStep(true); }

        void RemoveStep(bool ask)
        {
            if (cur == null) return;
            Baustein b = cat.Baustein_(cur.Source);
            if (b != null && b.Unique && !canAdmin)
            {
                MessageBox.Show(this, "\"" + b.Name + "\" ist ein Standard-Schritt der Kampagne. Entfernen braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ask && MessageBox.Show(this, "Schritt " + No(cur) + " \"" + TitleOf(cur) + "\" (Tag " + DaysOf(cur) + ") aus dem Ablauf entfernen?", "Schritt entfernen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            List<PackageStep> o = Ordered();
            int i = o.IndexOf(cur);
            o.RemoveAt(i); steps.Remove(cur);
            dirty = true;
            Bind(o.Count > 0 ? o[Math.Min(i, o.Count - 1)] : null);
        }

        // ---------- Kopfzeile, Titel, Tasten ----------

        void UpdateHeader()
        {
            string nm = tName.Text.Trim().Length > 0 ? tName.Text.Trim() : "(ohne Namen)";
            Text = "Paket-Editor – " + nm + (dirty ? " *" : "");
            List<string> parts = new List<string>();
            parts.Add((tName.Text.Trim().Length > 0 ? "Produkt „" + nm + "“" : "Produkt (noch ohne Namen)") + (Package.IsCode4(Product) ? " (" + Product + ")" : ""));
            parts.Add("Ablauf: " + steps.Count + (steps.Count == 1 ? " Schritt" : " Schritte"));
            if (cur != null) parts.Add("Schritt " + No(cur) + ": " + TitleOf(cur));
            if (dirty) crumbs.Set(parts.ToArray(), "● Ungespeicherte Änderungen", Theme.Current.BorderEdit);
            else crumbs.Set(parts.ToArray(), file != null ? "Gespeichert: " + System.IO.Path.GetFileName(file) : "Noch nicht als Datei gespeichert", Section.HelpColor);
        }

        void CrumbClicked(int i) { if (i == 0) tName.Focus(); else lv.Focus(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S)) { SaveFile(false); return true; }
            if (keyData == (Keys.Control | Keys.D)) { Duplicate(); return true; }
            if (keyData == Keys.Delete && (lv.Focused || strip.Focused)) { RemoveStep(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Ungespeicherte Aenderungen: Ja = speichern, Nein = verwerfen, Abbrechen = bleiben. false = nicht weitermachen.
        bool AskSave()
        {
            if (!dirty) return true;
            DialogResult r = MessageBox.Show(this, "Das Muster hat ungespeicherte Änderungen. Speichern?\n\nJa = Speichern, Nein = Verwerfen", "Paket-Editor",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return r == DialogResult.No || (r == DialogResult.Yes && SaveFile(false));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel && !AskSave()) e.Cancel = true;
        }

        // ---------- Paket laden/speichern/uebernehmen ----------

        void LoadPackage(Package p)
        {
            loading = true;
            steps.Clear(); cur = null;
            blocks = p != null ? p.Blocks : new List<PackageBlock>();
            tName.Text = p != null ? p.Name : ""; tProduct.Text = p != null ? p.ProductCode : "";
            if (p == null) { foreach (Baustein b in cat.Bausteine) if (b.DefaultInCampaign) steps.Add(FromBaustein(b)); }   // z. B. "Packen" ist standardmaessig dabei
            else steps.AddRange(p.Steps);
            lBlocks.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
            lBlocks.Visible = blocks.Count > 0;
            loading = false;
            dirty = false;
            List<PackageStep> o = Ordered();
            Bind(o.Count > 0 ? o[0] : null);
        }

        // null = Produktcode ungueltig (Meldung wurde gezeigt). Schritte als Kopie: der Kampagnenstart nummeriert um.
        Package Collect()
        {
            if (Product.Length > 0 && !Package.IsCode4(Product))
            {
                MessageBox.Show(this, "Der Produktcode muss aus genau 4 Ziffern bestehen (z. B. 1357) oder leer sein.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                catch (Exception ex) { MessageBox.Show(this, "Konnte nicht gespeichert werden.\n" + ex.Message, "Einrichten", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                cat = store.GetCatalog();
                BuildPosMenu();
                Bind(cur);
            }
        }

        void OpenFile()
        {
            if (!AskSave()) return;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg|Alle Dateien|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { LoadPackage(Package.Load(d.FileName)); file = d.FileName; UpdateHeader(); }
                catch (Exception ex) { MessageBox.Show(this, "Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        bool SaveFile(bool saveAs)
        {
            Package p = Collect();
            if (p == null) return false;
            string f = file;
            if (saveAs || f == null)
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = p.Name.Length > 0 ? p.Name : "Paket";
                    if (d.ShowDialog(this) != DialogResult.OK) return false;
                    f = d.FileName;
                }
            try { p.Save(f); }
            catch (Exception ex) { MessageBox.Show(this, "Paket konnte nicht gespeichert werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            file = f; dirty = false;
            UpdateHeader();
            return true;
        }

        void DoApply()
        {
            Package p = Collect();
            if (p == null) return;
            if (p.Steps.Count == 0 && p.Blocks.Count == 0) { MessageBox.Show(this, "Das Paket ist leer.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (PackageStep s in Ordered())
                if (!s.IsEmpty && s.Row.Length == 0)
                {
                    Bind(s);
                    MessageBox.Show(this, "Schritt " + No(s) + " \"" + TitleOf(s) + "\" hat keinen Raum. Bitte unter \"Wo?\" einen Block (Raum) wählen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            DateTime start; List<RunDate> schedule; ApplyMode mode;
            using (CampaignForm cf = new CampaignForm(p, store, weekStart.Date, ApplyMode.FillEmpty))   // Startdatum, erste Laufnummer, Termine, Modus
            {
                if (cf.ShowDialog(this) != DialogResult.OK) return;
                start = cf.Start; schedule = cf.Schedule; mode = cf.Mode;
            }
            try
            {
                string msg = p.Apply(store, start, mode, schedule, By);
                Applied = true;
                MessageBox.Show(this, msg, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, "Konnte nicht gespeichert werden.\n" + ex.Message, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

#if TESTHOOKS
        // Roundtrip-Test: geladenes Muster unveraendert wieder speichern.
        public string TestSave(string path) { Package p = Collect(); p.Save(path); return "dirty=" + dirty; }

        // Selbsttest der Bedienablaeufe am sichtbaren Editor: Schritte waehlen, hinzufuegen, duplizieren, verschieben,
        // Art/Wiederholung aendern, entfernen, leeres und neues Paket. Ausnahmen (z. B. Owner-Draw-Index) landen im Ergebnis.
        public static string TestFlow(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("Ablauftest"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                int n0 = f.steps.Count;
                foreach (PackageStep s in f.Ordered()) { f.Bind(s); Application.DoEvents(); }
                PackageStep e = new PackageStep(); e.Day = f.NextDay(); f.AddStep(e); Application.DoEvents();
                f.tTitle.Text = "Test äöü"; f.cbKind.SelectedIndex = 2; f.nCount.Value = 3; f.tSubNos.Text = "1/2, 3"; Application.DoEvents();
                f.Duplicate(); Application.DoEvents();
                f.Bind(f.Ordered()[0]); f.MoveStep(1); f.MoveStep(-1); f.MoveStep(-1); Application.DoEvents();
                int n1 = f.steps.Count;
                f.RemoveStep(false); f.RemoveStep(false); Application.DoEvents();
                int n2 = f.steps.Count;
                f.LoadPackage(new Package()); Application.DoEvents();
                bool empty = f.cur == null && f.lbHints.Items.Count > 0 && !f.body.Visible;
                f.LoadPackage(null); Application.DoEvents();
                bool fresh = f.steps.Count > 0 && f.cur != null && !f.dirty;
                return "Paket-Editor Ablauf: Schritte " + n0 + " -> +2 = " + n1 + " -> -2 = " + n2 + (n1 == n0 + 2 && n2 == n0 ? " ok" : " FEHLER")
                    + ", leeres Paket " + (empty ? "ok" : "FEHLER") + ", neues Paket " + (fresh ? "ok" : "FEHLER");
            }
            catch (Exception ex) { return "Paket-Editor Ablauf: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
#endif
    }
}
