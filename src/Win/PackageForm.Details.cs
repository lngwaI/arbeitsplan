using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Teil 3 (Schritt-Details: Was?/Wann?/Wo?/Zusaetze/Equipment) und Teil 4 (Vorschau & Pruefung).
    // Nicht zutreffende Felder werden ausgeblendet. "Was? Wo?" und "Wann?" stehen nebeneinander, darunter gleich das Equipment
    // (auch auf 1024x768 ohne Scrollen sichtbar), dann die Zusaetze.
    partial class PackageForm
    {
        readonly EditMarker marker = new EditMarker();
        readonly Panel scroll = new Panel(), body = new Panel(), equipRows = new Panel();
        Section gWhat, gWhen, gExtra, gEquip;
        readonly TextBox tTitle = new TextBox(), tSubNos = new TextBox(), tIdent = new TextBox(), tNote = new TextBox(), tPreview = new TextBox();
        readonly ComboBox cbKind = new ComboBox(), cbRow = new ComboBox();
        readonly NumericUpDown nDay = new NumericUpDown(), nCount = new NumericUpDown(), nInterval = new NumericUpDown(), nStartNo = new NumericUpDown();
        Label lTitle, lKind, lSub, lSubHelp, lDay, lCount, lCountUnit, lInterval, lIntervalUnit, lStartNo, lNoEx, lRow, rowHint, lIdent, lIdentHelp, lNote, lNoEquip;
        readonly Label[] equipHead = new Label[5];
        readonly Button bPos = new Button();
        readonly ContextMenuStrip menuPos = new ContextMenuStrip();
        readonly PlanStrip strip = new PlanStrip();
        readonly Panel stripScroll = new Panel();
        readonly ListBox lbHints = new ListBox();
        bool laying;
        List<Hint> hintList = new List<Hint>();   // Inhalt von lbHints (fuer MeasureItem)

        class Hint
        {
            public string Text; public PackageStep Step; public Control Field;
            public Hint(string t, PackageStep s, Control f) { Text = t; Step = s; Field = f; }
            public override string ToString() { return Text; }
        }

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
            Section sec = new Section("3   Schritt-Details", "", true, 700);
            sec.Dock = DockStyle.Fill;
            marker.Height = Font.Height + 14; marker.TabStop = false;
            scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.Tag = "keep";
            body.Dock = DockStyle.Top; body.Tag = "keep"; body.Width = 680;   // Resize -> LayoutDetails (PackageForm.Sizing.cs)
            scroll.Controls.Add(body);
            Panel gap = new Panel(); gap.Tag = "keep"; gap.Dock = DockStyle.Top; gap.Height = 8;
            sec.Controls.Add(scroll); sec.Controls.Add(gap); sec.Controls.Add(marker);
            wrap.Controls.Add(sec);

            // Was? Wo?
            gWhat = Group("Was? Wo?", "Titel, Art und Raum (Block im Plan)", 280);
            int lw = LabelW("Unterlauf-Nr.:", "Titel:", "Art:", "Raum (Block):"), x = 8 + lw, w = 280 - 8 - x;
            lTitle = Lbl(gWhat, "Titel:", 8, lw); tTitle.SetBounds(x, 0, w, IH);
            lKind = Lbl(gWhat, "Art:", 8, lw); cbKind.SetBounds(x, 0, w, IH); cbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            lRow = Lbl(gWhat, "Raum (Block):", 8, lw); cbRow.SetBounds(x, 0, w, IH); cbRow.DropDownStyle = ComboBoxStyle.DropDownList;
            cbRow.Items.AddRange(rowNames.ToArray());
            rowHint = Lbl(gWhat, "", x, w); rowHint.ForeColor = Color.DarkOrange; rowHint.AutoEllipsis = true;
            tip.SetToolTip(cbRow, "Block (Raum) im Plan, in dem der Schritt steht. Beim Kampagnenstart je Lauf änderbar.");
            cbKind.Items.AddRange(new object[] { "Normaler Schritt", "Lauf mit Laufnummer", "Unterlauf (Splitten/Einfrieren)" });
            lSub = Lbl(gWhat, "Unterlauf-Nr.:", 8, lw); tSubNos.SetBounds(x, 0, w, IH);
            lSubHelp = Lbl(gWhat, "Komma = nächster Lauf, „/“ = mehrere (z. B. 4186/4187, 4188); 4 Ziffern + Produktcode", x, w);
            lSubHelp.Height = 3 * Font.Height + 2; lSubHelp.TextAlign = ContentAlignment.TopLeft;
            gWhat.Controls.AddRange(new Control[] { tTitle, cbKind, cbRow, tSubNos });
            Grow(tTitle, cbKind, cbRow, rowHint, tSubNos, lSubHelp); Gray(lSubHelp);
            tip.SetToolTip(cbKind, "Normaler Schritt: Titel (bei Wiederholung „Titel 1, 2, 3“). Lauf: bekommt die Laufnummer XXYYZZZZ. Unterlauf: Splitten/Einfrieren eines Laufs.");

            // Wann?
            gWhen = Group("Wann?", "Tag 1 = Startdatum der Kampagne", 270);
            lw = LabelW("Tag der Kampagne:", "Wiederholen:", "Abstand:", "Erste Laufnummer:"); x = 8 + lw;
            lDay = Lbl(gWhen, "Tag der Kampagne:", 8, lw); Num(nDay, 1, 366, x, 60, gWhen);
            lCount = Lbl(gWhen, "Wiederholen:", 8, lw); Num(nCount, 1, 99, x, 60, gWhen); lCountUnit = Lbl(gWhen, "mal", x + 66, 270 - 8 - x - 66);
            lInterval = Lbl(gWhen, "Abstand:", 8, lw); Num(nInterval, 1, 30, x, 60, gWhen); lIntervalUnit = Lbl(gWhen, "Tage", x + 66, 270 - 8 - x - 66);
            lStartNo = Lbl(gWhen, "Erste Laufnummer:", 8, lw); Num(nStartNo, 0, 9999, x, 60, gWhen); lNoEx = Lbl(gWhen, "", x + 66, 270 - 8 - x - 66);
            lNoEx.AutoEllipsis = true;
            Grow(lCountUnit, lIntervalUnit, lNoEx); Gray(lNoEx);
            tip.SetToolTip(nStartNo, "Nummer des ersten Laufs; mit Produktcode die hinteren 4 Stellen (ZZZZ). Beim Kampagnenstart anpassbar.");


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
            gEquip = Group("Equipment", "Gerät aus der validierten Liste wählen oder eintippen (orange = nicht validiert). Ausweich-Gerät = Backup bei Ausfall.", 500);
            string[] heads = { "Position", "Gerät", "abwechselnd", "Ausweich-Gerät", "Notiz" };
            for (int i = 0; i < 5; i++) { equipHead[i] = Lbl(gEquip, heads[i], 0, 80); equipHead[i].Height = Font.Height + 4; equipHead[i].Font = new Font(Font, FontStyle.Bold); equipHead[i].AutoEllipsis = true; }
            equipRows.Tag = "keep"; gEquip.Controls.Add(equipRows);
            lNoEquip = Lbl(gEquip, "Keine Equipment-Positionen – mit „+ Position hinzufügen“ ergänzen.", 8, 480); Gray(lNoEquip); Grow(lNoEquip);
            Btn(bPos, "+ Position hinzufügen ▾", 8, 0, 190, delegate { menuPos.Show(bPos, new Point(0, bPos.Height)); });
            gEquip.Controls.Add(bPos);
            BuildPosMenu();
            gEquip.TabIndex = 3; gExtra.TabIndex = 4;   // Equipment steht ueber den Zusaetzen

            foreach (TextBox t in new[] { tTitle, tSubNos, tIdent, tNote }) Ui.SimpleMenu(t);
            tTitle.TextChanged += delegate { if (Edit()) { cur.Title = tTitle.Text.Trim(); Changed(); } };
            cbKind.SelectedIndexChanged += delegate { if (Edit() && cbKind.SelectedIndex >= 0) { cur.Kind = (StepKind)cbKind.SelectedIndex; Changed(); } };
            tSubNos.TextChanged += delegate { if (Edit()) { cur.SubNos = tSubNos.Text.Trim(); Changed(); } };
            nDay.ValueChanged += delegate { if (Edit()) { cur.Day = (int)nDay.Value; Changed(); } };
            nCount.ValueChanged += delegate { if (Edit()) { cur.Count = (int)nCount.Value; Changed(); } };
            nInterval.ValueChanged += delegate { if (Edit()) { cur.Interval = (int)nInterval.Value; Changed(); } };
            nStartNo.ValueChanged += delegate { if (Edit()) { cur.StartNo = (int)nStartNo.Value; Changed(); } };
            cbRow.SelectedIndexChanged += delegate { if (Edit()) { cur.Row = cbRow.Text; Changed(); } };
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

        Control BuildPreview()
        {
            Panel wrap = new Panel(); wrap.Tag = "keep"; wrap.Dock = DockStyle.Bottom; wrap.Padding = new Padding(8, 0, 8, 0);
            wrap.Height = 9 * Font.Height / 2 + 84;
            Section sec = new Section("4   Vorschau & Prüfung", "Kalender der Kampagne; der gewählte Schritt ist blau. Klick auf einen Eintrag wählt ihn aus.", true, 1000);
            sec.Dock = DockStyle.Fill;
            Panel right = Column(sec, "So erscheint der gewählte Schritt im Plan:", tPreview, 300);
            tPreview.Multiline = true; tPreview.ReadOnly = true; tPreview.ScrollBars = ScrollBars.Vertical; tPreview.BackColor = Color.White; Ui.SimpleMenu(tPreview);
            Panel hints = Column(sec, "Prüfhinweise (Klick = hinspringen):", lbHints, 290);
            lbHints.IntegralHeight = false; lbHints.DrawMode = DrawMode.OwnerDrawVariable;   // Hinweise umbrechen statt abschneiden
            lbHints.MeasureItem += delegate(object o, MeasureItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= hintList.Count) return;   // MeasureItem kommt beim Add, bevor Items[e.Index] existiert
                e.ItemHeight = Math.Min(255, TextRenderer.MeasureText(hintList[e.Index].Text, Font, new Size(lbHints.Width - SystemInformation.VerticalScrollBarWidth - 12, 0), TextFormatFlags.WordBreak).Height + 4);
            };
            lbHints.DrawItem += delegate(object o, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= lbHints.Items.Count) return;
                e.DrawBackground();
                bool sel = (e.State & DrawItemState.Selected) != 0;
                Rectangle r = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 2);
                TextRenderer.DrawText(e.Graphics, lbHints.Items[e.Index].ToString(), Font, r, sel ? SystemColors.HighlightText : lbHints.ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            };
            // Resize: nur bei echter Breitenaenderung neu umbrechen (RewrapHints, PackageForm.Sizing.cs)
            lbHints.Click += delegate { Hint h = lbHints.SelectedItem as Hint; if (h == null) return; if (h.Step != null) Bind(h.Step); if (h.Field != null) h.Field.Focus(); };
            stripScroll.Dock = DockStyle.Fill; stripScroll.AutoScroll = true; stripScroll.BackColor = Color.White; stripScroll.BorderStyle = BorderStyle.FixedSingle; stripScroll.Tag = "keep";
            strip.Location = Point.Empty; strip.StepClicked += delegate(PackageStep s) { Bind(s); };
            stripScroll.Controls.Add(strip);
            sec.Controls.Add(stripScroll); sec.Controls.Add(Spacer()); sec.Controls.Add(hints); sec.Controls.Add(Spacer()); sec.Controls.Add(right);
            wrap.Controls.Add(sec);
            return wrap;
        }

        static Panel Spacer() { Panel p = new Panel(); p.Tag = "keep"; p.Dock = DockStyle.Right; p.Width = 8; return p; }

        Panel Column(Control parent, string title, Control content, int w)
        {
            Panel p = new Panel(); p.Tag = "keep"; p.Dock = DockStyle.Right; p.Width = w;
            Label l = new Label(); l.Text = title; l.Dock = DockStyle.Top; l.Height = Font.Height + 4; l.AutoEllipsis = true;
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content); p.Controls.Add(l);
            parent.Controls.Add(p);
            return p;
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
                nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, s.Day));
                nCount.Value = Math.Max(1, Math.Min(99, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
                nStartNo.Value = Math.Max(0, Math.Min(9999, s.StartNo));
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
            lRuns.Text = Product.Length == 0 ? "leer = ohne Produktnummer" : (Package.IsCode4(Product) ? "→ Läufe " + Product + "0001, " + Product + "0002 …" : "bitte genau 4 Ziffern");
            lRuns.ForeColor = Product.Length == 0 || Package.IsCode4(Product) ? Section.HelpColor : Color.Firebrick;
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

        void LayoutDetails()
        {
            if (laying || cur == null) return;
            laying = true;
            PackageStep s = cur;
            bool sub = s.Kind == StepKind.SubRun, rep = s.Count > 1, num = s.Numbered;

            Baustein b = cat.Baustein_(s.Source);
            bool off = b != null && b.Row.Length > 0 && s.Row.Length > 0 && s.Row != b.Row;   // z. B. Packen in Suite 2
            rowHint.Text = off ? "Standard-Raum ist „" + b.Row + "“ (Sonderaktion?)" : "";
            tip.SetToolTip(rowHint, rowHint.Text);
            int y = gWhat.ContentTop;
            y = Row(y, true, lTitle, tTitle); y = Row(y, true, lKind, cbKind); y = Row(y, true, lRow, cbRow); y = Row(y, off, rowHint) - (off ? 4 : 0);
            y = Row(y, sub, lSub, tSubNos); y = Row(y, sub, lSubHelp);
            int hWhat = y + 2;

            y = gWhen.ContentTop;
            y = Row(y, true, lDay, nDay); y = Row(y, true, lCount, nCount, lCountUnit); y = Row(y, rep, lInterval, nInterval, lIntervalUnit); y = Row(y, num, lStartNo, nStartNo, lNoEx);
            lCountUnit.Text = rep ? "mal" : "mal (einmalig)";
            lStartNo.Text = s.Kind != StepKind.Normal ? "Erste Laufnummer:" : "Erste Nummer:";
            lNoEx.Text = num ? "→ " + s.RunNo(Product, 0) : "";
            tip.SetToolTip(lNoEx, lNoEx.Text);
            int hWhen = y + 2;

            y = gExtra.ContentTop;
            y = Row(y, true, lIdent, tIdent); y = Row(y, true, lIdentHelp) - 2; y = Row(y, true, lNote, tNote);
            int hExtra = y + 2;

            int w = body.ClientSize.Width, g = 8;
            int a = Math.Max((w - g) / 2, Math.Min(w - g - 250, (w - g) * 55 / 100)), h = Math.Max(hWhat, hWhen);   // Was? Wo? | Wann?
            gWhat.SetBounds(0, 0, a, h); gWhen.SetBounds(a + g, 0, w - a - g, h);
            y = h + g;
            gEquip.SetBounds(0, y, w, ArrangeEquip(w - 16)); y = gEquip.Bottom + g;
            gExtra.SetBounds(0, y, w, hExtra);
            body.Height = gExtra.Bottom + 4;
            laying = false;
        }

        int ArrangeEquip(int iw)
        {
            bool any = equipRows.Controls.Count > 0;
            int[] x = EquipRow.Cols(iw);
            int y = gEquip.ContentTop;
            for (int i = 0; i < 5; i++) equipHead[i].SetBounds(8 + x[i], 0, x[i + 1] - x[i] - 6, equipHead[i].Height);
            y = Row(y, any, equipHead) - 2;
            int ry = 0;
            foreach (Control c in equipRows.Controls) { EquipRow r = (EquipRow)c; r.Arrange(iw); r.Location = new Point(0, ry); ry += r.Height; }
            equipRows.SetBounds(8, y, x[6], ry);
            y += ry + 4;
            y = Row(y, !any, lNoEquip);
            y = Row(y, true, bPos);
            return y + 4;
        }

        // ---------- Vorschau und Pruefung ----------

        void UpdatePreview(bool scrollTo)
        {
            List<PlanStrip.Item> items = new List<PlanStrip.Item>();
            foreach (PackageStep s in Ordered())
                for (int r = 0; r < Math.Max(1, s.Count); r++)
                {
                    PlanStrip.Item it = new PlanStrip.Item(); it.Step = s; it.Day = s.DayOf(r);
                    it.Title = TitleOf(s); it.Missing = s.Row.Length == 0;
                    it.Sub = (s.Row.Length > 0 ? s.Row : "Raum fehlt") + (s.Numbered ? " · " + s.RunNo(Product, r) : "");
                    items.Add(it);
                }
            strip.Set(items, cur);
            if (scrollTo && cur != null)
            {
                int x = strip.XOf(cur);
                if (x >= 0 && (x < -stripScroll.AutoScrollPosition.X || x + 120 > -stripScroll.AutoScrollPosition.X + stripScroll.ClientSize.Width))
                    stripScroll.AutoScrollPosition = new Point(Math.Max(0, x - 30), -stripScroll.AutoScrollPosition.Y);
            }

            if (cur == null) { tPreview.Text = ""; return; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int run = 0;
            foreach (KeyValuePair<int, string> r in cur.Runs(Product))
            {
                sb.Append("— Tag ").Append(r.Key).Append(" · ").Append(cur.Row.Length > 0 ? cur.Row : "Raum fehlt").Append(" —\r\n");
                sb.Append(r.Value.Replace("\n", "\r\n")).Append("\r\n");
                if (++run >= 6 && cur.Count > 6) { sb.Append("… (").Append(cur.Count - 6).Append(" weitere Wiederholungen)\r\n"); break; }
            }
            tPreview.Text = sb.ToString();
        }

        void UpdateHints()
        {
            List<Hint> h = new List<Hint>();
            if (tName.Text.Trim().Length == 0) h.Add(new Hint("Das Muster hat noch keinen Namen.", null, tName));
            bool runs = steps.Exists(delegate(PackageStep s) { return s.Kind != StepKind.Normal; });
            if (Product.Length > 0 && !Package.IsCode4(Product)) h.Add(new Hint("Produktcode muss genau 4 Ziffern haben (z. B. 1357).", null, tProduct));
            else if (Product.Length == 0 && runs) h.Add(new Hint("Produktcode fehlt – Läufe bekommen keine 8-stellige Nummer.", null, tProduct));
            if (steps.Count == 0 && blocks.Count == 0) h.Add(new Hint("Der Ablauf ist leer – mit „+ Schritt hinzufügen“ beginnen.", null, bAdd));
            List<PackageStep> o = Ordered();
            for (int i = 0; i < o.Count; i++)
            {
                PackageStep s = o[i]; string n = "Schritt " + (i + 1);
                if (s.IsEmpty) { h.Add(new Hint(n + " ist leer und wird nicht gespeichert.", s, tTitle)); continue; }
                if (s.Row.Length == 0) h.Add(new Hint(n + " (" + TitleOf(s) + ") hat keinen Raum.", s, cbRow));
                else if (!rowNames.Contains(s.Row)) h.Add(new Hint(n + ": Raum „" + s.Row + "“ gibt es im Plan nicht.", s, cbRow));
                if (s.Kind == StepKind.SubRun && s.SubNos.Trim().Length == 0) h.Add(new Hint(n + ": Unterlauf-Nummern fehlen.", s, tSubNos));
                else if (s.Kind == StepKind.SubRun && s.SubNos.Split(',').Length != s.Count)
                    h.Add(new Hint(n + ": Unterlauf-Nummern für " + s.SubNos.Split(',').Length + " Lauf/Läufe, aber " + s.Count + " Wiederholung(en) (Komma = nächster Lauf).", s, tSubNos));
                int idents = s.Ident.Trim().Length == 0 ? 0 : s.Ident.Split(',').Length;
                if (idents > 0 && idents != s.Count) h.Add(new Hint(n + ": " + idents + " Kennung(en), aber " + s.Count + " Wiederholung(en) (Komma = nächster Lauf).", s, tIdent));
                if (s.Numbered && s.StartNo + s.Count - 1 > 9999) h.Add(new Hint(n + ": Laufnummer über 9999 – kleinere erste Nummer wählen.", s, nStartNo));
                foreach (EquipAssign a in s.Equipment)
                    foreach (string id in EquipRow.Unvalidated(a, cat, Product)) h.Add(new Hint(n + ": Gerät " + id + " (" + a.Short + ") ist " + (cat.Product(Product) != null ? "für dieses Produkt nicht vorgesehen." : "nicht validiert."), s, null));
                Package one = new Package(); one.ProductCode = Product; one.Steps.Add(s);   // Zugehoerigkeit: sperrt Speichern/Uebernehmen
                foreach (string e in cat.CheckPackage(one)) h.Add(new Hint(n + " FEHLER: " + e, s, null));
            }
            bool ok = h.Count == 0;
            if (ok) h.Add(new Hint("Keine Auffälligkeiten – das Muster kann übernommen werden.", null, null));
            hintList = h;
            lbHints.BeginUpdate();
            lbHints.Items.Clear();
            foreach (Hint x in h) lbHints.Items.Add(x);
            lbHints.EndUpdate();
            lbHints.ForeColor = ok ? Color.DarkGreen : Color.FromArgb(150, 60, 0);
        }
    }
}
