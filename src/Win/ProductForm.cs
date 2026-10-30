using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
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
        Font boldFont;

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
            lv.DoubleClick += delegate { EditSelected(); };
            lv.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EditSelected(); } };
            Controls.Add(lv);
            bNew = Btn("Neu…", 8, 570, 98, delegate { NewProduct(); });
            bCopy = Btn("Kopieren…", 111, 570, 98, delegate { CopySelected(); });
            bEdit = Btn("Bearbeiten…", 214, 570, 98, delegate { EditSelected(); });
            bDel = Btn("Entfernen", 8, 600, 98, delegate { RemoveSelected(); });
            bPuf = Btn("Pufferpaket…", 111, 600, 201, delegate { EditPuffer(); });   // Puffer des Produkts (ProductForm.Puffer.cs)
            Controls.AddRange(new Control[] { bNew, bCopy, bEdit, bDel, bPuf });

            head.SetBounds(322, 30, 670, 20); head.AutoEllipsis = true; head.UseMnemonic = false; boldFont = new Font(Font, FontStyle.Bold); head.Font = boldFont; Controls.Add(head);
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
        void Warn(string text) { Msg(text, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1); }
        static readonly Color Orange = Color.FromArgb(192, 96, 0);

        // Statuszeile: letzte Aktion + Zaehler der ungespeicherten Aenderungen
        string ChangeText() { return changes == 1 ? "1 Änderung" : changes + " Änderungen"; }
        void UpdateStatus() { status.Text = (lastAction.Length > 0 ? lastAction + "    ·    " : "") + (changes > 0 ? "Nicht gespeichert: " + ChangeText() : "Keine Änderungen"); }
        void Touch(string action) { changes++; lastAction = action; UpdateStatus(); }

        // Rueckfrage bei ungespeicherten Aenderungen auf jedem Weg ausser Beenden von Windows/Programm und erfolgreichem Speichern
        // (Abbrechen-Knopf und Esc liefern CloseReason.None).
        bool NeedsAsk(CloseReason r)
        {
            return !noAsk && changes > 0 && DialogResult != DialogResult.OK
                && r != CloseReason.WindowsShutDown && r != CloseReason.TaskManagerClosing && r != CloseReason.ApplicationExitCall;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel && NeedsAsk(e.CloseReason)
                && Msg("Änderungen verwerfen?\n\nNicht gespeichert: " + ChangeText() + ".", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true;
            base.OnFormClosing(e);
        }

        // Rueckfrage/Meldung; im Testbuild mit TestAnswer ohne Fenster
        DialogResult Msg(string text, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d)
        {
#if TESTHOOKS
            if (TestAnswer != DialogResult.None) { TestMsgs += text + "\n"; return TestAnswer; }
#endif
            return MessageBox.Show(this, text, "Produkte & Equipment", b, i, d);
        }

        // Katalog als Text (zum Erkennen echter Aenderungen: nur zaehlen, wenn sich etwas geaendert hat)
        static string Sig(Catalog c)
        {
            StoreData d = new StoreData(); d.Catalog = c;
            string x = Encoding.UTF8.GetString(Backup.ToXml(d));
            return x.Substring(Math.Max(0, x.IndexOf("<Zeilen")));   // ohne Kopfzeile mit Zeitstempel
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { swTip.Dispose(); if (boldFont != null) boldFont.Dispose(); }   // boldFont ist selbst erzeugt (nicht aus FontCache)
        }

        static string PufText(ProductProfile p) { return p.Puffer.Count > 0 ? p.Puffer.Count.ToString() : "–"; }

        void FillProducts(ProductProfile select)
        {
            loading = true;
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (ProductProfile p in cat.Products)
            {
                ListViewItem it = new ListViewItem(new[] { p.Code, p.Name, PufText(p) }); it.Tag = p; lv.Items.Add(it);
                if (p == select) { it.Selected = true; it.EnsureVisible(); }
            }
            lv.EndUpdate();
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
            if (codeChanged && Msg("Bestehende Pakete und der Plan verwenden weiter den Code " + old + "; für sie gilt dann nicht mehr dieses Profil.\n\nCode trotzdem ändern?",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            prod.Code = code; prod.Name = name;
            SelectProduct(prod);
            Touch(codeChanged ? "Code " + old + " zu " + code + " geändert. Bestehende Pakete und der Plan behalten den alten Code." : "Name von " + code + " geändert.");
        }

        void RemoveSelected()
        {
            if (prod == null) return;
            int own = prod.Positions.FindAll(delegate(ProfilePos x) { return x.Own; }).Count;
            List<string> lost = new List<string>();
            if (prod.Puffer.Count > 0) lost.Add("Pufferpaket mit " + (prod.Puffer.Count == 1 ? "1 Puffer" : prod.Puffer.Count + " Puffern"));
            if (prod.Swaps.Count > 0) lost.Add(prod.Swaps.Count == 1 ? "1 Austauschgruppe" : prod.Swaps.Count + " Austauschgruppen");
            if (own > 0) lost.Add(own == 1 ? "1 eigene Liste" : own + " eigene Listen");
            if (Msg("Produkt " + prod + " wirklich entfernen?\n" + (lost.Count > 0 ? "Damit gehen verloren: " + string.Join(", ", lost.ToArray()) + ".\n" : "")
                + "Im Plan ändert sich nichts; für das Produkt gilt danach wieder der Standard.", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string code = prod.Code;
            int i = cat.Products.IndexOf(prod); cat.Products.Remove(prod);
            FillProducts(cat.Products.Count > 0 ? cat.Products[Math.Min(i, cat.Products.Count - 1)] : null);
            Touch("Produkt " + code + " entfernt.");
            lv.Focus();
        }

        // Erst alle Produkte pruefen, dann leere Austauschgruppen entfernen und melden
        void Commit()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (ProductProfile p in cat.Products)
            {
                if (!Package.IsProductCode(p.Code)) { Warn("Produktcode \"" + p.Code + "\" ist ungültig: " + ProductAsk.CodeRule + "."); return; }
                if (!seen.Add(p.Code)) { Warn("Produkt " + p.Code + " ist doppelt vorhanden."); return; }
            }
            List<string> dropped = new List<string>();
            foreach (ProductProfile p in cat.Products)
            {
                foreach (SwapGroup g in p.Swaps) if (g.Ids.Count == 0) dropped.Add("Austauschgruppe „" + g.Name + "“ (" + p.Code + ") ohne Geräte wurde nicht gespeichert.");
                p.Swaps.RemoveAll(delegate(SwapGroup g) { return g.Ids.Count == 0; });
            }
            if (dropped.Count > 0) Msg(string.Join("\n", dropped.ToArray()), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            Result = cat;
            DialogResult = DialogResult.OK;
        }
    }
}
