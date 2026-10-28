using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kalenderleiste der Kampagne: je Tag eine Spalte mit Raum und Schritt (Tage ohne Schritt schmal, am Ende einige freie Tage
    // als Ziel); der gewaehlte Schritt ist hervorgehoben, Klick waehlt ihn. Ziehen auf einen anderen Tag verschiebt den Schritt
    // (Umschalt = nur diesen Lauf); waehrend des Ziehens ist der Zieltag markiert. Rechtsklick = Menue (PackageForm.Order.cs).
    class PlanStrip : Control
    {
        public class Item { public int Day, Run; public string Title = "", Sub = ""; public bool Missing, Shifted; public PackageStep Step; }
        const int DayW = 118, EmptyW = 24, MaxDays = 370, Spare = 3;
        List<Item> items = new List<Item>();
        readonly List<KeyValuePair<Rectangle, Item>> hit = new List<KeyValuePair<Rectangle, Item>>();
        PackageStep selected;
        Item press;            // Eintrag unter der gedrueckten Maustaste
        Point pressAt;
        int dropDay = -1;      // Zieltag beim Ziehen, -1 = es wird nicht gezogen
        public event Action<PackageStep> StepClicked;
        public event Action<Item, int, bool> ItemMoved;    // Eintrag, Zieltag, nur dieser Lauf
        public event Action<Item, Point> ItemMenu;

        public PlanStrip() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); }

        int HeadH { get { return Font.Height + 6; } }
        int ItemH { get { return Font.Height * 2 + 6; } }
        int Days { get { int m = 0; foreach (Item i in items) m = Math.Max(m, i.Day); return Math.Min(m + (m > 0 ? Spare : 0), MaxDays); } }
        int W(int day) { foreach (Item i in items) if (i.Day == day) return DayW; return EmptyW; }

        int[] Widths() { int[] w = new int[Days]; for (int d = 1; d <= w.Length; d++) w[d - 1] = W(d); return w; }

        // Tag (1-basiert) unter x bei den Spaltenbreiten widths (Tag 1 = widths[0]); links davor = 1, rechts dahinter = letzter Tag
        public static int DayAt(int[] widths, int x)
        {
            int left = 0;
            for (int d = 0; d < widths.Length; d++) { if (x < left + widths[d]) return d + 1; left += widths[d]; }
            return Math.Max(1, widths.Length);
        }

        public void Set(List<Item> list, PackageStep sel)
        {
            items = list; selected = sel;
            int w = 1, most = 0;
            for (int d = 1; d <= Days; d++) { w += W(d); int n = 0; foreach (Item i in items) if (i.Day == d) n++; most = Math.Max(most, n); }
            Size = new Size(Math.Max(w, 40), HeadH + Math.Max(1, most) * (ItemH + 3) + 4);
            Invalidate();
        }

        public int CountOf(PackageStep s) { return items.FindAll(delegate(Item i) { return i.Step == s; }).Count; }

        // x-Position des ersten Eintrags eines Schritts (zum Hinscrollen), -1 = nicht vorhanden
        public int XOf(PackageStep s)
        {
            int x = 0;
            for (int d = 1; d <= Days; d++) { foreach (Item i in items) if (i.Day == d && i.Step == s) return x; x += W(d); }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(Color.White);
            hit.Clear();
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (Font bold = new Font(Font, FontStyle.Bold))
            using (Pen line = new Pen(t.CellBorder))
            using (Pen sel = new Pen(t.BorderSel, 2f))
            using (Pen drop = new Pen(t.BorderEdit, 3f))
            using (SolidBrush selBack = new SolidBrush(t.ButtonOn))
            using (SolidBrush target = new SolidBrush(Color.FromArgb(255, 236, 205)))
            using (SolidBrush back = new SolidBrush(t.CellBackWeekend))
            {
                int x = 0;
                for (int d = 1; d <= Days; d++)
                {
                    int w = W(d);
                    bool any = w == DayW, isDrop = d == dropDay;
                    Rectangle head = new Rectangle(x, 0, w, HeadH);
                    if (isDrop) g.FillRectangle(target, x, 0, w, Height);
                    else if (any) t.FillHead(g, head, t.DayHead); else g.FillRectangle(back, x, 0, w, Height);
                    TextRenderer.DrawText(g, any || isDrop ? "Tag " + d : d.ToString(), any ? bold : Font, head, any || isDrop ? t.HeadText : Section.HelpColor,
                        ff | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    int y = HeadH + 3;
                    foreach (Item i in items)
                    {
                        if (i.Day != d) continue;
                        Rectangle r = new Rectangle(x + 3, y, w - 7, ItemH);
                        bool on = i.Step == selected, moving = press != null && dropDay > 0 && (i == press || (!OnlyRun && i.Step == press.Step));
                        if (on) g.FillRectangle(selBack, r);
                        g.DrawRectangle(on ? sel : line, r);
                        if (moving) { r.Inflate(-1, -1); using (Pen dash = new Pen(t.BorderEdit, 1f)) { dash.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; g.DrawRectangle(dash, r); } }
                        TextRenderer.DrawText(g, (i.Shifted ? "↔ " : "") + i.Title, bold, new Rectangle(r.X + 4, r.Y + 2, r.Width - 6, Font.Height + 1), t.HeadText, ff);
                        TextRenderer.DrawText(g, i.Sub, Font, new Rectangle(r.X + 4, r.Y + 3 + Font.Height, r.Width - 6, Font.Height + 1), i.Missing ? Color.Firebrick : Section.HelpColor, ff);
                        hit.Add(new KeyValuePair<Rectangle, Item>(r, i));
                        y += ItemH + 3;
                    }
                    if (isDrop) g.DrawLine(drop, x + 1, 0, x + 1, Height);   // Einrastmarke am Zieltag
                    g.DrawLine(line, x + w - 1, 0, x + w - 1, Height);
                    x += w;
                }
                if (items.Count == 0) TextRenderer.DrawText(g, "Noch keine Schritte.", Font, new Point(6, 6), Section.HelpColor);
            }
        }

        static bool OnlyRun { get { return (ModifierKeys & Keys.Shift) != 0; } }

        Item ItemAt(Point p) { foreach (KeyValuePair<Rectangle, Item> h in hit) if (h.Key.Contains(p)) return h.Value; return null; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Item it = ItemAt(e.Location);
            if (e.Button == MouseButtons.Right) { if (it != null && ItemMenu != null) ItemMenu(it, e.Location); return; }
            if (e.Button != MouseButtons.Left) return;
            press = it; pressAt = e.Location; dropDay = -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (press == null || (e.Button & MouseButtons.Left) == 0) return;
            if (dropDay < 0 && Math.Abs(e.X - pressAt.X) < SystemInformation.DragSize.Width) return;
            Capture = true; Cursor = Cursors.SizeWE;
            KeepVisible(e.X);
            int d = DayAt(Widths(), e.X);
            if (d != dropDay) { dropDay = d; Invalidate(); }
        }

        // Beim Ziehen an den Rand des sichtbaren Bereichs: weiterblaettern
        void KeepVisible(int x)
        {
            ScrollableControl p = Parent as ScrollableControl;
            if (p == null) return;
            int left = -p.AutoScrollPosition.X, right = left + p.ClientSize.Width;
            if (x > right - 20) p.AutoScrollPosition = new Point(left + 30, -p.AutoScrollPosition.Y);
            else if (x < left + 20 && left > 0) p.AutoScrollPosition = new Point(Math.Max(0, left - 30), -p.AutoScrollPosition.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Item it = press; int d = dropDay;
            press = null; dropDay = -1; Capture = false; Cursor = Cursors.Default;
            if (it == null || e.Button != MouseButtons.Left) return;
            if (d < 0) { if (StepClicked != null) StepClicked(it.Step); return; }   // nur geklickt
            Invalidate();
            if (d != it.Day && ItemMoved != null) ItemMoved(it, d, OnlyRun);
            else if (StepClicked != null) StepClicked(it.Step);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && dropDay > 0) { press = null; dropDay = -1; Cursor = Cursors.Default; Invalidate(); }   // z. B. Esc/Fensterwechsel: abbrechen
        }
    }
}
