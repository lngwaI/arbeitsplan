using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Alle Farben, Masse und Schriften der Oberflaeche an einer Stelle. Zwei Looks: "alt" (bisher) und "modern".
    // Umschalter: Datei theme.ini neben der exe ("UseModernTheme=false") oder die Konstante DefaultModern.
    // Nur Optik: keine Logik, keine Daten.
    static class Theme
    {
        const bool DefaultModern = true;
        public static readonly bool Modern = Load();

        static bool Load()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.ini");
                if (File.Exists(p))
                    foreach (string line in File.ReadAllLines(p))
                    {
                        string[] kv = line.Split('=');
                        if (kv.Length == 2 && kv[0].Trim().Equals("UseModernTheme", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(kv[1].Trim(), out b)) return b;
                        }
                    }
            }
            catch (Exception) { }
            return DefaultModern;
        }

        static T P<T>(T old, T modern) { return Modern ? modern : old; }
        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        // ---- Masse (Pixel) ----
        public static readonly int HeadH = P(24, 27), LkzH = P(22, 22), LW = P(26, 18), Pad = P(4, 10), Gap = P(4, 10);
        public static readonly int HeadTextX = P(3, 9);      // Abstand der Ueberschrift vom linken Rand
        public static readonly int ArrowSize = P(1, 0);      // 1 = bisheriger Pfeil, 0 = kleiner

        // ---- Farben ----
        public static readonly Color Window = P(SystemColors.Control, C(245, 246, 248));
        public static readonly Color CellBorder = P(C(150, 160, 175), C(214, 218, 225));
        public static readonly Color DayHead = P(C(205, 218, 234), C(228, 233, 241));
        public static readonly Color DayHeadToday = P(C(255, 224, 130), C(255, 232, 160));
        public static readonly Color RowHead = P(C(205, 218, 234), C(238, 241, 246));
        public static readonly Color RowHeadToday = P(C(255, 224, 130), C(255, 240, 200));
        public static readonly Color CellBack = P(Color.White, Color.White);
        public static readonly Color CellBackWeekend = P(C(244, 246, 250), C(250, 251, 253));
        public static readonly Color CellBackToday = P(C(255, 250, 230), C(255, 252, 242));
        public static readonly Color HeadText = P(Color.Black, C(45, 55, 75));
        public static readonly Color HeadTextCollapsed = P(C(70, 70, 70), C(95, 105, 125));
        public static readonly Color Arrow = P(C(70, 80, 100), C(130, 140, 158));
        public static readonly Color DayWeekday = P(Color.Black, C(35, 45, 65));
        public static readonly Color DayDate = P(Color.Black, C(125, 133, 148));
        public static readonly Color DayTodayText = P(Color.Black, C(175, 100, 0));
        public static readonly Color DayLine = P(C(205, 218, 234), C(205, 211, 222));          // nur moderner Look (Linie unter dem Tageskopf)
        public static readonly Color DayLineToday = P(C(255, 224, 130), C(255, 175, 40));
        public static readonly Color Separator = P(C(200, 205, 215), C(230, 233, 238));   // Linie unter der LKZ-Zeile
        public static readonly Color LkzOn = P(C(150, 220, 150), C(214, 238, 218));
        public static readonly Color LkzText = P(Color.Black, C(95, 103, 118));
        public static readonly bool ShiftSep = P(false, true);                          // duenne Trennlinien zwischen den Schichten
        public static readonly Color ShiftLine = P(C(200, 205, 215), C(226, 229, 235));
        public static readonly Color ShiftLabelText = P(Color.Black, C(70, 75, 90));
        public static readonly Color[] ShiftLabel = P(
            new Color[] { C(255, 225, 130), C(255, 190, 130), C(150, 175, 225) },
            new Color[] { C(255, 233, 165), C(255, 211, 170), C(180, 198, 234) });
        public static readonly Color[] ShiftBack = P(
            new Color[] { C(255, 248, 225), C(255, 240, 225), C(232, 238, 250) },
            new Color[] { C(255, 251, 238), C(255, 246, 238), C(242, 246, 253) });

        // Schaltflaechen / Leisten
        public static readonly bool FlatButtons = P(false, true);
        public static readonly Color ButtonBack = P(SystemColors.Control, Color.White);
        public static readonly Color ButtonBorder = P(SystemColors.ControlDark, C(205, 210, 219));
        public static readonly Color ButtonHover = P(SystemColors.Control, C(232, 238, 248));
        public static readonly Color ButtonDown = P(SystemColors.Control, C(213, 224, 242));
        public static readonly Color ButtonOn = P(C(190, 210, 240), C(214, 228, 248));     // Fett/Kursiv aktiv
        public static readonly Color SwatchSelected = P(Color.Black, C(40, 100, 200));      // Auswahlrahmen der Farbfelder
        public static readonly Color SwatchBorder = P(Color.Black, C(205, 210, 219));
        public static readonly Color CheckBorder = C(140, 148, 162);                      // nur moderner Look: eigene Checkbox
        public static readonly Color CheckMark = C(30, 120, 60);

        // ---- Schrift ----
        public static Font UiFont()
        {
            if (Modern)
            {
                Font f = Try("Segoe UI", 9f);
                if (f == null) f = Try("Tahoma", 8.25f);      // Windows XP
                if (f != null) return f;
            }
            return SystemFonts.MessageBoxFont;
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

        // Eigene Checkbox (13x13, GDI+), nur im modernen Look.
        public static void DrawCheck(Graphics g, Point p, bool on)
        {
            Rectangle r = new Rectangle(p.X, p.Y, 12, 12);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
            using (Pen bp = new Pen(on ? CheckMark : CheckBorder)) g.DrawRectangle(bp, r);
            if (!on) return;
            System.Drawing.Drawing2D.SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen cp = new Pen(CheckMark, 2f)) g.DrawLines(cp, new Point[] { new Point(p.X + 3, p.Y + 6), new Point(p.X + 5, p.Y + 8), new Point(p.X + 9, p.Y + 3) });
            g.SmoothingMode = sm;
        }

        // Schaltflaeche im aktuellen Look (alt: unveraendert).
        public static void StyleButton(Button b)
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
    }
}
