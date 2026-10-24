using System;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static class Ui
    {
        public static System.Drawing.Color Rgb(int rgb) { return System.Drawing.Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

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
