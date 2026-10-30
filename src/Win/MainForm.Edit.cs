using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Bearbeitungsmodus: Eingabefelder (Haken/Variante: MainForm.Checks.cs), Rechte, Speichern mit Konfliktpruefung, Tasten, Schliessen.
    partial class MainForm
    {
        void BuildEditors()
        {
            edContent.Font = plainFont;
            edContent.BorderStyle = BorderStyle.None; edContent.ScrollBars = RichTextBoxScrollBars.None; edContent.DetectUrls = false;
            edContent.HideSelection = false; edContent.AcceptsTab = false; WordBreak.Apply(edContent);
            edContent.TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.Content = Norm(edContent.Text); editCell.Dirty = true; UpdateChanged(); view.RelayoutCell(editCell); } };
            edContent.SelectionChanged += delegate { if (!extracting) SyncFormat(); };
            edContent.KeyDown += OnContentKey;
            edContent.ContextMenuStrip = BuildContentMenu();
            edContent.Visible = false; view.Controls.Add(edContent); editors.Add(edContent);
            for (int s = 0; s < 3; s++)
            {
                int si = s;
                TextBox t = edPpl[s];
                t.BorderStyle = BorderStyle.None; t.AutoSize = false; t.Multiline = true; t.AcceptsReturn = true; t.Visible = false;
                Ui.SimpleMenu(t);          // schlichtes Menue statt Windows-Standard (keine Unicode-/IME-Eintraege)
                t.TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.People[si] = Norm(t.Text); editCell.Dirty = true; UpdateChanged(); view.RelayoutCell(editCell); } };
                view.Controls.Add(t); editors.Add(t);
            }
        }

        // Bearbeiten erlaubt: Text aendern, oder leerer Block und Text hinzufuegen; sonst Meldung.
        bool CanEdit(CellData c)
        {
            if (me.Has(Perm.EditText) || (c.Cur.IsEmpty && c.Cur.Buffers.Count == 0 && !c.Cur.HasNotes && me.Has(Perm.AddText))) return true;
            MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        // Ueberschrift des Blocks; ohne Namen "Block <n>".
        string BlockTitle(CellData c)
        {
            RowInfo ri = rowInfo[c.RowIndex];
            return ri.Name.Length > 0 ? ri.Name : "Block " + (c.RowIndex + 1);
        }

        void SetCur(CellData c, Entry e)
        {
            c.Cur = e; c.Show(e);
        }

        // Hat ein anderer PC seit dem letzten Rebuild Daten geaendert, den Block aus dem aktuellen Stand nehmen
        // (die uebrige Ansicht baut der Abfrage-Timer neu auf, sobald nicht bearbeitet wird).
        void FreshCur(CellData c)
        {
            if (fstore == null) return;
            fstore.Refresh();
            if (fstore.Generation == viewGen) return;
            SetCur(c, store.Get(c.Day, rowInfo[c.RowIndex].Id) ?? new Entry());
        }

        // ---------- Bearbeitungsmodus (Soft-Lock) ----------

        void BeginEdit(CellData c)
        {
            if (editCell == c) return;
            if (!EndEdit(true)) return;
            FreshCur(c);                           // nie auf veraltetem Stand bearbeiten
            if (!CanEdit(c)) return;

            RowInfo ri = rowInfo[c.RowIndex];
            string holder = locks.Holder(c.Day, ri.Id, me.Display);
            if (holder != null && !AskLocked(c, holder)) return;   // Hinweis: trotzdem bearbeiten, Bearbeitung uebernehmen oder abbrechen (MainForm.TakeOver.cs)
            locks.AcquireAs(c.Day, ri.Id, me.Display, me.Login);   // Soft-Lock: danach trotzdem bearbeiten und speichern
            editStart = DateTime.Now;

            editCell = c;
            c.Editing = true; c.Dirty = true;
            view.Selected = c;
            LoadEditors(c);
            SetFormatBar(true);
            view.Relayout();
            edContent.Focus();
        }

        // Zeilenhoehe des Editors = Zeilenhoehe der Ansicht (Font.Height): PARAFORMAT2, Regel 4 = exakt (Twips), fuer den ganzen Text
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct ParaFmt2
        {
            public int cbSize, dwMask; public short wNumbering, wEffects; public int dxStartIndent, dxRightIndent, dxOffset;
            public short wAlignment, cTabCount;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 32)] public int[] rgxTabs;
            public int dySpaceBefore, dySpaceAfter, dyLineSpacing; public short sStyle; public byte bLineSpacingRule, bOutlineLevel;
            public short wShadingWeight, wShadingStyle, wNumberingStart, wNumberingStyle, wNumberingTab, wBorderSpace, wBorderWidth, wBorders;
        }
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref ParaFmt2 l);

        static void ExactLineSpacing(RichTextBox rb, int px)
        {
            float dpi; using (Graphics g = rb.CreateGraphics()) dpi = g.DpiY;
            ParaFmt2 pf = new ParaFmt2();
            pf.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ParaFmt2));
            pf.dwMask = 0x100;   // PFM_LINESPACING
            pf.dyLineSpacing = (int)Math.Round(px * 1440.0 / dpi); pf.bLineSpacingRule = 3;   // 3 = mindestens (grosse Schrift wird nicht abgeschnitten)
            int ss = rb.SelectionStart, sl = rb.SelectionLength;
            rb.SelectAll();
            SendMessage(rb.Handle, 0x447, IntPtr.Zero, ref pf);   // EM_SETPARAFORMAT (alle Absaetze)
            rb.Select(ss, sl);
        }

        void LoadEditors(CellData c)
        {
            loadingEditor = true;
            Color back = Theme.Current.CellBackFor(c.Day);
            edContent.BackColor = back;
            edContent.Text = c.Cur.Content;
            edContent.SelectAll(); edContent.SelectionFont = plainFont; edContent.SelectionColor = Color.Black;
            foreach (FormatSpan sp in c.Cur.Spans)
            {
                if (sp.Start < 0 || sp.Start + sp.Length > edContent.TextLength) continue;
                edContent.Select(sp.Start, sp.Length);
                edContent.SelectionFont = PlanView.SpanFont(sp, plainFont);
                edContent.SelectionColor = Ui.Rgb(sp.Rgb);
                if (sp.Note) edContent.SelectionBackColor = Theme.Current.NoteBack;   // Notizzeile (ExtractSpans liest den Hintergrund)
            }
            foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(edContent.Text))   // Statusfarbe wie in der Ansicht (beim Speichern nicht als Format uebernommen, siehe ExtractSpans)
            {
                edContent.Select(m.Index, m.Length);
                edContent.SelectionColor = Ui.Rgb(view.StatusRgb(m.Value));
            }
            int tw = c.RContent.Width - 2 * PlanView.TP, pos = 0;   // haengende Einrueckung wie in der Ansicht ("Kurzname:" -> Folgezeilen eingerueckt)
            foreach (string para in edContent.Text.Split('\n'))
            {
                int ind = TextBlock.Hanging(para, plainFont, tw);
                if (ind > 0) { edContent.Select(pos, para.Length); edContent.SelectionIndent = 0; edContent.SelectionHangingIndent = ind; }
                pos += para.Length + 1;
            }
            ExactLineSpacing(edContent, plainFont.Height);
            edContent.Select(edContent.TextLength, 0);
            bool task = rowInfo[c.RowIndex].Mode == ShiftMode.Task;
            for (int s = 0; s < 3; s++) { edPpl[s].Text = Win(c.Cur.People[s]); edPpl[s].BackColor = task ? Theme.Current.TaskBack : Theme.Current.ShiftBack[s]; }
            loadingEditor = false;
            fmtChanged = false; editChanged = false;
            SyncFormat();
        }

        // Billig (ohne ExtractSpans): weicht der Editor vom Ausgangsstand ab? Format zaehlt ueber fmtChanged.
        void UpdateChanged()
        {
            CellData c = editCell;
            editChanged = c != null && (fmtChanged || c.Lkz != c.Cur.Lkz || c.Content != c.Cur.Content || BufferSet.Key(c.Buffers) != BufferSet.Key(c.Cur.Buffers)
                || string.Join("|", c.Marks.ToArray()) != c.Cur.MarksKey()
                || c.People[0] != c.Cur.People[0] || c.People[1] != c.Cur.People[1] || c.People[2] != c.Cur.People[2]);
        }

        // Eingabefelder exakt ueber Inhalt und Kuerzel des Blocks legen (nach jedem Layout).
        void PositionEditors()
        {
            if (editCell == null) { foreach (Control c in editors) c.Visible = false; return; }
            CellData c0 = editCell;
            edContent.SetBounds(c0.RContent.X + PlanView.TP - 1, c0.RContent.Y + PlanView.TV, c0.RContent.Width - 2 * (PlanView.TP - 3) - 8, c0.RContent.Height - 2 * PlanView.TV);
            for (int s = 0; s < 3; s++)
                edPpl[s].SetBounds(c0.RPpl[s].X + PlanView.TP - 2, c0.RPpl[s].Y + PlanView.TV, c0.RPpl[s].Width - 2 * (PlanView.TP - 2), c0.RPpl[s].Height - 2 * PlanView.TV);
            edContent.Visible = true;
            for (int s = 0; s < 3; s++) edPpl[s].Visible = c0.RPpl[s].Height > 0;   // ausgeblendete Schicht: kein Editor (Text bleibt im Block)
        }

        Entry WorkEntry(CellData c)
        {
            Entry n = c.Cur.Clone();
            n.Content = Norm(edContent.Text);
            n.Spans = ExtractSpans();
            foreach (FormatSpan f in n.Spans)   // ursprüngliches Kursiv der Notizzeilen bleibt erhalten (Editor zeigt Notiz immer kursiv)
                if (f.Note) { FormatSpan o = c.Cur.Spans.Find(delegate(FormatSpan q) { return q.Note && q.NoteWasItalic && q.Start <= f.Start && f.Start < q.Start + q.Length; }); f.NoteWasItalic = o != null; }
            Notes.Normalize(n);   // Notizmarkierung auf ganze Zeilen, kursiv
            for (int s = 0; s < 3; s++) n.People[s] = Norm(edPpl[s].Text);
            n.Lkz = c.Lkz; n.Buffers = BufferSet.CloneAll(c.Buffers); n.Marks = new List<string>(c.Marks);
            return n;
        }

        bool EntryDirty(CellData c, Entry n)
        {
            return n.Content != c.Cur.Content || n.People[0] != c.Cur.People[0] || n.People[1] != c.Cur.People[1] || n.People[2] != c.Cur.People[2]
                || n.Lkz != c.Cur.Lkz || BufferSet.Key(n.Buffers) != BufferSet.Key(c.Cur.Buffers) || n.MarksKey() != c.Cur.MarksKey() || n.SpanKey() != c.Cur.SpanKey();
        }

        static bool quiet;   // Selbsttest: keine Meldungsfenster

        // Loeschen = Inhalt kuerzer, ODER im Inhalt verschwinden mehr alte Woerter als neue dazukommen (Ersetzen 1:1 bleibt erlaubt,
        // Ueberschreiben mit Leerzeichen/Zusammenziehen nicht), ODER ein Kuerzel gegenueber dem gespeicherten Stand entfernt.
        static bool RemovesText(Entry old, Entry n)
        {
            if (n.Content.Length < old.Content.Length) return true;
            List<string> added = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in PersonPlan.Token.Matches(n.Content)) added.Add(m.Value);
            int lost = 0;
            foreach (System.Text.RegularExpressions.Match m in PersonPlan.Token.Matches(old.Content)) if (!added.Remove(m.Value)) lost++;
            if (lost > added.Count) return true;   // added = nur noch die neu hinzugekommenen Woerter
            for (int s = 0; s < 3; s++)
            {
                List<string> now = new List<string>();
                foreach (System.Text.RegularExpressions.Match m in PersonPlan.Token.Matches(n.People[s])) now.Add(m.Value.ToUpperInvariant());
                foreach (System.Text.RegularExpressions.Match m in PersonPlan.Token.Matches(old.People[s])) if (!now.Contains(m.Value.ToUpperInvariant())) return true;
            }
            return false;
        }

        // Fenster-Icon orange, solange der Block Aenderungen hat (+1/-1 = unveraendert). Nur das Flag, kein ExtractSpans (Icon-Timer 200 ms).
        public bool HasUnsavedChanges { get { return editCell != null && editChanged; } }

        // false = Speichern fehlgeschlagen; Block bleibt im Bearbeitungsmodus, Text bleibt erhalten.
        bool EndEdit(bool save)
        {
            CellData c = editCell;
            if (c == null) return true;
            if (save)
            {
                Entry n = WorkEntry(c);
                if (EntryDirty(c, n))
                {
                    string bmiss = BufferRightMissing(c.Cur, n.Buffers);   // Puffer: Hinzufuegen/Aendern/Entfernen je nach Recht
                    if (bmiss != null)
                    {
                        if (!quiet) MessageBox.Show("Für die Änderung an den Puffern fehlt das Recht \"" + bmiss + "\".\n\nDer Block bleibt im Bearbeitungsmodus.", "Keine Berechtigung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    if (!me.Has(Perm.DeleteText) && RemovesText(c.Cur, n))
                    {
                        if (!quiet) MessageBox.Show("Sie dürfen keinen Text löschen (Recht \"Text löschen\" fehlt).\n\nDer Block bleibt im Bearbeitungsmodus: Entfernten Text wieder eintragen oder mit Esc verwerfen.",
                            "Keine Berechtigung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    List<string> eqWarn = new List<string>(), eqErr = store.GetCatalog().CheckText(n.Content, eqWarn);   // Zubehoer/Hauptgeraet
                    if (eqErr.Count > 0)
                    {
                        string t = "Equipment passt nicht zusammen:\n\n" + string.Join("\n", eqErr.ToArray());
                        bool admin = me.Has(Perm.PackageAdmin);   // nur "Pakete einrichten" darf trotzdem speichern
                        if (!admin) { if (!quiet) MessageBox.Show(t + "\n\nDer Block bleibt im Bearbeitungsmodus.", "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
                        if (!quiet && MessageBox.Show(t + "\n\nTrotzdem speichern?", "Equipment", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
                    }
                    if (!TrySave(c, ref n)) return false;
                    if (eqWarn.Count > 0) Status("Hinweis: " + string.Join(" ", eqWarn.ToArray()), true);   // Produkt-Freigabe nur als Hinweis
                    c.Cur = n;
                }
            }
            locks.Release(c.Day, rowInfo[c.RowIndex].Id, me.Display);
            c.Show(c.Cur);
            c.Editing = false;
            editCell = null; editChanged = fmtChanged = false;
            SetFormatBar(false);
            ComputeChecks();   // Pflichtpunkt-Zustaende nach dem Speichern
            view.Relayout();
            return true;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; return; }
            if (editCell == null && e.KeyCode == Keys.Escape && view.Selected != null) { view.Selected = null; view.Invalidate(); return; }   // Esc hebt die Auswahl auf
            if (editCell == null) return;
            if (e.Control && e.KeyCode == Keys.S) { EndEdit(true); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                if (!HasUnsavedChanges) { EndEdit(false); return; }
                DialogResult r = MessageBox.Show("Änderungen speichern?", "Arbeitsplan", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) EndEdit(true);          // schlaegt Speichern fehl, bleibt der Block im Editor
                else if (r == DialogResult.No) EndEdit(false);
            }
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (EndEdit(true)) return;
            if (MessageBox.Show("Nicht gespeicherte Änderung verwerfen und schließen?", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                EndEdit(false);
            else e.Cancel = true;
        }
    }
}
