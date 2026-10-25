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
        // Bausteine
        readonly ListBox lbBau = new ListBox();
        readonly TextBox bName = new TextBox(), bText = new TextBox();
        readonly ComboBox bRow = new ComboBox();
        readonly CheckedListBox bPos = new CheckedListBox();
        readonly CheckBox bUnique = new CheckBox(), bDefault = new CheckBox(), bSpecial = new CheckBox();
        bool loading;

        public CatalogForm(Catalog catalog, List<string> rowNames)
        {
            cat = catalog.Clone(); this.rowNames = rowNames;
            Text = "Equipment-Positionen und Bausteine einrichten";
            Font = Theme.Current.UiFont();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 520);

            TabControl tabs = new TabControl(); tabs.SetBounds(8, 8, 744, 462);
            TabPage t1 = new TabPage("Equipment-Positionen"), t2 = new TabPage("Bausteine (Ablaufschritte)");
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedIndex == 1) { RefillPos(); LoadBau(); } };
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2);
            BuildPositions(t1); BuildBausteine(t2);

            Button ok = new Button(); ok.Text = "Speichern"; ok.SetBounds(560, 480, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(660, 480, 95, 30); cancel.DialogResult = DialogResult.Cancel;
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
            pName.Text = e != null ? e.Name : ""; pShort.Text = e != null ? e.Short : "";
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
            p.Controls.Add(L("Standardtext (optional):", 260, 62)); bText.SetBounds(260, 82, 460, 22); p.Controls.Add(bText);
            p.Controls.Add(L("Fragt diese Equipment-Positionen ab:", 260, 116));
            bPos.SetBounds(260, 136, 220, 150); bPos.CheckOnClick = true;
            foreach (EquipPosition e in cat.Positions) bPos.Items.Add(e.Short + "  –  " + e.Name);
            p.Controls.Add(bPos);
            bUnique.Text = "Nur einmal je Kampagne (Standard, z. B. Packen)"; bUnique.SetBounds(500, 136, 230, 36);
            bDefault.Text = "In jeder neuen Kampagne enthalten"; bDefault.SetBounds(500, 176, 230, 22);
            bSpecial.Text = "Sonderereignis (z. B. TFF Flush)"; bSpecial.SetBounds(500, 202, 230, 22);
            p.Controls.AddRange(new Control[] { bUnique, bDefault, bSpecial });
            Label hint2 = L("Einmalige Schritte (Packen) dürfen nur Nutzer mit dem Recht \"Pakete einrichten\" löschen oder erneut einfügen.", 260, 300); hint2.AutoSize = false; hint2.SetBounds(260, 300, 460, 40); p.Controls.Add(hint2);

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
                if (e.NewValue == CheckState.Checked) b.Positions.Add(sh);
            };
            if (lbBau.Items.Count > 0) lbBau.SelectedIndex = 0;
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
            bUnique.Checked = b != null && b.Unique; bDefault.Checked = b != null && b.DefaultInCampaign; bSpecial.Checked = b != null && b.Special;
            for (int i = 0; i < bPos.Items.Count; i++) bPos.SetItemChecked(i, b != null && b.Positions.Contains(cat.Positions[i].Short));
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
