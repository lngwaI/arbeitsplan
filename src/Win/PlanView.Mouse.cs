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
        // ---------- Maus ----------

        CellData CellAt(Point p)
        {
            foreach (CellData c in Cells) if (c.R.Contains(p)) return c;
            return null;
        }

        public int MenuPara = -1;   // Rechtsklick: Zeile (Absatz) des Inhalts unter dem Mauszeiger, -1 = keine
        protected override void OnKeyDown(KeyEventArgs e) { MenuPara = -1; base.OnKeyDown(e); }

        readonly ToolTip tip = new ToolTip();
        string tipText = "";

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            CellData c = CellAt(e.Location);
            CheckSlot s = c != null ? SlotAt(c, e.Location) : null;
            int bi = c != null && s == null ? BufAt(c, e.Location) : -1, mi = c != null && s == null ? MadeAt(c, e.Location) : -1;
            Cursor = bi >= 0 ? Cursors.Hand : Cursors.Default;
            string t = c == null ? RoundTip(e.Location) : s != null ? CheckTip(c, s) : mi >= 0 ? c.BufParts[mi].MadeTip : bi >= 0 ? c.BufParts[bi].Tip
                : c.RHead.Contains(e.Location) ? (c.Runs.Count > 0 ? RunNumbers.Describe(c.Runs) : c.Note) : "";
            if (t != tipText) { tipText = t; tip.SetToolTip(this, t.Length > 0 ? t : null); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (RoundsMouse(e.Location, e.Clicks == 1 ? e.Button : MouseButtons.None)) return;
            CellData c = CellAt(e.Location);
            bool head = c != null && c.RHead.Contains(e.Location);
            MenuPara = e.Button == MouseButtons.Right && c != null && !c.Editing && !Collapsed[c.RowIndex] && c.CB != null && c.RContent.Contains(e.Location)
                ? c.CB.ParaAt(e.Location.Y - (c.RContent.Y + TV)) : -1;   // Textzeile unter dem Mauszeiger (Rechtsklick-Menue "Als Notiz markieren")
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
                int mi = MadeAt(c, e.Location);
                if (mi >= 0) { if (MadeClicked != null) MadeClicked(c, mi); return; }
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
