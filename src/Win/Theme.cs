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
        public bool Glossy, InnerLight, Stripe, FlatButtons;   // Glanz-Kopfleiste + Schlagschatten (win7), Akzentstreifen + 2-px-Auswahl (win10)
        public int ButtonH, BarH, Pad, Gap, ShadowSize, ShadowAlpha;   // ShadowSize 0 = kein Schatten

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Theme Make(string name)
        {
            Theme t = new Theme();
            t.Name = name;
            t.ShiftLabel = new Color[] { C(255, 225, 130), C(255, 190, 130), C(150, 175, 225) };
            t.ShiftBack = new Color[] { C(255, 248, 225), C(255, 240, 225), C(232, 238, 250) };
            t.Cell = Color.White; t.CellToday = C(255, 250, 230); t.BorderEdit = C(255, 140, 0); t.Arrow = C(70, 80, 100); t.Dot = C(215, 100, 0); t.LkzOn = C(150, 220, 150);
            if (name == "xp")          // der urspruengliche Look: Tahoma, Systemgrau, schlicht, eckig
            {
                t.FontName = "Tahoma"; t.FontSize = 8.25f; t.ButtonH = 30; t.BarH = 34; t.Pad = 4; t.Gap = 4;
                t.Window = SystemColors.Control; t.Bar = SystemColors.Control;
                t.Head = C(205, 218, 234); t.HeadToday = C(255, 224, 130); t.CellWeekend = C(244, 246, 250);
                t.Border = C(150, 160, 175); t.BorderSel = C(40, 100, 200);
            }
            else if (name == "win10")  // flach und luftig: viel Weissraum, duenne helle Rahmen, Akzentstreifen, 2-px-Auswahl, flache Menues/Buttons
            {
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Stripe = true; t.FlatButtons = true; t.ShadowSize = 2; t.ShadowAlpha = 24; t.ButtonH = 34; t.BarH = 42; t.Pad = 10; t.Gap = 8;
                t.Window = C(243, 243, 243); t.Bar = Color.White;
                t.Head = C(232, 238, 246); t.HeadToday = C(255, 244, 204); t.CellToday = C(255, 252, 240); t.CellWeekend = C(250, 250, 250);
                t.Border = C(224, 224, 224); t.BorderSel = C(0, 120, 215); t.Arrow = C(100, 100, 100); t.Dot = C(232, 110, 0); t.LkzOn = C(166, 226, 166);
                t.ShiftLabel = new Color[] { C(255, 233, 150), C(255, 208, 160), C(170, 198, 240) };
                t.ShiftBack = new Color[] { C(255, 251, 238), C(255, 245, 236), C(238, 243, 252) };
            }
            else                       // win7 (Aero): Segoe UI, Glanz-Kopfleisten, Schlagschatten, leicht bläuliche Flaechen
            {
                t.Name = "win7";
                t.FontName = "Segoe UI"; t.FontSize = 9f; t.Glossy = true; t.InnerLight = true; t.ShadowSize = 4; t.ShadowAlpha = 70; t.ButtonH = 30; t.BarH = 38; t.Pad = 6; t.Gap = 6;
                t.Window = C(228, 234, 243); t.Bar = C(240, 244, 250);
                t.Head = C(190, 208, 234); t.HeadToday = C(255, 220, 120); t.CellWeekend = C(240, 244, 250);
                t.Border = C(120, 140, 170); t.BorderSel = C(40, 100, 200);
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
            name = name == null ? "" : name.ToLowerInvariant().Replace("windows", "").Replace("win", "").Replace(" ", "");   // winXP, Win7, windows10, 10 ...
            name = name == "xp" ? "xp" : (name == "7" ? "win7" : (name == "10" ? "win10" : Detect()));
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

        public static Color Lighten(Color c, int d) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.R + d)), Math.Max(0, Math.Min(255, c.G + d)), Math.Max(0, Math.Min(255, c.B + d))); }

        // Kopfleiste: Glanzverlauf (win7), sonst flache Farbe
        public void FillHead(Graphics g, Rectangle r, Color c)
        {
            if (!Glossy || r.Width < 1 || r.Height < 2) { using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, r); return; }
            using (LinearGradientBrush b = new LinearGradientBrush(r, Lighten(c, 45), c, LinearGradientMode.Vertical))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Positions = new float[] { 0f, 0.5f, 0.5f, 1f };
                cb.Colors = new Color[] { Lighten(c, 62), Lighten(c, 28), Lighten(c, -6), Lighten(c, -26) };
                b.InterpolationColors = cb;
                g.FillRectangle(b, r);
            }
            using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255))) g.DrawLine(p, r.X, r.Y, r.Right - 1, r.Y);   // Glanzkante
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
