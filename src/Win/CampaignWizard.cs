using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pakete > Neue Kampagne…: 1) Produkt (Paket-Produktcodes, Katalog-Produkte oder frei eintippen), 2) Kampagne oder Sonderlauf,
    // 3) Paket dieses Produkts waehlen oder neues Paket anlegen. Danach macht MainForm den Kampagnenstart (CampaignForm) bzw. oeffnet den Editor.
    class CampaignWizard : Form
    {
        public string Product = "";      // gewaehlter Produktcode
        public bool Special;             // Sonderlauf statt Kampagne
        public string PackageFile;       // gewaehltes Paket; null + NewPackage = neues Paket
        public bool NewPackage;

        readonly string folder;
        readonly Catalog cat;
        readonly ComboBox cbProduct = new ComboBox();
        readonly RadioButton rbCampaign = new RadioButton(), rbSpecial = new RadioButton();
        readonly ListView list = new ListView();
        readonly Label[] heads = new Label[3];
        readonly Label[] hints = new Label[3];
        readonly Panel[] pages = new Panel[3];
        readonly Button bBack = new Button(), bNext = new Button(), bNew = new Button();
        int page;

        public CampaignWizard(string folder, Catalog cat, string preProduct)
        {
            this.folder = folder; this.cat = cat;
            Text = "Neue Kampagne";
            Ui.Dialog(this, 560, 360);
            string[] titles = { "1  Produkt", "2  Art des Laufs", "3  Paket" };
            string[] help = { "Für welches Produkt? Aus den vorhandenen Paketen und Produkten wählen oder den Code eintippen.",
                "Eine normale Kampagne oder ein Sonderlauf (z. B. TFF Flush)?",
                "Ein vorhandenes Paket dieses Produkts nehmen oder ein neues anlegen." };
            for (int i = 0; i < 3; i++)
            {
                pages[i] = new Panel(); pages[i].SetBounds(0, 0, 560, 300);
                heads[i] = new Label(); heads[i].Text = titles[i]; heads[i].Font = new Font(Font.FontFamily, Font.Size + 3f, FontStyle.Bold); heads[i].SetBounds(14, 10, 530, 28);
                hints[i] = new Label(); hints[i].AutoSize = false; hints[i].Text = help[i]; hints[i].SetBounds(14, 42, 530, 36);
                pages[i].Controls.Add(heads[i]); pages[i].Controls.Add(hints[i]);
                Controls.Add(pages[i]);
            }
            cbProduct.DropDownStyle = ComboBoxStyle.DropDown; cbProduct.SetBounds(14, 90, 300, 26);
            foreach (string c in ProductCodes()) cbProduct.Items.Add(c);
            cbProduct.Text = preProduct ?? "";
            pages[0].Controls.Add(cbProduct);
            rbCampaign.Text = "Kampagne (Packen, Läufe, …)"; rbCampaign.Checked = true; rbCampaign.SetBounds(20, 96, 400, 24);
            rbSpecial.Text = "Sonderlauf (Sonderereignis, z. B. TFF Flush)"; rbSpecial.SetBounds(20, 126, 400, 24);
            pages[1].Controls.Add(rbCampaign); pages[1].Controls.Add(rbSpecial);
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.Columns.Add("Paket", 330); list.Columns.Add("Produkt", 80); list.Columns.Add("Geändert", 110);
            list.SetBounds(14, 84, 530, 176); list.DoubleClick += delegate { Next(); };
            bNew.Text = "Neues Paket für dieses Produkt…"; bNew.SetBounds(14, 266, 240, 28);
            bNew.Click += delegate { NewPackage = true; PackageFile = null; DialogResult = DialogResult.OK; };
            pages[2].Controls.Add(list); pages[2].Controls.Add(bNew);
            bBack.Text = "‹ Zurück"; bBack.SetBounds(160, 318, 100, 30); bBack.Click += delegate { Go(page - 1); };
            bNext.Text = "Weiter ›"; bNext.SetBounds(268, 318, 170, 30); bNext.Click += delegate { Next(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(446, 318, 100, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = bNext; CancelButton = cancel;
            Controls.AddRange(new Control[] { bBack, bNext, cancel });
            Theme.Current.Style(this);
            Go(0);
        }

        // Codes aus den Paketen des Ordners und den Produkten im Katalog (ohne Doppelte)
        List<string> ProductCodes()
        {
            List<string> r = new List<string>();
            foreach (KeyValuePair<string, string> kv in PackagePicker.Files(folder)) if (Package.IsProductCode(kv.Value) && !r.Contains(kv.Value)) r.Add(kv.Value);
            foreach (ProductProfile p in cat.Products) if (Package.IsProductCode(p.Code) && !r.Contains(p.Code)) r.Add(p.Code);
            r.Sort(StringComparer.OrdinalIgnoreCase);
            return r;
        }

        void Go(int p)
        {
            page = Math.Max(0, Math.Min(2, p));
            for (int i = 0; i < 3; i++) pages[i].Visible = i == page;
            bBack.Enabled = page > 0;
            bNext.Text = page == 2 ? "Kampagne starten ›" : "Weiter ›";
            if (page == 2) FillList();
            Text = "Neue Kampagne – " + (Product.Length > 0 ? Product : "Produkt wählen");
        }

        void Next()
        {
            if (page == 0)
            {
                string c = cbProduct.Text.Trim();
                if (!Package.IsProductCode(c))
                {
                    MessageBox.Show(this, "Bitte einen Produktcode eingeben (2–12 Zeichen: Buchstaben, Ziffern, - _ . / +).", "Neue Kampagne", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    cbProduct.Focus(); return;
                }
                Product = c; Go(1);
            }
            else if (page == 1) { Special = rbSpecial.Checked; Go(2); }
            else
            {
                if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag == null) { MessageBox.Show(this, "Bitte ein Paket wählen oder „Neues Paket für dieses Produkt…“.", "Neue Kampagne", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                PackageFile = (string)list.SelectedItems[0].Tag; NewPackage = false; DialogResult = DialogResult.OK;
            }
        }

        // Pakete des Produkts; Sonderlauf: nur Pakete mit Sonderereignis-Schritt, Kampagne: alle mit mindestens einem normalen Schritt
        void FillList()
        {
            list.Items.Clear();
            foreach (KeyValuePair<string, string> kv in PackagePicker.Files(folder))
            {
                if (kv.Value != Product) continue;
                bool anySpecial = false, anyNormal = false;
                try
                {
                    foreach (PackageStep s in Package.Load(kv.Key).Steps)
                    {
                        Baustein b = cat.Baustein_(s.Source);
                        if (b != null && b.Special) anySpecial = true; else anyNormal = true;
                    }
                }
                catch (Exception) { continue; }
                if (Special ? !anySpecial : !anyNormal) continue;
                ListViewItem it = new ListViewItem(Path.GetFileNameWithoutExtension(kv.Key));
                it.SubItems.Add(kv.Value); it.SubItems.Add(File.GetLastWriteTime(kv.Key).ToString("dd.MM.yyyy HH:mm"));
                it.Tag = kv.Key; list.Items.Add(it);
            }
            if (list.Items.Count > 0) list.Items[0].Selected = true;
            else list.Items.Add(new ListViewItem("(noch kein " + (Special ? "Sonderlauf-" : "Kampagnen-") + "Paket für " + Product + " – unten ein neues anlegen)"));
        }

#if TESTHOOKS
        // Selbsttest: Seite setzen und Eingaben vorgeben, ohne Dialog zu zeigen
        public void TestPage(int p, string product, bool special) { cbProduct.Text = product; Product = product; rbSpecial.Checked = special; rbCampaign.Checked = !special; Special = special; Go(p); }
        public List<string> TestItems() { List<string> r = new List<string>(); foreach (ListViewItem i in list.Items) if (i.Tag != null) r.Add(i.Text); return r; }
        public List<string> TestProducts() { List<string> r = new List<string>(); foreach (object o in cbProduct.Items) r.Add((string)o); return r; }
#endif
    }
}
