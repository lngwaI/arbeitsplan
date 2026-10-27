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

    partial class MainForm : Form
    {
        const int WM_SETREDRAW = 0xB;
        static readonly int[] Swatches = { 0x000000, 0xC00000, 0x0050C8, 0x008000, 0xE06000, 0x7030A0, 0x808080 };
        static readonly string[] FontNames = { "Standardschrift", "Arial", "Tahoma", "Verdana", "Times New Roman", "Courier New" };
        static readonly string[] Sizes = { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24" };
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

        readonly IDataStore store;
        readonly ILockService locks;
        UserDb users;
        readonly FileStore fstore;   // null = Testdaten im Speicher
        readonly AppConfig cfg;
        readonly IEquipmentStatusProvider equip = new MockEquipmentProvider();
        readonly User me;
        readonly List<RowInfo> rowInfo = new List<RowInfo>();
        readonly Dictionary<int, bool> fold = new Dictionary<int, bool>();   // nur lokal in dieser Instanz, wird nicht gespeichert/synchronisiert
        readonly PlanView view;
        readonly Label weekLabel = new Label(), statusLeft = new Label(), statusRight = new Label(), searchLabel = new Label(), hitsLabel = new Label();
        readonly TextBox search = new TextBox();
        readonly Panel topBar = new Panel(), dateBar = new Panel(), bar = new Panel(), fmtBar = new Panel(), statusBar = new Panel();
        readonly QuietPanel canvas = new QuietPanel();
        readonly DayLabel[] dateLabels = new DayLabel[7];
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Bitmap pencilIcon = Ui.MakePencil();
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
        bool loadingEditor, inResize, syncingFormat, systemPc;
        bool extracting;                 // ExtractSpans laeuft: Auswahl-Handler gesperrt
        bool editChanged, fmtChanged;    // billiger "geaendert?"-Stand des Editors (fuer Icon-Timer und Esc)
        int viewGen;                     // FileStore.Generation beim letzten Rebuild
        int hitPos = -1;

        // cfg == null: Testdaten im Speicher (nur Test-Build); sonst verschluesselte Datendatei im Datenordner (FileStore)
        public MainForm(string login, bool failSaves, AppConfig cfg, FileStore fs)
        {
            this.cfg = cfg;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = Theme.Current.UiFont();    // je Modell Segoe UI / Tahoma (Rueckfall: Systemschrift)
            BackColor = Theme.Current.Window;
            plainFont = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            KeyPreview = true;

            start = MondayOf(DateTime.Today);
            if (cfg == null)
            {
                MemoryStore ms = MemoryStore.Sample(start);
                ms.FailSaves = failSaves; systemPc = failSaves;
                store = ms; users = UserDb.Sample(); locks = new MemoryLockService();
#if TESTHOOKS
                locks.Acquire(start.AddDays(1), 1, "Paula Werk");   // Mock: Dienstag, Zeile 1 ist "in Bearbeitung"
#endif
            }
            else
            {
                fstore = fs ?? new FileStore(cfg.DataFolder, cfg.ReadOnly, cfg.CachePath, cfg.CacheDays);
                store = fstore; users = fstore.Users; locks = new FileLockService(cfg.DataFolder);
                systemPc = fstore.ReadOnly;
            }
            me = ResolveUser(login);

            view = new PlanView(Font);
            view.Equip = equip;
            view.Colors = StatusColors.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "statuscolors.ini"));
            view.MyKuerzel = me.Kuerzel;
            view.Pencil = pencilIcon;
            view.Rows = rowInfo;
            view.CellActivated = BeginEdit;
            view.MarkClicked = ClickMark;
            view.VariantClicked = ChooseVariant;
            view.RoundClicked = ToggleRound;
            view.RoundToggled = ToggleRoundFold;
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
            Controls.Add(statusBar);   // zuletzt = ganz unten

            ResizeEnd += delegate { RefitIfScreenChanged(); };
            EventHandler displayChanged = delegate { RefitIfScreenChanged(); };   // statisches Ereignis: beim Schliessen abmelden
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayChanged;
            FormClosed += delegate { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayChanged; };
            KeyDown += OnKey;
            FormClosing += OnClosing;
            InitData();   // Verlauf-Menue, Sicherung beim Beenden (MainForm.Data.cs)
            StartPolling();   // Datendatei regelmaessig auf Aenderungen pruefen (MainForm.Storage.cs)
            Theme.Current.Style(this);
            Rebuild(true);
#if TESTHOOKS
            if (TestWeeks == 2) SetWeeks(2);
#endif
        }

        // ---------- Aufbau der Leisten ----------

        MenuStrip BuildMenu()
        {
            MenuStrip ms = new MenuStrip();
            bool exp = me.Has(Perm.Export), pkg = me.Has(Perm.ImportPackage) || exp;
            bool structure = me.Has(Perm.EditNames) || me.Has(Perm.EditHeight) || me.Has(Perm.SortRows);
            bool usr = me.Has(Perm.UserCreate) || me.Has(Perm.UserEdit) || me.Has(Perm.UserDelete);

            ToolStripMenuItem file = new ToolStripMenuItem("Woche");
            file.DropDownItems.Add("Meine Schichten…", null, delegate { ShowPersons(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Als Bild speichern (PNG)…", null, delegate { ExportImage(false); }).Enabled = exp;
            file.DropDownItems.Add("Als PDF speichern…", null, delegate { ExportImage(true); }).Enabled = exp;
            file.DropDownItems.Add("In Zwischenablage kopieren", null, delegate { ExportClipboard(); }).Enabled = exp;
            file.DropDownItems.Add("Als Paket speichern (.apkg)…", null, delegate { ExportPackage(); }).Enabled = exp;

            ToolStripMenuItem pk = new ToolStripMenuItem("Pakete");
            pk.DropDownItems.Add("Kampagne starten…", null, delegate { StartCampaignFromFile(); }).Enabled = me.Has(Perm.ImportPackage);
            pk.DropDownItems.Add(new ToolStripSeparator());
            pk.DropDownItems.Add("Neues Paket anlegen…", null, delegate { OpenPackageEditor(null); });
            pk.DropDownItems.Add("Paket öffnen und bearbeiten…", null, delegate { ImportPackageFile(); });
            pk.Enabled = pkg;

            ToolStripMenuItem set = new ToolStripMenuItem("Einstellungen");
            ToolStripItem st = set.DropDownItems.Add("Blockstruktur…", null, delegate { EditStructure(); }); st.Enabled = structure;
            ToolStripItem us = set.DropDownItems.Add("Nutzerverwaltung…", null, delegate { RefreshUsers(); using (UserAdminForm f = new UserAdminForm(users, me)) { f.ShowDialog(this); SaveUsers(f.Changes); } }); us.Enabled = usr;
            ToolStripItem cf = set.DropDownItems.Add("Equipment && Bausteine…", null, delegate { EditCatalog(); }); cf.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem mf = set.DropDownItems.Add("Equipment-Stammdaten…", null, delegate { EditMaster(); }); mf.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem du = set.DropDownItems.Add("Pflichtpunkte && Feiertage…", null, delegate { EditDuties(); }); du.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem pf = set.DropDownItems.Add("Produkte && Equipment…", null, delegate { EditProducts(); }); pf.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem bk = set.DropDownItems.Add("Sicherung…", null, delegate { OpenBackup(); }); bk.Enabled = me.Has(Perm.PackageAdmin);
            set.Visible = structure || usr || me.Has(Perm.PackageAdmin);

            ToolStripMenuItem help = new ToolStripMenuItem("Hilfe");
            help.DropDownItems.Add("Verknüpfung prüfen / anlegen…", null, delegate { Shortcut.Check(this); });
            help.DropDownItems.Add("Info…", null, delegate { ShowInfo(); });

            ms.Items.AddRange(new ToolStripItem[] { file, pk, set, help });
#if TESTHOOKS
            ToolStripMenuItem av = new ToolStripMenuItem("Ansicht (Test)");
            ToolStripMenuItem w2 = new ToolStripMenuItem("2 Wochen"); w2.CheckOnClick = true; w2.Checked = TestWeeks == 2;
            w2.CheckedChanged += delegate { SetWeeks(w2.Checked ? 2 : 1); };
            av.DropDownItems.Add(w2);
            ms.Items.Add(av);
#endif
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
            bar.Controls.Add(nav);
            bar.Resize += delegate { CenterBar(); };
            Shown += delegate { CenterBar(); };

            // Statusleiste: links Nutzer · Rolle · Kuerzel, rechts Datenstand (Status in MainForm.Storage.cs)
            statusBar.Dock = DockStyle.Bottom; statusBar.Height = Font.Height + 8;
            statusBar.Paint += delegate(object s, PaintEventArgs e) { using (Pen p = new Pen(Theme.Current.ButtonBorder)) e.Graphics.DrawLine(p, 0, 0, statusBar.Width, 0); };
            foreach (Label l in new[] { statusLeft, statusRight })
            {
                l.AutoSize = true; l.Padding = new Padding(8, 3, 8, 0); l.Dock = l == statusLeft ? DockStyle.Left : DockStyle.Right;
                statusBar.Controls.Add(l);
            }
            statusLeft.Text = me.Display + "  ·  " + users.RoleLabel(me) + (me.Kuerzel.Length > 0 ? "  ·  " + me.Kuerzel : "");
            ToolTip roleTip = new ToolTip(); roleTip.AutoPopDelay = 20000; roleTip.SetToolTip(statusLeft, users.RoleTooltip(me));
            Status(fstore != null ? "Aktualisiert " + DateTime.Now.ToString("HH:mm") : null, false);
        }

        void AddNav(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.AutoSize = true; b.MinimumSize = new Size(86, Theme.Current.ButtonH + 4); b.Margin = new Padding(3, 0, 3, 0);
            b.Click += click;
            Theme.Current.StyleButton(b);
            nav.Controls.Add(b);
        }

        void CenterBar() { nav.Left = Math.Max(10, (bar.ClientSize.Width - nav.Width) / 2); }

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
            if (pollNow != null) pollNow();   // Aenderungen anderer PCs gleich holen (im Hintergrund, haengt nie)
        }

        // Laedt die Woche neu und zeichnet nur neu (keine Steuerelemente werden erzeugt -> schnell).
        // resetFold: beim Wechsel der Woche gelten wieder die Standardwerte (leere Zeilen zugeklappt, belegte offen).
        void Rebuild(bool resetFold)
        {
            weekLabel.Text = PlanView.WeekTitle(start);
            for (int c = 0; c < 7; c++)
            {
                DateTime d = start.AddDays(c);
                dateLabels[c].Text = d.ToString("ddd, dd.MM.yyyy", Ui.De);
                dateLabels[c].Today = d == DateTime.Today; dateLabels[c].Invalidate();
            }
            int selRow = -1; DateTime selDay = DateTime.MinValue;
            if (view.Selected != null && view.Selected.RowIndex < rowInfo.Count) { selRow = rowInfo[view.Selected.RowIndex].Id; selDay = view.Selected.Day; }
            if (resetFold) { fold.Clear(); roundFold.Clear(); }

            if (fstore != null) viewGen = fstore.Generation;
            rowInfo.Clear(); rowInfo.AddRange(store.GetRows());
            duties = store.GetDuties();
            view.LoadCells(store, start);
            foreach (CellData d in view.Cells) if (rowInfo[d.RowIndex].Id == selRow && d.Day == selDay) view.Selected = d;
            ComputeChecks();
            view.MarkAllDirty();   // Equipment-Status neu abfragen
            ApplyFold();
#if TESTHOOKS
            if (week2 != null) week2.Load(store, rowInfo, start.AddDays(7));
#endif
            ResizeView();
        }

        // ---------- Zeilen ein-/ausklappen (nur lokal) ----------

        void ApplyFold()
        {
            bool[] d = view.DefaultFold();   // nichts drin im Zeitraum -> zugeklappt
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
    }
}
