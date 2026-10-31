using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Vorschau eines Schritts wie im Plan: je Lauf ein Block mit Raum-Kopf (Tag/Datum rechts), Herstellung und Plantext mit Format
    // (z. B. rote, fette Kennung). Statusfarben der Equipment-Nummern erst im Plan (wie im Bearbeitungsmodus).
    class BlockPreview : Control
    {
        public class Card { public string Room = "", Day = ""; public Entry Data; public bool Missing; }
        List<Card> cards = new List<Card>();
        readonly List<TextBlock> blocks = new List<TextBlock>();
        readonly List<List<BufPart>> bufs = new List<List<BufPart>>();   // Puffer je Karte (UX-5b): eigener Ton, Kopf "Name · für Nr", Abstand
        const int Pad = 6, Gap = 8, StripeW = 3, BufIndent = 10;

        public event Action<int> Page;   // Pfeiltasten: -1 = voriger, +1 = naechster Eintrag

        public BlockPreview() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true); TabStop = true; }

#if TESTHOOKS
        public int Count { get { return cards.Count; } }
        internal Card TestCard(int i) { return cards[i]; }
        internal List<Rectangle> TestBands(int i) { return Bands(i, Gap / 2); }   // erste Karte beginnt bei y = Gap / 2
        internal int TestBufCount(int i) { return bufs[i].Count; }
        public void TestKey(Keys k) { OnKeyDown(new KeyEventArgs(k)); }
