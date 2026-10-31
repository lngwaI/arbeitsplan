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
        public static readonly Color HelpColor = Color.FromArgb(70, 76, 90);
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
