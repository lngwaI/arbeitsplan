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
            left.Width = Math.Max(360, Math.Min(600, ClientSize.Width * 46 / 100));
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
            left.Dock = DockStyle.Left; left.Width = 520; left.Padding = new Padding(8, 8, 0, 8); left.Tag = "keep";
            int w = 480;
            Section flow = new Section("1   Ablauf", "Nach Datum sortiert (wie im Plan); gleicher Tag: deine Reihenfolge (▲▼).", true, w);
            lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.ShowItemToolTips = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable; lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Nr", 26); lv.Columns.Add("Wann", 140); lv.Columns.Add("Raum", 56); lv.Columns.Add("Schritt", 90); lv.Columns.Add("Läufe/Termine", 112); lv.Columns.Add("Wiederholung", 92);
            lv.SelectedIndexChanged += delegate { if (!loading && lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag != cur) Bind((PackageStep)lv.SelectedItems[0].Tag); };
            lv.MouseUp += delegate { if (lv.SelectedItems.Count == 0) FillList(); };   // Klick ins Leere: Auswahl bleibt beim aktuellen Schritt
            lv.Resize += delegate { FitColumns(); };
            InitListDrag();   // Ziehen = Reihenfolge aendern, Einfuegelinie zeigt das Ziel (PackageForm.Order.cs)

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
            tip.SetToolTip(lv, "Anklicken = rechts bearbeiten. Ziehen oder Alt+↑/↓ = Reihenfolge ändern (über die Tagesgrenze: übernimmt den Tag des Nachbarn).");
            tip.SetToolTip(bUp, "Schritt nach oben (Alt+Hoch); übernimmt dabei ggf. den Tag des Vorgängers");
            tip.SetToolTip(bDown, "Schritt nach unten (Alt+Runter); übernimmt dabei ggf. den Tag des Nachfolgers");
            tip.SetToolTip(bDup, "Gewählten Schritt kopieren (Strg+D)");
            tip.SetToolTip(bDel, "Gewählten Schritt entfernen (Entf)");
            lBlocks.Dock = DockStyle.Bottom; lBlocks.Height = 2 * Font.Height + 4; lBlocks.ForeColor = Section.HelpColor; lBlocks.Visible = false;
            flow.Controls.Add(lv); flow.Controls.Add(lBlocks); flow.Controls.Add(btns);
            flow.Dock = DockStyle.Fill;

            left.Controls.Add(flow);
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
                if (p.Result != null && p.Result.Name == Package.BackupName)
                {
                    PackageStep m = steps.Find(delegate(PackageStep x) { return x.BackupOf.Length == 0 && x.Source == Package.PackenName; });
                    if (m == null || Package.HasBackup(steps, m))
                    {
                        MessageBox.Show(this, m == null ? "Der Back-Up gehört zu \"Packen\" – zuerst den Schritt \"Packen\" einfügen." : "Zu \"Packen\" gibt es schon einen Back-Up.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    s = Package.MakeBackup(m, p.Result);
                }
                else if (p.Result == null) { s = new PackageStep(); s.Title = p.TitleText; s.Day = steps.Count == 0 ? 1 : NextDay(); }
                else s = FromBaustein(p.Result);
                if (!s.IsBackup) s.Row = p.Room;   // Back-Up bleibt im Raum des Packens
                if (!s.IsBackup) DefaultBind(s, p.Result);
                AddStep(s);
            }
        }

        // Neue Schritte folgen standardmaessig jedem Lauf des Hauptlaufs ("zu jedem Lauf"; Puffer 1 Tag davor); Packen, Sonderereignisse
        // und der erste Lauf (= Hauptlauf) bekommen ein eigenes Datum.
        void DefaultBind(PackageStep s, Baustein b)
        {
            if (StepsView().Head == null || s.Source == Package.PackenName || (b != null && b.Special)) return;
            s.Bind = StepBind.JeLauf; s.RunOffset = s.IsBuffer || s.UsesAnsatz ? -1 : 0; s.RunEvery = 1; s.NumberRuns = false;
        }

        void AddStep(PackageStep s) { steps.Add(s); StepsView().Normalize(); dirty = true; Bind(s); }   // Normalize: ein Puffer "1 Tag vor Lauf 1" darf nicht vor Tag 1 landen

        void Duplicate()
        {
            if (cur == null || cur.IsBackup || UniqueBlocked(cur.Source)) return;   // Back-Up gibt es nur einmal je Packen
            PackageStep c = cur.Clone();
            if (!IsJe(c)) { List<int> d = c.Days(pk.Day1); int first = d[0]; foreach (int x in d) first = Math.Min(first, x); c.MoveAll(NextDay() - first, pk.Day1); }   // auch Tagesliste/Wochentage wandern mit
            AddStep(c);
        }

        // Verschieben in der Ablaufliste (Pfeile, Alt+Hoch/Runter, Ziehen): PackageForm.Order.cs

        void RemoveStep() { RemoveStep(true); }

        void RemoveStep(bool ask)
        {
            if (cur == null) return;
            if (Package.HasBackup(steps, cur))   // gilt fuer alle, auch PackageAdmin: erst den Back-Up entfernen
            {
                if (ask) MessageBox.Show(this, "\"" + TitleOf(cur) + "\" hat einen Back-Up. Zuerst den Back-Up-Schritt entfernen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Baustein b = cat.Baustein_(cur.Source);
            if (b != null && b.Unique && !canAdmin)
            {
                MessageBox.Show(this, "\"" + b.Name + "\" ist ein Standard-Schritt der Kampagne. Entfernen braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ask && MessageBox.Show(this, "Schritt " + No(cur) + " \"" + TitleOf(cur) + "\" (" + WhenText(cur) + ") aus dem Ablauf entfernen?", "Schritt entfernen",
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
            if ((keyData == (Keys.Alt | Keys.Up) || keyData == (Keys.Alt | Keys.Down)) && (lv.Focused || bUp.Focused || bDown.Focused)) { MoveStep(keyData == (Keys.Alt | Keys.Up) ? -1 : 1); return true; }
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
            if (p != null) { p = p.Clone(); p.Migrate(); }   // Pakete, die nicht aus einer Datei kommen (Testpaket, Assistent): Schritte folgen dem Hauptlauf (Kopie, das Original bleibt)
            pk = new Package(); pk.Steps = steps;   // Kopf: Lauf 1 am, Typ, Wochentag von Tag 1
            if (p != null) { pk.Day1 = p.Day1; pk.Lauf1 = p.Lauf1; if (p.TypeIsSet) pk.Type = p.Type; }
            blocks = p != null ? p.Blocks : new List<PackageBlock>();
            tName.Text = p != null ? p.Name : ""; tProduct.Text = p != null ? p.ProductCode : "";
            prefix = p != null ? p.Prefix : "";   // alte Pakete: Produktcode (4 Ziffern) = Praefix
            string fr = p != null ? p.FirstRun : "";
            tFirst.Text = fr.Length > 0 ? fr : (prefix.Length > 0 ? Package.FormatRun(prefix, 1) : "");
            bool fresh = p == null || (p.Steps.Count == 0 && p.Blocks.Count == 0 && p.Name.Length == 0 && p.ProductCode.Length > 0);   // neues Paket mit vorbelegtem Produkt (Assistent)
            if (fresh)
            {
                foreach (Baustein b in cat.Bausteine) if (b.DefaultInCampaign) steps.Add(FromBaustein(b));   // z. B. "Packen" ist standardmaessig dabei
                PackageStep pack = steps.Find(delegate(PackageStep x) { return x.Source == Package.PackenName; });
                if (pack != null && cat.Baustein_(Package.BackupName) != null) steps.Add(Package.MakeBackup(pack, cat.Baustein_(Package.BackupName)));   // mit Back-Up
            }
            else steps.AddRange(p.Steps);
            lBlocks.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
            lBlocks.Visible = blocks.Count > 0;
            LoadHead();   // Typ, Lauf 1 am (PackageForm.Head.cs)
            loading = false;
            dirty = false;
            List<PackageStep> o = Ordered();
            Bind(o.Count > 0 ? o[0] : null);
        }

        // null = Kopfzeile ungueltig (Produktcode, erste Laufnummer, Ueberlauf; Meldung wurde gezeigt). Schritte als Kopie: der Kampagnenstart nummeriert um.
        Package Collect()
        {
            Package.SyncBackups(steps);
            string err = ProductError() ?? FirstRunError() ?? Package.BackupProblem(steps) ?? StepsView().Overflow();
            if (err != null)
            {
                MessageBox.Show(this, err, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                (ProductError() != null ? tProduct : tFirst).Focus(); return null;
            }
            Package p = new Package(); p.Name = tName.Text.Trim(); p.ProductCode = Product; p.RunPrefix = prefix;
            foreach (PackageStep s in steps) if (!s.IsEmpty) p.Steps.Add(s.Clone());
            p.Blocks = blocks;
            p.Lauf1 = pk.Lauf1; p.Day1 = pk.Day1;
            if (pk.TypeIsSet) p.Type = pk.Type;
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
            List<RunDate> schedule; ApplyMode mode; Package res;
            start = weekStart;
            if (!BelongsOk(owner, p, store.GetCatalog(), "übernommen")) return false;
            using (CampaignForm cf = new CampaignForm(p, store, weekStart.Date, ApplyMode.FillEmpty))
            {
                if (cf.ShowDialog(owner) != DialogResult.OK) return false;
                start = cf.Start; schedule = cf.Schedule; mode = cf.Mode; res = cf.Result;   // Kopie mit Laufanzahl, Nummern, Ausfaellen
            }
            try
            {
                string msg = res.Apply(store, start, mode, schedule, by);
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
