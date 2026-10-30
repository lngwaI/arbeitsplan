using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // UX-5b: Zeilen umsortieren. Jede EquipRow hat links einen Griff (ziehen, Einfuegelinie) und ▲▼-Knoepfe, die nur bei Hover oder Fokus
    // erscheinen. RowReorder gehoert zum Container der Zeilen (Karte bzw. Equipment-Liste) und rechnet Ziel und Einfuegelinie aus; die
    // eigentliche Aenderung macht move(from, to) im Modell (to = Index nach dem Verschieben).
    class RowReorder : IDisposable
    {
        readonly Control host, lineParent;      // Zeilen liegen im host; die Einfuegelinie im lineParent (Standard: host)
        readonly Func<int> count;
        readonly Func<int, Rectangle> bounds;   // Bereich der Zeile i im Container (inkl. Zusatzzeilen darunter)
        readonly Action<int, int> move;
        readonly Panel line = new Panel();
        readonly Timer poll = new Timer();
        readonly List<EquipRow> rows = new List<EquipRow>();
        EquipRow dragging;
        int target = -1;
        public Func<int, int> Lo, Hi;           // erlaubter Bereich fuer das Ziel je Quellzeile (Standard: ganze Liste)

        public RowReorder(Control host, Func<int> count, Func<int, Rectangle> bounds, Action<int, int> move, Control lineParent = null)
        {
            this.host = host; this.lineParent = lineParent ?? host; this.count = count; this.bounds = bounds; this.move = move;
            line.BackColor = Color.FromArgb(0, 120, 215); line.Height = 2; line.Visible = false; line.Enabled = false;
            this.lineParent.Controls.Add(line);
            poll.Interval = 100; poll.Tick += delegate { Poll(); }; poll.Start();
            host.Disposed += delegate { Dispose(); };
        }

        public void Add(EquipRow r) { rows.Add(r); r.Reorder = this; }

        public void Clear() { rows.Clear(); dragging = null; line.Visible = false; }

        // Hover: Zeile unter dem Mauszeiger (oder die gezogene) bekommt die Knoepfe
        void Poll()
        {
            if (!host.IsHandleCreated || !host.Visible) return;
            Point cp = Cursor.Position;
            foreach (EquipRow r in rows)
            {
                if (r.IsDisposed) continue;
                bool inside = r.Visible && r.RectangleToScreen(r.ClientRectangle).Contains(cp);
#if TESTHOOKS
                inside |= r.PinHot;
#endif
                r.SetHot(inside || r == dragging);
            }
        }

        int Lower(int from) { int v = Lo != null ? Lo(from) : 0; return Math.Max(0, v); }
        int Upper(int from) { int v = Hi != null ? Hi(from) : count() - 1; return Math.Min(count() - 1, v); }

        // Ein Schritt per Knopf; false = am Rand der Gruppe
        public bool Step(EquipRow r, int d)
        {
            int from = r.Slot, to = from + d;
            if (from < 0 || to < Lower(from) || to > Upper(from)) return false;
            move(from, to);
            return true;
        }

        public bool CanStep(int slot, int d) { int to = slot + d; return slot >= 0 && to >= Lower(slot) && to <= Upper(slot); }

        // Ziel (Index nach dem Verschieben) fuer die Mausposition y im Container
        public int TargetAt(int from, int y)
        {
            int b = 0;
            for (int i = 0; i < count(); i++) { Rectangle r = bounds(i); if (r.Top + r.Height / 2 < y) b = i + 1; }
            int to = b > from ? b - 1 : b;
            return Math.Max(Lower(from), Math.Min(Upper(from), to));
        }

        public void DragStart(EquipRow r) { dragging = r; target = r.Slot; }

        public void DragMove(EquipRow r) { DragTo(r, host.PointToClient(Cursor.Position).Y); }

        // Ziel zur Mausposition y (Container) bestimmen und die Einfuegelinie dort zeigen
        public void DragTo(EquipRow r, int y)
        {
            if (dragging != r) return;
            int from = r.Slot;
            target = TargetAt(from, y);
            if (target == from) { line.Visible = false; return; }
            Rectangle tb = bounds(target);
            Point o = lineParent == host ? Point.Empty : lineParent.PointToClient(host.PointToScreen(Point.Empty));
            line.SetBounds(o.X + r.Left, o.Y + (target > from ? tb.Bottom : tb.Top) - 1, r.Width, 2);
            line.Visible = true; line.BringToFront();
        }

        public void DragEnd(EquipRow r, bool commit)
        {
            if (dragging != r) return;
            int from = r.Slot, to = target;
            dragging = null; line.Visible = false; target = -1;
            r.SetHot(false);
            if (commit && to >= 0 && to != from) move(from, to);
        }

#if TESTHOOKS
        internal bool LineVisible { get { return line.Visible; } }
        internal Rectangle LineBounds { get { return line.Bounds; } }
        internal int TestTarget { get { return target; } }
#endif

        public void Dispose() { poll.Stop(); poll.Dispose(); }
    }

    // Griff zum Ziehen (zwei Spalten Punkte); erscheint nur, wenn die Zeile verschiebbar ist
    class RowGrip : Control
    {
        public bool Hot;
        readonly EquipRow row;
        Point down; bool pressed, moved;

        public RowGrip(EquipRow row)
        {
            this.row = row;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.SizeAll; TabStop = false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || row.Reorder == null) return;
            pressed = true; moved = false; down = Cursor.Position; row.Reorder.DragStart(row);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed) return;
            Point p = Cursor.Position;
            if (!moved && Math.Abs(p.Y - down.Y) < 4) return;
            moved = true; row.Reorder.DragMove(row);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!pressed) return;
            pressed = false;
            row.Reorder.DragEnd(row, moved && e.Button == MouseButtons.Left);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(BackColor);
            Color c = Hot ? Color.FromArgb(90, 96, 106) : Color.FromArgb(170, 176, 186);
            int cx = Width / 2, cy = Height / 2;
            using (Brush b = new SolidBrush(c))
                for (int r = -1; r <= 1; r++) for (int k = 0; k < 2; k++) g.FillRectangle(b, cx - 3 + k * 4, cy + r * 5 - 1, 2, 2);
        }
    }
}
