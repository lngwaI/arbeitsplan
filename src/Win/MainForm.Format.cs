using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Formatleiste, Kontextmenue des Inhalts, Schrift/Farbe, Einfuegen als Nur-Text, Format aus dem Editor lesen.
    partial class MainForm
    {
        // ---------- Format: Leiste und Kontextmenue (Rechtsklick auf markierten Text) ----------
        // Die Leiste gilt fuer die Markierung; ohne Markierung gilt die Auswahl fuer den danach getippten Text.

        void BuildFormatBar()
        {
            fmtBar.Dock = DockStyle.Top; fmtBar.Height = 32;
            fmtBar.Visible = me.Has(Perm.Format);
            int x = 6;
            Label l = new Label(); l.Text = "Textformat:"; l.AutoSize = true; l.Location = new Point(x, 9); fmtBar.Controls.Add(l); x += 78;
            fmtHint = new Label(); fmtHint.Text = "Textformat (Schrift, Größe, Farbe): erst im Bearbeitungsmodus – Doppelklick auf einen Block"; fmtHint.AutoSize = true; fmtHint.Location = new Point(6, 9); fmtHint.ForeColor = Section.HelpColor; fmtBar.Controls.Add(fmtHint);   // gleiche Hoehe: Plan springt nicht
            fmtLabel = l;
            cbFont.DropDownStyle = ComboBoxStyle.DropDownList; cbFont.SetBounds(x, 5, 130, 22); cbFont.Items.AddRange(FontNames); cbFont.SelectedIndex = 0;
            cbFont.SelectedIndexChanged += delegate { if (!syncingFormat) ChangeFont(delegate(Font f) { return MakeFont(cbFont.SelectedIndex == 0 ? plainFont.Name : cbFont.SelectedItem.ToString(), f.SizeInPoints, f.Style); }); };
            x += 136;
            cbSize.DropDownStyle = ComboBoxStyle.DropDownList; cbSize.SetBounds(x, 5, 52, 22); cbSize.Items.AddRange(Sizes); cbSize.SelectedItem = ((int)Math.Round(plainFont.SizeInPoints)).ToString();
            cbSize.SelectedIndexChanged += delegate { if (!syncingFormat) { float sz = float.Parse(cbSize.SelectedItem.ToString()); ChangeFont(delegate(Font f) { return MakeFont(f.Name, sz, f.Style); }); } };
            x += 58;
            btnBold.Text = "F"; btnBold.Font = new Font("Arial", 9f, FontStyle.Bold); btnBold.SetBounds(x, 4, 26, 24); x += 28;
            btnItalic.Text = "K"; btnItalic.Font = new Font("Arial", 9f, FontStyle.Italic); btnItalic.SetBounds(x, 4, 26, 24); x += 34;
            btnBold.Click += delegate { ToggleStyle(FontStyle.Bold); };
            btnItalic.Click += delegate { ToggleStyle(FontStyle.Italic); };
            fmtBar.Controls.Add(cbFont); fmtBar.Controls.Add(cbSize); fmtBar.Controls.Add(btnBold); fmtBar.Controls.Add(btnItalic);
            foreach (int rgb in Swatches)
            {
                int col = rgb;
                Button b = new Button();
                b.SetBounds(x, 5, 22, 22); x += 24; b.FlatStyle = FlatStyle.Flat; b.TabStop = false;
                b.BackColor = Ui.Rgb(col);
                b.Click += delegate { ApplyColor(col); MarkSwatch(b); };
                if (Theme.Current.FlatButtons) { b.Size = new Size(20, 20); b.Top = 6; b.FlatAppearance.BorderColor = Theme.Current.SwatchBorder; b.FlatAppearance.BorderSize = 1; b.FlatAppearance.MouseOverBackColor = b.BackColor; b.FlatAppearance.MouseDownBackColor = b.BackColor; }
                fmtBar.Controls.Add(b); fmtControls.Add(b);
            }
            Button more = new Button(); more.Text = "Farbe…"; more.SetBounds(x + 4, 4, 62, 24); more.Click += delegate { PickColor(); }; x += 70;
            Button clear = new Button(); clear.Text = "Standardformat"; clear.SetBounds(x, 4, 110, 24); clear.Click += delegate { ClearFormat(); };
            foreach (Button sb in new Button[] { btnBold, btnItalic, more, clear }) Theme.Current.StyleButton(sb);
            fmtBar.Controls.Add(more); fmtBar.Controls.Add(clear);
            fmtControls.AddRange(new Control[] { cbFont, cbSize, btnBold, btnItalic, more, clear });
            SetFormatBar(false);
        }

        Label fmtHint, fmtLabel;

        // Formatleiste nur im Bearbeitungsmodus zeigen (Leiste behaelt ihre Hoehe); sonst ein Hinweis an gleicher Stelle
        void SetFormatBar(bool editing)
        {
            foreach (Control c in fmtControls) { c.Enabled = editing && me.Has(Perm.Format); c.Visible = editing; }
            fmtLabel.Visible = editing; fmtHint.Visible = !editing;
        }

        ContextMenuStrip BuildContentMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Ausschneiden", null, delegate { edContent.Cut(); });
            m.Items.Add("Kopieren", null, delegate { edContent.Copy(); });
            m.Items.Add("Einfügen", null, delegate { PastePlain(); });
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem size = new ToolStripMenuItem("Schriftgröße");
            foreach (string s in Sizes) { string v = s; size.DropDownItems.Add(v, null, delegate { float sz = float.Parse(v); ChangeFont(delegate(Font f) { return MakeFont(f.Name, sz, f.Style); }); }); }
            ToolStripMenuItem fnt = new ToolStripMenuItem("Schriftart");
            foreach (string s in FontNames) { string v = s; fnt.DropDownItems.Add(v, null, delegate { ChangeFont(delegate(Font f) { return MakeFont(v == FontNames[0] ? plainFont.Name : v, f.SizeInPoints, f.Style); }); }); }
            ToolStripMenuItem col = new ToolStripMenuItem("Schriftfarbe");
            string[] names = { "Schwarz", "Rot", "Blau", "Grün", "Orange", "Violett", "Grau" };
            for (int i = 0; i < Swatches.Length; i++)
            {
                int rgb = Swatches[i];
                ToolStripItem it = col.DropDownItems.Add(names[i], null, delegate { ApplyColor(rgb); });
                it.ForeColor = Ui.Rgb(rgb);
            }
            col.DropDownItems.Add("Weitere Farben…", null, delegate { PickColor(); });
            m.Items.Add(size); m.Items.Add(fnt); m.Items.Add(col);
            m.Items.Add("Fett", null, delegate { ToggleStyle(FontStyle.Bold); });
            m.Items.Add("Kursiv", null, delegate { ToggleStyle(FontStyle.Italic); });
            m.Items.Add("Standardformat", null, delegate { ClearFormat(); });
            m.Opening += delegate { bool f = me.Has(Perm.Format); for (int i = 4; i < m.Items.Count; i++) m.Items[i].Enabled = f; };
            return m;
        }

        Font MakeFont(string name, float size, FontStyle style) { return FontCache.Get(name, size, style, plainFont); }

        void ChangeFont(Func<Font, Font> change)
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            Font cur = edContent.SelectionFont ?? plainFont;
            edContent.SelectionFont = change(cur);
            FormatTouched();
            edContent.Focus();
        }

        // Format einer Markierung geaendert -> Block gilt als geaendert (ohne Markierung zaehlt erst der getippte Text)
        void FormatTouched() { if (edContent.SelectionLength > 0) { fmtChanged = true; UpdateChanged(); } }

        void ToggleStyle(FontStyle s)
        {
            ChangeFont(delegate(Font f) { return MakeFont(f.Name, f.SizeInPoints, (f.Style & s) != 0 ? f.Style & ~s : f.Style | s); });
            if (s == FontStyle.Italic) RestoreNoteLook();   // Notizzeilen bleiben kursiv
        }

        Button markedSwatch;
        // Moderner Look: das zuletzt gewaehlte Farbfeld bekommt einen Auswahlrahmen.
        void MarkSwatch(Button b)
        {
            if (!Theme.Current.FlatButtons) return;
            if (markedSwatch != null) { markedSwatch.FlatAppearance.BorderColor = Theme.Current.SwatchBorder; markedSwatch.FlatAppearance.BorderSize = 1; }
            markedSwatch = b;
            b.FlatAppearance.BorderColor = Theme.Current.SwatchSelected; b.FlatAppearance.BorderSize = 2;
        }

        void ApplyColor(int rgb)
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            edContent.SelectionColor = Ui.Rgb(rgb);
            FormatTouched();
            edContent.Focus();
        }

        void PickColor()
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            using (ColorDialog d = new ColorDialog())
                if (d.ShowDialog(this) == DialogResult.OK) { edContent.SelectionColor = d.Color; FormatTouched(); edContent.Focus(); }
        }

        void ClearFormat()
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            edContent.SelectionFont = plainFont; edContent.SelectionColor = Color.Black; RestoreNoteLook(); FormatTouched(); edContent.Focus();   // Notizen bleiben Notizen
        }

        // Zeigt Schrift/Groesse/Fett/Kursiv der Markierung in der Leiste an.
        void SyncFormat()
        {
            if (loadingEditor || extracting) return;
            Font f = edContent.SelectionFont;
            if (f == null) return;
            syncingFormat = true;
            int i = Array.IndexOf(FontNames, f.Name);
            cbFont.SelectedIndex = f.Name == plainFont.Name ? 0 : (i > 0 ? i : 0);
            cbSize.SelectedItem = ((int)Math.Round(f.SizeInPoints)).ToString();
            btnBold.BackColor = f.Bold ? Theme.Current.ButtonOn : Theme.Current.ButtonBack;
            btnItalic.BackColor = f.Italic ? Theme.Current.ButtonOn : Theme.Current.ButtonBack;
            syncingFormat = false;
        }

        void PastePlain() { edContent.Paste(DataFormats.GetFormat(DataFormats.UnicodeText)); }   // fremde Formatierung wird nicht uebernommen

        void OnContentKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && NoteEnter()) { e.SuppressKeyPress = true; return; }   // neue Zeile nach einer Notiz ist keine Notiz
            if (e.Control && (e.KeyCode == Keys.U || e.KeyCode == Keys.E || e.KeyCode == Keys.R || e.KeyCode == Keys.L || e.KeyCode == Keys.J)) { e.SuppressKeyPress = true; return; }
            if (e.Control && (e.KeyCode == Keys.B || e.KeyCode == Keys.I) && !me.Has(Perm.Format)) { e.SuppressKeyPress = true; return; }
            if (e.Control && (e.KeyCode == Keys.B || e.KeyCode == Keys.I)) FormatTouched();
            if (e.Control && (e.KeyCode == Keys.Z || e.KeyCode == Keys.Y)) { fmtChanged = true; UpdateChanged(); }   // Rueckgaengig kann auch Format betreffen
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { PastePlain(); e.SuppressKeyPress = true; }
            if (e.Shift && !e.Control && e.KeyCode == Keys.Enter) { edContent.SelectedText = "\n"; e.SuppressKeyPress = true; }   // wie Enter (kein weicher Zeilenumbruch)
        }

        // Manuelle Formatierung aus dem Editor lesen (Zeichen fuer Zeichen; Standard = kein Bereich).
        List<FormatSpan> ExtractSpans()
        {
            List<FormatSpan> res = new List<FormatSpan>();
            int n = edContent.TextLength;
            if (n == 0) return res;
            int s0 = edContent.SelectionStart, l0 = edContent.SelectionLength;
            extracting = true;
            SendMessage(edContent.Handle, WM_SETREDRAW, 0, 0);
            try { return ExtractSpans(n, res); }
            finally
            {
                edContent.Select(s0, l0);
                SendMessage(edContent.Handle, WM_SETREDRAW, 1, 0);
                extracting = false;
            }
        }

        List<FormatSpan> ExtractSpans(int n, List<FormatSpan> res)
        {
            FormatSpan open = null;
            int noteArgb = Theme.Current.NoteBack.ToArgb();
            int[] stat = new int[n];   // Statusfarbe+1 je Zeichen einer Equipment-Nummer (0 = keine): diese Farbe ist kein manuelles Format
            foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(edContent.Text))
                for (int k = m.Index; k < m.Index + m.Length && k < n; k++) stat[k] = view.StatusRgb(m.Value) + 1;
            for (int i = 0; i <= n; i++)
            {
                FormatSpan a = null;
                if (i < n)
                {
                    edContent.Select(i, 1);
                    Font sf = edContent.SelectionFont, f = sf ?? plainFont;   // Getter erzeugt jedes Mal eine neue Font
                    Color c = edContent.SelectionColor;
                    FormatSpan t = new FormatSpan();
                    t.Rgb = (c.R << 16) | (c.G << 8) | c.B;
                    if (stat[i] == t.Rgb + 1) t.Rgb = 0;
                    t.Size = Math.Abs(f.SizeInPoints - plainFont.SizeInPoints) > 0.05f ? f.SizeInPoints : 0;
                    t.Bold = f.Bold; t.Italic = f.Italic;
                    t.Font = f.Name != plainFont.Name ? f.Name : null;
                    t.Note = edContent.SelectionBackColor.ToArgb() == noteArgb;   // Notizzeile: heller Hintergrund (Normalize setzt kursiv, ganze Zeilen)
                    if (sf != null) sf.Dispose();
                    if (t.Rgb != 0 || t.Size > 0 || t.Bold || t.Italic || t.Font != null || t.Note) a = t;
                }
                bool same = open != null && a != null && open.Rgb == a.Rgb && open.Size == a.Size && open.Bold == a.Bold && open.Italic == a.Italic && open.Font == a.Font && open.Note == a.Note;
                if (open != null && !same) { open.Length = i - open.Start; res.Add(open); open = null; }
                if (open == null && a != null) { a.Start = i; open = a; }
            }
            return res;
        }
    }
}
