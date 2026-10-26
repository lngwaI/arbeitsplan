using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kampagnenstart beim Uebernehmen eines Pakets: Startdatum und erste Laufnummer waehlen; je Lauf steht das vorgeschlagene
    // Datum (Paket-Rhythmus bzw. Abstaende der frueheren Kampagne) und das gewuenschte (eintippen oder -/+). Folgende Zeilen
    // wandern nur mit "Folgende mitverschieben" mit. Je Lauf ist der Block (Raum) waehlbar; das Equipment bleibt gleich.
    class CampaignForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        static readonly Color Orange = Color.FromArgb(255, 228, 190);   // abweichend vom Vorschlag
        readonly Package pkg;
        readonly DateTimePicker dtStart = new DateTimePicker();
        readonly NumericUpDown nFirst = new NumericUpDown();
        readonly Label lFirst = new Label(), info = new Label();
        readonly CheckBox chkShift = new CheckBox(), chkRooms = new CheckBox(), chkRhythm = new CheckBox();
        readonly DataGridView grid = new DataGridView();
        readonly List<int> gaps;
        List<RunDate> sched = new List<RunDate>();
        readonly Dictionary<long, int> offs = new Dictionary<long, int>();      // (Schritt, Lauf) -> Verschiebung in Tagen (gewuenscht - vorgeschlagen)
        readonly Dictionary<long, string> rooms = new Dictionary<long, string>();   // (Schritt, Lauf) -> gewaehlter Block, falls abweichend
        bool filling;

        readonly ComboBox cbMode = new ComboBox();

        public DateTime Start { get { return dtStart.Value.Date; } }
        public ApplyMode Mode { get { return (ApplyMode)cbMode.SelectedIndex; } }

        // Rhythmus der frueheren Kampagne verwenden (nur sichtbar, wenn eine gefunden wurde).
        public bool UseRhythm { get { return chkRhythm.Checked; } set { chkRhythm.Checked = value; } }

        static long K(RunDate d) { return (long)d.Step * 100000 + d.Run; }
        int Off(int i) { int v; return offs.TryGetValue(K(sched[i]), out v) ? v : 0; }
        string Room(int i) { string r; return rooms.TryGetValue(K(sched[i]), out r) ? r : sched[i].Row; }

        // Tag und Block je Lauf inklusive Verschiebungen/Raumwechsel, fuer Package.Apply.
        public List<RunDate> Schedule
        {
            get
            {
                List<RunDate> res = new List<RunDate>();
                for (int i = 0; i < sched.Count; i++)
                {
                    RunDate d = new RunDate(); d.Step = sched[i].Step; d.Run = sched[i].Run; d.Day = sched[i].Day + Off(i); d.Label = sched[i].Label;
                    d.Row = Room(i); d.Group = sched[i].Group;
                    res.Add(d);
                }
                return res;
            }
        }

        public CampaignForm(Package p, IDataStore store, DateTime start, ApplyMode mode)
        {
            pkg = p;
            Text = "Kampagne starten – " + (p.Name.Length > 0 ? p.Name : "Paket");
            Font = Theme.Current.UiFont();
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(720, 540);

            Controls.Add(L("Startdatum (Tag 1):", 10, 14));
            dtStart.Format = DateTimePickerFormat.Short; dtStart.Value = start; dtStart.SetBounds(130, 10, 110, 22); Controls.Add(dtStart);
            Controls.Add(L("Erster Lauf:" + (Package.IsCode4(p.ProductCode) ? "  " + p.ProductCode : ""), 265, 14));
            nFirst.SetBounds(375, 10, 65, 22); nFirst.Minimum = 0; nFirst.Maximum = 9999; Controls.Add(nFirst);
            lFirst.SetBounds(448, 14, 262, 18); Controls.Add(lFirst);

            int first = p.FirstRunNo;
            nFirst.Enabled = first >= 0;
            if (first >= 0) nFirst.Value = first;
            int no, interval; DateTime last;
            if (Package.LastRun(store, p.ProductCode, out no, out last, out interval))
            {
                info.Text = "Im Plan gefunden: letzter Lauf " + p.ProductCode + no.ToString("0000") + " am " + last.ToString("ddd dd.MM.yyyy", De)
                    + (interval > 0 ? ", Abstand meist " + interval + " Tag(e)" : "") + ". Vorschlag: nächste Nummer.";
                if (first >= 0 && no < 9999) nFirst.Value = no + 1;
            }
            else info.Text = Package.IsCode4(p.ProductCode) ? "Keine früheren Läufe dieses Produkts im Plan gefunden." : "Paket ohne Produktcode: Läufe werden einfach durchnummeriert.";
            info.SetBounds(10, 40, 700, 18); Controls.Add(info);

            gaps = Package.PastGaps(store, p.ProductCode);
            chkRhythm.Text = "Rhythmus aus früherer Kampagne übernommen (Abstände " + string.Join(", ", gaps.ConvertAll(delegate(int g) { return g.ToString(); }).ToArray()) + " …)";
            chkRhythm.Checked = chkRhythm.Visible = gaps.Count > 0;
            chkRhythm.SetBounds(10, 60, 700, 22); Controls.Add(chkRhythm);

            chkShift.Text = "Folgende mitverschieben"; chkShift.Checked = true; chkShift.SetBounds(10, 84, 170, 22); Controls.Add(chkShift);
            chkRooms.Text = "Raum für ALLE Folgeläufe ändern"; chkRooms.SetBounds(185, 84, 215, 22); Controls.Add(chkRooms);
            Controls.Add(L("Datum eintippen (z. B. 13.03.) oder − / +.", 405, 87));

            grid.SetBounds(10, 110, 700, 382);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.MultiSelect = false; grid.BackgroundColor = Color.White;
            Col(new DataGridViewTextBoxColumn(), "Schritt / Lauf", 230, true);
            DataGridViewComboBoxColumn cRoom = new DataGridViewComboBoxColumn(); cRoom.FlatStyle = FlatStyle.Flat;
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0 && !cRoom.Items.Contains(r.Name)) cRoom.Items.Add(r.Name);
            foreach (PackageStep s in p.Steps) if (s.Row.Length > 0 && !cRoom.Items.Contains(s.Row)) cRoom.Items.Add(s.Row);   // unbekannter Block bleibt sichtbar
            Col(cRoom, "Block (Raum)", 140, false);
            Col(new DataGridViewTextBoxColumn(), "Vorschlag", 120, true);
            Col(new DataGridViewTextBoxColumn(), "Gewünscht", 120, false);
            Col(new DataGridViewButtonColumn(), "−", 36, true);
            Col(new DataGridViewButtonColumn(), "+", 36, true);
            grid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex >= 4) Shift(e.RowIndex, e.ColumnIndex == 4 ? -1 : 1); };
            grid.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e) { if (e.ColumnIndex == 3) Typed(e.RowIndex); };
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty && grid.CurrentCell.ColumnIndex == 1) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (!filling && e.RowIndex >= 0 && e.ColumnIndex == 1) SetRoom(e.RowIndex, Convert.ToString(grid.Rows[e.RowIndex].Cells[1].Value) ?? "", chkRooms.Checked);
            };
            grid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            Controls.Add(grid);

            Controls.Add(L("Vorhandene Einträge:", 10, 508));   // Uebernahme-Modus wird erst hier gewaehlt (nicht im Paket-Editor)
            cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.SetBounds(135, 504, 250, 22);
            cbMode.Items.AddRange(new object[] { "Nur leere Blöcke füllen", "Vorhandenes überschreiben", "An vorhandenen Text anhängen" });
            cbMode.SelectedIndex = (int)mode; Controls.Add(cbMode);
            Button ok = new Button(); ok.Text = "In Plan übernehmen"; ok.SetBounds(480, 502, 135, 28);
            ok.Click += delegate { grid.EndEdit(); DialogResult = DialogResult.OK; };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(620, 502, 90, 28); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); CancelButton = cancel;

            dtStart.ValueChanged += delegate { Recalc(); };
            nFirst.ValueChanged += delegate { Recalc(); };
            chkRhythm.CheckedChanged += delegate { Recalc(); };
            Recalc();
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }

        void Col(DataGridViewColumn c, string head, int w, bool ro)
        {
            c.HeaderText = head; c.Width = w; c.ReadOnly = ro; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            DataGridViewButtonColumn b = c as DataGridViewButtonColumn;
            if (b != null) { b.Text = head; b.UseColumnTextForButtonValue = true; }
            grid.Columns.Add(c);
        }

        static string Fmt(DateTime d) { return d.ToString("ddd dd.MM.yyyy", De); }

        DateTime Proposed(int i) { return Start.AddDays(sched[i].Day - 1); }

        // Vorschlaege neu berechnen (Start/erste Nummer/Rhythmus geaendert); eigene Verschiebungen und Raeume bleiben je Lauf erhalten.
        void Recalc()
        {
            if (pkg.FirstRunNo >= 0) pkg.Renumber((int)nFirst.Value);
            sched = pkg.Schedule(chkRhythm.Checked ? gaps : null);
            sched.Sort(delegate(RunDate a, RunDate b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : (a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Run.CompareTo(b.Run)); });
            string firstLabel = "";
            foreach (RunDate d in sched) if (pkg.Steps[d.Step].Kind != StepKind.Normal) { firstLabel = d.Label; break; }
            lFirst.Text = firstLabel.Length > 0 ? "→ " + firstLabel : "(keine Läufe im Paket)";
            filling = true;
            grid.Rows.Clear();
            for (int i = 0; i < sched.Count; i++)
            {
                grid.Rows.Add(sched[i].Label, Room(i), Fmt(Proposed(i)), "");
                UpdateRow(i);
            }
            filling = false;
        }

        void UpdateRow(int i)
        {
            bool was = filling; filling = true;
            DataGridViewCell c = grid.Rows[i].Cells[3];
            c.Value = Fmt(Proposed(i).AddDays(Off(i)));
            c.Style.BackColor = Off(i) != 0 ? Orange : Color.Empty;
            DataGridViewCell r = grid.Rows[i].Cells[1];
            r.Value = Room(i);
            r.Style.BackColor = Room(i) != sched[i].Row ? Orange : Color.Empty;   // Raumwechsel
            filling = was;
        }

        // Zeile i um delta Tage verschieben; mit "Folgende mitverschieben" auch alle Zeilen darunter.
        public void Shift(int i, int delta)
        {
            if (delta == 0 || i < 0 || i >= sched.Count) return;
            for (int j = i; j < sched.Count && (j == i || chkShift.Checked); j++) { offs[K(sched[j])] = Off(j) + delta; UpdateRow(j); }
        }

        // Block der Zeile i aendern: gilt fuer alle Zeilen desselben Laufs im selben Raum (z. B. Lauf, Mischen, Backfill),
        // mit "Raum für ALLE Folgeläufe" zusaetzlich fuer alle spaeteren Zeilen dieses Raums.
        public void SetRoom(int i, string room, bool all)
        {
            if (i < 0 || i >= sched.Count || room.Length == 0) return;
            string orig = sched[i].Row; int g = sched[i].Group;
            for (int j = 0; j < sched.Count; j++)
            {
                if (j != i && (sched[j].Row != orig || !((all && j > i) || (g >= 0 && sched[j].Group == g)))) continue;
                if (room == sched[j].Row) rooms.Remove(K(sched[j])); else rooms[K(sched[j])] = room;
                UpdateRow(j);
            }
        }

        // Eingetipptes Datum: "13.03.", "13.03.26" oder "Fr 13.03.2026"; ohne Jahr gilt das bisherige.
        void Typed(int i)
        {
            if (i < 0 || i >= sched.Count) return;
            DateTime cur = Proposed(i).AddDays(Off(i));
            string s = Convert.ToString(grid.Rows[i].Cells[3].Value) ?? "";
            int k = 0; while (k < s.Length && !char.IsDigit(s[k])) k++;
            string[] parts = s.Substring(k).Split('.');
            int d = 0, m = 0, y = cur.Year;
            bool ok = parts.Length >= 2 && int.TryParse(parts[0].Trim(), out d) && int.TryParse(parts[1].Trim(), out m);
            if (ok && parts.Length > 2 && parts[2].Trim().Length > 0) { ok = int.TryParse(parts[2].Trim(), out y); if (y < 100) y += 2000; }
            ok = ok && y >= 2000 && y <= 2099 && m >= 1 && m <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m);
            if (!ok) { System.Media.SystemSounds.Beep.Play(); UpdateRow(i); return; }
            int delta = (new DateTime(y, m, d) - cur).Days;
            if (delta == 0) UpdateRow(i); else Shift(i, delta);
        }
    }
}
