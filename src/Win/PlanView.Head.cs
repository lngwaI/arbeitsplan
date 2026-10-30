using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Zeilenkopf (Raumname gross/fett, Akzent, untere Kante, Hinweis "KW offen"/dringend), Produktbalken, Tagesspalten-Hintergrund.
    partial class PlanView
    {
        Font headFont;
        Font HeadFont { get { if (headFont == null) headFont = new Font(Font.FontFamily, Theme.Current.HeadFontSize, FontStyle.Bold); return headFont; } }

        static bool Weekend(DateTime d) { return d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday; }

        // Tagesspalten: Wochenende und heute dezent getoent, feine senkrechte Linie mitten im Spaltenabstand.
        void PaintBands(Graphics g, Rectangle clip)
        {
            Theme th = Theme.Current;
            int top = Pad + (roundsH > 0 ? roundsH + Gap : 0) - Gap / 2, h = Height - top - Pad / 2;
            for (int col = 0; col < 7; col++)
            {
                DateTime d = Start.AddDays(col);
                int x = ColLeft(col);
                Color b = d.Date == DateTime.Today ? th.ColToday : Weekend(d) ? th.ColWeekend : Color.Empty;
                if (!b.IsEmpty) Fill(g, b, new Rectangle(x - Gap / 2 + 1, top, ColW + Gap - 2, h));
                if (col > 0) g.DrawLine(th.Pen(th.ColLine, 1), x - Gap / 2, top, x - Gap / 2, top + h);
            }
        }

        void PaintHead(Graphics g, CellData c, bool collapsed)
        {
            Theme th = Theme.Current;
            RowInfo ri = Rows[c.RowIndex];
            Color hb = c.Urgent ? th.UrgentBack : c.Day.Date == DateTime.Today ? th.RowHeadToday : Weekend(c.Day) ? th.PlanHeadWeekend : th.PlanHead;
            th.FillHead(g, c.RHead, hb);
            Fill(g, c.Urgent ? th.Urgent : th.HeadEdge, new Rectangle(c.R.X + 1, c.RHead.Bottom - 1, c.R.Width - 2, 2));   // klare untere Kante

            // Pfeil (zu = nach rechts, offen = nach unten) am rechten Rand; Klick auf den Kopf klappt die Zeile um
            int ax = c.RHead.Right - 15, ay = c.RHead.Y + (c.RHead.Height - 5) / 2;
            g.FillPolygon(th.Brush(th.Arrow), collapsed ? new Point[] { new Point(ax + 2, ay - 1), new Point(ax + 2, ay + 6), new Point(ax + 6, ay + 3) }
                                                        : new Point[] { new Point(ax, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 5) });

            int right = 18 + (c.Editing ? 20 : 0);                                   // Platz fuer Pfeil (+ Stift im Bearbeitungsmodus)
            int mk = ProductBars(g, c);
            Rectangle nr = new Rectangle(c.RHead.X + 6 + mk, c.RHead.Y, c.RHead.Width - 8 - right - mk, c.RHead.Height - 1);
            bool filled = collapsed && (!c.Cur.IsBlank || c.Conts.Count > 0);
            if (filled)   // zugeklappt, aber Inhalt vorhanden: Hinweispunkt vor dem Pfeil
            {
                g.FillEllipse(th.Brush(th.Dot), c.RHead.Right - 29, c.RHead.Y + (c.RHead.Height - 7) / 2, 8, 8);
                nr.Width -= 12;
            }
            else if (collapsed && c.Cur.HasNotes)   // zugeklappt, nur Notizen: kleiner grauer Hohlpunkt
            {
                g.DrawEllipse(th.Pen(th.Muted, 1), c.RHead.Right - 28, c.RHead.Y + (c.RHead.Height - 6) / 2, 6, 6);
                nr.Width -= 12;
            }
            nr.Width -= PaintNote(g, c, nr);   // Hinweis "KW offen" / dringend, auch zugeklappt
            TextRenderer.DrawText(g, ri.Name, HeadFont, nr, collapsed ? th.HeadTextCollapsed : th.HeadText,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (!collapsed && c.Editing && Pencil != null) g.DrawImage(Pencil, c.RHead.Right - 38, c.RHead.Y + (c.RHead.Height - 17) / 2, 18, 18);
        }

        // Hinweis rechts im Kopf; Raumname hat Vorrang (orange "offen" wird kuerzer), "dringend" (rot, fett) hat Vorrang vor dem Namen.
        // Rueckgabe = belegte Breite.
        int PaintNote(Graphics g, CellData c, Rectangle nr)
        {
            if (c.Note.Length == 0) return 0;
            Font f = c.Urgent ? bold : Font;
            Color col = c.Urgent ? Theme.Current.Urgent : Theme.Current.Dot;
            int room = nr.Width - TextW(Rows[c.RowIndex].Name, HeadFont) - 6, cut = c.Note.IndexOf(" – ");
            string[] tries = c.Urgent ? new[] { c.Note, cut > 0 ? c.Note.Substring(0, cut) : c.Note, "NICHT GEMESSEN", "LKZ!" } : new[] { c.Note, "offen" };
            string t = null;
            foreach (string x in tries) if (TextW(x, f) <= room) { t = x; break; }
            if (t == null)   // zu schmal: Plakette "!" (Text im Tooltip, Kopf bei "dringend" ohnehin rot)
            {
                int d = 15, y = nr.Y + (nr.Height - d) / 2;
                System.Drawing.Drawing2D.SmoothingMode sm = g.SmoothingMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(Theme.Current.Brush(col), nr.Right - d, y, d, d);
                g.SmoothingMode = sm;
                TextRenderer.DrawText(g, "!", bold, new Rectangle(nr.Right - d, y, d, d), Color.White, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return d + 4;
            }
            int w = TextW(t, f);
            TextRenderer.DrawText(g, t, f, new Rectangle(nr.Right - w, nr.Y, w, nr.Height), col, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            return w + 6;
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
            string txt = c.Editing ? c.Content : Notes.ContentText(c.Cur);   // Notizzeilen zaehlen nicht
            if (c.RunsFor != txt) { c.Runs = RunNumbers.Find(txt); c.RunsFor = txt; }
            if (c.Runs.Count == 0) return 0;
            List<string> prods = new List<string>();
            foreach (RunRef r in c.Runs) if (!prods.Contains(r.Product) && prods.Count < 3) prods.Add(r.Product);
            int x = c.RHead.X + 1, h = c.RHead.Height - 5;
            foreach (string p in prods) { Fill(g, ProductColor(p), new Rectangle(x, c.RHead.Y + 2, 4, h)); x += 5; }
            return prods.Count * 5 + 1;
        }
    }
}
