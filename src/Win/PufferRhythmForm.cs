using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Puffer-Rhythmus: je Puffer-Schritt eine Zeile, je Tag der Kampagne eine Spalte. Klick/Leertaste setzt oder entfernt einen Tag;
    // ein Muster (mob, mob/stat, stat, frei) fuellt bis zum letzten Lauf. Das Fenster aendert nichts am Paket, es liefert nur die Tage (OK).
    class PufferRhythmForm : Form
    {
        public class RRow
        {
            public PackageStep Step;                        // null = Puffer-Schritt wird erst bei OK angelegt
            public string Name = "", Typ = "";
            public bool Mob, Stat, IsNew;
            public HashSet<int> Days = new HashSet<int>(), Orig = new HashSet<int>();
            public bool Changed { get { return IsNew || !Days.SetEquals(Orig); } }
        }

        public readonly List<RRow> Rows;
        readonly RhythmGrid grid = new RhythmGrid();
        readonly TextBox tPat = new TextBox();
        readonly NumericUpDown nFrom = new NumericUpDown();
        readonly Label lMsg = new Label();
        readonly int lastRun; readonly bool canMob, canStat;

        // lastRun = Tag des letzten Laufs, cols = Anzahl Tage (Spalten), head = Spaltenkopf je Tag (Datum bzw. Bezug), runDays = Lauftage
        public PufferRhythmForm(List<RRow> rows, int lastRun, int cols, Func<int, string> head, HashSet<int> runDays, bool canMob, bool canStat)
        {
            Rows = rows; this.lastRun = lastRun; this.canMob = canMob; this.canStat = canStat;
            Text = "Puffer-Rhythmus";
            Ui.Dialog(this, 960, 400, false);
            FormBorderStyle = FormBorderStyle.Sizable; MinimumSize = new Size(Width, Height);
            Label l1 = new Label(); l1.Text = "Muster:"; l1.AutoSize = true; l1.Location = new Point(12, 14);
            tPat.SetBounds(70, 10, 300, 24); Ui.SimpleMenu(tPat);
            Label l2 = new Label(); l2.Text = "ab Tag:"; l2.AutoSize = true; l2.Location = new Point(386, 14);
            nFrom.SetBounds(440, 10, 60, 24); nFrom.Minimum = 1; nFrom.Maximum = Math.Max(1, cols); nFrom.Value = 1;
            Button fill = new Button(); fill.Text = "Einfüllen"; fill.SetBounds(514, 9, 96, 26); fill.Click += delegate { DoFill(); };
            Label hint = Ui.Hint("Einträge mob, stat, mob/stat, frei (auch m, s, f), durch Komma oder Leerzeichen; das Muster wiederholt sich bis zum letzten Lauf (Tag " + lastRun + "). Der erste Puffer je Typ wird ersetzt.", 12, 40, 936);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lMsg.AutoSize = false; lMsg.ForeColor = Color.Firebrick; lMsg.SetBounds(12, 62, 936, 20); lMsg.Anchor = hint.Anchor;
            grid.SetBounds(12, 86, 936, 250); grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.Setup(rows, cols, head, runDays);
            Label legend = Ui.Hint("Klick oder Leertaste setzt/entfernt einen Tag (Pfeiltasten wechseln die Zelle). Getönter Spaltenkopf = Lauftag.", 12, 346, 560);
            legend.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Button ok = new Button(); ok.Text = "OK"; ok.SetBounds(766, 360, 90, 30); ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(862, 360, 90, 30); cancel.Anchor = ok.Anchor; cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = null; CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, tPat, l2, nFrom, fill, hint, lMsg, grid, legend, ok, cancel });
            Theme.Current.Style(this);
        }

        void Msg(string t) { lMsg.Text = t; }

        RRow RowOf(bool mob)
        {
            RRow any = null;
            foreach (RRow r in Rows)
            {
                if (mob ? !r.Mob : !r.Stat) continue;
                if (mob ? !r.Stat : !r.Mob) return r;   // reiner Typ zuerst
                if (any == null) any = r;
            }
            return any;
        }

        RRow NewRow(bool mob)
        {
            RRow r = new RRow(); r.IsNew = true; r.Mob = mob; r.Stat = !mob; r.Name = mob ? "Puffer mobil" : "Puffer stationär"; r.Typ = mob ? "mobil" : "stationär";
            Rows.Add(r); return r;
        }

        void DoFill()
        {
            string err; List<int> m = PufferPattern.Parse(tPat.Text, out err);
            if (m == null) { Msg(err); return; }
            List<int> mob = new List<int>(), stat = new List<int>();
            PufferPattern.Fill(m, (int)nFrom.Value, lastRun, mob, stat);
            if (mob.Count + stat.Count == 0) { Msg("Zwischen Starttag und letztem Lauf (Tag " + lastRun + ") liegt kein Tag des Musters."); return; }
            bool useM = m.Exists(delegate(int v) { return (v & PufferPattern.Mob) != 0; }), useS = m.Exists(delegate(int v) { return (v & PufferPattern.Stat) != 0; });
            RRow rm = useM ? RowOf(true) : null, rs = useS ? RowOf(false) : null;
            if (useM && rm == null && !canMob || useS && rs == null && !canStat) { Msg("Es gibt keinen Baustein „Puffer mobil“ bzw. „Puffer stationär“ im Katalog – Puffer-Schritt zuerst anlegen."); return; }
            if (useM && rm == null) rm = NewRow(true);
            if (useS && rs == null) rs = NewRow(false);
            if (rm != null) rm.Days = new HashSet<int>(mob);
            if (rs != null) { if (rs == rm) rs.Days.UnionWith(stat); else rs.Days = new HashSet<int>(stat); }
            Msg(""); grid.Setup(Rows, -1, null, null);
        }

        void Commit()
        {
            foreach (RRow r in Rows) if (r.Changed && r.Days.Count == 0) { Msg("Mindestens ein Tag je geändertem Puffer – sonst bleibt alles wie es war."); return; }
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        public void TestPattern(string t) { tPat.Text = t; }
        public void TestFrom(int d) { nFrom.Value = d; }
        public void TestFill() { DoFill(); }
        public void TestToggle(int row, int day) { grid.Toggle(row, day); }
        public void TestCommit() { Commit(); }
        public string TestMsg { get { return lMsg.Text; } }
#endif
    }

    // Tabelle: Namensspalte steht fest, die Tage scrollen waagerecht
    sealed class RhythmGrid : Control
    {
        const int CellW = 54, NameW = 200;
        List<PufferRhythmForm.RRow> rows = new List<PufferRhythmForm.RRow>();
        int cols = 1, sx, curR, curC = 1;
        Func<int, string> head = delegate(int d) { return ""; };
        HashSet<int> runDays = new HashSet<int>();
        readonly HScrollBar hb = new HScrollBar();

        public RhythmGrid()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true; hb.Dock = DockStyle.Bottom; hb.Visible = false; hb.SmallChange = CellW;
            hb.ValueChanged += delegate { sx = hb.Value; Invalidate(); };
            Controls.Add(hb);
        }

        // cols < 0 / head null: nur die Zeilen neu zeichnen
        public void Setup(List<PufferRhythmForm.RRow> r, int c, Func<int, string> h, HashSet<int> run)
        {
            rows = r; if (c >= 0) cols = Math.Max(1, c); if (h != null) head = h; if (run != null) runDays = run;
            curR = Math.Min(curR, Math.Max(0, rows.Count - 1));
            UpdateScroll(); Invalidate();
        }

        int HeadH { get { return Font.Height * 3 + 8; } }
        int RowH { get { return Font.Height * 2 + 8; } }
        int ViewW { get { return Math.Max(0, ClientSize.Width - NameW); } }

        void UpdateScroll()
        {
            int content = cols * CellW;
            hb.Visible = content > ViewW;
            if (hb.Visible) { hb.LargeChange = Math.Max(1, ViewW); hb.Maximum = content; hb.Value = Math.Max(0, Math.Min(sx, content - ViewW)); }
            else { sx = 0; }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScroll(); }

        public void Toggle(int row, int day)
        {
            if (row < 0 || row >= rows.Count || day < 1 || day > cols) return;
            if (!rows[row].Days.Remove(day)) rows[row].Days.Add(day);
            Invalidate();
        }

        void Reveal()
        {
            int x = (curC - 1) * CellW;
            if (x < sx) sx = x; else if (x + CellW > sx + ViewW) sx = x + CellW - ViewW;
            if (hb.Visible) hb.Value = Math.Max(0, Math.Min(sx, hb.Maximum - hb.LargeChange + 1)); else sx = 0;
        }

        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) curC = Math.Max(1, curC - 1);
            else if (e.KeyCode == Keys.Right) curC = Math.Min(cols, curC + 1);
            else if (e.KeyCode == Keys.Up) curR = Math.Max(0, curR - 1);
            else if (e.KeyCode == Keys.Down) curR = Math.Min(rows.Count - 1, curR + 1);
            else if (e.KeyCode == Keys.Space) Toggle(curR, curC);
            else return;
            e.Handled = true; Reveal(); Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            int r = (e.Y - HeadH) / RowH;
            if (e.Y < HeadH || r < 0 || r >= rows.Count) return;
            curR = r;
            if (e.X >= NameW) { curC = Math.Max(1, Math.Min(cols, (e.X - NameW + sx) / CellW + 1)); Toggle(curR, curC); }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (hb.Visible) hb.Value = Math.Max(0, Math.Min(hb.Maximum - hb.LargeChange + 1, hb.Value - Math.Sign(e.Delta) * CellW * 3));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Theme t = Theme.Current;
            g.Clear(Color.White);
            TextFormatFlags mid = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            using (Pen line = new Pen(t.CellBorder))
            using (Font bold = new Font(Font, FontStyle.Bold))
            {
                g.SetClip(new Rectangle(NameW, 0, ViewW, ClientSize.Height));
                int first = Math.Max(1, sx / CellW + 1), last = Math.Min(cols, (sx + ViewW) / CellW + 1);
                for (int d = first; d <= last; d++)
                {
                    int x = NameW + (d - 1) * CellW - sx;
                    bool run = runDays.Contains(d);
                    using (SolidBrush hb2 = new SolidBrush(run ? Color.FromArgb(205, 224, 248) : t.RowHead)) g.FillRectangle(hb2, x, 0, CellW, HeadH);
                    g.DrawRectangle(line, x, 0, CellW, HeadH);
                    TextRenderer.DrawText(g, d.ToString(), bold, new Rectangle(x, 3, CellW, Font.Height), t.HeadText, mid);
                    TextRenderer.DrawText(g, head(d), Font, new Rectangle(x + 1, 4 + Font.Height, CellW - 2, Font.Height * 2), run ? t.HeadText : Color.FromArgb(90, 98, 112), mid);
                    for (int r = 0; r < rows.Count; r++)
                    {
                        Rectangle c = new Rectangle(x, HeadH + r * RowH, CellW, RowH);
                        if (run) using (SolidBrush tint = new SolidBrush(Color.FromArgb(240, 246, 254))) g.FillRectangle(tint, c);
                        if (rows[r].Days.Contains(d))
                            using (SolidBrush fb = new SolidBrush(t.BufColors[rows[r].Mob && rows[r].Stat ? 2 : (rows[r].Stat ? 1 : 0)])) g.FillRectangle(fb, c.X + 6, c.Y + 6, c.Width - 12, c.Height - 12);
                        g.DrawRectangle(line, c);
                        if (Focused && r == curR && d == curC) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(c, -2, -2));
                    }
                }
                g.ResetClip();
                for (int r = 0; r < rows.Count; r++)
                {
                    Rectangle c = new Rectangle(0, HeadH + r * RowH, NameW, RowH);
                    using (SolidBrush rb = new SolidBrush(t.RowHead)) g.FillRectangle(rb, c);
                    using (SolidBrush st = new SolidBrush(t.BufColors[rows[r].Mob && rows[r].Stat ? 2 : (rows[r].Stat ? 1 : 0)])) g.FillRectangle(st, c.X, c.Y, 4, c.Height);
                    g.DrawRectangle(line, c);
                    TextRenderer.DrawText(g, rows[r].Name, bold, new Rectangle(c.X + 8, c.Y + 3, c.Width - 12, Font.Height + 1), t.HeadText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, rows[r].Typ + (rows[r].IsNew ? " · wird angelegt" : ""), Font, new Rectangle(c.X + 8, c.Y + 4 + Font.Height, c.Width - 12, Font.Height + 1), Color.FromArgb(90, 98, 112), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                g.DrawRectangle(line, 0, 0, NameW, HeadH);
                if (rows.Count == 0) TextRenderer.DrawText(g, "Kein Puffer-Schritt im Paket – „Einfüllen“ legt ihn an.", Font, new Rectangle(8, HeadH + 8, ClientSize.Width - 16, Font.Height + 2), SystemColors.GrayText);
            }
        }
    }
}
