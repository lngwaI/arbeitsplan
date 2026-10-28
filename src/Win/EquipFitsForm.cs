using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // "Was passt zu diesem Geraet?" Mehrfachauswahl statt Einzel-Dropdown; Vorschlag nach Gruppe; gilt in beide Richtungen.
    class EquipFitsForm : Form
    {
        const string AllArts = "(alle Arten)";
        readonly EquipMaster m;
        readonly string id;
        readonly TextBox search = new TextBox();
        readonly ComboBox art = new ComboBox();
        readonly CheckedListBox list = new CheckedListBox();
        readonly ListBox sug = new ListBox();
        readonly Label preview = new Label();
        readonly HashSet<string> want = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool building;
        public int Added, Removed;

        public EquipFitsForm(EquipMaster master, string deviceId)
        {
            m = master; id = deviceId;
            EquipDevice me = m.Device(id);
            Text = "Passt zu – " + id;
            Ui.Dialog(this, 760, 560);
            Controls.Add(Ui.Hint("Haken setzen = dieses Gerät passt zu dem anderen (gilt in beide Richtungen). Haken entfernen = Paar löschen.", 10, 8, 740));
            Label h = new Label(); h.Text = "Gerät: " + id + (me != null && me.Class.Length > 0 ? "  (" + me.Class + ")" : ""); h.AutoSize = true; h.Font = new Font(Font, FontStyle.Bold); h.Location = new Point(10, 32); Controls.Add(h);

            Label ls = new Label(); ls.Text = "Suchen:"; ls.AutoSize = true; ls.Location = new Point(10, 62); Controls.Add(ls);
            search.SetBounds(62, 58, 150, 22); Ui.SimpleMenu(search); search.TextChanged += delegate { Fill(); }; Controls.Add(search);
            art.DropDownStyle = ComboBoxStyle.DropDownList; art.SetBounds(222, 58, 200, 22); Controls.Add(art);
            art.Items.Add(AllArts); foreach (EquipClass c in m.Classes) art.Items.Add(c.Name); art.SelectedIndex = 0;
            art.SelectedIndexChanged += delegate { Fill(); };

            list.SetBounds(10, 88, 440, 370); list.CheckOnClick = true; list.IntegralHeight = false; list.HorizontalScrollbar = true;
            list.ItemCheck += OnCheck; Controls.Add(list);
            Button all = new Button(); all.Text = "Alle angezeigten ankreuzen"; all.SetBounds(10, 464, 180, 26); all.Click += delegate { MarkShown(true); }; Controls.Add(all);
            Button none = new Button(); none.Text = "Alle angezeigten abwählen"; none.SetBounds(196, 464, 180, 26); none.Click += delegate { MarkShown(false); }; Controls.Add(none);

            Label lg = new Label(); lg.Text = "Vorschlag (steht in einer Gruppe mit einem Gerät, das schon passt):"; lg.AutoSize = false; lg.SetBounds(470, 62, 280, 32); Controls.Add(lg);
            sug.SetBounds(470, 96, 280, 250); sug.SelectionMode = SelectionMode.MultiExtended; Controls.Add(sug);
            Button take = new Button(); take.Text = "Vorschläge ankreuzen"; take.SetBounds(470, 352, 170, 26); take.Click += delegate { TakeSuggestions(); }; Controls.Add(take);
            preview.SetBounds(470, 392, 280, 80); preview.ForeColor = SystemColors.GrayText; Controls.Add(preview);

            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(556, 520, 100, 30); ok.Click += delegate { Apply(); DialogResult = DialogResult.OK; }; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(662, 520, 88, 30); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; Controls.Add(cancel);

            foreach (EquipDevice d in m.Devices) if (!Same(d.Id, id) && m.HasPair(id, d.Id)) want.Add(d.Id);
            foreach (string s in m.SuggestFits(id)) sug.Items.Add(s + (m.Device(s) != null && m.Device(s).Class.Length > 0 ? "  (" + m.Device(s).Class + ")" : ""));
            Fill(); UpdatePreview();
        }

        static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        List<EquipDevice> ShownList()
        {
            string q = search.Text.Trim(), cls = art.SelectedIndex <= 0 ? null : Convert.ToString(art.SelectedItem);
            List<EquipDevice> r = new List<EquipDevice>();
            foreach (EquipDevice d in m.Devices)
            {
                if (Same(d.Id, id) || (cls != null && d.Class != cls)) continue;
                if (q.Length > 0 && d.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && d.Note.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                r.Add(d);
            }
            return r;
        }

        void Fill()
        {
            building = true; list.BeginUpdate(); list.Items.Clear();
            foreach (EquipDevice d in ShownList())
            {
                bool viaRule = m.HasPair(id, d.Id) && !m.HasDirectPair(id, d.Id);
                list.Items.Add(d.Id + (d.Class.Length > 0 ? "  (" + d.Class + ")" : "") + (viaRule ? "   [über Art-/Gruppenregel]" : ""), want.Contains(d.Id));
            }
            list.EndUpdate(); building = false;
        }

        static string IdOf(string text) { int i = text.IndexOf("  ("); int j = text.IndexOf("   ["); int e = i >= 0 ? i : (j >= 0 ? j : text.Length); return text.Substring(0, e); }

        void OnCheck(object s, ItemCheckEventArgs e)
        {
            if (building) return;
            string d = IdOf(Convert.ToString(list.Items[e.Index]));
            if (e.NewValue == CheckState.Checked) want.Add(d); else want.Remove(d);
            UpdatePreview();
        }

        void MarkShown(bool on)
        {
            foreach (EquipDevice d in ShownList()) { if (on) want.Add(d.Id); else want.Remove(d.Id); }
            Fill(); UpdatePreview();
        }

        void TakeSuggestions()
        {
            bool any = sug.SelectedItems.Count > 0;
            for (int i = 0; i < sug.Items.Count; i++)
                if (!any || sug.GetSelected(i)) want.Add(IdOf(Convert.ToString(sug.Items[i])));
            Fill(); UpdatePreview();
        }

        void Diff(out int add, out int rem)
        {
            add = rem = 0;
            foreach (EquipDevice d in m.Devices)
            {
                if (Same(d.Id, id)) continue;
                bool w = want.Contains(d.Id);
                if (w && !m.HasPair(id, d.Id)) add++;
                else if (!w && m.HasDirectPair(id, d.Id)) rem++;
            }
        }

        void UpdatePreview()
        {
            int a, r; Diff(out a, out r);
            preview.Text = "Gewählt: " + want.Count + (want.Count == 1 ? " Gerät" : " Geräte") + ".\n" + (a == 0 && r == 0 ? "Keine Änderung." : a + " neue Paare, " + r + " entfernte Paare.")
                + "\nPaare über Art-/Gruppenregeln lassen sich nur dort ändern.";
        }

        void Apply() { m.SetFits(id, want, out Added, out Removed); }

#if TESTHOOKS
        public List<string> TestSuggestions() { List<string> r = new List<string>(); foreach (object o in sug.Items) r.Add(IdOf(Convert.ToString(o))); return r; }
        public void TestCheck(params string[] ids) { foreach (string x in ids) want.Add(x); Fill(); UpdatePreview(); }
        public void TestUncheck(string x) { want.Remove(x); Fill(); UpdatePreview(); }
        public void TestTakeSuggestions() { sug.ClearSelected(); TakeSuggestions(); }
        public string TestPreview() { return preview.Text; }
        public void TestApply() { Apply(); }
#endif
    }
}
