using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Tageskopf ("Mo, 19.10.2026"). Alter Look: farbige Flaeche, fetter Text. Moderner Look: ohne Rahmen/Flaeche,
    // Wochentag kraeftig, Datum heller, heute mit Akzentlinie.
    class DayLabel : Control
    {
        public bool Today;
        readonly Font bold;

        public DayLabel(Font f)
        {
            bold = new Font(f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void Dispose(bool disposing) { if (disposing) bold.Dispose(); base.Dispose(disposing); }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            Rectangle r = ClientRectangle;
            if (!Theme.Modern)
            {
                using (SolidBrush b = new SolidBrush(Today ? Theme.DayHeadToday : Theme.DayHead)) g.FillRectangle(b, r);
                TextRenderer.DrawText(g, Text, bold, r, Theme.DayWeekday, tf | TextFormatFlags.HorizontalCenter);
                return;
            }
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            int i = Text.IndexOf(", ");
            string wd = i > 0 ? Text.Substring(0, i) : Text, dt = i > 0 ? Text.Substring(i + 2) : "";
            Size sw = TextRenderer.MeasureText(g, wd, bold, new Size(1000, 100), tf | TextFormatFlags.NoPadding);
            Size sd = TextRenderer.MeasureText(g, dt, Font, new Size(1000, 100), tf | TextFormatFlags.NoPadding);
            int x = r.X + Math.Max(0, (r.Width - sw.Width - 8 - sd.Width) / 2);
            Rectangle rw = new Rectangle(x, r.Y, sw.Width + 2, r.Height - 3);
            TextRenderer.DrawText(g, wd, bold, rw, Today ? Theme.DayTodayText : Theme.DayWeekday, tf);
            TextRenderer.DrawText(g, dt, Font, new Rectangle(rw.Right + 6, r.Y, sd.Width + 4, r.Height - 3), Theme.DayDate, tf);
            using (SolidBrush b = new SolidBrush(Today ? Theme.DayLineToday : Theme.DayLine)) g.FillRectangle(b, r.X, r.Bottom - (Today ? 2 : 1), r.Width, Today ? 2 : 1);
        }
    }
}
