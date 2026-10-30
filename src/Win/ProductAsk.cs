using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Gemeinsamer Dialog "Produktcode + Name" (UX-6): neues Produkt in der Produktwahl der Pufferpakete; spaeter auch Produkte & Equipment.
    // Prueft Regel (Package.IsProductCode) und doppelten Code; Fehler stehen im Dialog, er bleibt offen.
    static class ProductAsk
    {
        public const string CodeRule = "2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (ohne Leerzeichen), z. B. PRD-1";

        // Leer = in Ordnung, sonst die Fehlermeldung. keepCode = beim Bearbeiten erlaubter eigener Code (sonst null).
        public static string Check(Catalog cat, string code, string keepCode)
        {
            if (!Package.IsProductCode(code)) return "Der Produktcode ist ungültig (Regel siehe oben).";
            if (code != keepCode && cat.Product(code) != null) return "Produkt " + code + " gibt es schon.";
            return "";
        }

        // false = abgebrochen. code/name = Vorbelegung und Ergebnis.
        public static bool Ask(IWin32Window owner, Catalog cat, string title, ref string code, ref string name, string keepCode, string okText = "Anlegen")
        {
            using (Form f = new Form())
            {
                f.Text = title; Ui.Dialog(f, 400, 224, false);
                Label l1 = new Label(); l1.Text = "Produktcode:"; l1.AutoSize = true; l1.UseMnemonic = false; l1.Location = new Point(10, 10);
                TextBox c = new TextBox(); c.Text = code; c.MaxLength = 12; c.SetBounds(10, 30, 150, 22); Ui.SimpleMenu(c);
                Label h1 = new Label(); h1.Text = CodeRule; h1.ForeColor = SystemColors.GrayText; h1.UseMnemonic = false; h1.SetBounds(10, 56, 380, 34);
                Label l2 = new Label(); l2.Text = "Name (frei, z. B. Produktbezeichnung):"; l2.AutoSize = true; l2.UseMnemonic = false; l2.Location = new Point(10, 98);
                TextBox n = new TextBox(); n.Text = name; n.SetBounds(10, 118, 380, 22); Ui.SimpleMenu(n);
                Label err = new Label(); err.ForeColor = Color.Firebrick; err.UseMnemonic = false; err.SetBounds(10, 144, 380, 34);
                Button ok = new Button(); ok.Text = okText; ok.SetBounds(194, 186, 96, 28);
                Button ca = new Button(); ca.Text = "Abbrechen"; ca.SetBounds(296, 186, 94, 28); ca.DialogResult = DialogResult.Cancel;
                ok.Click += delegate
                {
                    string m = Check(cat, c.Text.Trim(), keepCode);
                    if (m.Length > 0) { err.Text = m; c.Focus(); return; }
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { l1, c, h1, l2, n, err, ok, ca });
                f.AcceptButton = ok; f.CancelButton = ca;
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                code = c.Text.Trim(); name = n.Text.Trim();
                return true;
            }
        }
    }
}
