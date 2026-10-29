using System;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kleiner Dialog: nicht validierte Nummer als Geraet in die Equipment-Liste (Stammdaten) aufnehmen. Nur mit Recht PackageAdmin angeboten.
    class EquipAddForm : Form
    {
        readonly TextBox tId = new TextBox(), tNote = new TextBox();
        readonly ComboBox cbClass = new ComboBox();
        readonly Catalog cat;
        public EquipDevice Device;   // Ergebnis bei OK

        public EquipAddForm(string id, Catalog cat)
        {
            this.cat = cat;
            Text = "Zur Equipment-Liste hinzufügen";
            Ui.Dialog(this, 380, 250);
            Label l0 = new Label(); l0.Text = "Die Nummer ist noch nicht in der Equipment-Liste. Nach dem Hinzufügen gilt sie als validiert."; l0.SetBounds(12, 10, 356, 34);
            Label l1 = new Label(); l1.Text = "Nummer (ID)"; l1.SetBounds(12, 52, 110, 20);
            tId.SetBounds(126, 49, 242, 22); tId.Text = id;
            Label l2 = new Label(); l2.Text = "Geräteart"; l2.SetBounds(12, 84, 110, 20);
            cbClass.SetBounds(126, 81, 242, 22); cbClass.DropDownStyle = ComboBoxStyle.DropDownList;
            cbClass.Items.Add("(keine)");
            foreach (EquipClass c in cat.Master.Classes) cbClass.Items.Add(c.Name);
            cbClass.SelectedIndex = 0;
            Label l3 = new Label(); l3.Text = "Notiz"; l3.SetBounds(12, 116, 110, 20);
            tNote.SetBounds(126, 113, 242, 22); Ui.SimpleMenu(tNote);
            Button ok = new Button(); ok.Text = "Hinzufügen"; ok.SetBounds(172, 200, 96, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(274, 200, 94, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l0, l1, tId, l2, cbClass, l3, tNote, ok, cancel });
        }

        void Commit()
        {
            string id = tId.Text.Trim();
            if (id.Length == 0) { MessageBox.Show(this, "Bitte eine Nummer eingeben.", "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (cat.Master.Device(id) != null) { MessageBox.Show(this, "„" + id + "“ ist schon in der Liste.", "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            EquipDevice d = new EquipDevice();
            d.Id = id; d.Class = cbClass.SelectedIndex > 0 ? cbClass.SelectedItem.ToString() : ""; d.Note = tNote.Text.Trim();
            Device = d; DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        internal void TestFill(string cls, string note) { int i = cbClass.Items.IndexOf(cls); if (i >= 0) cbClass.SelectedIndex = i; tNote.Text = note; }
        internal void TestOk() { Commit(); }
#endif
    }
}
