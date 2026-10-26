using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Kleiner rahmenloser Startbildschirm: Begruessung + Ladeanimation. Der Name wird im Hintergrund ermittelt (blockiert nie).
    class Splash : Form
    {
        readonly Timer timer = new Timer();
        string name;
        int tick;
        readonly string greet;

        public Splash()
        {
            Theme t = Theme.Current;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false; TopMost = true;
            ClientSize = new Size(380, 130);
            BackColor = t.Window;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            int h = DateTime.Now.Hour;
            greet = h < 10 ? "Guten Morgen" : (h < 18 ? "Guten Tag" : "Guten Abend");
            timer.Interval = 40;
            timer.Tick += delegate { tick++; Invalidate(); };
            timer.Start();
        }

        public void SetName(string n) { name = n; Invalidate(); }

        // Vorname des Windows-Profils (Anzeigename, sonst Anmeldename); null wenn nicht ermittelbar. Kann dauern -> im Thread aufrufen.
        public static string LookupName()
        {
            string n = null;
            try { n = AdName(); } catch { }
            if (string.IsNullOrEmpty(n)) { try { n = Environment.UserName; } catch { } }
            if (n == null) return null;
            n = n.Trim();
            int sp = n.IndexOf(' ');
            if (sp > 0) n = n.Substring(0, sp);
            return n.Length == 0 ? null : n;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static string AdName()
        {
            System.DirectoryServices.AccountManagement.UserPrincipal u = System.DirectoryServices.AccountManagement.UserPrincipal.Current;
            if (u == null) return null;
            if (!string.IsNullOrEmpty(u.GivenName)) return u.GivenName;
            return u.DisplayName;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            g.Clear(t.Window);
            using (Pen p = new Pen(t.CellBorder)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            if (t.Stripe) using (Brush b = new SolidBrush(t.BorderSel)) g.FillRectangle(b, 0, 0, Width, 4);
            string text = name != null ? greet + " " + name : greet;
            using (Font big = new Font(t.FontName, 15f, FontStyle.Bold))
            using (Font small = new Font(t.FontName, 9f))
            using (Brush fg = new SolidBrush(SystemColors.WindowText))
            {
                StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.DrawString(text, big, fg, new RectangleF(10, 24, Width - 20, 34), sf);
                g.DrawString("Arbeitsplan wird geladen …", small, fg, new RectangleF(10, 66, Width - 20, 20), sf);
            }
            // Laufbalken
            Rectangle bar = new Rectangle(40, 100, Width - 80, 8);
            using (Brush b = new SolidBrush(t.CellBorder)) g.FillRectangle(b, bar);
            int w = bar.Width / 4;
            int x = (tick * 6) % (bar.Width + w) - w;
            Rectangle seg = Rectangle.Intersect(bar, new Rectangle(bar.X + x, bar.Y, w, bar.Height));
            using (Brush b = new SolidBrush(t.BorderSel)) g.FillRectangle(b, seg);
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}

