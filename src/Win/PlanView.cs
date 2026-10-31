using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Ein Block (Tag x Zeile) inkl. Layout-Ergebnis.
    class CellData
    {
        public DateTime Day;
        public int Col, RowIndex;
        public Entry Cur;                                   // zuletzt gespeicherter Stand
        public string Content;                              // angezeigter/bearbeiteter Text
        public string[] People = { "", "", "" };
        public bool Lkz;                                    // angezeigter/bearbeiteter LKZ-Haken
        public List<BufferItem> Buffers = new List<BufferItem>();   // angezeigte/bearbeitete Puffer
        public string Variant { get { return BufferSet.Types(Buffers); } }   // Typen als VariantSet
        public List<string> Marks = new List<string>();     // angezeigte/bearbeitete Pflichtpunkte
        public bool Editing, Dirty = true;
        public bool Changed;                                // von anderem PC geaendert, kurz hervorgehoben (nur Ansicht)

        // Angezeigten Stand aus e uebernehmen (ohne Cur zu aendern)
        public void Show(Entry e)
        {
            Content = e.Content; People = (string[])e.People.Clone(); Lkz = e.Lkz; Buffers = BufferSet.CloneAll(e.Buffers); Marks = new List<string>(e.Marks); Dirty = true;
        }
        public TextBlock CB;
        public TextBlock[] PB = new TextBlock[3];
        public int Hits;
        public List<RunRef> Runs = new List<RunRef>();      // Laufnummern im Inhalt (Kennzeichnung je Produkt)
        public string RunsFor;
        public DayState[] States;                           // je Pflichtpunkt der Zeile (PlanView.Defs), von MainForm berechnet
        public string Note = "";                            // Kopf-Hinweis, z. B. "LKZ KW44 offen"
        public bool Urgent;                                 // Wochenpflicht (LKZ) nach Sonntag nicht erfuellt: Kopf rot, Hinweis fett
        public Rectangle R, RHead, RChecks, RBuf, RContent;
        public List<BufPart> BufParts = new List<BufPart>(); // Puffer-Zeilen (PlanView.Buffers.cs)
        public int TitleH; public bool TitleMore;           // Titel = fuehrende Zeilen mit groesserer Schrift (Hoehe, 0 = keiner); TitleMore: danach folgt Text
        public int BufH;                                    // Hoehe des Puffer-Bereichs (0 = keine Puffer)
        public List<FreezeCycle> Conts = new List<FreezeCycle>();   // Fortsetzungen von Freeze-Vorgaengen frueherer Tage (PlanView.Freeze.cs, nur Freeze-Zeile)
        public List<FrzPart> FrzParts = new List<FrzPart>(); // Freeze-Vorgaenge dieses Tages
        public int FrzH, SplitH;                            // Hoehe des Freeze-Bereichs (ueber dem Inhalt) und der abgeleiteten "gesplittet in"-Zeile (unter dem Inhalt)
        public TextBlock SplitTB;
        public Rectangle RFrz, RSplit;
        public Rectangle[] RLbl = new Rectangle[3], RPpl = new Rectangle[3];
    }

    // Wochenansicht, selbst gezeichnet: schneller Tag-/Wochenwechsel (keine Hunderte Steuerelemente).
    // Nur beim Bearbeiten legt MainForm Eingabefelder ueber Inhalt und Kuerzel des aktiven Blocks.
    partial class PlanView : Control
    {
        public const int TP = 3, TV = 3, TaskLines = 1;
        public static int HeadH { get { return Theme.Current.HeadH; } }
        public static int LkzH { get { return Theme.Current.LkzH; } }
        public static int LW { get { return Theme.Current.LW; } }
        public static int Pad { get { return Theme.Current.Pad; } }
        public static int Gap { get { return Theme.Current.Gap; } }
        static readonly Color Yellow = Color.FromArgb(255, 235, 59), Green = Color.FromArgb(170, 235, 170);

        public List<RowInfo> Rows = new List<RowInfo>();
        public List<CellData> Cells = new List<CellData>();
        public DateTime Start;
        public IEquipmentStatusProvider Equip;
        public StatusColors Colors;
        public Image Pencil;                                // Symbol im Bearbeitungsmodus
        public string MyKuerzel = "", Query = "";
        public CellData Selected;
        public int HitCount, ColW;
        public Action<CellData> CellActivated;
        public Action<int> RowToggled;                      // Klick auf den Kopf einer Zeile: ein-/ausklappen (nur lokal)
        public bool[] Collapsed = new bool[0];              // je Zeile: zugeklappt (nur Kopfleiste sichtbar)
        public event EventHandler LayoutDone;

        readonly Font bold;
        int lastW = -1;
        int[] lblW;                                       // Breite der Schichtbezeichnung je Zeile (LabelW)
        readonly Dictionary<string, EquipmentInfo> equipCache = new Dictionary<string, EquipmentInfo>();   // je Rebuild (MarkAllDirty)

        public PlanView(Font f)
        {
            Font = f;
            bold = new Font(f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Current.Window;
        }

        public int ColLeft(int col) { return Pad + col * (ColW + Gap); }

        // Kopf "KWxx (TT.MM.JJJJ - TT.MM.JJJJ)" der Woche ab start.
        public static string WeekTitle(DateTime start)
        {
            return "KW" + Ui.De.Calendar.GetWeekOfYear(start, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + " (" + start.ToString("dd.MM.yyyy", Ui.De) + " - " + start.AddDays(6).ToString("dd.MM.yyyy", Ui.De) + ")";
        }

        // Woche ab start aus dem Bestand laden (Rows muss gesetzt sein); die Auswahl wird aufgehoben.
        public void LoadCells(IDataStore store, DateTime start)
        {
            Start = start; Cells.Clear(); Selected = null;
            for (int r = 0; r < Rows.Count; r++)
                for (int c = 0; c < 7; c++)
                {
                    CellData d = new CellData();
                    d.Day = start.AddDays(c); d.Col = c; d.RowIndex = r;
                    d.Cur = store.Get(d.Day, Rows[r].Id) ?? new Entry();
                    d.Show(d.Cur);
                    Cells.Add(d);
                }
            LoadFreeze(store, start);   // Fortsetzungen und Split-Index (PlanView.Freeze.cs)
        }

        // Standard-Zuklappen: Zeile ohne Inhalt und ohne Haken/Variante im angezeigten Zeitraum ist zugeklappt.
        public bool[] DefaultFold()
        {
            bool[] d = new bool[Rows.Count];
            if (Theme.Current.Dense) return d;   // Plan-Stil: alle Zeilen offen
            for (int r = 0; r < d.Length; r++) d[r] = true;
            foreach (CellData c in Cells) if (c.RowIndex < d.Length && (!c.Cur.IsBlank || c.Conts.Count > 0)) d[c.RowIndex] = false;
            return d;
        }
        // Alle Bloecke neu umbrechen; Equipment-Status wird dabei neu abgefragt.
        public void MarkAllDirty() { equipCache.Clear(); foreach (CellData c in Cells) c.Dirty = true; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { bold.Dispose(); tip.Dispose(); if (headFont != null) headFont.Dispose(); if (roundFont != null) roundFont.Dispose(); }
            base.Dispose(disposing);
        }

        // ---------- Layout ----------
        // Je Zeile sind Inhaltsfeld und jedes Schichtfeld (F/S/N) in allen 7 Spalten gleich hoch. Schichtfeld: fest mind. 3 Textzeilen
        // (3 Reihen 4-stelliger Kuerzel), waechst nur, wenn mehr noetig ist (kein Abschneiden).
        public void Relayout()
        {
            int w = Width, n = Rows.Count;
            if (w < 100 || n == 0) return;
            if (w != lastW) { MarkAllDirty(); lastW = w; }
            ColW = Math.Max(60, (w - 2 * Pad - 6 * Gap) / 7);
            int iw = ColW - 2, tw = iw - 2 * TP, lh = Font.Height;
            lblW = new int[n];
            for (int r = 0; r < n; r++) lblW[r] = LabelW(Rows[r]);

            if (Collapsed.Length != n) Collapsed = new bool[n];
            HitCount = 0;
            foreach (CellData c in Cells) { if (Collapsed[c.RowIndex]) continue; if (c.Dirty) Build(c, tw, iw - lblW[c.RowIndex] - 2 * TP); HitCount += c.Hits; }

            int[] ch = new int[n], lk = new int[n];
            int[][] sh = new int[n][];
            slots = new CheckSlot[n][];
            for (int r = 0; r < n; r++)
            {
                ch[r] = RowNeeds(r, lh, out sh[r]);
                lk[r] = LayoutChecks(r, iw);
            }

            int[] rowY = new int[n], rowH = new int[n];
            int y = Pad;
            y += LayoutRounds(y);
            for (int r = 0; r < n; r++)
            {
                rowH[r] = Collapsed[r] ? 1 + HeadH + 1 : 1 + HeadH + lk[r] + ch[r] + 1 + sh[r][0] + sh[r][1] + sh[r][2] + 1;
                rowY[r] = y; y += rowH[r] + Gap;
            }
            foreach (CellData c in Cells)
            {
                int r = c.RowIndex, x = ColLeft(c.Col);
                c.R = new Rectangle(x, rowY[r], ColW, rowH[r]);
                c.RHead = new Rectangle(x + 1, rowY[r] + 1, iw, HeadH - 1);
                if (Collapsed[r])
                {
                    c.RChecks = c.RBuf = c.RContent = c.RFrz = c.RSplit = Rectangle.Empty;
                    for (int s = 0; s < 3; s++) c.RLbl[s] = c.RPpl[s] = Rectangle.Empty;
                    continue;
                }
                int cy = rowY[r] + 1 + HeadH;
                c.RChecks = new Rectangle(x + 1, cy, iw, lk[r]);
                cy += lk[r];
                c.RBuf = new Rectangle(x + 1, cy, iw, c.BufH);                       // Puffer ueber dem freien Inhalt; ch[r] umfasst beides
                c.RFrz = new Rectangle(x + 1, cy + c.BufH, iw, c.FrzH);               // Freeze-Vorgaenge darunter
                c.RContent = new Rectangle(x + 1, cy + c.BufH + c.FrzH, iw, ch[r] - c.BufH - c.FrzH - c.SplitH);
                c.RSplit = new Rectangle(x + 1, c.RContent.Bottom, iw, c.SplitH);      // abgeleitete Zeile "gesplittet in ..." unter dem Inhalt
                int sy = cy + ch[r] + 1, lw = lblW[r];
                for (int s = 0; s < 3; s++)   // ausgeblendete Schicht: Hoehe 0 (nicht gezeichnet, nicht anklickbar, kein Editor)
                {
                    c.RLbl[s] = new Rectangle(x + 1, sy, lw, sh[r][s]);
                    c.RPpl[s] = new Rectangle(x + 1 + lw, sy, iw - lw, sh[r][s]);
                    sy += sh[r][s];
                }
            }
            if (Height != y + Pad) Height = y + Pad;
            Invalidate();
            if (LayoutDone != null) LayoutDone(this, EventArgs.Empty);
        }

        // Benoetigte Hoehe von Inhaltsfeld (Rueckgabe) und Schichtfeldern einer aufgeklappten Zeile (ueber alle 7 Spalten).
        int RowNeeds(int r, int lh, out int[] sh)
        {
            RowInfo ri = Rows[r];
            int ml = ri.MinLines;
            if (Theme.Current.Dense && !Collapsed[r])   // Plan-Stil: Zeile ohne jeden Inhalt in der Woche = 1 Textzeile (waechst beim Tippen)
            {
                bool any = false;
                foreach (CellData c in Cells) if (c.RowIndex == r && (c.Content.Trim().Length > 0 || c.BufH + c.FrzH + c.SplitH > 0 || c.Conts.Count > 0 || c.Editing)) { any = true; break; }
                if (!any) ml = Math.Min(ml, 1);
            }
            int minC = ml * lh + 2 * TV, ch = minC, min = (ri.Mode == ShiftMode.Task ? TaskLines : Theme.Current.ShiftLines) * lh + 2 * TV;
            sh = new int[3];
            for (int s = 0; s < 3; s++) sh[s] = ri.HasShift(s) ? min : 0;
            if (Collapsed[r]) return ch;
            foreach (CellData c in Cells)
            {
                if (c.RowIndex != r) continue;
                int extra = 0;      // Editor: gleiche Zeilenhoehe/Breite wie die Ansicht (ExactLineSpacing), keine Reserve mehr noetig
                ch = Math.Max(ch, c.BufH + c.FrzH + Math.Max(minC, c.CB.Height + 2 * TV + extra) + c.SplitH);
                for (int s = 0; s < 3; s++) if (sh[s] > 0) sh[s] = Math.Max(sh[s], c.PB[s].Height + 2 * TV + extra);
            }
            return ch;
        }

        // Breite der Schichtbezeichnung: Standard (ein Buchstabe) = schmaler Farbstreifen, laengere Namen so breit wie noetig
        // (hoechstens 1/3 der Spalte), Tagesaufgabe ohne Bezeichnung.
        int LabelW(RowInfo ri)
        {
            if (ri.Mode == ShiftMode.Task) return 0;
            int w = LW;
            for (int s = 0; s < 3; s++)
                if (ri.HasShift(s) && ri.Labels[s].Length > 1) w = Math.Max(w, TextW(ri.Labels[s], bold) + 8);
            return Math.Min(w, ColW / 3);
        }

        readonly Dictionary<string, int> textW = new Dictionary<string, int>();
        int TextW(string s, Font f)
        {
            string k = f.SizeInPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) + (int)f.Style + "|" + s;
            int w;
            if (!textW.TryGetValue(k, out w)) { w = TextRenderer.MeasureText(s, f, new Size(1000, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width; textW[k] = w; }
            return w;
        }

        // Nach einer Eingabe im Editor: nur diesen Block neu umbrechen; volles Layout nur, wenn sich die Hoehe der Zeile aendert.
        public void RelayoutCell(CellData c)
        {
            int n = Rows.Count;
            bool full = c == null || Width != lastW || Collapsed.Length != n || lblW == null || lblW.Length != n || c.RowIndex >= n || Collapsed[c.RowIndex] || c.RContent.IsEmpty;
            if (!full) foreach (CellData x in Cells) if (x != c && x.Dirty && !Collapsed[x.RowIndex]) { full = true; break; }
            if (full) { Relayout(); return; }
            int hits = c.Hits;
            if (c.Dirty) Build(c, ColW - 2 - 2 * TP, ColW - 2 - lblW[c.RowIndex] - 2 * TP);
            int[] sh;
            int ch = RowNeeds(c.RowIndex, Font.Height, out sh);
            if (c.Hits != hits || ch != c.RContent.Height + c.RBuf.Height + c.RFrz.Height + c.RSplit.Height || c.BufH != c.RBuf.Height || c.FrzH != c.RFrz.Height || c.SplitH != c.RSplit.Height || sh[0] != c.RPpl[0].Height || sh[1] != c.RPpl[1].Height || sh[2] != c.RPpl[2].Height) { Relayout(); return; }
            Invalidate(c.R);
        }

    }
}
