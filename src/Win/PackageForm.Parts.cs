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

    // Kalenderleiste der Kampagne: je Tag eine Kachel mit Raum und Schritt (Tage ohne Schritt schmal); der gewaehlte Schritt
    // ist hervorgehoben, Klick auf einen Eintrag waehlt den Schritt.
    class PlanStrip : Control
    {
        public class Item { public int Day; public string Title = "", Sub = ""; public bool Missing; public PackageStep Step; }
        const int DayW = 118, EmptyW = 24, MaxDays = 370;
        List<Item> items = new List<Item>();
        readonly List<KeyValuePair<Rectangle, PackageStep>> hit = new List<KeyValuePair<Rectangle, PackageStep>>();
        PackageStep selected;
        public event Action<PackageStep> StepClicked;

        public PlanStrip() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); }

        int HeadH { get { return Font.Height + 6; } }
        int ItemH { get { return Font.Height * 2 + 6; } }
        int Days { get { int m = 0; foreach (Item i in items) m = Math.Max(m, i.Day); return Math.Min(m, MaxDays); } }
        int W(int day) { foreach (Item i in items) if (i.Day == day) return DayW; return EmptyW; }

        public void Set(List<Item> list, PackageStep sel)
        {
            items = list; selected = sel;
            int w = 1, most = 0;
            for (int d = 1; d <= Days; d++) { w += W(d); int n = 0; foreach (Item i in items) if (i.Day == d) n++; most = Math.Max(most, n); }
            Size = new Size(Math.Max(w, 40), HeadH + Math.Max(1, most) * (ItemH + 3) + 4);
            Invalidate();
        }

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
            using (SolidBrush selBack = new SolidBrush(t.ButtonOn))
            using (SolidBrush back = new SolidBrush(t.CellBackWeekend))
            {
                int x = 0;
                for (int d = 1; d <= Days; d++)
                {
                    int w = W(d);
                    bool any = w == DayW;
                    Rectangle head = new Rectangle(x, 0, w, HeadH);
                    if (any) t.FillHead(g, head, t.DayHead); else g.FillRectangle(back, head);
                    TextRenderer.DrawText(g, any ? "Tag " + d : d.ToString(), any ? bold : Font, head, any ? t.HeadText : Section.HelpColor,
                        ff | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    if (!any) g.FillRectangle(back, x, HeadH, w, Height - HeadH);
                    int y = HeadH + 3;
                    foreach (Item i in items)
                    {
                        if (i.Day != d) continue;
                        Rectangle r = new Rectangle(x + 3, y, w - 7, ItemH);
                        bool on = i.Step == selected;
                        if (on) g.FillRectangle(selBack, r);
                        g.DrawRectangle(on ? sel : line, r);
                        TextRenderer.DrawText(g, i.Title, bold, new Rectangle(r.X + 4, r.Y + 2, r.Width - 6, Font.Height + 1), t.HeadText, ff);
                        TextRenderer.DrawText(g, i.Sub, Font, new Rectangle(r.X + 4, r.Y + 3 + Font.Height, r.Width - 6, Font.Height + 1), i.Missing ? Color.Firebrick : Section.HelpColor, ff);
                        hit.Add(new KeyValuePair<Rectangle, PackageStep>(r, i.Step));
                        y += ItemH + 3;
                    }
                    g.DrawLine(line, x + w - 1, 0, x + w - 1, Height);
                    x += w;
                }
                if (items.Count == 0) TextRenderer.DrawText(g, "Noch keine Schritte.", Font, new Point(6, 6), Section.HelpColor);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            foreach (KeyValuePair<Rectangle, PackageStep> h in hit) if (h.Key.Contains(e.Location)) { if (StepClicked != null) StepClicked(h.Value); return; }
        }
    }

    // Auswahl beim "+ Schritt hinzufuegen": Bausteine aus dem Katalog mit Kurzbeschreibung, Sonderereignisse, leerer Schritt.
    class BausteinPicker : Form
    {
        readonly ListBox list = new ListBox();
        readonly List<Baustein> items = new List<Baustein>();   // null = leerer Schritt
        readonly List<string> descs = new List<string>();
        public Baustein Result;
        public bool EmptyStep;

        public BausteinPicker(Catalog cat, List<PackageStep> steps)
        {
            Text = "Schritt hinzufügen";
            Ui.Dialog(this, 520, 430, false);
            Label l = new Label(); l.Text = "Welche Art von Schritt soll in den Ablauf? Vorlagen (Bausteine) bringen Raum und Equipment-Positionen schon mit."; l.SetBounds(12, 10, 496, 34);
            list.SetBounds(12, 48, 496, 330); list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = Font.Height * 2 + 10; list.IntegralHeight = false;
            foreach (Baustein b in cat.Bausteine) if (!b.Special) Add(b, steps);
            foreach (Baustein b in cat.Bausteine) if (b.Special) Add(b, steps);
            items.Add(null); descs.Add("Ohne Vorlage: Titel, Raum und Equipment selbst festlegen."); list.Items.Add("Leerer Schritt");
            list.DrawItem += DrawItem;
            list.DoubleClick += delegate { Commit(); };
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            Button ok = new Button(); ok.Text = "Hinzufügen"; ok.SetBounds(308, 390, 100, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(414, 390, 94, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l, list, ok, cancel });
            Theme.Current.Style(this);
        }

        void Add(Baustein b, List<PackageStep> steps)
        {
            List<string> d = new List<string>();
            if (b.Name == "Lauf") d.Add("Lauf mit Laufnummer"); else if (b.Name == "Splitten") d.Add("Unterläufe");
            d.Add(b.Row.Length > 0 ? "Raum: " + b.Row : "Raum frei wählbar");
            d.Add(b.Positions.Count > 0 ? "Equipment: " + string.Join(", ", b.Positions.ToArray()) : "ohne Equipment");
            if (b.Unique) d.Add("nur einmal je Kampagne");
            if (b.Special) d.Add("Sonderereignis");
            if (steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; })) d.Add("schon im Ablauf");
            items.Add(b); descs.Add(string.Join(" · ", d.ToArray())); list.Items.Add(b.Name);
        }

        void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            e.DrawBackground();
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color main = sel ? SystemColors.HighlightText : SystemColors.WindowText, sub = sel ? SystemColors.HighlightText : Section.HelpColor;
            Rectangle r = e.Bounds;
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (Font b = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Convert.ToString(list.Items[e.Index]), b, new Rectangle(r.X + 6, r.Y + 4, r.Width - 10, Font.Height + 1), main, ff);
            TextRenderer.DrawText(e.Graphics, descs[e.Index], Font, new Rectangle(r.X + 6, r.Y + 5 + Font.Height, r.Width - 10, Font.Height + 1), sub, ff);
            e.DrawFocusRectangle();
        }

        void Commit()
        {
            int i = list.SelectedIndex;
            if (i < 0) return;
            Result = items[i]; EmptyStep = Result == null;
            DialogResult = DialogResult.OK;
        }
    }
}
