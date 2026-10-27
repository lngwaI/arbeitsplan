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
        public string Variant = "";                         // angezeigte/bearbeitete Variante
        public List<string> Marks = new List<string>();     // angezeigte/bearbeitete Pflichtpunkte
        public bool Editing, Dirty = true;

        // Angezeigten Stand aus e uebernehmen (ohne Cur zu aendern)
        public void Show(Entry e)
        {
            Content = e.Content; People = (string[])e.People.Clone(); Lkz = e.Lkz; Variant = e.Variant; Marks = new List<string>(e.Marks); Dirty = true;
        }
        public TextBlock CB;
        public TextBlock[] PB = new TextBlock[3];
        public int Hits;
        public List<RunRef> Runs = new List<RunRef>();      // Laufnummern im Inhalt (Kennzeichnung je Produkt)
        public string RunsFor;
        public Rectangle R, RHead, RLkz, RContent;
        public Rectangle[] RLbl = new Rectangle[3], RPpl = new Rectangle[3];
    }

    // Wochenansicht, selbst gezeichnet: schneller Tag-/Wochenwechsel (keine Hunderte Steuerelemente).
    // Nur beim Bearbeiten legt MainForm Eingabefelder ueber Inhalt und Kuerzel des aktiven Blocks.
    class PlanView : Control
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
        public Action<CellData> CellActivated, LkzClicked;
        public Action<int> RowToggled;                      // Klick auf den Kopf einer Zeile: ein-/ausklappen (nur lokal)
        public bool[] Collapsed = new bool[0];              // je Zeile: zugeklappt (nur Kopfleiste sichtbar)
        public event EventHandler LayoutDone;

        readonly Font bold;
        int lastW = -1;
        int lkzLabelW, lkzTagW;                             // Textbreiten, einmal je Layout gemessen (nicht je Zelle im Paint)
        int[] nameW;                                        // null = neu messen
        int[] lblW;                                         // Breite der Schichtbezeichnung je Zeile (LabelW)
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
            if (disposing) { bold.Dispose(); tip.Dispose(); }
            base.Dispose(disposing);
        }

        // ---------- Textstuecke (Schrift, Farben, Markierungen) ----------

        static Color Rgb(int rgb) { return Ui.Rgb(rgb); }

        List<Piece> Group(string text, Color[] fore, Font[] fnt, int[] mode)
        {
            List<Piece> res = new List<Piece>();
            int n = text.Length, s = 0;
            for (int i = 1; i <= n; i++)
                if (i == n || fore[i] != fore[s] || mode[i] != mode[s] || fnt[i] != fnt[s])
                {
                    res.Add(new Piece(text.Substring(s, i - s), fnt[s], fore[s], mode[s] != 0, mode[s] == 1 ? Yellow : Green));
                    s = i;
                }
            return res;
        }

        int MarkQuery(string text, int[] mode)
        {
            int hits = 0;
            if (Query.Length == 0) return 0;
            int idx = 0;
            while ((idx = text.IndexOf(Query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                for (int k = idx; k < idx + Query.Length; k++) mode[k] = 1;
                hits++; idx += Query.Length;
            }
            return hits;
        }

        public static Font SpanFont(FormatSpan sp, Font baseFont)
        {
            FontStyle st = (sp.Bold ? FontStyle.Bold : 0) | (sp.Italic ? FontStyle.Italic : 0);
            return FontCache.Get(string.IsNullOrEmpty(sp.Font) ? baseFont.Name : sp.Font, sp.Size > 0 ? sp.Size : baseFont.SizeInPoints, st, baseFont);
        }

        // Inhalt: manuelle Formatierung, darueber Statusfarbe der Equipment-Nummern, Suchtreffer gelb.
        List<Piece> ContentPieces(CellData c, out int hits)
        {
            string text = c.Content;
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            foreach (FormatSpan sp in c.Cur.Spans)
            {
                Font f = SpanFont(sp, plain);
                for (int i = sp.Start; i < sp.Start + sp.Length && i < fore.Length; i++) { if (sp.Rgb != 0) fore[i] = Rgb(sp.Rgb); fnt[i] = f; }
            }
            // Erkannte Equipment-Nummer: Statusfarbe; Status unbekannt/nicht abrufbar -> Standard schwarz (Lesbarkeit).
            foreach (Match m in EquipmentMatcher.Pattern.Matches(text))
            {
                EquipmentInfo info;
                if (!equipCache.TryGetValue(m.Value, out info))   // je Nummer nur einmal je Rebuild (langsame Quelle blockiert nicht jedes Layout)
                {
                    try { if (Equip != null) info = Equip.Get(m.Value); } catch (Exception) { info = null; /* Quelle nicht erreichbar */ }
                    equipCache[m.Value] = info;
                }
                Color col = info != null ? Rgb(Colors.Get(info.Status)) : Color.Black;
                for (int i = m.Index; i < m.Index + m.Length; i++) fore[i] = col;
            }
            hits = MarkQuery(text, mode);
            return Group(text, fore, fnt, mode);
        }

        // Kuerzel: eigenes gruen, Suchtreffer gelb.
        List<Piece> PeoplePieces(string text, out int hits)
        {
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            if (MyKuerzel.Length > 0)
                foreach (Match m in PersonPlan.Token.Matches(text))
                    if (string.Equals(m.Value, MyKuerzel, StringComparison.OrdinalIgnoreCase))
                        for (int i = m.Index; i < m.Index + m.Length; i++) mode[i] = 2;
            hits = MarkQuery(text, mode);
            return Group(text, fore, fnt, mode);
        }

        List<Piece> Plain(string text)
        {
            List<Piece> l = new List<Piece>();
            if (text.Length > 0) l.Add(new Piece(text, Font, Color.Black, false, Color.Empty));
            return l;
        }

        void Build(CellData c, int tw, int pw)
        {
            int h = 0, h2;
            if (c.Editing)
            {
                c.CB = TextBlock.Build(Plain(c.Content), Font, tw);
                for (int s = 0; s < 3; s++) c.PB[s] = TextBlock.Build(Plain(c.People[s]), Font, pw);
            }
            else
            {
                c.CB = TextBlock.Build(ContentPieces(c, out h), Font, tw);
                for (int s = 0; s < 3; s++) { c.PB[s] = TextBlock.Build(PeoplePieces(c.People[s], out h2), Font, pw); if (Rows[c.RowIndex].HasShift(s)) h += h2; }
            }
            c.Hits = h;
            c.Dirty = false;
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

            nameW = null;   // Textbreiten beim naechsten Zeichnen neu messen

            int[] ch = new int[n], lk = new int[n];
            int[][] sh = new int[n][];
            for (int r = 0; r < n; r++)
            {
                ch[r] = RowNeeds(r, lh, out sh[r]);
                lk[r] = Rows[r].HasLkz ? LkzH : 0;
            }

            int[] rowY = new int[n], rowH = new int[n];
            int y = Pad;
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
                    c.RLkz = c.RContent = Rectangle.Empty;
                    for (int s = 0; s < 3; s++) c.RLbl[s] = c.RPpl[s] = Rectangle.Empty;
                    continue;
                }
                int cy = rowY[r] + 1 + HeadH;
                c.RLkz = new Rectangle(x + 1, cy, iw, lk[r]);
                cy += lk[r];
                c.RContent = new Rectangle(x + 1, cy, iw, ch[r]);
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
            int ch = ri.MinLines * lh + 2 * TV, min = (ri.Mode == ShiftMode.Task ? TaskLines : ShiftLines) * lh + 2 * TV;
            sh = new int[3];
            for (int s = 0; s < 3; s++) sh[s] = ri.HasShift(s) ? min : 0;
            if (Collapsed[r]) return ch;
            foreach (CellData c in Cells)
            {
                if (c.RowIndex != r) continue;
                int extra = c.Editing ? lh : 0;      // Editor bricht minimal anders um: eine Zeile Reserve
                ch = Math.Max(ch, c.CB.Height + 2 * TV + extra);
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
            string k = (f == bold ? "b|" : "n|") + s;
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
            if (c.Hits != hits || ch != c.RContent.Height || sh[0] != c.RPpl[0].Height || sh[1] != c.RPpl[1].Height || sh[2] != c.RPpl[2].Height) { Relayout(); return; }
            Invalidate(c.R);
        }

        // ---------- Zeichnen ----------

        static void Fill(Graphics g, Color col, Rectangle r) { g.FillRectangle(Theme.Current.Brush(col), r); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (nameW == null || nameW.Length != Rows.Count)   // einmal je Layout mit dem Zeichen-Graphics messen (wie zuvor je Zelle)
            {
                const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                Size big = new Size(1000, 100);
                lkzLabelW = TextRenderer.MeasureText(g, "LKZ gemessen?", Font, big, tf).Width;
                lkzTagW = TextRenderer.MeasureText(g, "(LKZ)", bold, big, tf).Width;
                int[] w = new int[Rows.Count];
                for (int r = 0; r < w.Length; r++) if (Rows[r].HasLkz) w[r] = TextRenderer.MeasureText(g, Rows[r].Name, bold, big, tf).Width;
                nameW = w;
            }
            foreach (CellData c in Cells)
                if (c.R.IntersectsWith(e.ClipRectangle)) { PaintCell(g, c); Overlay(g, c); }
        }

        // Modellabhaengiges: Akzentstreifen und 2-px-Auswahlrahmen (win10)
        void Overlay(Graphics g, CellData c)
        {
            Theme th = Theme.Current;
            if (!th.Stripe) return;
            bool today = c.Day.Date == DateTime.Today;
            Color a = c.Editing ? th.BorderEdit : (c == Selected ? th.BorderSel : (today ? th.Dot : Color.Empty));
            if (a.IsEmpty) return;
            Fill(g, a, new Rectangle(c.R.X, c.R.Y, c.R.Width, 3));                       // Streifen oben
            if (c.Editing || c == Selected)
                g.DrawRectangle(th.Pen(a, 2), c.R.X + 1, c.R.Y + 1, c.R.Width - 3, c.R.Height - 3);
        }

        void PaintCell(Graphics g, CellData c)
        {
            bool today = c.Day.Date == DateTime.Today;
            Theme th = Theme.Current;
            Color back = th.CellBackFor(c.Day);
            Color border = c.Editing ? th.BorderEdit : (c == Selected ? th.BorderSel : th.CellBorder);
            RowInfo ri = Rows[c.RowIndex];

            bool collapsed = Collapsed[c.RowIndex];
            Fill(g, border, c.R);
            th.FillHead(g, c.RHead, today ? th.RowHeadToday : th.RowHead);
            if (!collapsed) Fill(g, back, c.RContent);
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

            // Pfeil (zu = nach rechts, offen = nach unten); Klick auf den Kopf klappt die Zeile um
            // Der Pfeil sitzt am rechten Rand, die Ueberschrift steht in jeder Spalte (auch zugeklappt).
            int ax = c.RHead.Right - 15, ay = c.RHead.Y + (c.RHead.Height - 7) / 2;
            Point[] tri = collapsed ? new Point[] { new Point(ax + 1, ay - 1), new Point(ax + 1, ay + 8), new Point(ax + 6, ay + 4) }
                                    : new Point[] { new Point(ax - 1, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 6) };
            if (th.Modern)   // heller Look: kleinerer Pfeil, mittig in der Kopfleiste
            {
                ay = c.RHead.Y + (c.RHead.Height - 5) / 2;
                tri = collapsed ? new Point[] { new Point(ax + 2, ay - 1), new Point(ax + 2, ay + 6), new Point(ax + 6, ay + 3) }
                                : new Point[] { new Point(ax, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 5) };
            }
            g.FillPolygon(th.Brush(th.Arrow), tri);

            int right = 18 + (c.Editing ? 20 : 0);                                   // Platz fuer Pfeil (+ Stift im Bearbeitungsmodus)
            int mk = ProductBars(g, c);
            Rectangle nr = new Rectangle(c.RHead.X + th.HeadTextX + mk, c.RHead.Y, c.RHead.Width - 2 * th.HeadTextX - right - mk, c.RHead.Height);
            if (collapsed)
            {
                bool filled = !c.Cur.IsBlank;
                if (filled)   // zugeklappt, aber Inhalt vorhanden: Hinweispunkt vor dem Pfeil
                    g.FillEllipse(th.Brush(th.Dot),c.RHead.Right - 29, c.RHead.Y + (c.RHead.Height - 7) / 2, 8, 8);
                nr.Width -= filled ? 12 : 0;
                TextRenderer.DrawText(g, ri.Name, bold, nr, th.HeadTextCollapsed,tf | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                LkzTag(g, c, ri, nr, tf);
                return;
            }
            TextRenderer.DrawText(g, ri.Name, bold, nr, th.HeadText,tf | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            LkzTag(g, c, ri, nr, tf);
            if (c.Editing && Pencil != null) g.DrawImage(Pencil, c.RHead.Right - 38, c.RHead.Y + (c.RHead.Height - 17) / 2, 18, 18);

            if (ri.HasLkz)
            {
                Fill(g, c.Lkz ? th.LkzOn : back, c.RLkz);
                g.DrawLine(th.Pen(th.Separator, 1), c.RLkz.X, c.RLkz.Bottom - 1, c.RLkz.Right, c.RLkz.Bottom - 1);
                Rectangle lr = new Rectangle(c.RLkz.X + TP, c.RLkz.Y, c.RLkz.Width - TP, c.RLkz.Height);
                TextRenderer.DrawText(g, "LKZ gemessen?", Font, lr, th.LkzText, tf | TextFormatFlags.VerticalCenter);
                Point cp = new Point(c.RLkz.X + TP + lkzLabelW + 6, c.RLkz.Y + (c.RLkz.Height - 13) / 2);
                if (!th.Modern) CheckBoxRenderer.DrawCheckBox(g, cp, c.Lkz ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
                else th.DrawCheck(g, cp, c.Lkz);
            }

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

        // Dezente Produktfarben (stabil aus dem Produktcode); im hellen Look etwas blasser.
        static readonly int[][] Tones = {
            new[] { 70, 130, 180 }, new[] { 90, 160, 110 }, new[] { 190, 120, 60 }, new[] { 150, 100, 170 },
            new[] { 60, 150, 160 }, new[] { 180, 90, 100 }, new[] { 140, 140, 60 }, new[] { 110, 120, 150 } };

        public static Color ProductColor(string product)
        {
            int[] t = Tones[RunNumbers.ProductIndex(product, Tones.Length)];
            return Color.FromArgb(t[0], t[1], t[2]);
        }

        // Farbbalken je Produkt am linken Rand der Kopfleiste (max. 3); Rueckgabe = belegte Breite.
        int ProductBars(Graphics g, CellData c)
        {
            if (c.RunsFor != c.Content) { c.Runs = RunNumbers.Find(c.Content); c.RunsFor = c.Content; }
            if (c.Runs.Count == 0) return 0;
            List<string> prods = new List<string>();
            foreach (RunRef r in c.Runs) if (!prods.Contains(r.Product) && prods.Count < 3) prods.Add(r.Product);
            int x = c.RHead.X + 1, h = c.RHead.Height - 4;
            foreach (string p in prods) { Fill(g, ProductColor(p), new Rectangle(x, c.RHead.Y + 2, 4, h)); x += 5; }
            return prods.Count * 5 + 1;
        }

        // "(LKZ)" neben dem Zeilennamen: rot = noch zu messen, gruen = gemessen (nur Zeilen mit LKZ-Feld).
        void LkzTag(Graphics g, CellData c, RowInfo ri, Rectangle nr, TextFormatFlags tf)
        {
            if (!ri.HasLkz) return;
            int w = nameW[c.RowIndex], tw = lkzTagW;
            if (w + 6 + tw > nr.Width) return;
            TextRenderer.DrawText(g, "(LKZ)", bold, new Rectangle(nr.X + w + 6, nr.Y, tw + 2, nr.Height), c.Lkz ? Color.FromArgb(30, 140, 60) : Color.FromArgb(200, 30, 30), tf | TextFormatFlags.VerticalCenter);
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
            string t = c != null && c.RHead.Contains(e.Location) && c.Runs.Count > 0 ? RunNumbers.Describe(c.Runs) : "";
            if (t != tipText) { tipText = t; tip.SetToolTip(this, t.Length > 0 ? t : null); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CellData c = CellAt(e.Location);
            if (c == null) return;
            if (c != Selected) { Selected = c; Invalidate(); }
            if (e.Clicks != 1 || e.Button != MouseButtons.Left) return;
            if (c.RHead.Contains(e.Location)) { if (RowToggled != null) RowToggled(c.RowIndex); return; }
            if (!Collapsed[c.RowIndex] && Rows[c.RowIndex].HasLkz && c.RLkz.Contains(e.Location) && LkzClicked != null) LkzClicked(c);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            CellData c = CellAt(e.Location);
            if (c == null || Collapsed[c.RowIndex] || c.RHead.Contains(e.Location)) return;
            if (!(Rows[c.RowIndex].HasLkz && c.RLkz.Contains(e.Location)) && CellActivated != null) CellActivated(c);
        }
    }
}
