using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstieg in Schnellerfassung, "Nicht zugeordnet", "Passt zu" (Mehrfachauswahl) und Verwaltung der Eigenschaftsfelder je Art.
    partial class EquipMasterForm
    {
        readonly Button btnUnassigned = new Button();
        readonly ListBox lbFields = new ListBox();
        const string FieldHelp = "Felder sind die Vorlage für alle Geräte dieser Art und erscheinen als leere Zeile an jedem Gerät.\n"
            + "Umbenennen zieht die Werte an den Geräten mit. Entfernen nimmt das Feld nur aus der Vorlage; bereits eingetragene Werte bleiben am Gerät.\n"
            + "Namensende „(Link)“ macht den Wert zu einem anklickbaren Link.";

        void BuildQuick()
        {
            Controls.Add(Btn("Schnell erfassen…", 654, 606, 118, delegate { OpenQuick(); }));
            btnUnassigned.SetBounds(690, 6, 282, 26); btnUnassigned.Click += delegate { OpenUnassigned(); }; Controls.Add(btnUnassigned);
            RefreshUnassigned();
        }

        void RefreshUnassigned()
        {
            int n = m.Unassigned().Count;
            btnUnassigned.Text = n == 0 ? "Alle Geräte sind zugeordnet" : n + " noch nicht zugeordnet – jetzt zuordnen…";
            btnUnassigned.Enabled = n > 0;
            btnUnassigned.ForeColor = n > 0 ? Color.DarkOrange : SystemColors.ControlText;
        }

        void OpenQuick()
        {
            using (EquipQuickForm f = new EquipQuickForm(m))
                if (f.ShowDialog(this) == DialogResult.OK && f.Added > 0) { search.Text = ""; RebuildTree(cur); RefreshUnassigned(); }
        }

        void OpenUnassigned()
        {
            using (EquipUnassignedForm f = new EquipUnassignedForm(m)) { f.ShowDialog(this); }
            RebuildTree(cur); RefreshUnassigned();
        }

        void OpenFits(EquipDevice d)
        {
            using (EquipFitsForm f = new EquipFitsForm(m, d.Id)) { if (f.ShowDialog(this) == DialogResult.OK) LoadRelations(d); }
        }

        // ---------- Eigenschaftsfelder der Art ----------

        void BuildFields(Panel p)
        {
            p.Controls.Add(L("Eigenschaftsfelder dieser Art:", 0, 74));
            lbFields.SetBounds(0, 92, 300, 168); p.Controls.Add(lbFields);
            p.Controls.Add(Btn("+ Feld", 0, 264, 56, delegate { AddField(); }));
            p.Controls.Add(Btn("Umbenennen", 60, 264, 90, delegate { RenameField(); }));
            p.Controls.Add(Btn("▲", 154, 264, 32, delegate { MoveField(-1); }));
            p.Controls.Add(Btn("▼", 190, 264, 32, delegate { MoveField(1); }));
            p.Controls.Add(Btn("Entfernen", 226, 264, 74, delegate { RemoveField(); }));
        }

        void LoadFields(EquipClass c, int select)
        {
            lbFields.Items.Clear();
            foreach (string f in c.Fields) lbFields.Items.Add(f);
            if (select >= 0 && select < lbFields.Items.Count) lbFields.SelectedIndex = select;
        }

        EquipClass FieldClass { get { return cur as EquipClass; } }

        void AddField()
        {
            EquipClass c = FieldClass; if (c == null) return;
            string n = EquipAsk.Name(this, "Neues Feld", "Name des Feldes (z. B. Volumen, Höhe, Datenblatt (Link)):", "");
            if (n == null) return;
            if (c.Fields.Exists(delegate(string x) { return Same(x, n); })) { Warn("Das Feld \"" + n + "\" gibt es schon."); return; }
            c.Fields.Add(n); LoadFields(c, c.Fields.Count - 1);
        }

        void RenameField()
        {
            EquipClass c = FieldClass; int i = lbFields.SelectedIndex; if (c == null || i < 0) return;
            string old = c.Fields[i], n = EquipAsk.Name(this, "Feld umbenennen", "Neuer Name für \"" + old + "\":", old);
            if (n == null || n == old) return;
            if (c.Fields.Exists(delegate(string x) { return Same(x, n); })) { Warn("Das Feld \"" + n + "\" gibt es schon."); return; }
            RenameFieldIn(c, old, n); LoadFields(c, i);
        }

        // Feld der Art umbenennen und die Werte der Geraete dieser Art mitziehen
        void RenameFieldIn(EquipClass c, string old, string now)
        {
            int i = c.Fields.IndexOf(old); if (i >= 0) c.Fields[i] = now;
            foreach (EquipDevice d in m.Devices)
                if (d.Class == c.Name) foreach (EquipProp p in d.Props) if (p.Name == old) p.Name = now;
        }

        void MoveField(int dir)
        {
            EquipClass c = FieldClass; int i = lbFields.SelectedIndex, j = i + dir;
            if (c == null || i < 0 || j < 0 || j >= c.Fields.Count) return;
            string t = c.Fields[i]; c.Fields[i] = c.Fields[j]; c.Fields[j] = t; LoadFields(c, j);
        }

        void RemoveField()
        {
            EquipClass c = FieldClass; int i = lbFields.SelectedIndex; if (c == null || i < 0) return;
            if (MessageBox.Show(this, "Feld \"" + c.Fields[i] + "\" aus der Vorlage nehmen?\nBereits eingetragene Werte bleiben an den Geräten erhalten.", "Stammdaten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            c.Fields.RemoveAt(i); LoadFields(c, Math.Min(i, c.Fields.Count - 1));
        }

#if TESTHOOKS
        public int TestUnassignedCount { get { return m.Unassigned().Count; } }
        public string TestUnassignedText() { return btnUnassigned.Text; }
        public void TestAddField(string n) { EquipClass c = FieldClass; if (c != null && !c.Fields.Contains(n)) { c.Fields.Add(n); LoadFields(c, c.Fields.Count - 1); } }
        public void TestRenameField(string old, string now) { EquipClass c = FieldClass; if (c != null) { RenameFieldIn(c, old, now); LoadFields(c, -1); } }
        public void TestMoveField(string name, int dir) { EquipClass c = FieldClass; if (c == null) return; lbFields.SelectedIndex = c.Fields.IndexOf(name); MoveField(dir); }
        public string TestFields() { EquipClass c = FieldClass; return c == null ? "" : string.Join(",", c.Fields.ToArray()); }
        public void TestQuick(string text, string cls) { List<QuickRow> r = m.ParseQuick(text); foreach (QuickRow q in r) if (q.State == QuickState.New && q.Class.Length == 0) q.Class = cls; m.AddQuick(r); RebuildTree(cur); RefreshUnassigned(); }
        public void TestFits(string id, params string[] want) { int a, r; m.SetFits(id, want, out a, out r); }
#endif
    }
}
