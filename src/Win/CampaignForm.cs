using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kampagnenstart beim Uebernehmen eines Pakets: "Lauf 1 am" (Datum), Laufanzahl, erste Laufnummer und die Laufnummer des Packens (von Hand,
    // Pflicht) waehlen; je Zeile steht das vorgeschlagene Datum (Paket-Rhythmus bzw. Abstaende der frueheren Kampagne), der Bezug ("3 Tage vor
    // Lauf 1") und das gewuenschte Datum (eintippen oder -/+). "faellt aus" nimmt einen Lauf heraus (Luecke bei Laufnummer und Kennung).
    // Das Paket wird kopiert (Result): Laufanzahl, Nummern und Ausfaelle aendern nie die Datei. Einzelpaket: einfacher Modus (Datum, Raum, Nummer).
    class CampaignForm : Form
    {
        public static bool Quiet;   // Selbsttest: keine Meldungen
        static readonly Color Orange = Color.FromArgb(255, 228, 190);   // abweichend vom Vorschlag
        static readonly Color Belegt = Color.FromArgb(255, 205, 205);   // Zeile vor Lauf 1 trifft einen belegten Block
        const int cLabel = 0, cRel = 1, cRoom = 2, cProp = 3, cWant = 4, cMinus = 5, cPlus = 6, cSkip = 7;
        readonly Package pkg;      // Kopie des Pakets: Laufanzahl, Nummern, Ausfaelle
        readonly IDataStore store;
        readonly bool simple, needPack;
        readonly ApplyMode initialMode;
        readonly DateTimePicker dtLauf1 = new DateTimePicker();
        readonly NumericUpDown nRuns = new NumericUpDown();
        readonly TextBox tFirst = new TextBox(), tPack = new TextBox();   // tFirst: komplette 8-stellige erste Laufnummer; tPack: Nummer des Packens von Hand
        readonly Label lFirst = new Label(), lPack = new Label(), info = new Label(), lSum = new Label();
        readonly ComboBox cbRoom = new ComboBox(), cbMode = new ComboBox();
        readonly Button ok = new Button();
        readonly CheckBox chkShift = new CheckBox(), chkRooms = new CheckBox(), chkRhythm = new CheckBox();
        readonly DataGridView grid = new DataGridView();
        readonly List<int> gaps;
        readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        List<RunDate> sched = new List<RunDate>();
        List<RunDate> effHead = new List<RunDate>();   // stattfindende Hauptlaeufe mit Verschiebung (Bezug "n Tage vor Lauf 1")
        bool[] skipped = new bool[0];
        readonly Dictionary<long, int> offs = new Dictionary<long, int>();      // (Schritt, Lauf) -> Verschiebung in Tagen (gewuenscht - vorgeschlagen)
        readonly Dictionary<long, string> rooms = new Dictionary<long, string>();   // (Schritt, Lauf) -> gewaehlter Block, falls abweichend
        bool filling, building = true, modeTouched;
        int firstRunDay = int.MaxValue;
        Font strike;

        // Tag 1 des Pakets (alle Rechnungen laufen ueber Tag-Nummern ab hier), aus "Lauf 1 am"
        public DateTime Start { get { return pkg.StartFor(dtLauf1.Value.Date); } }
        public string Warning { get; private set; }       // ungueltige erste Laufnummer oder Ueberlauf > 9999; null = ok
        public string PackWarning { get; private set; }   // Laufnummer Packen fehlt/ungueltig; null = ok. Beide sperren "In Plan übernehmen".
        public Package Result { get { return pkg; } }     // das Paket mit Laufanzahl, Nummern und Ausfaellen: damit uebernehmen (Package.Apply)
        public ApplyMode Mode { get { return (ApplyMode)cbMode.SelectedIndex; } }

        // Erste Laufnummer eintippen (wie der Nutzer), z. B. "13570030"
        public string FirstRunText { get { return tFirst.Text; } set { tFirst.Text = value; } }

        // Rhythmus der frueheren Kampagne verwenden (nur sichtbar, wenn eine gefunden wurde).
        public bool UseRhythm { get { return chkRhythm.Checked; } set { chkRhythm.Checked = value; } }

        int Off(int i) { int v; return offs.TryGetValue(sched[i].Key(), out v) ? v : 0; }
        string Room(int i) { string r; return rooms.TryGetValue(sched[i].Key(), out r) ? r : sched[i].Row; }
        int HeadIdx() { return pkg.Steps.IndexOf(pkg.Head); }

        // Tag und Block je Lauf inklusive Verschiebungen/Raumwechsel, fuer Package.Apply (ausgefallene Laeufe fehlen).
        public List<RunDate> Schedule
        {
            get
            {
                List<RunDate> res = new List<RunDate>();
                for (int i = 0; i < sched.Count; i++)
                {
                    if (skipped[i]) continue;
                    RunDate d = new RunDate(); d.Step = sched[i].Step; d.Run = sched[i].Run; d.Day = sched[i].Day + Off(i); d.Label = sched[i].Label;
                    d.Row = Room(i); d.Group = sched[i].Group;
                    res.Add(d);
                }
                return res;
            }
        }

        public CampaignForm(Package p, IDataStore store, DateTime start, ApplyMode mode)
        {
            this.store = store; initialMode = mode;
            pkg = p.Clone();
            DateTime? saved = pkg.Lauf1; pkg.Lauf1 = null;
            simple = pkg.Type == PackageType.Einzel;
            needPack = pkg.HandNoSteps().Count > 0;
            if (!saved.HasValue) pkg.Day1 = start.DayOfWeek;   // Wiederholung an Wochentagen haengt vom Starttag ab
            DateTime lauf1 = saved.HasValue ? saved.Value.Date : start.Date.AddDays(pkg.Lauf1Day - 1);   // im Paket gespeicherte Vorgabe "Lauf 1 am" hat Vorrang
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0 && !ids.ContainsKey(r.Name)) ids[r.Name] = r.Id;
            Text = "Kampagne starten – " + (p.Name.Length > 0 ? p.Name : "Paket");
            Ui.Dialog(this, 900, 590, false);
            strike = new Font(Font, FontStyle.Strikeout);

            Controls.Add(L(simple ? "Datum:" : "Lauf 1 am:", 10, 14));
            dtLauf1.Format = DateTimePickerFormat.Custom; dtLauf1.CustomFormat = "dd.MM.yyyy";   // immer TT.MM.JJJJ (GMP), unabhaengig von der Windows-Einstellung
            dtLauf1.Value = lauf1; dtLauf1.SetBounds(simple ? 70 : 85, 10, 110, 22); Controls.Add(dtLauf1);
            Font big = new Font(Font.FontFamily, Font.SizeInPoints + 2f, FontStyle.Bold);
            Label ln = L("Läufe:", 200, 14); Controls.Add(ln);
            nRuns.Minimum = 1; nRuns.Maximum = PackageStep.MaxRuns; nRuns.Value = Math.Max(1, Math.Min(PackageStep.MaxRuns, pkg.RunCount));
            nRuns.Enabled = pkg.Head != null; nRuns.SetBounds(ln.Left + ln.PreferredWidth + 4, 10, 52, 22); Controls.Add(nRuns);
            Label lf = L("Erste Laufnummer:", nRuns.Right + 18, 12); lf.Font = big; Controls.Add(lf);
            tFirst.Font = big; tFirst.MaxLength = 8; tFirst.SetBounds(lf.Left + lf.PreferredWidth + 4, 8, 112, tFirst.PreferredHeight); Ui.SimpleMenu(tFirst); Controls.Add(tFirst);
            lFirst.SetBounds(tFirst.Right + 8, 14, 890 - tFirst.Right - 8, 18); lFirst.AutoEllipsis = true; Controls.Add(lFirst);
            if (simple) { ln.Visible = nRuns.Visible = lf.Visible = tFirst.Visible = lFirst.Visible = false; }

            int first = pkg.FirstRunNo;
            tFirst.Enabled = first >= 0;
            tFirst.Text = pkg.FirstRun;
            string pre = pkg.Prefix;   // Laufnummern-Praefix XXYY (alte Pakete: Produktcode)
            Package.RunHistory h = Package.History(store, pre, Start);   // nur Laeufe vor dem Kampagnenstart
            if (h.Found)
            {
                info.Text = "Im Plan gefunden: letzter Lauf " + Package.FormatRun(pre, h.No) + " am " + Ui.DayText(h.Day)
                    + (h.Interval > 0 ? ", Abstand meist " + h.Interval + " Tag(e)" : "") + ". Vorschlag: nächste Nummer.";
                if (first >= 0) tFirst.Text = Package.FormatRun(pre, h.No + 1);
            }
            else info.Text = pre.Length > 0 ? "Keine früheren Läufe " + pre + "xxxx im Plan gefunden." : "Paket ohne 8-stellige Laufnummer: Läufe werden einfach durchnummeriert.";

            int y = 42;
            if (needPack)
            {
                Label lp = L("Laufnummer Packen (von Hand):", 10, y + 2); lp.Font = big; Controls.Add(lp);
                tPack.Font = big; tPack.MaxLength = 8; tPack.SetBounds(lp.Left + lp.PreferredWidth + 4, y - 2, 112, tPack.PreferredHeight); Ui.SimpleMenu(tPack); Controls.Add(tPack);   // kein Vorschlag: immer von Hand
                lPack.SetBounds(tPack.Right + 8, y + 4, 890 - tPack.Right - 8, 18); lPack.AutoEllipsis = true; Controls.Add(lPack);
                y += 34;
            }
            info.SetBounds(10, y, 880, 18); Controls.Add(info); info.Visible = !simple; y += 20;

            gaps = h.Gaps;
            chkRhythm.Text = "Rhythmus aus früherer Kampagne übernommen (Abstände " + string.Join(", ", gaps.ConvertAll(delegate(int g) { return g.ToString(); }).ToArray()) + " …)";
            chkRhythm.Checked = chkRhythm.Visible = gaps.Count > 0 && !simple;
            chkRhythm.SetBounds(10, y, 880, 22); Controls.Add(chkRhythm); if (chkRhythm.Visible) y += 24;

            chkShift.Text = "Folgende mitverschieben"; chkShift.Checked = true; chkShift.SetBounds(10, y, 170, 22); Controls.Add(chkShift);
            chkRooms.Text = "Raum für ALLE Folgeläufe ändern"; chkRooms.SetBounds(185, y, 215, 22); Controls.Add(chkRooms);
            Label lt = L("Datum als TT.MM.JJJJ eintippen oder − / +.", 405, y + 3); Controls.Add(lt);
            chkShift.Visible = chkRooms.Visible = lt.Visible = !simple;

            grid.SetBounds(10, y + 26, 880, 546 - (y + 26));
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.MultiSelect = false; grid.BackgroundColor = Color.White;
            Col(new DataGridViewTextBoxColumn(), "Schritt / Lauf", 245, true);
            Col(new DataGridViewTextBoxColumn(), "Bezug", 155, true);
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0 && !names.Contains(r.Name)) names.Add(r.Name);
            foreach (PackageStep s in pkg.Steps) if (s.Row.Length > 0 && !names.Contains(s.Row)) names.Add(s.Row);   // unbekannter Block bleibt sichtbar
            DataGridViewComboBoxColumn cr = new DataGridViewComboBoxColumn(); cr.FlatStyle = FlatStyle.Flat; cr.Items.AddRange(names.ToArray());
            Col(cr, "Block (Raum)", 105, false);
            Col(new DataGridViewTextBoxColumn(), "Vorschlag", 100, true);
            Col(new DataGridViewTextBoxColumn(), "Gewünscht", 100, false);
            Col(new DataGridViewButtonColumn(), "−", 28, true);
            Col(new DataGridViewButtonColumn(), "+", 28, true);
            Col(new DataGridViewCheckBoxColumn(), "fällt aus", 70, false);
            grid.CellContentClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && (e.ColumnIndex == cMinus || e.ColumnIndex == cPlus)) Shift(e.RowIndex, e.ColumnIndex == cMinus ? -1 : 1); };
            grid.CellEndEdit += delegate(object s, DataGridViewCellEventArgs e) { if (e.ColumnIndex == cWant) Typed(e.RowIndex); };
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty && (grid.CurrentCell.ColumnIndex == cRoom || grid.CurrentCell.ColumnIndex == cSkip)) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (filling || e.RowIndex < 0) return;
                if (e.ColumnIndex == cRoom) SetRoom(e.RowIndex, Convert.ToString(grid.Rows[e.RowIndex].Cells[cRoom].Value) ?? "", chkRooms.Checked);
                else if (e.ColumnIndex == cSkip) SetSkip(e.RowIndex, grid.Rows[e.RowIndex].Cells[cSkip].Value is bool && (bool)grid.Rows[e.RowIndex].Cells[cSkip].Value);
            };
            grid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            Controls.Add(grid);

            if (simple)   // Einzelpaket: Datum, Raum, Laufnummer von Hand; darunter was geschrieben wird
            {
                grid.Visible = false;
                Controls.Add(L("Raum:", 10, y + 4));
                cbRoom.DropDownStyle = ComboBoxStyle.DropDownList; cbRoom.Items.AddRange(names.ToArray()); cbRoom.SetBounds(70, y, 200, 22); Controls.Add(cbRoom);
                lSum.SetBounds(10, y + 36, 880, 546 - (y + 36)); Controls.Add(lSum);
                cbRoom.SelectedIndexChanged += delegate { if (!building) SimpleRoom(); };
            }

            Controls.Add(L("Vorhandene Einträge:", 10, 562));   // Uebernahme-Modus wird erst hier gewaehlt (nicht im Paket-Editor)
            cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.SetBounds(135, 558, 250, 22);
            cbMode.Items.AddRange(new object[] { "Nur leere Blöcke füllen", "Vorhandenes überschreiben", "An vorhandenen Text anhängen" });
            cbMode.SelectedIndex = (int)mode; cbMode.SelectionChangeCommitted += delegate { modeTouched = true; }; Controls.Add(cbMode);
            ok.Text = "In Plan übernehmen"; ok.SetBounds(640, 556, 135, 28);
            ok.Click += delegate { grid.EndEdit(); DialogResult = DialogResult.OK; };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(785, 556, 105, 28); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); CancelButton = cancel;

            dtLauf1.ValueChanged += delegate { Recalc(); };
            tFirst.TextChanged += delegate { Recalc(); };
            tPack.TextChanged += delegate { Recalc(); };
            nRuns.ValueChanged += delegate { if (!building) { pkg.SetRunCount((int)nRuns.Value); Recalc(); } };
            chkRhythm.CheckedChanged += delegate { Recalc(); };
            building = false;
            Recalc();
            if (simple && sched.Count > 0) { building = true; string r0 = Room(0); if (!cbRoom.Items.Contains(r0)) cbRoom.Items.Add(r0); cbRoom.Text = r0; building = false; }
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

        // Wochentag von Tag 1 so setzen, dass Wochentags-Wiederholungen auf echten Wochentagen liegen (haengt von "Lauf 1 am" ab)
        void SyncDay1()
        {
            DateTime l1 = dtLauf1.Value.Date;
            for (int k = 0; k < 7; k++)
            {
                DayOfWeek d = pkg.StartFor(l1).DayOfWeek;
                if (pkg.Day1 == d) break;
                pkg.Day1 = d;
            }
        }

        // Vorschlaege neu berechnen (Datum/Laeufe/Nummern/Rhythmus geaendert); eigene Verschiebungen und Raeume bleiben je Lauf erhalten.
        void Recalc()
        {
            if (building) return;
            bool valid = pkg.FirstRunNo < 0 || pkg.SetFirstRun(tFirst.Text);   // 8 Ziffern: Praefix + Lauf; Folgelaeufe zaehlen hoch
            string pn = tPack.Text.Trim(); bool pv = PackageStep.IsManualNo(pn);
            foreach (PackageStep s in pkg.HandNoSteps()) s.ManualNo = pv ? pn : "";
            PackWarning = !needPack || pv ? null : (pn.Length == 0 ? "Laufnummer Packen fehlt (8 Ziffern, z. B. 12340001)." : "Laufnummer Packen: bitte 8 Ziffern eingeben.");
            lPack.Text = PackWarning ?? "→ Packen (Nr. " + pn + ")"; lPack.ForeColor = PackWarning == null ? SystemColors.ControlText : Color.Firebrick;
            SyncDay1();
            string over = pkg.Overflow();
            Package view = pkg.Clone();   // ohne Ausfaelle: auch ausgefallene Laeufe bleiben als Zeile sichtbar (zum Zuruecknehmen)
            foreach (PackageStep s in view.Steps) s.Skip.Clear();
            sched = view.Schedule(chkRhythm.Checked ? gaps : null);
            sched.Sort(delegate(RunDate a, RunDate b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : (a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Run.CompareTo(b.Run)); });
            string firstLabel = "";
            foreach (RunDate d in sched) if (pkg.Steps[d.Step].Kind != StepKind.Normal) { firstLabel = d.Label; break; }
            lFirst.Text = !valid ? "bitte 8 Ziffern eingeben (z. B. 13570030)" : over ?? (firstLabel.Length > 0 ? "→ " + firstLabel : "(keine Läufe im Paket)");
            lFirst.ForeColor = valid && over == null ? SystemColors.ControlText : Color.Firebrick;
            Warning = valid ? over : lFirst.Text;
            filling = true;
            grid.Rows.Clear();
            for (int i = 0; i < sched.Count; i++) grid.Rows.Add(new object[] { sched[i].Label, "", Room(i), Ui.DayText(Proposed(i)), "", null, null, false });
            filling = false;
            skipped = new bool[sched.Count];
            UpdateAll();
        }

        // Alle Zeilen neu beschriften (Ausfaelle, Bezug, belegt, Datum) und Uebernehmen freigeben bzw. sperren
        void UpdateAll()
        {
            Dictionary<long, bool> real = new Dictionary<long, bool>();   // stattfindende Laeufe (mit Ausfaellen)
            foreach (RunDate d in pkg.Schedule(chkRhythm.Checked ? gaps : null)) real[d.Key()] = true;
            skipped = new bool[sched.Count];
            effHead = new List<RunDate>(); firstRunDay = int.MaxValue;
            int hi = HeadIdx();
            for (int i = 0; i < sched.Count; i++)
            {
                skipped[i] = !real.ContainsKey(sched[i].Key());
                if (sched[i].Step != hi || skipped[i]) continue;
                RunDate d = new RunDate(); d.Step = sched[i].Step; d.Run = sched[i].Run; d.Day = sched[i].Day + Off(i);
                effHead.Add(d); firstRunDay = Math.Min(firstRunDay, d.Day);
            }
            bool anyBusy = false;
            for (int i = 0; i < sched.Count; i++) { UpdateRow(i); anyBusy |= IsBusy(i); }
            if (!modeTouched) cbMode.SelectedIndex = anyBusy ? (int)ApplyMode.Append : (int)initialMode;   // Vorkampagne trifft belegte Bloecke: anhaengen
            ok.Enabled = Warning == null && PackWarning == null;
            SimpleSummary();
        }

        // Zeile liegt vor Lauf 1 und der Block ist im Plan schon belegt (Vorkampagne: Packen/Puffer)
        bool IsBusy(int i)
        {
            if (skipped[i] || effHead.Count == 0 || sched[i].Day + Off(i) >= firstRunDay) return false;
            int id;
            if (!ids.TryGetValue(Room(i), out id)) return false;
            Entry e = store.Get(Start.AddDays(sched[i].Day + Off(i) - 1), id);
            return e != null && (!e.IsEmpty || e.Buffers.Count > 0);
        }

        void UpdateRow(int i)
        {
            if (i >= grid.Rows.Count) return;
            bool was = filling; filling = true;
            DataGridViewRow row = grid.Rows[i];
            bool sk = skipped[i], busy = IsBusy(i);
            row.Cells[cLabel].Value = sched[i].Label;
            row.Cells[cRel].Value = pkg.RelText(sched[i].Day + Off(i), effHead) + (busy ? " · belegt" : "");
            row.Cells[cRel].Style.BackColor = busy ? Belegt : Color.Empty;
            row.Cells[cProp].Value = Ui.DayText(Proposed(i));
            row.Cells[cWant].Value = Ui.DayText(Proposed(i).AddDays(Off(i)));
            row.Cells[cWant].Style.BackColor = Off(i) != 0 ? Orange : Color.Empty;
            row.Cells[cRoom].Value = Room(i);
            row.Cells[cRoom].Style.BackColor = Room(i) != sched[i].Row ? Orange : Color.Empty;   // Raumwechsel
            bool head = sched[i].Step == HeadIdx();
            row.Cells[cSkip].Value = sk && head;
            row.Cells[cSkip].ReadOnly = !head;
            foreach (DataGridViewCell c in row.Cells) { c.Style.ForeColor = sk ? Color.Gray : Color.Empty; c.Style.Font = sk && c.ColumnIndex != cSkip ? strike : null; }
            filling = was;
        }

        // Zeile i um delta Tage verschieben; mit "Folgende mitverschieben" auch alle Zeilen darunter.
        public void Shift(int i, int delta)
        {
            if (delta == 0 || i < 0 || i >= sched.Count) return;
            List<int> idx = new List<int>();
            for (int j = i; j < sched.Count && (j == i || chkShift.Checked); j++) idx.Add(j);
            foreach (int j in idx.ToArray()) foreach (int k in BackupRows(j)) if (!idx.Contains(k)) idx.Add(k);   // Back-Up folgt dem Packen
            foreach (int j in idx) offs[sched[j].Key()] = Off(j) + delta;
            UpdateAll();
        }

        // Lauf (Zeile des Hauptlaufs) faellt aus bzw. findet wieder statt: Luecke bei Laufnummer und Kennung, JeLauf-Schritte dieses Laufs entfallen
        public void SetSkip(int i, bool on)
        {
            if (i < 0 || i >= sched.Count || sched[i].Step != HeadIdx()) return;
            pkg.Head.SetSkip(sched[i].Run, on);
            UpdateAll();
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
                SetRoomOf(j, room);
            }
            foreach (int k in BackupRows(i)) SetRoomOf(k, room);   // Back-Up im selben Raum
            UpdateAll();
        }

        void SetRoomOf(int j, string room) { if (room == sched[j].Row) rooms.Remove(sched[j].Key()); else rooms[sched[j].Key()] = room; }

        // Einzelpaket: ein Raum fuer alle Zeilen
        void SimpleRoom()
        {
            if (cbRoom.Text.Length == 0) return;
            for (int j = 0; j < sched.Count; j++) SetRoomOf(j, cbRoom.Text);
            UpdateAll();
        }

        void SimpleSummary()
        {
            if (!simple) return;
            List<string> l = new List<string>();
            for (int i = 0; i < sched.Count; i++) l.Add(sched[i].Label.Replace("\n", " ") + "  →  " + Ui.DayText(Proposed(i).AddDays(Off(i))) + ", " + Room(i) + (IsBusy(i) ? " (belegt)" : ""));
            lSum.Text = string.Join("\r\n", l.ToArray());
        }

