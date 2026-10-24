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
        public static readonly int HeadH = P(24, 27), LkzH = P(22, 22), LW = P(26, 18), Pad = P(4, 4), Gap = P(4, 4);
        public static readonly int HeadTextX = P(3, 9);      // Abstand der Ueberschrift vom linken Rand
        public static readonly int ArrowSize = P(1, 0);      // 1 = bisheriger Pfeil, 0 = kleiner

        // ---- Farben ----
        public static readonly Color Window = P(SystemColors.Control, SystemColors.Control);
        public static readonly Color CellBorder = P(C(150, 160, 175), C(150, 160, 175));
        public static readonly Color DayHead = P(C(205, 218, 234), C(228, 233, 241));
        public static readonly Color DayHeadToday = P(C(255, 224, 130), C(255, 232, 160));
        public static readonly Color RowHead = P(C(205, 218, 234), C(238, 241, 246));
        public static readonly Color RowHeadToday = P(C(255, 224, 130), C(255, 240, 200));
        public static readonly Color CellBack = P(Color.White, Color.White);
        public static readonly Color CellBackWeekend = P(C(244, 246, 250), C(244, 246, 250));
        public static readonly Color CellBackToday = P(C(255, 250, 230), C(255, 250, 230));
        public static readonly Color HeadText = P(Color.Black, C(45, 55, 75));
        public static readonly Color HeadTextCollapsed = P(C(70, 70, 70), C(95, 105, 125));
        public static readonly Color Arrow = P(C(70, 80, 100), C(130, 140, 158));
        public static readonly Color DayWeekday = P(Color.Black, C(35, 45, 65));
        public static readonly Color DayDate = P(Color.Black, C(125, 133, 148));
        public static readonly Color DayTodayText = P(Color.Black, C(175, 100, 0));
        public static readonly Color DayLine = P(C(205, 218, 234), C(205, 211, 222));          // nur moderner Look (Linie unter dem Tageskopf)
        public static readonly Color DayLineToday = P(C(255, 224, 130), C(255, 175, 40));
        public static readonly Color Separator = P(C(200, 205, 215), C(200, 205, 215));   // Linie unter der LKZ-Zeile
        public static readonly Color LkzOn = P(C(150, 220, 150), C(150, 220, 150));
        public static readonly Color LkzText = P(Color.Black, Color.Black);
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
        public static readonly bool FlatButtons = P(false, false);
        public static readonly Color ButtonBack = P(SystemColors.Control, SystemColors.Control);
        public static readonly Color ButtonBorder = P(SystemColors.ControlDark, SystemColors.ControlDark);
        public static readonly Color ButtonHover = P(SystemColors.Control, SystemColors.Control);
        public static readonly Color ButtonDown = P(SystemColors.Control, SystemColors.Control);
        public static readonly Color ButtonOn = P(C(190, 210, 240), C(190, 210, 240));     // Fett/Kursiv aktiv
        public static readonly Color SwatchSelected = P(Color.Black, Color.Black);

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
