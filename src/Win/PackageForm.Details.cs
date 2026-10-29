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
        readonly TextBox tTitle = new TextBox(), tSubNos = new TextBox(), tIdent = new TextBox(), tNote = new TextBox(), tManualNo = new TextBox();
        readonly ComboBox cbKind = new ComboBox(), cbRow = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        Label lTitle, lKind, lSub, lSubHelp, lRow, rowHint, lVar, lIdent, lIdentHelp, lNote, lNoEquip, lManual;
        readonly Label[] equipHead = new Label[5];
        readonly Button bPos = new Button(), bMore = new Button();
        readonly ContextMenuStrip menuPos = new ContextMenuStrip();
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
            int lw = LabelW("Unterlauf-Nr.:", "Titel:", "Wo im Plan?", "Art:", "Puffer-Typ:", "Gebinde:", "Laufnummer (von Hand):"), x = 8 + lw, w = 280 - 8 - x;
            lTitle = Lbl(gWhat, "Titel:", 8, lw); tTitle.SetBounds(x, 0, w, IH);
            lRow = Lbl(gWhat, "Wo im Plan?", 8, lw); lRow.Font = new Font(Font, FontStyle.Bold);
            cbRow.SetBounds(x, 0, w, IH); cbRow.DropDownStyle = ComboBoxStyle.DropDownList; cbRow.Items.AddRange(rowNames.ToArray());
            rowHint = Lbl(gWhat, "", x, w); rowHint.ForeColor = Color.DarkOrange; rowHint.AutoEllipsis = true;
            tip.SetToolTip(cbRow, "Raum (Block) im Plan, in dem der Schritt steht. Beim Kampagnenstart je Lauf änderbar.");
            lKind = Lbl(gWhat, "Art:", 8, lw); cbKind.SetBounds(x, 0, w, IH); cbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            cbKind.Items.AddRange(new object[] { "Sonstiges (ohne Laufnummer)", "Lauf-Schritt mit Laufnummer", "Unterlauf (Splitten/Einfrieren)" });   // Eintrag 2: beim Hauptlauf "Hauptlauf"
            lManual = Lbl(gWhat, "Laufnummer (von Hand):", 8, lw); tManualNo.SetBounds(x, 0, w, IH); tManualNo.MaxLength = 8; gWhat.Controls.Add(tManualNo);
            tip.SetToolTip(tManualNo, "Nummer des Packens, 8 Ziffern (z. B. 12340001); wird nicht vorgeschlagen. Im Plan: „Packen (Nr. 12340001)“, zählt nicht als Kampagnenlauf.");
            lVar = Lbl(gWhat, "Puffer-Typ:", 8, lw);
            tip.SetToolTip(lVar, "Ergibt sich aus dem Baustein (Puffer mobil / Puffer stationär); ein Schritt = ein Puffer.");
            lSub = Lbl(gWhat, "Unterlauf-Nr.:", 8, lw); tSubNos.SetBounds(x, 0, w, IH);
            lSubHelp = Lbl(gWhat, "Komma = nächster Lauf, „/“ = mehrere (z. B. 4186/4187, 4188); 4 Ziffern + Praefix der Laufnummer", x, w);
            lSubHelp.Height = 3 * Font.Height + 2; lSubHelp.TextAlign = ContentAlignment.TopLeft;
            gWhat.Controls.AddRange(new Control[] { tTitle, cbRow, cbKind, tSubNos });
            Grow(tTitle, cbKind, cbRow, rowHint, tSubNos, lSubHelp, tManualNo); Gray(lSubHelp);
            tip.SetToolTip(cbKind, "Hauptlauf bzw. Lauf-Schritt: bekommt die 8-stellige Laufnummer. Unterlauf: Splitten/Einfrieren eines Laufs. Sonstiges: nur der Titel (bei Wiederholung „Titel 1, 2, 3“).");
            BuildAnsatz(lw, x, w);   // Herstellung mit Anzahl, Ansatz (PackageForm.When.cs)
            BuildWhen();             // Wann? (PackageForm.When.cs)

            // Mehr Optionen (ein-/ausklappen)
            bMore.SetBounds(0, 0, 400, Theme.Current.ButtonH - 2); bMore.TextAlign = ContentAlignment.MiddleLeft; bMore.Click += delegate { SetMore(!more); };
            body.Controls.Add(bMore);
            tip.SetToolTip(bMore, "Eigene Laufnummer, abwechselnde Geräte, Ausweich-Gerät, Kennung und Notiz");

            // Zusaetze
            gExtra = Group("Zusätze", "Optional; erscheinen im Plan unter dem Equipment", 500);
            lw = LabelW("Kennung Start:", "Notiz:"); x = 8 + lw; w = 500 - 8 - x;
            tNote.Multiline = true; tNote.AcceptsReturn = true; tNote.ScrollBars = ScrollBars.Vertical; tNote.WordWrap = true; tNote.Font = new Font(Font, FontStyle.Italic);
            lIdent = Lbl(gExtra, "Kennung Start:", 8, lw); tIdent.SetBounds(x, 0, w, IH);
            lIdentHelp = Lbl(gExtra, "z. B. PA 40010001 (+1 je Lauf); im Plan rot, groß, fett als letzte Zeile", x, w);
            lIdentHelp.AutoEllipsis = true; lIdentHelp.Height = Font.Height + 2;
            lNote = Lbl(gExtra, "Notizen – stehen bei jedem Lauf (je Zeile eine Notiz)", 8, 480); tNote.SetBounds(8, 0, 480, 3 * Font.Height + 10);
            gExtra.Controls.AddRange(new Control[] { tIdent, tNote });
            Grow(tIdent, tNote, lIdentHelp, lNote); Gray(lIdentHelp);
            tip.SetToolTip(tNote, "Enter = nächste Notiz. Jede Zeile steht im Plan als eigene Notiz (kursiv, zählt nicht als Inhalt) bei jedem Lauf.");
            tip.SetToolTip(lIdentHelp, lIdentHelp.Text);
            BuildIdents();   // Kennungstabelle + Materialvorlage (PackageForm.Idents.cs)

            // Equipment
            gEquip = Group("Equipment", "Gerät aus der validierten Liste wählen oder eintippen (orange = nicht validiert).", 500);
            string[] heads = { "Position", "Gerät", "abwechselnd", "Ausweich-Gerät", "Notiz" };
            for (int i = 0; i < 5; i++) { equipHead[i] = Lbl(gEquip, heads[i], 0, 80); equipHead[i].Height = Font.Height + 4; equipHead[i].Font = new Font(Font, FontStyle.Bold); equipHead[i].AutoEllipsis = true; }
            equipRows.Tag = "keep"; gEquip.Controls.Add(equipRows);
            lNoEquip = Lbl(gEquip, "Keine Equipment-Positionen – mit „+ Position hinzufügen“ ergänzen.", 8, 480); Gray(lNoEquip); Grow(lNoEquip);
            Btn(bPos, "+ Position hinzufügen ▾", 8, 0, 190, delegate { menuPos.Show(bPos, new Point(0, bPos.Height)); });
            gEquip.Controls.Add(bPos);
            BuildPosMenu();
            BuildPuffer();   // Karten je Puffer (PackageForm.PufferCards.cs)
            bMore.TabIndex = 3; gEquip.TabIndex = 4; gExtra.TabIndex = 5;

            foreach (TextBox t in new[] { tTitle, tSubNos, tIdent, tNote }) Ui.SimpleMenu(t);
            tTitle.TextChanged += delegate { if (Edit()) { cur.Title = tTitle.Text.Trim(); Changed(); } };
            cbKind.SelectedIndexChanged += delegate
            {
                if (!Edit() || cbKind.SelectedIndex < 0) return;
                if (cur == StepsView().Head && (StepKind)cbKind.SelectedIndex != StepKind.Run)
                {   // der Hauptlauf ist die Grundlage der Kampagne: Art nicht umstellbar
                    loading = true; cbKind.SelectedIndex = (int)StepKind.Run; loading = false;
                    Say("Der Hauptlauf bleibt Lauf-Art; weiteren Lauf-Schritt anlegen.");
                    return;
                }
                bool wasNormal = cur.Kind == StepKind.Normal;
                cur.Kind = (StepKind)cbKind.SelectedIndex;
                if (wasNormal && cur.Kind != StepKind.Normal)
                {
                    cur.StartNo = NewRunNo(); loading = true; nStartNo.Value = Math.Min(nStartNo.Maximum, cur.StartNo); loading = false;
                    PackageStep h = StepsView().Head;
                    if (h != null && h != cur && cur.Bind == StepBind.Frei && !cur.IsBackup && !Package.HasBackup(steps, cur)) SetJe(cur);   // Lauf-Schritt/Unterlauf folgen jedem Lauf
                }
                dirty = true; BindWhen(cur); UpdateView(false);
            };
            tManualNo.TextChanged += delegate { if (Edit()) { cur.ManualNo = PackageStep.IsManualNo(tManualNo.Text.Trim()) ? tManualNo.Text.Trim() : ""; tManualNo.ForeColor = tManualNo.Text.Trim().Length == 0 || cur.ManualNo.Length > 0 ? SystemColors.WindowText : Color.Firebrick; Changed(); } };
            tSubNos.TextChanged += delegate { if (Edit()) { cur.SubNos = tSubNos.Text.Trim(); Changed(); } };
            cbRow.SelectedIndexChanged += delegate { if (Edit()) { cur.Row = cbRow.Text; loading = true; FillVariants(cur); loading = false; Changed(); } };
            tIdent.TextChanged += delegate { if (Edit()) { cur.Ident = ""; cur.IdentStart = tIdent.Text.Trim(); Changed(); } };
            tNote.TextChanged += delegate { if (Edit()) { cur.Note = tNote.Text.Replace("\r", "").Trim(); Changed(); } };
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

        // ---------- Schritt anzeigen ----------

        void Bind(PackageStep s)
        {
            if (s != cur) curRun = -1;   // anderer Schritt: Vorschau beginnt beim ersten Eintrag (ausser nextRun, Klick im Kalender)
            cur = s;
            loading = true;
            body.SuspendLayout();
            while (equipRows.Controls.Count > 0) { Control c = equipRows.Controls[0]; equipRows.Controls.RemoveAt(0); c.Dispose(); }
            if (s != null && s.IsBuffer) s.MigratePuffers();   // alter Modus -> eigene Puffer (verlustfrei, gleicher Plantext, kein "geändert")
            BindPuffer(s);   // Karten je Puffer (PackageForm.PufferCards.cs)
            if (s != null)
            {
                foreach (EquipAssign a in s.Equipment) equipRows.Controls.Add(new EquipRow(a, cat, Product, delegate { Changed(); }, RemoveAssign));
                tTitle.Text = s.Title;
                cbKind.Items[1] = s == StepsView().Head ? "Hauptlauf" : "Lauf-Schritt mit Laufnummer";
                cbKind.SelectedIndex = (int)s.Kind; tSubNos.Text = s.SubNos;
                tManualNo.Text = s.ManualNo; tManualNo.ForeColor = SystemColors.WindowText;
                if (s.Row.Length > 0 && !cbRow.Items.Contains(s.Row)) cbRow.Items.Add(s.Row);   // unbekannter Block bleibt sichtbar
                cbRow.SelectedIndex = cbRow.Items.IndexOf(s.Row);
                FillVariants(s); BindAnsatz(s); BindWhen(s);
                s.IdentToStart(StepsView().RunCountOf(s));   // alte Kommaliste -> Start + Ueberschreibungen (gleiches Ergebnis)
                tNote.Text = s.Note.Replace("\r", "").Replace("\n", "\r\n"); tIdent.Text = s.IdentStart;
            }
            body.Visible = s != null;
            body.ResumeLayout();
            loading = false;
            UpdateView(true);
            HookLeave(body);   // Feld verlassen = Rueckgaengig-Schnappschuss (PackageForm.Undo.cs)
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

        void Changed() { if (loading) return; dirty = true; UpdateView(false); if (!(ActiveControl is TextBoxBase)) CommitUndo(); }   // Text: erst beim Verlassen des Felds

        // Alles nachziehen: Liste, Felder ein-/ausblenden, Kopfzeile, Vorschau, Pruefhinweise; scrollTo = Kalender zum Schritt scrollen.
        void UpdateView(bool scrollTo)
        {
            Repin();   // feste Laeufe bleiben an ihrem Tag (PackageForm.Order.cs)
            if (Package.SyncBackups(steps) && cur != null && cur.IsBackup) { bool was = loading; loading = true; BindWhen(cur); loading = was; }   // Back-Up folgt dem Packen
            FillList();
            FillIdents();
            LayoutDetails();
            UpdateHeader();
            int n = cur != null ? No(cur) : 0;
            marker.Active = cur != null;
            marker.Text = cur != null
                ? "Du bearbeitest gerade: Schritt " + n + " von " + steps.Count + " – " + TitleOf(cur) + "   (" + WhenText(cur) + ", " + (cur.Row.Length > 0 ? cur.Row : "Raum fehlt") + ")"
                : "Kein Schritt gewählt – links im Ablauf einen Schritt anklicken oder „+ Schritt hinzufügen“.";
            marker.Invalidate();
            UpdateRunsLabel();
            bUp.Enabled = bDown.Enabled = bDup.Enabled = bDel.Enabled = cur != null;
            UpdatePreview(scrollTo);
            UpdateHints();
            RecordPins(cur);
        }

        // Zeile in einer Gruppe: setzt Sichtbarkeit und Hoehe; liefert die naechste y-Position.
        int Row(int y, bool show, params Control[] cs)
        {
            int h = 0;
            foreach (Control c in cs) { c.Visible = show; c.Top = y; h = Math.Max(h, c.Height); }
            return show ? y + Math.Max(h, IH) + 6 : y;
        }

        // Equipment mit Spalten abwechselnd/Ausweich: aufgeklappt oder wenn ein Wert gesetzt ist (nichts verschwindet still)
        bool WideEquip { get { return more || (cur != null && cur.AllEquip().Exists(delegate(EquipAssign a) { return a.Alternate || a.Fallback.Length > 0; })); } }

        // Zusammenfassung der eingeklappten Optionen auf der Schaltflaeche
        string MoreSummary(PackageStep s)
        {
            List<string> p = new List<string>();
            List<EquipAssign> all = s.AllEquip();
            if (all.Exists(delegate(EquipAssign a) { return a.Alternate; })) p.Add("abwechselnd");
            if (all.Exists(delegate(EquipAssign a) { return a.Fallback.Length > 0; })) p.Add("Ausweich");
            if (s.IdentList(StepsView().RunCountOf(s)).Length > 0) p.Add("Kennung");
            if (s.Note.Length > 0) p.Add(s.NoteTexts().Count > 1 ? "Notizen" : "Notiz");
            if (s.RunEvery > 1) p.Add("reicht für " + s.RunEvery + " Läufe");
            return p.Count > 0 ? "  –  gesetzt: " + string.Join(", ", p.ToArray()) : "";
        }

        void LayoutDetails()
        {
            if (laying || cur == null) return;
            laying = true;
            PackageStep s = cur;
            bool sub = s.Kind == StepKind.SubRun;
            bool var = s.IsBuffer && s.Puffers.Count == 0;   // Typ steht je Puffer in der Karte (PackageForm.PufferCards.cs)
            int w = body.ClientSize.Width, g = 8;
            int a = Math.Max((w - g) / 2, Math.Min(w - g - 250, (w - g) * 55 / 100));   // Breite Was? Wo? | Rest Wann?
            gWhat.Width = a; gWhen.Width = w - a - g;   // Feldbreiten (Anker) stehen vor dem Anordnen fest

            Baustein b = cat.Baustein_(s.Source);
            bool off = b != null && b.Row.Length > 0 && s.Row.Length > 0 && s.Row != b.Row;   // z. B. Packen in Suite 2
            rowHint.Text = off ? "Standard-Raum ist „" + b.Row + "“ (Sonderaktion?)" : "";
            tip.SetToolTip(rowHint, rowHint.Text);
            int y = gWhat.ContentTop;
            y = Row(y, true, lTitle, tTitle); y = Row(y, true, lRow, cbRow); y = Row(y, off, rowHint) - (off ? 4 : 0);
            y = Row(y, true, lKind, cbKind); y = LayoutVariants(y, var); y = LayoutAnsatz(y, s);
            y = Row(y, s.Source == Package.PackenName && !s.IsBackup, lManual, tManualNo);
            y = Row(y, sub, lSub, tSubNos); y = Row(y, sub, lSubHelp);
            int hWhat = y + 2, hWhen = LayoutWhen(s);   // Wann?: PackageForm.When.cs

            gExtra.Width = w;   // vor dem Anordnen, damit die Anker-Breiten (Tabelle) stimmen
            y = gExtra.ContentTop;
            y = Row(y, true, lIdent, tIdent); y = Row(y, true, lIdentHelp) - 2;
            gridId.SetBounds(8, gridId.Top, w - 16, gridId.Height); y = Row(y, showIdents, gridId);
            y = Row(y, true, lNote); y = Row(y, true, tNote) - 2;
            y = Row(y, cat.NoteTemplateFor(s).Length > 0, bTemplate);
            int hExtra = y + 2;

            int h = Math.Max(hWhat, hWhen);
            gWhat.SetBounds(0, 0, a, h); gWhen.SetBounds(a + g, 0, w - a - g, h);
            y = h + 4;
            bMore.Text = (more ? "▾ Weniger Optionen" : "▸ Mehr Optionen (eigene Nummer, Abwechseln, Ausweich, Kennung, Notizen)" + MoreSummary(s));
            bMore.SetBounds(0, y, w, bMore.Height); y = bMore.Bottom + 4;
            gEquip.Visible = !s.IsBuffer; gPuf.Visible = s.IsBuffer;   // Puffer-Schritt: je Puffer eine Karte statt der gemeinsamen Equipment-Liste
            if (s.IsBuffer) { gPuf.Width = w; gPuf.SetBounds(0, y, w, ArrangePuffer(w - 16)); y = gPuf.Bottom + g; }
            else { gEquip.SetBounds(0, y, w, ArrangeEquip(w - 16)); y = gEquip.Bottom + g; }
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