#if TESTHOOKS
        public bool TestShiftFollowing { set { chkShift.Checked = value; } }
        public string TestTyped(int i, string s) { grid.Rows[i].Cells[cWant].Value = s; Typed(i); return Convert.ToString(grid.Rows[i].Cells[cWant].Value); }
        public string TestCell(int i) { return Convert.ToString(grid.Rows[i].Cells[cWant].Value); }
        public bool TestStartField() { return Controls.Contains(dtLauf1) && dtLauf1.Format == DateTimePickerFormat.Custom && dtLauf1.CustomFormat == "dd.MM.yyyy"; }
        public DateTime TestLauf1 { get { return dtLauf1.Value.Date; } set { dtLauf1.Value = value; } }
        public int TestRuns { get { return (int)nRuns.Value; } set { nRuns.Value = value; } }
        public string TestPackNo { get { return tPack.Text; } set { tPack.Text = value; } }
        public bool TestOkEnabled { get { return ok.Enabled; } }
        public bool TestPackVisible { get { return tPack.Parent != null && needPack; } }
        public bool TestSimple { get { return simple; } }
        public string TestRoom { get { return cbRoom.Text; } set { cbRoom.SelectedItem = value; } }
        public int TestRowCount { get { return sched.Count; } }
        public bool TestSkipped(int i) { return skipped[i]; }
        public string TestRel(int i) { return Convert.ToString(grid.Rows[i].Cells[cRel].Value); }
        public string TestLabel(int i) { return sched[i].Label; }
        public int TestIndexOf(int step, int run) { return sched.FindIndex(delegate(RunDate d) { return d.Step == step && d.Run == run; }); }
        public string TestSummary { get { return lSum.Text; } }
