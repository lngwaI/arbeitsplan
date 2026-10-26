using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

// Nur .NET-4.0-APIs, C# 5 und Systemschrift (Windows-XP-Kompatibilitaet).
namespace Arbeitsplan.Win
{
    // Panel, das beim Fokuswechsel nicht selbst scrollt (sonst springt die Ansicht nach oben).
    class QuietPanel : Panel
    {
        protected override Point ScrollToControl(Control activeControl) { return DisplayRectangle.Location; }
    }

    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        const int WM_SETREDRAW = 0xB;
        static readonly int[] Swatches = { 0x000000, 0xC00000, 0x0050C8, 0x008000, 0xE06000, 0x7030A0, 0x808080 };
        static readonly string[] FontNames = { "Standardschrift", "Arial", "Tahoma", "Verdana", "Times New Roman", "Courier New" };
        static readonly string[] Sizes = { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24" };
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

        readonly IDataStore store;
        readonly ILockService locks = new MemoryLockService();
        readonly UserDb users = UserDb.Sample();
        readonly IEquipmentStatusProvider equip = new MockEquipmentProvider();
        readonly User me;
        readonly List<RowInfo> rowInfo = new List<RowInfo>();
        readonly Dictionary<int, bool> fold = new Dictionary<int, bool>();   // nur lokal in dieser Instanz, wird nicht gespeichert/synchronisiert
        readonly PlanView view;
        readonly Label weekLabel = new Label(), userLabel = new Label(), searchLabel = new Label(), hitsLabel = new Label();
        readonly TextBox search = new TextBox();
        readonly Panel topBar = new Panel(), dateBar = new Panel(), bar = new Panel(), fmtBar = new Panel();
        readonly QuietPanel canvas = new QuietPanel();
        readonly DayLabel[] dateLabels = new DayLabel[7];
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Bitmap pencilIcon = MakePencil();
        readonly Font plainFont;

        // Eingabefelder, nur im Bearbeitungsmodus sichtbar (liegen ueber Inhalt und Kuerzel des aktiven Blocks)
        readonly TextBox[] edPpl = { new TextBox(), new TextBox(), new TextBox() };
        readonly RichTextBox edContent = new RichTextBox();
        readonly List<Control> editors = new List<Control>();
        // Formatleiste
        readonly ComboBox cbFont = new ComboBox(), cbSize = new ComboBox();
        readonly Button btnBold = new Button(), btnItalic = new Button();
        readonly List<Control> fmtControls = new List<Control>();

        DateTime start;
        CellData editCell;
        bool loadingEditor, inResize, syncingFormat;
        int hitPos = -1;

        public MainForm(string login, bool failSaves)
        {
#if TESTHOOKS
            Text = "Arbeitsplan (Prototyp, lokale Testdaten) - Oberfläche: " + Theme.Current.Name;
#else
            Text = "Arbeitsplan (Testdaten, keine Speicherung) - Oberfläche: " + Theme.Current.Name;
#endif
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = Theme.Current.UiFont();    // je Modell Segoe UI / Tahoma (Rueckfall: Systemschrift)
            BackColor = Theme.Current.Window;
            plainFont = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            KeyPreview = true;

            start = MondayOf(DateTime.Today);
            MemoryStore ms = MemoryStore.Sample(start);
            ms.FailSaves = failSaves;
            store = ms;
#if TESTHOOKS
            locks.Acquire(start.AddDays(1), 1, "Paula Werk");   // Mock: Dienstag, Zeile 1 ist "in Bearbeitung"
#endif

            me = users.FindByLogin(login);
            if (me == null)
            {
                // Annahme (Prototyp): unbekannter Windows-Nutzer = Produktion mit Windows-Namen.
                me = new User(); me.Login = login;
#if TESTHOOKS
                UserDb.ApplyPackage(me.Rights, users.Package("Produktion"));
#else
                UserDb.ApplyPackage(me.Rights, users.Package("Leser"));   // Release: unbekannte Nutzer nur lesend
#endif
            }

            view = new PlanView(Font);
            view.Equip = equip;
            view.Colors = StatusColors.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "statuscolors.ini"));
            view.MyKuerzel = me.Kuerzel;
            view.Pencil = pencilIcon;
            view.Rows = rowInfo;
            view.CellActivated = BeginEdit;
            view.LkzClicked = ToggleLkz;
            view.RowToggled = ToggleRow;
            view.LayoutDone += delegate { PositionEditors(); hitsLabel.Text = view.Query.Length > 0 ? view.HitCount + " in dieser Woche" : ""; };
            view.MouseEnter += delegate { if (editCell == null && !(ActiveControl is TextBox)) view.Focus(); };   // Mausrad scrollt

            BuildEditors();
            BuildFormatBar();
            BuildTopBar();
            BuildBottomBar();

            dateBar.Dock = DockStyle.Top;
            dateBar.Height = 26;
            for (int c = 0; c < 7; c++)
            {
                DayLabel l = new DayLabel(Font);
                dateLabels[c] = l;
                dateBar.Controls.Add(l);
            }

            canvas.Dock = DockStyle.Fill;
            canvas.AutoScroll = true;
            canvas.Controls.Add(view);
            canvas.Resize += delegate { ResizeView(); };

            Controls.Add(canvas);
            Controls.Add(dateBar);
            Controls.Add(fmtBar);
            Controls.Add(topBar);
            Controls.Add(BuildMenu());
            Controls.Add(bar);

