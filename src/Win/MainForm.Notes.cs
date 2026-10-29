using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Notizzeilen (Core/Notes.cs): ganze Zeilen des Inhalts, kursiv mit hellem Hintergrund, zaehlen nicht als Inhalt.
    // Ansicht: Rechtsklick auf eine Textzeile -> "Als Notiz markieren" / "Notiz aufheben", gespeichert wie der LKZ-Haken (Change).
    // Editor: gleiche Eintraege im Kontextmenue; die Markierung ist der Hintergrund (SelectionBackColor), ExtractSpans liest ihn.
    partial class MainForm
    {
        int NoteArgb { get { return Theme.Current.NoteBack.ToArgb(); } }

        // vm = Menue der Ansicht, cm = Menue des Editors (aus InitData)
        void InitNotes(ContextMenuStrip vm, ContextMenuStrip cm)
        {
            ToolStripSeparator vsep = new ToolStripSeparator();
            ToolStripItem vOn = new ToolStripMenuItem("Als Notiz markieren", null, delegate { ViewNote(true); });
            ToolStripItem vOff = new ToolStripMenuItem("Notiz aufheben", null, delegate { ViewNote(false); });
            vm.Items.Add(vsep); vm.Items.Add(vOn); vm.Items.Add(vOff);
            vm.Opening += delegate
            {
                CellData c = view.Selected; int p = view.MenuPara;
                string[] ls = c != null && !c.Editing ? c.Cur.Content.Split('\n') : new string[0];
                bool line = p >= 0 && p < ls.Length && ls[p].Trim().Length > 0;
                bool note = line && NoteLineIndex(c.Cur, p);
                vOn.Visible = line && !note; vOff.Visible = line && note; vsep.Visible = line;
                vOn.Enabled = vOff.Enabled = me.Has(Perm.EditText);
            };
            ToolStripItem eOn = cm.Items.Add("Als Notiz markieren", null, delegate { EditorNote(true); });
            ToolStripItem eOff = cm.Items.Add("Notiz aufheben", null, delegate { EditorNote(false); });
            cm.Opening += delegate { eOn.Enabled = eOff.Enabled = me.Has(Perm.EditText); };
        }

        static bool NoteLineIndex(Entry e, int line)
        {
            foreach (int[] n in Notes.NoteLines(e)) if (n[0] == line) return true;
            return false;
        }

        // Zeile im Stand e: bevorzugt dieselbe Nummer, sonst die erste mit gleichem Text (ein anderer PC kann Zeilen verschoben haben); -1 = nicht mehr da
        static int FindLine(Entry e, int para, string text)
        {
            string[] ls = e.Content.Split('\n');
            if (para < ls.Length && ls[para] == text) return para;
            return Array.IndexOf(ls, text);
        }

        // ---------- Ansicht (sofort speichern) ----------

        void ViewNote(bool on)
        {
            CellData c = view.Selected; int p = view.MenuPara;
            if (c == null || c.Editing || p < 0) return;
            if (!me.Has(Perm.EditText))
            {
                if (!quiet) MessageBox.Show("Notizen markieren dürfen nur Nutzer mit dem Recht \"Text ändern\".", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string[] ls = c.Cur.Content.Split('\n');
            if (p >= ls.Length) return;
            string text = ls[p];
            Change(c, delegate(CellData x) { },
                delegate(Entry e) { int i = FindLine(e, p, text); return i < 0 || NoteLineIndex(e, i) == on; },
                delegate(Entry e) { int i = FindLine(e, p, text); if (i >= 0) Notes.SetNote(e, i, i, on); });
        }

        // ---------- Editor ----------

        // Zeilen a..a+n im Editor als Notiz darstellen (Hintergrund + kursiv) bzw. zuruecksetzen; Aufrufer sperrt die Auswahl-Handler.
        void StyleLine(int a, int n, bool on)
        {
            edContent.Select(a, n);
            Font f = edContent.SelectionFont;   // null = gemischt
            string name = f != null ? f.Name : plainFont.Name;
            float size = f != null ? f.SizeInPoints : plainFont.SizeInPoints;
            FontStyle st = f != null ? f.Style : FontStyle.Regular;
            if (f != null) f.Dispose();
            edContent.SelectionBackColor = on ? Theme.Current.NoteBack : edContent.BackColor;
            edContent.SelectionFont = MakeFont(name, size, on ? st | FontStyle.Italic : st & ~FontStyle.Italic);
        }

        void EditorNote(bool on)
        {
            if (editCell == null || !me.Has(Perm.EditText)) return;
            string text = edContent.Text;
            int s0 = edContent.SelectionStart, l0 = edContent.SelectionLength, end = s0 + l0;
            if (l0 > 0 && end > 0 && text[end - 1] == '\n') end--;
            int first = Notes.LineAt(text, s0), last = Notes.LineAt(text, end);
            List<int[]> ls = Notes.Lines(text);
            extracting = true;
            SendMessage(edContent.Handle, WM_SETREDRAW, 0, 0);
            try
            {
                for (int i = first; i <= last && i < ls.Count; i++)
                    if (ls[i][1] > 0 && text.Substring(ls[i][0], ls[i][1]).Trim().Length > 0) StyleLine(ls[i][0], ls[i][1], on);
            }
            finally
            {
                edContent.Select(s0, l0);
                SendMessage(edContent.Handle, WM_SETREDRAW, 1, 0);
                extracting = false;
                edContent.Invalidate();
            }
            fmtChanged = true; UpdateChanged();
            edContent.Focus();
        }

        // Nach "Standardformat"/Kursiv-Schalter: Notizzeilen behalten ihr Aussehen (die Markierung ist der Hintergrund)
        void RestoreNoteLook()
        {
            if (editCell == null) return;
            string text = edContent.Text;
            int s0 = edContent.SelectionStart, l0 = edContent.SelectionLength;
            extracting = true;
            SendMessage(edContent.Handle, WM_SETREDRAW, 0, 0);
            try
            {
                foreach (int[] l in Notes.Lines(text))
                {
                    if (l[1] == 0) continue;
                    edContent.Select(l[0], 1);
                    if (edContent.SelectionBackColor.ToArgb() == NoteArgb) StyleLine(l[0], l[1], true);
                }
            }
            finally
            {
                edContent.Select(s0, l0);
                SendMessage(edContent.Handle, WM_SETREDRAW, 1, 0);
                extracting = false;
                edContent.Invalidate();
            }
        }

        // Enter am Ende einer Notizzeile: neue Zeile ohne Notizformat. true = behandelt.
        bool NoteEnter()
        {
            if (editCell == null || edContent.SelectionLength != 0) return false;
            int p = edContent.SelectionStart; string t = edContent.Text;
            if (p == 0 || t[p - 1] == '\n' || (p < t.Length && t[p] != '\n')) return false;
            extracting = true;
            bool note;
            try { edContent.Select(p - 1, 1); note = edContent.SelectionBackColor.ToArgb() == NoteArgb; edContent.Select(p, 0); }
            finally { extracting = false; }
            if (!note) return false;
            edContent.SelectedText = "\n";
            edContent.SelectionBackColor = edContent.BackColor; edContent.SelectionFont = plainFont; edContent.SelectionColor = Color.Black;
            return true;
        }
    }
}
