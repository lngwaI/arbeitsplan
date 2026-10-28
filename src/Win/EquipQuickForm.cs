using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kleine Namensabfrage (z. B. neue Geraeteart)
    static class EquipAsk
    {
        public static string Name(IWin32Window owner, string title, string label, string initial)
        {
            using (Form f = new Form())
            {
                Ui.Dialog(f, 360, 110); f.Text = title; f.ShowInTaskbar = false;
                Label l = new Label(); l.Text = label; l.AutoSize = true; l.Location = new Point(10, 10); f.Controls.Add(l);
                TextBox t = new TextBox(); t.SetBounds(10, 30, 340, 22); t.Text = initial; Ui.SimpleMenu(t); f.Controls.Add(t);
                Button ok = new Button(); ok.Text = "OK"; ok.SetBounds(190, 68, 78, 28); ok.DialogResult = DialogResult.OK; f.Controls.Add(ok);
                Button no = new Button(); no.Text = "Abbrechen"; no.SetBounds(272, 68, 78, 28); no.DialogResult = DialogResult.Cancel; f.Controls.Add(no);
                f.AcceptButton = ok; f.CancelButton = no;
                if (f.ShowDialog(owner) != DialogResult.OK) return null;
                string r = t.Text.Trim();
                return r.Length == 0 ? null : r;
            }
        }
    }

    // Schnellerfassung: viele IDs auf einmal (Text oder aus Excel), Vorschau mit Geraeteart-Vorschlag nach Nummernschema.
    class EquipQuickForm : Form
    {
        public const string None = "(Nicht zugeordnet)";
        readonly EquipMaster m;
        readonly TextBox box = new TextBox();
        readonly ListView lv = new ListView();
        readonly Label sum = new Label();
        readonly ComboBox cbCls = new ComboBox();
        readonly Button ok = new Button();
        List<QuickRow> rows = new List<QuickRow>();
        public int Added;

        public EquipQuickForm(EquipMaster master)
        {
            m = master;
            Text = "Schnellerfassung";
            Ui.Dialog(this, 700, 556);
            Controls.Add(Ui.Hint("Eine ID pro Zeile oder aus Excel einfügen (ID, Tabulator, Notiz). Die Geräteart wird nach dem Nummernschema vorgeschlagen.", 10, 8, 680));
            box.Multiline = true; box.AcceptsReturn = true; box.ScrollBars = ScrollBars.Vertical; box.SetBounds(10, 30, 680, 110); Ui.SimpleMenu(box); Controls.Add(box);
            box.TextChanged += delegate { Recheck(); };
            sum.SetBounds(10, 146, 680, 18); Controls.Add(sum);

            lv.SetBounds(10, 168, 680, 260); lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = true; lv.HideSelection = false; lv.GridLines = true;
            lv.Columns.Add("Geräte-ID", 130); lv.Columns.Add("Geräteart (Klick zum Ändern)", 180); lv.Columns.Add("Notiz", 150); lv.Columns.Add("Hinweis", 200);
            lv.MouseUp += OnListClick; Controls.Add(lv);

            Label l = new Label(); l.Text = "Geräteart für die markierten Zeilen (Strg/Umschalt = mehrere):"; l.AutoSize = true; l.Location = new Point(10, 438); Controls.Add(l);
            cbCls.DropDownStyle = ComboBoxStyle.DropDownList; cbCls.SetBounds(10, 458, 200, 22); Controls.Add(cbCls);
            Button set = new Button(); set.Text = "Setzen"; set.SetBounds(216, 456, 70, 26); set.Click += delegate { SetMarked(Pick()); }; Controls.Add(set);
            Button nw = new Button(); nw.Text = "Neue Art…"; nw.SetBounds(292, 456, 90, 26); nw.Click += delegate { NewClass(); }; Controls.Add(nw);
            Controls.Add(Ui.Hint("Ohne Geräteart landen Geräte unter „Nicht zugeordnet“ und lassen sich später gesammelt zuordnen.", 10, 490, 680));

            ok.SetBounds(446, 516, 150, 30); ok.Click += delegate { Added = m.AddQuick(rows); DialogResult = DialogResult.OK; }; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(604, 516, 86, 30); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; Controls.Add(cancel);
            FillClasses(); Recheck();
        }

        void FillClasses()
        {
            cbCls.Items.Clear(); cbCls.Items.Add(None);
            foreach (EquipClass c in m.Classes) cbCls.Items.Add(c.Name);
            cbCls.SelectedIndex = 0;
        }

        string Pick() { return cbCls.SelectedIndex <= 0 ? "" : Convert.ToString(cbCls.SelectedItem); }

        void Recheck()
        {
            // Zuweisungen, die der Nutzer per Hand gemacht hat, bleiben je ID erhalten
            Dictionary<string, string> manual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (QuickRow r in rows) if (r.State == QuickState.New && r.Why == "manuell") manual[r.Id] = r.Class;
            rows = m.ParseQuick(box.Text);
            foreach (QuickRow r in rows) { string c; if (r.State == QuickState.New && manual.TryGetValue(r.Id, out c)) { r.Class = c; r.Why = "manuell"; } }
            Redraw();
        }

        void Redraw()
        {
            lv.BeginUpdate(); lv.Items.Clear();
            int nNew = 0, nEx = 0, nDup = 0, nBad = 0, nNone = 0;
            foreach (QuickRow r in rows)
            {
                ListViewItem it = new ListViewItem(r.Id); it.Tag = r;
                it.SubItems.Add(r.State == QuickState.New ? (r.Class.Length == 0 ? None : r.Class) : r.State == QuickState.Exists ? (r.Class.Length == 0 ? None : r.Class) : "");
                it.SubItems.Add(r.Note);
                it.SubItems.Add(r.State == QuickState.New ? (r.Why == "manuell" ? "Art von Hand gesetzt" : r.Class.Length > 0 ? "Art nach Schema" : "neu") : r.Why);
                if (r.State == QuickState.Exists) it.ForeColor = Color.Gray;
                else if (r.State != QuickState.New) it.ForeColor = Color.Firebrick;
                else if (r.Class.Length == 0) it.SubItems[1].ForeColor = Color.DarkOrange;
                if (r.State == QuickState.New) { nNew++; if (r.Class.Length == 0) nNone++; }
                else if (r.State == QuickState.Exists) nEx++; else if (r.State == QuickState.Duplicate) nDup++; else nBad++;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            sum.Text = rows.Count == 0 ? "Noch nichts eingegeben." : nNew + " neu (davon " + nNone + " ohne Art), " + nEx + " schon vorhanden (grau, übersprungen), " + nDup + " doppelt, " + nBad + " ungültig.";
            ok.Text = nNew > 0 ? nNew + " Geräte anlegen" : "Nichts anzulegen"; ok.Enabled = nNew > 0;
        }

        void SetMarked(string cls)
        {
            foreach (ListViewItem it in lv.SelectedItems)
            {
                QuickRow r = (QuickRow)it.Tag;
                if (r.State == QuickState.New) { r.Class = cls; r.Why = "manuell"; }
            }
            List<int> keep = new List<int>(); foreach (int i in lv.SelectedIndices) keep.Add(i);
            Redraw();
            foreach (int i in keep) if (i < lv.Items.Count) lv.Items[i].Selected = true;
        }

        void NewClass()
        {
            string n = EquipAsk.Name(this, "Neue Geräteart", "Name der neuen Geräteart:", "");
            if (n == null) return;
            if (m.Class(n) == null) { EquipClass c = new EquipClass(); c.Name = n; m.Classes.Add(c); }
            FillClasses(); cbCls.SelectedItem = n; SetMarked(n);
        }

        void OnListClick(object s, MouseEventArgs e)
        {
            ListViewHitTestInfo h = lv.HitTest(e.Location);
            if (e.Button != MouseButtons.Left || h.Item == null || h.Item.SubItems.IndexOf(h.SubItem) != 1) return;
            if (((QuickRow)h.Item.Tag).State != QuickState.New) return;
            if (!h.Item.Selected) { lv.SelectedItems.Clear(); h.Item.Selected = true; }
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(None, null, delegate { SetMarked(""); });
            foreach (EquipClass c in m.Classes) { string n = c.Name; menu.Items.Add(n, null, delegate { SetMarked(n); }); }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Neue Art…", null, delegate { NewClass(); });
            menu.Show(lv, e.Location);
        }

#if TESTHOOKS
        public void TestPaste(string text) { box.Text = text.Replace("\n", "\r\n"); }
        public List<QuickRow> TestRows { get { return rows; } }
        public void TestSetClass(int index, string cls) { lv.SelectedItems.Clear(); lv.Items[index].Selected = true; SetMarked(cls); }
        public void TestSetClassAll(string cls) { foreach (ListViewItem it in lv.Items) it.Selected = true; SetMarked(cls); }
        public string TestSummary() { return sum.Text; }
        public int TestApply() { Added = m.AddQuick(rows); return Added; }
#endif
    }
}
