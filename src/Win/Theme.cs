using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Alle Farben, Masse und Schriften der Oberflaeche an einer Stelle. Nur Optik: keine Logik, keine Daten.
    // Drei Modelle (Start: --theme xp|win7|win10, sonst passend zum Betriebssystem):
    //   xp    = urspruenglicher Look (Tahoma, Systemgrau, farbige Kopfleisten, eckig)
    //   win7  = Segoe UI, dezent blaeuliche Flaechen, leichter Verlauf in den Kopfleisten (kein Glanz/Schatten)
    //   win10 = heller, flacher Look (Seitenstreifen der Schichten, eigene Checkbox), Akzentstreifen + 2-px-Auswahl, flache Menues/Buttons
    // Alle laufen ab XP SP3: nur .NET-2.0/4.0-GDI-APIs, Schrift mit Rueckfall (Segoe UI -> Tahoma -> Systemschrift).
    class Theme
    {
        public static Theme Current = Make(Detect());

        public string Name, FontName;
        public float FontSize;
        public bool Modern;          // heller Look: Tageskoepfe ohne Flaeche, eigene Checkbox, kleiner Pfeil
        public bool Gradient;        // win7: leichter Verlauf in den Kopfleisten
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

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Theme Make(string name)
        {
            Theme t = new Theme();
            t.Name = name;
            t.BorderSel = C(40, 100, 200); t.BorderEdit = C(255, 140, 0); t.Dot = C(215, 100, 0);
            t.CellBack = Color.White;
            if (name == "win10")       // heller, flacher Look (UI-Umbau) + Akzentstreifen, flache Menues
            {
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Modern = true; t.Stripe = true; t.FlatButtons = true; t.ShiftSep = true;
                t.HeadH = 27; t.LW = 18; t.Pad = 10; t.Gap = 10; t.HeadTextX = 9; t.ButtonH = 30; t.BarH = 36;
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
                return t;
            }
            // xp (Original) und win7 teilen die Grundwerte
            t.HeadH = 24; t.LW = 26; t.Pad = 4; t.Gap = 4; t.HeadTextX = 3; t.ButtonH = 30; t.BarH = 34;
            t.Window = SystemColors.Control; t.Bar = SystemColors.Control; t.CellBorder = C(150, 160, 175);
            t.DayHead = C(205, 218, 234); t.DayHeadToday = C(255, 224, 130); t.RowHead = t.DayHead; t.RowHeadToday = t.DayHeadToday;
            t.CellBackWeekend = C(244, 246, 250); t.CellBackToday = C(255, 250, 230);
            t.HeadText = Color.Black; t.HeadTextCollapsed = C(70, 70, 70); t.Arrow = C(70, 80, 100);
            t.DayWeekday = Color.Black; t.DayDate = Color.Black; t.DayTodayText = Color.Black; t.DayLine = t.DayHead; t.DayLineToday = t.DayHeadToday;
            t.Separator = C(200, 205, 215); t.LkzOn = C(150, 220, 150); t.LkzText = Color.Black;
            t.ShiftLine = C(200, 205, 215); t.ShiftLabelText = Color.Black;
            t.ShiftLabel = new Color[] { C(255, 225, 130), C(255, 190, 130), C(150, 175, 225) };
            t.ShiftBack = new Color[] { C(255, 248, 225), C(255, 240, 225), C(232, 238, 250) };
            t.ButtonBack = SystemColors.Control; t.ButtonBorder = SystemColors.ControlDark; t.ButtonHover = SystemColors.Control; t.ButtonDown = SystemColors.Control;
            t.ButtonOn = C(190, 210, 240); t.SwatchSelected = Color.Black; t.SwatchBorder = Color.Black;
            if (name == "xp") { t.FontName = "Tahoma"; t.FontSize = 8.25f; return t; }

            t.Name = "win7";           // dezent blaeulich, leichter Verlauf, keine Schatten
            t.FontName = "Segoe UI"; t.FontSize = 9f; t.Gradient = true; t.HeadH = 25; t.Pad = 6; t.Gap = 6; t.HeadTextX = 5; t.BarH = 36;
            t.Window = C(233, 239, 247); t.Bar = C(242, 246, 251); t.CellBorder = C(165, 182, 208);
            t.DayHead = C(214, 226, 242); t.DayHeadToday = C(255, 226, 150); t.RowHead = t.DayHead; t.RowHeadToday = t.DayHeadToday;
            t.CellBackWeekend = C(245, 248, 252); t.CellBackToday = C(255, 251, 235);
            t.HeadText = C(25, 40, 70); t.HeadTextCollapsed = C(80, 95, 120); t.Arrow = C(80, 100, 135);
            t.Separator = C(208, 218, 232); t.LkzOn = C(180, 228, 180);
            return t;
        }

        // XP (5.x) -> xp, Vista/7 (6.0/6.1) -> win7, ab 6.2 -> win10 (ohne Manifest meldet auch Win 10 "6.2").
        public static string Detect()
        {
            Version v = Environment.OSVersion.Version;
            return v.Major < 6 ? "xp" : (v.Major == 6 && v.Minor < 2 ? "win7" : "win10");
        }

        public static void Select(string name)
        {
            name = name == null ? "" : name.ToLowerInvariant().Replace("windows", "").Replace("win", "").Replace(" ", "");   // winXP, Win7, windows10, 10 ...
            name = name == "xp" ? "xp" : (name == "7" ? "win7" : (name == "10" ? "win10" : Detect()));
            Current = Make(name);
            ToolStripManager.Renderer = Current.Stripe ? (ToolStripRenderer)new FlatRenderer() : new ToolStripProfessionalRenderer();
        }

        // ---- Schrift: gewuenschte Familie, sonst Tahoma (XP), sonst Systemschrift ----
        public Font UiFont()
        {
            Font f = Try(FontName, FontSize);
            if (f == null) f = Try("Tahoma", 8.25f);
            return f ?? SystemFonts.MessageBoxFont;
        }

        static Font Try(string name, float size)
        {
            try
            {
                Font f = new Font(name, size, FontStyle.Regular, GraphicsUnit.Point);
                if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
            }
            catch (Exception) { }
            return null;
        }

        static Color Lighten(Color c, int d) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.R + d)), Math.Max(0, Math.Min(255, c.G + d)), Math.Max(0, Math.Min(255, c.B + d))); }

        // Kopfleiste: leichter Verlauf (win7), sonst flache Farbe
        public void FillHead(Graphics g, Rectangle r, Color c)
        {
            if (!Gradient || r.Width < 1 || r.Height < 2) { using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, r); return; }
            using (LinearGradientBrush b = new LinearGradientBrush(r, Lighten(c, 18), Lighten(c, -6), LinearGradientMode.Vertical)) g.FillRectangle(b, r);
        }

        // Eigene Checkbox (13x13, GDI+), nur im Modern-Look.
        public void DrawCheck(Graphics g, Point p, bool on)
        {
            Rectangle r = new Rectangle(p.X, p.Y, 12, 12);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
            using (Pen bp = new Pen(on ? CheckMark : CheckBorder)) g.DrawRectangle(bp, r);
            if (!on) return;
            SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen cp = new Pen(CheckMark, 2f)) g.DrawLines(cp, new Point[] { new Point(p.X + 3, p.Y + 6), new Point(p.X + 5, p.Y + 8), new Point(p.X + 9, p.Y + 3) });
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
