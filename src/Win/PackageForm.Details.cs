using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Teil 2 (Schritt-Details). Standardansicht schlank: "Was? Wo im Plan?" (Titel, Raum, Art, ggf. Herstellung) und
    // "Wann?" (Tag) nebeneinander, darunter das Equipment (Position, Geraet, Notiz). "Mehr Optionen" klappt Wiederholen/Abstand/
    // eigene Nummer, abwechselnd/Ausweich-Geraet und Kennung/Notiz auf; eingeklappt fasst die Schaltflaeche gesetzte Werte zusammen.
    partial class PackageForm
    {
        readonly EditMarker marker = new EditMarker();
        readonly Panel scroll = new Panel(), body = new Panel(), equipRows = new Panel();
        Section gWhat, gWhen, gExtra, gEquip;
        readonly TextBox tTitle = new TextBox(), tSubNos = new TextBox(), tIdent = new TextBox(), tNote = new TextBox();
        readonly ComboBox cbKind = new ComboBox(), cbRow = new ComboBox(), cbVar = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        Label lTitle, lKind, lSub, lSubHelp, lDay, lCount, lCountUnit, lInterval, lIntervalUnit, lStartNo, lNoEx, lRow, rowHint, lVar, lSum, lIdent, lIdentHelp, lNote, lNoEquip;
        readonly Label[] equipHead = new Label[5];
        readonly Button bPos = new Button(), bMore = new Button();
        readonly ContextMenuStrip menuPos = new ContextMenuStrip();
        readonly List<string> varValues = new List<string>();   // Werte zu cbVar ("" = keine Angabe)
        bool laying, more;

        static void Grow(params Control[] cs) { foreach (Control c in cs) c.Anchor = TLR; }
        static void Gray(params Label[] ls) { foreach (Label l in ls) l.ForeColor = Section.HelpColor; }
        int LabelW(params string[] texts) { int m = 0; foreach (string t in texts) m = Math.Max(m, TextRenderer.MeasureText(t, Font).Width); return m + 6; }

        Section Group(string title, string help, int w)
        {
            Section g = new Section(title, help, false, w);
            body.Controls.Add(g); return g;
        }

        void Num(NumericUpDown n, int min, int max, int x, int w, Section g)
        {
            n.Minimum = min; n.Maximum = max; n.Value = Math.Max(min, 1); n.SetBounds(x, 0, w, IH); n.TextAlign = HorizontalAlignment.Right;
            g.Controls.Add(n);
        }

        Control BuildDetails()
        {
            Panel wrap = new Panel(); wrap.Tag = "keep"; wrap.Dock = DockStyle.Fill; wrap.Padding = new Padding(8, 8, 8, 8);
            Section sec = new Section("2   Schritt-Details", "", true, 700);
            sec.Dock = DockStyle.Fill;
            marker.Height = Font.Height + 14; marker.TabStop = false;
            scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.Tag = "keep";
            body.Dock = DockStyle.Top; body.Tag = "keep"; body.Width = 680;   // Resize -> LayoutDetails (PackageForm.Sizing.cs)
            scroll.Controls.Add(body);
            Panel gap = new Panel(); gap.Tag = "keep"; gap.Dock = DockStyle.Top; gap.Height = 8;
            sec.Controls.Add(scroll); sec.Controls.Add(gap); sec.Controls.Add(marker);
            wrap.Controls.Add(sec);

            // Was? Wo im Plan? – der Raum steht direkt unter dem Titel
            gWhat = Group("Was? Wo im Plan?", "Titel, Raum (Block im Plan) und Art des Schritts", 280);
            int lw = LabelW("Unterlauf-Nr.:", "Titel:", "Wo im Plan?", "Art:", "Herstellung:"), x = 8 + lw, w = 280 - 8 - x;
            lTitle = Lbl(gWhat, "Titel:", 8, lw); tTitle.SetBounds(x, 0, w, IH);
            lRow = Lbl(gWhat, "Wo im Plan?", 8, lw); lRow.Font = new Font(Font, FontStyle.Bold);
            cbRow.SetBounds(x, 0, w, IH); cbRow.DropDownStyle = ComboBoxStyle.DropDownList; cbRow.Items.AddRange(rowNames.ToArray());
            rowHint = Lbl(gWhat, "", x, w); rowHint.ForeColor = Color.DarkOrange; rowHint.AutoEllipsis = true;
            tip.SetToolTip(cbRow, "Raum (Block) im Plan, in dem der Schritt steht. Beim Kampagnenstart je Lauf änderbar.");
            lKind = Lbl(gWhat, "Art:", 8, lw); cbKind.SetBounds(x, 0, w, IH); cbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            cbKind.Items.AddRange(new object[] { "Normaler Schritt", "Lauf mit Laufnummer", "Unterlauf (Splitten/Einfrieren)" });
            lVar = Lbl(gWhat, "Herstellung:", 8, lw); cbVar.SetBounds(x, 0, w, IH); cbVar.DropDownStyle = ComboBoxStyle.DropDownList;
            tip.SetToolTip(cbVar, "Herstellungsvariante, die beim Übernehmen am Block gesetzt wird (z. B. Puffer mobil/stationär).");
            lSub = Lbl(gWhat, "Unterlauf-Nr.:", 8, lw); tSubNos.SetBounds(x, 0, w, IH);
            lSubHelp = Lbl(gWhat, "Komma = nächster Lauf, „/“ = mehrere (z. B. 4186/4187, 4188); 4 Ziffern + Praefix der Laufnummer", x, w);
            lSubHelp.Height = 3 * Font.Height + 2; lSubHelp.TextAlign = ContentAlignment.TopLeft;
            gWhat.Controls.AddRange(new Control[] { tTitle, cbRow, cbKind, cbVar, tSubNos });
            Grow(tTitle, cbKind, cbRow, cbVar, rowHint, tSubNos, lSubHelp); Gray(lSubHelp);
            tip.SetToolTip(cbKind, "Normaler Schritt: Titel (bei Wiederholung „Titel 1, 2, 3“). Lauf: bekommt die 8-stellige Laufnummer. Unterlauf: Splitten/Einfrieren eines Laufs.");

            // Wann?
            gWhen = Group("Wann?", "Tag 1 = Startdatum der Kampagne", 270);
            lw = LabelW("Tag der Kampagne:", "Wiederholen:", "Abstand:", "Laufnummer ab:"); x = 8 + lw;
            lDay = Lbl(gWhen, "Tag der Kampagne:", 8, lw); Num(nDay, 1, 366, x, 60, gWhen);
            lSum = Lbl(gWhen, "", 8, 254); lSum.AutoEllipsis = true; Gray(lSum); Grow(lSum);
            lCount = Lbl(gWhen, "Wiederholen:", 8, lw); Num(nCount, 1, 99, x, 60, gWhen); lCountUnit = Lbl(gWhen, "mal", x + 66, 270 - 8 - x - 66);
            lInterval = Lbl(gWhen, "Abstand:", 8, lw); Num(nInterval, 1, 30, x, 60, gWhen); lIntervalUnit = Lbl(gWhen, "Tage", x + 66, 270 - 8 - x - 66);
            lStartNo = Lbl(gWhen, "Laufnummer ab:", 8, lw); Num(nStartNo, 0, 99999, x, 66, gWhen); lNoEx = Lbl(gWhen, "", x + 72, 270 - 8 - x - 72);
            lNoEx.AutoEllipsis = true;
            Grow(lCountUnit, lIntervalUnit, lNoEx); Gray(lNoEx);
            tip.SetToolTip(nStartNo, "Hintere 4 Stellen (ZZZZ) des ersten Laufs dieses Schritts; normalerweise wie „Erste Laufnummer“ oben.");

            // Mehr Optionen (ein-/ausklappen)
            bMore.SetBounds(0, 0, 400, Theme.Current.ButtonH - 2); bMore.TextAlign = ContentAlignment.MiddleLeft; bMore.Click += delegate { SetMore(!more); };
            body.Controls.Add(bMore);
            tip.SetToolTip(bMore, "Wiederholen, Abstand, eigene Laufnummer, abwechselnde Geräte, Ausweich-Gerät, Kennung und Notiz");

            // Zusaetze
            gExtra = Group("Zusätze", "Optional; erscheinen im Plan unter dem Equipment", 500);
            lw = LabelW("Kennung je Lauf:", "Notiz:"); x = 8 + lw; w = 500 - 8 - x;
            lIdent = Lbl(gExtra, "Kennung je Lauf:", 8, lw); tIdent.SetBounds(x, 0, w, IH);
            lIdentHelp = Lbl(gExtra, "z. B. PA 40010001, PA 40010017 – Komma = nächster Lauf; im Plan rot, groß und fett als letzte Zeile", x, w);
            lIdentHelp.AutoEllipsis = true; lIdentHelp.Height = Font.Height + 2;
            lNote = Lbl(gExtra, "Notiz:", 8, lw); tNote.SetBounds(x, 0, w, IH);
            gExtra.Controls.AddRange(new Control[] { tIdent, tNote });
            Grow(tIdent, tNote, lIdentHelp); Gray(lIdentHelp);
            tip.SetToolTip(lIdentHelp, lIdentHelp.Text);

            // Equipment
            gEquip = Group("Equipment", "Gerät aus der validierten Liste wählen oder eintippen (orange = nicht validiert).", 500);
            string[] heads = { "Position", "Gerät", "abwechselnd", "Ausweich-Gerät", "Notiz" };
            for (int i = 0; i < 5; i++) { equipHead[i] = Lbl(gEquip, heads[i], 0, 80); equipHead[i].Height = Font.Height + 4; equipHead[i].Font = new Font(Font, FontStyle.Bold); equipHead[i].AutoEllipsis = true; }
            equipRows.Tag = "keep"; gEquip.Controls.Add(equipRows);
            lNoEquip = Lbl(gEquip, "Keine Equipment-Positionen – mit „+ Position hinzufügen“ ergänzen.", 8, 480); Gray(lNoEquip); Grow(lNoEquip);
            Btn(bPos, "+ Position hinzufügen ▾", 8, 0, 190, delegate { menuPos.Show(bPos, new Point(0, bPos.Height)); });
            gEquip.Controls.Add(bPos);
            BuildPosMenu();
            bMore.TabIndex = 3; gEquip.TabIndex = 4; gExtra.TabIndex = 5;

            foreach (TextBox t in new[] { tTitle, tSubNos, tIdent, tNote }) Ui.SimpleMenu(t);
            tTitle.TextChanged += delegate { if (Edit()) { cur.Title = tTitle.Text.Trim(); Changed(); } };
            cbKind.SelectedIndexChanged += delegate
            {
                if (!Edit() || cbKind.SelectedIndex < 0) return;
                bool wasNormal = cur.Kind == StepKind.Normal;
                cur.Kind = (StepKind)cbKind.SelectedIndex;
                if (wasNormal && cur.Kind != StepKind.Normal) { cur.StartNo = NewRunNo(); loading = true; nStartNo.Value = Math.Min(nStartNo.Maximum, cur.StartNo); loading = false; }
                Changed();
            };
            tSubNos.TextChanged += delegate { if (Edit()) { cur.SubNos = tSubNos.Text.Trim(); Changed(); } };
            nDay.ValueChanged += delegate { if (Edit()) { cur.Day = (int)nDay.Value; Changed(); } };
            nCount.ValueChanged += delegate { if (Edit()) { cur.Count = (int)nCount.Value; Changed(); } };
            nInterval.ValueChanged += delegate { if (Edit()) { cur.Interval = (int)nInterval.Value; Changed(); } };
            nStartNo.ValueChanged += delegate { if (Edit()) { cur.StartNo = (int)nStartNo.Value; Changed(); } };
            cbRow.SelectedIndexChanged += delegate { if (Edit()) { cur.Row = cbRow.Text; loading = true; FillVariants(cur); loading = false; Changed(); } };
            cbVar.SelectedIndexChanged += delegate { if (Edit() && cbVar.SelectedIndex >= 0) { cur.Variant = varValues[cbVar.SelectedIndex]; Changed(); } };
            tIdent.TextChanged += delegate { if (Edit()) { cur.Ident = tIdent.Text.Trim(); Changed(); } };
            tNote.TextChanged += delegate { if (Edit()) { cur.Note = tNote.Text.Trim(); Changed(); } };
            return wrap;
        }

        bool Edit() { return !loading && cur != null; }

        void BuildPosMenu()
        {
            menuPos.Items.Clear();
            foreach (EquipPosition p in cat.Positions) { EquipPosition pp = p; menuPos.Items.Add(p.Name + "  (" + p.Short + ")", null, delegate { AddPosition(pp.Short); }); }
        }

        void SetMore(bool on) { more = on; if (cur != null) Bind(cur); }

        static string VarText(string v) { return VariantSet.Display(v).Replace("stationaer", "stationär"); }

        // Herstellung: Varianten der Zeile (RowInfo.Variants), bei mehreren "beides"; gesetzter Wert bleibt auch ohne Zeilen-Varianten sichtbar
        void FillVariants(PackageStep s)
        {
            string[] vs; if (!rowVars.TryGetValue(s.Row, out vs)) vs = new string[0];
            cbVar.Items.Clear(); varValues.Clear();
            cbVar.Items.Add("– keine Angabe –"); varValues.Add("");
            foreach (string v in vs) { cbVar.Items.Add(VarText(v)); varValues.Add(v); }
            if (vs.Length > 1) { string all = VariantSet.Merge("", "", string.Join("|", vs)); cbVar.Items.Add((vs.Length == 2 ? "beides (" : "alle (") + VarText(all) + ")"); varValues.Add(all); }
            string curV = VariantSet.Merge("", "", s.Variant);
            if (!varValues.Contains(curV)) { cbVar.Items.Add(VarText(curV)); varValues.Add(curV); }
            cbVar.SelectedIndex = varValues.IndexOf(curV);
        }

        // ---------- Schritt anzeigen ----------

        void Bind(PackageStep s)
        {
            cur = s;
            loading = true;
            body.SuspendLayout();
            while (equipRows.Controls.Count > 0) { Control c = equipRows.Controls[0]; equipRows.Controls.RemoveAt(0); c.Dispose(); }
            if (s != null)
            {
                foreach (EquipAssign a in s.Equipment) equipRows.Controls.Add(new EquipRow(a, cat, Product, delegate { Changed(); }, RemoveAssign));
                tTitle.Text = s.Title;
                cbKind.SelectedIndex = (int)s.Kind; tSubNos.Text = s.SubNos;
                if (s.Row.Length > 0 && !cbRow.Items.Contains(s.Row)) cbRow.Items.Add(s.Row);   // unbekannter Block bleibt sichtbar
                cbRow.SelectedIndex = cbRow.Items.IndexOf(s.Row);
                FillVariants(s);
                nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, s.Day));
                nCount.Value = Math.Max(1, Math.Min(99, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
                nStartNo.Value = Math.Max(0, Math.Min(nStartNo.Maximum, s.StartNo));
                tNote.Text = s.Note; tIdent.Text = s.Ident;
            }
            body.Visible = s != null;
            body.ResumeLayout();
            loading = false;
            UpdateView(true);
        }

        void AddPosition(string shortName)
        {
            if (cur == null) return;
            EquipAssign a = new EquipAssign(); a.Short = shortName;
            cur.Equipment.Add(a);
            dirty = true;
            Bind(cur);
        }

        void RemoveAssign(EquipAssign a) { if (cur == null) return; cur.Equipment.Remove(a); dirty = true; Bind(cur); }

        void Changed() { if (loading) return; dirty = true; UpdateView(false); }

        // Alles nachziehen: Liste, Felder ein-/ausblenden, Kopfzeile, Vorschau, Pruefhinweise; scrollTo = Kalender zum Schritt scrollen.
        void UpdateView(bool scrollTo)
        {
            FillList();
            LayoutDetails();
            UpdateHeader();
            int n = cur != null ? No(cur) : 0;
            marker.Active = cur != null;
            marker.Text = cur != null
                ? "Du bearbeitest gerade: Schritt " + n + " von " + steps.Count + " – " + TitleOf(cur) + "   (Tag " + DaysOf(cur) + ", " + (cur.Row.Length > 0 ? cur.Row : "Raum fehlt") + ")"
                : "Kein Schritt gewählt – links im Ablauf einen Schritt anklicken oder „+ Schritt hinzufügen“.";
            marker.Invalidate();
            UpdateRunsLabel();
            bUp.Enabled = bDown.Enabled = bDup.Enabled = bDel.Enabled = cur != null;
            UpdatePreview(scrollTo);
            UpdateHints();
        }

        // Zeile in einer Gruppe: setzt Sichtbarkeit und Hoehe; liefert die naechste y-Position.
        int Row(int y, bool show, params Control[] cs)
        {
            int h = 0;
            foreach (Control c in cs) { c.Visible = show; c.Top = y; h = Math.Max(h, c.Height); }
            return show ? y + Math.Max(h, IH) + 6 : y;
        }

        // Equipment mit Spalten abwechselnd/Ausweich: aufgeklappt oder wenn ein Wert gesetzt ist (nichts verschwindet still)
        bool WideEquip { get { return more || (cur != null && cur.Equipment.Exists(delegate(EquipAssign a) { return a.Alternate || a.Fallback.Length > 0; })); } }

        // Zusammenfassung der eingeklappten Optionen auf der Schaltflaeche
        string MoreSummary(PackageStep s)
        {
            List<string> p = new List<string>();
            if (s.Count > 1) p.Add(s.Count + "× alle " + s.Interval + (s.Interval == 1 ? " Tag" : " Tage"));
            if (s.Equipment.Exists(delegate(EquipAssign a) { return a.Alternate; })) p.Add("abwechselnd");
            if (s.Equipment.Exists(delegate(EquipAssign a) { return a.Fallback.Length > 0; })) p.Add("Ausweich");
            if (s.Ident.Length > 0) p.Add("Kennung");
            if (s.Note.Length > 0) p.Add("Notiz");
            return p.Count > 0 ? "  –  gesetzt: " + string.Join(", ", p.ToArray()) : "";
        }

        void LayoutDetails()
        {
            if (laying || cur == null) return;
            laying = true;
            PackageStep s = cur;
            bool sub = s.Kind == StepKind.SubRun, rep = s.Count > 1, num = s.Numbered;
            string[] vs;
            bool var = (rowVars.TryGetValue(s.Row, out vs) && vs.Length > 0) || s.Variant.Length > 0;

            Baustein b = cat.Baustein_(s.Source);
            bool off = b != null && b.Row.Length > 0 && s.Row.Length > 0 && s.Row != b.Row;   // z. B. Packen in Suite 2
            rowHint.Text = off ? "Standard-Raum ist „" + b.Row + "“ (Sonderaktion?)" : "";
            tip.SetToolTip(rowHint, rowHint.Text);
            int y = gWhat.ContentTop;
            y = Row(y, true, lTitle, tTitle); y = Row(y, true, lRow, cbRow); y = Row(y, off, rowHint) - (off ? 4 : 0);
            y = Row(y, true, lKind, cbKind); y = Row(y, var, lVar, cbVar);
            y = Row(y, sub, lSub, tSubNos); y = Row(y, sub, lSubHelp);
            int hWhat = y + 2;

            y = gWhen.ContentTop;
            y = Row(y, true, lDay, nDay);
            lSum.Text = (rep ? s.Count + "× alle " + s.Interval + (s.Interval == 1 ? " Tag" : " Tage") + " (Tag " + DaysOf(s) + ")" : "einmalig") + (num ? " · " + s.Label(prefix, 0) : "");
            tip.SetToolTip(lSum, lSum.Text);
            y = Row(y, !more, lSum);
            y = Row(y, more, lCount, nCount, lCountUnit); y = Row(y, more && rep, lInterval, nInterval, lIntervalUnit); y = Row(y, more && num, lStartNo, nStartNo, lNoEx);
            lCountUnit.Text = rep ? "mal" : "mal (einmalig)";
            lStartNo.Text = s.Kind != StepKind.Normal ? "Laufnummer ab:" : "Erste Nummer:";
            lNoEx.Text = num ? "→ " + s.RunNo(prefix, 0) : "";
            tip.SetToolTip(lNoEx, lNoEx.Text);
            int hWhen = y + 2;

            y = gExtra.ContentTop;
            y = Row(y, true, lIdent, tIdent); y = Row(y, true, lIdentHelp) - 2; y = Row(y, true, lNote, tNote);
            int hExtra = y + 2;

            int w = body.ClientSize.Width, g = 8;
            int a = Math.Max((w - g) / 2, Math.Min(w - g - 250, (w - g) * 55 / 100)), h = Math.Max(hWhat, hWhen);   // Was? Wo? | Wann?
            gWhat.SetBounds(0, 0, a, h); gWhen.SetBounds(a + g, 0, w - a - g, h);
            y = h + 4;
            bMore.Text = (more ? "▾ Weniger Optionen" : "▸ Mehr Optionen (Wiederholen, Abwechseln, Ausweich, Kennung, Notiz)" + MoreSummary(s));
            bMore.SetBounds(0, y, w, bMore.Height); y = bMore.Bottom + 4;
            gEquip.SetBounds(0, y, w, ArrangeEquip(w - 16)); y = gEquip.Bottom + g;
            gExtra.Visible = more;
            if (more) { gExtra.SetBounds(0, y, w, hExtra); y = gExtra.Bottom; }
            body.Height = y + 4;
            laying = false;
        }

        int ArrangeEquip(int iw)
        {
            bool any = equipRows.Controls.Count > 0, wide = WideEquip;
            int[] x = EquipRow.Cols(iw, wide);
            int y = gEquip.ContentTop;
            for (int i = 0; i < 5; i++) equipHead[i].SetBounds(8 + x[i], 0, Math.Max(0, x[i + 1] - x[i] - 6), equipHead[i].Height);
            y = Row(y, any, equipHead) - 2;
            equipHead[2].Visible = equipHead[3].Visible = any && wide;
            int ry = 0;
            foreach (Control c in equipRows.Controls) { EquipRow r = (EquipRow)c; r.Arrange(iw, wide); r.Location = new Point(0, ry); ry += r.Height; }
            equipRows.SetBounds(8, y, x[6], ry);
            y += ry + 4;
            y = Row(y, !any, lNoEquip);
            y = Row(y, true, bPos);
            return y + 4;
        }
    }
}
