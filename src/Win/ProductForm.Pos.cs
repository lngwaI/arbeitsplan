using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Reiter "Geraete je Position" von "Produkte & Equipment": Quelle Standardliste/eigene Liste, Geraete nach Geraetegruppe (Gruppenzeile = ganze
    // Gruppe), fest/frei, zuruecksetzen. Positionseintraege und Equipment-Gruppen: ProductForm.Groups.cs.
    partial class ProductForm
    {
        PosItem cur;          // gewaehlter Eintrag der Positionsliste (Position oder Equipment-Gruppe)
        EquipPosition pos;    // erste Position von cur (Referenz fuer die Anzeige)
        readonly ListBox lbPos = new ListBox();
        readonly Label posTitle = new Label(), posInfo = new Label();
        readonly RadioButton rStd = new RadioButton(), rOwn = new RadioButton(), rFixed = new RadioButton(), rFree = new RadioButton();
        readonly LinkLabel stdLink = new LinkLabel();
        readonly CheckedListBox devs = new CheckedListBox();
        readonly Button reset = new Button(), unify = new Button();

        class DevItem { public string Id, Text; public override string ToString() { return Text; } }
        class GroupHead { public string Name; public List<string> Ids = new List<string>(); public override string ToString() { return "▸ " + Name + " (ganze Gruppe)"; } }

        void BuildPos()
        {
            tabPos.Controls.Add(L("Equipment-Positionen:", 8, 8));
            lbPos.SetBounds(8, 26, 282, 518); lbPos.IntegralHeight = false; tabPos.Controls.Add(lbPos);
            lbPos.SelectedIndexChanged += delegate { if (!loading) SelectPos(lbPos.SelectedItem as PosItem); };

            posTitle.SetBounds(300, 8, 354, 18); posTitle.AutoEllipsis = true; posTitle.UseMnemonic = false; tabPos.Controls.Add(posTitle);
            rStd.Text = "Standardliste (aus „Equipment & Bausteine“)"; rStd.AutoSize = true; rStd.UseMnemonic = false; rStd.Location = new Point(0, 2);
            stdLink.Text = "Standardliste bearbeiten…"; stdLink.AutoSize = true; stdLink.Location = new Point(22, 26); stdLink.Visible = rowNames != null;
            stdLink.LinkClicked += delegate { EditStandard(); };
            rOwn.Text = "eigene Liste für dieses Produkt"; rOwn.AutoSize = true; rOwn.UseMnemonic = false; rOwn.Location = new Point(0, 50);
            Panel src = new Panel(); src.SetBounds(300, 28, 354, 74); src.Controls.AddRange(new Control[] { rStd, stdLink, rOwn }); tabPos.Controls.Add(src);   // eigene Panels: zwei getrennte Optionsgruppen
            rOwn.CheckedChanged += delegate { if (!loading) SetOwn(rOwn.Checked); };

            devs.SetBounds(300, 104, 354, 300); devs.CheckOnClick = true; devs.IntegralHeight = false; tabPos.Controls.Add(devs);
            devs.ItemCheck += delegate(object s, ItemCheckEventArgs e) { DevsCheck(e); };

            rFixed.Text = "Fest: ein Gerät je Kampagne"; rFixed.AutoSize = true; rFixed.UseMnemonic = false; rFixed.Location = new Point(0, 0);
            rFree.Text = "Frei: Geräte tauschbar"; rFree.AutoSize = true; rFree.UseMnemonic = false; rFree.Location = new Point(0, 24);
            rFixed.CheckedChanged += delegate { if (!loading) SetFixed(rFixed.Checked); };
            reset.Text = "Auf Standard zurücksetzen"; reset.SetBounds(484, 412, 170, 26);
            reset.Click += delegate { if (prod != null && cur != null) { WriteAll(false, false, null); Changed("Liste auf Standard zurückgesetzt."); } };
            posInfo.SetBounds(300, 468, 354, 46); posInfo.ForeColor = SystemColors.GrayText; posInfo.UseMnemonic = false;
            unify.SetBounds(300, 516, 354, 26); unify.Visible = false; unify.Click += delegate { Unify(); };
            Panel mode = new Panel(); mode.SetBounds(300, 410, 180, 50); mode.Controls.AddRange(new Control[] { rFixed, rFree }); tabPos.Controls.AddRange(new Control[] { mode, reset, posInfo, unify });
        }

        void FillPos()
        {
            string key = cur != null ? cur.Key : null;
            loading = true;
            lbPos.Items.Clear();
            PosItem pick = null;
            if (prod != null) foreach (PosItem pi in BuildItems(prod)) { lbPos.Items.Add(pi); if (pi.Key == key) pick = pi; }
            if (pick == null && lbPos.Items.Count > 0) pick = (PosItem)lbPos.Items[0];
            if (pick != null) lbPos.SelectedItem = pick;
            lbPos.Enabled = prod != null;
            loading = false;
            SelectPos(pick);
        }

        void RefreshPosList()
        {
            bool o = loading; loading = true;
            int si = lbPos.SelectedIndex;
            for (int i = 0; i < lbPos.Items.Count; i++) lbPos.Items[i] = lbPos.Items[i];
            if (si >= 0) lbPos.SelectedIndex = si;
            loading = o;
        }

        // Aenderung der Liste/Quelle: zaehlen, Anzeige neu aufbauen
        void Changed(string action) { Touch(action); RefreshPosList(); SelectPos(cur); }

        string CurName { get { return cur.IsGroup ? "Gruppe " + cur.GroupName : pos.Short; } }

        void SetOwn(bool own)
        {
            if (prod == null || cur == null) return;
            ProfilePos r = prod.Pos(pos.Short);
            List<string> ids = own ? (r != null && r.Own ? r.Ids : StdUnion()) : null;   // Start mit der Standardliste
            WriteAll(own, r != null && r.Fixed, ids);
            Changed(own ? "Eigene Liste für " + CurName + " angelegt (Start: Standardliste)." : "Standardliste gilt wieder für " + CurName + ".");
        }

        void SetFixed(bool fix)
        {
            if (prod == null || cur == null) return;
            ProfilePos r = prod.Pos(pos.Short);
            WriteAll(r != null && r.Own, fix, r != null ? r.Ids : null);
            Changed(CurName + (fix ? ": fest (ein Gerät je Kampagne)." : ": frei (Geräte tauschbar)."));
        }

        // Standardliste: "Equipment & Bausteine" direkt auf der Arbeitskopie dieses Dialogs (Produkte bleiben)
        void EditStandard()
        {
            if (rowNames == null) return;
            using (CatalogForm f = new CatalogForm(cat, rowNames, variants))
                if (f.ShowDialog(this) == DialogResult.OK) ApplyStandard(f.Result);
        }

        void ApplyStandard(Catalog src)
        {
            cat.Positions = src.Positions; cat.Groups = src.Groups; cat.Bausteine = src.Bausteine;   // nur diese Teile ändert CatalogForm
            Touch("Standardliste geändert – wird mit „Speichern“ gesichert.");
            SelectProduct(prod);
        }

        // Auswahl: Standardgeraete, eigene Liste und alle Geraete derselben Geraeteart(en) aus den Stammdaten (Vereinigung ueber den Eintrag)
        List<string> Candidates()
        {
            List<string> r = new List<string>(), cls = new List<string>();
            foreach (EquipPosition e in cur.Ps)
            {
                foreach (string id in e.Ids) if (!r.Contains(id)) r.Add(id);
                ProfilePos pp = prod.Pos(e.Short);
                if (pp != null) foreach (string id in pp.Ids) if (!r.Contains(id)) r.Add(id);
            }
            foreach (string id in r) { EquipDevice d = cat.Master.Device(id); if (d != null && d.Class.Length > 0 && !cls.Contains(d.Class)) cls.Add(d.Class); }
            foreach (EquipDevice d in cat.Master.Devices) if (cls.Contains(d.Class) && !r.Contains(d.Id)) r.Add(d.Id);
            return r;
        }

        void SelectPos(PosItem pi)
        {
            cur = pi; pos = pi != null ? pi.Ps[0] : null;
            loading = true;
            devs.Items.Clear();
            bool any = prod != null && pi != null, diff = any && GroupDiffers();
            rStd.Enabled = rOwn.Enabled = rFixed.Enabled = rFree.Enabled = reset.Enabled = any;
            unify.Visible = diff; posInfo.ForeColor = diff ? Orange : SystemColors.GrayText;
            posTitle.Text = !any ? "" : pi.IsGroup ? "Gemeinsame Liste: " + pi.GroupName + " (" + pi.Shorts + ")" : "Position: " + pos.Name + " (" + pos.Short + ")";
            if (any)
            {
                ProfilePos pp = prod.Pos(pos.Short);
                bool own = pp != null && pp.Own;
                List<string> std = StdUnion(), on = own ? pp.Ids : std;
                List<GroupHead> heads = new List<GroupHead>(); GroupHead rest = new GroupHead(); rest.Name = "ohne Gerätegruppe";
                foreach (string id in Candidates())
                {
                    List<EquipSeries> ss = cat.Master.SeriesOf(id);
                    GroupHead h = rest;
                    if (ss.Count > 0) { h = heads.Find(delegate(GroupHead x) { return x.Name == ss[0].Name; }); if (h == null) { h = new GroupHead(); h.Name = ss[0].Name; heads.Add(h); } }
                    h.Ids.Add(id);
                }
                if (rest.Ids.Count > 0) heads.Add(rest);
                foreach (GroupHead h in heads)
                {
                    devs.Items.Add(h, h.Ids.TrueForAll(delegate(string x) { return on.Contains(x); }));
                    foreach (string id in h.Ids)
                    {
                        EquipDevice d = cat.Master.Device(id);
                        DevItem it = new DevItem(); it.Id = id;
                        it.Text = "      " + id + (d != null && !d.Active ? " (Wartung)" : "") + (std.Contains(id) ? "" : "  – nicht im Standard");
                        devs.Items.Add(it, on.Contains(id));
                    }
                }
                devs.ForeColor = own ? SystemColors.WindowText : SystemColors.GrayText;   // Standard: nur ansehen (Haken werden zurueckgesetzt)
                rStd.Checked = !own; rOwn.Checked = own;
                rFixed.Checked = pp != null && pp.Fixed; rFree.Checked = !rFixed.Checked;
                unify.Text = "Vereinheitlichen (Liste von " + pos.Short + " für alle übernehmen)";
                posInfo.Text = diff ? "Die Positionen der Gruppe haben unterschiedliche Listen."
                    : (own ? "Eigene Liste: Haken setzen oder entfernen; Haken an „▸ …“ wählt die ganze Gerätegruppe."
                    : "Es gilt die Standardliste (" + std.Count + " Geräte). Für eine eigene Auswahl „eigene Liste für dieses Produkt“ wählen.")
                    + (pi.IsGroup ? "\nÄnderungen gelten für alle Positionen der Gruppe." : "\nFest: im Paket-Editor ist „abwechselnd“ gesperrt.");
            }
            else posInfo.Text = "";
            loading = false;
        }

        void DevsCheck(ItemCheckEventArgs e)
        {
            if (loading) return;
            ProfilePos pp = prod != null && cur != null ? prod.Pos(pos.Short) : null;
            if (pp == null || !pp.Own) { e.NewValue = e.CurrentValue; return; }
            bool on = e.NewValue == CheckState.Checked;
            loading = true;
            if (devs.Items[e.Index] is GroupHead)
                for (int i = e.Index + 1; i < devs.Items.Count && devs.Items[i] is DevItem; i++) devs.SetItemChecked(i, on);
            List<string> ids = new List<string>();
            for (int i = 0; i < devs.Items.Count; i++)
            {
                DevItem d = devs.Items[i] as DevItem;
                if (d != null && (i == e.Index ? on : devs.GetItemChecked(i))) ids.Add(d.Id);
            }
            WriteAll(true, pp.Fixed, ids);
            for (int i = 0; i < devs.Items.Count; i++)   // Gruppenzeilen nachziehen
            {
                GroupHead h = devs.Items[i] as GroupHead;
                if (h != null && i != e.Index) devs.SetItemChecked(i, h.Ids.TrueForAll(delegate(string x) { return ids.Contains(x); }));
            }
            loading = false;
            RefreshPosList();
            Touch("Geräteliste für " + CurName + " geändert.");
            if (unify.Visible) { unify.Visible = false; posInfo.ForeColor = SystemColors.GrayText; posInfo.Text = "Änderungen gelten für alle Positionen der Gruppe."; }
        }
    }
}
