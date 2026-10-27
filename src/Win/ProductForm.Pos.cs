using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Rechte Seite von "Produkte & Equipment": Quelle Standard/eigen, Geraete nach Gerätegruppe (Gruppenzeile = ganze Gruppe), fest/frei,
    // zuruecksetzen; darunter die Austauschgruppen des Produkts.
    partial class ProductForm
    {
        readonly Label posTitle = new Label(), posInfo = new Label();
        readonly RadioButton rStd = new RadioButton(), rOwn = new RadioButton(), rFixed = new RadioButton(), rFree = new RadioButton();
        readonly CheckedListBox devs = new CheckedListBox();
        readonly Button reset = new Button();
        readonly ListBox lbSwap = new ListBox(), swMembers = new ListBox();
        readonly TextBox swName = new TextBox();

        class DevItem { public string Id, Text; public override string ToString() { return Text; } }
        class GroupHead { public string Name; public List<string> Ids = new List<string>(); public override string ToString() { return "▸ " + Name + " (ganze Gruppe)"; } }
        class SwapItem { public SwapGroup G; public override string ToString() { return (G.Name.Length > 0 ? G.Name : "(ohne Name)") + " (" + G.Ids.Count + ")"; } }

        void BuildPos()
        {
            posTitle.SetBounds(470, 30, 500, 18); Controls.Add(posTitle);
            Panel src = new Panel(); src.SetBounds(470, 48, 500, 24);
            rStd.Text = "Standard (wie „Equipment & Bausteine“)"; rStd.AutoSize = true; rStd.Location = new Point(0, 2);
            rOwn.Text = "eigen für dieses Produkt"; rOwn.AutoSize = true; rOwn.Location = new Point(270, 2);
            src.Controls.Add(rStd); src.Controls.Add(rOwn); Controls.Add(src);
            rOwn.CheckedChanged += delegate { if (!loading) SetOwn(rOwn.Checked); };

            devs.SetBounds(470, 76, 300, 330); devs.CheckOnClick = true; devs.IntegralHeight = false; Controls.Add(devs);
            devs.ItemCheck += delegate(object s, ItemCheckEventArgs e) { DevsCheck(e); };

            Panel mode = new Panel(); mode.SetBounds(780, 76, 192, 50);
            rFixed.Text = "Fest: ein Gerät je Kampagne"; rFixed.AutoSize = true; rFixed.Location = new Point(0, 0);
            rFree.Text = "Frei: Geräte tauschbar"; rFree.AutoSize = true; rFree.Location = new Point(0, 24);
            mode.Controls.Add(rFixed); mode.Controls.Add(rFree); Controls.Add(mode);
            rFixed.CheckedChanged += delegate { if (!loading) SetFixed(rFixed.Checked); };
            reset.Text = "Auf Standard zurücksetzen"; reset.SetBounds(780, 134, 192, 26); Controls.Add(reset);
            reset.Click += delegate { if (prod != null && pos != null) { prod.Positions.Remove(prod.Pos(pos.Short)); Changed(); } };
            posInfo.SetBounds(780, 170, 192, 236); posInfo.ForeColor = SystemColors.GrayText; Controls.Add(posInfo);
        }

        // Eigene Position des Produkts (anlegen bei Bedarf)
        ProfilePos MakePos()
        {
            ProfilePos pp = prod.Pos(pos.Short);
            if (pp == null) { pp = new ProfilePos(); pp.Short = pos.Short; prod.Positions.Add(pp); }
            return pp;
        }

        void DropIfDefault(ProfilePos pp) { if (!pp.Own && !pp.Fixed) prod.Positions.Remove(pp); }

        void SetOwn(bool own)
        {
            if (prod == null || pos == null) return;
            ProfilePos pp = MakePos();
            pp.Own = own;
            if (own && pp.Ids.Count == 0) pp.Ids.AddRange(pos.Ids);   // Start mit der Standardliste
            if (!own) { pp.Ids.Clear(); DropIfDefault(pp); }
            Changed();
        }

        void SetFixed(bool fix)
        {
            if (prod == null || pos == null) return;
            ProfilePos pp = MakePos(); pp.Fixed = fix; DropIfDefault(pp);
            Changed();
        }

        void Changed() { SelectPos(pos); RefreshItem(lbPos); FillSwaps(lbSwap.SelectedItem is SwapItem ? ((SwapItem)lbSwap.SelectedItem).G : null); }

        // Auswahl: globale Geraete der Position, eigene Liste und alle Geraete derselben Geraeteart(en) aus den Stammdaten
        List<string> Candidates(ProfilePos pp)
        {
            List<string> r = new List<string>(pos.Ids), cls = new List<string>();
            if (pp != null) foreach (string id in pp.Ids) if (!r.Contains(id)) r.Add(id);
            foreach (string id in r) { EquipDevice d = cat.Master.Device(id); if (d != null && d.Class.Length > 0 && !cls.Contains(d.Class)) cls.Add(d.Class); }
            foreach (EquipDevice d in cat.Master.Devices) if (cls.Contains(d.Class) && !r.Contains(d.Id)) r.Add(d.Id);
            return r;
        }

        void SelectPos(EquipPosition g)
        {
            pos = g;
            loading = true;
            devs.Items.Clear();
            bool any = prod != null && g != null;
            rStd.Enabled = rOwn.Enabled = rFixed.Enabled = rFree.Enabled = reset.Enabled = any;
            posTitle.Text = any ? "Position: " + g.Name + " (" + g.Short + ") – Produkt " + prod : "Bitte links ein Produkt anlegen oder wählen.";
            if (any)
            {
                ProfilePos pp = prod.Pos(g.Short);
                bool own = pp != null && pp.Own;
                List<string> on = own ? pp.Ids : g.Ids;
                List<GroupHead> heads = new List<GroupHead>(); GroupHead rest = new GroupHead(); rest.Name = "ohne Gerätegruppe";
                foreach (string id in Candidates(pp))
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
                        it.Text = "      " + id + (d != null && !d.Active ? " (Wartung)" : "") + (pos.Ids.Contains(id) ? "" : "  – nicht im Standard");
                        devs.Items.Add(it, on.Contains(id));
                    }
                }
                devs.ForeColor = own ? SystemColors.WindowText : SystemColors.GrayText;   // Standard: nur ansehen (Haken werden zurueckgesetzt)
                rStd.Checked = !own; rOwn.Checked = own;
                rFixed.Checked = pp != null && pp.Fixed; rFree.Checked = !rFixed.Checked;
                posInfo.Text = (own ? "Eigene Liste: Haken setzen oder entfernen; Haken an „▸ …“ wählt die ganze Gerätegruppe."
                    : "Es gilt der Standard (" + g.Ids.Count + " Geräte). Für eine eigene Auswahl „eigen für dieses Produkt“ wählen.")
                    + "\n\nFest: im Paket-Editor ist „abwechselnd“ gesperrt.";
            }
            else posInfo.Text = "";
            loading = false;
        }

        void DevsCheck(ItemCheckEventArgs e)
        {
            if (loading) return;
            ProfilePos pp = prod != null && pos != null ? prod.Pos(pos.Short) : null;
            if (pp == null || !pp.Own) { e.NewValue = e.CurrentValue; return; }
            bool on = e.NewValue == CheckState.Checked;
            loading = true;
            if (devs.Items[e.Index] is GroupHead)
                for (int i = e.Index + 1; i < devs.Items.Count && devs.Items[i] is DevItem; i++) devs.SetItemChecked(i, on);
            pp.Ids.Clear();
            for (int i = 0; i < devs.Items.Count; i++)
            {
                DevItem d = devs.Items[i] as DevItem;
                if (d != null && (i == e.Index ? on : devs.GetItemChecked(i))) pp.Ids.Add(d.Id);
            }
            for (int i = 0; i < devs.Items.Count; i++)   // Gruppenzeilen nachziehen
            {
                GroupHead h = devs.Items[i] as GroupHead;
                if (h != null && i != e.Index) devs.SetItemChecked(i, h.Ids.TrueForAll(delegate(string x) { return pp.Ids.Contains(x); }));
            }
            loading = false;
            RefreshItem(lbPos);
        }

        // ---------- Austauschgruppen ----------

        void BuildSwaps()
        {
            Controls.Add(L("Austauschgruppen dieses Produkts (Geräte vertreten sich gegenseitig, Ausweich-Vorschlag zuerst):", 250, 416));
            lbSwap.SetBounds(250, 436, 210, 140); lbSwap.IntegralHeight = false; Controls.Add(lbSwap);
            lbSwap.SelectedIndexChanged += delegate { if (!loading) LoadSwap(); };
            Controls.Add(Btn("Neu", 250, 580, 60, delegate { NewSwap(); }));
            Controls.Add(Btn("Entfernen", 314, 580, 80, delegate { SwapItem s = lbSwap.SelectedItem as SwapItem; if (s != null) { prod.Swaps.Remove(s.G); FillSwaps(null); } }));
            Controls.Add(L("Name:", 470, 439)); swName.SetBounds(520, 436, 250, 22); Ui.SimpleMenu(swName); Controls.Add(swName);
            Controls.Add(L("Mitglieder (Mehrfachauswahl mit Strg/Umschalt):", 470, 462));
            swMembers.SetBounds(470, 480, 300, 126); swMembers.SelectionMode = SelectionMode.MultiExtended; swMembers.IntegralHeight = false; Controls.Add(swMembers);
            Label h = new Label(); h.Text = "Zur Auswahl stehen die für dieses Produkt erlaubten Geräte aller Positionen.";
            h.SetBounds(780, 480, 192, 60); h.ForeColor = SystemColors.GrayText; Controls.Add(h);
            swName.TextChanged += delegate { SwapItem s = lbSwap.SelectedItem as SwapItem; if (loading || s == null) return; s.G.Name = swName.Text.Trim(); RefreshItem(lbSwap); };
            swMembers.SelectedIndexChanged += delegate
            {
                SwapItem s = lbSwap.SelectedItem as SwapItem; if (loading || s == null) return;
                s.G.Ids.Clear(); foreach (object o in swMembers.SelectedItems) s.G.Ids.Add((string)o);
                RefreshItem(lbSwap);
            };
        }

        void FillSwaps(SwapGroup select)
        {
            loading = true;
            lbSwap.Items.Clear();
            if (prod != null) foreach (SwapGroup g in prod.Swaps) { SwapItem s = new SwapItem(); s.G = g; lbSwap.Items.Add(s); if (g == select) lbSwap.SelectedItem = s; }
            if (lbSwap.SelectedIndex < 0 && lbSwap.Items.Count > 0) lbSwap.SelectedIndex = 0;
            loading = false;
            LoadSwap();
        }

        void LoadSwap()
        {
            SwapItem s = lbSwap.SelectedItem as SwapItem;
            loading = true;
            swName.Text = s != null ? s.G.Name : ""; swMembers.Items.Clear();
            swName.Enabled = swMembers.Enabled = s != null;
            if (s != null)
            {
                List<string> all = new List<string>();
                foreach (EquipPosition g in cat.Positions) foreach (string id in cat.Allowed(prod.Code, g.Short)) if (!all.Contains(id)) all.Add(id);
                foreach (string id in s.G.Ids) if (!all.Contains(id)) all.Add(id);
                foreach (string id in all) { int i = swMembers.Items.Add(id); if (s.G.Ids.Contains(id)) swMembers.SetSelected(i, true); }
            }
            loading = false;
        }

        void NewSwap()
        {
            if (prod == null) return;
            SwapGroup g = new SwapGroup(); g.Name = "Neue Austauschgruppe"; prod.Swaps.Add(g);
            FillSwaps(g); swName.Focus(); swName.SelectAll();
        }
    }
}
