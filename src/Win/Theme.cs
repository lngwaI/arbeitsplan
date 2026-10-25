using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Drei Oberflaechenmodelle (Start: --theme xp|win7|win10, sonst passend zum Betriebssystem).
    // Alle laufen auf jedem Windows ab XP SP3: nur .NET-2.0/4.0-GDI-APIs, Schrift mit Rueckfall (Segoe UI -> Tahoma).
    class Theme
    {
        public static Theme Current = Make("win7");

        public string Name, FontName;
        public float FontSize;
        public Color Window, Bar, Head, HeadToday, Cell, CellToday, CellWeekend, Border, BorderSel, BorderEdit, Arrow, Dot, LkzOn;
        public Color[] ShiftLabel, ShiftBack;
        public bool Gradient, FlatButtons;
        public int ButtonH, BarH;

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Theme Make(string name)
        {
            Theme t = new Theme();
            t.Name = name;
            if (name == "xp")          // Luna: Tahoma, beige Flaechen, blaue Kopfleisten mit Verlauf
            {
                t.FontName = "Tahoma"; t.FontSize = 8.25f; t.Gradient = true; t.ButtonH = 24; t.BarH = 30;
                t.Window = C(236, 233, 216); t.Bar = C(236, 233, 216);
                t.Head = C(196, 211, 240); t.HeadToday = C(255, 222, 140);
                t.Cell = Color.White; t.CellToday = C(255, 250, 230); t.CellWeekend = C(245, 244, 238);
                t.Border = C(127, 157, 185); t.BorderSel = C(0, 60, 200); t.BorderEdit = C(255, 140, 0);
                t.Arrow = C(60, 70, 110); t.Dot = C(215, 100, 0); t.LkzOn = C(150, 220, 150);
                t.ShiftLabel = new Color[] { C(255, 225, 130), C(255, 190, 130), C(150, 175, 225) };
                t.ShiftBack = new Color[] { C(255, 248, 225), C(255, 240, 225), C(232, 238, 250) };
            }
            else if (name == "win10")  // flach: keine Verlaeufe, helle Flaechen, Akzentblau
            {
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Gradient = false; t.FlatButtons = true; t.ButtonH = 32; t.BarH = 40;
                t.Window = C(243, 243, 243); t.Bar = C(255, 255, 255);
                t.Head = C(221, 232, 245); t.HeadToday = C(255, 232, 160);
                t.Cell = Color.White; t.CellToday = C(255, 251, 237); t.CellWeekend = C(248, 248, 248);
                t.Border = C(200, 200, 200); t.BorderSel = C(0, 120, 215); t.BorderEdit = C(255, 140, 0);
                t.Arrow = C(90, 90, 90); t.Dot = C(232, 110, 0); t.LkzOn = C(160, 225, 160);
                t.ShiftLabel = new Color[] { C(255, 228, 140), C(255, 200, 150), C(160, 190, 235) };
                t.ShiftBack = new Color[] { C(255, 250, 232), C(255, 243, 232), C(236, 242, 252) };
            }
            else                       // win7 (Aero): Segoe UI, dezente Verlaeufe, hellgraue Flaechen
            {
                t.Name = "win7";
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Gradient = true; t.ButtonH = 28; t.BarH = 36;
                t.Window = C(240, 240, 240); t.Bar = C(245, 247, 250);
                t.Head = C(205, 218, 234); t.HeadToday = C(255, 224, 130);
                t.Cell = Color.White; t.CellToday = C(255, 250, 230); t.CellWeekend = C(244, 246, 250);
                t.Border = C(150, 160, 175); t.BorderSel = C(40, 100, 200); t.BorderEdit = C(255, 140, 0);
                t.Arrow = C(70, 80, 100); t.Dot = C(215, 100, 0); t.LkzOn = C(150, 220, 150);
                t.ShiftLabel = new Color[] { C(255, 225, 130), C(255, 190, 130), C(150, 175, 225) };
                t.ShiftBack = new Color[] { C(255, 248, 225), C(255, 240, 225), C(232, 238, 250) };
            }
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
            if (name == null || (name != "xp" && name != "win7" && name != "win10")) name = Detect();
            Current = Make(name);
            ToolStripManager.Renderer = Current.Name == "win10" ? (ToolStripRenderer)new FlatRenderer() : new ToolStripProfessionalRenderer();
        }

        // Windows-Schrift: gewuenschte Familie, sonst Systemschrift (z. B. Segoe UI fehlt auf XP).
        public Font UiFont()
        {
            foreach (FontFamily ff in FontFamily.Families)
                if (string.Equals(ff.Name, FontName, StringComparison.OrdinalIgnoreCase) && ff.IsStyleAvailable(FontStyle.Regular))
                    return new Font(ff, FontSize, FontStyle.Regular, GraphicsUnit.Point);
            return SystemFonts.MessageBoxFont;
        }

        public static Color Lighten(Color c, int d) { return Color.FromArgb(Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d)); }

        // Kopfleiste: Verlauf (XP, Win7) oder flache Farbe (Win10)
        public void FillHead(Graphics g, Rectangle r, Color c)
        {
            if (!Gradient || r.Width < 1 || r.Height < 1) { using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, r); return; }
            using (LinearGradientBrush b = new LinearGradientBrush(r, Lighten(c, Name == "xp" ? 38 : 28), c, LinearGradientMode.Vertical))
                g.FillRectangle(b, r);
        }

        // Fenster, Leisten, Schaltflaechen einheitlich einfaerben (rekursiv; selbst gestaltete Steuerelemente bleiben unberuehrt).
        public void Style(Control root)
        {
            if (root is Form) root.BackColor = Window;
            foreach (Control c in root.Controls)
            {
                if (c is Panel && !(c is TabPage) && c.Tag == null && (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom || c.Dock == DockStyle.Right)) c.BackColor = Bar;
                else if (c is Button) StyleButton((Button)c);
                if (c.Controls.Count > 0) Style(c);
            }
        }

        void StyleButton(Button b)
        {
            if (!FlatButtons || b.FlatStyle != FlatStyle.Standard) return;   // Farbfelder (schon Flat) nicht anfassen
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false;
            b.BackColor = C(225, 225, 225);
            b.FlatAppearance.BorderColor = C(173, 173, 173);
            b.FlatAppearance.MouseOverBackColor = C(229, 241, 251);
            b.FlatAppearance.MouseDownBackColor = C(204, 228, 247);
        }

        // Win10-Menues: flach, weiss, hellblaue Auswahl (statt Verlauf)
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
