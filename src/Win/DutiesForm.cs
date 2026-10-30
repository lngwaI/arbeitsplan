using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen -> Pflichtpunkte & Feiertage (Recht PackageAdmin): Pflichtpunkte je Zeile, Rundgaenge (aktiv/Name),
    // Bundesland und zusaetzliche/entfallene Feiertage. LKZ-Felder kommen weiter aus der Blockstruktur.
    class DutiesForm : Form
    {
        // Reihenfolge = CheckRule (IfOccupied, WeeklyOnce, Workdays)
        static readonly string[] RuleNames = { "wenn belegt", "einmal je KW", "werktags" };
        readonly DataGridView defs = new DataGridView(), rounds = new DataGridView();
        readonly ComboBox land = new ComboBox();
        readonly CheckBox madeBox = new CheckBox();   // N2: Puffer "hergestellt" (DutyConfig.BufMade)
        readonly TextBox extra = new TextBox(), removed = new TextBox();
        readonly List<string> rowNames = new List<string>();
        readonly List<int> rowIds = new List<int>();
        readonly DutyConfig src;
        public DutyConfig Result;

        public DutiesForm(DutyConfig cfg, List<RowInfo> rows)
        {
            src = cfg;
            Text = "Pflichtpunkte & Feiertage";
            Ui.Dialog(this, 780, 540);
            for (int i = 0; i < rows.Count; i++)
            {
                string n = rows[i].Name.Length > 0 ? rows[i].Name : "(Zeile " + (i + 1) + ")";
                if (rowNames.Contains(n)) n += " #" + rows[i].Id;
                rowNames.Add(n); rowIds.Add(rows[i].Id);
            }

            Label l1 = L("Pflichtpunkte je Zeile (Haken im Block):", 10, 8, 760);
            Grid(defs, 10, 28, 760, 170);
            Text_(defs, "Name", 150); Text_(defs, "Kurz", 70);
            Combo(defs, "Zeile", 140, rowNames.ToArray()); Combo(defs, "Regel", 120, RuleNames);
            Text_(defs, "Bedingung", 110);
            DataGridViewCheckBoxColumn wk = new DataGridViewCheckBoxColumn(); wk.HeaderText = "1× je KW"; wk.Width = 70; defs.Columns.Add(wk);
            foreach (CheckDef d in cfg.Defs)
            {
                int ri = rowIds.IndexOf(d.RowId);
                int i = defs.Rows.Add(d.Name, d.Short, ri >= 0 ? rowNames[ri] : null, RuleNames[(int)d.Rule], d.Cond, d.Weekly);
                defs.Rows[i].Tag = d.Clone();
            }
            Button bAdd = B("+ Pflichtpunkt", 10, 204, 120), bDel = B("Entfernen", 136, 204, 90);
            bAdd.Click += delegate { int i = defs.Rows.Add("", "", null, RuleNames[0], "", false); defs.Rows[i].Tag = new CheckDef(); defs.CurrentCell = defs.Rows[i].Cells[0]; };
            bDel.Click += delegate { if (defs.CurrentRow != null) defs.Rows.Remove(defs.CurrentRow); };
            Label hint = L("Bedingung: +v = bei Variante v automatisch erfüllt, !v = entfällt bei Variante v (mehrere mit Komma).\n\"1× je KW\" (nur \"wenn belegt\"): leere Tage zählen nicht, sobald die KW erledigt ist. LKZ kommt aus der Blockstruktur.", 236, 202, 534);
            hint.Height = 34;

            Label l2 = L("Rundgänge je Kalenderwoche:", 10, 244, 370);
            Grid(rounds, 10, 264, 370, 190);
            DataGridViewCheckBoxColumn ak = new DataGridViewCheckBoxColumn(); ak.HeaderText = "Aktiv"; ak.Width = 50; rounds.Columns.Add(ak);
            Text_(rounds, "Name", 220); Text_(rounds, "Kurz", 80);
            foreach (RoundDef r in cfg.Rounds) { int i = rounds.Rows.Add(r.Active, r.Name, r.Short); rounds.Rows[i].Tag = r.Clone(); }

            Label l3 = L("Feiertage (gesetzliche werden berechnet):", 400, 244, 370);
            Label ll = L("Land:", 400, 270, 45);
            land.DropDownStyle = ComboBoxStyle.DropDownList; land.Items.AddRange(new object[] { "BW", "BY" }); land.SetBounds(450, 266, 80, 24);
            land.SelectedItem = cfg.Land == "BY" ? "BY" : "BW";
            Label le = L("Zusätzlich (TT.MM.JJJJ):", 400, 298, 180), lr = L("Entfallen (TT.MM.JJJJ):", 590, 298, 180);
            foreach (TextBox t in new[] { extra, removed }) { t.Multiline = true; t.ScrollBars = ScrollBars.Vertical; Ui.SimpleMenu(t); }
            extra.SetBounds(400, 318, 180, 136); removed.SetBounds(590, 318, 180, 136);
            extra.Text = Dates(cfg.ExtraHolidays); removed.Text = Dates(cfg.RemovedHolidays);

            madeBox.Text = "Puffer als hergestellt abhaken (Tages-/Wochenprobe folgt daraus)"; madeBox.SetBounds(10, 456, 760, 20); madeBox.Checked = cfg.BufMade;
            Label mh = L("Mobiler Puffer hergestellt: Tagesprobe gezogen; stationärer: Wochenprobe der KW. Der WFI-Rundgang bleibt davon unabhängig.", 28, 476, 742);
            Button bOk = B("Speichern", 570, 498, 95), bCancel = B("Abbrechen", 675, 498, 95), bPuffer = B("Standard Puffer einrichten", 10, 498, 200);
            bOk.Click += delegate { Commit(); };
            bPuffer.Click += delegate { AddPuffer(PufferSetup.Row(rows)); };
            bCancel.DialogResult = DialogResult.Cancel; CancelButton = bCancel;
            Controls.AddRange(new Control[] { l1, defs, bAdd, bDel, hint, l2, rounds, l3, ll, land, le, lr, extra, removed, madeBox, mh, bPuffer, bOk, bCancel });
        }

        public bool PufferVariants;   // beim Speichern: Varianten mobil/stationaer an der Zeile Puffer setzen, falls noch keine

        // Fehlende Standard-Pflichtpunkte der Zeile "Puffer" in die Tabelle (vorhandene Schluessel bleiben unveraendert)
        void AddPuffer(RowInfo pr)
        {
            if (pr == null) { MessageBox.Show("Es gibt keine Zeile \"Puffer\" (Einstellungen → Blockstruktur).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DutyConfig t = new DutyConfig(); t.AddPufferDefaults(pr.Id);
            int n = 0;
            foreach (CheckDef d in t.Defs)
            {
                bool have = false;
                foreach (DataGridViewRow r in defs.Rows) have |= ((CheckDef)r.Tag).Key == d.Key;
                if (have) continue;
                int i = defs.Rows.Add(d.Name, d.Short, rowNames[rowIds.IndexOf(pr.Id)], RuleNames[(int)d.Rule], d.Cond, d.Weekly);
                defs.Rows[i].Tag = d.Clone(); n++;
            }
            PufferVariants = pr.Variants.Length == 0;
            MessageBox.Show((n > 0 ? n + " Pflichtpunkt(e) ergänzt" : "Pflichtpunkte für Puffer sind schon vorhanden")
                + (PufferVariants ? "; Herstellung mobil/stationär wird an der Zeile Puffer eingerichtet" : "") + ". Wirksam nach \"Speichern\".", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static Label L(string text, int x, int y, int w) { Label l = new Label(); l.Text = text; l.SetBounds(x, y, w, 18); return l; }
        static Button B(string text, int x, int y, int w) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 28); return b; }

        static void Grid(DataGridView g, int x, int y, int w, int h)
        {
            g.SetBounds(x, y, w, h);
            g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false; g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false; g.MultiSelect = false; g.BackgroundColor = SystemColors.Window;
            g.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };   // unbekannte Zeile: Feld bleibt leer
        }

        static void Text_(DataGridView g, string title, int w)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = title; c.Width = w; c.SortMode = DataGridViewColumnSortMode.NotSortable;
            g.Columns.Add(c);
        }

        static void Combo(DataGridView g, string title, int w, string[] items)
        {
            DataGridViewComboBoxColumn c = new DataGridViewComboBoxColumn(); c.HeaderText = title; c.Width = w; c.FlatStyle = FlatStyle.Flat;
            c.Items.AddRange(items);
            g.Columns.Add(c);
        }

        static string Dates(List<DateTime> l)
        {
            List<string> s = new List<string>();
            foreach (DateTime d in l) s.Add(d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            return string.Join("\r\n", s.ToArray());
        }

        // Nur vollstaendige Daten TT.MM.JJJJ (GMP); null = Fehler (Meldung gezeigt)
        List<DateTime> ParseDates(TextBox t, string what)
        {
            List<DateTime> l = new List<DateTime>();
            foreach (string line in t.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                DateTime d;
                if (line.Trim().Length == 0) continue;
                if (!DateTime.TryParseExact(line.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                {
                    MessageBox.Show(what + ": \"" + line.Trim() + "\" ist kein Datum im Format TT.MM.JJJJ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                if (!l.Contains(d)) l.Add(d);
            }
            l.Sort();
            return l;
        }

        static string S(object o) { return o == null ? "" : o.ToString().Trim(); }

        void Commit()
        {
            defs.EndEdit(); rounds.EndEdit();
            DutyConfig c = new DutyConfig();
            c.Land = S(land.SelectedItem) == "BY" ? "BY" : "BW"; c.BufMade = madeBox.Checked;
            List<string> keys = new List<string>();
            foreach (DataGridViewRow r in defs.Rows) { CheckDef o = (CheckDef)r.Tag; if (o.Key.Length > 0) keys.Add(o.Key); }
            foreach (DataGridViewRow r in defs.Rows)
            {
                CheckDef d = ((CheckDef)r.Tag).Clone();
                d.Name = S(r.Cells[0].Value); d.Short = S(r.Cells[1].Value);
                int ri = rowNames.IndexOf(S(r.Cells[2].Value)), rule = Array.IndexOf(RuleNames, S(r.Cells[3].Value));
                if (d.Name.Length == 0 || ri < 0)
                {
                    MessageBox.Show("Jeder Pflichtpunkt braucht einen Namen und eine Zeile.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    defs.CurrentCell = r.Cells[d.Name.Length == 0 ? 0 : 2];
                    return;
                }
                if (d.Short.Length == 0) d.Short = d.Name.Length > 10 ? d.Name.Substring(0, 10) : d.Name;
                d.RowId = rowIds[ri]; d.Rule = rule < 0 ? CheckRule.IfOccupied : (CheckRule)rule;
                d.Cond = S(r.Cells[4].Value).Replace(" ", "");
                d.Weekly = d.Rule == CheckRule.IfOccupied && r.Cells[5].Value is bool && (bool)r.Cells[5].Value;
                if (d.Key.Length == 0) { d.Key = NewKey(d.Short, keys); keys.Add(d.Key); }   // Schluessel bleibt fuer immer (Haken in den Bloecken)
                c.Defs.Add(d);
            }
            foreach (DataGridViewRow r in rounds.Rows)
            {
                RoundDef d = ((RoundDef)r.Tag).Clone();
                d.Active = r.Cells[0].Value is bool && (bool)r.Cells[0].Value;
                if (S(r.Cells[1].Value).Length > 0) d.Name = S(r.Cells[1].Value);
                d.Short = S(r.Cells[2].Value).Length > 0 ? S(r.Cells[2].Value) : d.Short;
                c.Rounds.Add(d);
            }
            c.ExtraHolidays = ParseDates(extra, "Zusätzliche Feiertage");
            if (c.ExtraHolidays == null) return;
            c.RemovedHolidays = ParseDates(removed, "Entfallene Feiertage");
            if (c.RemovedHolidays == null) return;
            Result = c;
            DialogResult = DialogResult.OK;
        }

        // Schluessel aus der Kurzform (Grossbuchstaben/Ziffern), eindeutig, nie "LKZ"
        static string NewKey(string s, List<string> used)
        {
            string b = "";
            foreach (char ch in s.ToUpperInvariant()) if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) b += ch;
            if (b.Length == 0) b = "P";
            if (b.Length > 8) b = b.Substring(0, 8);
            string k = b;
            for (int i = 2; used.Contains(k) || k == CheckDef.LkzKey; i++) k = b + i;
            return k;
        }

#if TESTHOOKS
        public static string SelfTest()
        {
            List<string> used = new List<string>(new[] { "PROBE" });
            return "Pflichtpunkt-Schlüssel: " + (NewKey("Probe", used) == "PROBE2" && NewKey("LKZ", used) == "LKZ2" && NewKey("ü-1", used) == "1" ? "OK" : "FEHLER");
        }
#endif
    }
}
