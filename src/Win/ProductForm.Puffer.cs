using System;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstieg "Pufferpaket…" in Produkte & Equipment (Recht PackageAdmin); der Dialog selbst steht in PufferPackForm.cs. Er arbeitet auf der
    // Arbeitskopie des Katalogs dieses Dialogs: im Pufferpaket-Dialog aufgenommene Geraete werden mit "Speichern" von Produkte & Equipment gesichert.
    partial class ProductForm
    {
        void EditPuffer()
        {
            if (prod == null) return;
            using (PufferPackForm f = new PufferPackForm(cat, prod))
            {
                bool ok = f.ShowDialog(this) == System.Windows.Forms.DialogResult.OK;
                if (ok) prod.Puffer = f.Result;
                if (ok || f.Added.Count > 0) { SelectProduct(prod); Touch(ok ? "Pufferpaket von " + prod.Code + " geändert" : f.Added.Count + " Gerät(e) in die Liste aufgenommen"); }   // aufgenommene Geraete stehen schon in der Arbeitskopie
            }
        }
    }
}
