using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen -> Produkte & Equipment (Recht PackageAdmin): je Produkt (Code frei, z. B. AB1) und Equipment-Position die erlaubten Geraete
    // (Standard oder eigene Liste), fest/frei und Austauschgruppen. Bearbeitet eine Katalog-Kopie; gespeichert wird der ganze Katalog.
    // Links Produktliste, rechts Reiter "Geraete je Position" (Pos.cs, Groups.cs) und "Austauschgruppen" (Swaps.cs); unten Statuszeile.
    partial class ProductForm : Form
    {
        readonly Catalog cat;
        readonly List<string> rowNames;   // Zeilennamen des Plans (fuer "Standardliste bearbeiten…"); null = Link ausgeblendet
        readonly string[] variants;
        public Catalog Result;
        bool loading, noAsk;
        ProductProfile prod;
        int changes;
        string lastAction = "";

        readonly ListView lv = new ListView();
        readonly Label head = new Label(), status = new Label();
        readonly TabControl tabs = new TabControl();
        readonly TabPage tabPos = new TabPage("Geräte je Position"), tabSwap = new TabPage("Austauschgruppen (0)");
        Button bNew, bCopy, bEdit, bDel, bPuf;

        public ProductForm(Catalog catalog, List<string> rowNames = null, string[] variants = null)
        {
            cat = catalog.Clone(); this.rowNames = rowNames; this.variants = variants;
            Text = "Produkte & Equipment";
            Ui.Dialog(this, 1000, 680);
            Label intro = Ui.Hint("Je Produkt legt man fest, welche Geräte an jeder Position erlaubt sind. Ohne eigene Liste gilt die Standardliste aus „Equipment & Bausteine“.", 8, 6, 984); intro.UseMnemonic = false; Controls.Add(intro);

            Controls.Add(L("Produkte:", 8, 30));
            lv.SetBounds(8, 48, 304, 516); lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.Columns.Add("Code", 78); lv.Columns.Add("Name", 140); lv.Columns.Add("Puffer", 58);
            lv.SelectedIndexChanged += delegate { if (!loading && lv.SelectedItems.Count > 0) SelectProduct(lv.SelectedItems[0].Tag as ProductProfile); };
            Controls.Add(lv);
            bNew = Btn("Neu…", 8, 570, 98, delegate { NewProduct(); });
            bCopy = Btn("Kopieren…", 111, 570, 98, delegate { CopySelected(); });
            bEdit = Btn("Bearbeiten…", 214, 570, 98, delegate { EditSelected(); });
            bDel = Btn("Entfernen", 8, 600, 98, delegate { RemoveSelected(); });
            bPuf = Btn("Pufferpaket…", 111, 600, 201, delegate { EditPuffer(); });   // Puffer des Produkts (ProductForm.Puffer.cs)
            Controls.AddRange(new Control[] { bNew, bCopy, bEdit, bDel, bPuf });

            head.SetBounds(322, 30, 670, 20); head.AutoEllipsis = true; head.UseMnemonic = false; head.Font = new Font(Font, FontStyle.Bold); Controls.Add(head);
            tabs.SetBounds(322, 54, 670, 582); tabs.TabPages.Add(tabPos); tabs.TabPages.Add(tabSwap); Controls.Add(tabs);
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedTab == tabSwap) LoadSwap(); };
            BuildPos(); BuildSwaps();

            status.SetBounds(8, 648, 776, 20); status.AutoEllipsis = true; status.UseMnemonic = false; Controls.Add(status);
            Button ok = Btn("Speichern", 794, 642, 96, delegate { Commit(); }); ok.Height = 30;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(896, 642, 96, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { ok, cancel });
            UpdateStatus();
            FillProducts(cat.Products.Count > 0 ? cat.Products[0] : null);
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.UseMnemonic = false; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }
        void Warn(string text) { MessageBox.Show(this, text, "Produkte & Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        static readonly Color Orange = Color.FromArgb(192, 96, 0);

        // Statuszeile: letzte Aktion + Zaehler der ungespeicherten Aenderungen
        string ChangeText() { return changes == 1 ? "1 Änderung" : changes + " Änderungen"; }
        void UpdateStatus() { status.Text = (lastAction.Length > 0 ? lastAction + "    ·    " : "") + (changes > 0 ? "Nicht gespeichert: " + ChangeText() : "Keine Änderungen"); }
        void Touch(string action) { changes++; lastAction = action; UpdateStatus(); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel && !noAsk && changes > 0 && DialogResult != DialogResult.OK && e.CloseReason == CloseReason.UserClosing
                && MessageBox.Show(this, "Änderungen verwerfen?\n\nNicht gespeichert: " + ChangeText() + ".", "Produkte & Equipment", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true;
            base.OnFormClosing(e);
        }

        static string PufText(ProductProfile p) { return p.Puffer.Count > 0 ? p.Puffer.Count.ToString() : "–"; }

        void FillProducts(ProductProfile select)
        {
            loading = true;
            lv.Items.Clear();
            foreach (ProductProfile p in cat.Products)
            {
                ListViewItem it = new ListViewItem(new[] { p.Code, p.Name, PufText(p) }); it.Tag = p; lv.Items.Add(it);
                if (p == select) { it.Selected = true; it.EnsureVisible(); }
            }
            loading = false;
            SelectProduct(select);
        }

        void RefreshProductRow()
        {
            foreach (ListViewItem it in lv.Items)
                if (it.Tag == prod) { it.SubItems[0].Text = prod.Code; it.SubItems[1].Text = prod.Name; it.SubItems[2].Text = PufText(prod); }
        }

        void SelectProduct(ProductProfile p)
        {
            prod = p;
            bool any = p != null;
            head.Text = any ? p.Code + (p.Name.Length > 0 ? " · " + p.Name : "") : "Kein Produkt gewählt – links „Neu…“ anlegen";
            tabs.Enabled = bCopy.Enabled = bEdit.Enabled = bDel.Enabled = bPuf.Enabled = any;
            bPuf.Text = "Pufferpaket" + (any && p.Puffer.Count > 0 ? " (" + p.Puffer.Count + ")" : "") + "…";
            RefreshProductRow();
            FillPos();
            FillSwaps(null);
        }

        void NewProduct()
        {
            string code = "", name = "";
            if (ProductAsk.Ask(this, cat, "Neues Produkt", ref code, ref name, null)) AddProduct(code, name);
        }

        void AddProduct(string code, string name)
        {
            ProductProfile p = new ProductProfile(); p.Code = code; p.Name = name; cat.Products.Add(p);
            FillProducts(p);
            Touch("Produkt " + code + " angelegt.");
        }

        void CopySelected()
        {
            if (prod == null) return;
            string code = "", name = prod.Name, from = prod.Code;
            if (ProductAsk.Ask(this, cat, "Wie Produkt " + from + " kopieren", ref code, ref name, null, "Kopieren")) { FillProducts(cat.CopyProduct(from, code, name)); Touch("Produkt " + code + " als Kopie von " + from + " angelegt."); }
        }

        void EditSelected()
        {
            if (prod == null) return;
            string code = prod.Code, name = prod.Name;
            if (ProductAsk.Ask(this, cat, "Produkt " + prod.Code + " bearbeiten", ref code, ref name, prod.Code, "Übernehmen")) ApplyEdit(code, name);
        }

        void ApplyEdit(string code, string name)
        {
            bool codeChanged = code != prod.Code, nameChanged = name != prod.Name;
            if (!codeChanged && !nameChanged) return;
            string old = prod.Code;
            prod.Code = code; prod.Name = name;
            SelectProduct(prod);
            Touch(codeChanged ? "Code " + old + " zu " + code + " geändert. Bestehende Pakete und der Plan behalten den alten Code." : "Name von " + code + " geändert.");
        }

        void RemoveSelected()
        {
            if (prod == null) return;
            if (MessageBox.Show(this, "Produkt " + prod + " wirklich entfernen?\nIm Plan ändert sich nichts; für das Produkt gilt danach wieder der Standard.",
                "Produkte & Equipment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string code = prod.Code;
            int i = cat.Products.IndexOf(prod); cat.Products.Remove(prod);
            FillProducts(cat.Products.Count > 0 ? cat.Products[Math.Min(i, cat.Products.Count - 1)] : null);
            Touch("Produkt " + code + " entfernt.");
        }

        void Commit()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (ProductProfile p in cat.Products)
            {
                if (!Package.IsProductCode(p.Code)) { Warn("Produktcode \"" + p.Code + "\" ist ungültig: " + ProductAsk.CodeRule + "."); return; }
                if (!seen.Add(p.Code)) { Warn("Produkt " + p.Code + " ist doppelt vorhanden."); return; }
                p.Swaps.RemoveAll(delegate(SwapGroup g) { return g.Ids.Count == 0; });
            }
            Result = cat;
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        public void TestSelect(string code, string shortName)
        {
            FillProducts(cat.Product(code));
            foreach (object o in lbPos.Items) { PosItem pi = (PosItem)o; if (!pi.IsGroup && pi.Ps[0].Short == shortName) { lbPos.SelectedItem = pi; SelectPos(pi); break; } }
        }
        public void TestSelectGroup(string name) { foreach (object o in lbPos.Items) { PosItem pi = (PosItem)o; if (pi.IsGroup && pi.GroupName == name) { lbPos.SelectedItem = pi; SelectPos(pi); break; } } }
        public void TestAdd(string code, string name) { AddProduct(code, name); }
        public bool TestCopy(string to, string name) { ProductProfile c = prod != null ? cat.CopyProduct(prod.Code, to, name) : null; FillProducts(c); return c != null; }
        public bool TestEdit(string code, string name) { if (prod == null || ProductAsk.Check(cat, code, prod.Code).Length > 0) return false; ApplyEdit(code, name); return true; }
        public string TestOwn(bool own, bool fix, params string[] ids)
        {
            SetOwn(own); SetFixed(fix);
            for (int i = 0; i < devs.Items.Count; i++) { DevItem d = devs.Items[i] as DevItem; if (d != null) devs.SetItemChecked(i, Array.IndexOf(ids, d.Id) >= 0); }
            return string.Join(",", cat.Allowed(prod.Code, pos.Short).ToArray());
        }
        public void TestCheckGroup(int index) { devs.SetItemChecked(index, true); }
        public void TestSwap(string name, params string[] ids) { NewSwap(); swName.Text = name; for (int i = 0; i < swMembers.Items.Count; i++) swMembers.SetItemChecked(i, Array.IndexOf(ids, swMembers.Items[i]) >= 0); }
        public bool TestCommit() { Commit(); return Result != null; }
        public Catalog TestCatalog { get { return cat; } }
        public string TestPufButton { get { return bPuf.Text + "|" + bPuf.Enabled; } }
        public int TestChanges { get { return changes; } }
        public string TestStatus { get { return status.Text; } }
        public string TestHead { get { return head.Text; } }
        public void TestShowPos() { tabs.SelectedTab = tabPos; }
        public void TestNoAsk() { noAsk = true; }
        public bool TestNeedsAsk { get { return changes > 0 && DialogResult != DialogResult.OK; } }
        public string TestPosList { get { List<string> r = new List<string>(); foreach (object o in lbPos.Items) r.Add(o.ToString()); return string.Join("\n", r.ToArray()); } }
        public string TestPosInfo { get { return posInfo.Text; } }
        public bool TestUnifyVisible { get { return unify.Visible; } }
        public void TestUnify() { Unify(); }
        public void TestApplyStandard(Catalog src) { ApplyStandard(src); }
        public string TestProductRow(int i) { ListViewItem it = lv.Items[i]; return it.SubItems[0].Text + "|" + it.SubItems[1].Text + "|" + it.SubItems[2].Text; }
#endif
    }
}
