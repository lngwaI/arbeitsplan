using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Ein- und Ausgabe des Freeze-Dialogs (auch vom Selbsttest ohne Dialog genutzt).
    class FreezeChoice
    {
        public enum Act { Ok, Remove }
        public Act Action = Act.Ok;
        public FreezeItem Item = new FreezeItem();      // Arbeitskopie; nach OK das Ergebnis
        public FreezeConfig Cfg = new FreezeConfig();
        public bool IsNew, ReadOnly;
        public string Where = "";                       // z. B. "Freeze / Thaw · Di 03.03.2031"
        public List<string> Mains = new List<string>(), Tanks = new List<string>();   // Vorschlaege: Laeufe im Plan (+-3 Tage), Freezetanks aus dem Katalog
        public string SpareTank = "";                   // Vorschlag oben in der Tank-Auswahl: FZ-E aus dem vorherigen Lauf (FreezeSet.SpareTank)
    }

    // "Freeze planen": Art (Bag/Tank), je Art nur die noetigen Felder (ein-/ausgeblendet), Hauptlauf/Unterlauf/Tanks/Notiz und die Cycle-Tabelle
    // mit Live-Rechnung (FreezeCalc). Tabelle und Rechenlogik: FreezeForm.Grid.cs.
    partial class FreezeForm : Form
    {
        readonly FreezeChoice ch;
        readonly FreezeItem it;
        readonly RadioButton rBag = new RadioButton(), rTank = new RadioButton();
        readonly CheckBox k2 = new CheckBox(), k3 = new CheckBox();   // "Start Zyklus 2/3 angeben" (sonst geschaetzt)
        readonly CheckBox kOpen = new CheckBox();                     // Startzeit noch offen (nur bei einem Vorgang, der offen geladen wurde); eine Zeit eintragen nimmt den Haken weg
        readonly NumericUpDown n2 = new NumericUpDown(), n3 = new NumericUpDown();   // Versatz Z2/Z3 zu Z1 in Stunden
        readonly DateTimePicker dC1 = new DateTimePicker(), dC2 = new DateTimePicker(), dC3 = new DateTimePicker(), dTank = new DateTimePicker();
        readonly ComboBox cGwk = new ComboBox(), cKreis = new ComboBox(), cMain = new ComboBox(), cPick = new ComboBox();
        readonly NumericUpDown nDur = new NumericUpDown(), nGap = new NumericUpDown(), nCnt = new NumericUpDown();
        readonly TextBox tSub = new TextBox(), tTanks = new TextBox(), tNote = new TextBox();
        readonly Label lbDur = new Label(), lbEnd = new Label(), lbEndV = new Label(), lbCycles = new Label(), lbHint = new Label(), lbWhere = new Label();
        readonly Button bOk = new Button(), bCancel = new Button(), bRemove = new Button();
        readonly List<Control> bagOnly = new List<Control>(), tankOnly = new List<Control>();
        bool loading, bagMode = true, openLoaded;
        int tab;

        const int LblX = 12, LblW = 196, FldX = 212, FldW = 176, HintX = 394, HintW = 84, OffX = 456, OffW = 46, RLblX = 518, RLblW = 78, RFldX = 596, RFldW = 172, Row0 = 52, Pitch = 30;

        public FreezeForm(FreezeChoice choice)
        {
            ch = choice; it = ch.Item;
            Text = ch.IsNew ? "Freeze hinzufügen" : ch.ReadOnly ? "Freeze ansehen" : "Freeze bearbeiten";
            Ui.Dialog(this, 780, 680, false);
            Theme th = Theme.Current;

            rBag.Text = "&Bag-Freeze"; rBag.SetBounds(LblX, 12, 120, 24); rBag.Checked = it.Art == FreezeArt.Bag; Place(rBag);
            rTank.Text = "&Tank-Freeze"; rTank.SetBounds(140, 12, 120, 24); rTank.Checked = it.Art == FreezeArt.Tank; Place(rTank);
            lbWhere.Text = ch.Where; lbWhere.ForeColor = Section.HelpColor; lbWhere.TextAlign = ContentAlignment.MiddleRight; lbWhere.SetBounds(300, 12, 468, 24); Controls.Add(lbWhere);

            // linke Spalte: je Art andere Felder in denselben Zeilen
            Label l1 = Lbl("Start &Zyklus 1:", 0);
            Check(k2, "Start Z&yklus 2:", 1); Check(k3, "Start Zyk&lus 3:", 2);
            Setup(dC1, FldX, Row(0)); Place(dC1);
            Setup(dC2, FldX, Row(1)); Place(dC2); Label h2 = Hint("sonst Z1 +", 1), u2 = Offset(n2, 1);
            Setup(dC3, FldX, Row(2)); Place(dC3); Label h3 = Hint("sonst Z1 +", 2), u3 = Offset(n3, 2);
            bagOnly.AddRange(new Control[] { l1, dC1, k2, dC2, h2, n2, u2, k3, dC3, h3, n3, u3 });
            kOpen.Text = "Zeit offen"; kOpen.AutoSize = false; kOpen.SetBounds(HintX, Row(0) + 1, 90, 22); Place(kOpen);
            Label g1 = Lbl("&GWK-Anlage:", 0), g2 = Lbl("&Kreislauf:", 1), g3 = Lbl("&Start:", 2);
            cGwk.DropDownStyle = ComboBoxStyle.DropDownList; cGwk.SetBounds(FldX, Row(0) - 2, FldW, 24); for (int i = 1; i <= Math.Max(1, ch.Cfg.GwkAnlagen); i++) cGwk.Items.Add("GWK " + i); Place(cGwk);
            cKreis.DropDownStyle = ComboBoxStyle.DropDownList; cKreis.SetBounds(FldX, Row(1) - 2, FldW, 24); for (int i = 1; i <= Math.Max(1, ch.Cfg.Kreislaeufe); i++) cKreis.Items.Add("Kreislauf " + i); Place(cKreis);
            Setup(dTank, FldX, Row(2)); Place(dTank);
            tankOnly.AddRange(new Control[] { g1, cGwk, g2, cKreis, g3, dTank });

            lbDur.SetBounds(LblX, Row(3), LblW, 20); lbDur.AutoSize = false; Place(lbDur);
            Hours(nDur, Row(3)); Place(nDur); Hint("Stunden", 3);
            Label ab = Lbl("Z&eit zwischen zwei Zyklen:", 4); Hours(nGap, Row(4)); Place(nGap); Label abh = Hint("Std. je Anlage", 4); abh.Width = 98;
            Label an = Lbl("&Anzahl Zyklen:", 5); nCnt.Minimum = 1; nCnt.Maximum = FreezeCalc.MaxCycles; nCnt.SetBounds(FldX, Row(5) - 2, 70, 24); Place(nCnt); Label anh = Hint("1 bis " + FreezeCalc.MaxCycles + ", Anlagen reihum", 5); anh.Width = RFldX - HintX - 4;
            bagOnly.AddRange(new Control[] { ab, nGap, abh, an, nCnt, anh });
            lbEnd.Text = "Ende:"; lbEnd.SetBounds(LblX, Row(4), LblW, 20); lbEndV.SetBounds(FldX, Row(4), 260, 20); Controls.Add(lbEnd); Controls.Add(lbEndV);
            tankOnly.AddRange(new Control[] { lbEnd, lbEndV });

            // rechte Spalte: Zuordnung
            RLbl("&Hauptlauf:", 0); cMain.SetBounds(RFldX, Row(0) - 2, RFldW, 24); cMain.DropDownStyle = ComboBoxStyle.DropDown; cMain.MaxLength = 8; foreach (string m in ch.Mains) cMain.Items.Add(m); Place(cMain);
            RLbl("&Unterlauf:", 1); tSub.SetBounds(RFldX, Row(1) - 2, RFldW, 24); tSub.MaxLength = 8; Ui.SimpleMenu(tSub); Place(tSub);
            RLbl("Ta&nks:", 2); tTanks.SetBounds(RFldX, Row(2) - 2, RFldW, 24); Ui.SimpleMenu(tTanks); Place(tTanks);
            RLbl("Tank &wählen:", 3); cPick.SetBounds(RFldX, Row(3) - 2, RFldW, 24); cPick.DropDownStyle = ComboBoxStyle.DropDownList; cPick.Items.Add("(aus Katalog hinzufügen)"); cPick.DropDownWidth = 280;
            if (ch.SpareTank.Length > 0) cPick.Items.Add(ch.SpareTank + " (Ersatz aus Lauf n−1)");
            foreach (string t in ch.Tanks) if (t != ch.SpareTank) cPick.Items.Add(t);
            cPick.SelectedIndex = 0; Place(cPick);
            RLbl("&Notiz:", 4); tNote.SetBounds(RFldX, Row(4) - 2, RFldW, 66); tNote.Multiline = true; tNote.AcceptsReturn = true; tNote.ScrollBars = ScrollBars.Vertical; Ui.SimpleMenu(tNote); Place(tNote);

            lbCycles.Text = "Zyklen – unter „tatsächlicher Start“ eine Zeit eintragen: Folge-Zyklen derselben Anlage ziehen nach."; lbCycles.SetBounds(LblX, 240, 756, 18); lbCycles.AutoSize = false; Controls.Add(lbCycles);
            Label lq = new Label(); lq.Text = "Namen vergeben:"; lq.AutoSize = false; lq.SetBounds(LblX, 262, 110, 20); Controls.Add(lq);
            cQuick.DropDownStyle = ComboBoxStyle.DropDownList; cQuick.SetBounds(LblX + 112, 258, 170, 24); cQuick.Items.AddRange(new object[] { "(Schnellbelegung)", "Z1, Z2, Z3 …", "A, B, C …", "1, 2, 3 …" });
            cQuick.SelectedIndex = 0; cQuick.SelectionChangeCommitted += delegate { QuickNames(cQuick.SelectedIndex); cQuick.SelectedIndex = 0; }; Controls.Add(cQuick);
            bagOnly.AddRange(new Control[] { lq, cQuick });
            BuildGrid(286);
            lbHint.SetBounds(LblX, 594, 756, 40); lbHint.AutoSize = false; Controls.Add(lbHint);

            bRemove.Text = "Entfernen"; bRemove.Width = 90; bRemove.Height = 30; bRemove.Visible = !ch.IsNew && !ch.ReadOnly; bRemove.Click += delegate { Remove(); }; Place(bRemove);
            bOk.Text = "Speichern"; bOk.Width = 100; bOk.Height = 30; bOk.Visible = !ch.ReadOnly; bOk.Click += delegate { Accept(); }; Place(bOk);
            bCancel.Text = ch.ReadOnly ? "Schließen" : "Abbrechen"; bCancel.Width = 100; bCancel.Height = 30; bCancel.DialogResult = DialogResult.Cancel; Place(bCancel);
            AcceptButton = ch.ReadOnly ? (IButtonControl)bCancel : bOk; CancelButton = bCancel;
            th.StyleButton(bOk); th.StyleButton(bCancel); th.StyleButton(bRemove);

            LoadFields(); WireEvents(); ApplyArt();
            if (ch.ReadOnly) foreach (Control c in Controls) if (c != bCancel && c != grid && c != lbHint && c != lbWhere && c != lbCycles && !(c is Label)) c.Enabled = false;
        }

        void Place(Control c) { c.TabIndex = tab++; if (!Controls.Contains(c)) Controls.Add(c); }
        static int Row(int i) { return Row0 + i * Pitch; }

        void Check(CheckBox k, string text, int row) { k.Text = text; k.AutoSize = false; k.SetBounds(LblX, Row(row) + 1, LblW, 22); Place(k); }
        Label Lbl(string text, int row) { Label l = new Label(); l.Text = text; l.AutoSize = false; l.SetBounds(LblX, Row(row) + 2, LblW, 20); Place(l); return l; }
        Label RLbl(string text, int row) { Label l = new Label(); l.Text = text; l.AutoSize = false; l.SetBounds(RLblX, Row(row) + 2, RLblW, 20); Place(l); return l; }
        Label Hint(string text, int row) { Label l = new Label(); l.Text = text; l.AutoSize = false; l.ForeColor = Section.HelpColor; l.SetBounds(HintX, Row(row) + 2, HintW, 20); Controls.Add(l); return l; }

        static void Setup(DateTimePicker d, int x, int y)
        {
            d.Format = DateTimePickerFormat.Custom; d.CustomFormat = "dd.MM.yyyy  HH:mm"; d.SetBounds(x, y - 2, FldW, 24);
            d.MinDate = new DateTime(2000, 1, 1); d.MaxDate = new DateTime(2099, 12, 31, 23, 59, 0);
        }

        Label Offset(NumericUpDown n, int row)
        {
            n.DecimalPlaces = 2; n.Increment = 0.25m; n.Minimum = 0; n.Maximum = 168; n.SetBounds(OffX, Row(row) - 2, OffW, 24); Place(n);
            Label u = Hint("h", row); u.SetBounds(OffX + OffW + 1, Row(row) + 2, 14, 20); return u;
        }

        static void Hours(NumericUpDown n, int y) { n.DecimalPlaces = 2; n.Increment = 0.25m; n.Minimum = 0.25m; n.Maximum = 168; n.SetBounds(FldX, y - 2, 90, 24); }

        static DateTime Clamp(DateTime d, DateTimePicker p) { return d < p.MinDate ? p.MinDate : d > p.MaxDate ? p.MaxDate : d; }
        static DateTime Trunc(DateTime d) { return new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0); }
        static int Mins(NumericUpDown n) { return Math.Max(1, (int)Math.Round((double)n.Value * 60)); }
        static decimal Hrs(int min, NumericUpDown n) { return Math.Max(n.Minimum, Math.Min(n.Maximum, Math.Round((decimal)min / 60m, 2))); }

        // Felder aus dem Vorgang fuellen
        void LoadFields()
        {
            loading = true;
            DateTime c1 = it.TimeOpen ? it.Start1to3[0].Date.AddHours(6) : it.Valid ? it.Start1to3[0] : DateTime.Today.AddHours(6);
            kOpen.Checked = kOpen.Visible = it.TimeOpen; openLoaded = it.TimeOpen;
            n2.Value = Hrs(it.Offset2Min, n2); n3.Value = Hrs(it.Offset3Min, n3);
            dC1.Value = Clamp(c1, dC1); dTank.Value = dC1.Value;
            DateTime[] est = { DateTime.MinValue, c1.AddMinutes(it.Offset2Min), c1.AddMinutes(it.Offset3Min) };
            dC2.Value = Clamp(it.Start1to3[1] != DateTime.MinValue ? it.Start1to3[1] : est[1], dC2); k2.Checked = dC2.Enabled = it.Start1to3[1] != DateTime.MinValue; n2.Enabled = !k2.Checked;
            dC3.Value = Clamp(it.Start1to3[2] != DateTime.MinValue ? it.Start1to3[2] : est[2], dC3); k3.Checked = dC3.Enabled = it.Start1to3[2] != DateTime.MinValue; n3.Enabled = !k3.Checked;
            cGwk.SelectedIndex = Math.Max(0, Math.Min(cGwk.Items.Count - 1, it.Anlage - 1)); cKreis.SelectedIndex = Math.Max(0, Math.Min(cKreis.Items.Count - 1, it.Kreislauf - 1));
            nDur.Value = Hrs(it.DauerMin, nDur); nGap.Value = Hrs(it.AbstandMin, nGap); nCnt.Value = Math.Max(1, Math.Min(FreezeCalc.MaxCycles, it.Cycles));
            cMain.Text = it.MainRef; tSub.Text = it.SubNo; tTanks.Text = it.Tanks; tNote.Text = it.Note.Replace("\n", "\r\n");
            loading = false;
        }

        void WireEvents()
        {
            EventHandler ch1 = delegate { if (!loading) { SyncEstimates(); Recalc(); } };
            dC1.ValueChanged += delegate { if (loading) return; loading = true; dTank.Value = dC1.Value; kOpen.Checked = false; loading = false; SyncEstimates(); Recalc(); };
            dTank.ValueChanged += delegate { if (loading) return; loading = true; dC1.Value = dTank.Value; kOpen.Checked = false; loading = false; SyncEstimates(); Recalc(); };
            dC2.ValueChanged += ch1; dC3.ValueChanged += ch1;
            k2.CheckedChanged += delegate { if (loading) return; dC2.Enabled = k2.Checked; n2.Enabled = !k2.Checked; SyncEstimates(); Recalc(); };
            k3.CheckedChanged += delegate { if (loading) return; dC3.Enabled = k3.Checked; n3.Enabled = !k3.Checked; SyncEstimates(); Recalc(); }; cGwk.SelectedIndexChanged += ch1; cKreis.SelectedIndexChanged += ch1;
            nDur.ValueChanged += ch1; nGap.ValueChanged += ch1; nCnt.ValueChanged += ch1; n2.ValueChanged += ch1; n3.ValueChanged += ch1; kOpen.CheckedChanged += ch1;
            rBag.CheckedChanged += delegate { if (loading) return; ApplyArt(); };
            cPick.SelectionChangeCommitted += delegate { PickTank(); };
        }

        // Gewaehlten Tank in die Tanks uebernehmen; beim Vorschlag nur die Nummer, nicht die Klammer
        void PickTank()
        {
            if (cPick.SelectedIndex <= 0) return;
            string t = ch.SpareTank.Length > 0 && cPick.SelectedIndex == 1 ? ch.SpareTank : (string)cPick.SelectedItem; List<string> have = SplitTanks(tTanks.Text);
            if (!have.Contains(t)) have.Add(t);
            tTanks.Text = string.Join(", ", have.ToArray()); cPick.SelectedIndex = 0;
        }

        // Nicht angehakte Starts C2/C3 folgen C1 (+ Versatz), damit ein spaeteres Anhaken beim erwarteten Wert beginnt
        void SyncEstimates()
        {
            loading = true;
            if (!k2.Checked) dC2.Value = Clamp(dC1.Value.AddMinutes((int)Math.Round(n2.Value * 60m)), dC2);
            if (!k3.Checked) dC3.Value = Clamp(dC1.Value.AddMinutes((int)Math.Round(n3.Value * 60m)), dC3);
            loading = false;
        }

        // Art wechseln: Felder der anderen Art ausblenden (kein Neuaufbau), Grosse des Dialogs anpassen
        void ApplyArt()
        {
            bool bag = bagMode = rBag.Checked;
            foreach (Control c in bagOnly) c.Visible = bag;
            foreach (Control c in tankOnly) c.Visible = !bag;
            lbDur.Text = bag ? "&Dauer je Zyklus:" : "&Dauer:";
            kOpen.Top = Row(bag ? 0 : 2) + 1; kOpen.Visible = openLoaded;
            lbCycles.Visible = grid.Visible = bag;
            int hy = bag ? 594 : 240, by = bag ? 640 : 288;
            lbHint.Top = hy; lbHint.Height = bag ? 40 : 40;
            bRemove.SetBounds(LblX, by, 90, 30); bCancel.SetBounds(668, by, 100, 30); bOk.SetBounds(560, by, 100, 30);
            ClientSize = new Size(780, by + 42);
            Recalc();
        }

        static List<string> SplitTanks(string s)
        {
            List<string> r = new List<string>();
            foreach (string p in s.Split(',', ';', '\n', '\r')) { string t = p.Trim(); if (t.Length > 0 && !r.Contains(t)) r.Add(t); }
            return r;
        }

        // Dialogfelder in den Vorgang schreiben (ohne zu pruefen)
        void Collect()
        {
            it.Art = rBag.Checked ? FreezeArt.Bag : FreezeArt.Tank;
            it.TimeOpen = kOpen.Checked;   // offen: Start1to3[0] = nur der Tag, Speichern ohne Eintrag aendert nichts
            if (it.Art == FreezeArt.Bag)
            {
                it.Start1to3[0] = it.TimeOpen ? dC1.Value.Date : Trunc(dC1.Value);
                it.Start1to3[1] = k2.Checked ? Trunc(dC2.Value) : DateTime.MinValue; it.Start1to3[2] = k3.Checked ? Trunc(dC3.Value) : DateTime.MinValue;
                it.AbstandMin = Mins(nGap); it.Cycles = (int)nCnt.Value;
                it.Offset2Min = (int)Math.Round(n2.Value * 60m); it.Offset3Min = (int)Math.Round(n3.Value * 60m);
            }
            else { it.Start1to3[0] = it.TimeOpen ? dTank.Value.Date : Trunc(dTank.Value); it.Anlage = cGwk.SelectedIndex + 1; it.Kreislauf = cKreis.SelectedIndex + 1; }
            it.DauerMin = Mins(nDur);
            it.MainRef = cMain.Text.Trim(); it.SubNo = tSub.Text.Trim(); it.Tanks = string.Join(", ", SplitTanks(tTanks.Text).ToArray());
            it.Note = tNote.Text.Replace("\r\n", "\n").TrimEnd();
        }

        static readonly Regex Eight = new Regex(@"^\d{8}$", RegexOptions.Compiled);

        // null = in Ordnung, sonst Meldung (Problem + Loesung)
        public string Check()
        {
            if (it.MainRef.Length > 0 && !Eight.IsMatch(it.MainRef)) return "Der Hauptlauf muss 8 Ziffern haben (z. B. 13570030). Bitte korrigieren oder das Feld leer lassen.";
            if (it.SubNo.Length > 0 && !Eight.IsMatch(it.SubNo)) return "Der Unterlauf muss 8 Ziffern haben (z. B. 13579010). Bitte korrigieren oder das Feld leer lassen.";
            return null;
        }

        void Accept()
        {
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            Collect();
            string miss = Check();
            if (miss != null) { MessageBox.Show(this, miss, "Freeze", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            ch.Action = FreezeChoice.Act.Ok; DialogResult = DialogResult.OK;
        }

        void Remove()
        {
            if (MessageBox.Show(this, "Diesen Freeze-Vorgang wirklich entfernen?\nAlle Zyklen und tatsächlichen Starts gehen aus dem Plan.", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            ch.Action = FreezeChoice.Act.Remove; DialogResult = DialogResult.OK;
        }
    }
}
