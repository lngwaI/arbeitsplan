using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Ein angezeigter Puffer: Kopfzeile "Puffer n: Name" und eingerueckter Text; Y/H relativ zum Puffer-Bereich.
    class BufPart { public TextBlock Head, Body; public int Y, H; public string Typ = "", Tip = ""; }

    // Puffer-Bereich eines Blocks (Zeilen mit Puffer-Typen): ueber dem freien Inhalt je Puffer eine Zeile "Puffer 1: Name" mit
    // eingerueckter Beschreibung; der Typ steht nie als Text, nur als 3-px-Seitenstrich links (Farbe aus Theme.BufColors) und im Tooltip.
    partial class PlanView
    {
        public Action<CellData, int> BufferClicked;   // Klick auf Puffer (Index) bzw. auf "+ Puffer" (-1)
        const int StripeW = 3, BufIndent = 10, BufGap = 3;

        Font SmallFont { get { return FontCache.Get(Font.Name, Math.Max(7f, Font.SizeInPoints - 1f), FontStyle.Regular, Font); } }

        static string TypTip(string typ)
        {
            if (typ == "mobil") return "mobil – Tagesprobe mit Herstellung erledigt";
            if (typ == "stationaer") return "stationär – erledigt Wochenprobe der KW";
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
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            foreach (Match m in EquipmentMatcher.Pattern.Matches(text))
            {
                Color col = Rgb(StatusRgb(m.Value));
                for (int i = m.Index; i < m.Index + m.Length; i++) fore[i] = col;
            }
            hits = search ? MarkQuery(text, mode) : 0;
            return Group(text, fore, fnt, mode);
        }

        // Kopfzeile "Puffer n: Name" (Nummer fett; ohne Namen "–" grau), Suchtreffer nur im Namen
        List<Piece> BufferHead(int n, string name, bool search, out int hits)
        {
            string lead = "Puffer " + n + ":", text = lead + " " + (name.Length > 0 ? name : "–");
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < text.Length; i++)
            {
                fore[i] = i > lead.Length && name.Length == 0 ? Theme.Current.Muted : Color.Black;
                fnt[i] = i < lead.Length ? bold : plain;
            }
            hits = 0;
            if (search && name.Length > 0 && Query.Length > 0)
            {
                int[] nm = new int[name.Length];
                hits = MarkQuery(name, nm);
                Array.Copy(nm, 0, mode, lead.Length + 1, nm.Length);
            }
            return Group(text, fore, fnt, mode);
        }

        void BuildBuffers(CellData c, int tw, out int hits)
        {
            hits = 0;
            c.BufParts.Clear(); c.BufH = 0;
            if (c.Buffers.Count == 0) return;
            int aw = tw - StripeW, y = TV, h2;
            for (int i = 0; i < c.Buffers.Count; i++)
            {
                BufferItem b = c.Buffers[i];
                BufPart p = new BufPart(); p.Typ = b.Typ; p.Tip = TypTip(b.Typ) + "\nKlick: Puffer bearbeiten";
                p.Head = TextBlock.Build(BufferHead(i + 1, b.Name, !c.Editing, out h2), Font, aw); hits += h2;
                if (b.Text.Length > 0) { p.Body = TextBlock.Build(BufferPieces(b.Text, !c.Editing, out h2), Font, aw - BufIndent); hits += h2; }
                p.Y = y; p.H = p.Head.Height + (p.Body != null ? p.Body.Height : 0);
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
            foreach (BufPart p in c.BufParts)
            {
                int y = c.RBuf.Y + p.Y;
                Fill(g, TypColor(c.RowIndex, p.Typ), new Rectangle(c.RBuf.X, y, StripeW, p.H));
                p.Head.Draw(g, x, y);
                if (p.Body != null) p.Body.Draw(g, x + BufIndent, y + p.Head.Height);
            }
            if (c.Editing && c.BufParts.Count > 0)
            {
                BufPart l = c.BufParts[c.BufParts.Count - 1];
                TextRenderer.DrawText(g, "Puffer per Klick bearbeiten", SmallFont, new Rectangle(x, c.RBuf.Y + l.Y + l.H + 2, c.RBuf.Width - x + c.RBuf.X, SmallFont.Height),
                    th.Muted, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            g.DrawLine(th.Pen(th.Separator, 1), c.RBuf.X, c.RBuf.Bottom - 1, c.RBuf.Right, c.RBuf.Bottom - 1);
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
        public Point BufPoint(CellData c, int index)
        {
            if (index < 0 || index >= c.BufParts.Count) return Point.Empty;
            return new Point(c.RBuf.X + 12, c.RBuf.Y + c.BufParts[index].Y + 2);
        }
#endif
    }
}
