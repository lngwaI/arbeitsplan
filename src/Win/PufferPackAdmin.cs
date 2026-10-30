using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pufferpakete verwalten (UX-5a/UX-6): vom Paket-Editor ("Pufferpakete verwalten…") und aus Einstellungen -> "Pufferpakete…" erreichbar.
    // Erst Produktauswahl (ProductPickForm, dort auch "Neues Produkt…"), dann der Pufferpaket-Dialog (PufferPackForm, PufferPackForm.cs);
    // gespeichert wird der Katalog. Mit Recht PackageAdmin aenderbar, sonst nur ansehen.
    // Was wird wann gespeichert (nur Admin):
    //  - "Speichern" im Pufferpaket-Dialog: neue Produkte + im Dialog aufgenommene Geraete + das Pufferpaket des Produkts (auch ein leeres).
    //  - "Abbrechen" im Pufferpaket-Dialog: das Pufferpaket bleibt unveraendert, aber bewusst angelegte Produkte und aufgenommene Geraete
    //    werden trotzdem gespeichert (sie wurden mit eigener Bestaetigung angelegt; sonst waere die Rueckmeldung "angelegt" falsch).
    //  - Abbrechen schon in der Produktwahl: nichts wird gespeichert.
    static class PufferPackAdmin
    {
        public const string Title = "Pufferpakete";

        // true = Katalog wurde geaendert und gespeichert. code = Produkt des Editors (Vorauswahl; ohne Profil als "neu" anbietbar).
        public static bool Manage(IWin32Window owner, IDataStore store, string code, bool admin)
        {
            Catalog cat = store.GetCatalog().Clone();
            ProductProfile prod; List<ProductProfile> created;
            using (ProductPickForm d = new ProductPickForm(cat, code, admin))
            {
                if (d.ShowDialog(owner) != DialogResult.OK || d.Result == null) return false;
                prod = d.Result; created = d.NewProducts;
            }
            List<DevAdd> adds;
            using (PufferPackForm f = new PufferPackForm(cat, prod, !admin))
            {
                bool ok = f.ShowDialog(owner) == DialogResult.OK;
                if (!admin) return false;
                adds = f.Added;
                if (ok) prod.Puffer = f.Result; else prod = null;   // Abbruch: Pufferpaket bleibt, Produkte/Geraete werden trotzdem uebernommen
            }
            if (prod == null && created.Count == 0 && adds.Count == 0) return false;
            try { store.SaveCatalog(Merged(store, prod, created, adds)); return true; }
            catch (Exception ex) { MessageBox.Show(owner, "Konnte nicht gespeichert werden.\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        }

        internal static Catalog Merged(IDataStore store, ProductProfile prod) { return Merged(store, prod, null, null); }

        // N7: Katalog unmittelbar vor dem Speichern neu laden und nur die eigenen Aenderungen einsetzen (Aenderungen anderer Admins am Katalog
        // waehrend der Dialog offen war bleiben erhalten): neu angelegte Produkte (created; ein schon vorhandenes Produkt gleichen Codes bleibt),
        // aufgenommene Geraete (adds; Stammdaten + Position) und das Pufferpaket von prod (null = unveraendert; unbekanntes Produkt wird angehaengt).
        internal static Catalog Merged(IDataStore store, ProductProfile prod, List<ProductProfile> created, List<DevAdd> adds)
        {
            Catalog fresh = store.GetCatalog().Clone();
            if (created != null) foreach (ProductProfile c in created) if (fresh.Product(c.Code) == null) fresh.Products.Add(c.Clone());
            if (adds != null) foreach (DevAdd a in adds) a.ApplyTo(fresh);
            if (prod == null) return fresh;
            ProductProfile have = fresh.Product(prod.Code);
            if (have != null) have.Puffer = prod.Puffer; else fresh.Products.Add(prod);
            return fresh;
        }
    }

    // Produktauswahl vor dem Pufferpaket-Dialog (B3): Code | Name | Pufferpaket; Doppelklick = wählen; "Neues Produkt…" (Admin) legt im Katalog-Klon an.
    class ProductPickForm : Form
    {
        readonly Catalog cat; readonly bool admin;
        readonly ListView lv = new ListView();
        readonly Button ok = new Button(), bNew = new Button(), cancel = new Button();
        readonly Label status = new Label(), empty = new Label();   // empty: Leerzustand ueber der Liste
        ProductProfile newProd;   // Produkt des Editors ohne Profil (Admin: als "neu" anbietbar)
        public readonly List<ProductProfile> NewProducts = new List<ProductProfile>();   // in diesem Dialog angelegt
        ListViewItem sel; bool any;   // markierter Eintrag und "Liste nicht leer" selbst gefuehrt (ListView/Visible sind ohne angezeigtes Fenster unzuverlaessig)
        public ProductProfile Result { get { return sel != null ? sel.Tag as ProductProfile : null; } }

        public ProductPickForm(Catalog cat, string code, bool admin)
        {
            this.cat = cat; this.admin = admin;
            Text = "Pufferpakete – Produkt wählen";
            Ui.Dialog(this, 520, 384, false);
            Label help = new Label(); help.Text = "Wähle das Produkt, dessen Pufferpaket du " + (admin ? "anlegen oder ändern" : "ansehen") + " willst.";
            help.SetBounds(10, 10, 500, 18); help.UseMnemonic = false; Controls.Add(help);
            lv.SetBounds(10, 34, 500, 264); lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false;
            lv.Columns.Add("Code", 90); lv.Columns.Add("Name", 200); lv.Columns.Add("Pufferpaket", 186); Controls.Add(lv);
            empty.Text = admin ? "Noch keine Produkte angelegt." : "Noch keine Produkte angelegt. Der Admin legt Produkte an.";
            empty.SetBounds(10, 59, 500, 239); empty.ForeColor = SystemColors.GrayText; empty.BackColor = SystemColors.Window; empty.BorderStyle = BorderStyle.FixedSingle;
            empty.Padding = new Padding(8, 8, 8, 0); empty.UseMnemonic = false; Controls.Add(empty);
            status.SetBounds(10, 304, 500, 34); status.ForeColor = SystemColors.GrayText; status.UseMnemonic = false; Controls.Add(status);
            bNew.Text = "Neues Produkt…"; bNew.SetBounds(10, 346, 130, 28); bNew.Visible = admin; bNew.Click += delegate { NewProduct(); }; Controls.Add(bNew);
            ok.Text = admin ? "Pufferpaket öffnen" : "Ansehen"; ok.SetBounds(262, 346, 140, 28); ok.DialogResult = DialogResult.OK; Controls.Add(ok);
            cancel.Text = "Abbrechen"; cancel.SetBounds(408, 346, 102, 28); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            CancelButton = cancel;
            if (admin && Package.IsProductCode(code ?? "") && cat.Product(code) == null) { newProd = new ProductProfile(); newProd.Code = code; }
            lv.DoubleClick += delegate { if (Result != null) DialogResult = DialogResult.OK; };
            lv.SelectedIndexChanged += delegate { sel = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0] : null; ok.Enabled = Result != null; };
            Fill(code);
        }

        void Fill(string select)
        {
            lv.Items.Clear();
            if (newProd != null && cat.Product(newProd.Code) == null) lv.Items.Add(Row(newProd, true));
            foreach (ProductProfile p in cat.Products) lv.Items.Add(Row(p, false));
            any = lv.Items.Count > 0;
            empty.Visible = !any; sel = null;
            lv.Height = any ? 264 : 25;   // leer: nur die Kopfzeile, darunter die Erklaerung (kein Ueberlagern des ListView)
            if (any)
            {
                foreach (ListViewItem i in lv.Items) if (((ProductProfile)i.Tag).Code == select) sel = i;
                if (sel == null) sel = lv.Items[0];
                sel.Selected = true;
            }
            ok.Visible = any; ok.Enabled = Result != null;
            cancel.Text = any || admin ? "Abbrechen" : "Schließen";
            AcceptButton = any ? ok : (admin ? bNew : null);
        }

        static ListViewItem Row(ProductProfile p, bool isNew)
        {
            ListViewItem i = new ListViewItem(p.Code); i.Tag = p;
            i.SubItems.Add(p.Name);
            i.SubItems.Add(isNew ? "neu, noch ohne Profil" : p.Puffer.Count == 0 ? "– Standardpuffer gilt" : p.Puffer.Count + " Puffer");
            return i;
        }

        void NewProduct()
        {
            string c = "", n = "";
            if (!ProductAsk.Ask(this, cat, "Neues Produkt", ref c, ref n, null)) return;
            ProductProfile p = new ProductProfile(); p.Code = c; p.Name = n;
            cat.Products.Add(p); NewProducts.Add(p);
            Fill(c);
            status.Text = "Produkt " + c + " angelegt – Geräte je Position später unter „Produkte & Equipment“.";
            lv.Focus();
        }

#if TESTHOOKS
        public int TestCount { get { return lv.Items.Count; } }
        public string TestText(int i) { ListViewItem it = lv.Items[i]; return it.Text + " | " + it.SubItems[1].Text + " | " + (it.SubItems.Count > 2 ? it.SubItems[2].Text : ""); }
        public string TestSelected { get { return Result != null ? Result.Code : ""; } }
        public string TestStatus { get { return status.Text; } }
        public string TestEmptyText { get { return any ? "" : empty.Text; } }
        public bool TestOkVisible { get { return any; } }
        public Button TestAccept { get { return AcceptButton as Button; } }
        public bool TestNew(string code, string name) { ProductProfile p = new ProductProfile(); p.Code = code; p.Name = name; cat.Products.Add(p); NewProducts.Add(p); Fill(code); return Result == p; }
#endif
    }
}
