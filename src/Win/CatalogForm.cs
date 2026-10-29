using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einrichten fuer hoehere Nutzer: Equipment-Positionen (mit validierten Geraeten) und Bausteine (wiederverwendbare Ablaufschritte).
    class CatalogForm : Form
    {
        readonly Catalog cat;
        readonly List<string> rowNames;
        public Catalog Result;

        // Positionen
        readonly ListBox lbPos = new ListBox();
        readonly TextBox pName = new TextBox(), pShort = new TextBox(), pIds = new TextBox();
        readonly CheckBox pRot = new CheckBox();
        // Bausteine
        readonly ListBox lbBau = new ListBox();
        readonly TextBox bName = new TextBox(), bText = new TextBox(), bNote = new TextBox();
        readonly ComboBox bRow = new ComboBox();
        readonly CheckedListBox bPos = new CheckedListBox();
        readonly CheckBox bUnique = new CheckBox(), bDefault = new CheckBox(), bSpecial = new CheckBox();
        readonly NumericUpDown bCount = new NumericUpDown();   // Standard-Anzahl Equipment-Zeilen der markierten Position
        // Austausch-Gruppen
        readonly ListBox lbGrp = new ListBox();
        readonly TextBox gName = new TextBox();
        readonly CheckedListBox gPos = new CheckedListBox();
        readonly ComboBox bVar = new ComboBox();
        string[] varVals;   // Index 0 = kein Typ, danach die Varianten der Puffer-Zeile
        bool loading;

        public CatalogForm(Catalog catalog, List<string> rowNames, string[] variants = null)
        {
            cat = catalog.Clone(); this.rowNames = rowNames;
            List<string> vl = new List<string>(); vl.Add("");
            foreach (string v in variants ?? new[] { "mobil", "stationaer" }) if (v.Length > 0 && !vl.Contains(v)) vl.Add(v);
            varVals = vl.ToArray();
            Text = "Equipment-Positionen und Bausteine einrichten";
            Ui.Dialog(this, 760, 540);
            Controls.Add(Ui.Hint("Hier gilt alles für alle Produkte (Standard). Geräte je Produkt: Einstellungen → Produkte & Equipment; Geräte selbst: Equipment-Stammdaten.", 8, 6, 744));

            TabControl tabs = new TabControl(); tabs.SetBounds(8, 28, 744, 462);
            TabPage t1 = new TabPage("Equipment-Positionen"), t2 = new TabPage("Bausteine (Ablaufschritte)");
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedIndex == 1) { RefillPos(); LoadBau(); } else if (tabs.SelectedIndex == 2) { RefillGroupPos(); LoadGrp(); } };
            TabPage t3 = new TabPage("Austausch-Gruppen");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2); tabs.TabPages.Add(t3);
            BuildPositions(t1); BuildBausteine(t2); BuildGroups(t3);

            Button ok = new Button(); ok.Text = "Speichern"; ok.SetBounds(560, 500, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(660, 500, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { tabs, ok, cancel });
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }

        // ---------- Positionen ----------

        void BuildPositions(TabPage p)
        {
            p.Controls.Add(L("Positionen (z. B. TFF-Skid, Pumpe):", 8, 8));
            lbPos.SetBounds(8, 28, 230, 360);
            foreach (EquipPosition e in cat.Positions) lbPos.Items.Add(e);
            lbPos.DisplayMember = "Name";
            lbPos.SelectedIndexChanged += delegate { LoadPos(); };
            p.Controls.Add(lbPos);
            p.Controls.Add(Btn("+ Neu", 8, 394, 70, delegate { EquipPosition e = new EquipPosition(); e.Name = "Neue Position"; e.Short = "Neu"; cat.Positions.Add(e); lbPos.Items.Add(e); lbPos.SelectedIndex = lbPos.Items.Count - 1; }));
            p.Controls.Add(Btn("Entfernen", 82, 394, 72, delegate { RemoveSel(lbPos, cat.Positions); }));
            p.Controls.Add(Btn("Hoch", 158, 394, 48, delegate { MoveSel(lbPos, cat.Positions, -1); }));
            p.Controls.Add(Btn("Runter", 210, 394, 54, delegate { MoveSel(lbPos, cat.Positions, 1); }));

            p.Controls.Add(L("Name (lang):", 260, 8)); pName.SetBounds(260, 28, 460, 22); p.Controls.Add(pName);
            p.Controls.Add(L("Kurzform im Plantext (z. B. PT, TFF):", 260, 62)); pShort.SetBounds(260, 82, 160, 22); p.Controls.Add(pShort);
            p.Controls.Add(L("Validierte Geräte (eine ID je Zeile, z. B. H1-X-0201):", 260, 116));
            pIds.Multiline = true; pIds.AcceptsReturn = true; pIds.ScrollBars = ScrollBars.Vertical; pIds.SetBounds(260, 136, 460, 200); p.Controls.Add(pIds);
            Label hint = L("Mehrere Geräte pro Position: im Paket-Editor wird das konkrete Gerät gewählt; ein Platzhalter (z. B. H1-X-020X) steht nur optional zur Verfügung.", 260, 344); hint.AutoSize = false; hint.SetBounds(260, 344, 460, 40); p.Controls.Add(hint);
            pName.TextChanged += delegate { if (!loading && lbPos.SelectedItem != null) { ((EquipPosition)lbPos.SelectedItem).Name = pName.Text; Refresh(lbPos); } };
            pRot.Text = "Abwechselnd (reihum) im Paket-Editor erlauben"; pRot.AutoSize = true; pRot.Location = new Point(430, 84); p.Controls.Add(pRot);
            pRot.CheckedChanged += delegate { if (!loading && lbPos.SelectedItem != null) ((EquipPosition)lbPos.SelectedItem).AllowRotation = pRot.Checked; };
            pShort.TextChanged += delegate { if (!loading && lbPos.SelectedItem != null) ((EquipPosition)lbPos.SelectedItem).Short = pShort.Text.Trim(); };
            pIds.TextChanged += delegate
            {
                if (loading || lbPos.SelectedItem == null) return;
                EquipPosition e = (EquipPosition)lbPos.SelectedItem; e.Ids.Clear();
                foreach (string s in pIds.Text.Replace("\r", "").Split('\n')) if (s.Trim().Length > 0) e.Ids.Add(s.Trim());
            };
            if (lbPos.Items.Count > 0) lbPos.SelectedIndex = 0;
        }

        void LoadPos()
        {
            EquipPosition e = lbPos.SelectedItem as EquipPosition;
            loading = true;
            pName.Text = e != null ? e.Name : ""; pShort.Text = e != null ? e.Short : ""; pRot.Checked = e != null && e.AllowRotation;
            pIds.Text = e != null ? string.Join("\r\n", e.Ids.ToArray()) : "";
            loading = false;
        }

        // ---------- Bausteine ----------

        void BuildBausteine(TabPage p)
        {
            p.Controls.Add(L("Bausteine (z. B. Packen, Lauf, TFF Flush):", 8, 8));
            lbBau.SetBounds(8, 28, 230, 360);
            foreach (Baustein b in cat.Bausteine) lbBau.Items.Add(b);
            lbBau.DisplayMember = "Name";
            lbBau.SelectedIndexChanged += delegate { LoadBau(); };
            p.Controls.Add(lbBau);
            p.Controls.Add(Btn("+ Neu", 8, 394, 70, delegate { Baustein b = new Baustein(); b.Name = "Neuer Baustein"; cat.Bausteine.Add(b); lbBau.Items.Add(b); lbBau.SelectedIndex = lbBau.Items.Count - 1; }));
            p.Controls.Add(Btn("Entfernen", 82, 394, 72, delegate { RemoveSel(lbBau, cat.Bausteine); }));
            p.Controls.Add(Btn("Hoch", 158, 394, 48, delegate { MoveSel(lbBau, cat.Bausteine, -1); }));
            p.Controls.Add(Btn("Runter", 210, 394, 54, delegate { MoveSel(lbBau, cat.Bausteine, 1); }));

            p.Controls.Add(L("Name:", 260, 8)); bName.SetBounds(260, 28, 220, 22); p.Controls.Add(bName);
            p.Controls.Add(L("Standard-Block:", 500, 8)); bRow.DropDownStyle = ComboBoxStyle.DropDownList; bRow.SetBounds(500, 28, 220, 22);
            bRow.Items.Add("");
            foreach (string r in rowNames) if (r.Length > 0) bRow.Items.Add(r);
            p.Controls.Add(bRow);
            p.Controls.Add(L("Standardtext (optional):", 260, 62)); bText.SetBounds(260, 82, 220, 22); p.Controls.Add(bText);
            p.Controls.Add(L("Herstellungstyp (Puffer):", 500, 62)); bVar.DropDownStyle = ComboBoxStyle.DropDownList; bVar.SetBounds(500, 82, 220, 22);
            bVar.Items.Add("(keiner)");
            foreach (string v in varVals) if (v.Length > 0) bVar.Items.Add(PlanView.VarLabel(v));
            p.Controls.Add(bVar);
            bVar.SelectedIndexChanged += delegate { if (!loading && lbBau.SelectedItem != null && bVar.SelectedIndex >= 0) ((Baustein)lbBau.SelectedItem).Variant = varVals[bVar.SelectedIndex]; };
            p.Controls.Add(L("Fragt diese Equipment-Positionen ab:", 260, 116));
            bPos.SetBounds(260, 136, 220, 150); bPos.CheckOnClick = true;
            foreach (EquipPosition e in cat.Positions) bPos.Items.Add(e.Short + "  –  " + e.Name);
            p.Controls.Add(bPos);
            bUnique.Text = "Nur einmal je Kampagne (Standard, z. B. Packen)"; bUnique.SetBounds(500, 136, 230, 36);
            bDefault.Text = "In jeder neuen Kampagne enthalten"; bDefault.SetBounds(500, 176, 230, 22);
            bSpecial.Text = "Sonderereignis (z. B. TFF Flush)"; bSpecial.SetBounds(500, 202, 230, 22);
            p.Controls.AddRange(new Control[] { bUnique, bDefault, bSpecial });
            Label lc = L("Standard-Anzahl der markierten Position\n(neue Schritte legen so viele Zeilen an):", 500, 232); lc.AutoSize = false; lc.SetBounds(500, 232, 220, 34); p.Controls.Add(lc);
            bCount.Minimum = 1; bCount.Maximum = 9; bCount.SetBounds(500, 268, 60, 22); p.Controls.Add(bCount);
            bCount.ValueChanged += delegate { SetCount(); };
            bPos.SelectedIndexChanged += delegate { ShowCount(); };
            p.Controls.Add(L("Notiz-Vorlage (je Zeile eine Notiz, z. B. Puffer-Material):", 260, 292));
            bNote.Multiline = true; bNote.AcceptsReturn = true; bNote.ScrollBars = ScrollBars.Vertical; bNote.SetBounds(260, 312, 460, 52); p.Controls.Add(bNote);
            Label hint2 = L("Einmalige Schritte (Packen) dürfen nur Nutzer mit dem Recht \"Pakete einrichten\" löschen oder erneut einfügen.", 260, 372); hint2.AutoSize = false; hint2.SetBounds(260, 372, 460, 36); p.Controls.Add(hint2);
            bNote.TextChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).NoteTemplate = bNote.Text.Replace("\r", "").Trim(); };

            bName.TextChanged += delegate { if (!loading && lbBau.SelectedItem != null) { ((Baustein)lbBau.SelectedItem).Name = bName.Text; Refresh(lbBau); } };
            bRow.SelectedIndexChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).Row = bRow.Text; };
            bText.TextChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).Text = bText.Text; };
            bUnique.CheckedChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).Unique = bUnique.Checked; };
            bDefault.CheckedChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).DefaultInCampaign = bDefault.Checked; };
            bSpecial.CheckedChanged += delegate { if (!loading && lbBau.SelectedItem != null) ((Baustein)lbBau.SelectedItem).Special = bSpecial.Checked; };
            bPos.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                if (loading || lbBau.SelectedItem == null) return;
                Baustein b = (Baustein)lbBau.SelectedItem;
                string sh = cat.Positions[e.Index].Short;
                b.Positions.RemoveAll(delegate(string x) { return x == sh; });
                if (e.NewValue == CheckState.Checked) b.Positions.Add(sh); else b.Counts.Remove(sh);
                if (e.Index == bPos.SelectedIndex) { bCount.Enabled = e.NewValue == CheckState.Checked; bCount.Value = 1; }
            };
            if (lbBau.Items.Count > 0) lbBau.SelectedIndex = 0;
        }

        // Standard-Anzahl der markierten (nicht nur angekreuzten) Position anzeigen / setzen
        void ShowCount()
        {
            Baustein b = lbBau.SelectedItem as Baustein;
            int i = bPos.SelectedIndex;
            bool on = b != null && i >= 0 && i < cat.Positions.Count && b.Positions.Contains(cat.Positions[i].Short);
            bool was = loading; loading = true;
            bCount.Enabled = on; bCount.Value = on ? b.DefaultCount(cat.Positions[i].Short) : 1;
            loading = was;
        }

        void SetCount()
        {
            Baustein b = lbBau.SelectedItem as Baustein;
            int i = bPos.SelectedIndex;
            if (loading || b == null || i < 0 || i >= cat.Positions.Count) return;
            b.SetDefaultCount(cat.Positions[i].Short, (int)bCount.Value);
        }

        // Positionen koennen im anderen Tab geaendert worden sein: Liste im Baustein-Tab neu fuellen
        void RefillPos()
        {
            loading = true;
            bPos.Items.Clear();
            foreach (EquipPosition e in cat.Positions) bPos.Items.Add(e.Short + "  –  " + e.Name);
            loading = false;
        }

        void LoadBau()
        {
            Baustein b = lbBau.SelectedItem as Baustein;
            loading = true;
            bName.Text = b != null ? b.Name : "";
            bRow.SelectedIndex = b != null && b.Row.Length > 0 && bRow.Items.IndexOf(b.Row) >= 0 ? bRow.Items.IndexOf(b.Row) : 0;
            bText.Text = b != null ? b.Text : "";
            int vi = b != null ? Array.IndexOf(varVals, b.Variant) : 0;
            if (vi < 0) { List<string> nv = new List<string>(varVals); nv.Add(b.Variant); varVals = nv.ToArray(); bVar.Items.Add(PlanView.VarLabel(b.Variant)); vi = varVals.Length - 1; }   // unbekannter Wert bleibt erhalten
            bVar.SelectedIndex = vi;
            bNote.Text = b != null ? b.NoteTemplate.Replace("\n", "\r\n") : "";
            bUnique.Checked = b != null && b.Unique; bDefault.Checked = b != null && b.DefaultInCampaign; bSpecial.Checked = b != null && b.Special;
            for (int i = 0; i < bPos.Items.Count; i++) bPos.SetItemChecked(i, b != null && b.Positions.Contains(cat.Positions[i].Short));
            loading = false;
            ShowCount();
        }

        // ---------- Austausch-Gruppen ----------
        // Geraete der angekreuzten Positionen koennen sich gegenseitig vertreten bzw. abwechselnd fuer denselben Zweck eingesetzt werden.

        void BuildGroups(TabPage p)
        {
            p.Controls.Add(L("Austausch-Gruppen (z. B. Freezetanks):", 8, 8));
            lbGrp.SetBounds(8, 28, 230, 360);
            foreach (EquipGroup g in cat.Groups) lbGrp.Items.Add(g);
            lbGrp.SelectedIndexChanged += delegate { LoadGrp(); };
            p.Controls.Add(lbGrp);
            p.Controls.Add(Btn("+ Neu", 8, 394, 70, delegate { EquipGroup g = new EquipGroup(); g.Name = "Neue Gruppe"; cat.Groups.Add(g); lbGrp.Items.Add(g); lbGrp.SelectedIndex = lbGrp.Items.Count - 1; }));
            p.Controls.Add(Btn("Entfernen", 82, 394, 72, delegate { RemoveSel(lbGrp, cat.Groups); }));

            p.Controls.Add(L("Name der Gruppe:", 260, 8)); gName.SetBounds(260, 28, 460, 22); p.Controls.Add(gName);
            p.Controls.Add(L("Diese Positionen können sich gegenseitig vertreten:", 260, 62));
            gPos.SetBounds(260, 82, 460, 250); gPos.CheckOnClick = true; p.Controls.Add(gPos);
            Label hint = L("Im Paket-Editor stehen beim \"abwechselnd\" und beim Ausweich-Equipment dann alle Geräte dieser Positionen zur Auswahl (z. B. Freezetank 1 → Freezetank 2).", 260, 340);
            hint.AutoSize = false; hint.SetBounds(260, 340, 460, 40); p.Controls.Add(hint);
            gName.TextChanged += delegate { if (!loading && lbGrp.SelectedItem != null) { ((EquipGroup)lbGrp.SelectedItem).Name = gName.Text; Refresh(lbGrp); } };
            gPos.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                if (loading || lbGrp.SelectedItem == null) return;
                EquipGroup g = (EquipGroup)lbGrp.SelectedItem;
                string sh = cat.Positions[e.Index].Short;
                g.Shorts.RemoveAll(delegate(string x) { return x == sh; });
                if (e.NewValue == CheckState.Checked) g.Shorts.Add(sh);
            };
            RefillGroupPos();
            if (lbGrp.Items.Count > 0) lbGrp.SelectedIndex = 0;
        }

        void RefillGroupPos()
        {
            loading = true;
            gPos.Items.Clear();
            foreach (EquipPosition e in cat.Positions) gPos.Items.Add(e.Short + "  –  " + e.Name);
            loading = false;
        }

        void LoadGrp()
        {
            EquipGroup g = lbGrp.SelectedItem as EquipGroup;
            loading = true;
            gName.Text = g != null ? g.Name : "";
            for (int i = 0; i < gPos.Items.Count; i++) gPos.SetItemChecked(i, g != null && g.Shorts.Contains(cat.Positions[i].Short));
            loading = false;
        }

        // ---------- gemeinsame Hilfen ----------

        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }

        static void Refresh(ListBox lb) { int i = lb.SelectedIndex; if (i >= 0) lb.Items[i] = lb.Items[i]; }

        static void RemoveSel<T>(ListBox lb, List<T> list)
        {
            int i = lb.SelectedIndex;
            if (i < 0) return;
            list.RemoveAt(i); lb.Items.RemoveAt(i);
            if (lb.Items.Count > 0) lb.SelectedIndex = Math.Min(i, lb.Items.Count - 1);
        }

        static void MoveSel<T>(ListBox lb, List<T> list, int delta)
        {
            int i = lb.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count) return;
            T t = list[i]; list[i] = list[j]; list[j] = t;
            object o = lb.Items[i]; lb.Items[i] = lb.Items[j]; lb.Items[j] = o;
            lb.SelectedIndex = j;
        }

        void Commit()
        {
            foreach (EquipPosition e in cat.Positions)
                if (e.Short.Length == 0) { MessageBox.Show("Jede Position braucht eine Kurzform (z. B. PT).", "Einrichten", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Result = cat;
            DialogResult = DialogResult.OK;
        }
    }
}
