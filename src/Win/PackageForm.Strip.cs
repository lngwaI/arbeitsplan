using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kalender der Kampagne als Mini-Arbeitsplan ohne Details: links je Raum eine Zeile (Reihenfolge wie im Plan, Raeume ohne Schritt
    // ausgeblendet), Spalten = Tage (Kopf: Datum bzw. "3 Tage vor Lauf 1"), je Schritt ein Kaestchen (Name + Laufnummer) in Raum und Tag.
    // Kopf und Raumnamen bleiben beim Scrollen stehen. Der gewaehlte Schritt ist blau (sein gewaehlter Lauf kraeftig umrandet), Klick waehlt,
    // Ziehen auf einen anderen Tag verschiebt diesen und alle folgenden Laeufe (Umschalt = nur dieser Lauf), von Hand verschobene Laeufe
    // tragen ein Schloss, Rechtsklick = Menue (Maus: PackageForm.StripMouse.cs; Zoom, "nur Auswahl", Tooltip: PackageForm.Zoom.cs).
    partial class PlanStrip : Control
    {
        public class Item { public int Day, Run; public string Title = "", Sub = "", Row = ""; public bool Shifted, Je; public PackageStep Step; }   // Shifted = fest verschoben (Schloss), Je = folgt dem Hauptlauf (Ziehen verschiebt alle)
        class RowL { public string Name = ""; public int Y, H; public bool Missing; }
        const int EmptyW = 24, MaxDays = 370, Spare = 3, Wheel = 40;
        List<Item> all = new List<Item>(), items = new List<Item>();   // all = alle Eintraege, items = angezeigte ("nur Auswahl")
        List<string> order = new List<string>();                  // Raeume des Plans in Planreihenfolge
        readonly List<RowL> rowsL = new List<RowL>();
        readonly List<KeyValuePair<Rectangle, Item>> cells = new List<KeyValuePair<Rectangle, Item>>();   // Kaestchen (Inhaltskoordinaten)
        int[] widths = new int[0], xs = new int[0];                // Breite und Start je Tag (Index = Tag - 1)
        int rowW = 64, contentW, contentH, sx, sy;
        readonly HScrollBar hb = new HScrollBar();
        readonly VScrollBar vb = new VScrollBar();
        PackageStep selected; int selRun;
        public Func<int, bool, string> HeadText;           // Kopf je Tag (Tag, breite Spalte): Datum bzw. Bezug zu Lauf 1, nie "Tag n"
        public event Action<Item> ItemClicked;
        public event Action<Item, int, bool> ItemMoved;    // Eintrag, Zieltag, nur dieser Lauf
        public event Action<Item, Point> ItemMenu;

        public PlanStrip()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            hb.Visible = vb.Visible = false; hb.SmallChange = vb.SmallChange = Wheel;
            hb.ValueChanged += delegate { sx = hb.Value; Invalidate(); };
            vb.ValueChanged += delegate { sy = vb.Value; Invalidate(); };
            Controls.Add(hb); Controls.Add(vb);
        }

        int HeadH { get { return Font.Height + 6; } }
        int ItemH { get { return Font.Height * 2 + 6; } }
        int ViewW { get { return Math.Max(0, ClientSize.Width - rowW - (vb.Visible ? vb.Width : 0)); } }
        int ViewH { get { return Math.Max(0, ClientSize.Height - HeadH - (hb.Visible ? hb.Height : 0)); } }

        // Tag (1-basiert) unter x bei den Spaltenbreiten widths (Tag 1 = widths[0]); links davor = 1, rechts dahinter = letzter Tag
        public static int DayAt(int[] widths, int x)
        {
            int left = 0;
            for (int d = 0; d < widths.Length; d++) { if (x < left + widths[d]) return d + 1; left += widths[d]; }
            return Math.Max(1, widths.Length);
        }

        public void SetRows(List<string> planOrder) { order = planOrder; }

        public void Set(List<Item> list, PackageStep sel, int run) { all = list; selected = sel; selRun = run; Rebuild(); Invalidate(); }

        // Nur die Auswahl wechseln (Blaettern, Klick): kein Neuaufbau
        public void Select(PackageStep s, int run) { selected = s; selRun = run; if (only) Rebuild(); ScrollTo(s, run); Invalidate(); }

        public int CountOf(PackageStep s) { return items.FindAll(delegate(Item i) { return i.Step == s; }).Count; }
        public int SelectedRun { get { return selRun; } }

        // Sichtbare Raumzeilen von oben nach unten (nur Raeume mit Schritten)
        public List<string> RowNames() { List<string> l = new List<string>(); foreach (RowL r in rowsL) l.Add(r.Name); return l; }

        static string LabelOf(RowL r) { return r.Name.Length > 0 ? r.Name : "Raum fehlt"; }

        void Rebuild()
        {
            items = only && selected != null ? all.FindAll(delegate(Item i) { return i.Step == selected; }) : all;
            rowsL.Clear();
            foreach (string n in order) if (n.Length > 0 && items.Exists(delegate(Item i) { return i.Row == n; })) rowsL.Add(new RowL { Name = n });
            foreach (Item i in items) { string n = i.Row; if (!rowsL.Exists(delegate(RowL r) { return r.Name == n; })) rowsL.Add(new RowL { Name = n, Missing = true }); }   // unbekannter/fehlender Raum: ans Ende
            int maxLabel = 0;
            using (Font bold = new Font(Font, FontStyle.Bold))
                foreach (RowL r in rowsL) maxLabel = Math.Max(maxLabel, TextRenderer.MeasureText(LabelOf(r), bold).Width);
            rowW = Math.Max(64, Math.Min(150, maxLabel + 16));
            int nd = 0;
            foreach (Item i in items) nd = Math.Max(nd, i.Day);
            nd = Math.Min(nd + (nd > 0 ? Spare : 0), MaxDays);
            widths = new int[nd]; xs = new int[nd]; contentW = 0; dayW = CalcDayW();
            for (int d = 1; d <= nd; d++)
            {
                widths[d - 1] = items.Exists(delegate(Item i) { return i.Day == d; }) ? dayW : EmptyW;
                xs[d - 1] = contentW; contentW += widths[d - 1];
            }
            cells.Clear(); contentH = 0;
            foreach (RowL r in rowsL)
            {
                int[] count = new int[nd + 1]; int most = 1;
                r.Y = contentH;
                foreach (Item i in items)
                {
                    if (i.Row != r.Name || i.Day < 1 || i.Day > nd) continue;
                    int k = count[i.Day]++; most = Math.Max(most, k + 1);
                    cells.Add(new KeyValuePair<Rectangle, Item>(new Rectangle(xs[i.Day - 1] + 3, r.Y + 3 + k * (ItemH + 3), widths[i.Day - 1] - 7, ItemH), i));
                }
                r.H = most * (ItemH + 3) + 4; contentH += r.H;
            }
            UpdateBars();
        }

        // Bildlaufleisten nur bei Bedarf; Kopf (oben) und Raumnamen (links) sind fest
        void UpdateBars()
        {
            int vw = ClientSize.Width - rowW, vh = ClientSize.Height - HeadH;
            bool h = contentW > vw, v = contentH > vh;
            if (h) v = contentH > vh - hb.Height;
            if (v) h = contentW > vw - vb.Width;
            hb.Visible = h; vb.Visible = v;
            hb.SetBounds(rowW, ClientSize.Height - hb.Height, Math.Max(1, ClientSize.Width - rowW - (v ? vb.Width : 0)), hb.Height);
            vb.SetBounds(ClientSize.Width - vb.Width, HeadH, vb.Width, Math.Max(1, ClientSize.Height - HeadH - (h ? hb.Height : 0)));
            if (h) { hb.Minimum = 0; hb.LargeChange = Math.Max(1, ViewW); hb.Maximum = Math.Max(contentW - 1, hb.LargeChange); }
            if (v) { vb.Minimum = 0; vb.LargeChange = Math.Max(1, ViewH); vb.Maximum = Math.Max(contentH - 1, vb.LargeChange); }
            SetX(h ? sx : 0); SetY(v ? sy : 0);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (CalcDayW() != dayW) Rebuild(); else UpdateBars(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int d = e.Delta > 0 ? -Wheel : Wheel;
            if (vb.Visible) SetY(sy + d); else if (hb.Visible) SetX(sx + d);
        }

        void SetX(int x) { x = Math.Max(0, Math.Min(Math.Max(0, contentW - ViewW), x)); sx = x; if (hb.Visible && hb.Value != x) hb.Value = x; }
        void SetY(int y) { y = Math.Max(0, Math.Min(Math.Max(0, contentH - ViewH), y)); sy = y; if (vb.Visible && vb.Value != y) vb.Value = y; }

        // Kaestchen von (s, run) ins Bild holen (Auswahl aus der Liste, Blaettern)
        public void ScrollTo(PackageStep s, int run)
        {
            foreach (KeyValuePair<Rectangle, Item> c in cells)
            {
                if (c.Value.Step != s || c.Value.Run != run) continue;
                Rectangle r = c.Key;
                if (r.X < sx || r.Right > sx + ViewW) SetX(r.X - 30);
                if (r.Y < sy || r.Bottom > sy + ViewH) SetY(r.Y - 3);
                return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(Color.White);
            int vw = ViewW, vh = ViewH;
            if (vw <= 0 || vh <= 0) return;
            Rectangle body = new Rectangle(rowW, HeadH, vw, vh);
            int ox = rowW - sx, oy = HeadH - sy;
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (Font bold = new Font(Font, FontStyle.Bold))
            using (Pen line = new Pen(t.CellBorder))
            using (Pen sel = new Pen(t.BorderSel, 2f))
            using (Pen drop = new Pen(t.BorderEdit, 3f))
            using (SolidBrush selBack = new SolidBrush(t.ButtonOn))
            using (SolidBrush target = new SolidBrush(Color.FromArgb(255, 236, 205)))
            using (SolidBrush back = new SolidBrush(t.CellBackWeekend))
            {
                g.SetClip(body);
                for (int d = 1; d <= widths.Length; d++)
                {
                    int x = ox + xs[d - 1], w = widths[d - 1];
                    if (x + w < body.X || x > body.Right) continue;
                    if (d == dropDay) g.FillRectangle(target, x, body.Y, w, vh); else if (w <= EmptyW) g.FillRectangle(back, x, body.Y, w, vh);
                    g.DrawLine(line, x + w - 1, body.Y, x + w - 1, body.Bottom);
                }
                foreach (RowL r in rowsL) g.DrawLine(line, body.X, oy + r.Y + r.H - 1, body.Right, oy + r.Y + r.H - 1);
                foreach (KeyValuePair<Rectangle, Item> c in cells)
                {
                    Item i = c.Value; Rectangle r = c.Key; r.Offset(ox, oy);
                    if (!r.IntersectsWith(body)) continue;
                    bool on = i.Step == selected, cur = on && i.Run == selRun, moving = press != null && dropDay > 0 && (i == press || (!OnlyRun && i.Step == press.Step && (press.Je || i.Run >= press.Run)));   // wandert mit
                    if (on) g.FillRectangle(selBack, r);
                    if (cur) g.DrawRectangle(sel, r); else g.DrawRectangle(on ? t.Pen(t.BorderSel, 1) : line, r);
                    if (moving) { r.Inflate(-1, -1); using (Pen dash = new Pen(t.BorderEdit, 1f)) { dash.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; g.DrawRectangle(dash, r); } }
                    TextRenderer.DrawText(g, i.Title, bold, new Rectangle(r.X + 4, r.Y + 2, r.Width - (i.Shifted ? 20 : 6), Font.Height + 1), t.HeadText, ff);
                    if (i.Shifted) DrawLock(g, r.Right - 14, r.Y + 4, t.BorderEdit);
                    TextRenderer.DrawText(g, i.Sub, Font, new Rectangle(r.X + 4, r.Y + 3 + Font.Height, r.Width - 6, Font.Height + 1), Section.HelpColor, ff);
                }
                if (dropDay > 0 && dropDay <= widths.Length) g.DrawLine(drop, ox + xs[dropDay - 1] + 1, body.Y, ox + xs[dropDay - 1] + 1, body.Bottom);   // Einrastmarke am Zieltag
                if (items.Count == 0) TextRenderer.DrawText(g, "Noch keine Schritte.", Font, new Point(body.X + 6, body.Y + 6), Section.HelpColor);
                g.SetClip(new Rectangle(body.X, 0, vw, HeadH));
                for (int d = 1; d <= widths.Length; d++)
                {
                    int x = ox + xs[d - 1], w = widths[d - 1];
                    if (x + w < body.X || x > body.Right) continue;
                    bool any = w > EmptyW, isDrop = d == dropDay;
                    Rectangle head = new Rectangle(x, 0, w, HeadH);
                    if (isDrop) g.FillRectangle(target, head); else if (any) t.FillHead(g, head, t.DayHead); else g.FillRectangle(back, head);
                    TextRenderer.DrawText(g, HeadText != null ? HeadText(d, any || isDrop) : "", any ? bold : Font, head, any || isDrop ? t.HeadText : Section.HelpColor,
                        ff | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    g.DrawLine(line, x + w - 1, 0, x + w - 1, HeadH);
                }
                g.SetClip(new Rectangle(0, HeadH, rowW, vh));
                foreach (RowL r in rowsL)
                {
                    Rectangle lb = new Rectangle(0, oy + r.Y, rowW, r.H);
                    if (lb.Bottom < HeadH || lb.Y > body.Bottom) continue;
                    t.FillHead(g, lb, t.RowHead);
                    TextRenderer.DrawText(g, LabelOf(r), bold, new Rectangle(lb.X + 6, lb.Y + 3, rowW - 10, Font.Height + 2), r.Missing ? Color.Firebrick : t.HeadText, ff);
                    g.DrawRectangle(line, lb.X, lb.Y, lb.Width - 1, lb.Height - 1);
                }
                g.ResetClip();
                t.FillHead(g, new Rectangle(0, 0, rowW, HeadH), t.DayHead);
                g.DrawLine(line, rowW - 1, 0, rowW - 1, HeadH);
                g.DrawLine(line, 0, HeadH - 1, ClientSize.Width, HeadH - 1);
            }
        }
    }
}
