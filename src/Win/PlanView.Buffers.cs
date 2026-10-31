using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Ein angezeigter Puffer: Kopfzeile "Puffer n: Name" und eingerueckter Text; Y/H relativ zum Puffer-Bereich.
    class BufPart { public TextBlock Head, Body; public int Y, H; public string Typ = "", Tip = "", MadeTip = ""; public bool Made; public Rectangle Box; }   // Box = Haken "hergestellt" (relativ zum Puffer-Bereich)

    // Puffer-Bereich eines Blocks (Zeilen mit Puffer-Typen): ueber dem freien Inhalt je Puffer eine Zeile "Puffer 1: Name" mit
    // eingerueckter Beschreibung; der Typ steht nie als Text, nur als 3-px-Seitenstrich links (Farbe aus Theme.BufColors) und im Tooltip.
    partial class PlanView
    {
        public Action<CellData, int> BufferClicked;   // Klick auf Puffer (Index) bzw. auf "+ Puffer" (-1)
        public Action<CellData, int> MadeClicked;     // Klick auf den Haken "hergestellt" des Puffers (Index), nur mit BufMade
        public bool BufMade;                          // Einstellung "Puffer als hergestellt abhaken" (DutyConfig.BufMade): zeigt den Haken je Puffer
        const int StripeW = 3, BufIndent = 10, BufGap = BufStyle.Gap, BufPad = BufStyle.Pad;

        Font SmallFont { get { return FontCache.Get(Font.Name, Math.Max(7f, Font.SizeInPoints - 1f), FontStyle.Regular, Font); } }

        string TypTip(string typ)
        {
            if (typ == "mobil") return BufMade ? "mobil – Tagesprobe gezogen, sobald dieser oder ein anderer mobiler Puffer des Tages hergestellt ist" : "mobil – Tagesprobe mit Herstellung erledigt";
            if (typ == "stationaer") return BufMade ? "stationär – Wochenprobe gezogen, sobald ein stationärer Puffer der KW hergestellt ist" : "stationär – erledigt Wochenprobe der KW";
            return typ.Length > 0 ? VarLabel(typ) : "ohne Typ";
        }

        Color TypColor(int row, string typ)
        {
            Theme th = Theme.Current;
            int i = Array.IndexOf(Rows[row].VariantList(), typ);
            return i < 0 ? th.Muted : th.BufColors[i % th.BufColors.Length];
        }

        // Text mit Statusfarbe erkannter Equipment-Nummern und Suchtreffern (wie der Inhalt, ohne Formatierung)
        List<Piece> BufferPieces(string text, bool search, out int hits)
        {
            bool[] ital; text = BufferSet.ForDisplay(text, out ital);   // Notiz einer Equipment-Zeile: eigene eingerueckte Zeile, kursiv (UX-5f)
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font), italic = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Italic, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = ital[i] ? italic : plain; }
            foreach (Match m in EquipmentMatcher.Pattern.Matches(text))
            {
                Color col = Rgb(StatusRgb(m.Value));
                for (int i = m.Index; i < m.Index + m.Length; i++) fore[i] = col;
            }
            hits = search ? MarkQuery(text, mode) : 0;
            return Group(text, fore, fnt, mode);
        }

        // Kopfzeile "Name · Lauf m" ganz fett (ohne Namen "–" grau, Lauf-Angabe grau), Suchtreffer nur im Namen
        List<Piece> BufferHead(BufferItem b, bool search, out int hits)
        {
            string name = b.Name, text = b.HeadText();
            int laufAt = text.Length - b.LaufText.Length;
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                fore[i] = i >= laufAt || name.Length == 0 ? Theme.Current.Muted : Color.Black;
                fnt[i] = bold;
            }
            hits = 0;
            if (search && name.Length > 0 && Query.Length > 0)
            {
                int[] nm = new int[name.Length];
                hits = MarkQuery(name, nm);
                Array.Copy(nm, 0, mode, 0, nm.Length);
            }
            return Group(text, fore, fnt, mode);
        }

        void BuildBuffers(CellData c, int tw, out int hits)
        {
            hits = 0;
            c.BufParts.Clear(); c.BufH = 0;
            if (c.Buffers.Count == 0) return;
            int aw = tw - StripeW, y = TV, h2, bs = Theme.Current.CheckBox, mw = BufMade ? TextW("hergestellt", SmallFont) + bs + 10 : 0;
            for (int i = 0; i < c.Buffers.Count; i++)
            {
                BufferItem b = c.Buffers[i];
                BufPart p = new BufPart(); p.Typ = b.Typ; p.Tip = TypTip(b.Typ) + (b.LaufRef > 0 ? "\nbedient Lauf " + b.LaufRef : "") + "\nKlick: Puffer bearbeiten";
                p.Head = TextBlock.Build(BufferHead(b, !c.Editing, out h2), Font, aw - 2 - mw); hits += h2;
                if (b.Text.Length > 0) { p.Body = TextBlock.Build(BufferPieces(b.Text, !c.Editing, out h2), Font, aw - BufIndent - 2); hits += h2; }
                if (BufMade)
                {
                    p.Made = b.Made; p.Box = new Rectangle(tw - mw - 3, y + BufPad, mw, bs + 2);
                    p.MadeTip = "Puffer hergestellt: Klick = hergestellt / nicht hergestellt (speichert sofort).\n" + (b.Typ == "mobil" ? "Hergestellt → Tagesprobe dieses Tages gezogen (ein mobiler Puffer genügt)."
                        : b.Typ == "stationaer" ? "Hergestellt → Wochenprobe dieser KW gezogen." : "Hergestellt-Haken ohne Wirkung auf die Proben (Typ weder mobil noch stationär).") + "\nUnabhängig vom WFI-Rundgang.";
                }
                p.Y = y; p.H = BufPad + Math.Max(p.Head.Height, BufMade ? bs + 2 : 0) + (p.Body != null ? p.Body.Height : 0) + BufPad;
                c.BufParts.Add(p);
                y += p.H + BufGap;
            }
            y -= BufGap;
            if (c.Editing) y += 2 + SmallFont.Height;   // Hinweis unter den Puffern
            c.BufH = y + TV + 1;                          // + Trennlinie
        }

        void PaintBuffers(Graphics g, CellData c, Color back)
        {
            Theme th = Theme.Current;
            Fill(g, back, c.RBuf);
            int x = c.RBuf.X + StripeW + 3;
            for (int i = 0; i < c.BufParts.Count; i++)
            {
                BufPart p = c.BufParts[i];
                int y = c.RBuf.Y + p.Y;
                Fill(g, BufStyle.Tint(i), new Rectangle(c.RBuf.X, y, c.RBuf.Width, p.H));   // eigener Hintergrundton je Puffer (wechselt)
                Fill(g, TypColor(c.RowIndex, p.Typ), new Rectangle(c.RBuf.X, y, StripeW, p.H));
                if (BufMade) PaintMade(g, c, p, y);
                p.Head.Draw(g, x, y + BufPad);
                if (p.Body != null) p.Body.Draw(g, x + BufIndent, y + BufPad + p.Head.Height);
                if (i < c.BufParts.Count - 1) g.DrawLine(th.Pen(BufStyle.Line, 1), c.RBuf.X + 2, y + p.H + BufGap / 2, c.RBuf.Right - 3, y + p.H + BufGap / 2);   // Trennlinie in der Luecke
            }
            if (c.Editing && c.BufParts.Count > 0)
            {
                BufPart l = c.BufParts[c.BufParts.Count - 1];
                TextRenderer.DrawText(g, "Puffer per Klick bearbeiten", SmallFont, new Rectangle(x, c.RBuf.Y + l.Y + l.H + 2, c.RBuf.Width - x + c.RBuf.X, SmallFont.Height),
                    th.Muted, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            g.DrawLine(th.Pen(th.Separator, 1), c.RBuf.X, c.RBuf.Bottom - 1, c.RBuf.Right, c.RBuf.Bottom - 1);
        }

        // Haken "hergestellt" rechts in der Kopfzeile; hergestellt = Kopf dezent gruen, sonst neutral
        void PaintMade(Graphics g, CellData c, BufPart p, int y)
        {
            Theme th = Theme.Current;
            int bs = th.CheckBox;
            if (p.Made) Fill(g, th.LkzOn, new Rectangle(c.RBuf.X + StripeW, y, c.RBuf.Width - StripeW, BufPad + Math.Max(p.Head.Height, p.Box.Height)));
            Rectangle bx = new Rectangle(c.RBuf.X + p.Box.X, c.RBuf.Y + p.Box.Y, p.Box.Width, p.Box.Height);
            th.DrawCheck(g, new Point(bx.X, bx.Y + 1), p.Made, bs);
            TextRenderer.DrawText(g, "hergestellt", SmallFont, new Rectangle(bx.X + bs + 4, bx.Y, bx.Width - bs - 4, bx.Height), p.Made ? th.DoneText : th.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Index des Puffers, dessen Haken "hergestellt" unter p liegt (-1 = keiner)
        int MadeAt(CellData c, Point p)
        {
            if (!BufMade || Collapsed[c.RowIndex] || !c.RBuf.Contains(p)) return -1;
            for (int i = 0; i < c.BufParts.Count; i++)
            {
                Rectangle r = c.BufParts[i].Box; r.Offset(c.RBuf.X, c.RBuf.Y); r.Inflate(3, 2);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        // Index des Puffers unter p (-1 = keiner)
        int BufAt(CellData c, Point p)
        {
            if (Collapsed[c.RowIndex] || !c.RBuf.Contains(p)) return -1;
            for (int i = 0; i < c.BufParts.Count; i++)
            {
                BufPart b = c.BufParts[i];
                if (p.Y >= c.RBuf.Y + b.Y && p.Y < c.RBuf.Y + b.Y + b.H + BufGap) return i;
            }
            return -1;
        }

#if TESTHOOKS
        // Selbsttest: Punkt in der Zeile des Puffers index (Klick wie mit der Maus)
        public Point MadePoint(CellData c, int index)
        {
            if (index < 0 || index >= c.BufParts.Count) return Point.Empty;
            Rectangle r = c.BufParts[index].Box; return new Point(c.RBuf.X + r.X + 4, c.RBuf.Y + r.Y + 4);
        }
        public Point BufPoint(CellData c, int index)
        {
            if (index < 0 || index >= c.BufParts.Count) return Point.Empty;
            return new Point(c.RBuf.X + 12, c.RBuf.Y + c.BufParts[index].Y + 2);
        }
#endif
    }
}
