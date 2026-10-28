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
        readonly Dictionary<string, string[]> rowVars = new Dictionary<string, string[]>();   // Block -> Herstellungsvarianten (z. B. Puffer: mobil, stationaer)
        Catalog cat;
        readonly List<PackageStep> steps = new List<PackageStep>();
        List<PackageBlock> blocks = new List<PackageBlock>();
        PackageStep cur;
        bool loading, dirty;
        string file;            // Datei des Musters, null = noch nicht gespeichert
        public bool Applied;
        public DateTime AppliedStart;   // Startdatum der zuletzt uebernommenen Kampagne (Hauptfenster springt dorthin)
        public string Folder;   // Paketordner (Standard fuer Oeffnen/Speichern); null = klassischer Dateidialog
        public User By;        // angemeldeter Nutzer (fuer das Aenderungsprotokoll bei der Uebernahme)

        readonly Crumbs crumbs = new Crumbs();
        readonly Panel left = new Panel();
        readonly Label lBlocks = new Label();
        readonly ListView lv = new ListView();
        readonly Button bAdd = new Button(), bUp = new Button(), bDown = new Button(), bDup = new Button(), bDel = new Button();
        readonly ToolTip tip = new ToolTip();

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
            cat.AddStandardBausteine();   // alte Bestaende: Puffer, Freeze / Thaw, CIP / SIP, Einwaage anbieten (gespeichert erst mit "Katalog einrichten")
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) { rowNames.Add(r.Name); rowVars[r.Name] = r.VariantList(); }

            Panel head = BuildHead();
            Control details = BuildDetails();
            BuildLeft();
            Control preview = BuildPreview();
            Panel bar = BuildBar();
            crumbs.Height = Font.Height * 2 + 8; crumbs.TabStop = false; crumbs.PartClicked += CrumbClicked;
            Controls.Add(details); Controls.Add(left); Controls.Add(preview); Controls.Add(bar); Controls.Add(head); Controls.Add(crumbs);   // zuletzt hinzugefuegt = zuerst angedockt
            head.TabIndex = 0; left.TabIndex = 1; details.TabIndex = 2; preview.TabIndex = 3; bar.TabIndex = 4;
            InitSizing();   // Doppelpuffer, Layoutsperre beim Ziehen (PackageForm.Sizing.cs)
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

        // ---------- Ablauf (Produkt/Laufnummer: Kopfzeile, PackageForm.Head.cs) ----------

        void BuildLeft()
        {
            left.Dock = DockStyle.Left; left.Width = 400; left.Padding = new Padding(8, 8, 0, 8); left.Tag = "keep";
            int w = 360;
            Section flow = new Section("1   Ablauf", "Schritte nach Tag sortiert. Anklicken = rechts bearbeiten.", true, w);
            lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.ShowItemToolTips = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable; lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Nr", 30); lv.Columns.Add("Tag", 46); lv.Columns.Add("Raum", 76); lv.Columns.Add("Schritt", 110); lv.Columns.Add("Wiederholung", 96);
            lv.SelectedIndexChanged += delegate { if (!loading && lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag != cur) Bind((PackageStep)lv.SelectedItems[0].Tag); };
            lv.MouseUp += delegate { if (lv.SelectedItems.Count == 0) FillList(); };   // Klick ins Leere: Auswahl bleibt beim aktuellen Schritt
            lv.Resize += delegate { FitColumns(); };
            lv.AllowDrop = true;   // Ziehen = Reihenfolge aendern (wie mehrfach ▲/▼, uebernimmt den Tag am Ziel)
            lv.ItemDrag += delegate(object o, ItemDragEventArgs e)
            {
                PackageStep s = ((ListViewItem)e.Item).Tag as PackageStep;
                if (s == null) return;
                if (s != cur) Bind(s);
                lv.DoDragDrop(s, DragDropEffects.Move);
            };
            lv.DragOver += delegate(object o, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(typeof(PackageStep)) ? DragDropEffects.Move : DragDropEffects.None; };
            lv.DragDrop += delegate(object o, DragEventArgs e)
            {
                ListViewItem it = lv.GetItemAt(lv.PointToClient(new Point(e.X, e.Y)).X, lv.PointToClient(new Point(e.X, e.Y)).Y);
                if (it != null && it.Tag is PackageStep) DropAt(Ordered().IndexOf((PackageStep)it.Tag));
            };

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
            tip.SetToolTip(lv, "Anklicken = rechts bearbeiten. Ziehen = Reihenfolge ändern.");
            tip.SetToolTip(bUp, "Schritt nach oben (früher); übernimmt dabei ggf. den Tag des Vorgängers");
            tip.SetToolTip(bDown, "Schritt nach unten (später); übernimmt dabei ggf. den Tag des Nachfolgers");
            tip.SetToolTip(bDup, "Gewählten Schritt kopieren (Strg+D)");
            tip.SetToolTip(bDel, "Gewählten Schritt entfernen (Entf)");
            lBlocks.Dock = DockStyle.Bottom; lBlocks.Height = 2 * Font.Height + 4; lBlocks.ForeColor = Section.HelpColor; lBlocks.Visible = false;
            flow.Controls.Add(lv); flow.Controls.Add(lBlocks); flow.Controls.Add(btns);
            flow.Dock = DockStyle.Fill;

            left.Controls.Add(flow);
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
        static string TitleOf(PackageStep s) { return s.Title.Length > 0 ? s.Title : "(ohne Titel)"; }
        static string DaysOf(PackageStep s) { return s.Count > 1 ? s.Day + "–" + s.DayOf(s.Count - 1) : s.Day.ToString(); }
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
            foreach (PackageStep s in steps) d = Math.Max(d, s.DayOf(Math.Max(1, s.Count) - 1));
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
            if (s.Kind != StepKind.Normal) s.StartNo = NewRunNo();
            foreach (string sh in b.Positions) { EquipAssign a = new EquipAssign(); a.Short = sh; s.Equipment.Add(a); }
            return s;
        }

        // Erst die Art (Baustein oder "Sonstiges"), dann der Raum (vorbelegt, Pflicht)
        void AddStepDialog()
        {
            using (StepPicker p = new StepPicker(cat, steps, rowNames))
            {
                if (p.ShowDialog(this) != DialogResult.OK) return;
                if (p.Result != null && UniqueBlocked(p.Result.Name)) return;
                PackageStep s;
                if (p.Result == null) { s = new PackageStep(); s.Title = p.TitleText; s.Day = steps.Count == 0 ? 1 : NextDay(); }
                else s = FromBaustein(p.Result);
                s.Row = p.Room;
                AddStep(s);
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

        // Gewaehlten Schritt an Position "to" der Ablaufliste bringen (gezogen)
        void DropAt(int to)
        {
            for (int guard = 0; cur != null && guard < 200; guard++)
            {
                int i = Ordered().IndexOf(cur);
                if (i < 0 || i == to) break;
                MoveStep(i < to ? 1 : -1);
            }
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
            parts.Add((tName.Text.Trim().Length > 0 ? "Produkt „" + nm + "“" : "Produkt (noch ohne Namen)") + (Package.IsProductCode(Product) ? " (" + Product + ")" : ""));
            string camp = CampaignText();
            parts.Add("Ablauf: " + steps.Count + (steps.Count == 1 ? " Schritt" : " Schritte") + (camp != null ? " (" + camp + ")" : ""));
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
            prefix = p != null ? p.Prefix : "";   // alte Pakete: Produktcode (4 Ziffern) = Praefix
            string fr = p != null ? p.FirstRun : "";
            tFirst.Text = fr.Length > 0 ? fr : (prefix.Length > 0 ? Package.FormatRun(prefix, 1) : "");
            if (p == null) { foreach (Baustein b in cat.Bausteine) if (b.DefaultInCampaign) steps.Add(FromBaustein(b)); }   // z. B. "Packen" ist standardmaessig dabei
            else steps.AddRange(p.Steps);
            lBlocks.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
            lBlocks.Visible = blocks.Count > 0;
            loading = false;
            dirty = false;
            List<PackageStep> o = Ordered();
            Bind(o.Count > 0 ? o[0] : null);
        }

        // null = Kopfzeile ungueltig (Produktcode, erste Laufnummer, Ueberlauf; Meldung wurde gezeigt). Schritte als Kopie: der Kampagnenstart nummeriert um.
        Package Collect()
        {
            string err = ProductError() ?? FirstRunError() ?? StepsView().Overflow();
            if (err != null)
            {
                MessageBox.Show(this, err, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                (ProductError() != null ? tProduct : tFirst).Focus(); return null;
            }
            Package p = new Package(); p.Name = tName.Text.Trim(); p.ProductCode = Product; p.RunPrefix = prefix;
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
            string fn = PackagePicker.Pick(this, Folder, canAdmin);
            if (fn == null) return;
            try { LoadPackage(Package.Load(fn)); file = fn; UpdateHeader(); }
            catch (Exception ex) { MessageBox.Show(this, "Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        bool SaveFile(bool saveAs)
        {
            Package p = Collect();
            if (p == null || !BelongsOk(this, p, cat, "gespeichert")) return false;
            string f = file;
            if (saveAs || f == null)
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = p.Name.Length > 0 ? p.Name : "Paket";
                    PackagePicker.SetSaveFolder(d, Folder);
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
                if (!s.IsEmpty && (s.Row.Length == 0 || !rowNames.Contains(s.Row)))   // sonst fiele der Schritt beim Uebernehmen still weg
                {
                    Bind(s);
                    MessageBox.Show(this, "Schritt " + No(s) + " \"" + TitleOf(s) + "\" " + (s.Row.Length == 0 ? "hat keinen Raum." : "steht im Raum \"" + s.Row + "\", den es im Plan nicht gibt.")
                        + " Bitte unter \"Wo im Plan?\" einen Raum (Block) wählen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            DateTime st;
            if (StartCampaign(this, p, store, weekStart, By, out st)) { Applied = true; AppliedStart = st; }
        }

        // Kampagnenstart (Startdatum, erste Laufnummer, Termine je Lauf, Modus) und Uebernahme in den Plan; true = Plan geaendert.
        // Auch direkt aus dem Menue "Pakete > Kampagne starten…" (ohne Editor), wenn das Paket vollstaendig ist (Ready).
        public static bool StartCampaign(IWin32Window owner, Package p, IDataStore store, DateTime weekStart, User by, out DateTime start)
        {
            List<RunDate> schedule; ApplyMode mode;
            start = weekStart;
            if (!BelongsOk(owner, p, store.GetCatalog(), "übernommen")) return false;
            using (CampaignForm cf = new CampaignForm(p, store, weekStart.Date, ApplyMode.FillEmpty))
            {
                if (cf.ShowDialog(owner) != DialogResult.OK) return false;
                start = cf.Start; schedule = cf.Schedule; mode = cf.Mode;
            }
            try
            {
                string msg = p.Apply(store, start, mode, schedule, by);
                MessageBox.Show(owner, msg, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex) { MessageBox.Show(owner, "Konnte nicht gespeichert werden.\n" + ex.Message, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        }

        // Direkt startbar: hat Schritte, jeder (nicht leere) Schritt hat einen Raum, den es im Plan gibt
        public static bool Ready(Package p, IDataStore store)
        {
            if (p.Steps.Count == 0) return false;
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) names.Add(r.Name);
            foreach (PackageStep s in p.Steps) if (!s.IsEmpty && !names.Contains(s.Row)) return false;
            return true;
        }
    }
}
