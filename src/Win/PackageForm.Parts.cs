using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Beschrifteter Bereich: Kopfleiste mit Titel und darunter eine Zeile Hilfetext in Grau. Der Inhalt beginnt bei Padding.Top
    // (gilt auch fuer angedockte Kinder). major = Hauptbereich (gefuellte Kopfleiste), sonst Untergruppe (Titel mit Linie).
    class Section : Panel
    {
        public static readonly Color HelpColor = Color.FromArgb(100, 106, 118);
        readonly bool major;
        string title, help;

        public Section(string title, string help, bool major, int width)
        {
            this.title = title; this.help = help; this.major = major;
            Tag = "section";   // Theme.Style faerbt angedockte Bereiche sonst als Leiste
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Theme.Current.Modern ? Color.White : Theme.Current.Window;
            Font = Theme.Current.UiFont();   // schon beim Aufbau richtig (ContentTop haengt von der Schrift ab)
            Width = width;
            Pad();
        }

        public string Help { get { return help; } set { help = value; Pad(); Invalidate(); } }
        public string Title { get { return title; } set { title = value; Invalidate(); } }
        int HeadH { get { return Font.Height + (major ? 10 : 6); } }
        public int ContentTop { get { return Padding.Top; } }

        void Pad() { Padding = new Padding(8, HeadH + (help.Length > 0 ? Font.Height + 2 : 0) + 6, 8, 8); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Pad(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(BackColor);
            Rectangle head = new Rectangle(0, 0, Width, HeadH);
            using (Font b = new Font(Font, FontStyle.Bold))
            {
                if (major)
                {
                    t.FillHead(g, head, t.RowHead);
                    TextRenderer.DrawText(g, title, b, new Rectangle(8, 0, Width - 12, HeadH), t.HeadText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    TextRenderer.DrawText(g, title, b, new Rectangle(8, 0, Width - 12, HeadH), t.HeadText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    using (Pen p = new Pen(t.Separator)) g.DrawLine(p, 8, HeadH - 1, Width - 9, HeadH - 1);
                }
            }
            if (help.Length > 0)
                TextRenderer.DrawText(g, help, Font, new Rectangle(8, HeadH + 2, Width - 14, Font.Height + 2), HelpColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (Pen p = new Pen(major ? t.CellBorder : t.Separator)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // Kopfzeile "Produkt … › Ablauf › Schritt 3: Lauf": zeigt jederzeit, wo man ist; Klick auf einen Teil springt dorthin.
    class Crumbs : Control
    {
        public string[] Parts = new string[0];
        public string Status = "";
        public Color StatusColor = Color.Black;
        public event Action<int> PartClicked;
        readonly List<Rectangle> hit = new List<Rectangle>();

        public Crumbs()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            Dock = DockStyle.Top;
        }

        public void Set(string[] parts, string status, Color statusColor) { Parts = parts; Status = status; StatusColor = statusColor; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(t.Bar);
            if (t.Stripe) using (SolidBrush b = new SolidBrush(t.BorderSel)) g.FillRectangle(b, 0, 0, Width, 3);
            using (Pen p = new Pen(t.CellBorder)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            hit.Clear();
            TextFormatFlags ff = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (Font big = new Font(Font.FontFamily, Font.SizeInPoints + 2f))
            using (Font bold = new Font(big, FontStyle.Bold))
            {
                int sw = Status.Length > 0 ? TextRenderer.MeasureText(g, Status, Font, Size.Empty, ff).Width + 16 : 0;
                int x = 12, y = (Height - big.Height) / 2 + (t.Stripe ? 1 : 0);
                int sepW = TextRenderer.MeasureText(g, "›", big, Size.Empty, ff).Width;
                for (int i = 0; i < Parts.Length; i++)
                {
                    bool last = i == Parts.Length - 1;
                    Font f = last ? bold : big;
                    int w = Math.Max(20, Math.Min(TextRenderer.MeasureText(g, Parts[i], f, Size.Empty, ff).Width + 2, Width - sw - x - 8));
                    Rectangle r = new Rectangle(x, y, w, big.Height + 2);
                    TextRenderer.DrawText(g, Parts[i], f, r, last ? t.BorderSel : t.HeadText, ff | TextFormatFlags.EndEllipsis);
                    hit.Add(r); x = r.Right + 8;
                    if (!last) { TextRenderer.DrawText(g, "›", big, new Point(x, y), t.Arrow, ff); x += sepW + 8; }
                }
                if (sw > 0) TextRenderer.DrawText(g, Status, Font, new Rectangle(Width - sw, 0, sw - 8, Height), StatusColor, ff | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }

        int HitPart(Point p) { for (int i = 0; i < hit.Count - 1; i++) if (hit[i].Contains(p)) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = HitPart(e.Location) >= 0 ? Cursors.Hand : Cursors.Default; }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); int i = HitPart(e.Location); if (i >= 0 && PartClicked != null) PartClicked(i); }
    }

    // "Du bearbeitest gerade: …" ueber den Schritt-Details (orange wie der Bearbeitungsmodus im Plan).
    class EditMarker : Control
    {
        public bool Active;
        public EditMarker() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Dock = DockStyle.Top; }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(Active ? Color.FromArgb(255, 243, 226) : Parent.BackColor);
            if (Active) using (SolidBrush b = new SolidBrush(t.BorderEdit)) g.FillRectangle(b, 0, 0, 5, Height);
            using (Font f = new Font(Font.FontFamily, Font.SizeInPoints + 1f, Active ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, Text, f, new Rectangle(12, 0, Width - 16, Height), Active ? Color.FromArgb(120, 60, 0) : Section.HelpColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
