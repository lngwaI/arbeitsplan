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
        public QuietPanel() { DoubleBuffered = true; }   // Wochenansicht: Hintergrund und Bildlauf ohne Flackern beim Vergroessern (UX-5e)

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
        readonly PlanSnapshot shownPlan;   // != null: Fenster zeigt eine Plan-Datei (nur ansehen)
        readonly ManualStatusProvider equip;      // manuell gesetzter Status (Store) vor der Mock-/spaeteren MES-Quelle
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
        public MainForm(string login, bool failSaves, AppConfig cfg, FileStore fs) : this(login, failSaves, cfg, fs, null) { }

        // plan != null: Plan-Datei (.applan) nur ansehen, ohne Datenordner (MainForm.PlanFile.cs)
        MainForm(string login, bool failSaves, AppConfig cfg, FileStore fs, PlanSnapshot plan)
        {
            this.cfg = cfg; shownPlan = plan;
            LoadLook();   // gespeicherter Look vor dem Aufbau der Ansicht (MainForm.Look.cs)
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = Theme.Current.UiFont();    // je Modell Segoe UI / Tahoma (Rueckfall: Systemschrift)
            BackColor = Theme.Current.Window;
            plainFont = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            KeyPreview = true;

            start = MondayOf(DateTime.Today);
            if (plan != null)
            {
                start = MondayOf(plan.From);
                store = ViewStore(plan); systemPc = true;
                users = UserDb.Default(); locks = new MemoryLockService();
            }
            else if (cfg == null)
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
            // Basisquelle: Statusdatei im Datenordner (FileStore); Testdaten/Plan-Datei: Mock. Manuell gilt davor (ManuellTage).
            IEquipmentStatusProvider baseStatus = plan == null && cfg != null ? (IEquipmentStatusProvider)new FileStatusProvider(cfg.StatusPath, cfg.StatusMaxHours) : new MockEquipmentProvider();
            equip = new ManualStatusProvider(store, baseStatus);
            if (cfg != null) equip.ManualDays = cfg.ManualDays;
            ManualStatusProvider.CheckTransitions = cfg != null && cfg.CheckTransitions;

            view = new PlanView(Font);
            view.Equip = equip;
            view.Colors = StatusColors.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "statuscolors.ini"));
            view.MyKuerzel = me.Kuerzel;
            view.Pencil = pencilIcon;
            view.Rows = rowInfo;
            view.CellActivated = BeginEdit;
            view.MarkClicked = ClickMark;
            view.BufferClicked = EditBuffer;
            view.FreezeClicked = EditFreeze;
            view.MadeClicked = ToggleMade;
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
#if DEMO
            Controls.Add(BuildDemoBanner());   // Vorfuehr-Version: Banner unter dem Menue (MainForm.Demo.cs)
#endif
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
            InitNotices();    // Benachrichtigungen, Uebernahme der Bearbeitung (MainForm.TakeOver.cs)
            Theme.Current.Style(this);
#if DEMO
            StyleDemoBanner();
#endif
            Rebuild(true);
#if TESTHOOKS
            if (TestWeeks == 2) SetWeeks(2);
#endif
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
            duties = store.GetDuties(); view.BufMade = duties.BufMade;
            view.LoadCells(store, start);
            MarkChanged();   // Hervorhebung von Aenderungen anderer PCs bleibt ueber Neuaufbau erhalten (MainForm.Changes.cs)
            foreach (CellData d in view.Cells) if (rowInfo[d.RowIndex].Id == selRow && d.Day == selDay) view.Selected = d;
            ComputeChecks();
            equip.Reload();        // manuell gesetzte Status (auch von anderen PCs)
            view.MarkAllDirty();   // Equipment-Status neu abfragen
            ApplyFold();
#if TESTHOOKS
            if (week2 != null) { week2.Load(store, rowInfo, start.AddDays(7)); ComputeChecks(week2.View, week2.View.Start); }
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
