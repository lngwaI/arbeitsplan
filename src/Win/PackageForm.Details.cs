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
        RowReorder equipOrder;   // Reihenfolge der Equipment-Zeilen normaler Schritte (UX-5b)
        RowPool rowPool;   // Zeilen von equipRows werden wiederverwendet statt neu gebaut (UX-5e); aktive Zeilen in eqRows, freie bleiben versteckt in equipRows
        readonly List<EquipRow> eqRows = new List<EquipRow>();
        Section gWhat, gWhen, gExtra, gEquip;
        readonly TextBox tTitle = new TextBox(), tSubNos = new TextBox(), tIdent = new TextBox(), tNote = new TextBox(), tManualNo = new TextBox();
        readonly ComboBox cbKind = new ComboBox(), cbRow = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        Label lTitle, lKind, lSub, lSubHelp, lRow, rowHint, lVar, lIdent, lIdentHelp, lNote, lNoEquip, lManual;
        readonly Label[] equipHead = new Label[5];
        readonly Button bPos = new Button(), bMore = new Button();
        readonly ContextMenuStrip menuPos = new ContextMenuStrip();
        bool laying, more;
        string markerTip = "";

        static void Grow(params Control[] cs) { foreach (Control c in cs) c.Anchor = TLR; }
        static void Gray(params Label[] ls) { foreach (Label l in ls) l.ForeColor = Section.HelpColor; }
        int LabelW(params string[] texts) { int m = 0; foreach (string t in texts) m = Math.Max(m, TextRenderer.MeasureText(t, Font).Width); return m + 6; }

        Section Group(string title, string help, int w)
        {
            Section g = new Section(title, "", false, w);   // Hilfetext nur noch als Tooltip der Gruppe (weniger Text)
            if (help.Length > 0) tip.SetToolTip(g, help);
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
            marker.Height = Font.Height + 8; marker.TabStop = false;
            scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.Tag = "keep";
            body.Dock = DockStyle.Top; body.Tag = "keep"; body.Width = 680;   // Resize -> LayoutDetails (PackageForm.Sizing.cs)
            scroll.Controls.Add(body);
            Panel gap = new Panel(); gap.Tag = "keep"; gap.Dock = DockStyle.Top; gap.Height = 3;
            sec.Controls.Add(scroll); sec.Controls.Add(eqBar); InitEquipBar(); sec.Controls.Add(gap); sec.Controls.Add(marker);   // eqBar: Leiste "nicht in der Liste" (N5)
            wrap.Controls.Add(sec);

            // Was? Wo im Plan? – der Raum steht direkt unter dem Titel
            gWhat = Group("Was? Wo im Plan?", "Titel, Raum (Block im Plan) und Art des Schritts", 280);
            int lw = LabelW("Unterlauf-Nr.:", "Titel:", "Wo im Plan?", "Art:", "Puffer-Typ:", "Gebinde:", "Nr. von Hand:"), x = 8 + lw, w = 280 - 8 - x;
            lTitle = Lbl(gWhat, "Titel:", 8, lw); tTitle.SetBounds(x, 0, w, IH);
            lRow = Lbl(gWhat, "Wo im Plan?", 8, lw); lRow.Font = new Font(Font, FontStyle.Bold);
            cbRow.SetBounds(x, 0, w, IH); cbRow.DropDownStyle = ComboBoxStyle.DropDownList; cbRow.Items.AddRange(rowNames.ToArray());
            rowHint = Lbl(gWhat, "", x, w); rowHint.ForeColor = Color.DarkOrange; rowHint.AutoEllipsis = true;
            tip.SetToolTip(cbRow, "Raum (Block) im Plan, in dem der Schritt steht. Beim Kampagnenstart je Lauf änderbar.");
            lKind = Lbl(gWhat, "Art:", 8, lw); cbKind.SetBounds(x, 0, w, IH); cbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            cbKind.Items.AddRange(new object[] { "Sonstiges (ohne Laufnummer)", "Lauf-Schritt mit Laufnummer", "Unterlauf (Splitten/Einfrieren)" });   // Eintrag 2: beim Hauptlauf "Hauptlauf"
            lManual = Lbl(gWhat, "Nr. von Hand:", 8, lw); tManualNo.SetBounds(x, 0, w, IH); tManualNo.MaxLength = 8; gWhat.Controls.Add(tManualNo);
            tip.SetToolTip(tManualNo, "Nummer des Packens, 8 Ziffern (z. B. 12340001); wird nicht vorgeschlagen. Kommt aus „Nr. für Packen“ im Kopf, hier nur ändern, wenn dieser Schritt eine andere hat. Im Plan: „Packen (Nr. 12340001)“, zählt nicht als Kampagnenlauf.");
            BuildManualHint(gWhat, x, w);
            lVar = Lbl(gWhat, "Puffer-Typ:", 8, lw);
            tip.SetToolTip(lVar, "Ergibt sich aus dem Baustein (Puffer mobil / Puffer stationär); ein Schritt = ein Puffer.");
            lSub = Lbl(gWhat, "Unterlauf-Nr.:", 8, lw); tSubNos.SetBounds(x, 0, w, IH);
            lSubHelp = Lbl(gWhat, "Komma = nächster Lauf, „/“ = mehrere (z. B. 4186/4187, 4188); 4 Ziffern + Praefix der Laufnummer", x, w);
            lSubHelp.Height = 3 * Font.Height + 2; lSubHelp.TextAlign = ContentAlignment.TopLeft;
            gWhat.Controls.AddRange(new Control[] { tTitle, cbRow, cbKind, tSubNos });
            Grow(tTitle, cbKind, cbRow, rowHint, tSubNos, lSubHelp, tManualNo, lManualHint); Gray(lSubHelp);
            tip.SetToolTip(cbKind, "Hauptlauf bzw. Lauf-Schritt: bekommt die 8-stellige Laufnummer. Unterlauf: Splitten/Einfrieren eines Laufs. Sonstiges: nur der Titel (bei Wiederholung „Titel 1, 2, 3“).");
            BuildAnsatz(lw, x, w);   // Herstellung mit Anzahl, Ansatz (PackageForm.When.cs)
            BuildWhen();             // Wann? (PackageForm.When.cs)

            // Mehr Optionen (ein-/ausklappen)
            bMore.SetBounds(0, 0, 400, Theme.Current.ButtonH - 2); bMore.TextAlign = ContentAlignment.MiddleLeft; bMore.Click += delegate { SetMore(!more); };
            body.Controls.Add(bMore);
            bMore.AutoEllipsis = true; tip.SetToolTip(bMore, MoreTip);

            // Zusaetze
            gExtra = Group("Zusätze", "Optional; erscheinen im Plan unter dem Equipment", 500);
            lw = LabelW("PA-/Kennungsnummer:", "Notiz:"); x = 8 + lw; w = 500 - 8 - x;
            tNote.Multiline = true; tNote.AcceptsReturn = true; tNote.ScrollBars = ScrollBars.Vertical; tNote.WordWrap = true; tNote.Font = new Font(Font, FontStyle.Italic);
            lIdent = Lbl(gExtra, "PA-/Kennungsnummer:", 8, lw); tIdent.SetBounds(x, 0, w, IH);
            lIdentHelp = Lbl(gExtra, "z. B. PA 40010001 – steht im Plan als rote letzte Zeile; zählt je Lauf +1; ausgefallener Lauf = Lücke", x, w);
            lIdentHelp.TextAlign = ContentAlignment.TopLeft; lIdentHelp.Height = 2 * Font.Height + 2;
            tip.SetToolTip(tIdent, "PA-/Kennungsnummer des ersten Laufs, z. B. PA 40010001. Im Plan steht sie als rote, große, fette letzte Zeile des Blocks; jeder weitere Lauf zählt +1. Fällt ein Lauf aus, bleibt seine Nummer als Lücke. Nur bei Lauf-/Unterlauf-Schritten.");
            lNote = Lbl(gExtra, "Notizen – stehen bei jedem Lauf (je Zeile eine Notiz)", 8, 480); tNote.SetBounds(8, 0, 480, 3 * Font.Height + 10);
            gExtra.Controls.AddRange(new Control[] { tIdent, tNote });
            Grow(tIdent, tNote, lIdentHelp, lNote); Gray(lIdentHelp);
            tip.SetToolTip(tNote, "Enter = nächste Notiz. Jede Zeile steht im Plan als eigene Notiz (kursiv, zählt nicht als Inhalt) bei jedem Lauf.");
            tip.SetToolTip(lIdentHelp, lIdentHelp.Text); tip.SetToolTip(lIdent, lIdentHelp.Text);
            BuildIdents();   // Kennungstabelle + Materialvorlage (PackageForm.Idents.cs)

            // Equipment
            gEquip = Group("Equipment", "Gerät aus der validierten Liste wählen oder eintippen (orange = nicht validiert).", 500);
            string[] heads = { "Position", "Gerät", "abwechselnd", "Ausweich-Gerät", "Notiz" };
            for (int i = 0; i < 5; i++) { equipHead[i] = Lbl(gEquip, heads[i], 0, 80); equipHead[i].Height = Font.Height + 4; equipHead[i].Font = new Font(Font, FontStyle.Bold); equipHead[i].AutoEllipsis = true; }
            equipRows.Tag = "keep"; gEquip.Controls.Add(equipRows);
            rowPool = new RowPool(equipRows);
            equipOrder = new RowReorder(equipRows, delegate { return eqRows.Count; }, delegate(int i) { return eqRows[i].Bounds; },
                delegate(int f, int t) { if (cur != null && PackageStep.MoveItem(cur.Equipment, f, t)) { dirty = true; MoveEquipRow(f, t); } }, gEquip);
            lNoEquip = Lbl(gEquip, "Keine Equipment-Positionen – mit „+ Position hinzufügen“ ergänzen.", 8, 480); Gray(lNoEquip); Grow(lNoEquip);
            Btn(bPos, "+ Position hinzufügen ▾", 8, 0, 190, delegate { menuPos.Show(bPos, new Point(0, bPos.Height)); });
            gEquip.Controls.Add(bPos);
            BuildPosMenu();
            BuildPuffer();   // Karten je Puffer (PackageForm.PufferCards.cs)
            BuildFreeze();   // Freeze-Vorlage (PackageForm.Freeze.cs)
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
            tManualNo.TextChanged += delegate { if (Edit()) { cur.ManualNo = StepPackNo(tManualNo.Text); tManualNo.ForeColor = tManualNo.Text.Trim().Length == 0 || PackageStep.IsManualNo(tManualNo.Text.Trim()) ? SystemColors.WindowText : Color.Firebrick; ShowManualHint(cur); Changed(); } };
            tSubNos.TextChanged += delegate { if (Edit()) { cur.SubNos = tSubNos.Text.Trim(); Changed(); } };
            cbRow.SelectedIndexChanged += delegate { if (Edit()) { foreach (PackageStep bs in steps) if (bs.BacksUp(cur) && bs.Row == cur.Row) bs.Row = cbRow.Text; cur.Row = cbRow.Text; loading = true; FillVariants(cur); loading = false; Changed(); } };   // Back-Up im selben Raum zieht mit
            tIdent.TextChanged += delegate { if (Edit()) { cur.Ident = ""; cur.IdentStart = tIdent.Text.Trim(); Changed(); } };
            tNote.TextChanged += delegate { if (Edit()) { cur.Note = tNote.Text.Replace("\r", "").Trim(); Changed(); } };
            return wrap;
        }

        bool Edit() { return !loading && cur != null; }

        static bool IdentVisible(PackageStep s) { return s.Kind != StepKind.Normal || s.IdentStart.Length > 0 || s.IdentOver.Count > 0 || s.Ident.Trim().Length > 0; }

        void BuildPosMenu()
        {
            menuPos.Items.Clear();
            foreach (EquipPosition p in cat.Positions) { EquipPosition pp = p; menuPos.Items.Add(p.Name + "  (" + p.Short + ")", null, delegate { AddPosition(pp.Short); }); }
        }

        void SetMore(bool on)
        {
            more = on;
            if (cur == null) return;
            HoldDraw();   // nur andere Spalten/Gruppen anordnen, nichts neu bauen
            try { LayoutDetails(); } finally { ReleaseDraw(); }
        }

        static string VarText(string v) { return VariantSet.Display(v).Replace("stationaer", "stationär"); }

        // ---------- Schritt anzeigen ----------

        // Schritt anzeigen. Das Zeichnen ruht, bis alles befuellt und angeordnet ist (kein schrittweises Aufbauen); Bildlaufstand bleibt beim selben Schritt.
        void Bind(PackageStep s)
        {
            PerfB(PfBind);
            CancelView();   // ausstehende Tipp-Aktualisierung ist damit erledigt
            Point sp = scroll.AutoScrollPosition; bool same = s == cur;
            HoldDraw();
            try { BindCore(s); if (same) scroll.AutoScrollPosition = new Point(-sp.X, -sp.Y); }
            finally { ReleaseDraw(); }
            PerfE(PfBind);
        }

        void BindCore(PackageStep s)
        {
            if (s != cur) curRun = -1;   // anderer Schritt: Vorschau beginnt beim ersten Eintrag (ausser nextRun, Klick im Kalender)
            cur = s;
            loading = true;
            body.SuspendLayout();
            equipOrder.Clear();
            while (eqRows.Count > 0) { rowPool.Release(eqRows[eqRows.Count - 1]); eqRows.RemoveAt(eqRows.Count - 1); }
            if (s != null && s.IsBuffer) s.MigratePuffers();   // alter Modus -> eigene Puffer (verlustfrei, gleicher Plantext, kein "geändert")
            BindPuffer(s);   // Karten je Puffer (PackageForm.PufferCards.cs)
            if (s != null)
            {
                foreach (EquipAssign a in s.Equipment) AddEquipRow(a);
                tTitle.Text = s.Title;
                cbKind.Items[1] = s == StepsView().Head ? "Hauptlauf" : "Lauf-Schritt mit Laufnummer";
                cbKind.SelectedIndex = (int)s.Kind; tSubNos.Text = s.SubNos;
                tManualNo.Text = s.ManualNo; tManualNo.ForeColor = SystemColors.WindowText; ShowManualHint(s);
                if (s.Row.Length > 0 && !cbRow.Items.Contains(s.Row)) cbRow.Items.Add(s.Row);   // unbekannter Block bleibt sichtbar
                cbRow.SelectedIndex = cbRow.Items.IndexOf(s.Row);
                FillVariants(s); BindAnsatz(s); BindWhen(s); BindFreeze(s);
                s.IdentToStart(StepsView().RunCountOf(s));   // alte Kommaliste -> Start + Ueberschreibungen (gleiches Ergebnis)
                tNote.Text = s.Note.Replace("\r", "").Replace("\n", "\r\n"); tIdent.Text = s.IdentStart;
            }
            body.Visible = s != null;
            body.ResumeLayout();
            loading = false;
            UpdateView(true);
            HookLeave(body);   // Feld verlassen = Rueckgaengig-Schnappschuss (PackageForm.Undo.cs)
        }

        // Alles nachziehen: Liste, Felder ein-/ausblenden, Kopfzeile, Vorschau, Pruefhinweise; scrollTo = Kalender zum Schritt scrollen.
        void UpdateView(bool scrollTo)
        {
            PerfB(PfView);
            Repin();   // feste Laeufe bleiben an ihrem Tag (PackageForm.Order.cs)
            if (Package.SyncBackups(steps) && cur != null && cur.IsBackup) { bool was = loading; loading = true; BindWhen(cur); loading = was; }   // Back-Up folgt dem Packen
            FillList();
            FillIdents();
            LayoutDetails();
            UpdateHeader();
            int n = cur != null ? No(cur) : 0;
            marker.Active = cur != null;
            marker.Text = cur != null
                ? "Schritt " + n + " von " + steps.Count + " – " + TitleOf(cur)
                : "Kein Schritt gewählt – links im Ablauf einen Schritt anklicken oder „+ Schritt hinzufügen“.";
            string mtip = cur != null ? "Du bearbeitest gerade diesen Schritt: " + TitleOf(cur) + " (" + WhenText(cur) + ", " + (cur.Row.Length > 0 ? cur.Row : "Raum fehlt") + ")" : "";
            if (mtip != markerTip) { markerTip = mtip; tip.SetToolTip(marker, mtip); }
            marker.Invalidate();
            UpdateRunsLabel();
            bDup.Enabled = cur != null;
            bool canDel = cur != null && StepsView().CanRemove(cur);   // sperrt vorab (Grund im Tooltip)
            bDel.Enabled = canDel;
            bDel.ToolTipText = (cur == null || canDel ? "Gewählten Schritt entfernen (Entf)" : "Nicht möglich: der Hauptlauf ist die Grundlage der Kampagne. Erst einen anderen Lauf-Schritt anlegen.");
            bUp.Enabled = n > 1; bDown.Enabled = n > 0 && n < steps.Count;   // am Listenrand gesperrt
            PerfB(PfPreview); UpdatePreview(scrollTo); PerfE(PfPreview);
            PerfB(PfHints); UpdateHints(); PerfE(PfHints);
            RecordPins(cur);
            PerfE(PfView);
        }
    }
}
