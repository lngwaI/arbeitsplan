using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Serienanlage: viele Geraete einer Art auf einmal ("H1-F-0101" bis "0110"), optional mit gemeinsamem Hauptgeraet, Eigenschaften und Gruppe.
    class EquipSerialForm : Form
    {
        readonly EquipMaster m;
        readonly ComboBox cbClass = new ComboBox(), cbParent = new ComboBox();
        readonly TextBox tFrom = new TextBox(), tTo = new TextBox(), tProps = new TextBox(), tGroup = new TextBox();
        readonly CheckBox cbGroup = new CheckBox();
        readonly Label preview = new Label();
        readonly Button ok = new Button();
        public string Cls = "", MainDevice = "", GroupName = "";
        public List<string> Ids = new List<string>();
        public List<EquipProp> Props = new List<EquipProp>();

        public EquipSerialForm(EquipMaster master, string cls, string parent)
        {
            m = master;
            Text = "Serie anlegen";
            Ui.Dialog(this, 480, 430);
            Controls.Add(Ui.Hint("Mehrere Geräte auf einmal: erste und letzte ID (oder nur die letzte Nummer). Vorhandene IDs bleiben unberührt.", 10, 8, 450));
            Controls.Add(L("Geräteart:", 10, 36)); cbClass.DropDownStyle = ComboBoxStyle.DropDownList; cbClass.SetBounds(10, 54, 200, 22); Controls.Add(cbClass);
            foreach (EquipClass c in m.Classes) cbClass.Items.Add(c.Name);
            cbClass.SelectedItem = cls != null && cbClass.Items.Contains(cls) ? cls : null;
            Controls.Add(L("Erste ID:", 10, 86)); tFrom.SetBounds(10, 104, 200, 22); Ui.SimpleMenu(tFrom); Controls.Add(tFrom);
            Controls.Add(L("Letzte ID (oder Nummer):", 230, 86)); tTo.SetBounds(230, 104, 200, 22); Ui.SimpleMenu(tTo); Controls.Add(tTo);
            tFrom.Text = cls != null ? m.SuggestId(cls) : "";
            preview.SetBounds(10, 132, 450, 34); preview.ForeColor = SystemColors.GrayText; Controls.Add(preview);
            Controls.Add(L("Gehört zu (Hauptgerät, optional):", 10, 172)); cbParent.DropDownStyle = ComboBoxStyle.DropDown; cbParent.SetBounds(10, 190, 200, 22); Controls.Add(cbParent);
            cbParent.Items.Add(""); foreach (EquipDevice d in m.Devices) if (d.Parent.Length == 0) cbParent.Items.Add(d.Id);
            cbParent.Text = parent ?? "";
            Controls.Add(L("Eigenschaften für alle (eine je Zeile, Name=Wert):", 10, 222));
            tProps.Multiline = true; tProps.AcceptsReturn = true; tProps.SetBounds(10, 240, 450, 62); Ui.SimpleMenu(tProps); Controls.Add(tProps);
            cbGroup.Text = "Als Gerätegruppe anlegen:"; cbGroup.AutoSize = true; cbGroup.Location = new Point(10, 314); Controls.Add(cbGroup);
            tGroup.SetBounds(200, 311, 260, 22); Ui.SimpleMenu(tGroup); tGroup.Enabled = false; Controls.Add(tGroup);
            cbGroup.CheckedChanged += delegate { tGroup.Enabled = cbGroup.Checked; if (cbGroup.Checked && tGroup.Text.Length == 0) tGroup.Text = SuggestGroup(); };

            ok.Text = "Anlegen"; ok.SetBounds(284, 360, 90, 28); Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(380, 360, 84, 28); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; Controls.Add(cancel);
            ok.Click += delegate { if (Take()) DialogResult = DialogResult.OK; };
            tFrom.TextChanged += delegate { Preview(); }; tTo.TextChanged += delegate { Preview(); }; cbClass.SelectedIndexChanged += delegate { Preview(); };
            Preview();
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }

        string SuggestGroup() { return tFrom.Text.Trim() + " bis " + tTo.Text.Trim(); }

        List<string> Current(out string err)
        {
            if (tFrom.Text.Trim().Length == 0) { err = "Erste ID eingeben."; return null; }
            if (tTo.Text.Trim().Length == 0) { err = "Letzte ID (oder Nummer) eingeben."; return null; }
            return EquipMaster.SerialIds(tFrom.Text, tTo.Text, out err);
        }

        void Preview()
        {
            string err; List<string> ids = Current(out err);
            if (ids == null) { preview.Text = err; ok.Enabled = false; return; }
            int free = ids.FindAll(delegate(string id) { return m.Device(id) == null; }).Count;
            preview.Text = ids.Count + " Geräte (" + ids[0] + " … " + ids[ids.Count - 1] + "), davon " + free + " neu" + (free < ids.Count ? ", " + (ids.Count - free) + " schon vorhanden" : "") + ".";
            ok.Enabled = free > 0 && cbClass.SelectedItem != null;
        }

        bool Take()
        {
            string err; List<string> ids = Current(out err);
            if (ids == null || cbClass.SelectedItem == null) return false;
            Cls = Convert.ToString(cbClass.SelectedItem); MainDevice = cbParent.Text.Trim(); Ids = ids;
            if (MainDevice.Length > 0 && m.Device(MainDevice) == null) { MessageBox.Show(this, "Das Hauptgerät \"" + MainDevice + "\" ist nicht angelegt.", "Serie anlegen", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            Props.Clear();
            foreach (string line in tProps.Text.Replace("\r", "").Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(eq + 1).Trim().Length > 0) Props.Add(new EquipProp(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }
            GroupName = cbGroup.Checked ? tGroup.Text.Trim() : "";
            return true;
        }

#if TESTHOOKS
        public bool TestRun(string cls, string from, string to, string group, string props)
        {
            cbClass.SelectedItem = cls; tFrom.Text = from; tTo.Text = to; tProps.Text = props;
            if (group.Length > 0) { cbGroup.Checked = true; tGroup.Text = group; }
            return Take();
        }
#endif
    }
}
