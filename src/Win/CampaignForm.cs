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
    partial class CampaignForm : Form
    {
        public static bool Quiet;   // Selbsttest: keine Meldungen
        static readonly Color Orange = Color.FromArgb(255, 228, 190);   // abweichend vom Vorschlag
        static readonly Color Rot = Color.FromArgb(255, 150, 150);       // Zeile verletzt eine Sperr-Regel
        const int cLabel = 0, cRel = 1, cRoom = 2, cProp = 3, cWant = 4, cMinus = 5, cPlus = 6, cSkip = 7;
        readonly Package pkg;      // Kopie des Pakets: Laufanzahl, Nummern, Ausfaelle
        readonly IDataStore store;
        readonly bool simple, needPack;
        readonly ApplyMode initialMode;
        readonly DateTimePicker dtLauf1 = new DateTimePicker();
        readonly NumericUpDown nRuns = new NumericUpDown();
        readonly TextBox tFirst = new TextBox(), tPack = new TextBox();   // tFirst: komplette 8-stellige erste Laufnummer; tPack: Nummer des Packens von Hand
        readonly Label lFirst = new Label(), lPack = new Label(), info = new Label(), lSum = new Label(), lRule = new Label(), lBusy = new Label();
        readonly ComboBox cbRoom = new ComboBox(), cbMode = new ComboBox();
        readonly Button ok = new Button();
        readonly Label lWhy = new Label();       // Grund, warum "In Plan uebernehmen" gesperrt ist (neben dem Knopf)
        readonly ToolTip tip = new ToolTip();
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
        string packInit = "", packLast = "";   // Packen-Nr. aus dem Paket-Kopf (vorbelegt) und zuletzt uebernommener Feldwert
        Control[] foot;   // nur Einzelmodus: untere Zeile (Modus, Knöpfe), wandert mit der Höhe
        int firstRunDay = int.MaxValue;
        Font strike;

        // Tag 1 des Pakets (alle Rechnungen laufen ueber Tag-Nummern ab hier), aus "Lauf 1 am"
        public DateTime Start { get { return pkg.StartFor(dtLauf1.Value.Date); } }
        public string Warning { get; private set; }       // ungueltige erste Laufnummer oder Ueberlauf > 9999; null = ok
        public string PackWarning { get; private set; }   // Laufnummer Packen fehlt/ungueltig; null = ok. Beide sperren "In Plan übernehmen".
        public List<PackageProblem> Problems { get; private set; }   // Regelpruefung (Package.Check) mit Plan; Sperre sperrt "In Plan übernehmen"
        public string RuleText { get { return lRule.Text; } }
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
            foreach (PackageStep hs in pkg.HandNoSteps()) if (hs.ManualNo.Length > 0) { packInit = hs.ManualNo; break; }   // Kopfwert des Paket-Editors: vorbelegt, nur aenderbar
            packLast = packInit;
            if (!saved.HasValue) pkg.Day1 = start.DayOfWeek;   // Wiederholung an Wochentagen haengt vom Starttag ab
            DateTime lauf1 = saved.HasValue ? saved.Value.Date : start.Date.AddDays(pkg.Lauf1Day - 1);   // im Paket gespeicherte Vorgabe "Lauf 1 am" hat Vorrang
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0 && !ids.ContainsKey(r.Name)) ids[r.Name] = r.Id;
            Text = "Kampagne starten – " + (p.Name.Length > 0 ? p.Name : "Paket");
            Ui.Dialog(this, 900, 590, false);
            strike = new Font(Font, FontStyle.Strikeout);

            Controls.Add(L(simple ? "Datum:" : "Lauf 1 am:", 10, 14));
            dtLauf1.Format = DateTimePickerFormat.Custom; dtLauf1.CustomFormat = "dd.MM.yyyy";   // immer TT.MM.JJJJ (GMP), unabhaengig von der Windows-Einstellung
            dtLauf1.Value = lauf1 < dtLauf1.MinDate ? dtLauf1.MinDate : lauf1 > dtLauf1.MaxDate ? dtLauf1.MaxDate : lauf1; dtLauf1.SetBounds(simple ? 70 : 85, 10, 110, 22); Controls.Add(dtLauf1);
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
                Label lp = L(simple ? "Laufnummer:" : "Laufnummer Packen:", 10, y + 2); lp.Font = big; Controls.Add(lp);
                tPack.Font = big; tPack.MaxLength = 8; tPack.SetBounds(lp.Left + lp.PreferredWidth + 4, y - 2, 112, tPack.PreferredHeight); Ui.SimpleMenu(tPack); tPack.Text = packInit; Controls.Add(tPack);   // kein Vorschlag; vorbelegt nur mit der Nummer aus dem Paket-Kopf
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
            chkShift.Visible = chkRooms.Visible = !simple;
            Label lt = L("Datum in der Spalte „Gewünscht“ als TT.MM.JJJJ eintippen, oder mit „Tage −“ / „Tage +“ um einen Tag verschieben.", 10, y + 28); lt.ForeColor = Section.HelpColor; Controls.Add(lt);   // eigene Zeile
            lt.Visible = !simple;

            grid.SetBounds(10, y + 50, 880, 508 - (y + 50));
            lBusy.SetBounds(10, 511, 880, 18); lBusy.AutoEllipsis = true; lBusy.Visible = !simple; Controls.Add(lBusy);
            lRule.SetBounds(10, 530, 880, 18); lRule.AutoEllipsis = true; Controls.Add(lRule);
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect; grid.MultiSelect = false; grid.BackgroundColor = Color.White;
            Col(new DataGridViewTextBoxColumn(), "Schritt / Lauf", 190, true);
            Col(new DataGridViewTextBoxColumn(), "Bezug", 225, true);
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0 && !names.Contains(r.Name)) names.Add(r.Name);
            foreach (PackageStep s in pkg.Steps) if (s.Row.Length > 0 && !names.Contains(s.Row)) names.Add(s.Row);   // unbekannter Block bleibt sichtbar
            DataGridViewComboBoxColumn cr = new DataGridViewComboBoxColumn(); cr.FlatStyle = FlatStyle.Flat; cr.Items.AddRange(names.ToArray());
            Col(cr, "Block (Raum)", 90, false);
            Col(new DataGridViewTextBoxColumn(), "Vorschlag", 95, true);
            Col(new DataGridViewTextBoxColumn(), "Gewünscht", 95, false);
            Col(new DataGridViewButtonColumn(), "Tage −", 50, true); ((DataGridViewButtonColumn)grid.Columns[cMinus]).Text = "−";   // Kopf nennt die Einheit, Knopf nur das Zeichen
            Col(new DataGridViewButtonColumn(), "Tage +", 50, true); ((DataGridViewButtonColumn)grid.Columns[cPlus]).Text = "+";
            Col(new DataGridViewCheckBoxColumn(), "entfällt", 60, false);
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
                lSum.SetBounds(10, y + 36, 880, 526 - (y + 36)); Controls.Add(lSum);
                cbRoom.SelectedIndexChanged += delegate { if (!building) SimpleRoom(); };
            }

            Label lMode = L("Vorhandene Einträge:", 10, 562); Controls.Add(lMode);   // Uebernahme-Modus wird erst hier gewaehlt (nicht im Paket-Editor)
            cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.SetBounds(135, 558, 215, 22);
            cbMode.Items.AddRange(new object[] { "Nur leere Blöcke füllen", "Vorhandenes überschreiben", "An vorhandenen Text anhängen" });
            cbMode.SelectedIndex = (int)mode; cbMode.SelectionChangeCommitted += delegate { modeTouched = true; if (!building) UpdateAll(); }; Controls.Add(cbMode);
            ok.Text = "In Plan übernehmen"; ok.SetBounds(640, 556, 135, 28);
            ok.Click += delegate { grid.EndEdit(); DialogResult = DialogResult.OK; };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(785, 556, 105, 28); cancel.DialogResult = DialogResult.Cancel;
            lWhy.SetBounds(358, 561, 274, 18); lWhy.AutoEllipsis = true; lWhy.ForeColor = Color.Firebrick; lWhy.UseMnemonic = false; Controls.Add(lWhy);
            Controls.Add(ok); Controls.Add(cancel); CancelButton = cancel; AcceptButton = ok;   // Enter = In Plan uebernehmen (nur wenn freigegeben), Esc = Abbrechen
            ok.Font = new Font(Font, FontStyle.Bold);
            Control[] tab = { dtLauf1, nRuns, tFirst, tPack, chkRhythm, chkShift, chkRooms, grid, cbMode, ok, cancel };   // Tab-Reihenfolge = Lesereihenfolge
            for (int i = 0; i < tab.Length; i++) tab[i].TabIndex = i;
            if (simple) foot = new Control[] { lMode, cbMode, lWhy, ok, cancel };

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