            KeyDown += OnKey;
            FormClosing += OnClosing;
            Theme.Current.Style(this);
            Rebuild(true);
        }

        // ---------- Aufbau der Leisten ----------

        MenuStrip BuildMenu()
        {
            MenuStrip ms = new MenuStrip();
            bool exp = me.Has(Perm.Export), pkg = me.Has(Perm.ImportPackage) || exp;
            bool structure = me.Has(Perm.EditNames) || me.Has(Perm.EditHeight) || me.Has(Perm.SortRows);
            bool usr = me.Has(Perm.UserCreate) || me.Has(Perm.UserEdit) || me.Has(Perm.UserDelete);

            ToolStripMenuItem file = new ToolStripMenuItem("Woche");
            file.DropDownItems.Add("Als Bild speichern (PNG)…", null, delegate { ExportImage(false); });
            file.DropDownItems.Add("Als PDF speichern…", null, delegate { ExportImage(true); });
            file.DropDownItems.Add("In Zwischenablage kopieren", null, delegate { ExportClipboard(); });
            file.DropDownItems.Add("Als Paket speichern (.apkg)…", null, delegate { ExportPackage(); });
            file.Enabled = exp;

            ToolStripMenuItem pk = new ToolStripMenuItem("Pakete");
            pk.DropDownItems.Add("Paket-Editor…", null, delegate { OpenPackageEditor(null); });
            pk.DropDownItems.Add("Paket aus Datei importieren…", null, delegate { ImportPackageFile(); });
            pk.Enabled = pkg;

            ToolStripMenuItem set = new ToolStripMenuItem("Einstellungen");
            ToolStripItem st = set.DropDownItems.Add("Blockstruktur…", null, delegate { EditStructure(); }); st.Enabled = structure;
            ToolStripItem us = set.DropDownItems.Add("Nutzerverwaltung…", null, delegate { using (UserAdminForm f = new UserAdminForm(users, me)) f.ShowDialog(this); }); us.Enabled = usr;
            ToolStripItem cf = set.DropDownItems.Add("Equipment && Bausteine…", null, delegate { EditCatalog(); }); cf.Enabled = me.Has(Perm.PackageAdmin);
            set.Visible = structure || usr || me.Has(Perm.PackageAdmin);

            ms.Items.AddRange(new ToolStripItem[] { file, pk, set });
            ms.Dock = DockStyle.Top;
            return ms;
        }

        void BuildTopBar()
        {
            weekLabel.Dock = DockStyle.Fill;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

            // rechts: Suche in Inhalt/Equipment/Kuerzel (nicht Raumnamen)
            searchLabel.Text = "Suchen:"; searchLabel.AutoSize = true; searchLabel.Location = new Point(0, 9);
            search.Width = 130; search.Location = new Point(searchLabel.PreferredWidth + 6, 6);
            hitsLabel.AutoSize = true; hitsLabel.Location = new Point(search.Right + 6, 9);
            search.TextChanged += delegate { OnSearchChanged(); };
            Ui.SimpleMenu(search);
            search.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { NextHit(); e.SuppressKeyPress = true; } };
            Panel sp = new Panel();
            sp.Dock = DockStyle.Right; sp.Width = search.Right + 130;
            sp.Controls.Add(searchLabel); sp.Controls.Add(search); sp.Controls.Add(hitsLabel);

            topBar.Dock = DockStyle.Top; topBar.Height = Theme.Current.BarH;
            topBar.Controls.Add(weekLabel);
            topBar.Controls.Add(sp);
        }

        void BuildBottomBar()
        {
            bar.Dock = DockStyle.Bottom;
            bar.Height = Theme.Current.ButtonH + 18;
            nav.AutoSize = true; nav.WrapContents = false; nav.Top = 6;
            AddNav("«  Woche", delegate { Go(-7); });
            AddNav("‹  Tag", delegate { Go(-1); });
            AddNav("Heute", delegate { Go(MondayOf(DateTime.Today) - start); });
            AddNav("Tag  ›", delegate { Go(1); });
            AddNav("Woche  »", delegate { Go(7); });
            userLabel.AutoSize = true;
            userLabel.Location = new Point(10, 16);
            userLabel.Text = "Angemeldet: " + me.Display + " (" + (users.PackageOf(me) != null ? users.PackageOf(me).Name : "benutzerdefiniert") + ")";
            bar.Controls.Add(nav); bar.Controls.Add(userLabel);
            bar.Resize += delegate { CenterBar(); };
            Shown += delegate { CenterBar(); };
        }

        void AddNav(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.AutoSize = true; b.MinimumSize = new Size(86, Theme.Current.ButtonH + 4); b.Margin = new Padding(3, 0, 3, 0);
            b.Click += click;
            Theme.Current.StyleButton(b);
            nav.Controls.Add(b);
        }

        void CenterBar() { nav.Left = Math.Max(userLabel.Right + 10, (bar.ClientSize.Width - nav.Width) / 2); }

        void BuildEditors()
        {
            edContent.Font = plainFont;
            edContent.BorderStyle = BorderStyle.None; edContent.ScrollBars = RichTextBoxScrollBars.None; edContent.DetectUrls = false;
            edContent.HideSelection = false; edContent.AcceptsTab = false;
            edContent.TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.Content = Norm(edContent.Text); editCell.Dirty = true; view.Relayout(); } };
            edContent.SelectionChanged += delegate { SyncFormat(); };
            edContent.KeyDown += OnContentKey;
            edContent.ContextMenuStrip = BuildContentMenu();
            edContent.Visible = false; view.Controls.Add(edContent); editors.Add(edContent);
            for (int s = 0; s < 3; s++)
            {
                int si = s;
                TextBox t = edPpl[s];
                t.BorderStyle = BorderStyle.None; t.AutoSize = false; t.Multiline = true; t.AcceptsReturn = true; t.Visible = false;
                Ui.SimpleMenu(t);          // schlichtes Menue statt Windows-Standard (keine Unicode-/IME-Eintraege)
                t.TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.People[si] = Norm(t.Text); editCell.Dirty = true; view.Relayout(); } };
                view.Controls.Add(t); editors.Add(t);
            }
        }

        // ---------- Format: Leiste und Kontextmenue (Rechtsklick auf markierten Text) ----------
        // Die Leiste gilt fuer die Markierung; ohne Markierung gilt die Auswahl fuer den danach getippten Text.

        void BuildFormatBar()
        {
            fmtBar.Dock = DockStyle.Top; fmtBar.Height = 32;
            fmtBar.Visible = me.Has(Perm.Format);
            int x = 6;
            Label l = new Label(); l.Text = "Textformat:"; l.AutoSize = true; l.Location = new Point(x, 9); fmtBar.Controls.Add(l); x += 78;
            cbFont.DropDownStyle = ComboBoxStyle.DropDownList; cbFont.SetBounds(x, 5, 130, 22); cbFont.Items.AddRange(FontNames); cbFont.SelectedIndex = 0;
            cbFont.SelectedIndexChanged += delegate { if (!syncingFormat) ChangeFont(delegate(Font f) { return MakeFont(cbFont.SelectedIndex == 0 ? plainFont.Name : cbFont.SelectedItem.ToString(), f.SizeInPoints, f.Style); }); };
            x += 136;
            cbSize.DropDownStyle = ComboBoxStyle.DropDownList; cbSize.SetBounds(x, 5, 52, 22); cbSize.Items.AddRange(Sizes); cbSize.SelectedItem = ((int)Math.Round(plainFont.SizeInPoints)).ToString();
            cbSize.SelectedIndexChanged += delegate { if (!syncingFormat) { float sz = float.Parse(cbSize.SelectedItem.ToString()); ChangeFont(delegate(Font f) { return MakeFont(f.Name, sz, f.Style); }); } };
            x += 58;
            btnBold.Text = "F"; btnBold.Font = new Font("Arial", 9f, FontStyle.Bold); btnBold.SetBounds(x, 4, 26, 24); x += 28;
            btnItalic.Text = "K"; btnItalic.Font = new Font("Arial", 9f, FontStyle.Italic); btnItalic.SetBounds(x, 4, 26, 24); x += 34;
            btnBold.Click += delegate { ToggleStyle(FontStyle.Bold); };
            btnItalic.Click += delegate { ToggleStyle(FontStyle.Italic); };
            fmtBar.Controls.Add(cbFont); fmtBar.Controls.Add(cbSize); fmtBar.Controls.Add(btnBold); fmtBar.Controls.Add(btnItalic);
            foreach (int rgb in Swatches)
            {
                int col = rgb;
                Button b = new Button();
                b.SetBounds(x, 5, 22, 22); x += 24; b.FlatStyle = FlatStyle.Flat; b.TabStop = false;
                b.BackColor = Ui.Rgb(col);
                b.Click += delegate { ApplyColor(col); MarkSwatch(b); };
                if (Theme.Current.FlatButtons) { b.Size = new Size(20, 20); b.Top = 6; b.FlatAppearance.BorderColor = Theme.Current.SwatchBorder; b.FlatAppearance.BorderSize = 1; b.FlatAppearance.MouseOverBackColor = b.BackColor; b.FlatAppearance.MouseDownBackColor = b.BackColor; }
                fmtBar.Controls.Add(b); fmtControls.Add(b);
            }
            Button more = new Button(); more.Text = "Farbe…"; more.SetBounds(x + 4, 4, 62, 24); more.Click += delegate { PickColor(); }; x += 70;
            Button clear = new Button(); clear.Text = "Standardformat"; clear.SetBounds(x, 4, 110, 24); clear.Click += delegate { ClearFormat(); };
            foreach (Button sb in new Button[] { btnBold, btnItalic, more, clear }) Theme.Current.StyleButton(sb);
            fmtBar.Controls.Add(more); fmtBar.Controls.Add(clear);
            fmtControls.AddRange(new Control[] { cbFont, cbSize, btnBold, btnItalic, more, clear });
            foreach (Control c in fmtControls) c.Enabled = false;
        }

        ContextMenuStrip BuildContentMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Ausschneiden", null, delegate { edContent.Cut(); });
            m.Items.Add("Kopieren", null, delegate { edContent.Copy(); });
            m.Items.Add("Einfügen", null, delegate { PastePlain(); });
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem size = new ToolStripMenuItem("Schriftgröße");
            foreach (string s in Sizes) { string v = s; size.DropDownItems.Add(v, null, delegate { float sz = float.Parse(v); ChangeFont(delegate(Font f) { return MakeFont(f.Name, sz, f.Style); }); }); }
            ToolStripMenuItem fnt = new ToolStripMenuItem("Schriftart");
            foreach (string s in FontNames) { string v = s; fnt.DropDownItems.Add(v, null, delegate { ChangeFont(delegate(Font f) { return MakeFont(v == FontNames[0] ? plainFont.Name : v, f.SizeInPoints, f.Style); }); }); }
            ToolStripMenuItem col = new ToolStripMenuItem("Schriftfarbe");
            string[] names = { "Schwarz", "Rot", "Blau", "Grün", "Orange", "Violett", "Grau" };
            for (int i = 0; i < Swatches.Length; i++)
            {
                int rgb = Swatches[i];
                ToolStripItem it = col.DropDownItems.Add(names[i], null, delegate { ApplyColor(rgb); });
                it.ForeColor = Ui.Rgb(rgb);
            }
            col.DropDownItems.Add("Weitere Farben…", null, delegate { PickColor(); });
            m.Items.Add(size); m.Items.Add(fnt); m.Items.Add(col);
            m.Items.Add("Fett", null, delegate { ToggleStyle(FontStyle.Bold); });
            m.Items.Add("Kursiv", null, delegate { ToggleStyle(FontStyle.Italic); });
            m.Items.Add("Standardformat", null, delegate { ClearFormat(); });
            m.Opening += delegate { bool f = me.Has(Perm.Format); for (int i = 4; i < m.Items.Count; i++) m.Items[i].Enabled = f; };
            return m;
        }

        Font MakeFont(string name, float size, FontStyle style) { return FontCache.Get(name, size, style, plainFont); }

        void ChangeFont(Func<Font, Font> change)
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            Font cur = edContent.SelectionFont ?? plainFont;
            edContent.SelectionFont = change(cur);
            edContent.Focus();
        }

        void ToggleStyle(FontStyle s)
        {
            ChangeFont(delegate(Font f) { return MakeFont(f.Name, f.SizeInPoints, (f.Style & s) != 0 ? f.Style & ~s : f.Style | s); });
        }

        Button markedSwatch;
        // Moderner Look: das zuletzt gewaehlte Farbfeld bekommt einen Auswahlrahmen.
        void MarkSwatch(Button b)
        {
            if (!Theme.Current.FlatButtons) return;
            if (markedSwatch != null) { markedSwatch.FlatAppearance.BorderColor = Theme.Current.SwatchBorder; markedSwatch.FlatAppearance.BorderSize = 1; }
            markedSwatch = b;
            b.FlatAppearance.BorderColor = Theme.Current.SwatchSelected; b.FlatAppearance.BorderSize = 2;
        }

        void ApplyColor(int rgb)
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            edContent.SelectionColor = Ui.Rgb(rgb);
            edContent.Focus();
        }

        void PickColor()
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            using (ColorDialog d = new ColorDialog())
                if (d.ShowDialog(this) == DialogResult.OK) { edContent.SelectionColor = d.Color; edContent.Focus(); }
        }

        void ClearFormat()
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            edContent.SelectionFont = plainFont; edContent.SelectionColor = Color.Black; edContent.Focus();
        }

        // Zeigt Schrift/Groesse/Fett/Kursiv der Markierung in der Leiste an.
        void SyncFormat()
        {
            Font f = edContent.SelectionFont;
            if (f == null || loadingEditor) return;
            syncingFormat = true;
            int i = Array.IndexOf(FontNames, f.Name);
            cbFont.SelectedIndex = f.Name == plainFont.Name ? 0 : (i > 0 ? i : 0);
            cbSize.SelectedItem = ((int)Math.Round(f.SizeInPoints)).ToString();
            btnBold.BackColor = f.Bold ? Theme.Current.ButtonOn : Theme.Current.ButtonBack;
            btnItalic.BackColor = f.Italic ? Theme.Current.ButtonOn : Theme.Current.ButtonBack;
            syncingFormat = false;
        }

        void PastePlain() { edContent.Paste(DataFormats.GetFormat(DataFormats.UnicodeText)); }   // fremde Formatierung wird nicht uebernommen

        void OnContentKey(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.U || e.KeyCode == Keys.E || e.KeyCode == Keys.R || e.KeyCode == Keys.L || e.KeyCode == Keys.J)) { e.SuppressKeyPress = true; return; }
            if (e.Control && (e.KeyCode == Keys.B || e.KeyCode == Keys.I) && !me.Has(Perm.Format)) { e.SuppressKeyPress = true; return; }
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { PastePlain(); e.SuppressKeyPress = true; }
            if (e.Shift && !e.Control && e.KeyCode == Keys.Enter) { edContent.SelectedText = "\n"; e.SuppressKeyPress = true; }   // wie Enter (kein weicher Zeilenumbruch)
        }

        // ---------- Navigation ----------

        static DateTime MondayOf(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }
        static string Norm(string s) { return s.Replace("\r\n", "\n"); }
        static string Win(string s) { return s.Replace("\r\n", "\n").Replace("\n", "\r\n"); }

        void Go(int days) { Go(TimeSpan.FromDays(days)); }

        void Go(TimeSpan delta)
        {
            if (!EndEdit(true)) return;           // Speichern fehlgeschlagen: nicht wechseln, nichts geht verloren
            start = start.Add(delta);
            Rebuild(true);
        }

        // Laedt die Woche neu und zeichnet nur neu (keine Steuerelemente werden erzeugt -> schnell).
        // resetFold: beim Wechsel der Woche gelten wieder die Standardwerte (leere Zeilen zugeklappt, belegte offen).
        void Rebuild(bool resetFold)
        {
            weekLabel.Text = "KW" + De.Calendar.GetWeekOfYear(start, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + " (" + start.ToString("dd.MM.yyyy", De) + " - " + start.AddDays(6).ToString("dd.MM.yyyy", De) + ")";
            for (int c = 0; c < 7; c++)
            {
                DateTime d = start.AddDays(c);
                dateLabels[c].Text = d.ToString("ddd, dd.MM.yyyy", De);
                dateLabels[c].Today = d == DateTime.Today; dateLabels[c].Invalidate();
            }
            int selRow = -1; DateTime selDay = DateTime.MinValue;
            if (view.Selected != null && view.Selected.RowIndex < rowInfo.Count) { selRow = rowInfo[view.Selected.RowIndex].Id; selDay = view.Selected.Day; }
            if (resetFold) fold.Clear();

            rowInfo.Clear(); rowInfo.AddRange(store.GetRows());
            view.Start = start;
            view.Cells.Clear();
            view.Selected = null;
            for (int r = 0; r < rowInfo.Count; r++)
                for (int c = 0; c < 7; c++)
                {
                    CellData d = new CellData();
                    d.Day = start.AddDays(c); d.Col = c; d.RowIndex = r;
                    d.Cur = store.Get(d.Day, rowInfo[r].Id) ?? new Entry();
                    d.Content = d.Cur.Content; d.People = (string[])d.Cur.People.Clone(); d.Lkz = d.Cur.Lkz;
                    view.Cells.Add(d);
                    if (rowInfo[r].Id == selRow && d.Day == selDay) view.Selected = d;
                }
            ApplyFold();
            ResizeView();
        }

        // ---------- Zeilen ein-/ausklappen (nur lokal) ----------

        bool RowHasContent(int r)
        {
            foreach (CellData c in view.Cells) if (c.RowIndex == r && (!c.Cur.IsEmpty || c.Cur.Lkz)) return true;
            return false;
        }

        bool[] DefaultFold()
        {
            bool[] d = new bool[rowInfo.Count];
            for (int r = 0; r < d.Length; r++) d[r] = !RowHasContent(r);   // nichts drin im Zeitraum -> zugeklappt
            return d;
        }

        void ApplyFold()
        {
            bool[] d = DefaultFold();
            for (int r = 0; r < d.Length; r++) { bool v; if (fold.TryGetValue(rowInfo[r].Id, out v)) d[r] = v; }
            view.Collapsed = d;
        }

        void ToggleRow(int r)
        {
            if (editCell != null && editCell.RowIndex == r && !EndEdit(true)) return;
            fold[rowInfo[r].Id] = !view.Collapsed[r];
            ApplyFold();
            view.Relayout();
        }

        void ResizeView()
        {
            if (inResize) return;
            inResize = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int cw = canvas.ClientSize.Width;
                    view.Width = cw;
                    view.MarkAllDirty();
                    view.Relayout();
                    if (canvas.ClientSize.Width == cw) break;   // Scrollbalken kann die Breite aendern
                }
                for (int c = 0; c < 7; c++) dateLabels[c].SetBounds(view.ColLeft(c), 2, view.ColW, 22);
            }
            finally { inResize = false; }
        }

        // ---------- Einstellungen: Blockstruktur ----------

        void EditStructure()
        {
            if (!EndEdit(true)) return;
            List<RowInfo> copy = store.GetRows();
            using (StructureForm f = new StructureForm(copy, me.Has(Perm.EditNames), me.Has(Perm.EditHeight), me.Has(Perm.SortRows)))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveStructure(f.Result); } catch (Exception ex) { ShowSaveError(ex); return; }
            }
            Rebuild(false);
        }

        void EditCatalog()
        {
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) names.Add(r.Name);
            using (CatalogForm f = new CatalogForm(store.GetCatalog(), names))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); } catch (Exception ex) { ShowSaveError(ex); }
            }
        }

        // ---------- Suche ----------

        void OnSearchChanged()
        {
            view.Query = search.Text.Trim();
            hitPos = -1;
            view.MarkAllDirty();
            view.Relayout();
        }

        // Enter: naechster Treffer (auch in anderen Wochen); Raumnamen werden nicht durchsucht.
        void NextHit()
        {
            if (view.Query.Length == 0) return;
            List<SearchHit> hits = store.Find(view.Query);
            if (hits.Count == 0) { hitsLabel.Text = "keine Treffer"; return; }
            hitPos = (hitPos + 1) % hits.Count;
            SearchHit h = hits[hitPos];
            if (h.Day < start || h.Day > start.AddDays(6))
            {
                if (!EndEdit(true)) return;
                start = MondayOf(h.Day);
                Rebuild(true);
            }
            foreach (CellData c in view.Cells)
                if (c.Day == h.Day && rowInfo[c.RowIndex].Id == h.RowId)
                {
                    if (view.Collapsed[c.RowIndex]) { fold[h.RowId] = false; ApplyFold(); view.Relayout(); }
                    view.Selected = c; view.Invalidate();
                    canvas.AutoScrollPosition = new Point(0, Math.Max(0, c.R.Y - 4));
                    break;
                }
            hitsLabel.Text = "Treffer " + (hitPos + 1) + " von " + hits.Count;
        }

        // ---------- LKZ-Haken ----------

        void ToggleLkz(CellData c)
        {
            bool allowed = me.Has(Perm.EditText) || (c.Cur.IsEmpty && me.Has(Perm.AddText));
            if (!allowed) { MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (c.Editing) { c.Lkz = !c.Lkz; view.Invalidate(); return; }   // wird mit dem Block gespeichert
            Entry n = c.Cur.Clone();
            n.Lkz = !c.Cur.Lkz;
            if (!TrySave(c, n)) return;                                     // Haken sofort speichern
            c.Cur = n; c.Lkz = n.Lkz;
            view.Invalidate();
        }

        // ---------- Bearbeitungsmodus (Soft-Lock) ----------

        void BeginEdit(CellData c)
        {
            if (editCell == c) return;
            bool allowed = me.Has(Perm.EditText) || (c.Cur.IsEmpty && me.Has(Perm.AddText));
            if (!allowed) { MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!EndEdit(true)) return;

            RowInfo ri = rowInfo[c.RowIndex];
            string holder = locks.Holder(c.Day, ri.Id, me.Display);
            if (holder != null)
            {
                string title = ri.Name.Length > 0 ? ri.Name : "Block " + (c.RowIndex + 1);
                MessageBox.Show("Das Fenster " + title + " wird gerade von " + holder + " bearbeitet.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            locks.Acquire(c.Day, ri.Id, me.Display);   // Soft-Lock: danach trotzdem bearbeiten und speichern

            editCell = c;
            c.Editing = true; c.Dirty = true;
            view.Selected = c;
            LoadEditors(c);
            foreach (Control f in fmtControls) f.Enabled = me.Has(Perm.Format);
            view.Relayout();
            edContent.Focus();
        }

        void LoadEditors(CellData c)
        {
            loadingEditor = true;
            bool today = c.Day.Date == DateTime.Today;
            bool weekend = c.Day.DayOfWeek == DayOfWeek.Saturday || c.Day.DayOfWeek == DayOfWeek.Sunday;
            Color back = today ? Theme.Current.CellBackToday : (weekend ? Theme.Current.CellBackWeekend : Theme.Current.CellBack);
            edContent.BackColor = back;
            edContent.Text = c.Cur.Content;
            edContent.SelectAll(); edContent.SelectionFont = plainFont; edContent.SelectionColor = Color.Black;
            foreach (FormatSpan sp in c.Cur.Spans)
            {
                if (sp.Start < 0 || sp.Start + sp.Length > edContent.TextLength) continue;
                edContent.Select(sp.Start, sp.Length);
                edContent.SelectionFont = PlanView.SpanFont(sp, plainFont);
                edContent.SelectionColor = Ui.Rgb(sp.Rgb);
            }
            edContent.Select(edContent.TextLength, 0);
            for (int s = 0; s < 3; s++) { edPpl[s].Text = Win(c.Cur.People[s]); edPpl[s].BackColor = Theme.Current.ShiftBack[s]; }
            loadingEditor = false;
            SyncFormat();
        }

        // Eingabefelder exakt ueber Inhalt und Kuerzel des Blocks legen (nach jedem Layout).
        void PositionEditors()
        {
            if (editCell == null) { foreach (Control c in editors) c.Visible = false; return; }
            CellData c0 = editCell;
            edContent.SetBounds(c0.RContent.X + PlanView.TP - 3, c0.RContent.Y + PlanView.TV, c0.RContent.Width - 2 * (PlanView.TP - 3) - 6, c0.RContent.Height - 2 * PlanView.TV);
            for (int s = 0; s < 3; s++)
                edPpl[s].SetBounds(c0.RPpl[s].X + PlanView.TP - 2, c0.RPpl[s].Y + PlanView.TV, c0.RPpl[s].Width - 2 * (PlanView.TP - 2), c0.RPpl[s].Height - 2 * PlanView.TV);
            foreach (Control c in editors) c.Visible = true;
        }

        // Manuelle Formatierung aus dem Editor lesen (Zeichen fuer Zeichen; Standard = kein Bereich).
        List<FormatSpan> ExtractSpans()
        {
            List<FormatSpan> res = new List<FormatSpan>();
            int n = edContent.TextLength;
            if (n == 0) return res;
            int s0 = edContent.SelectionStart, l0 = edContent.SelectionLength;
            SendMessage(edContent.Handle, WM_SETREDRAW, 0, 0);
            FormatSpan open = null;
            for (int i = 0; i <= n; i++)
            {
                FormatSpan a = null;
                if (i < n)
                {
                    edContent.Select(i, 1);
                    Font f = edContent.SelectionFont ?? plainFont;
                    Color c = edContent.SelectionColor;
                    FormatSpan t = new FormatSpan();
                    t.Rgb = (c.R << 16) | (c.G << 8) | c.B;
                    t.Size = Math.Abs(f.SizeInPoints - plainFont.SizeInPoints) > 0.05f ? f.SizeInPoints : 0;
                    t.Bold = f.Bold; t.Italic = f.Italic;
                    t.Font = f.Name != plainFont.Name ? f.Name : null;
                    if (t.Rgb != 0 || t.Size > 0 || t.Bold || t.Italic || t.Font != null) a = t;
                }
                bool same = open != null && a != null && open.Rgb == a.Rgb && open.Size == a.Size && open.Bold == a.Bold && open.Italic == a.Italic && open.Font == a.Font;
                if (open != null && !same) { open.Length = i - open.Start; res.Add(open); open = null; }
                if (open == null && a != null) { a.Start = i; open = a; }
            }
            edContent.Select(s0, l0);
            SendMessage(edContent.Handle, WM_SETREDRAW, 1, 0);
            return res;
        }

        Entry WorkEntry(CellData c)
        {
            Entry n = c.Cur.Clone();
            n.Content = Norm(edContent.Text);
            n.Spans = ExtractSpans();
            for (int s = 0; s < 3; s++) n.People[s] = Norm(edPpl[s].Text);
            n.Lkz = c.Lkz;
            return n;
        }

        bool EntryDirty(CellData c, Entry n)
        {
            return n.Content != c.Cur.Content || n.People[0] != c.Cur.People[0] || n.People[1] != c.Cur.People[1] || n.People[2] != c.Cur.People[2]
                || n.Lkz != c.Cur.Lkz || n.SpanKey() != c.Cur.SpanKey();
        }

        bool IsDirty(CellData c) { return EntryDirty(c, WorkEntry(c)); }

        // false = Speichern fehlgeschlagen; Block bleibt im Bearbeitungsmodus, Text bleibt erhalten.
        bool EndEdit(bool save)
        {
            CellData c = editCell;
            if (c == null) return true;
            if (save)
            {
                Entry n = WorkEntry(c);
                if (EntryDirty(c, n))
                {
                    if (!TrySave(c, n)) return false;
                    c.Cur = n;
                }
            }
            locks.Release(c.Day, rowInfo[c.RowIndex].Id, me.Display);
            c.Content = c.Cur.Content; c.People = (string[])c.Cur.People.Clone(); c.Lkz = c.Cur.Lkz;
            c.Editing = false; c.Dirty = true;
            editCell = null;
            foreach (Control f in fmtControls) f.Enabled = false;
            view.Relayout();
            return true;
        }

        bool TrySave(CellData c, Entry n)
        {
            int rowId = rowInfo[c.RowIndex].Id;
            try
            {
                if (store.Save(c.Day, rowId, n)) return true;
                Entry latest = store.Get(c.Day, rowId);
                if (MessageBox.Show("Der Block wurde inzwischen von jemand anderem geändert.\n\nJa = meine Fassung speichern (überschreibt die andere)\nNein = im Editor bleiben",
                    "Konflikt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                n.Version = latest != null ? latest.Version : 0;
                if (store.Save(c.Day, rowId, n)) return true;
                MessageBox.Show("Der Block wurde erneut geändert. Bitte noch einmal speichern.", "Konflikt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex) { ShowSaveError(ex); return false; }
        }

        static void ShowSaveError(Exception ex)
        {
            MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message +
                "\n\nIhre Änderung ist NICHT verloren und bleibt im Editor sichtbar.\nSpeichern später erneut versuchen (Strg+S) oder mit Esc beenden.",
                "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; return; }
            if (editCell == null) return;
            if (e.Control && e.KeyCode == Keys.S) { EndEdit(true); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                if (!IsDirty(editCell)) { EndEdit(false); return; }
                DialogResult r = MessageBox.Show("Änderungen speichern?", "Arbeitsplan", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) EndEdit(true);          // schlaegt Speichern fehl, bleibt der Block im Editor
                else if (r == DialogResult.No) EndEdit(false);
            }
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (EndEdit(true)) return;
            if (MessageBox.Show("Nicht gespeicherte Änderung verwerfen und schließen?", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                EndEdit(false);
            else e.Cancel = true;
        }

        // ---------- Export und Pakete ----------

        // Rendert die ganze Woche; zugeklappte Zeilen mit Inhalt werden dafuer aufgeklappt (nichts darf im Export fehlen).
        Bitmap RenderAll()
        {
            if (!EndEdit(true)) return null;
            bool[] keep = view.Collapsed; CellData sel = view.Selected;
            view.Collapsed = DefaultFold(); view.Selected = null; view.Relayout();
            Bitmap bmp = Export.RenderWeek(view, weekLabel.Text, start);
            view.Collapsed = keep; view.Selected = sel; view.Relayout();
            return bmp;
        }

        void ExportImage(bool pdf)
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = pdf ? "PDF (*.pdf)|*.pdf" : "PNG-Bild (*.png)|*.png";
                d.FileName = "Arbeitsplan_" + weekLabel.Text.Substring(0, weekLabel.Text.IndexOf(' '));
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Bitmap bmp = RenderAll()) { if (bmp == null) return; if (pdf) Export.SavePdf(bmp, d.FileName); else Export.SavePng(bmp, d.FileName); }
                }
                catch (Exception ex) { MessageBox.Show("Export fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void ExportClipboard()
        {
            try { Bitmap bmp = RenderAll(); if (bmp != null) Clipboard.SetImage(bmp); }
            catch (Exception ex) { MessageBox.Show("Kopieren fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void ExportPackage()
        {
            if (!EndEdit(true)) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = "Woche_" + start.ToString("yyyy-MM-dd");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Package.FromWeek(store, start, weekLabel.Text).Save(d.FileName); }
                catch (Exception ex) { MessageBox.Show("Export fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void OpenPackageEditor(Package p)
        {
            if (!EndEdit(true)) return;
            using (PackageForm f = new PackageForm(store, equip, start, p, me.Has(Perm.ImportPackage), me.Has(Perm.PackageAdmin)))
            {
                f.ShowDialog(this);
                if (f.Applied) Rebuild(false);
            }
        }

        void ImportPackageFile()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg|Alle Dateien|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { OpenPackageEditor(Package.Load(d.FileName)); }
                catch (Exception ex) { MessageBox.Show("Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

#if TESTHOOKS
        // ---------- Testhilfen (--shot) ----------

        public Form TestUsers()
        {
            UserAdminForm u = new UserAdminForm(users, me);
            u.StartPosition = FormStartPosition.Manual; u.Location = new Point(0, 0); u.TopMost = true;
            u.Show();
            return u;
        }

        public Form TestForm(string which)
        {
            Package demo = null;
            if (which == "package2" || which == "campaign")   // Produkt 1357: Packen, Lauf (3x alle 3 Tage, abwechselnd), Splitten in Unterlaeufe
            {
                demo = new Package(); demo.Name = "Produkt Beispiel"; demo.ProductCode = "1357";
                Catalog cat = store.GetCatalog();
                foreach (string bn in new[] { "Packen", "Lauf", "Splitten" })
                {
                    Baustein b = cat.Baustein_(bn);
                    PackageStep s = new PackageStep(); s.Title = b.Name; s.Source = b.Name; s.Row = b.Row; s.Day = bn == "Packen" ? 1 : (bn == "Lauf" ? 2 : 3);
                    foreach (string sh in b.Positions) { EquipAssign a = new EquipAssign(); a.Short = sh; s.Equipment.Add(a); }
                    if (bn == "Splitten") { s.Kind = StepKind.SubRun; s.Count = 3; s.Interval = 3; s.StartNo = 30; s.SubNos = "4186/4187, 4188"; }
                    if (bn == "Lauf")
                    {
                        s.Kind = StepKind.Run; s.Count = 3; s.Interval = 3; s.StartNo = 30;
                        s.Equipment[0].Choices.Add(new EquipChoice("PT", "H1-T-0102")); s.Equipment[1].Choices.Add(new EquipChoice("Pumpe", "H1-P-0101-05"));
                        s.Equipment[2].Choices.Add(new EquipChoice("TFF", "H1-X-0101")); s.Equipment[2].Choices.Add(new EquipChoice("TFF", "H1-X-0102"));
                        s.Equipment[2].Alternate = true; s.Equipment[2].Fallback = "H1-X-0103";
                        s.Equipment[3].Choices.Add(new EquipChoice("MH", "H2-X-0999")); s.Equipment[3].Note = "neu validieren";   // nicht validiert -> orange
                    }
                    demo.Steps.Add(s);
                }
            }
            Form f;
            if (which == "structure") f = new StructureForm(store.GetRows(), true, true, true);
            else if (which == "catalog") { List<string> nm = new List<string>(); foreach (RowInfo r in store.GetRows()) nm.Add(r.Name); f = new CatalogForm(store.GetCatalog(), nm); }
            else if (which == "campaign") { CampaignForm cf = new CampaignForm(demo, store, start, "nur leere Blöcke füllen"); cf.Shift(1, 1); f = cf; }   // Lauf 13570030 (und Folgende) einen Tag spaeter
            else f = new PackageForm(store, equip, start, demo, true, true);
            Hide();                                   // nur fuer das Testbild: Hauptfenster nicht dahinter
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0); f.TopMost = true;
            f.Show();
            PackageForm pf = f as PackageForm;
            if (pf != null && which == "package2") pf.SelectStep(1);
            return f;
        }

        public void TestEdit(int col, int row)
        {
            foreach (CellData c in view.Cells) if (c.RowIndex == row && c.Col == col) { fold[rowInfo[row].Id] = false; ApplyFold(); view.Relayout(); BeginEdit(c); }
        }

        public void TestSearch(string q) { search.Text = q; }

        public void TestScroll(int y) { canvas.AutoScrollPosition = new Point(0, y); }

        // Messung: n Wochenwechsel inkl. Neuzeichnen (ms je Wechsel).
        public double Bench(int n)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) { Go(i % 2 == 0 ? 7 : -7); Refresh(); }
            return sw.ElapsedMilliseconds / (double)n;
        }

        // Produkt-Paket: Laufnummern XXYYZZZZ, Unterlaeufe, Kampagnenstart mit verschobenem Lauf, Datei-Roundtrip, fruehere Kampagne.
        void SelfTestCampaign(System.Text.StringBuilder sb)
        {
            Package c = new Package(); c.Name = "Produkt Test"; c.ProductCode = "1357";
            PackageStep pk = new PackageStep(); pk.Title = "Packen"; pk.Row = "Suite 2"; pk.Day = 1;
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Row = "Suite 2"; lf.Day = 2; lf.Count = 4; lf.Interval = 2; lf.StartNo = 30; lf.Kind = StepKind.Run;
            PackageStep sp = new PackageStep(); sp.Title = "Splitten"; sp.Row = "Mix / Split"; sp.Day = 3; sp.Count = 4; sp.Interval = 2; sp.StartNo = 30; sp.Kind = StepKind.SubRun; sp.SubNos = "4186/13574187, , 4190";
            c.Steps.Add(pk); c.Steps.Add(lf); c.Steps.Add(sp);
            bool okNo = lf.Compose(0, "1357") == "Lauf 13570030" && lf.Compose(1, "") == "Lauf 31" && pk.Compose(0, "1357") == "Packen";
            string sub0 = sp.Compose(0, "1357"), sub1 = sp.Compose(1, "1357");
            bool okSub = sub0 == "Splitten\nUnterlauf 13574186 (aus 13570030)\nUnterlauf 13574187 (aus 13570030)" && sub1 == "Splitten\nUnterlauf (aus 13570031)"
                && sp.Compose(2, "1357").EndsWith("Unterlauf 13574190 (aus 13570032)");
            c.Renumber(40);
            bool okRe = lf.StartNo == 40 && sp.StartNo == 40 && pk.StartNo == 1 && lf.Label("1357", 0) == "Lauf 13570040";
            sb.AppendLine("Produkt: Nummern=" + Ok(okNo) + " Unterlauf=" + Ok(okSub) + " Umnummern=" + Ok(okRe) + " [" + sub0.Replace("\n", "|") + "]");

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selftest_produkt.apkg");
            c.Save(path);
            Package q = Package.Load(path);
            bool okRt = q.ProductCode == "1357" && q.Steps.Count == 3 && q.Steps[1].Kind == StepKind.Run && q.Steps[2].Kind == StepKind.SubRun && q.Steps[2].SubNos == sp.SubNos;
            for (int s = 0; s < 3; s++) for (int r = 0; r < c.Steps[s].Count; r++) okRt &= q.Steps[s].Compose(r, q.ProductCode) == c.Steps[s].Compose(r, c.ProductCode);
            System.IO.File.WriteAllText(path, "<Paket name=\"Alt\" version=\"2\"><Schritt titel=\"Lauf\" block=\"Suite 2\" tag=\"1\" anzahl=\"2\" abstand=\"1\" startnr=\"1\"/></Paket>");
            Package old = Package.Load(path);
            bool okOld = old.ProductCode == "" && old.Steps[0].Kind == StepKind.Normal && old.Steps[0].Compose(1, old.ProductCode) == "Lauf 2";
            sb.AppendLine("Produkt-Datei: Roundtrip=" + Ok(okRt) + " altesPaket=" + Ok(okOld));

            // Kampagnenstart-Dialog (ohne Anzeige): Zeile 1 (Lauf 13570040) +1 Tag, Folgende wandern mit, Zeile 0 nicht
            DateTime s2 = start.AddDays(35);
            bool okDlg;
            using (CampaignForm cf = new CampaignForm(q, store, s2, "test"))
            {
                cf.Shift(1, 1);
                List<RunDate> sd = cf.Schedule;
                okDlg = sd[0].Day == 1 && sd[1].Day == 3 && sd[2].Day == 4 && sd[sd.Count - 1].Day == 10 && cf.Start == s2;
            }
            // Datums-Ueberschreibung: nur der letzte Lauf (13570043) einen Tag spaeter
            List<RunDate> sched = q.Schedule();
            RunDate last = sched.Find(delegate(RunDate d) { return d.Step == 1 && d.Run == 3; });
            last.Day += 1;
            string msg = q.Apply(store, s2, ApplyMode.FillEmpty, sched);
            int suite2 = rowInfo.Find(delegate(RowInfo r) { return r.Name == "Suite 2"; }).Id, mix = rowInfo.Find(delegate(RowInfo r) { return r.Name == "Mix / Split"; }).Id;
            Entry e8 = store.Get(s2.AddDays(7), suite2), e9 = store.Get(s2.AddDays(8), suite2), m3 = store.Get(s2.AddDays(2), mix);
            bool okOver = (e8 == null || e8.Content.IndexOf("13570043") < 0) && e9 != null && e9.Content == "Lauf 13570043" && m3 != null && m3.Content.StartsWith("Splitten\nUnterlauf 13574186");
            int no, iv; DateTime lastDay;
            bool found = Package.LastRun(store, "1357", out no, out lastDay, out iv);
            bool okLast = found && no == 43 && lastDay == s2.AddDays(8) && iv == 2;
            sb.AppendLine("Kampagne: Dialog/Mitverschieben=" + Ok(okDlg) + " Überschreibung=" + Ok(okOver) + " früheKampagne=" + Ok(okLast) + " (letzter " + no + ", Abstand " + iv + ") " + msg);
        }

        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }

        // Selbsttest (Admin): Zuklappen, Bearbeiten mit Format, LKZ, Struktur, Export/Import, Paket-Schritte.
        public string SelfTest()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int n = rowInfo.Count;
            string coll = ""; for (int r = 0; r < n; r++) coll += (view.Collapsed[r] ? "z" : "o");
            sb.AppendLine("Zeilen=" + n + " Standard offen(o)/zu(z): " + coll + " | " + rowInfo[4].Name + " vor " + rowInfo[5].Name);

            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 0 && x.Col == 4) c = x;
            int rowId = rowInfo[0].Id; DateTime day = c.Day;
            BeginEdit(c);
            edContent.Text = "Neuer Text LKZ ABC"; edContent.Select(11, 3);
            edContent.SelectionColor = Color.Red; edContent.SelectionFont = MakeFont("Arial", 14f, FontStyle.Bold | FontStyle.Italic);
            edPpl[0].Text = "TEST, ABCD";
            c.Lkz = true;
            bool ok = EndEdit(true);
            Entry e = store.Get(day, rowId);
            sb.AppendLine("Save=" + ok + " [" + e.Content + "] Spans=" + e.SpanKey() + " F=[" + e.People[0] + "] Lkz=" + e.Lkz + " Ver=" + e.Version);

            // LKZ-Haken ohne Bearbeitungsmodus
            Rebuild(false);
            CellData c2 = view.Cells[4]; ToggleLkz(c2);
            sb.AppendLine("Lkz nach Klick: " + store.Get(day, rowId).Lkz);

            // Struktur: Zeile umbenennen, neue Zeile, Reihenfolge
            List<RowInfo> rows = store.GetRows();
            rows[0].Name = "Suite 1A"; rows[0].MinLines = 5;
            RowInfo nr = new RowInfo(); nr.Name = "Neu"; rows.Insert(1, nr);
            store.SaveStructure(rows);
            Rebuild(false);
            sb.AppendLine("Struktur: " + rowInfo[0].Name + "/" + rowInfo[0].MinLines + ", " + rowInfo[1].Name + " (Id " + rowInfo[1].Id + "), Zeilen=" + rowInfo.Count);

            // Paket: Woche exportieren, Schritte anwenden
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selftest.apkg");
            Package p = Package.FromWeek(store, start, "Test");
            PackageStep st = new PackageStep(); st.Title = "Lauf"; st.Day = 2; st.Row = "Neu"; st.Count = 3; st.Interval = 1; st.Note = "nur wenn frei";
            EquipAssign tff = new EquipAssign(); tff.Short = "TFF"; tff.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); tff.Choices.Add(new EquipChoice("TFF", "H1-X-0102")); tff.Alternate = true; tff.Fallback = "H1-X-0103";
            EquipAssign pt = new EquipAssign(); pt.Short = "PT"; pt.Choices.Add(new EquipChoice("PT", "H1-T-0102")); pt.Note = "Charge 7 mit einer sehr langen Notiz, die umbrechen muss und eingerückt weiterlaufen soll";
            EquipAssign sl = new EquipAssign(); sl.Short = "Schlauch"; sl.Note = "S-12 (grün)";
            st.Equipment.Add(pt); st.Equipment.Add(tff); st.Equipment.Add(sl);
            p.Steps.Add(st);
            // Austausch-Gruppe: Freezetank 1 und 2 wechseln sich ab (Positions-Kurzname folgt dem gewaehlten Geraet)
            PackageStep fz = new PackageStep(); fz.Title = "Freeze"; fz.Day = 6; fz.Row = "Neu"; fz.Count = 3;
            EquipAssign fa = new EquipAssign(); fa.Short = "FZ1"; fa.Alternate = true;
            fa.Choices.Add(new EquipChoice("FZ1", "H2-T-0251")); fa.Choices.Add(new EquipChoice("FZ2", "H2-T-0252"));
            fz.Equipment.Add(fa); p.Steps.Add(fz);
            sb.AppendLine("Austausch FZ1: " + store.GetCatalog().Interchangeable("FZ1").Count + " Geräte, Positionen=" + store.GetCatalog().Positions.Count);
            sb.AppendLine("Platzhalter TFF: " + store.GetCatalog().Position("TFF").Placeholder() + " | Pumpe: " + store.GetCatalog().Position("Pumpe").Placeholder());
            p.Save(path);
            Package q = Package.Load(path);
            PackageBlock fr = q.Blocks.Find(delegate(PackageBlock b) { return b.Row == "Suite 1A" && b.Day == 5; });
            sb.AppendLine("Paket: Blöcke=" + q.Blocks.Count + " Schritte=" + q.Steps.Count + " Format erhalten=" + (fr == null ? -1 : fr.Data.Spans.Count));
            sb.AppendLine("Apply: " + q.Apply(store, start.AddDays(7), ApplyMode.FillEmpty, null));
            for (int k = 0; k < 3; k++)
            {
                Entry ne = store.Get(start.AddDays(8 + k), rowInfo[1].Id);
                sb.AppendLine("Lauf-Tag " + (k + 1) + ": [" + (ne == null ? "-" : ne.Content.Replace("\n", "|")) + "]");
            }
            SelfTestCampaign(sb);
            Rebuild(true);
            sb.AppendLine("Find 'TEST': " + store.Find("TEST").Count);
            byte[] blob = Protected.Protect("Geheim äöü", "pw1");
            bool wrong = false, tampered = false;
            try { Protected.Unprotect(blob, "pw2"); } catch (System.Security.Cryptography.CryptographicException) { wrong = true; }
            blob[40] ^= 1;
            try { Protected.Unprotect(blob, "pw1"); } catch (System.Security.Cryptography.CryptographicException) { tampered = true; }
            blob[40] ^= 1;
            sb.AppendLine("Protected: ok=" + (Protected.Unprotect(blob, "pw1") == "Geheim äöü") + " falschesPw=" + wrong + " manipuliert=" + tampered);
            return sb.ToString();
        }
#endif

        // Notizblock mit Stift, per GDI+ gezeichnet (keine Bilddatei, keine Sonderzeichen-Schrift noetig).
        static Bitmap MakePencil()
        {
            Bitmap b = new Bitmap(18, 18);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillRectangle(Brushes.White, 1, 3, 11, 13);
                g.DrawRectangle(Pens.DimGray, 1, 3, 11, 13);
                g.FillRectangle(Brushes.SteelBlue, 1, 1, 11, 3);
                for (int i = 0; i < 3; i++) g.DrawLine(Pens.Gray, 3, 7 + i * 3, 10, 7 + i * 3);
                g.FillPolygon(Brushes.Orange, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
                g.DrawPolygon(Pens.Sienna, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
            }
            return b;
        }
    }
}
