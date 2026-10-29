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
        public static bool Quiet;   // Selbsttest: keine Meldungen
        static readonly Color Orange = Color.FromArgb(255, 228, 190);   // abweichend vom Vorschlag
        readonly Package pkg;
        readonly DateTimePicker dtStart = new DateTimePicker();
        readonly TextBox tFirst = new TextBox();   // komplette 8-stellige erste Laufnummer (z. B. 13570030)
        readonly Label lFirst = new Label(), info = new Label();
        readonly Button ok = new Button();
        readonly CheckBox chkShift = new CheckBox(), chkRooms = new CheckBox(), chkRhythm = new CheckBox();
        readonly DataGridView grid = new DataGridView();
        readonly List<int> gaps;
        List<RunDate> sched = new List<RunDate>();
        readonly Dictionary<long, int> offs = new Dictionary<long, int>();      // (Schritt, Lauf) -> Verschiebung in Tagen (gewuenscht - vorgeschlagen)
        readonly Dictionary<long, string> rooms = new Dictionary<long, string>();   // (Schritt, Lauf) -> gewaehlter Block, falls abweichend
        bool filling;

        readonly ComboBox cbMode = new ComboBox();

        public DateTime Start { get { return dtStart.Value.Date; } }
        public string Warning { get; private set; }   // ungueltige erste Laufnummer oder Ueberlauf > 9999 (sperrt "In Plan übernehmen"); null = ok

        // Erste Laufnummer eintippen (wie der Nutzer), z. B. "13570030"
        public string FirstRunText { get { return tFirst.Text; } set { tFirst.Text = value; } }
        public ApplyMode Mode { get { return (ApplyMode)cbMode.SelectedIndex; } }

        // Rhythmus der frueheren Kampagne verwenden (nur sichtbar, wenn eine gefunden wurde).
        public bool UseRhythm { get { return chkRhythm.Checked; } set { chkRhythm.Checked = value; } }

        int Off(int i) { int v; return offs.TryGetValue(sched[i].Key(), out v) ? v : 0; }
        string Room(int i) { string r; return rooms.TryGetValue(sched[i].Key(), out r) ? r : sched[i].Row; }

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
            if (p.Lauf1.HasValue) start = p.StartFor(p.Lauf1.Value);   // im Paket gespeicherte Vorgabe "Lauf 1 am" -> Startdatum (Tag 1) passend dazu
            Text = "Kampagne starten – " + (p.Name.Length > 0 ? p.Name : "Paket");
            Ui.Dialog(this, 720, 540, false);

            Controls.Add(L("Startdatum (Tag 1):", 10, 14));
            dtStart.Format = DateTimePickerFormat.Custom; dtStart.CustomFormat = "dd.MM.yyyy";   // immer TT.MM.JJJJ (GMP), unabhaengig von der Windows-Einstellung
            dtStart.Value = start; dtStart.SetBounds(130, 10, 110, 22); Controls.Add(dtStart);
            Font big = new Font(Font.FontFamily, Font.SizeInPoints + 2f, FontStyle.Bold);
            Label lf = L("Erste Laufnummer:", 255, 12); lf.Font = big; Controls.Add(lf);
            tFirst.Font = big; tFirst.MaxLength = 8; tFirst.SetBounds(lf.Left + lf.PreferredWidth + 4, 8, 112, tFirst.PreferredHeight); Ui.SimpleMenu(tFirst); Controls.Add(tFirst);
            lFirst.SetBounds(tFirst.Right + 8, 14, 710 - tFirst.Right - 8, 18); lFirst.AutoEllipsis = true; Controls.Add(lFirst);

            int first = p.FirstRunNo;
            tFirst.Enabled = first >= 0;
            tFirst.Text = p.FirstRun;
            string pre = p.Prefix;   // Laufnummern-Praefix XXYY (alte Pakete: Produktcode)
            Package.RunHistory h = Package.History(store, pre, start);   // nur Laeufe vor dem Kampagnenstart
            if (h.Found)
            {
                info.Text = "Im Plan gefunden: letzter Lauf " + Package.FormatRun(pre, h.No) + " am " + Ui.DayText(h.Day)
                    + (h.Interval > 0 ? ", Abstand meist " + h.Interval + " Tag(e)" : "") + ". Vorschlag: nächste Nummer.";
                if (first >= 0) tFirst.Text = Package.FormatRun(pre, h.No + 1);
            }
            else info.Text = pre.Length > 0 ? "Keine früheren Läufe " + pre + "xxxx im Plan gefunden." : "Paket ohne 8-stellige Laufnummer: Läufe werden einfach durchnummeriert.";
            info.SetBounds(10, 40, 700, 18); Controls.Add(info);

            gaps = h.Gaps;
            chkRhythm.Text = "Rhythmus aus früherer Kampagne übernommen (Abstände " + string.Join(", ", gaps.ConvertAll(delegate(int g) { return g.ToString(); }).ToArray()) + " …)";
            chkRhythm.Checked = chkRhythm.Visible = gaps.Count > 0;
            chkRhythm.SetBounds(10, 60, 700, 22); Controls.Add(chkRhythm);

            chkShift.Text = "Folgende mitverschieben"; chkShift.Checked = true; chkShift.SetBounds(10, 84, 170, 22); Controls.Add(chkShift);
            chkRooms.Text = "Raum für ALLE Folgeläufe ändern"; chkRooms.SetBounds(185, 84, 215, 22); Controls.Add(chkRooms);
            Controls.Add(L("Datum als TT.MM.JJJJ eintippen oder − / +.", 405, 87));

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
            ok.Text = "In Plan übernehmen"; ok.SetBounds(480, 502, 135, 28);
            ok.Click += delegate { grid.EndEdit(); DialogResult = DialogResult.OK; };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(620, 502, 90, 28); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); CancelButton = cancel;

            dtStart.ValueChanged += delegate { Recalc(); };
            tFirst.TextChanged += delegate { Recalc(); };
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

        DateTime Proposed(int i) { return Start.AddDays(sched[i].Day - 1); }

        // Vorschlaege neu berechnen (Start/erste Nummer/Rhythmus geaendert); eigene Verschiebungen und Raeume bleiben je Lauf erhalten.
        void Recalc()
        {
            bool valid = pkg.FirstRunNo < 0 || pkg.SetFirstRun(tFirst.Text);   // 8 Ziffern: Praefix + Lauf; Folgelaeufe zaehlen hoch
            pkg.Day1 = Start.DayOfWeek;   // Wiederholung an Wochentagen haengt vom Starttag ab
            string over = pkg.Overflow();
            sched = pkg.Schedule(chkRhythm.Checked ? gaps : null);
            sched.Sort(delegate(RunDate a, RunDate b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : (a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Run.CompareTo(b.Run)); });
            string firstLabel = "";
            foreach (RunDate d in sched) if (pkg.Steps[d.Step].Kind != StepKind.Normal) { firstLabel = d.Label; break; }
            lFirst.Text = !valid ? "bitte 8 Ziffern eingeben (z. B. 13570030)" : over ?? (firstLabel.Length > 0 ? "→ " + firstLabel : "(keine Läufe im Paket)");
            lFirst.ForeColor = valid && over == null ? SystemColors.ControlText : Color.Firebrick;
            Warning = valid ? over : lFirst.Text;
            ok.Enabled = Warning == null;
            filling = true;
            grid.Rows.Clear();
            for (int i = 0; i < sched.Count; i++)
            {
                grid.Rows.Add(sched[i].Label, Room(i), Ui.DayText(Proposed(i)), "");
                UpdateRow(i);
            }
            filling = false;
        }

        void UpdateRow(int i)
        {
            bool was = filling; filling = true;
            DataGridViewCell c = grid.Rows[i].Cells[3];
            c.Value = Ui.DayText(Proposed(i).AddDays(Off(i)));
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
            List<int> idx = new List<int>();
            for (int j = i; j < sched.Count && (j == i || chkShift.Checked); j++) idx.Add(j);
            foreach (int j in idx.ToArray()) foreach (int k in BackupRows(j)) if (!idx.Contains(k)) idx.Add(k);   // Back-Up folgt dem Packen
            foreach (int j in idx) { offs[sched[j].Key()] = Off(j) + delta; UpdateRow(j); }
        }

        // Zeilen der Back-Ups (gleicher Lauf), wenn Zeile j ein Packen-Schritt mit Back-Up ist
        List<int> BackupRows(int j)
        {
            List<int> r = new List<int>();
            for (int k = 0; k < sched.Count; k++)
                if (sched[k].Run == sched[j].Run && pkg.Steps[sched[k].Step].BacksUp(pkg.Steps[sched[j].Step])) r.Add(k);
            return r;
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
                if (room == sched[j].Row) rooms.Remove(sched[j].Key()); else rooms[sched[j].Key()] = room;
                UpdateRow(j);
            }
            foreach (int k in BackupRows(i)) { if (room == sched[k].Row) rooms.Remove(sched[k].Key()); else rooms[sched[k].Key()] = room; UpdateRow(k); }   // Back-Up im selben Raum
        }

