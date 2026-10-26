using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Bearbeitungsmodus: Eingabefelder, LKZ-Haken, Rechte, Speichern mit Konfliktpruefung, Tasten, Schliessen.
    partial class MainForm
    {
        void BuildEditors()
        {
            edContent.Font = plainFont;
            edContent.BorderStyle = BorderStyle.None; edContent.ScrollBars = RichTextBoxScrollBars.None; edContent.DetectUrls = false;
            edContent.HideSelection = false; edContent.AcceptsTab = false;
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

        // ---------- LKZ-Haken ----------

        void ToggleLkz(CellData c)
        {
            bool want = !c.Lkz;                     // Ziel = Gegenteil dessen, was der Nutzer sieht
            if (!c.Editing) FreshCur(c);
            if (!CanEdit(c)) return;
            if (c.Editing) { c.Lkz = want; UpdateChanged(); view.Invalidate(); return; }   // wird mit dem Block gespeichert
            RowInfo ri = rowInfo[c.RowIndex];
            if (c.Cur.Lkz == want) { view.Relayout(); return; }   // anderer PC hat den Haken schon so gesetzt
            Entry n = c.Cur.Clone(); n.Lkz = want;
            try
            {
                bool ok = Audit.Save(store, c.Day, ri, me, n);                 // Haken sofort speichern
                if (!ok)
                {   // anderer PC hat den Block geaendert: dessen Inhalt behalten, nur den Haken setzen (einmal still erneut)
                    Entry latest = store.Get(c.Day, ri.Id);
                    n = latest != null ? latest.Clone() : new Entry(); n.Lkz = want;
                    ok = (latest != null && latest.Lkz == want) || Audit.Save(store, c.Day, ri, me, n);
                }
                if (!ok) { if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden, weil der Block gerade geändert wird. Bitte erneut klicken.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            catch (Exception ex)
            {
                if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden.\n" + ex.Message, "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SetCur(c, n);
            view.Relayout();
        }

        // Bearbeiten erlaubt: Text aendern, oder leerer Block und Text hinzufuegen; sonst Meldung.
        bool CanEdit(CellData c)
        {
            if (me.Has(Perm.EditText) || (c.Cur.IsEmpty && me.Has(Perm.AddText))) return true;
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
            c.Cur = e; c.Content = e.Content; c.People = (string[])e.People.Clone(); c.Lkz = e.Lkz; c.Dirty = true;
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
            if (holder != null)
            {
                MessageBox.Show("Das Fenster " + BlockTitle(c) + " wird gerade von " + holder + " bearbeitet.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            locks.Acquire(c.Day, ri.Id, me.Display);   // Soft-Lock: danach trotzdem bearbeiten und speichern

            editCell = c;
            c.Editing = true; c.Dirty = true;
            view.Selected = c;
            LoadEditors(c);
            foreach (Control f in fmtControls) f.Enabled = me.Has(Perm.Format);
            view.Relayout();
            edContent.Focus();
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
            }
            edContent.Select(edContent.TextLength, 0);
            for (int s = 0; s < 3; s++) { edPpl[s].Text = Win(c.Cur.People[s]); edPpl[s].BackColor = Theme.Current.ShiftBack[s]; }
            loadingEditor = false;
            fmtChanged = false; editChanged = false;
            SyncFormat();
        }

        // Billig (ohne ExtractSpans): weicht der Editor vom Ausgangsstand ab? Format zaehlt ueber fmtChanged.
        void UpdateChanged()
        {
            CellData c = editCell;
            editChanged = c != null && (fmtChanged || c.Lkz != c.Cur.Lkz || c.Content != c.Cur.Content
                || c.People[0] != c.Cur.People[0] || c.People[1] != c.Cur.People[1] || c.People[2] != c.Cur.People[2]);
        }

        // Eingabefelder exakt ueber Inhalt und Kuerzel des Blocks legen (nach jedem Layout).
        void PositionEditors()
        {
            if (editCell == null) { foreach (Control c in editors) c.Visible = false; return; }
            CellData c0 = editCell;
            edContent.SetBounds(c0.RContent.X + PlanView.TP - 3, c0.RContent.Y + PlanView.TV, c0.RContent.Width - 2 * (PlanView.TP - 3) - 6, c0.RContent.Height - 2 * PlanView.TV);
            for (int s = 0; s < 3; s++)
                edPpl[s].SetBounds(c0.RPpl[s].X + PlanView.TP - 2, c0.RPpl[s].Y + PlanView.TV, c0.RPpl[s].Width - 2 * (PlanView.TP - 2), c0.RPpl[s].Height - 2 * PlanView.TV);
            foreach (Control c in editors) c.Visible = true;
        }

        Entry WorkEntry(CellData c)
        {
            Entry n = c.Cur.Clone();
            n.Content = Norm(edContent.Text);
            n.Spans = ExtractSpans();
            for (int s = 0; s < 3; s++) n.People[s] = Norm(edPpl[s].Text);
            n.Lkz = c.Lkz;
            return n;
        }

        bool EntryDirty(CellData c, Entry n)
        {
            return n.Content != c.Cur.Content || n.People[0] != c.Cur.People[0] || n.People[1] != c.Cur.People[1] || n.People[2] != c.Cur.People[2]
                || n.Lkz != c.Cur.Lkz || n.SpanKey() != c.Cur.SpanKey();
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
                    if (!me.Has(Perm.DeleteText) && RemovesText(c.Cur, n))
                    {
                        if (!quiet) MessageBox.Show("Sie dürfen keinen Text löschen (Recht \"Text löschen\" fehlt).\n\nDer Block bleibt im Bearbeitungsmodus: Entfernten Text wieder eintragen oder mit Esc verwerfen.",
                            "Keine Berechtigung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    if (!TrySave(c, ref n)) return false;
                    c.Cur = n;
                }
            }
            locks.Release(c.Day, rowInfo[c.RowIndex].Id, me.Display);
            c.Content = c.Cur.Content; c.People = (string[])c.Cur.People.Clone(); c.Lkz = c.Cur.Lkz;
            c.Editing = false; c.Dirty = true;
            editCell = null; editChanged = fmtChanged = false;
            foreach (Control f in fmtControls) f.Enabled = false;
            view.Relayout();
            return true;
        }

        // Hat ein anderer PC den Block inzwischen gespeichert (Versionskonflikt), wird nie still ueberschrieben: Aenderungen an
        // verschiedenen Stellen fuehrt Conflict.Merge zusammen und speichert; nur bei derselben Stelle fragt ConflictForm.
        // n ist danach das Gespeicherte. false = nicht gespeichert, Block bleibt im Editor (ggf. mit dem Vorschlag).
        bool TrySave(CellData c, ref Entry n)
        {
            RowInfo ri = rowInfo[c.RowIndex];
            Entry bas = c.Cur;   // Stand beim Beginn der Bearbeitung; nach einer Zusammenfuehrung der damals gespeicherte Stand
            try
            {
                for (int round = 0; round < 5; round++)
                {
                    if (Audit.Save(store, c.Day, ri, me, n)) return true;   // mit Protokolleintrag
                    Entry latest = store.Get(c.Day, ri.Id) ?? new Entry();
                    List<string> clashes = new List<string>();
                    Entry merged = Conflict.Merge(bas, n, latest, clashes);
                    if (clashes.Count > 0)
                    {
                        ConflictForm.Choice ch = quiet ? quietChoice : AskConflict(c, latest, n, merged);
                        if (ch == ConflictForm.Choice.Cancel) return false;
                        if (ch == ConflictForm.Choice.Merge) { LoadMerged(c, latest, merged); return false; }
                        merged = n.Clone(); merged.Version = latest.Version;   // bewusst die eigene Fassung (anderes bleibt im Verlauf)
                    }
                    n = merged; bas = latest;
                }
                if (!quiet) MessageBox.Show("Der Block wird gerade laufend von anderen geändert. Bitte gleich noch einmal speichern (Strg+S).", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex) { ShowSaveError(ex); return false; }
        }

        static ConflictForm.Choice quietChoice = ConflictForm.Choice.Merge;   // Selbsttest: Antwort statt Rueckfrage

        ConflictForm.Choice AskConflict(CellData c, Entry latest, Entry mine, Entry merged)
        {
            string who = "Jemand anderes", when = "";
            AuditEntry last = null;
            foreach (AuditEntry a in store.GetAudit(c.Day, rowInfo[c.RowIndex].Id)) if (last == null || a.Time >= last.Time) last = a;
            if (last != null)
            {
                User u = users.FindByLogin(last.Login);
                who = u != null ? u.Display : last.Login.Length > 0 ? last.Login : who;
                when = "um " + last.Time.ToString("HH:mm") + " Uhr";
            }
            using (ConflictForm f = new ConflictForm(BlockTitle(c) + ", " + Ui.DayText(c.Day), who, when, latest, mine, merged))
            {
                f.ShowDialog(this);
                return f.Result;
            }
        }

        // Vorschlag in den Editor; Basis ist jetzt der gespeicherte Stand (das naechste Speichern vergleicht damit)
        void LoadMerged(CellData c, Entry latest, Entry merged)
        {
            c.Cur = merged; LoadEditors(c);
            c.Cur = latest; c.Content = merged.Content; c.People = (string[])merged.People.Clone(); c.Lkz = merged.Lkz;
            fmtChanged = merged.SpanKey() != latest.SpanKey();
            UpdateChanged();
            view.Relayout();
        }

        static void ShowSaveError(Exception ex)
        {
            if (quiet) return;
            MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message +
                "\n\nIhre Änderung ist NICHT verloren und bleibt im Editor sichtbar.\nSpeichern später erneut versuchen (Strg+S) oder mit Esc beenden.",
                "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; return; }
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
