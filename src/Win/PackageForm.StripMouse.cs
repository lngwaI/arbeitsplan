using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Maus im Kalender (PlanStrip): Klick waehlt, Ziehen auf einen anderen Tag verschiebt nur diesen Block (Umschalt = dieser und alle folgenden), Rechtsklick = Menue.
    partial class PlanStrip
    {
        Item press;            // Eintrag unter der gedrueckten Maustaste
        Point pressAt;
        int dropDay = -1;      // Zieltag beim Ziehen, -1 = es wird nicht gezogen

        // Ziehen = nur dieser Block, Umschalt = dieser und alle folgenden
        static bool OnlyRun { get { return (ModifierKeys & Keys.Shift) == 0; } }

        Item ItemAt(Point p)
        {
            if (p.X < rowW || p.Y < HeadH || p.X >= rowW + ViewW || p.Y >= HeadH + ViewH) return null;
            Point c = new Point(p.X - rowW + sx, p.Y - HeadH + sy);
            foreach (KeyValuePair<Rectangle, Item> h in cells) if (h.Key.Contains(c)) return h.Value;
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Item it = ItemAt(e.Location);
            if (hover != null) { hover = null; tt.Hide(this); }
            if (e.Button == MouseButtons.Right) { if (it != null && ItemMenu != null) ItemMenu(it, e.Location); return; }
            if (e.Button != MouseButtons.Left) return;
            press = it; pressAt = e.Location; dropDay = -1;
            if (it != null && CanFocus) Focus();   // Esc bricht das Ziehen ab (ProcessCmdKey)
        }

        // N7: Ziehen abbrechen (Esc, Fokus-/Mausverlust): nichts wird verschoben
        internal void CancelDrag()
        {
            bool was = press != null;
            press = null; dropDay = -1; Cursor = Cursors.Default;
            if (Capture) Capture = false;
            if (was) Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && press != null) { CancelDrag(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (press == null || (e.Button & MouseButtons.Left) == 0) { if (press == null) TipMove(e.Location); return; }
            if (dropDay < 0 && Math.Abs(e.X - pressAt.X) < SystemInformation.DragSize.Width) return;
            if (hover != null) { hover = null; tt.Hide(this); }
            Capture = true; Cursor = Cursors.SizeWE;
            if (e.X > ClientSize.Width - 20) SetX(sx + 30);   // am Rand weiterblaettern
            else if (e.X < rowW + 20) SetX(sx - 30);
            int d = DayAt(widths, e.X - rowW + sx);
            if (d != dropDay) { dropDay = d; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Item it = press; int d = dropDay;
            press = null; dropDay = -1; Capture = false; Cursor = Cursors.Default;
            if (it == null || e.Button != MouseButtons.Left) return;
            Invalidate();
            if (d > 0 && d != it.Day) { if (ItemMoved != null) ItemMoved(it, d, OnlyRun); }
            else if (ItemClicked != null) ItemClicked(it);   // nur geklickt (oder auf denselben Tag gezogen)
        }

#if TESTHOOKS
        internal void TestDrag(Item it, int day) { press = it; dropDay = day; }
        internal bool TestDragging { get { return press != null || dropDay > 0; } }
        internal bool TestEsc() { Message m = new Message(); return ProcessCmdKey(ref m, Keys.Escape); }
        internal List<Item> TestItems { get { return all; } }
#endif

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && dropDay > 0) CancelDrag();   // z. B. Esc/Fensterwechsel: abbrechen
        }
    }
}
