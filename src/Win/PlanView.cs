using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
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
        public bool Editing, Dirty = true;
        public TextBlock CB;
        public TextBlock[] PB = new TextBlock[3];
        public int Hits;
        public Rectangle R, RHead, RContent;
        public Rectangle[] RLbl = new Rectangle[3], RPpl = new Rectangle[3];
    }

    // Wochenansicht, selbst gezeichnet: schneller Tag-/Wochenwechsel (keine Hunderte Steuerelemente).
    // Nur beim Bearbeiten legt MainForm Eingabefelder ueber den aktiven Block.
    class PlanView : Control
    {
        public const int HeadH = 24, LW = 26, Pad = 4, Gap = 4, TP = 3, TV = 3, ShiftLines = 3;
        public static readonly Color[] ShiftLabel = { Color.FromArgb(255, 225, 130), Color.FromArgb(255, 190, 130), Color.FromArgb(150, 175, 225) };
        public static readonly Color[] ShiftBack = { Color.FromArgb(255, 248, 225), Color.FromArgb(255, 240, 225), Color.FromArgb(232, 238, 250) };
        static readonly Regex Token = new Regex(@"[^\s,;/]+", RegexOptions.Compiled);
        static readonly Color Yellow = Color.FromArgb(255, 235, 59), Green = Color.FromArgb(170, 235, 170);

        public List<RowInfo> Rows = new List<RowInfo>();
        public List<CellData> Cells = new List<CellData>();
        public RowInfo Override;                            // Arbeitskopie der gerade bearbeiteten Zeile
        public DateTime Start;
        public IEquipmentStatusProvider Equip;
        public StatusColors Colors;
        public string MyKuerzel = "", Query = "";
        public CellData Selected;
        public int HitCount, ColW;
        public Action<CellData> CellActivated;
        public Action SelectionChanged;
        public event EventHandler LayoutDone;

        readonly Font bold;
        int lastW = -1;

        public PlanView(Font f)
        {
            Font = f;
            bold = new Font(f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = SystemColors.Control;
        }

        public RowInfo RowFor(int r) { return (Override != null && Override.Id == Rows[r].Id) ? Override : Rows[r]; }
        public int ColLeft(int col) { return Pad + col * (ColW + Gap); }
        public void MarkAllDirty() { foreach (CellData c in Cells) c.Dirty = true; }

        // ---------- Textstuecke (Farben, Markierungen) ----------

        static Color Rgb(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        static List<Piece> Group(string text, Color[] fore, int[] mode)
        {
            List<Piece> res = new List<Piece>();
            int n = text.Length, s = 0;
            for (int i = 1; i <= n; i++)
                if (i == n || fore[i] != fore[s] || mode[i] != mode[s])
                {
                    if (n > 0) res.Add(new Piece(text.Substring(s, i - s), fore[s], mode[s] != 0, mode[s] == 1 ? Yellow : Green));
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

        // Inhalt: manuelle Farben, darueber Statusfarbe erkannter Equipment-Nummern, Suchtreffer gelb.
        List<Piece> ContentPieces(CellData c, out int hits)
        {
            string text = c.Content;
            Color[] fore = new Color[text.Length];
            int[] mode = new int[text.Length];
            for (int i = 0; i < fore.Length; i++) fore[i] = Color.Black;
            foreach (ColorSpan sp in c.Cur.Spans)
                for (int i = sp.Start; i < sp.Start + sp.Length && i < fore.Length; i++) fore[i] = Rgb(sp.Rgb);
            if (Equip != null)
                foreach (Match m in EquipmentMatcher.Pattern.Matches(text))
                {
                    EquipmentInfo info = Equip.Get(m.Value);
                    if (info == null) continue;     // nur Nummern, die in der Tabelle existieren; nur die Nummer wird eingefaerbt
                    Color col = Rgb(Colors.Get(info.Status));
                    for (int i = m.Index; i < m.Index + m.Length; i++) fore[i] = col;
                }
            hits = MarkQuery(text, mode);
            return Group(text, fore, mode);
        }

        // Kuerzel: eigenes gruen, Suchtreffer gelb.
        List<Piece> PeoplePieces(string text, out int hits)
        {
            Color[] fore = new Color[text.Length];
            int[] mode = new int[text.Length];
            for (int i = 0; i < fore.Length; i++) fore[i] = Color.Black;
            if (MyKuerzel.Length > 0)
                foreach (Match m in Token.Matches(text))
                    if (string.Equals(m.Value, MyKuerzel, StringComparison.OrdinalIgnoreCase))
                        for (int i = m.Index; i < m.Index + m.Length; i++) mode[i] = 2;
            hits = MarkQuery(text, mode);
            return Group(text, fore, mode);
        }

        static List<Piece> Plain(string text)
        {
            List<Piece> l = new List<Piece>();
            if (text.Length > 0) l.Add(new Piece(text, Color.Black, false, Color.Empty));
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
                for (int s = 0; s < 3; s++) { c.PB[s] = TextBlock.Build(PeoplePieces(c.People[s], out h2), Font, pw); h += h2; }
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
            int iw = ColW - 2, tw = iw - 2 * TP, pw = iw - LW - 2 * TP, lh = Font.Height;

            HitCount = 0;
            foreach (CellData c in Cells) { if (c.Dirty) Build(c, tw, pw); HitCount += c.Hits; }

            int[] ch = new int[n];
            int[][] sh = new int[n][];
            for (int r = 0; r < n; r++)
            {
                ch[r] = RowFor(r).MinLines * lh + 2 * TV;
                sh[r] = new int[] { ShiftLines * lh + 2 * TV, ShiftLines * lh + 2 * TV, ShiftLines * lh + 2 * TV };
            }
            foreach (CellData c in Cells)
            {
                int extra = c.Editing ? lh : 0;      // Editor bricht minimal anders um: eine Zeile Reserve
                ch[c.RowIndex] = Math.Max(ch[c.RowIndex], c.CB.Height + 2 * TV + extra);
                for (int s = 0; s < 3; s++) sh[c.RowIndex][s] = Math.Max(sh[c.RowIndex][s], c.PB[s].Height + 2 * TV + extra);
            }

            int[] rowY = new int[n], rowH = new int[n];
            int y = Pad;
            for (int r = 0; r < n; r++)
            {
                rowH[r] = 1 + HeadH + ch[r] + 1 + sh[r][0] + sh[r][1] + sh[r][2] + 1;
                rowY[r] = y; y += rowH[r] + Gap;
            }
            foreach (CellData c in Cells)
            {
                int r = c.RowIndex, x = ColLeft(c.Col);
                c.R = new Rectangle(x, rowY[r], ColW, rowH[r]);
                c.RHead = new Rectangle(x + 1, rowY[r] + 1, iw, HeadH - 1);
                int cy = rowY[r] + 1 + HeadH;
                c.RContent = new Rectangle(x + 1, cy, iw, ch[r]);
                int sy = cy + ch[r] + 1;
                for (int s = 0; s < 3; s++)
                {
                    c.RLbl[s] = new Rectangle(x + 1, sy, LW, sh[r][s]);
                    c.RPpl[s] = new Rectangle(x + 1 + LW, sy, iw - LW, sh[r][s]);
                    sy += sh[r][s];
                }
            }
            if (Height != y + Pad) Height = y + Pad;
            Invalidate();
            if (LayoutDone != null) LayoutDone(this, EventArgs.Empty);
        }

        // ---------- Zeichnen ----------

        static void Fill(Graphics g, Color col, Rectangle r) { using (SolidBrush b = new SolidBrush(col)) g.FillRectangle(b, r); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            foreach (CellData c in Cells)
                if (c.R.IntersectsWith(e.ClipRectangle)) PaintCell(g, c);
        }

        void PaintCell(Graphics g, CellData c)
        {
            bool today = c.Day.Date == DateTime.Today;
            bool weekend = c.Day.DayOfWeek == DayOfWeek.Saturday || c.Day.DayOfWeek == DayOfWeek.Sunday;
            Color headBack = today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            Color back = today ? Color.FromArgb(255, 250, 230) : (weekend ? Color.FromArgb(244, 246, 250) : Color.White);
            Color border = c.Editing ? Color.FromArgb(255, 140, 0) : (c == Selected ? Color.FromArgb(40, 100, 200) : Color.FromArgb(150, 160, 175));

            Fill(g, border, c.R);
            Fill(g, headBack, c.RHead);
            Fill(g, back, c.RContent);
            RowInfo ri = RowFor(c.RowIndex);
            if (!c.Editing)
            {
                Rectangle nr = new Rectangle(c.RHead.X + 2, c.RHead.Y, c.RHead.Width - 4, c.RHead.Height);
                TextRenderer.DrawText(g, ri.Name, bold, nr, Color.Black, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                c.CB.Draw(g, Font, c.RContent.X + TP, c.RContent.Y + TV);
            }
            for (int s = 0; s < 3; s++)
            {
                Fill(g, ShiftLabel[s], c.RLbl[s]);
                Fill(g, ShiftBack[s], c.RPpl[s]);
                if (c.Editing) continue;
                TextRenderer.DrawText(g, ri.Labels[s], bold, c.RLbl[s], Color.Black, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                c.PB[s].Draw(g, Font, c.RPpl[s].X + TP, c.RPpl[s].Y + TV);
            }
        }

        // ---------- Maus ----------

        CellData CellAt(Point p)
        {
            foreach (CellData c in Cells) if (c.R.Contains(p)) return c;
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CellData c = CellAt(e.Location);
            if (c != null && c != Selected) { Selected = c; Invalidate(); if (SelectionChanged != null) SelectionChanged(); }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            CellData c = CellAt(e.Location);
            if (c != null && CellActivated != null) CellActivated(c);
        }
    }
}
