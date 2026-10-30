using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class PlanView : Control
    {
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
            if (c.RFrz.Height > 0 || c.RSplit.Height > 0) PaintFreeze(g, c, back);   // PlanView.Freeze.cs

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
    }
}