#if TESTHOOKS
        public bool TestShiftFollowing { set { chkShift.Checked = value; } }
        public string TestTyped(int i, string s) { grid.Rows[i].Cells[3].Value = s; Typed(i); return Convert.ToString(grid.Rows[i].Cells[3].Value); }
        public string TestCell(int i) { return Convert.ToString(grid.Rows[i].Cells[3].Value); }
        public bool TestStartField() { return Controls.Contains(dtStart) && dtStart.Format == DateTimePickerFormat.Custom && dtStart.CustomFormat == "dd.MM.yyyy"; }
#endif

        // Eingetipptes Datum nur vollstaendig als TT.MM.JJJJ (GMP; auch "Fr 13.03.2026"), sonst Meldung und alter Wert.
        public void Typed(int i)
        {
            if (i < 0 || i >= sched.Count) return;
            DateTime cur = Proposed(i).AddDays(Off(i));
            string s = Convert.ToString(grid.Rows[i].Cells[3].Value) ?? "";
            int k = 0; while (k < s.Length && !char.IsDigit(s[k])) k++;
            DateTime want;
            bool ok = DateTime.TryParseExact(s.Substring(k).Trim(), "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out want) && want.Year >= 2000;
            if (!ok)
            {
                if (!Quiet) MessageBox.Show(this, "Bitte das Datum vollständig als TT.MM.JJJJ eingeben (z. B. 13.03." + cur.Year + ").", "Kampagne starten", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateRow(i); return;
            }
            if (want < Start && !Quiet) MessageBox.Show(this, "Hinweis: " + want.ToString("dd.MM.yyyy") + " liegt vor dem Startdatum der Kampagne.", "Kampagne starten", MessageBoxButtons.OK, MessageBoxIcon.Information);
            int y = want.Year, m = want.Month, d = want.Day;
            int delta = (new DateTime(y, m, d) - cur).Days;
            if (delta == 0) UpdateRow(i); else Shift(i, delta);
        }
    }
}
