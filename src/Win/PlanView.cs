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
        public int BufH;                                    // Hoehe des Puffer-Bereichs (0 = keine Puffer)
        public Rectangle[] RLbl = new Rectangle[3], RPpl = new Rectangle[3];
    }

    // Wochenansicht, selbst gezeichnet: schneller Tag-/Wochenwechsel (keine Hunderte Steuerelemente).
    // Nur beim Bearbeiten legt MainForm Eingabefelder ueber Inhalt und Kuerzel des aktiven Blocks.
    partial class PlanView : Control
    {
        public const int TP = 3, TV = 3, ShiftLines = 3, TaskLines = 1;
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
        }

        // Standard-Zuklappen: Zeile ohne Inhalt und ohne Haken/Variante im angezeigten Zeitraum ist zugeklappt.
        public bool[] DefaultFold()
        {
            bool[] d = new bool[Rows.Count];
            for (int r = 0; r < d.Length; r++) d[r] = true;
            foreach (CellData c in Cells) if (c.RowIndex < d.Length && !c.Cur.IsBlank) d[c.RowIndex] = false;
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
                    c.RChecks = c.RBuf = c.RContent = Rectangle.Empty;
                    for (int s = 0; s < 3; s++) c.RLbl[s] = c.RPpl[s] = Rectangle.Empty;
                    continue;
                }
                int cy = rowY[r] + 1 + HeadH;
                c.RChecks = new Rectangle(x + 1, cy, iw, lk[r]);
                cy += lk[r];
                c.RBuf = new Rectangle(x + 1, cy, iw, c.BufH);                       // Puffer ueber dem freien Inhalt; ch[r] umfasst beides
                c.RContent = new Rectangle(x + 1, cy + c.BufH, iw, ch[r] - c.BufH);
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
            int minC = ri.MinLines * lh + 2 * TV, ch = minC, min = (ri.Mode == ShiftMode.Task ? TaskLines : ShiftLines) * lh + 2 * TV;
            sh = new int[3];
            for (int s = 0; s < 3; s++) sh[s] = ri.HasShift(s) ? min : 0;
            if (Collapsed[r]) return ch;
            foreach (CellData c in Cells)
            {
                if (c.RowIndex != r) continue;
                int extra = 0;      // Editor: gleiche Zeilenhoehe/Breite wie die Ansicht (ExactLineSpacing), keine Reserve mehr noetig
                ch = Math.Max(ch, c.BufH + Math.Max(minC, c.CB.Height + 2 * TV + extra));
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
            if (c.Hits != hits || ch != c.RContent.Height + c.RBuf.Height || c.BufH != c.RBuf.Height || sh[0] != c.RPpl[0].Height || sh[1] != c.RPpl[1].Height || sh[2] != c.RPpl[2].Height) { Relayout(); return; }
            Invalidate(c.R);
        }

        // ---------- Zeichnen ----------

        static void Fill(Graphics g, Color col, Rectangle r) { g.FillRectangle(Theme.Current.Brush(col), r); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            PaintBands(g, e.ClipRectangle);
            PaintRounds(g, e.ClipRectangle);
            foreach (CellData c in Cells)
                if (c.R.IntersectsWith(e.ClipRectangle)) { PaintCell(g, c); Overlay(g, c); }
        }

        // Modellabhaengiges: Akzentstreifen und 2-px-Auswahlrahmen (win10)
        void Overlay(Graphics g, CellData c)
        {
            Theme th = Theme.Current;
            if (c.Changed && !c.Editing && c != Selected)   // dezent: 1-px-Rahmen + Ecke oben rechts
            {
                g.DrawRectangle(th.Pen(th.Changed, 1), c.R.X + 1, c.R.Y + 1, c.R.Width - 3, c.R.Height - 3);
                int x = c.R.Right - 1, y = c.R.Y + 1;
                g.FillPolygon(th.Brush(th.Changed), new Point[] { new Point(x - 8, y), new Point(x, y), new Point(x, y + 8) });
            }
            if (!th.Stripe) return;
            bool today = c.Day.Date == DateTime.Today, sel = c == Selected && !Collapsed[c.RowIndex];   // zugeklappt: nie ein Auswahlrahmen
            Color a = c.Editing ? th.BorderEdit : (sel ? th.BorderSel : (today ? th.Dot : Color.Empty));
            if (a.IsEmpty) return;
            Fill(g, a, new Rectangle(c.R.X, c.R.Y, c.R.Width, 3));                       // Streifen oben
            if (c.Editing || sel)
                g.DrawRectangle(th.Pen(a, 2), c.R.X + 1, c.R.Y + 1, c.R.Width - 3, c.R.Height - 3);
        }

        void PaintCell(Graphics g, CellData c)
        {
            Theme th = Theme.Current;
            Color back = th.CellBackFor(c.Day);
            RowInfo ri = Rows[c.RowIndex];
            bool collapsed = Collapsed[c.RowIndex];
            Color border = c.Editing ? th.BorderEdit : (c == Selected && !collapsed ? th.BorderSel : th.CellBorder);
            Fill(g, border, c.R);
            PaintHead(g, c, collapsed);   // PlanView.Head.cs
            if (collapsed) return;
            Fill(g, back, c.RContent);
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            if (c.RChecks.Height > 0) PaintChecks(g, c, back);
            if (c.RBuf.Height > 0) PaintBuffers(g, c, back);

            if (!c.Editing) c.CB.Draw(g, c.RContent.X + TP, c.RContent.Y + TV);
            bool task = ri.Mode == ShiftMode.Task;
            for (int s = 0; s < 3; s++)
            {
                if (c.RPpl[s].Height == 0) continue;   // ausgeblendete Schicht (Daten bleiben erhalten)
                Fill(g, task ? th.TaskBack : th.ShiftBack[s], c.RPpl[s]);
                if (c.RLbl[s].Width > 0)
                {
                    Fill(g, th.ShiftLabel[s], c.RLbl[s]);
                    TextRenderer.DrawText(g, ri.Labels[s], bold, new Rectangle(c.RLbl[s].X, c.RLbl[s].Y + 1, c.RLbl[s].Width, c.RLbl[s].Height), th.ShiftLabelText, tf | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                }
                if (!c.Editing) c.PB[s].Draw(g, c.RPpl[s].X + TP, c.RPpl[s].Y + TV);
                if (task && !c.Editing && c.People[s].Trim().Length == 0)   // Tagesaufgabe: Hinweis statt Schichtname
                    TextRenderer.DrawText(g, "Erledigt von", Font, new Rectangle(c.RPpl[s].X + TP, c.RPpl[s].Y + TV, c.RPpl[s].Width - TP, Font.Height), th.Muted, tf);
                if (th.ShiftSep) g.DrawLine(th.Pen(th.ShiftLine, 1), c.RLbl[s].X, c.RLbl[s].Y, c.RPpl[s].Right - 1, c.RLbl[s].Y);
            }
        }

        // ---------- Maus ----------

        CellData CellAt(Point p)
        {
            foreach (CellData c in Cells) if (c.R.Contains(p)) return c;
            return null;
        }

        readonly ToolTip tip = new ToolTip();
        string tipText = "";

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            CellData c = CellAt(e.Location);
            CheckSlot s = c != null ? SlotAt(c, e.Location) : null;
            int bi = c != null && s == null ? BufAt(c, e.Location) : -1;
            Cursor = bi >= 0 ? Cursors.Hand : Cursors.Default;
            string t = c == null ? RoundTip(e.Location) : s != null ? CheckTip(c, s) : bi >= 0 ? c.BufParts[bi].Tip
                : c.RHead.Contains(e.Location) ? (c.Runs.Count > 0 ? RunNumbers.Describe(c.Runs) : c.Note) : "";
            if (t != tipText) { tipText = t; tip.SetToolTip(this, t.Length > 0 ? t : null); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (RoundsMouse(e.Location, e.Clicks == 1 ? e.Button : MouseButtons.None)) return;
            CellData c = CellAt(e.Location);
            bool head = c != null && c.RHead.Contains(e.Location);
            // Auswahl nur durch Klick in einen Block; freie Flaeche und Linksklick auf den Zeilenkopf (Zuklappen) heben sie auf.
            // Rechtsklick auf den Kopf waehlt den Block (Verlauf…).
            CellData sel = c == null || (head && e.Button == MouseButtons.Left) ? null : c;
            if (sel != Selected) { Selected = sel; Invalidate(); }
            if (c == null || e.Button != MouseButtons.Left) return;
            CheckSlot s = head ? null : SlotAt(c, e.Location);
            if (e.Clicks != 1) return;
            if (head) { if (RowToggled != null) RowToggled(c.RowIndex); return; }
            if (s == null)
            {
                int bi = BufAt(c, e.Location);
                if (bi >= 0 && BufferClicked != null) BufferClicked(c, bi);
                return;
            }
            if (s.Add) { if (BufferClicked != null) BufferClicked(c, -1); }
            else if (MarkClicked != null && StateOf(c, s.Def) != DayState.NA && StateOf(c, s.Def) != DayState.Auto) MarkClicked(c, DefsOf(c.RowIndex)[s.Def].Key);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            CellData c = CellAt(e.Location);
            if (c == null || Collapsed[c.RowIndex] || c.RHead.Contains(e.Location)) return;
            if (!c.RChecks.Contains(e.Location) && !c.RBuf.Contains(e.Location) && CellActivated != null) CellActivated(c);
        }
    }
}
