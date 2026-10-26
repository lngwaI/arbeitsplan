using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static class Ui
    {
        public static System.Drawing.Color Rgb(int rgb) { return System.Drawing.Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static readonly System.Globalization.CultureInfo De = new System.Globalization.CultureInfo("de-DE");

        // Datum mit Wochentag, z. B. "Mo 13.03.2026".
        public static string DayText(DateTime d) { return d.ToString("ddd dd.MM.yyyy", De); }

        // Gemeinsamer Grundaufbau fester Dialoge: Schrift des Modells, fester Rahmen, mittig ueber dem Hauptfenster.
        public static void Dialog(Form f, int w, int h, bool inTaskbar = true)
        {
            f.Font = Theme.Current.UiFont();
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = f.MinimizeBox = false;
            f.StartPosition = FormStartPosition.CenterParent;
            if (!inTaskbar) f.ShowInTaskbar = false;
            f.ClientSize = new System.Drawing.Size(w, h);
        }

        // Notizblock mit Stift, per GDI+ gezeichnet (keine Bilddatei, keine Sonderzeichen-Schrift noetig).
        public static Bitmap MakePencil()
        {
            Bitmap b = new Bitmap(18, 18);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillRectangle(Brushes.White, 1, 3, 11, 13);
                g.DrawRectangle(Pens.DimGray, 1, 3, 11, 13);
                g.FillRectangle(Brushes.SteelBlue, 1, 1, 11, 3);
                for (int i = 0; i < 3; i++) g.DrawLine(Pens.Gray, 3, 7 + i * 3, 10, 7 + i * 3);
                g.FillPolygon(Brushes.Orange, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
                g.DrawPolygon(Pens.Sienna, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
            }
            return b;
        }

        // Ersetzt das Windows-Standardmenue (Unicode-Steuerzeichen, IME, Lesefolge ...) durch ein schlichtes Bearbeiten-Menue,
        // damit nichts versehentlich verstellt wird. Formatierung gibt es in diesen Feldern nicht (reiner Text).
        public static void SimpleMenu(TextBoxBase t)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            ToolStripItem undo = m.Items.Add("Rückgängig", null, delegate { t.Undo(); });
            m.Items.Add(new ToolStripSeparator());
            ToolStripItem cut = m.Items.Add("Ausschneiden", null, delegate { t.Cut(); });
            ToolStripItem copy = m.Items.Add("Kopieren", null, delegate { t.Copy(); });
            ToolStripItem paste = m.Items.Add("Einfügen", null, delegate { t.Paste(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Alles markieren", null, delegate { t.SelectAll(); });
            m.Opening += delegate
            {
                bool ro = t.ReadOnly, sel = t.SelectionLength > 0;
                undo.Enabled = !ro && t.CanUndo;
                cut.Enabled = !ro && sel;
                copy.Enabled = sel;
                paste.Enabled = !ro && Clipboard.ContainsText();
            };
            t.ContextMenuStrip = m;
        }
    }
}
