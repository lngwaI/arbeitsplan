using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Geraete ohne Geraeteart: markieren (Strg/Umschalt) und mit einem Klick einer Art zuweisen.
    class EquipUnassignedForm : Form
    {
        readonly EquipMaster m;
        readonly ListView lv = new ListView();
        readonly Label count = new Label();
        readonly FlowLayoutPanel flow = new FlowLayoutPanel();
        readonly TextBox tNew = new TextBox();
        public int Moved;

        public EquipUnassignedForm(EquipMaster master)
        {
            m = master;
            Text = "Nicht zugeordnete Geräte";
            Ui.Dialog(this, 760, 520);
            Controls.Add(Ui.Hint("Geräte ohne Geräteart: links markieren (Strg/Umschalt = mehrere), rechts die Art wählen. Nichts geht verloren.", 10, 8, 740));
            count.SetBounds(10, 30, 430, 18); Controls.Add(count);
            lv.SetBounds(10, 52, 430, 400); lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = true; lv.HideSelection = false; lv.GridLines = true;
            lv.Columns.Add("Geräte-ID", 150); lv.Columns.Add("Notiz", 130); lv.Columns.Add("Eigenschaften", 130);
            Controls.Add(lv);
            Button all = new Button(); all.Text = "Alle markieren"; all.SetBounds(10, 458, 110, 26); all.Click += delegate { foreach (ListViewItem it in lv.Items) it.Selected = true; lv.Focus(); }; Controls.Add(all);
            Button sug = new Button(); sug.Text = "Gleicher Kennbuchstabe wie markiert"; sug.SetBounds(126, 458, 220, 26); sug.Click += delegate { SameLetter(); }; Controls.Add(sug);

            Label l = new Label(); l.Text = "Alle markierten sind:"; l.AutoSize = true; l.Location = new Point(460, 30); Controls.Add(l);
            flow.SetBounds(460, 52, 290, 300); flow.AutoScroll = true; flow.FlowDirection = FlowDirection.TopDown; flow.WrapContents = false; Controls.Add(flow);
            Label ln = new Label(); ln.Text = "Oder neue Geräteart anlegen:"; ln.AutoSize = true; ln.Location = new Point(460, 364); Controls.Add(ln);
            tNew.SetBounds(460, 384, 290, 22); Ui.SimpleMenu(tNew); Controls.Add(tNew);
            Button nw = new Button(); nw.Text = "Anlegen und den markierten zuweisen"; nw.SetBounds(460, 412, 290, 28); nw.Click += delegate { NewAndAssign(tNew.Text.Trim()); }; Controls.Add(nw);

            Button close = new Button(); close.Text = "Fertig"; close.SetBounds(660, 480, 90, 30); close.DialogResult = DialogResult.OK; AcceptButton = close; CancelButton = close; Controls.Add(close);
            Reload();
        }

        void Reload()
        {
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (EquipDevice d in m.Unassigned())
            {
                ListViewItem it = new ListViewItem(d.Id); it.Tag = d; it.SubItems.Add(d.Note);
                StringBuilder sb = new StringBuilder(); foreach (EquipProp p in d.Props) { if (sb.Length > 0) sb.Append(", "); sb.Append(p.Name).Append('=').Append(p.Value); }
                it.SubItems.Add(sb.ToString()); lv.Items.Add(it);
            }
            lv.EndUpdate();
            count.Text = lv.Items.Count == 0 ? "Alle Geräte sind zugeordnet." : lv.Items.Count + " noch nicht zugeordnet.";
            flow.Controls.Clear();
            foreach (EquipClass c in m.Classes)
            {
                string n = c.Name; Button b = new Button(); b.Text = "→ " + n; b.Width = 250; b.Height = 26; b.TextAlign = ContentAlignment.MiddleLeft;
                b.Click += delegate { Assign(n); }; flow.Controls.Add(b);
            }
        }

        List<string> Marked()
        {
            List<string> ids = new List<string>();
            foreach (ListViewItem it in lv.SelectedItems) ids.Add(((EquipDevice)it.Tag).Id);
            return ids;
        }

        int Assign(string cls)
        {
            List<string> ids = Marked();
            if (ids.Count == 0) { MessageBox.Show(this, "Bitte zuerst links Geräte markieren.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return 0; }
            int n = m.AssignClass(ids, cls); Moved += n; Reload(); return n;
        }

        void NewAndAssign(string name)
        {
            if (name.Length == 0) { MessageBox.Show(this, "Bitte einen Namen eingeben.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (Assign(name) > 0) tNew.Text = "";
        }

        // Markiert alle Geraete mit demselben Kennbuchstaben (H1-T-… / H2-T-…) wie die bereits markierten
        void SameLetter()
        {
            List<string> letters = new List<string>();
            foreach (string id in Marked()) { string l = EquipMaster.LetterOf(id); if (l.Length > 0 && !letters.Contains(l)) letters.Add(l); }
            foreach (ListViewItem it in lv.Items) if (letters.Contains(EquipMaster.LetterOf(((EquipDevice)it.Tag).Id))) it.Selected = true;
            lv.Focus();
        }

#if TESTHOOKS
        public void TestMark(params string[] ids) { foreach (ListViewItem it in lv.Items) it.Selected = Array.IndexOf(ids, ((EquipDevice)it.Tag).Id) >= 0; }
        public int TestAssign(string cls) { return Assign(cls); }
        public int TestCount { get { return lv.Items.Count; } }
#endif
    }
}
