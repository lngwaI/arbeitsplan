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

        readonly Panel left = new Panel();
        readonly Label lBlocks = new Label();
        readonly ListView lv = new ListView();
        readonly Button bAdd = new Button(), bUp = new Button(), bDown = new Button(), bMoreStep = new Button();
        readonly ToolStripMenuItem bDup = new ToolStripMenuItem(), bDel = new ToolStripMenuItem();   // Menue "Mehr ▾" unter dem Ablauf
        readonly ContextMenuStrip menuStep = new ContextMenuStrip();
        readonly ToolTip tip = new ToolTip();
        Button bApplyBtn;   // "In Plan uebernehmen" (Standardschaltflaeche)

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
            MinimumSize = new Size(960, 640);   // kleiner nicht: sonst bleibt fuer "Schritt-Details" zu wenig Platz (OnLoad begrenzt auf den Bildschirm)
            cat = store.GetCatalog();
            cat.AddStandardBausteine();   // alte Bestaende: Puffer, Freeze / Thaw, CIP / SIP, Einwaage anbieten (gespeichert erst mit "Katalog einrichten")
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) { rowNames.Add(r.Name); rowVars[r.Name] = r.VariantList(); }

            Panel head = BuildHead();
            Control details = BuildDetails();
            BuildLeft();
            Control preview = BuildPreview();
            Panel bar = BuildBar();
            Controls.Add(details); Controls.Add(left); Controls.Add(paneSplit); Controls.Add(preview); Controls.Add(bar); Controls.Add(head);   // zuletzt hinzugefuegt = zuerst angedockt
            head.TabIndex = 0; left.TabIndex = 1; details.TabIndex = 2; preview.TabIndex = 3; bar.TabIndex = 4;
            InitSizing();   // Doppelpuffer, Layoutsperre beim Ziehen (PackageForm.Sizing.cs)
            Theme.Current.Style(this);
            bApplyBtn.FlatAppearance.BorderColor = Theme.Current.BorderSel; bApplyBtn.FlatAppearance.BorderSize = 2;   // Primaraktion farblich abgesetzt
            ClampPane();
            LoadPackage(initial);
            ActiveControl = lv;   // Start im Ablauf, nicht mit markiertem Namen
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle wa = Screen.FromControl(this).WorkingArea;   // kleiner Bildschirm (z. B. 1024x768): Fenster passt sich an, Bereiche scrollen
            MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
            if (Width <= wa.Width && Height <= wa.Height) return;
            Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
            Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
        }

        protected override void OnResize(EventArgs e)
        {
            bool narrow = ClientSize.Width < 1150;   // schmales Fenster: mehr Platz fuer Schritt-Details und Kalender, schmalere Hinweis-/Vorschauspalte
            left.Width = Math.Max(360, Math.Min(600, ClientSize.Width * (narrow ? 38 : 46) / 100));
            if (hintsCol != null) { hintsCol.Width = narrow ? 210 : 290; pagerCol.Width = narrow ? 290 : 372; }
            base.OnResize(e);
            ClampPane();
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
            Section flow = new Section("1   Ablauf", "", true, w);
            lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.ShowItemToolTips = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable; lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Nr", 26); lv.Columns.Add("Wann", 160); lv.Columns.Add("Schritt", 140);   // Raum, Läufe/Termine, Wiederholung: Tooltip der Zeile (Unterelemente 3-5 bleiben gefuellt)
            lv.SelectedIndexChanged += delegate { if (!loading && lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag != cur) Bind((PackageStep)lv.SelectedItems[0].Tag); };
            lv.MouseUp += delegate { if (lv.SelectedItems.Count == 0) FillList(); };   // Klick ins Leere: Auswahl bleibt beim aktuellen Schritt
            lv.Resize += delegate { FitColumns(); };
            InitListDrag();   // Ziehen = Reihenfolge aendern, Einfuegelinie zeigt das Ziel (PackageForm.Order.cs)

            int bh = Theme.Current.ButtonH - 2;
            Panel btns = new Panel(); btns.Tag = "keep"; btns.Dock = DockStyle.Bottom; btns.Height = bh + 8; btns.Width = w - 16;
            Btn(bAdd, "+ Schritt", 0, 4, 100, delegate { AddStepDialog(); });
            bAdd.Font = new Font(Font, FontStyle.Bold);
            Btn(bUp, "▲", 106, 4, 34, delegate { MoveStep(-1); });
            Btn(bDown, "▼", 144, 4, 34, delegate { MoveStep(1); });
            Btn(bMoreStep, "Mehr ▾", 184, 4, 90, delegate { menuStep.Show(bMoreStep, new Point(0, bMoreStep.Height)); });
            bDup.Text = "Duplizieren"; bDup.ShortcutKeyDisplayString = "Strg+D"; bDup.Click += delegate { Duplicate(); };
            bDel.Text = "Entfernen…"; bDel.ShortcutKeyDisplayString = "Entf"; bDel.Click += delegate { RemoveStep(); };
            menuStep.Items.Add(bDup); menuStep.Items.Add(bDel);
            btns.Controls.AddRange(new Control[] { bAdd, bUp, bDown, bMoreStep });
            BuildBufferPkg(menuStep);   // Pufferpaket (PackageForm.BufferPkg.cs)
            tip.SetToolTip(bAdd, "Baustein (Packen, Lauf, Splitten …) oder leeren Schritt hinzufügen");
            tip.SetToolTip(lv, "Nach Datum sortiert (wie im Plan); gleicher Tag: deine Reihenfolge. Anklicken = rechts bearbeiten. Ziehen oder Alt+↑/↓ = Reihenfolge ändern (über die Tagesgrenze: übernimmt den Tag des Nachbarn). Raum und Wiederholung stehen im Tooltip der Zeile.");
            tip.SetToolTip(bUp, "Schritt nach oben (Alt+Hoch); übernimmt dabei ggf. den Tag des Vorgängers");
            tip.SetToolTip(bDown, "Schritt nach unten (Alt+Runter); übernimmt dabei ggf. den Tag des Nachfolgers");
            tip.SetToolTip(bMoreStep, "Duplizieren (Strg+D), Entfernen (Entf), Pufferpaket");
            lBlocks.Dock = DockStyle.Bottom; lBlocks.Height = 2 * Font.Height + 4; lBlocks.ForeColor = Section.HelpColor; lBlocks.Visible = false;
            flow.Controls.Add(lv); flow.Controls.Add(lBlocks); flow.Controls.Add(btns);
            flow.Dock = DockStyle.Fill;

            left.Controls.Add(flow);
        }

        Panel BuildBar()
        {
            Panel bar = new Panel(); bar.Dock = DockStyle.Bottom; bar.Height = Theme.Current.ButtonH + 14;
            int y = 7;
            ContextMenuStrip menuFile = new ContextMenuStrip();   // Datei ▾: Oeffnen, Speichern unter, Katalog einrichten (mit Recht)
            Button bFile = new Button(); Btn(bFile, "Datei ▾", 8, y, 96, delegate { menuFile.Show(bFile, new Point(0, 0), ToolStripDropDownDirection.AboveRight); });
            menuFile.Items.Add("Öffnen…", null, delegate { OpenFile(); });
            menuFile.Items.Add("Speichern unter…", null, delegate { SaveFile(true); });
            if (canAdmin) menuFile.Items.Add("Katalog einrichten…", null, delegate { Setup(); }).ToolTipText = "Equipment-Positionen und Bausteine einrichten (Recht \"Pakete einrichten\")";
            Button bSave = Btn(new Button(), "Speichern", 110, y, 96, delegate { SaveFile(false); });
            Button bClose = Btn(new Button(), "Schließen", 0, y, 100, delegate { Close(); });
            Button bApply = Btn(new Button(), "In Plan übernehmen…", 0, y, 170, delegate { DoApply(); });
            bApply.Enabled = canApply; bApply.Font = new Font(Font, FontStyle.Bold);
            if (canApply) AcceptButton = bApply;   // Enter = Primaraktion (Kampagne starten), Esc = Schliessen (mit Rueckfrage bei ungespeicherten Aenderungen)
            CancelButton = bClose; bApplyBtn = bApply;
            Control[] ord = { bFile, bSave, bApply, bClose };
            for (int i = 0; i < ord.Length; i++) ord[i].TabIndex = i;
            bClose.Left = ClientSize.Width - 108; bApply.Left = bClose.Left - 176;
            bClose.Anchor = bApply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Width = ClientSize.Width;
            bar.Controls.AddRange(ord);
            tip.SetToolTip(bFile, "Öffnen, Speichern unter" + (canAdmin ? ", Katalog einrichten" : ""));
            tip.SetToolTip(bSave, "Muster als Datei (.apkg) speichern (Strg+S)");
            tip.SetToolTip(bApply, "Kampagne starten: Startdatum, erste Laufnummer und Umgang mit vorhandenen Einträgen wählen");
            return bar;
        }

        // ---------- Schritte ----------

        // Einmal-Bausteine: ein weiterer braucht PackageAdmin. Packen nie (zweites Packen in anderem Raum, Hinweis in AddStep).
        bool UniqueBlocked(string source)
        {
            Baustein b = cat.Baustein_(source);
            if (b == null || !b.Unique || canAdmin || b.Name == Package.PackenName || !steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; })) return false;
            MessageBox.Show(this, "\"" + b.Name + "\" ist in jeder Kampagne nur einmal vorgesehen. Ein weiterer Schritt braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        PackageStep FromBaustein(Baustein b)
        {
            PackageStep s = Package.FromBaustein(b, steps.Count == 0 ? 1 : NextDay(), NewRunNo());
            if (RowInfo.IsFreezeName(b.Name)) s.Freeze = NewFreezeTemplate();   // P6: Freeze-Vorgang im Plan standardmaessig an (alte Pakete unveraendert)
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
                    PackageStep m = FreePacken();
                    if (m == null)
                    {
                        MessageBox.Show(this, !steps.Exists(delegate(PackageStep x) { return x.IsPack; }) ? "Der Back-Up gehört zu \"Packen\" – zuerst den Schritt \"Packen\" einfügen." : "Zu \"Packen\" gibt es schon einen Back-Up.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        void AddStep(PackageStep s) { bool more = NewPacken(s); steps.Add(s); AdoptPackNo(s); if (more) steps.Add(Package.MakeBackup(s, cat.Baustein_(Package.BackupName))); StepsView().Normalize(); dirty = true; Bind(s); CommitUndo(); if (more) Say("Zweites Packen: anderen Raum und Skid wählen"); }   // Normalize: ein Puffer "1 Tag vor Lauf 1" darf nicht vor Tag 1 landen

        void Duplicate()
        {
            if (cur == null || cur.IsBackup || UniqueBlocked(cur.Source)) return;   // Back-Up gibt es nur einmal je Packen
            PackageStep c = cur.Clone();
            if (c.IsPack) c.ManualNo = "";   // jedes Packen hat seine eigene Nummer
            if (!IsJe(c)) { List<int> d = c.Days(pk.Day1); int first = d[0]; foreach (int x in d) first = Math.Min(first, x); c.MoveAll(NextDay() - first, pk.Day1); }   // auch Tagesliste/Wochentage wandern mit
            AddStep(c);
        }

        // Verschieben in der Ablaufliste (Pfeile, Alt+Hoch/Runter, Ziehen): PackageForm.Order.cs

        void RemoveStep() { RemoveStep(true); }

        // Anzahl Schritte "zu jedem Lauf", die am Hauptlauf s haengen (0 = s ist nicht der Hauptlauf)
        internal int HeadDeps(PackageStep s) { return s == null || s != StepsView().Head ? 0 : steps.FindAll(delegate(PackageStep x) { return x != s && !x.IsEmpty && x.Bind == StepBind.JeLauf; }).Count; }

        void RemoveStep(bool ask)
        {
            if (cur == null) return;
            if (!StepsView().CanRemove(cur))   // gilt fuer alle: die Kampagne braucht ihren Hauptlauf
            {
                if (ask) MessageBox.Show(this, "\"" + TitleOf(cur) + "\" ist der Hauptlauf der Kampagne und lässt sich nicht entfernen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Baustein b = cat.Baustein_(cur.Source);
            if (b != null && b.Unique && !canAdmin && cur.Source != Package.PackenName)   // Packen darf jeder entfernen, der Pakete bearbeitet
            {
                MessageBox.Show(this, "\"" + b.Name + "\" ist ein Standard-Schritt der Kampagne. Entfernen braucht das Recht \"Pakete einrichten\".", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int deps = HeadDeps(cur);   // N7: Hauptlauf (nur ausserhalb von Kampagnen entfernbar) mit abhaengigen "zu jedem Lauf"-Schritten: eigene Rueckfrage, Standard Abbrechen
            List<PackageStep> gone = steps.FindAll(delegate(PackageStep x) { return x.BacksUp(cur); });   // Packen nimmt seinen Back-Up mit
            if (ask && deps > 0 && MessageBox.Show(this, "Schritt " + No(cur) + " \"" + TitleOf(cur) + "\" ist der Hauptlauf. " + deps + (deps == 1 ? " Schritt folgt" : " Schritte folgen")
                + " ihm „zu jedem Lauf“ und " + (deps == 1 ? "verliert" : "verlieren") + " damit ihren Takt (ihre Termine ändern sich).\n\nHauptlauf trotzdem entfernen?", "Hauptlauf entfernen",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
            if (ask && deps == 0 && MessageBox.Show(this, "Schritt " + No(cur) + " \"" + TitleOf(cur) + "\" (" + WhenText(cur) + ")" + (gone.Count > 0 ? " und seinen Back-Up" : "") + " aus dem Ablauf entfernen?", "Schritt entfernen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            List<PackageStep> o = Ordered();
            int i = o.IndexOf(cur);
            gone.Add(cur);
            foreach (PackageStep x in gone) { if (o.IndexOf(x) < i) i--; o.Remove(x); steps.Remove(x); }
            dirty = true;
            Bind(o.Count > 0 ? o[Math.Min(i, o.Count - 1)] : null);
            CommitUndo();
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
            lStatus.Text = dirty ? "● Ungespeicherte Änderungen" : file != null ? "Gespeichert: " + System.IO.Path.GetFileName(file) : "Noch nicht gespeichert";
            lStatus.ForeColor = dirty ? Theme.Current.BorderEdit : Section.HelpColor;
            tip.SetToolTip(lStatus, string.Join("  ›  ", parts.ToArray()));   // frueherer Weg "Produkt › Ablauf › Schritt"
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S)) { SaveFile(false); return true; }
            if (keyData == (Keys.Control | Keys.D)) { Duplicate(); return true; }
            if (UndoKey(keyData)) return true;
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

    }
}
