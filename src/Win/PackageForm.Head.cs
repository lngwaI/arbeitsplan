using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Kopfzeile oben (gross, fett): "Kampagne · Produkt [Name] · Code · Erste Laufnummer · Läufe [n] · Lauf 1 am [Datum]".
    // Der Produktcode ist frei (Package.IsProductCode); die 8-stellige erste Laufnummer ist davon unabhaengig: ihre ersten 4 Stellen
    // merkt sich das Paket als Praefix (Package.RunPrefix), die letzten 4 zaehlen die Folgelaeufe hoch (Ueberlauf > 9999 wird gemeldet).
    // "Laeufe" setzt die Laufanzahl des Hauptlaufs EINMAL (Package.RunCount); alle anderen Schritte beziehen sich darauf.
    // "Lauf 1 am" ist optional (Package.Lauf1): mit Datum zeigen Tabelle, Kalender und Vorschau echte Daten, sonst nur "3 Tage vor Lauf 1".
    partial class PackageForm
    {
        readonly TextBox tName = new TextBox(), tProduct = new TextBox(), tFirst = new TextBox();
        readonly ComboBox cbType = new ComboBox();
        readonly NumericUpDown nRuns = new NumericUpDown();
        readonly DateTimePicker dtLauf1 = new DateTimePicker();
        readonly Label lRuns = new Label();
        readonly List<Control[]> headItems = new List<Control[]>();   // Beschriftung + Feld je Eintrag (Umbruch bei schmalem Fenster)
        Panel headPanel;
        int headH;
        string prefix = "";     // Laufnummern-Praefix XXYY aus "Erste Laufnummer" ("" = Laeufe ohne 8-stellige Nummer)

        string Product { get { return tProduct.Text.Trim(); } }

        Panel BuildHead()
        {
            Panel head = headPanel = new Panel(); head.Dock = DockStyle.Top; head.Tag = "keep"; head.BackColor = Color.White;
            Font big = new Font(Font.FontFamily, Font.SizeInPoints + 1f, FontStyle.Bold);
            int h = headH = big.Height + 10;
            cbType.DropDownStyle = ComboBoxStyle.DropDownList; cbType.Font = big; cbType.Items.AddRange(new object[] { "Kampagne", "Einzelpaket", "Ereignis" });
            cbType.Width = TextRenderer.MeasureText("Einzelpaket", big).Width + 36;
            nRuns.Font = big; nRuns.Minimum = 1; nRuns.Maximum = PackageStep.MaxRuns; nRuns.TextAlign = HorizontalAlignment.Right; nRuns.Width = 58;
            dtLauf1.Font = big; dtLauf1.Format = DateTimePickerFormat.Custom; dtLauf1.CustomFormat = "dd.MM.yyyy"; dtLauf1.ShowCheckBox = true; dtLauf1.Checked = false;
            dtLauf1.Width = TextRenderer.MeasureText("00.00.0000", big).Width + 44;
            tName.Width = 150; tProduct.Width = 80; tFirst.Width = 104;
            headItems.Add(new Control[] { null, cbType });
            headItems.Add(new Control[] { Head_("Produkt:", big, h), tName });
            headItems.Add(new Control[] { Head_("Code:", big, h), tProduct });
            headItems.Add(new Control[] { Head_("Erste Laufnummer:", big, h), tFirst });
            lPackNo = Head_("Packen-Nr.:", big, h); lPackNo.Width = Math.Max(lPackNo.Width, TextRenderer.MeasureText("Laufnummer:", big).Width + 4); tPackNo.Width = 104;
            headItems.Add(new Control[] { lPackNo, tPackNo });   // Packen-Nr. (PackageForm.PackNo.cs)
            headItems.Add(new Control[] { Head_("Läufe:", big, h), nRuns });
            headItems.Add(new Control[] { Head_("Lauf 1 am:", big, h), dtLauf1 });
            foreach (Control[] c in headItems)
            {
                if (c[0] != null) head.Controls.Add(c[0]);
                c[1].Font = big; head.Controls.Add(c[1]);
                TextBox t = c[1] as TextBox; if (t != null) Ui.SimpleMenu(t);
            }
            tProduct.MaxLength = 12; tFirst.MaxLength = 8; tPackNo.MaxLength = 8;
            lRuns.TextAlign = ContentAlignment.MiddleLeft; lRuns.AutoEllipsis = true; lRuns.UseMnemonic = false;
            head.Controls.Add(lRuns);
            head.Resize += delegate { LayoutHead(h); };
            LayoutHead(h);
            tip.SetToolTip(cbType, "Kampagne: Hauptlauf mit mehreren Läufen. Einzelpaket: eigene Laufnummer (z. B. anderer Raum). Ereignis: Sonderereignis ohne Läufe (z. B. TFF Flush).");
            tip.SetToolTip(tProduct, "Produktcode, frei wählbar: 2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (z. B. AB1, AB12345678-). Leer = ohne Produkt.");
            tip.SetToolTip(tFirst, "Komplette 8-stellige Laufnummer des ersten Laufs (z. B. 13570030). Folgeläufe zählen die letzten 4 Stellen hoch. Beim Kampagnenstart anpassbar.");
            tip.SetToolTip(nRuns, "Anzahl der Läufe der Kampagne – gilt einmal für alle Schritte (Puffer, Unterläufe, Kennung …). Beim Kampagnenstart änderbar.");
            tip.SetToolTip(dtLauf1, "Optional: Datum von Lauf 1. Dann zeigen Tabelle, Kalender und Vorschau echte Daten; sonst nur „3 Tage vor Lauf 1“. Beim Kampagnenstart anpassbar.");
            tip.SetToolTip(tPackNo, "Nummer des Packens, 8 Ziffern (z. B. 12340001), von Hand, kein Vorschlag. Gilt für alle Packen-Schritte; im Schritt änderbar. Leer = beim Kampagnenstart eintragen.");
            tPackNo.TextChanged += delegate { PackNoTyped(); };
            tName.TextChanged += delegate { Changed(); };
            cbType.SelectedIndexChanged += delegate { if (!loading && cbType.SelectedIndex >= 0 && (int)pk.Type != cbType.SelectedIndex) { pk.Type = (PackageType)cbType.SelectedIndex; Changed(); } };
            tProduct.TextChanged += delegate   // anderes Produkt: Geraetelisten der Zeilen neu fuellen (Produktprofil)
            {
                if (!loading && cur != null && (Product.Length == 0 || Package.IsProductCode(Product))) { dirty = true; Bind(cur); } else Changed();
            };
            tFirst.TextChanged += delegate { FirstRunTyped(); };
            nRuns.ValueChanged += delegate { RunsChanged(); };
            dtLauf1.ValueChanged += delegate { Lauf1Changed(); };
            return head;
        }

        Label Head_(string text, Font big, int h)
        {
            Label l = new Label(); l.Text = text; l.Font = big; l.AutoSize = false; l.UseMnemonic = false; l.TextAlign = ContentAlignment.MiddleLeft;
            l.Size = new Size(TextRenderer.MeasureText(text, big).Width + 4, h);
            return l;
        }

        // Eintraege von links nach rechts; passt einer nicht mehr in die Zeile, beginnt eine neue (schmales Fenster)
        void LayoutHead(int h)
        {
            int w = headPanel.ClientSize.Width, x = 10, y = 8, gap = 12;
            foreach (Control[] c in headItems)
            {
                Label l = (Label)c[0];
                if (c[1] == tPackNo && !packShow) { l.Visible = tPackNo.Visible = false; continue; }
                if (c[1] == tPackNo) l.Visible = tPackNo.Visible = true;
                int need = (l != null ? l.Width + 2 : 0) + c[1].Width;
                if (x > 10 && x + need > w - 10) { x = 10; y += h + 4; }
                if (l != null) { l.Location = new Point(x, y); x = l.Right + 2; }
                c[1].Location = new Point(x, y + (h - c[1].Height) / 2);
                x = c[1].Right + gap;
            }
            int avail = w - x - 10;
            bool err = lRuns.ForeColor == Color.Firebrick && lRuns.Text.Length > 0;
            if (avail < 200 && err) { x = 10; y += h + 4; avail = w - 20; }   // Fehlermeldung bekommt eine eigene Zeile, der Hinweis "→ Laufnummern" entfaellt bei Platzmangel
            lRuns.Visible = avail >= 200;
            lRuns.SetBounds(x, y, Math.Max(40, avail), h);
            int nh = y + h + 8;
            if (headPanel.Height != nh) headPanel.Height = nh;
        }

        // Typ und "Lauf 1 am" aus dem Paket in die Felder uebernehmen (beim Laden)
        void LoadHead()
        {
            bool was = loading; loading = true;
            cbType.SelectedIndex = (int)pk.Type;
            dtLauf1.Checked = pk.Lauf1.HasValue;
            if (pk.Lauf1.HasValue && pk.Lauf1.Value >= dtLauf1.MinDate && pk.Lauf1.Value <= dtLauf1.MaxDate) dtLauf1.Value = pk.Lauf1.Value;
            else if (pk.Lauf1.HasValue) { pk.Lauf1 = null; dtLauf1.Checked = false; }
            SyncRuns();
            LoadPackNo();   // Packen-Nr. aus den Packen-Schritten (PackageForm.PackNo.cs)
            loading = was;
        }

        // Laeufe-Feld und Typ an das Paket angleichen (nach jeder Aenderung)
        void SyncRuns()
        {
            bool was = loading; loading = true;
            PackageStep h = StepsView().Head;
            nRuns.Enabled = h != null && pk.Type == PackageType.Kampagne;
            int n = h != null ? pk.RunCount : 1;
            nRuns.Value = Math.Max(nRuns.Minimum, Math.Min(nRuns.Maximum, n));
            if (!pk.TypeIsSet && cbType.SelectedIndex != (int)pk.Type) cbType.SelectedIndex = (int)pk.Type;
            loading = was;
        }

        // Laufanzahl im Kopf geaendert: der Hauptlauf bekommt n Laeufe, Schritte "zu jedem Lauf" folgen; eigene Wiederholungen
        // "fuer alle Laeufe" ebenfalls (gleiche Anzahl wie bisher).
        void RunsChanged()
        {
            PackageStep h = StepsView().Head;
            if (loading || h == null) return;
            int n = (int)nRuns.Value;
            if (n == pk.RunCount) return;
            pk.SetRunCount(n);
            if (cur != null) { bool was = loading; loading = true; BindWhen(cur); loading = was; }
            Changed();
        }

        // "Lauf 1 am" geaendert: Wochentag von Tag 1 so setzen, dass Wochentags-Wiederholungen auf echten Wochentagen liegen
        void Lauf1Changed()
        {
            if (loading) return;
            pk.Lauf1 = dtLauf1.Checked ? dtLauf1.Value.Date : (DateTime?)null;
            if (pk.Lauf1.HasValue)
            {
                DateTime fix = pk.SyncDay1(pk.Lauf1.Value);
                if (fix != pk.Lauf1.Value)
                {
                    pk.Lauf1 = fix; loading = true; dtLauf1.Value = fix; loading = false;
                    Say(Package.Day1Hint + " – auf " + fix.ToString("dd.MM.yyyy") + " gesetzt.");
                }
            }
            if (cur != null) { bool was = loading; loading = true; BindWhen(cur); loading = was; }
            Changed();
        }

        // Alle Schritte als Paket-Sicht: PackageForm.Table.cs (StepsView)

        void FirstRunTyped()
        {
            if (loading) return;
            string p; int no;
            if (Package.ParseRun(tFirst.Text, out p, out no))
            {
                prefix = p;
                StepsView().Renumber(no);   // Lauf-Schritte zaehlen ab hier (Abstaende zwischen den Schritten bleiben)
                if (cur != null) { loading = true; nStartNo.Value = Math.Max(nStartNo.Minimum, Math.Min(nStartNo.Maximum, cur.StartNo)); loading = false; }
            }
            else if (tFirst.Text.Trim().Length == 0) prefix = "";
            Changed();
        }

        // Startnummer (ZZZZ) fuer einen neuen Lauf-Schritt: wie die vorhandenen Laeufe, sonst aus "Erste Laufnummer"
        int NewRunNo()
        {
            int f = StepsView().FirstRunNo;
            string p; int no;
            return f >= 0 ? f : (Package.ParseRun(tFirst.Text, out p, out no) ? no : 1);
        }

        // Pruefung der Kopfzeile; null = in Ordnung, sonst Meldung (fuer Hinweisliste, Speichern und Uebernehmen)
        string ProductError() { return Product.Length > 0 && !Package.IsProductCode(Product) ? "Produktcode ungültig: 2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (ohne Leerzeichen), z. B. AB1." : null; }

        string FirstRunError()
        {
            string p; int no;
            return tFirst.Text.Trim().Length > 0 && !Package.ParseRun(tFirst.Text, out p, out no) ? "Erste Laufnummer: bitte 8 Ziffern eingeben (z. B. 13570030)." : null;
        }

        void UpdateRunsLabel()
        {
            SyncPackNo();
            string err = ProductError() ?? FirstRunError() ?? PackNoError() ?? StepsView().Overflow();
            bool runs = StepsView().FirstRunNo >= 0;
            if (err != null) lRuns.Text = err;
            else if (!runs) lRuns.Text = prefix.Length > 0 ? "noch kein Lauf im Ablauf" : "";
            else
            {
                Package v = StepsView();
                lRuns.Text = prefix.Length > 0 ? "→ " + v.FirstRun + ", " + Package.FormatRun(prefix, v.FirstRunNo + 1) + " …" : "ohne 8-stellige Nummer: Läufe 1, 2, 3 …";
            }
            lRuns.ForeColor = err != null ? Color.Firebrick : Section.HelpColor;
            tip.SetToolTip(lRuns, lRuns.Text);
            LayoutHead(headH);
            SyncRuns();
        }

        // Klick auf einen Pruefhinweis: erst nach der Mausverarbeitung der Liste ausfuehren (Bind baut die Hinweisliste neu auf,
        // waehrend die ListBox ihren Klick noch abarbeitet); Felder unter "Mehr Optionen" klappen dabei auf.
        void HintClicked(Hint h)
        {
            if (h == null || IsDisposed || !IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate { GoToHint(h); });
        }

        void GoToHint(Hint h)
        {
            if (IsDisposed || h == null) return;
            if (h.Kids != null) { ToggleGroup(h); return; }                       // Gruppenkopf: auf-/zuklappen
            if (h.UnvalId != null) { AddToList(h.UnvalId, h.UnvalShort); return; }   // nicht validiert: zur Liste hinzufuegen / vormerken
            if (h.Step != null && steps.Contains(h.Step))
            {
                if (h.Run >= 0) SelectRun(h.Step, h.Run); else if (h.Step != cur) Bind(h.Step);   // erster Beteiligter
                strip.Mark(h.Step2 != null && steps.Contains(h.Step2) ? h.Step2 : null, h.Run2);    // zweiter Beteiligter rot umrandet
            }
            Control f = h.Field;
            if (f == null || f.IsDisposed) return;
            if (!more && (f == nStartNo || f == tIdent || f == tNote || f == nEvery)) SetMore(true);
            if (f.CanFocus) f.Focus();
        }
    }
}
