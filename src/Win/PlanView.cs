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
        public bool Editing, Dirty = true;
        public TextBlock CB;
        public TextBlock[] PB = new TextBlock[3];
        public int Hits;
        public Rectangle R, RHead, RLkz, RContent;
        public Rectangle[] RLbl = new Rectangle[3], RPpl = new Rectangle[3];
    }

    // Wochenansicht, selbst gezeichnet: schneller Tag-/Wochenwechsel (keine Hunderte Steuerelemente).
    // Nur beim Bearbeiten legt MainForm Eingabefelder ueber Inhalt und Kuerzel des aktiven Blocks.
    class PlanView : Control
    {
        public const int TP = 3, TV = 3, ShiftLines = 3;
        public static readonly int HeadH = Theme.HeadH, LkzH = Theme.LkzH, LW = Theme.LW, Pad = Theme.Pad, Gap = Theme.Gap;
        static readonly Regex Token = new Regex(@"[^\s,;/]+", RegexOptions.Compiled);
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
        public Action SelectionChanged;
        public bool[] Collapsed = new bool[0];              // je Zeile: zugeklappt (nur Kopfleiste sichtbar)
        public event EventHandler LayoutDone;

        readonly Font bold;
        int lastW = -1;

        public PlanView(Font f)
        {
            Font = f;
            bold = new Font(f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
        }

        public int ColLeft(int col) { return Pad + col * (ColW + Gap); }
        public void MarkAllDirty() { foreach (CellData c in Cells) c.Dirty = true; }

        // ---------- Textstuecke (Schrift, Farben, Markierungen) ----------

        static Color Rgb(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

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
                EquipmentInfo info = null;
                try { if (Equip != null) info = Equip.Get(m.Value); } catch (Exception) { /* Quelle nicht erreichbar */ }
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
                foreach (Match m in Token.Matches(text))
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

            if (Collapsed.Length != n) Collapsed = new bool[n];
            HitCount = 0;
            foreach (CellData c in Cells) { if (Collapsed[c.RowIndex]) continue; if (c.Dirty) Build(c, tw, pw); HitCount += c.Hits; }

            int[] ch = new int[n], lk = new int[n];
            int[][] sh = new int[n][];
            for (int r = 0; r < n; r++)
            {
                ch[r] = Rows[r].MinLines * lh + 2 * TV;
                lk[r] = Rows[r].HasLkz ? LkzH : 0;
                sh[r] = new int[] { ShiftLines * lh + 2 * TV, ShiftLines * lh + 2 * TV, ShiftLines * lh + 2 * TV };
            }
            foreach (CellData c in Cells)
            {
                if (Collapsed[c.RowIndex]) continue;
                int extra = c.Editing ? lh : 0;      // Editor bricht minimal anders um: eine Zeile Reserve
                ch[c.RowIndex] = Math.Max(ch[c.RowIndex], c.CB.Height + 2 * TV + extra);
                for (int s = 0; s < 3; s++) sh[c.RowIndex][s] = Math.Max(sh[c.RowIndex][s], c.PB[s].Height + 2 * TV + extra);
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
            Color back = today ? Theme.CellBackToday : (weekend ? Theme.CellBackWeekend : Theme.CellBack);
            Color border = c.Editing ? Color.FromArgb(255, 140, 0) : (c == Selected ? Color.FromArgb(40, 100, 200) : Theme.CellBorder);
            RowInfo ri = Rows[c.RowIndex];

            bool collapsed = Collapsed[c.RowIndex];
            Fill(g, border, c.R);
            Fill(g, today ? Theme.RowHeadToday : Theme.RowHead, c.RHead);
            if (!collapsed) Fill(g, back, c.RContent);
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

            // Pfeil (zu = nach rechts, offen = nach unten); Klick auf den Kopf klappt die Zeile um
            // Der Pfeil sitzt am rechten Rand, die Ueberschrift steht in jeder Spalte (auch zugeklappt).
            int ax = c.RHead.Right - 15, ay = c.RHead.Y + (c.RHead.Height - 7) / 2;
            Point[] tri = collapsed ? new Point[] { new Point(ax + 1, ay - 1), new Point(ax + 1, ay + 8), new Point(ax + 6, ay + 4) }
                                    : new Point[] { new Point(ax - 1, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 6) };
            if (Theme.ArrowSize == 0)   // moderner Look: kleinerer Pfeil, mittig in der Kopfleiste
            {
                ay = c.RHead.Y + (c.RHead.Height - 5) / 2;
                tri = collapsed ? new Point[] { new Point(ax + 2, ay - 1), new Point(ax + 2, ay + 6), new Point(ax + 6, ay + 3) }
                                : new Point[] { new Point(ax, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 5) };
            }
            using (SolidBrush ab = new SolidBrush(Theme.Arrow)) g.FillPolygon(ab, tri);

            int right = 18 + (c.Editing ? 20 : 0);                                   // Platz fuer Pfeil (+ Stift im Bearbeitungsmodus)
            Rectangle nr = new Rectangle(c.RHead.X + Theme.HeadTextX, c.RHead.Y, c.RHead.Width - 2 * Theme.HeadTextX - right, c.RHead.Height);
            if (collapsed)
            {
                bool filled = !c.Cur.IsEmpty || c.Cur.Lkz;
                if (filled)   // zugeklappt, aber Inhalt vorhanden: Hinweispunkt vor dem Pfeil
                    using (SolidBrush db = new SolidBrush(Color.FromArgb(215, 100, 0))) g.FillEllipse(db, c.RHead.Right - 29, c.RHead.Y + (c.RHead.Height - 7) / 2, 8, 8);
                nr.Width -= filled ? 12 : 0;
                TextRenderer.DrawText(g, ri.Name, bold, nr, Theme.HeadTextCollapsed, tf | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }
            TextRenderer.DrawText(g, ri.Name, bold, nr, Theme.HeadText, tf | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (c.Editing && Pencil != null) g.DrawImage(Pencil, c.RHead.Right - 38, c.RHead.Y + (c.RHead.Height - 17) / 2, 18, 18);

            if (ri.HasLkz)
            {
                Fill(g, c.Lkz ? Theme.LkzOn : back, c.RLkz);
                using (Pen p = new Pen(Theme.Separator)) g.DrawLine(p, c.RLkz.X, c.RLkz.Bottom - 1, c.RLkz.Right, c.RLkz.Bottom - 1);
                Rectangle lr = new Rectangle(c.RLkz.X + TP, c.RLkz.Y, c.RLkz.Width - TP, c.RLkz.Height);
                TextRenderer.DrawText(g, "LKZ gemessen?", Font, lr, Theme.LkzText, tf | TextFormatFlags.VerticalCenter);
                int tw = TextRenderer.MeasureText(g, "LKZ gemessen?", Font, new Size(1000, 100), tf).Width;
                Point cp = new Point(c.RLkz.X + TP + tw + 6, c.RLkz.Y + (c.RLkz.Height - 13) / 2);
                if (!Theme.Modern) CheckBoxRenderer.DrawCheckBox(g, cp, c.Lkz ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
                else Theme.DrawCheck(g, cp, c.Lkz);
            }

            if (!c.Editing) c.CB.Draw(g, c.RContent.X + TP, c.RContent.Y + TV);
            for (int s = 0; s < 3; s++)
            {
                Fill(g, Theme.ShiftLabel[s], c.RLbl[s]);
                Fill(g, Theme.ShiftBack[s], c.RPpl[s]);
                TextRenderer.DrawText(g, ri.Labels[s], bold, new Rectangle(c.RLbl[s].X, c.RLbl[s].Y + 1, c.RLbl[s].Width, c.RLbl[s].Height), Theme.ShiftLabelText, tf | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                if (!c.Editing) c.PB[s].Draw(g, c.RPpl[s].X + TP, c.RPpl[s].Y + TV);
                if (Theme.ShiftSep) using (Pen p = new Pen(Theme.ShiftLine)) g.DrawLine(p, c.RLbl[s].X, c.RLbl[s].Y, c.RPpl[s].Right - 1, c.RLbl[s].Y);
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
            if (c == null) return;
            if (c != Selected) { Selected = c; Invalidate(); if (SelectionChanged != null) SelectionChanged(); }
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