#endif

        // Eingetipptes Datum nur vollstaendig als TT.MM.JJJJ (GMP; auch "Fr 13.03.2026"), sonst Meldung und alter Wert.
        public void Typed(int i)
        {
            if (i < 0 || i >= sched.Count) return;
            DateTime cur = Proposed(i).AddDays(Off(i));
            string s = Convert.ToString(grid.Rows[i].Cells[cWant].Value) ?? "";
            int k = 0; while (k < s.Length && !char.IsDigit(s[k])) k++;
            DateTime want;
            bool ok = DateTime.TryParseExact(s.Substring(k).Trim(), "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out want) && want.Year >= 2000;
            if (!ok)
            {
                if (!Quiet) MessageBox.Show(this, "Bitte das Datum vollständig als TT.MM.JJJJ eingeben (z. B. 13.03." + cur.Year + ").", "Kampagne starten", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateRow(i); return;
            }
            if (want < Start && !Quiet) MessageBox.Show(this, "Hinweis: " + want.ToString("dd.MM.yyyy") + " liegt vor dem frühesten Termin der Kampagne.", "Kampagne starten", MessageBoxButtons.OK, MessageBoxIcon.Information);
            int delta = (new DateTime(want.Year, want.Month, want.Day) - cur).Days;
            if (delta == 0) UpdateRow(i); else Shift(i, delta);
        }
    }
}
