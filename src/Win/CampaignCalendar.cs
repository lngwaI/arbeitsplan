using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kampagnen im Monatsueberblick (eigenes, frei skalierbares Fenster): Monate als Zeilen, Tage als Spalten.
    // Daten werden beim Oeffnen einmal berechnet. Klick waehlt eine Kampagne, Ziehen setzt einen Zeitraum (Rueckruf an den Export-Dialog).
    class CampaignCalendar : Form
    {
        class Item
        {
            public EquipExport.CampaignSpan C;
            public DateTime From, To;   // inkl. Vorlauf/Puffer und Nachlauf
            public HashSet<DateTime> Run;
            public Color Col;
            public int Lane;            // Spur, damit sich ueberlappende Kampagnen nicht verdecken
        }

        const int HeadH = 22, LabelW = 76, RowTarget = 44, RowMin = 36, CellMin = 16, MinMonths = 4, MaxMonths = 12;
        static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        readonly List<Item> items = new List<Item>();
        readonly Action<EquipExport.CampaignSpan> onPick;
        readonly Action<DateTime, DateTime> onRange;
        readonly DateTime minMonth, maxMonth;
        readonly Panel scroller = new Panel();
        readonly Grid grid;
        readonly Button prev = new Button(), next = new Button();
        readonly Label title = new Label();
        readonly ToolTip tips = new ToolTip();
        DateTime first;     // erster gezeigter Monat
        int months = MinMonths, rowH = RowTarget, cellW = CellMin;
        Item selected;

        public CampaignCalendar(IDataStore store, List<EquipExport.CampaignSpan> camps, int before, int after, DateTime week, Action<EquipExport.CampaignSpan> onPick, Action<DateTime, DateTime> onRange)
        {
            this.onPick = onPick; this.onRange = onRange;
            Theme th = Theme.Current;
            Text = "Kampagnen-Kalender"; Font = th.UiFont();
            StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(460, 320); ClientSize = new Size(1000, 660);
            BackColor = th.Window;
            first = new DateTime(week.Year, week.Month, 1);
            DateTime lo = week.AddDays(-180), hi = week.AddDays(365);   // Fenster wie die Kampagnenliste des Dialogs
            minMonth = new DateTime(lo.Year, lo.Month, 1); maxMonth = new DateTime(hi.Year, hi.Month, 1);
            Dictionary<string, string> legend = BuildItems(store, camps, before, after);

            Panel top = new Panel(); top.Dock = DockStyle.Top; top.Height = 38;
            prev.Text = "◀"; prev.SetBounds(10, 5, 34, 28); next.Text = "▶"; next.SetBounds(48, 5, 34, 28);
            th.StyleButton(prev); th.StyleButton(next);
            prev.Click += delegate { Go(-1); }; next.Click += delegate { Go(1); };
            title.AutoSize = true; title.Location = new Point(92, 11);
            top.Controls.AddRange(new Control[] { prev, next, title });

            FlowLayoutPanel leg = new FlowLayoutPanel(); leg.Dock = DockStyle.Bottom; leg.AutoSize = true; leg.AutoSizeMode = AutoSizeMode.GrowAndShrink; leg.Padding = new Padding(8, 4, 8, 4);
            foreach (Item it in items) { string k = Key(it.C); if (legend.ContainsKey(k)) { AddLegend(leg, it.Col, legend[k]); legend.Remove(k); } }
            Label hint = new Label(); hint.AutoSize = true; hint.ForeColor = th.HeadTextCollapsed; hint.Margin = new Padding(14, 3, 3, 3);
            hint.Text = items.Count == 0 ? "Keine Kampagnen im Plan gefunden." : "kräftig = Lauf, hell = Puffer/Vorlauf · Klick wählt die Kampagne, Ziehen setzt den Zeitraum";
            leg.Controls.Add(hint);

            grid = new Grid(this);
            scroller.Dock = DockStyle.Fill; scroller.AutoScroll = true; scroller.Controls.Add(grid);
            scroller.Resize += delegate { Fit(); };
            Controls.Add(scroller); Controls.Add(leg); Controls.Add(top);   // Fill zuerst: Reihenfolge fuers Andocken
            Go(0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }

        static string Key(EquipExport.CampaignSpan c) { return c.ProductCode.Length > 0 ? c.ProductCode : c.Prefix; }

        // Items mit Spur und Farbe; Rueckgabe: Produkt-Schluessel -> Legendentext
        Dictionary<string, string> BuildItems(IDataStore store, List<EquipExport.CampaignSpan> camps, int before, int after)
        {
            Dictionary<string, string> texts = new Dictionary<string, string>();
            List<DateTime> laneEnd = new List<DateTime>();
            foreach (EquipExport.CampaignSpan c in camps)
            {
                Item it = new Item(); it.C = c; it.Run = new HashSet<DateTime>(c.RunDays);
                EquipExport.CountCampaign(store, c, before, after, out it.From, out it.To);   // liefert auch Puffer mit Lauf-Angabe vor dem Vorlauf
                items.Add(it);
                texts[Key(c)] = c.ProductText;
            }
            items.Sort(delegate(Item a, Item b) { return a.From.CompareTo(b.From); });
            foreach (Item it in items)
            {
                int l = laneEnd.FindIndex(delegate(DateTime e) { return e < it.From; });
                if (l < 0) { l = laneEnd.Count; laneEnd.Add(it.To); } else laneEnd[l] = it.To;
                it.Lane = l;
            }
            // Feste Farbe je Produkt: Hash in die Palette; Kollisionen weichen aus, solange Farben frei sind
            Color[] pal = Theme.Current.CalPalette;
            List<string> keys = new List<string>(texts.Keys); keys.Sort(StringComparer.Ordinal);
            Dictionary<string, Color> col = new Dictionary<string, Color>(); HashSet<int> used = new HashSet<int>();
            foreach (string k in keys)
            {
                int i = RunNumbers.ProductIndex(k, pal.Length);
                while (used.Contains(i) && used.Count < pal.Length) i = (i + 1) % pal.Length;
                used.Add(i); col[k] = pal[i];
            }
            foreach (Item it in items) it.Col = col[Key(it.C)];
            return texts;
        }

        static void AddLegend(FlowLayoutPanel p, Color c, string text)
        {
            Panel sw = new Panel(); sw.Size = new Size(14, 14); sw.BackColor = c; sw.Margin = new Padding(6, 5, 2, 3);
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Margin = new Padding(0, 3, 8, 3);
            p.Controls.Add(sw); p.Controls.Add(l);
        }

        void Go(int delta)
        {
            DateTime m = first.AddMonths(delta);
            if (m < minMonth) m = minMonth; else if (m > maxMonth) m = maxMonth;
            first = m;
            prev.Enabled = first > minMonth; next.Enabled = first < maxMonth;
            Fit();
        }

        // Mehr Platz: mehr Monate und groessere Zellen; zu wenig Platz: Bildlauf (AutoScroll)
        void Fit()
        {
            Size v = scroller.ClientSize;
            if (v.Width <= 0 || v.Height <= 0) return;
            int avail = v.Height - HeadH;
            months = Math.Max(MinMonths, Math.Min(MaxMonths, avail / RowTarget));
            rowH = Math.Max(RowMin, avail / months);
            cellW = Math.Max(CellMin, (v.Width - LabelW) / 31);
            grid.Size = new Size(Math.Max(v.Width, LabelW + 31 * CellMin), Math.Max(v.Height, HeadH + months * RowMin));
            title.Text = first.ToString("MMMM yyyy", De) + " – " + first.AddMonths(months - 1).ToString("MMMM yyyy", De);
            grid.Invalidate();
        }

        // ---- Lage ----
        DateTime? DayAt(Point p)
        {
            if (p.X < LabelW || p.Y < HeadH) return null;
            int row = (p.Y - HeadH) / rowH, col = (p.X - LabelW) / cellW;
            if (row >= months || col >= 31) return null;
            DateTime m = first.AddMonths(row);
            return col < DateTime.DaysInMonth(m.Year, m.Month) ? (DateTime?)m.AddDays(col) : null;
        }

        int LanesIn(DateTime month)
        {
            DateTime end = month.AddMonths(1); int n = 1;
            foreach (Item it in items) if (it.From < end && it.To >= month && it.Lane >= n) n = it.Lane + 1;
            return n;
        }

        // Band einer Spur innerhalb der Zelle (oben bleibt Platz fuer den Wochentag)
        Rectangle Band(int row, int day, int lane, int lanes)
        {
            int top = HeadH + row * rowH + (ShowWeekday ? 15 : 3), h = (rowH - (ShowWeekday ? 18 : 6)) / lanes;
            return new Rectangle(LabelW + (day - 1) * cellW, top + lane * h, cellW, Math.Max(2, h - 1));
        }

        bool ShowWeekday { get { return cellW >= 20 && rowH >= 40; } }

        Item ItemAt(Point p)
        {
            DateTime? d = DayAt(p);
            if (d == null) return null;
            DateTime m = new DateTime(d.Value.Year, d.Value.Month, 1);
            int row = (p.Y - HeadH) / rowH, lanes = LanesIn(m);
            Item any = null;
            foreach (Item it in items)
                if (it.From <= d.Value && it.To >= d.Value)
                {
                    if (Band(row, d.Value.Day, it.Lane, lanes).Contains(p)) return it;
                    if (any == null) any = it;   // Klick neben das Band trifft die Kampagne des Tages
                }
            return any;
        }

        // ---- Maus ----
        DateTime? dragA, dragB;
        bool dragging;
        Point downAt;
        Item hover;

        void Down(Point p) { dragA = DayAt(p); dragB = dragA; dragging = false; downAt = p; }

        void Move_(Point p, bool pressed)
        {
            if (pressed && dragA != null)
            {
                DateTime? d = DayAt(p);
                if (d != null && d != dragB) { dragB = d; dragging = dragB != dragA; grid.Invalidate(); }
                return;
            }
            Item it = ItemAt(p);
            if (it != hover) { hover = it; tips.SetToolTip(grid, it != null ? it.C.Label : ""); }
        }

        void Up(Point p)
        {
            bool drag = dragging; DateTime? a = dragA, b = dragB;
            dragA = dragB = null; dragging = false;
            if (drag && a != null && b != null)
            {
                selected = null; grid.Invalidate();
                onRange(a.Value < b.Value ? a.Value : b.Value, a.Value < b.Value ? b.Value : a.Value);
                return;
            }
            Item it = ItemAt(downAt);
            if (it != null) { selected = it; grid.Invalidate(); onPick(it.C); }
        }

        // ---- Zeichnen ----
        void Draw(Graphics g)
        {
            Theme th = Theme.Current;
            for (int d = 1; d <= 31; d++)
                TextRenderer.DrawText(g, d.ToString(), Font, new Rectangle(LabelW + (d - 1) * cellW, 0, cellW, HeadH), th.DayDate, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            DateTime lo = DateTime.MaxValue, hi = DateTime.MinValue;
            if (dragging) { lo = dragA.Value < dragB.Value ? dragA.Value : dragB.Value; hi = dragA.Value < dragB.Value ? dragB.Value : dragA.Value; }
            for (int r = 0; r < months; r++)
            {
                DateTime m = first.AddMonths(r);
                int y = HeadH + r * rowH, days = DateTime.DaysInMonth(m.Year, m.Month), lanes = LanesIn(m);
                TextRenderer.DrawText(g, m.ToString("MMM yyyy", De), Font, new Rectangle(4, y, LabelW - 6, rowH), th.HeadText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                for (int d = 1; d <= days; d++)
                {
                    DateTime day = m.AddDays(d - 1);
                    Rectangle c = new Rectangle(LabelW + (d - 1) * cellW, y, cellW, rowH);
                    g.FillRectangle(th.Brush(th.CellBackFor(day)), c);
                    g.DrawRectangle(th.Pen(th.CellBorder, 1), c);
                    if (ShowWeekday) TextRenderer.DrawText(g, day.ToString("ddd", De).Substring(0, 2), Font, new Rectangle(c.X, c.Y + 1, c.Width, 14), th.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                }
                foreach (Item it in items)
                {
                    if (it.To < m || it.From >= m.AddMonths(1)) continue;
                    for (DateTime day = it.From < m ? m : it.From; day <= it.To && day.Month == m.Month; day = day.AddDays(1))
                    {
                        Rectangle b = Band(r, day.Day, it.Lane, lanes);
                        g.FillRectangle(th.Brush(it.Run.Contains(day) ? it.Col : th.CalSoft(it.Col)), b);
                        if (it == selected) { g.DrawLine(th.Pen(th.BorderSel, 2), b.Left, b.Top + 1, b.Right, b.Top + 1); g.DrawLine(th.Pen(th.BorderSel, 2), b.Left, b.Bottom - 1, b.Right, b.Bottom - 1); }
                    }
                }
                for (int d = 1; d <= days; d++)
                {
                    DateTime day = m.AddDays(d - 1);
                    Rectangle c = new Rectangle(LabelW + (d - 1) * cellW, y, cellW, rowH);
                    if (day >= lo && day <= hi) g.FillRectangle(th.Brush(Color.FromArgb(80, th.BorderSel)), c);
                    if (day == DateTime.Today) g.DrawRectangle(th.Pen(th.DayLineToday, 2), c.X + 1, c.Y + 1, c.Width - 2, c.Height - 2);
                }
            }
        }

        // Zeichenflaeche im Bildlauf-Panel
        class Grid : Control
        {
            readonly CampaignCalendar cal;
            public Grid(CampaignCalendar cal)
            {
                this.cal = cal;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Current.Window;
            }
#if TESTHOOKS
            public bool Buffered { get { return GetStyle(ControlStyles.OptimizedDoubleBuffer); } }
#endif
            protected override void OnPaint(PaintEventArgs e) { cal.Draw(e.Graphics); }
            protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) cal.Down(e.Location); }
            protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); cal.Move_(e.Location, e.Button == MouseButtons.Left); }
            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) cal.Up(e.Location); }
        }

#if TESTHOOKS
        Point PointOf(DateTime d)
        {
            int row = (d.Year - first.Year) * 12 + d.Month - first.Month;
            return new Point(LabelW + (d.Day - 1) * cellW + cellW / 2, HeadH + row * rowH + rowH / 2);
        }
        public void TestClick(DateTime d) { Point p = PointOf(d); Down(p); Up(p); }
        public void TestDrag(DateTime a, DateTime b) { Down(PointOf(a)); Move_(PointOf(b), true); Up(PointOf(b)); }
        public void TestGo(int delta) { Go(delta); }
        public DateTime TestFirst { get { return first; } }
        public string TestTip(DateTime d) { Item it = ItemAt(PointOf(d)); return it != null ? it.C.Label : ""; }
        public Size TestCell { get { return new Size(cellW, rowH); } }
        public int TestMonths { get { return months; } }
        public bool TestBuffered { get { return grid.Buffered; } }
        public bool TestSoftAt(DateTime d) { foreach (Item it in items) if (it.From <= d && it.To >= d) return !it.Run.Contains(d); return false; }
#endif
    }
}
