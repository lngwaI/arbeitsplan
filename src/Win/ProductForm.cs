using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen -> Produkte & Equipment (Recht PackageAdmin): je Produkt (Code frei, z. B. AB1) und Equipment-Position die erlaubten Geraete
    // (Standard oder eigene Liste), fest/frei und Austauschgruppen. Bearbeitet eine Katalog-Kopie; gespeichert wird der ganze Katalog.
    partial class ProductForm : Form
    {
        readonly Catalog cat;
        public Catalog Result;
        bool loading;
        ProductProfile prod;
        EquipPosition pos;

        readonly ListBox lbProd = new ListBox(), lbPos = new ListBox();
        readonly TextBox pCode = new TextBox(), pName = new TextBox();

        public ProductForm(Catalog catalog)
        {
            cat = catalog.Clone();
            Text = "Produkte & Equipment";
            Ui.Dialog(this, 980, 650);
            Controls.Add(Ui.Hint("Was ist das? Je Produkt legt man fest, welche Geräte an jeder Position erlaubt sind. Ohne eigene Liste gilt der Standard aus „Equipment & Bausteine“.", 8, 6, 964));

            Controls.Add(L("Produkte (Code):", 8, 30));
            lbProd.SetBounds(8, 48, 230, 440); lbProd.IntegralHeight = false; Controls.Add(lbProd);
            lbProd.SelectedIndexChanged += delegate { if (!loading) SelectProduct(lbProd.SelectedItem as ProductProfile); };
            Controls.Add(L("Code:", 8, 496)); pCode.SetBounds(8, 514, 100, 22); pCode.MaxLength = 12; Ui.SimpleMenu(pCode); Controls.Add(pCode);
            Controls.Add(L("Name:", 114, 496)); pName.SetBounds(114, 514, 124, 22); Ui.SimpleMenu(pName); Controls.Add(pName);
            pCode.TextChanged += delegate { if (!loading && prod != null) { prod.Code = pCode.Text.Trim(); RefreshItem(lbProd); } };
            pName.TextChanged += delegate { if (!loading && prod != null) { prod.Name = pName.Text.Trim(); RefreshItem(lbProd); } };
            Controls.Add(Btn("Neu…", 8, 544, 60, delegate { NewProduct(); }));
            Controls.Add(Btn("Wie markiertes kopieren…", 72, 544, 166, delegate { CopySelected(); }));
            Controls.Add(Btn("Entfernen", 8, 574, 80, delegate { RemoveSelected(); }));

            Controls.Add(L("Equipment-Positionen:", 250, 30));
            lbPos.SetBounds(250, 48, 210, 360); lbPos.IntegralHeight = false; Controls.Add(lbPos);
            lbPos.SelectedIndexChanged += delegate { if (!loading) { PosItem pi = lbPos.SelectedItem as PosItem; SelectPos(pi != null ? pi.P : null); } };
            BuildPos(); BuildSwaps();

            Button ok = Btn("Speichern", 780, 612, 95, delegate { Commit(); }); ok.Height = 30;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(880, 612, 92, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { ok, cancel });
            FillProducts(cat.Products.Count > 0 ? cat.Products[0] : null);
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }
        static void RefreshItem(ListBox lb) { int i = lb.SelectedIndex; if (i >= 0) lb.Items[i] = lb.Items[i]; }
        void Warn(string text) { MessageBox.Show(this, text, "Produkte & Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        static bool ValidCode(string c) { return Package.IsProductCode(c); }   // frei: 2-12 Zeichen, z. B. AB1 (alt 1357)
        const string CodeRule = "2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (ohne Leerzeichen), z. B. AB1 oder AB12345678-";

        // Position in der Liste, mit Kennzeichen, wenn das Produkt dort eine eigene Liste oder "fest" hat
        class PosItem
        {
            public EquipPosition P; public ProductProfile Pr;
            public override string ToString()
            {
                ProfilePos pp = Pr != null ? Pr.Pos(P.Short) : null;
                string tag = pp == null ? "" : (pp.Own ? "  ● eigen" : "") + (pp.Fixed ? "  ● fest" : "");
                return P.Name + " (" + P.Short + ")" + tag;
            }
        }

        void FillProducts(ProductProfile select)
        {
            loading = true;
            lbProd.Items.Clear();
            foreach (ProductProfile p in cat.Products) lbProd.Items.Add(p);
            lbProd.SelectedItem = select;
            loading = false;
            SelectProduct(select);
        }

        void SelectProduct(ProductProfile p)
        {
            prod = p;
            loading = true;
            pCode.Text = p != null ? p.Code : ""; pName.Text = p != null ? p.Name : "";
            pCode.Enabled = pName.Enabled = lbPos.Enabled = p != null;
            EquipPosition keep = pos;
            lbPos.Items.Clear();
            foreach (EquipPosition g in cat.Positions) { PosItem pi = new PosItem(); pi.P = g; pi.Pr = p; lbPos.Items.Add(pi); if (g == keep) lbPos.SelectedItem = pi; }
            if (lbPos.SelectedIndex < 0 && lbPos.Items.Count > 0) lbPos.SelectedIndex = 0;
            loading = false;
            PosItem sel = lbPos.SelectedItem as PosItem;
            SelectPos(p != null && sel != null ? sel.P : null);
            FillSwaps(null);
        }

        // Code/Name abfragen (Neu, Kopieren); false = abgebrochen
        bool Ask(string title, ref string code, ref string name)
        {
            using (Form f = new Form())
            {
                f.Text = title; Ui.Dialog(f, 330, 130, false);
                TextBox c = new TextBox(), n = new TextBox(); c.Text = code; n.Text = name;
                f.Controls.Add(L("Produktcode:", 8, 10)); c.SetBounds(100, 7, 120, 22); c.MaxLength = 12; f.Controls.Add(c);
                f.Controls.Add(L("Name:", 8, 42)); n.SetBounds(60, 39, 262, 22); f.Controls.Add(n);
                Button ok = Btn("OK", 152, 90, 80, null); ok.DialogResult = DialogResult.OK;
                Button ca = Btn("Abbrechen", 238, 90, 84, null); ca.DialogResult = DialogResult.Cancel;
                f.Controls.Add(ok); f.Controls.Add(ca); f.AcceptButton = ok; f.CancelButton = ca;
                while (f.ShowDialog(this) == DialogResult.OK)
                {
                    string cc = c.Text.Trim();
                    if (!ValidCode(cc)) { Warn("Produktcode ungültig: " + CodeRule + "."); continue; }
                    if (cat.Product(cc) != null) { Warn("Produkt " + cc + " gibt es schon."); continue; }
                    code = cc; name = n.Text.Trim();
                    return true;
                }
                return false;
            }
        }

        void NewProduct()
        {
            string code = "", name = "";
            if (Ask("Neues Produkt", ref code, ref name)) AddProduct(code, name);
        }

        void AddProduct(string code, string name)
        {
            ProductProfile p = new ProductProfile(); p.Code = code; p.Name = name; cat.Products.Add(p);
            FillProducts(p);
        }

        void CopySelected()
        {
            if (prod == null) { Warn("Bitte zuerst links das Produkt markieren, das als Vorlage dient."); return; }
            string code = "", name = prod.Name;
            if (Ask("Wie Produkt " + prod.Code + " kopieren", ref code, ref name)) FillProducts(cat.CopyProduct(prod.Code, code, name));
        }

        void RemoveSelected()
        {
            if (prod == null) return;
            if (MessageBox.Show(this, "Produkt " + prod + " wirklich entfernen?\nIm Plan ändert sich nichts; für das Produkt gilt danach wieder der Standard.",
                "Produkte & Equipment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int i = cat.Products.IndexOf(prod); cat.Products.Remove(prod);
            FillProducts(cat.Products.Count > 0 ? cat.Products[Math.Min(i, cat.Products.Count - 1)] : null);
        }

        void Commit()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (ProductProfile p in cat.Products)
            {
                if (!ValidCode(p.Code)) { Warn("Produktcode \"" + p.Code + "\" ist ungültig: " + CodeRule + "."); return; }
                if (!seen.Add(p.Code)) { Warn("Produkt " + p.Code + " ist doppelt vorhanden."); return; }
                p.Swaps.RemoveAll(delegate(SwapGroup g) { return g.Ids.Count == 0; });
            }
            Result = cat;
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        public void TestSelect(string code, string shortName)
        {
            SelectProduct(cat.Product(code));
            foreach (EquipPosition g in cat.Positions) if (g.Short == shortName)
            {
                object sel = null;
                foreach (object o in lbPos.Items) if (((PosItem)o).P == g) sel = o;
                if (sel != null) lbPos.SelectedItem = sel;
                SelectPos(g);
                break;
            }
        }
        public void TestAdd(string code, string name) { AddProduct(code, name); }
        public bool TestCopy(string to, string name) { ProductProfile c = prod != null ? cat.CopyProduct(prod.Code, to, name) : null; FillProducts(c); return c != null; }
        public string TestOwn(bool own, bool fix, params string[] ids)
        {
            SetOwn(own); SetFixed(fix);
            for (int i = 0; i < devs.Items.Count; i++) { DevItem d = devs.Items[i] as DevItem; if (d != null) devs.SetItemChecked(i, Array.IndexOf(ids, d.Id) >= 0); }
            return string.Join(",", cat.Allowed(prod.Code, pos.Short).ToArray());
        }
        public void TestCheckGroup(int index) { devs.SetItemChecked(index, true); }
        public void TestSwap(string name, params string[] ids) { NewSwap(); swName.Text = name; for (int i = 0; i < swMembers.Items.Count; i++) swMembers.SetSelected(i, Array.IndexOf(ids, swMembers.Items[i]) >= 0); }
        public bool TestCommit() { Commit(); return Result != null; }
        public Catalog TestCatalog { get { return cat; } }
#endif
    }
}
