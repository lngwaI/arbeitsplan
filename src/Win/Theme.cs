using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Alle Farben, Masse und Schriften der Oberflaeche an einer Stelle. Nur Optik: keine Logik, keine Daten.
    // Ein Look (Mindestsystem Windows 10): heller, flacher Look (Seitenstreifen der Schichten, eigene Checkbox),
    // Akzentstreifen + 2-px-Auswahl, flache Menues/Buttons. Schrift Segoe UI mit Rueckfall (Tahoma -> Systemschrift).
    class Theme
    {
        public static Theme Current = Make("win10");

        public string Name, FontName;
        public float FontSize;
        public bool Modern;          // heller Look: Tageskoepfe ohne Flaeche, eigene Checkbox, kleiner Pfeil
        public bool Stripe;          // win10: Akzentstreifen oben + 2-px-Auswahlrahmen
        public bool FlatButtons;     // flache Schaltflaechen mit Hover, Farbfelder mit Auswahlrahmen
        public bool ShiftSep;        // duenne Trennlinien zwischen den Schichten

        // ---- Masse (Pixel) ----
        public int HeadH, LkzH = 22, LW, Pad, Gap, HeadTextX, ButtonH, BarH;

        // ---- Farben ----
        public Color Window, Bar, CellBorder, BorderSel, BorderEdit, DayHead, DayHeadToday, RowHead, RowHeadToday,
            CellBack, CellBackWeekend, CellBackToday, HeadText, HeadTextCollapsed, Arrow, Dot,
            DayWeekday, DayDate, DayTodayText, DayLine, DayLineToday, Separator, LkzOn, LkzText, ShiftLine, ShiftLabelText,
            ButtonBack, ButtonBorder, ButtonHover, ButtonDown, ButtonOn, SwatchSelected, SwatchBorder;
        public Color[] ShiftLabel, ShiftBack;
        public readonly Color CheckBorder = C(140, 148, 162), CheckMark = C(30, 120, 60);   // eigene Checkbox (Modern)
        // Tagesaufgabe, Pruefleiste (erledigt = LkzOn, automatisch, fehlt), gedaempfter Text, Rundgaenge vollstaendig
        public readonly Color TaskBack = C(247, 248, 251), CheckAuto = C(234, 246, 236), CheckMissing = C(253, 222, 222), CheckMissingText = C(185, 30, 30),
            Muted = C(150, 156, 168), DoneText = C(30, 130, 60), Changed = C(90, 150, 220);   // Changed: von anderem PC geaendert
        public int RoundH = 21;      // Zeilenhoehe der Rundgangsleiste

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Theme Make(string name)
        {
            Theme t = new Theme();
            t.Name = name;
            t.BorderSel = C(40, 100, 200); t.BorderEdit = C(255, 140, 0); t.Dot = C(215, 100, 0);
            t.CellBack = Color.White;
            {   // heller, flacher Look + Akzentstreifen, flache Menues
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Modern = true; t.Stripe = true; t.FlatButtons = true; t.ShiftSep = true;
                t.HeadH = 27; t.LW = 14; t.Pad = 10; t.Gap = 10; t.HeadTextX = 9; t.ButtonH = 30; t.BarH = 36;
                t.Window = C(245, 246, 248); t.Bar = t.Window; t.BorderSel = C(0, 120, 215); t.Dot = C(232, 110, 0);
                t.CellBorder = C(214, 218, 225); t.DayHead = C(228, 233, 241); t.DayHeadToday = C(255, 232, 160);
                t.RowHead = C(238, 241, 246); t.RowHeadToday = C(255, 240, 200);
                t.CellBackWeekend = C(250, 251, 253); t.CellBackToday = C(255, 252, 242);
                t.HeadText = C(45, 55, 75); t.HeadTextCollapsed = C(95, 105, 125); t.Arrow = C(130, 140, 158);
                t.DayWeekday = C(35, 45, 65); t.DayDate = C(125, 133, 148); t.DayTodayText = C(175, 100, 0);
                t.DayLine = C(205, 211, 222); t.DayLineToday = C(255, 175, 40);
                t.Separator = C(230, 233, 238); t.LkzOn = C(214, 238, 218); t.LkzText = C(95, 103, 118);
                t.ShiftLine = C(226, 229, 235); t.ShiftLabelText = C(70, 75, 90);
                t.ShiftLabel = new Color[] { C(255, 233, 165), C(255, 211, 170), C(180, 198, 234) };
                t.ShiftBack = new Color[] { C(255, 251, 238), C(255, 246, 238), C(242, 246, 253) };
                t.ButtonBack = Color.White; t.ButtonBorder = C(205, 210, 219); t.ButtonHover = C(232, 238, 248); t.ButtonDown = C(213, 224, 242);
                t.ButtonOn = C(214, 228, 248); t.SwatchSelected = C(40, 100, 200); t.SwatchBorder = C(205, 210, 219);
            }
            return t;
        }

        // Bleibt als Einstieg fuer Program.cs; es gibt nur noch einen Look.
        public static void Select(string name)
        {
            Current.FreeGdi();
            Current = Make("win10");
            ToolStripManager.Renderer = new FlatRenderer();
        }

        // ---- Schrift: gewuenschte Familie, sonst Tahoma (XP), sonst Systemschrift ----
        public Font UiFont()
        {
            Font f = Try(FontName, FontSize);
            if (f == null) f = Try("Tahoma", 8.25f);
            return f ?? SystemFonts.MessageBoxFont;
        }

        static Font Try(string name, float size) { return Try(name, size, FontStyle.Regular); }

        // null, wenn die Schrift fehlt (Windows wuerde stillschweigend MS Sans Serif nehmen)
        public static Font Try(string name, float size, FontStyle style)
        {
            try
            {
                Font f = new Font(name, size, style, GraphicsUnit.Point);
                if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
            }
            catch (Exception) { }
            return null;
        }

        static Color Lighten(Color c, int d) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.R + d)), Math.Max(0, Math.Min(255, c.G + d)), Math.Max(0, Math.Min(255, c.B + d))); }

        // ---- Gecachte Pinsel/Stifte (nur UI-Thread; nie selbst freigeben, das macht FreeGdi beim Modellwechsel) ----
        readonly System.Collections.Generic.Dictionary<int, SolidBrush> brushes = new System.Collections.Generic.Dictionary<int, SolidBrush>();
        readonly System.Collections.Generic.Dictionary<long, Pen> pens = new System.Collections.Generic.Dictionary<long, Pen>();

        // Zellhintergrund: heute, Wochenende oder normal.
        public Color CellBackFor(DateTime day)
        {
            if (day.Date == DateTime.Today) return CellBackToday;
            return day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday ? CellBackWeekend : CellBack;
        }

        public SolidBrush Brush(Color c)
        {
            SolidBrush b;
            if (!brushes.TryGetValue(c.ToArgb(), out b)) { b = new SolidBrush(c); brushes[c.ToArgb()] = b; }
            return b;
        }

        public Pen Pen(Color c, int width)
        {
            long k = ((long)c.ToArgb() << 8) | (uint)width;
            Pen p;
            if (!pens.TryGetValue(k, out p)) { p = new Pen(c, width); pens[k] = p; }
            return p;
        }

        public void FreeGdi()
        {
            foreach (SolidBrush b in brushes.Values) b.Dispose();
            foreach (Pen p in pens.Values) p.Dispose();
            brushes.Clear(); pens.Clear();
        }

        // Kopfleiste: flache Farbe.
        public void FillHead(Graphics g, Rectangle r, Color c) { g.FillRectangle(Brush(c), r); }

        // Eigene Checkbox (13x13, GDI+), nur im Modern-Look.
        public void DrawCheck(Graphics g, Point p, bool on)
        {
            Rectangle r = new Rectangle(p.X, p.Y, 12, 12);
            g.FillRectangle(Brush(Color.White), r);
            g.DrawRectangle(Pen(on ? CheckMark : CheckBorder, 1), r);
            if (!on) return;
            SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawLines(Pen(CheckMark, 2), new Point[] { new Point(p.X + 3, p.Y + 6), new Point(p.X + 5, p.Y + 8), new Point(p.X + 9, p.Y + 3) });
            g.SmoothingMode = sm;
        }

        // Fenster und Leisten einfaerben, Standard-Schaltflaechen im Look (rekursiv; selbst gestaltete Steuerelemente bleiben unberuehrt).
        public void Style(Control root)
        {
            if (root is Form) root.BackColor = Window;
            foreach (Control c in root.Controls)
            {
                if (c is Panel && !(c is TabPage) && c.Tag == null && (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom || c.Dock == DockStyle.Right)) c.BackColor = Bar;
                else if (c is Button && ((Button)c).FlatStyle == FlatStyle.Standard) StyleButton((Button)c);
                if (c.Controls.Count > 0) Style(c);
            }
        }

        // Schaltflaeche im aktuellen Look (xp/win7: unveraendert).
        public void StyleButton(Button b)
        {
            if (!FlatButtons) return;
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = ButtonBack;
            b.FlatAppearance.BorderColor = ButtonBorder;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ButtonHover;
            b.FlatAppearance.MouseDownBackColor = ButtonDown;
        }

        // win10-Menues: flach, weiss, hellblaue Auswahl (statt Verlauf)
        class FlatColors : ProfessionalColorTable
        {
            public FlatColors() { UseSystemColors = false; }
            public override Color MenuStripGradientBegin { get { return Color.White; } }
            public override Color MenuStripGradientEnd { get { return Color.White; } }
            public override Color ToolStripDropDownBackground { get { return Color.White; } }
            public override Color ImageMarginGradientBegin { get { return Color.White; } }
            public override Color ImageMarginGradientMiddle { get { return Color.White; } }
            public override Color ImageMarginGradientEnd { get { return Color.White; } }
            public override Color MenuItemSelected { get { return C(229, 241, 251); } }
            public override Color MenuItemSelectedGradientBegin { get { return C(229, 241, 251); } }
            public override Color MenuItemSelectedGradientEnd { get { return C(229, 241, 251); } }
            public override Color MenuItemPressedGradientBegin { get { return C(204, 228, 247); } }
            public override Color MenuItemPressedGradientMiddle { get { return C(204, 228, 247); } }
            public override Color MenuItemPressedGradientEnd { get { return C(204, 228, 247); } }
            public override Color MenuItemBorder { get { return C(204, 228, 247); } }
            public override Color MenuBorder { get { return C(204, 204, 204); } }
            public override Color SeparatorDark { get { return C(215, 215, 215); } }
            public override Color SeparatorLight { get { return Color.White; } }
        }

        class FlatRenderer : ToolStripProfessionalRenderer
        {
            public FlatRenderer() : base(new FlatColors()) { RoundedEdges = false; }
        }
    }
}
