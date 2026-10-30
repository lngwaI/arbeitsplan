using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pufferpakete verwalten (UX-5a): vom Paket-Editor ("Pufferpakete verwalten…") und aus Einstellungen -> "Pufferpakete…" erreichbar.
    // Erst Produktauswahl (ProductPickForm), dann der bestehende Pufferpaket-Dialog (PufferPackForm, ProductForm.Puffer.cs); gespeichert wird der
    // Katalog. Mit Recht PackageAdmin aenderbar, sonst nur ansehen. Der Zugang im Produktdialog bleibt zusaetzlich.
    static class PufferPackAdmin
    {
        public const string Title = "Pufferpakete";

        // true = Katalog wurde geaendert und gespeichert. code = Produkt des Editors (Vorauswahl; ohne Profil als "neu" anbietbar).
        public static bool Manage(IWin32Window owner, IDataStore store, string code, bool admin)
        {
            Catalog cat = store.GetCatalog().Clone();
            if (cat.Products.Count == 0 && !(admin && Package.IsProductCode(code ?? "")))
            {
                MessageBox.Show(owner, "Es sind noch keine Produkte angelegt." + (admin ? "\nProdukte legt man unter Einstellungen → „Produkte & Equipment“ an." : "\nDer Admin legt Produkte und Pufferpakete an."), Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            ProductProfile prod;
            using (ProductPickForm d = new ProductPickForm(cat, code, admin))
            {
                if (d.ShowDialog(owner) != DialogResult.OK || d.Result == null) return false;
                prod = d.Result;
            }
            using (PufferPackForm f = new PufferPackForm(cat, prod, !admin))
            {
                if (f.ShowDialog(owner) != DialogResult.OK || !admin) return false;
                prod.Puffer = f.Result;
                if (!cat.Products.Contains(prod)) cat.Products.Add(prod);
            }
            try { store.SaveCatalog(cat); return true; }
            catch (Exception ex) { MessageBox.Show(owner, "Konnte nicht gespeichert werden.\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        }
    }

    // Produktauswahl vor dem Pufferpaket-Dialog: Liste der Produkte mit Anzahl Puffer; Doppelklick = wählen
    class ProductPickForm : Form
    {
        class Item
        {
            public ProductProfile P; public bool IsNew;
            public override string ToString() { return P.Code + (P.Name.Length > 0 ? " · " + P.Name : "") + (IsNew ? "  (neu, noch ohne Profil)" : "  –  " + (P.Puffer.Count == 0 ? "kein Pufferpaket (Standardpuffer gilt)" : P.Puffer.Count + " Puffer")); }
        }

        readonly ListBox lb = new ListBox();
        readonly Button ok = new Button();
        public ProductProfile Result { get { Item i = lb.SelectedItem as Item; return i != null ? i.P : null; } }

        public ProductPickForm(Catalog cat, string code, bool admin)
        {
            Text = "Pufferpakete – Produkt wählen";
            Ui.Dialog(this, 460, 330, false);
            Label help = new Label(); help.Text = "Für welches Produkt soll das Pufferpaket " + (admin ? "angelegt oder geändert" : "angesehen") + " werden? Weitere Produkte: Einstellungen → Produkte & Equipment.";
            help.SetBounds(10, 8, 440, 2 * Font.Height + 2); help.ForeColor = SystemColors.GrayText; help.UseMnemonic = false; Controls.Add(help);
            lb.SetBounds(10, 12 + help.Height, 440, 330 - 62 - help.Height); lb.IntegralHeight = false; Controls.Add(lb);
            bool known = false;
            foreach (ProductProfile p in cat.Products) { Item i = new Item(); i.P = p; lb.Items.Add(i); if (p.Code == code) { lb.SelectedItem = i; known = true; } }
            if (!known && admin && Package.IsProductCode(code ?? ""))
            {
                ProductProfile np = new ProductProfile(); np.Code = code; Item i = new Item(); i.P = np; i.IsNew = true; lb.Items.Insert(0, i); lb.SelectedIndex = 0;
            }
            if (lb.SelectedIndex < 0 && lb.Items.Count > 0) lb.SelectedIndex = 0;
            ok.Text = admin ? "Pufferpaket öffnen" : "Ansehen"; ok.SetBounds(240, 330 - 40, 120, 28); ok.DialogResult = DialogResult.OK; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(368, 330 - 40, 82, 28); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            lb.DoubleClick += delegate { if (lb.SelectedItem != null) DialogResult = DialogResult.OK; };
            lb.SelectedIndexChanged += delegate { ok.Enabled = lb.SelectedItem != null; };
            ok.Enabled = lb.SelectedItem != null;
        }

#if TESTHOOKS
        public int TestCount { get { return lb.Items.Count; } }
        public string TestText(int i) { return lb.Items[i].ToString(); }
        public string TestSelected { get { return Result != null ? Result.Code : ""; } }
#endif
    }
}
