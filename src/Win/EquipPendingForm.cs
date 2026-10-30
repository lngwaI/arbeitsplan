using System;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Vorgemerkte Geraete (N5): Nutzer ohne Recht haben Nummern gemeldet, die nicht in der Equipment-Liste stehen. Der Admin nimmt sie auf
    // (Geraeteart aus dem Schema geraten, in die Position der Zeile) oder verwirft sie. Arbeitet auf dem Katalog des Stammdaten-Dialogs;
    // gespeichert wird mit "Speichern" dort.
    class EquipPendingForm : Form
    {
        readonly Catalog cat;
        readonly ListBox lb = new ListBox();
        public int Changes;   // aufgenommen + verworfen

        public EquipPendingForm(Catalog cat)
        {
            this.cat = cat;
            Text = "Vorgemerkte Geräte";
            Ui.Dialog(this, 580, 340);
            Label l0 = new Label(); l0.Text = "Diese Nummern stehen in einem Paket, sind aber nicht in der Equipment-Liste. Übernehmen legt das Gerät an (Geräteart aus dem Schema geraten), Verwerfen löscht die Vormerkung."; l0.SetBounds(12, 8, 556, 44);
            lb.SetBounds(12, 56, 556, 220); lb.IntegralHeight = false;
            Button acc = new Button(); acc.Text = "Übernehmen"; acc.SetBounds(12, 290, 110, 30); acc.Click += delegate { Accept(lb.SelectedIndex); };
            Button dis = new Button(); dis.Text = "Verwerfen"; dis.SetBounds(128, 290, 100, 30); dis.Click += delegate { Discard(lb.SelectedIndex); };
            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(468, 290, 100, 30); close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            Controls.AddRange(new Control[] { l0, lb, acc, dis, close });
            Fill();
        }

        public static string Describe(EquipPending p)
        {
            return p.Id + "    Position " + (p.Short.Length > 0 ? p.Short : "–") + "    von " + (p.By.Length > 0 ? p.By : "?") + ", " + (p.At == DateTime.MinValue ? "" : p.At.ToString("dd.MM.yyyy HH:mm"));
        }

        void Fill()
        {
            int sel = lb.SelectedIndex;
            lb.Items.Clear();
            foreach (EquipPending p in cat.Master.Pending) lb.Items.Add(Describe(p));
            if (lb.Items.Count > 0) lb.SelectedIndex = Math.Max(0, Math.Min(sel, lb.Items.Count - 1));
        }

        internal void Accept(int i)
        {
            if (i < 0 || i >= cat.Master.Pending.Count) return;
            EquipPending p = cat.Master.Pending[i];
            EquipPosition pos = cat.Position(p.Short);
            if (cat.Master.Device(p.Id) == null) cat.Master.Devices.Add(cat.Master.NewDevice(p.Id, pos));
            if (pos != null && !pos.Ids.Contains(p.Id)) pos.Ids.Add(p.Id);
            cat.Master.Pending.RemoveAt(i); Changes++; Fill();
        }

        internal void Discard(int i)
        {
            if (i < 0 || i >= cat.Master.Pending.Count) return;
            cat.Master.Pending.RemoveAt(i); Changes++; Fill();
        }
    }
}
