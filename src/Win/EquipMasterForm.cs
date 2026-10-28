using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Equipment-Stammdaten (Recht PackageAdmin). In der Oberflaeche: Klasse = "Geräteart", Reihe = "Gerätegruppe" (Datenmodell/XML unveraendert).
    // Oben Suche, links Baum Geraeteart > Gruppe > Geraet (Zubehoer zusaetzlich unter seinem Hauptgeraet), rechts eine Seite je Knotentyp.
    partial class EquipMasterForm : Form
    {
        readonly Catalog cat;
        readonly EquipMaster m;
        public Catalog Result;
        bool loading;
        object cur;                 // gewaehlter Knoten (EquipClass, EquipSeries, EquipDevice oder null)
        TreeNode curNode;

        const string NoParent = "(eigenständig)";
        readonly TreeView tree = new TreeView();
        readonly TextBox search = new TextBox();
        readonly Panel pCls = new Panel(), pSer = new Panel(), pDev = new Panel();

        public EquipMasterForm(Catalog catalog)
        {
            cat = catalog.Clone(); m = cat.Master;
            Text = "Equipment-Stammdaten";
            Ui.Dialog(this, 980, 650);

            Controls.Add(L("Suchen (ID, Notiz, Eigenschaft):", 8, 11));
            search.SetBounds(200, 8, 200, 22); Ui.SimpleMenu(search); Controls.Add(search);
            search.TextChanged += delegate { RebuildTree(cur); };
            Controls.Add(Ui.Hint("Baum: Geräteart → Gerätegruppe → Gerät. Zubehör (mit „Gehört zu“) steht zusätzlich grau unter seinem Hauptgerät.", 412, 11, 560));

            tree.SetBounds(8, 40, 300, 560); tree.HideSelection = false; tree.ShowNodeToolTips = true;
            tree.AfterSelect += delegate(object s, TreeViewEventArgs e) { ShowFor(e.Node); };
            Controls.Add(tree);
            Controls.Add(Btn("+ Geräteart", 8, 606, 78, delegate { NewClass(); }));
            Controls.Add(Btn("+ Gruppe", 90, 606, 66, delegate { NewSeries(); }));
            Controls.Add(Btn("+ Gerät", 160, 606, 62, delegate { NewDevice(); }));
            Controls.Add(Btn("Entfernen", 226, 606, 82, delegate { RemoveSelected(); }));
            Controls.Add(Btn("+ Serie…", 320, 606, 70, delegate { NewSerial(); }));
            Controls.Add(Btn("Duplizieren", 394, 606, 84, delegate { DuplicateDevice(); }));
            Controls.Add(Btn("Tabelle…", 500, 606, 76, delegate { OpenTable(); }));
            Controls.Add(Btn("Matrix…", 580, 606, 70, delegate { OpenMatrix(); }));

            foreach (Panel pn in new[] { pCls, pSer, pDev }) { pn.SetBounds(320, 40, 652, 560); pn.Visible = false; Controls.Add(pn); }
            BuildClass(); BuildGroup(); BuildCard();

            Button ok = Btn("Speichern", 780, 612, 95, delegate { Commit(); }); ok.Height = 30;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(880, 612, 92, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { ok, cancel });
            RebuildTree(null);
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }
        static TextBox Multi(int x, int y, int w, int h) { TextBox t = new TextBox(); t.Multiline = true; t.AcceptsReturn = true; t.ScrollBars = ScrollBars.Vertical; t.SetBounds(x, y, w, h); Ui.SimpleMenu(t); return t; }
        static void DropList(ComboBox c, int x, int y, int w) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.SetBounds(x, y, w, 22); }
        static void IdCombo(ComboBox c, int x, int y, int w) { c.DropDownStyle = ComboBoxStyle.DropDown; c.AutoCompleteMode = AutoCompleteMode.SuggestAppend; c.AutoCompleteSource = AutoCompleteSource.ListItems; c.SetBounds(x, y, w, 22); }
        static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        static List<string> Lines(string text)
        {
            List<string> r = new List<string>();
            foreach (string s in text.Replace("\r", "").Split('\n')) if (s.Trim().Length > 0) r.Add(s.Trim());
            return r;
        }

        // Listeneintrag mit eigenem Text (Id = Geraet, Tag = zugehoeriges Objekt)
        class Item
        {
            public string Id, Text; public object Tag;
            public Item(string id, string text, object tag) { Id = id; Text = text; Tag = tag; }
            public override string ToString() { return Text; }
        }

        // Geraete-IDs in eine Auswahl fuellen (ohne except), Text bleibt erhalten
        void FillIds(ComboBox c, EquipDevice except, string cls)
        {
            c.Items.Clear();
            foreach (EquipDevice d in m.Devices) if (d != except && (cls == null || d.Class == cls)) c.Items.Add(d.Id);
        }

        void FillParents(ComboBox c, EquipDevice except)
        {
            c.Items.Clear(); c.Items.Add(NoParent);
            foreach (EquipDevice d in m.Devices) if (d != except && d.Parent.Length == 0) c.Items.Add(d.Id);
        }

        // ---------- Neu / Entfernen ----------

        string SelClass()
        {
            if (cur is EquipClass) return ((EquipClass)cur).Name;
            if (cur is EquipSeries) return ((EquipSeries)cur).Class;
            if (cur is EquipDevice) return ((EquipDevice)cur).Class;
            return m.Classes.Count > 0 ? m.Classes[0].Name : "";
        }

        void NewClass() { EquipClass c = new EquipClass(); c.Name = "Neue Geräteart"; m.Classes.Add(c); search.Text = ""; RebuildTree(c); cName.Focus(); cName.SelectAll(); }

        void NewSeries()
        {
            EquipSeries s = new EquipSeries(); s.Name = "Neue Gerätegruppe"; s.Class = SelClass(); m.Series.Add(s);
            search.Text = ""; RebuildTree(s); sName.Focus(); sName.SelectAll();
        }

        void NewDevice()
        {
            EquipDevice d = new EquipDevice(); d.Class = SelClass(); m.Devices.Add(d);
            d.Id = m.SuggestId(d.Class); if (d.Id.Length == 0) d.Id = "Neues Gerät";   // Vorschlag: naechste Nummer nach dem letzten Geraet der Art
            EquipDevice tpl = cur as EquipDevice;
            if (tpl != null && tpl.Class == d.Class) d.Parent = tpl.Parent;               // neben einem markierten Geraet: gleiches Hauptgeraet
            else if (cur is EquipSeries) { List<EquipDevice> mem = m.Members((EquipSeries)cur); if (mem.Count > 0) d.Parent = mem[mem.Count - 1].Parent; }
            EquipSeries s = cur as EquipSeries;
            if (s != null) s.Ids.Add(d.Id);   // in der markierten Gruppe anlegen (ID-Aenderung wird mitgezogen)
            search.Text = ""; RebuildTree(d); dId.Focus(); dId.SelectAll();
        }

        void RemoveSelected()
        {
            object t = cur; if (t == null) return;
            string what = t is EquipClass ? "Geräteart" : t is EquipSeries ? "Gerätegruppe" : "Gerät";
            string extra = t is EquipClass ? "\nGeräte und Gruppen dieser Art bleiben erhalten (ohne Geräteart)."
                : t is EquipDevice ? "\n„Passt zu“, Ersatz und „Gehört zu“ mit diesem Gerät werden ebenfalls entfernt." : "\nDie Geräte selbst bleiben erhalten.";
            if (MessageBox.Show(this, what + " wirklich entfernen?" + extra, "Stammdaten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (t is EquipClass) { m.RemoveToken("@" + ((EquipClass)t).Name); m.Classes.Remove((EquipClass)t); }
            else if (t is EquipSeries) { m.RemoveToken("#" + ((EquipSeries)t).Name); m.Series.Remove((EquipSeries)t); }
            else if (t is EquipDevice)
            {
                EquipDevice d = (EquipDevice)t; m.Devices.Remove(d);
                m.Links.RemoveAll(delegate(EquipLink l) { return l.From == d.Id; });
                foreach (EquipLink l in m.Links) l.To.RemoveAll(delegate(string x) { return x == d.Id; });
                m.Links.RemoveAll(delegate(EquipLink l) { return l.To.Count == 0; });
                m.Substs.RemoveAll(delegate(EquipSubst s) { return s.Id == d.Id || s.Replaces == d.Id; });
                foreach (EquipSeries s in m.Series) s.Ids.RemoveAll(delegate(string x) { return x == d.Id; });
                foreach (EquipDevice x in m.Devices) if (x.Parent == d.Id) x.Parent = "";
            }
            RebuildTree(null);
        }

        // ID eines Geraets geaendert: Verweise in "Passt zu", Ersatz, Gruppen und "Gehört zu" mitziehen
        void RenameId(string old, string now)
        {
            if (old == now || old.Length == 0) return;
            foreach (EquipLink l in m.Links) { if (l.From == old) l.From = now; for (int i = 0; i < l.To.Count; i++) if (l.To[i] == old) l.To[i] = now; }
            foreach (EquipSubst s in m.Substs) { if (s.Id == old) s.Id = now; if (s.Replaces == old) s.Replaces = now; }
            foreach (EquipSeries s in m.Series) for (int i = 0; i < s.Ids.Count; i++) if (s.Ids[i] == old) s.Ids[i] = now;
            foreach (EquipDevice d in m.Devices) if (d.Parent == old) d.Parent = now;
        }

        void Commit()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EquipDevice d in m.Devices)
            {
                if (d.Id.Length == 0) { Warn("Ein Gerät hat keine ID."); return; }
                if (!seen.Add(d.Id)) { Warn("Die Geräte-ID \"" + d.Id + "\" ist doppelt vorhanden."); return; }
            }
            foreach (EquipClass c in m.Classes) if (c.Name.Length == 0) { Warn("Eine Geräteart hat keinen Namen."); return; }
            Result = cat;
            DialogResult = DialogResult.OK;
        }

        void Warn(string text) { MessageBox.Show(this, text, "Stammdaten", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

#if TESTHOOKS
        // Testhilfen: Baum als Text, Knoten waehlen, Karte bedienen, Speichern
        public string TestTree() { System.Text.StringBuilder sb = new System.Text.StringBuilder(); Dump(tree.Nodes, "", sb); return sb.ToString(); }
        static void Dump(TreeNodeCollection nodes, string ind, System.Text.StringBuilder sb) { foreach (TreeNode n in nodes) { sb.Append(ind).Append(n.Text).Append('\n'); Dump(n.Nodes, ind + "  ", sb); } }
        public void TestSelect(string text)
        {
            object tag = m.Device(text); if (tag == null) tag = m.Class(text);
            if (tag == null) foreach (EquipSeries s in m.Series) if (s.Name == text) tag = s;
            RebuildTree(tag);
        }
        public void TestSearch(string q) { search.Text = q; }
        public void TestParent(string parent) { SetParent((EquipDevice)cur, parent); }
        public void TestRule(string token) { RuleBox b = cur is EquipClass ? clsRules : serRules; b.Pick.Text = token; AddRule(b); }
        public void TestSerial(string cls, string from, string to, string group, string props)
        {
            using (EquipSerialForm sf = new EquipSerialForm(m, cls, ""))
            {
                if (!sf.TestRun(cls, from, to, group, props)) return;
                m.AddSerial(sf.Cls, sf.Ids, sf.MainDevice, sf.Props, sf.GroupName); RebuildTree(null);
            }
        }
        public void TestDuplicate() { DuplicateDevice(); }
        public void TestNewDevice() { NewDevice(); }
        public string TestCurrentId() { return Dev != null ? Dev.Id : null; }
        public void TestLink(string id) { AddLink((EquipDevice)cur, id); }
        public void TestSubst(string id, string note, bool both) { UpsertSub((EquipDevice)cur, id, note, both); }
        public void TestGroupParent(string parent) { for (int i = 0; i < sMembers.Items.Count; i++) sMembers.SetItemChecked(i, true); SetParentMarked(parent); }
        public bool TestCommit() { Commit(); return Result != null; }
#endif
    }
}
