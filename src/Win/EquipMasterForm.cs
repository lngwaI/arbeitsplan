using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Equipment-Stammdaten (fuer Nutzer mit PackageAdmin): Baum Klasse > Reihe > Geraet mit Eigenschaften, Notizen und Wartungsstatus;
    // dazu "Verwendbar mit"-Regeln und Ersatz-Beziehungen. Nur Modellierung, im Arbeitsplan selbst noch nicht sichtbar.
    partial class EquipMasterForm : Form
    {
        readonly Catalog cat;
        readonly EquipMaster m;
        public Catalog Result;
        bool loading;

        readonly TreeView tree = new TreeView();
        readonly Panel pCls = new Panel(), pSer = new Panel(), pDev = new Panel();
        // Klasse
        readonly TextBox cName = new TextBox(), cFields = new TextBox();
        // Reihe
        readonly TextBox sName = new TextBox(), sFrom = new TextBox(), sTo = new TextBox();
        TextBox sIds, sNote;
        readonly ComboBox sCls = new ComboBox();
        readonly Label sMembers = new Label();
        // Geraet
        readonly TextBox dId = new TextBox();
        TextBox dProps, dNote;
        readonly ComboBox dCls = new ComboBox();
        readonly CheckBox dActive = new CheckBox();
        readonly Label dSeries = new Label();

        public EquipMasterForm(Catalog catalog)
        {
            cat = catalog.Clone(); m = cat.Master;
            Text = "Equipment-Stammdaten";
            Ui.Dialog(this, 780, 560);

            TabControl tabs = new TabControl(); tabs.SetBounds(8, 8, 764, 500);
            TabPage t1 = new TabPage("Klassen, Reihen, Geräte"), t2 = new TabPage("Verwendbar mit"), t3 = new TabPage("Ersatz");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2); tabs.TabPages.Add(t3);
            BuildTree(t1); BuildLinks(t2); BuildSubsts(t3);
            tabs.SelectedIndexChanged += delegate { RefillLists(); };

            Button ok = new Button(); ok.Text = "Speichern"; ok.SetBounds(580, 516, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(680, 516, 90, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { tabs, ok, cancel });
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }
        static TextBox Multi(int x, int y, int w, int h) { TextBox t = new TextBox(); t.Multiline = true; t.AcceptsReturn = true; t.ScrollBars = ScrollBars.Vertical; t.SetBounds(x, y, w, h); Ui.SimpleMenu(t); return t; }

        // ---------- Baum ----------

        void BuildTree(TabPage p)
        {
            tree.SetBounds(8, 8, 270, 410); tree.HideSelection = false; tree.ShowNodeToolTips = true;
            tree.AfterSelect += delegate { ShowFor(tree.SelectedNode != null ? tree.SelectedNode.Tag : null); };
            p.Controls.Add(tree);
            p.Controls.Add(Btn("+ Klasse", 8, 424, 62, delegate { NewClass(); }));
            p.Controls.Add(Btn("+ Reihe", 74, 424, 58, delegate { NewSeries(); }));
            p.Controls.Add(Btn("+ Gerät", 136, 424, 58, delegate { NewDevice(); }));
            p.Controls.Add(Btn("Entfernen", 198, 424, 80, delegate { RemoveSelected(); }));

            foreach (Panel pn in new[] { pCls, pSer, pDev }) { pn.SetBounds(290, 8, 460, 440); pn.Visible = false; p.Controls.Add(pn); }

            // Klasse
            pCls.Controls.Add(L("Klasse (z. B. Tank, Skid, Filtergehäuse):", 0, 0)); cName.SetBounds(0, 20, 300, 22); pCls.Controls.Add(cName);
            pCls.Controls.Add(L("Eigenschaftsfelder (eines je Zeile, z. B. Volumen, Höhe, Größe):", 0, 54));
            cFields.Multiline = true; cFields.AcceptsReturn = true; cFields.SetBounds(0, 74, 300, 160); Ui.SimpleMenu(cFields); pCls.Controls.Add(cFields);
            Label ch = L("Die Felder sind die Vorlage für jedes Gerät dieser Klasse; weitere Zeilen kann man am Gerät selbst ergänzen.", 0, 242);
            ch.AutoSize = false; ch.SetBounds(0, 242, 440, 36); pCls.Controls.Add(ch);
            cName.TextChanged += delegate
            {
                EquipClass c = Sel as EquipClass; if (loading || c == null) return;
                string old = c.Name; c.Name = cName.Text.Trim();
                foreach (EquipDevice d in m.Devices) if (d.Class == old) d.Class = c.Name;
                foreach (EquipSeries s in m.Series) if (s.Class == old) s.Class = c.Name;
                tree.SelectedNode.Text = c.Name;
            };
            cFields.TextChanged += delegate { EquipClass c = Sel as EquipClass; if (loading || c == null) return; c.Fields = Lines(cFields.Text); };

            // Reihe
            pSer.Controls.Add(L("Name der Reihe:", 0, 0)); sName.SetBounds(0, 20, 300, 22); pSer.Controls.Add(sName);
            pSer.Controls.Add(L("Klasse:", 320, 0)); sCls.DropDownStyle = ComboBoxStyle.DropDownList; sCls.SetBounds(320, 20, 130, 22); pSer.Controls.Add(sCls);
            pSer.Controls.Add(L("Nummernbereich von:", 0, 54)); sFrom.SetBounds(0, 74, 140, 22); pSer.Controls.Add(sFrom);
            pSer.Controls.Add(L("bis:", 160, 54)); sTo.SetBounds(160, 74, 140, 22); pSer.Controls.Add(sTo);
            pSer.Controls.Add(L("zusätzlich einzelne Geräte (eine ID je Zeile):", 0, 108)); sIds = Multi(0, 128, 300, 70); pSer.Controls.Add(sIds);
            pSer.Controls.Add(L("Reihen-Notiz (gilt für alle Geräte der Reihe):", 0, 208)); sNote = Multi(0, 228, 450, 90); pSer.Controls.Add(sNote);
            sMembers.SetBounds(0, 328, 450, 80); pSer.Controls.Add(sMembers);
            sName.TextChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.Name = sName.Text.Trim(); tree.SelectedNode.Text = s.Name; };
            sCls.SelectedIndexChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.Class = sCls.Text; };
            sFrom.TextChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.From = sFrom.Text.Trim(); ShowMembers(s); };
            sTo.TextChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.To = sTo.Text.Trim(); ShowMembers(s); };
            sIds.TextChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.Ids = Lines(sIds.Text); ShowMembers(s); };
            sNote.TextChanged += delegate { EquipSeries s = Sel as EquipSeries; if (loading || s == null) return; s.Note = sNote.Text.Trim(); };

            // Geraet
            pDev.Controls.Add(L("Geräte-ID:", 0, 0)); dId.SetBounds(0, 20, 200, 22); pDev.Controls.Add(dId);
            pDev.Controls.Add(L("Klasse:", 220, 0)); dCls.DropDownStyle = ComboBoxStyle.DropDownList; dCls.SetBounds(220, 20, 160, 22); pDev.Controls.Add(dCls);
            dActive.Text = "verfügbar (nicht in Wartung)"; dActive.AutoSize = true; dActive.Location = new Point(0, 50); pDev.Controls.Add(dActive);
            pDev.Controls.Add(L("Eigenschaften (Name: Wert, eine je Zeile):", 0, 80)); dProps = Multi(0, 100, 300, 120); pDev.Controls.Add(dProps);
            pDev.Controls.Add(L("Notiz (z. B. Unterschied zu baugleichen Geräten):", 0, 230)); dNote = Multi(0, 250, 450, 80); pDev.Controls.Add(dNote);
            dSeries.SetBounds(0, 340, 450, 80); pDev.Controls.Add(dSeries);
            dId.TextChanged += delegate
            {
                EquipDevice d = Sel as EquipDevice; if (loading || d == null) return;
                string old = d.Id; d.Id = dId.Text.Trim(); RenameId(old, d.Id); tree.SelectedNode.Text = d.ToString();
            };
            dCls.SelectedIndexChanged += delegate { EquipDevice d = Sel as EquipDevice; if (loading || d == null) return; d.Class = dCls.Text; };
            dActive.CheckedChanged += delegate { EquipDevice d = Sel as EquipDevice; if (loading || d == null) return; d.Active = dActive.Checked; tree.SelectedNode.Text = d.ToString(); };
            dProps.TextChanged += delegate { EquipDevice d = Sel as EquipDevice; if (loading || d == null) return; d.Props = ParseProps(dProps.Text); };
            dNote.TextChanged += delegate { EquipDevice d = Sel as EquipDevice; if (loading || d == null) return; d.Note = dNote.Text.Trim(); };

            RebuildTree(null);
        }

        object Sel { get { return tree.SelectedNode != null ? tree.SelectedNode.Tag : null; } }

        static List<string> Lines(string text)
        {
            List<string> r = new List<string>();
            foreach (string s in text.Replace("\r", "").Split('\n')) if (s.Trim().Length > 0) r.Add(s.Trim());
            return r;
        }

        static List<EquipProp> ParseProps(string text)
        {
            List<EquipProp> r = new List<EquipProp>();
            foreach (string s in Lines(text))
            {
                int i = s.IndexOf(':');
                string n = i < 0 ? s : s.Substring(0, i).Trim(), v = i < 0 ? "" : s.Substring(i + 1).Trim();
                if (n.Length > 0 && v.Length > 0) r.Add(new EquipProp(n, v));
            }
            return r;
        }

        void RebuildTree(object select)
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            List<EquipDevice> rest = new List<EquipDevice>(m.Devices);
            foreach (EquipClass c in m.Classes)
            {
                TreeNode cn = new TreeNode(c.Name); cn.Tag = c; tree.Nodes.Add(cn);
                foreach (EquipSeries s in m.Series)
                {
                    if (s.Class != c.Name) continue;
                    TreeNode sn = new TreeNode(s.Name); sn.Tag = s; sn.ToolTipText = s.Note; cn.Nodes.Add(sn);
                    foreach (EquipDevice d in m.Members(s)) sn.Nodes.Add(DevNode(d));
                }
                foreach (EquipDevice d in m.Devices) if (d.Class == c.Name) { rest.Remove(d); if (m.SeriesOf(d.Id).Count == 0) cn.Nodes.Add(DevNode(d)); }
            }
            // Geraete ohne bekannte Klasse und Reihen ohne Klasse
            rest.RemoveAll(delegate(EquipDevice d) { return m.Class(d.Class) != null; });
            List<EquipSeries> freeSeries = m.Series.FindAll(delegate(EquipSeries s) { return m.Class(s.Class) == null; });
            if (rest.Count > 0 || freeSeries.Count > 0)
            {
                TreeNode on = new TreeNode("(ohne Klasse)"); tree.Nodes.Add(on);
                foreach (EquipSeries s in freeSeries) { TreeNode sn = new TreeNode(s.Name); sn.Tag = s; on.Nodes.Add(sn); }
                foreach (EquipDevice d in rest) on.Nodes.Add(DevNode(d));
            }
            tree.ExpandAll();
            TreeNode pick = Find(tree.Nodes, select);
            tree.EndUpdate();
            if (pick == null && tree.Nodes.Count > 0) pick = tree.Nodes[0];
            if (pick != null) { tree.SelectedNode = pick; pick.EnsureVisible(); } else ShowFor(null);
        }

        TreeNode DevNode(EquipDevice d)
        {
            TreeNode n = new TreeNode(d.ToString()); n.Tag = d;
            if (!d.Active) n.ForeColor = Color.Firebrick;
            return n;
        }

        static TreeNode Find(TreeNodeCollection nodes, object tag)
        {
            if (tag == null) return null;
            foreach (TreeNode n in nodes) { if (n.Tag == tag) return n; TreeNode f = Find(n.Nodes, tag); if (f != null) return f; }
            return null;
        }

        void ShowFor(object tag)
        {
            loading = true;
            pCls.Visible = tag is EquipClass; pSer.Visible = tag is EquipSeries; pDev.Visible = tag is EquipDevice;
            sCls.Items.Clear(); dCls.Items.Clear();
            foreach (EquipClass c in m.Classes) { sCls.Items.Add(c.Name); dCls.Items.Add(c.Name); }
            EquipClass cl = tag as EquipClass; EquipSeries s = tag as EquipSeries; EquipDevice d = tag as EquipDevice;
            if (cl != null) { cName.Text = cl.Name; cFields.Text = string.Join("\r\n", cl.Fields.ToArray()); }
            if (s != null)
            {
                sName.Text = s.Name; sCls.Text = s.Class; sFrom.Text = s.From; sTo.Text = s.To; sIds.Text = string.Join("\r\n", s.Ids.ToArray()); sNote.Text = s.Note;
                ShowMembers(s);
            }
            if (d != null)
            {
                dId.Text = d.Id; dCls.Text = d.Class; dActive.Checked = d.Active; dNote.Text = d.Note;
                StringBuilder sb = new StringBuilder();
                foreach (EquipProp p in d.Props) sb.Append(p.Name).Append(": ").Append(p.Value).Append("\r\n");
                EquipClass dc = m.Class(d.Class);
                if (dc != null) foreach (string f in dc.Fields) if (!d.Props.Exists(delegate(EquipProp p) { return p.Name == f; })) sb.Append(f).Append(": \r\n");
                dProps.Text = sb.ToString();
                List<string> sers = new List<string>(); foreach (EquipSeries x in m.SeriesOf(d.Id)) sers.Add(x.Name + (x.Note.Length > 0 ? " – " + x.Note : ""));
                dSeries.Text = sers.Count > 0 ? "Gehört zu: " + string.Join("\r\n", sers.ToArray()) : "Gehört zu keiner Reihe.";
            }
            loading = false;
        }

        void ShowMembers(EquipSeries s)
        {
            List<string> ids = new List<string>(); foreach (EquipDevice d in m.Members(s)) ids.Add(d.Id);
            sMembers.Text = ids.Count > 0 ? "Geräte in dieser Reihe (" + ids.Count + "): " + string.Join(", ", ids.ToArray()) : "Noch keine angelegten Geräte in dieser Reihe.";
        }

        // ---------- Neu / Entfernen ----------

        string SelClass()
        {
            object t = Sel;
            if (t is EquipClass) return ((EquipClass)t).Name;
            if (t is EquipSeries) return ((EquipSeries)t).Class;
            if (t is EquipDevice) return ((EquipDevice)t).Class;
            return m.Classes.Count > 0 ? m.Classes[0].Name : "";
        }

        void NewClass() { EquipClass c = new EquipClass(); c.Name = "Neue Klasse"; m.Classes.Add(c); RebuildTree(c); cName.Focus(); cName.SelectAll(); }

        void NewSeries()
        {
            EquipSeries s = new EquipSeries(); s.Name = "Neue Reihe"; s.Class = SelClass(); m.Series.Add(s); RebuildTree(s); sName.Focus(); sName.SelectAll();
        }

        void NewDevice()
        {
            EquipDevice d = new EquipDevice(); d.Class = SelClass(); d.Id = "Neues Gerät"; m.Devices.Add(d);
            RebuildTree(d); dId.Focus(); dId.SelectAll();
        }

        void RemoveSelected()
        {
            object t = Sel; if (t == null) return;
            string what = t is EquipClass ? "Klasse" : t is EquipSeries ? "Reihe" : "Gerät";
            string extra = t is EquipClass ? "\nGeräte und Reihen dieser Klasse bleiben erhalten (ohne Klasse)." : t is EquipDevice ? "\nRegeln und Ersatz-Beziehungen mit diesem Gerät werden ebenfalls entfernt." : "\nDie Geräte selbst bleiben erhalten.";
            if (MessageBox.Show(this, what + " wirklich entfernen?" + extra, "Stammdaten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (t is EquipClass) m.Classes.Remove((EquipClass)t);
            else if (t is EquipSeries) m.Series.Remove((EquipSeries)t);
            else if (t is EquipDevice)
            {
                EquipDevice d = (EquipDevice)t; m.Devices.Remove(d);
                m.Links.RemoveAll(delegate(EquipLink l) { return l.From == d.Id; });
                foreach (EquipLink l in m.Links) l.To.RemoveAll(delegate(string x) { return x == d.Id; });
                m.Links.RemoveAll(delegate(EquipLink l) { return l.To.Count == 0; });
                m.Substs.RemoveAll(delegate(EquipSubst s) { return s.Id == d.Id || s.Replaces == d.Id; });
                foreach (EquipSeries s in m.Series) s.Ids.RemoveAll(delegate(string x) { return x == d.Id; });
            }
            RebuildTree(null);
        }

        // ID eines Geraets geaendert: Verweise in Regeln, Ersatz und Reihen mitziehen
        void RenameId(string old, string now)
        {
            if (old == now || old.Length == 0) return;
            foreach (EquipLink l in m.Links) { if (l.From == old) l.From = now; for (int i = 0; i < l.To.Count; i++) if (l.To[i] == old) l.To[i] = now; }
            foreach (EquipSubst s in m.Substs) { if (s.Id == old) s.Id = now; if (s.Replaces == old) s.Replaces = now; }
            foreach (EquipSeries s in m.Series) for (int i = 0; i < s.Ids.Count; i++) if (s.Ids[i] == old) s.Ids[i] = now;
        }

        void Commit()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EquipDevice d in m.Devices)
            {
                if (d.Id.Length == 0) { Warn("Ein Gerät hat keine ID."); return; }
                if (!seen.Add(d.Id)) { Warn("Die Geräte-ID \"" + d.Id + "\" ist doppelt vorhanden."); return; }
            }
            foreach (EquipClass c in m.Classes) if (c.Name.Length == 0) { Warn("Eine Klasse hat keinen Namen."); return; }
            Result = cat;
            DialogResult = DialogResult.OK;
        }

        void Warn(string text) { MessageBox.Show(this, text, "Stammdaten", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
