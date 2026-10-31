using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Grosse Vorschau: Kalender (mit Leiste) und Pruefhinweise werden in ein maximiertes modales Fenster umgehaengt (kein zweiter Kalender)
    // und beim Schliessen (X, Esc, Ausnahme) mit altem Platz und alter Reihenfolge zurueckgesetzt. Esc beim Ziehen bricht nur das Ziehen ab.
    partial class PackageForm
    {
        readonly Button bBig = new Button();
        Form big;                       // offen = Kalender/Hinweise sind umgehaengt
        Control bigHome; int bigStripAt, bigHintsAt;

        sealed class BigForm : Form
        {
            public Func<Keys, bool> KeyHook;
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (KeyHook != null && KeyHook(keyData)) return true;
                if (keyData == Keys.Escape) { Close(); return true; }   // beim Ziehen faengt PlanStrip.ProcessCmdKey Esc vorher ab
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        void InitBig()
        {
            bBig.Text = "Groß"; bBig.Tag = "keep"; bBig.TabStop = false; bBig.Font = Font;
            bBig.Click += delegate { if (big != null) big.Close(); else ShowBig(true); };
            tip.SetToolTip(bBig, "Kalender und Prüfhinweise im großen Fenster (Esc schließt, Strg+Z / Strg+Y gelten weiter)");
        }

        // Strg+Z / Strg+Y: Rueckgaengig/Wiederholen (auch aus der grossen Vorschau)
        bool UndoKey(Keys keyData)
        {
            bool redo = keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z);
            if (!(redo || keyData == (Keys.Control | Keys.Z)) || (big == null && TextUndo())) return false;   // im grossen Fenster gibt es keine Textfelder des Editors
            UndoStep(redo ? 1 : -1); return true;
        }

        // modal = false nur fuer Testbilder
        void ShowBig(bool modal)
        {
            if (big != null || stripScroll.Parent == null || hintsCol.Parent != stripScroll.Parent) return;
            BigForm f = new BigForm(); big = f; bigHome = stripScroll.Parent;
            bigStripAt = bigHome.Controls.GetChildIndex(stripScroll); bigHintsAt = bigHome.Controls.GetChildIndex(hintsCol);
            f.Text = "Große Vorschau – " + (tName.Text.Trim().Length > 0 ? tName.Text.Trim() : "Paket"); f.Font = Font; f.KeyHook = UndoKey;
            f.StartPosition = FormStartPosition.Manual; f.Bounds = Screen.FromControl(this).WorkingArea; f.ShowInTaskbar = false;
            if (modal) f.WindowState = FormWindowState.Maximized;
            f.FormClosed += delegate { RestoreBig(); };
            bigHome.SuspendLayout();
            try
            {
                bigHome.Controls.Remove(stripScroll); bigHome.Controls.Remove(hintsCol);
                Panel gap = new Panel(); gap.Dock = DockStyle.Right; gap.Width = 8;
                stripScroll.Dock = DockStyle.Fill; hintsCol.Dock = DockStyle.Right;
                f.Controls.Add(stripScroll); f.Controls.Add(gap); f.Controls.Add(hintsCol);   // Fill zuerst, Raender danach (wie im Editor)
                f.Padding = new Padding(8);
                bBig.Text = "Klein"; tip.SetToolTip(bBig, "Zurück in den Editor (Esc)");
            }
            finally { bigHome.ResumeLayout(true); }
            try { if (modal) f.ShowDialog(this); else f.Show(this); }
            finally { if (modal) { RestoreBig(); f.Dispose(); } }
        }

        void RestoreBig()
        {
            Form f = big;
            if (f == null) return;
            big = null;
            Control home = bigHome;
            home.SuspendLayout();
            try
            {
                f.Controls.Remove(stripScroll); f.Controls.Remove(hintsCol);
                bool stripFirst = bigStripAt < bigHintsAt;   // kleineren Index zuerst einsetzen, dann stimmt SetChildIndex fuer beide
                for (int k = 0; k < 2; k++)
                {
                    bool strip1 = (k == 0) == stripFirst;
                    Control c = strip1 ? (Control)stripScroll : hintsCol;
                    home.Controls.Add(c); home.Controls.SetChildIndex(c, strip1 ? bigStripAt : bigHintsAt);
                }
                stripScroll.Dock = DockStyle.Fill; hintsCol.Dock = DockStyle.Right;
                bBig.Text = "Groß"; tip.SetToolTip(bBig, "Kalender und Prüfhinweise im großen Fenster (Esc schließt, Strg+Z / Strg+Y gelten weiter)");
            }
            finally { home.ResumeLayout(true); }
            UpdatePreview(false);
        }

#if TESTHOOKS
        internal Form TestBig(bool open, bool modal = false) { if (open) ShowBig(modal); else if (big != null) big.Close(); return big; }
        internal bool TestBigKey(Keys k) { return big is BigForm && ((BigForm)big).KeyHook(k); }
        internal int TestStripIndex { get { return stripScroll.Parent != null ? stripScroll.Parent.Controls.GetChildIndex(stripScroll) : -1; } }
        internal int TestHintsIndex { get { return hintsCol.Parent != null ? hintsCol.Parent.Controls.GetChildIndex(hintsCol) : -1; } }
        internal Control TestStripParent { get { return stripScroll.Parent; } }
        internal Control TestHintsParent { get { return hintsCol.Parent; } }
        internal Control TestStripBar { get { return stripBar; } }
#endif
    }
}