#endif

        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            bool back = e.KeyCode == Keys.Left || e.KeyCode == Keys.Up, fwd = e.KeyCode == Keys.Right || e.KeyCode == Keys.Down;
            if ((back || fwd) && Page != null) { Page(back ? -1 : 1); e.Handled = true; }
        }

        int HeadH { get { return Font.Height + 8; } }

        static int BufsH(List<BufPart> l) { int h = 0; foreach (BufPart p in l) h += p.H + BufStyle.Gap; return l.Count == 0 ? 0 : h - BufStyle.Gap + Pad; }

        // Puffer-Baender der Karte i, wenn sie bei y beginnt (Zeichnen und Test nutzen dieselbe Rechnung)
        List<Rectangle> Bands(int i, int y)
        {
            List<Rectangle> r = new List<Rectangle>(); int ty = y + HeadH + Pad;
            foreach (BufPart p in bufs[i]) { r.Add(new Rectangle(Gap + 1, ty, Width - 2 * Gap - 2, p.H)); ty += p.H + BufStyle.Gap; }
            return r;
        }

        // Hoehe der Karte i: Kopf, Puffer, Inhalt, Innenabstand
        int CardH(int i) { return HeadH + BufsH(bufs[i]) + (blocks[i] != null ? blocks[i].Height : 0) + 2 * Pad; }

        public void Set(List<Card> list, int width) { cards = list; Relayout(width); }

        public void Relayout(int width)
        {
            blocks.Clear(); bufs.Clear();
            int h = Gap / 2, cw = Math.Max(40, width - 2 * Gap - 2 * Pad), tw = Math.Max(40, width - 2 * Gap - 2 - StripeW - 8);
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font), bold = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Bold, Font);
            foreach (Card c in cards)
            {
                List<BufPart> parts = new List<BufPart>();
                if (c.Data != null)
                    for (int i = 0; i < c.Data.Buffers.Count; i++)
                    {
                        BufferItem bi = c.Data.Buffers[i];
                        BufPart p = new BufPart(); p.Typ = bi.Typ;
                        string head = bi.HeadText();
                        List<Piece> hp = new List<Piece>();
                        hp.Add(new Piece(head.Substring(0, head.Length - bi.LaufText.Length), bold, Color.Black, false, Color.Empty));
                        if (bi.LaufText.Length > 0) hp.Add(new Piece(bi.LaufText, bold, Theme.Current.Muted, false, Color.Empty));
                        p.Head = TextBlock.Build(hp, bold, tw);
                        if (bi.Text.Length > 0) p.Body = TextBlock.Build(NotePieces(bi.Text, plain), plain, tw - BufIndent);
                        p.Y = 0; p.H = BufStyle.Pad + p.Head.Height + (p.Body != null ? p.Body.Height : 0) + BufStyle.Pad;
                        parts.Add(p);
                    }
                bufs.Add(parts);
                TextBlock b = c.Data != null && (parts.Count == 0 || c.Data.Content.Length > 0) ? TextBlock.Build(Pieces(c.Data, plain), plain, cw) : null;
                blocks.Add(b);
                h += CardH(bufs.Count - 1) + Gap;
            }
            Size = new Size(Math.Max(40, width), Math.Max(h, 20));
            Invalidate();
        }

        // Puffer-Text: die Notiz einer Equipment-Zeile (BufferSet.NoteMark) steht kursiv, eingerueckt unter dem Geraet (UX-5f)
        static List<Piece> NotePieces(string text, Font plain)
        {
            bool[] it; string t = BufferSet.ForDisplay(text, out it);
            Font ital = FontCache.Get(plain.Name, plain.SizeInPoints, FontStyle.Italic, plain);
            List<Piece> res = new List<Piece>();
            for (int s = 0, i = 1; i <= t.Length; i++)
                if (i == t.Length || it[i] != it[s]) { res.Add(new Piece(t.Substring(s, i - s), it[s] ? ital : plain, Color.Black, false, Color.Empty)); s = i; }
            return res;
        }

        static List<Piece> Pieces(Entry e, Font plain)
        {
            string text = e.Content;
            Color[] fore = new Color[text.Length]; Font[] fnt = new Font[text.Length];
            for (int i = 0; i < text.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            foreach (FormatSpan sp in e.Spans)
            {
                Font f = PlanView.SpanFont(sp, plain);
                for (int i = sp.Start; i < sp.Start + sp.Length && i < text.Length; i++) { if (sp.Rgb != 0) fore[i] = Ui.Rgb(sp.Rgb); fnt[i] = f; }
            }
            foreach (int[] nl in Notes.NoteLines(e))   // Notizzeilen kursiv, wie im Plan
                for (int i = nl[1]; i < nl[1] + nl[2] && i < text.Length; i++) fnt[i] = FontCache.Get(fnt[i].Name, fnt[i].SizeInPoints, fnt[i].Style | FontStyle.Italic, fnt[i]);
            List<Piece> res = new List<Piece>();
            for (int s = 0, i = 1; i <= text.Length; i++)
                if (i == text.Length || fore[i] != fore[s] || fnt[i] != fnt[s]) { res.Add(new Piece(text.Substring(s, i - s), fnt[s], fore[s], false, Color.Empty)); s = i; }
            return res;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(Color.White);
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            int y = Gap / 2, w = Width - 2 * Gap;
            using (Font bold = new Font(Font, FontStyle.Bold))
                for (int i = 0; i < cards.Count; i++)
                {
                    Card c = cards[i];
                    TextBlock b = blocks[i];
                    int h = CardH(i);
                    Rectangle head = new Rectangle(Gap, y, w, HeadH);
                    t.FillHead(g, head, t.RowHead);
                    int dw = TextRenderer.MeasureText(c.Day, Font).Width + 8;
                    TextRenderer.DrawText(g, c.Room, bold, new Rectangle(head.X + 6, y, w - dw - 10, HeadH), c.Missing ? Color.Firebrick : t.HeadText, ff);
                    TextRenderer.DrawText(g, c.Day, Font, new Rectangle(head.Right - dw, y, dw - 4, HeadH), t.HeadText, ff | TextFormatFlags.Right);
                    int ty = y + HeadH + Pad;
                    List<BufPart> parts = bufs[i];
                    List<Rectangle> bands = Bands(i, y);
                    for (int k = 0; k < parts.Count; k++)
                    {
                        BufPart p = parts[k];
                        Rectangle band = bands[k];
                        using (Brush tb = new SolidBrush(BufStyle.Tint(k))) g.FillRectangle(tb, band);   // eigener Hintergrundton je Puffer (wechselt), Seitenstrich = Typ
                        using (Brush sb = new SolidBrush(t.BufColors[p.Typ == "stationaer" ? 1 : 0])) g.FillRectangle(sb, band.X, band.Y, StripeW, band.Height);
                        p.Head.Draw(g, band.X + StripeW + 4, band.Y + BufStyle.Pad);
                        if (p.Body != null) p.Body.Draw(g, band.X + StripeW + 4 + BufIndent, band.Y + BufStyle.Pad + p.Head.Height);
                        if (k < parts.Count - 1) { int ly = band.Bottom + BufStyle.Gap / 2; g.DrawLine(t.Pen(BufStyle.Line, 1), band.X + 2, ly, band.Right - 3, ly); }
                    }
                    if (parts.Count > 0) ty = bands[parts.Count - 1].Bottom + Pad;
                    if (b != null) b.Draw(g, Gap + Pad, ty);
                    g.DrawRectangle(t.Pen(t.CellBorder, 1), Gap, y, w - 1, h - 1);
                    y += h + Gap;
                }
            if (cards.Count == 0) TextRenderer.DrawText(g, "Kein Schritt gewählt.", Font, new Point(6, 6), Section.HelpColor);
        }
    }
}
