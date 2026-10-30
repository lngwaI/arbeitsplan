using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Reihum-Verteilung: Geraete ankreuzen, die sich abwechseln. Angeboten werden alle Geraete, die laut Admin-Hinterlegung
    // (Austausch-Gruppen) gleich eingesetzt werden koennen; eigene Eintraege sind moeglich.
    class AlternativesPicker : Form
    {
        readonly CheckedListBox list = new CheckedListBox();
        readonly TextBox own = new TextBox();
        readonly List<EquipChoice> offered;
        readonly string ownShort;
        public List<EquipChoice> Result = new List<EquipChoice>();

        public AlternativesPicker(List<EquipChoice> offered, List<EquipChoice> current, string ownShort)
        {
            this.offered = offered; this.ownShort = ownShort;
            Text = "Abwechselnd verwenden";
            Ui.Dialog(this, 360, 400);
            Label l1 = new Label(); l1.Text = "Geräte ankreuzen, die sich abwechseln (A, B, A, B …). Angeboten werden alle Geräte, die laut Admin gleich eingesetzt werden können:"; l1.SetBounds(10, 8, 340, 48);
            list.SetBounds(10, 60, 340, 220); list.CheckOnClick = true;
            List<string> extras = new List<string>();
            foreach (EquipChoice c in offered) list.Items.Add(c.ToString(), current.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; }));
            foreach (EquipChoice c in current) if (!offered.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; })) extras.Add(c.Id);
            Label l2 = new Label(); l2.Text = "Weitere (Komma getrennt):"; l2.SetBounds(10, 290, 340, 18);
            own.SetBounds(10, 310, 340, 22); own.Text = string.Join(", ", extras.ToArray()); Ui.SimpleMenu(own);
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(150, 356, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(255, 356, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, own, ok, cancel });
        }

        void Commit()
        {
            Result = new List<EquipChoice>();
            for (int i = 0; i < offered.Count; i++) if (list.GetItemChecked(i)) Result.Add(offered[i].Clone());
            foreach (string t in own.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string v = t.Trim();
                if (v.Length > 0 && !Result.Exists(delegate(EquipChoice x) { return x.Id == v; })) Result.Add(new EquipChoice(ownShort, v));
            }
            DialogResult = DialogResult.OK;
        }
    }
}