#if TESTHOOKS
        public bool TestShiftFollowing { set { chkShift.Checked = value; } }
        public string TestTyped(int i, string s) { grid.Rows[i].Cells[cWant].Value = s; Typed(i); return Convert.ToString(grid.Rows[i].Cells[cWant].Value); }
        public string TestCell(int i) { return Convert.ToString(grid.Rows[i].Cells[cWant].Value); }
        public bool TestStartField() { return Controls.Contains(dtLauf1) && dtLauf1.Format == DateTimePickerFormat.Custom && dtLauf1.CustomFormat == "dd.MM.yyyy"; }
        public DateTime TestLauf1 { get { return dtLauf1.Value.Date; } set { dtLauf1.Value = value; } }
        public int TestRuns { get { return (int)nRuns.Value; } set { nRuns.Value = value; } }
        public string TestPackNo { get { return tPack.Text; } set { tPack.Text = value; } }
        public bool TestOkEnabled { get { return ok.Enabled; } }
        public string TestWhy { get { return lWhy.Text; } }
        public bool TestButtons { get { return AcceptButton == ok && CancelButton != null && ok.Text == "In Plan übernehmen"; } }
        public string TestHeaders { get { List<string> h = new List<string>(); foreach (DataGridViewColumn c in grid.Columns) h.Add(c.HeaderText); return string.Join("|", h.ToArray()); } }
        public bool TestPackVisible { get { return tPack.Parent != null && needPack; } }
        public bool TestSimple { get { return simple; } }
        public string TestRoom { get { return cbRoom.Text; } set { cbRoom.SelectedItem = value; } }
        public int TestRowCount { get { return sched.Count; } }
        public bool TestSkipped(int i) { return skipped[i]; }
        public bool TestRowRed(int i) { return grid.Rows[i].Cells[cLabel].Style.BackColor == Rot; }
        public string TestRel(int i) { return Convert.ToString(grid.Rows[i].Cells[cRel].Value); }
        public string TestLabel(int i) { return sched[i].Label; }
        public int TestIndexOf(int step, int run) { return sched.FindIndex(delegate(RunDate d) { return d.Step == step && d.Run == run; }); }
        public string TestSummary { get { return lSum.Text; } }
        public string TestShown(int i) { return Convert.ToString(grid.Rows[i].Cells[cLabel].Value); }
        public string TestBusyLine { get { return lBusy.Text; } }
        public Color TestRelColor(int i) { return grid.Rows[i].Cells[cRel].Style.BackColor; }
        public string TestRelTip(int i) { return grid.Rows[i].Cells[cRel].ToolTipText; }
        public ApplyMode TestMode { get { return Mode; } set { cbMode.SelectedIndex = (int)value; modeTouched = true; UpdateAll(); } }
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
