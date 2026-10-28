using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Baum der Equipment-Stammdaten mit Suche: Geraeteart > Gerätegruppe > Geraet; Zubehoer zusaetzlich (grau) unter dem Hauptgeraet.
    partial class EquipMasterForm
    {
        string Query { get { return search.Text.Trim(); } }
        static bool Has(string s, string q) { return s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        bool Match(EquipDevice d)
        {
            string q = Query;
            if (q.Length == 0 || Has(d.Id, q) || Has(d.Note, q) || Has(d.Parent, q)) return true;
            foreach (EquipProp p in d.Props) if (Has(p.Name, q) || Has(p.Value, q)) return true;
            return false;
        }

        // Geraet steht in einer Gruppe (passende Art, Bereich oder Einzel-ID)
        bool InGroup(EquipDevice d)
        {
            foreach (EquipSeries s in m.Series) if ((s.Class.Length == 0 || s.Class == d.Class) && s.Contains(d.Id)) return true;
            return false;
        }

        void RebuildTree(object select)
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            foreach (EquipClass c in m.Classes) AddArt(c);
            AddArt(null);   // Geraete/Gruppen ohne bekannte Geraeteart
            tree.ExpandAll();
            TreeNode pick = Find(tree.Nodes, select);
            tree.EndUpdate();
            if (pick == null && tree.Nodes.Count > 0) pick = tree.Nodes[0];
            if (pick != null) { tree.SelectedNode = pick; pick.EnsureVisible(); }
            if (curNode != pick || pick == null) ShowFor(pick);   // ohne Fensterhandle kommt kein AfterSelect
            RefreshUnassigned();
        }

        void AddArt(EquipClass c)
        {
            string q = Query;
            TreeNode cn = new TreeNode(c != null ? c.Name : "(ohne Geräteart)"); cn.Tag = c;
            cn.ForeColor = Color.SteelBlue;
            foreach (EquipSeries s in m.Series)
            {
                if (c != null ? s.Class != c.Name : m.Class(s.Class) != null) continue;
                TreeNode sn = new TreeNode(s.Name); sn.Tag = s; sn.ToolTipText = s.Note;
                foreach (EquipDevice d in m.Members(s)) if (Match(d)) sn.Nodes.Add(DevNode(d));
                if (sn.Nodes.Count > 0 || q.Length == 0 || Has(s.Name, q) || Has(s.Note, q)) cn.Nodes.Add(sn);
            }
            foreach (EquipDevice d in m.Devices)
                if ((c != null ? d.Class == c.Name : m.Class(d.Class) == null) && !InGroup(d) && Match(d)) cn.Nodes.Add(DevNode(d));
            if (cn.Nodes.Count > 0 || (c != null && (q.Length == 0 || Has(c.Name, q)))) tree.Nodes.Add(cn);
        }

        TreeNode DevNode(EquipDevice d)
        {
            TreeNode n = new TreeNode(DevText(d)); n.Tag = d; n.ToolTipText = m.Info(d.Id);
            if (!d.Active) n.ForeColor = Color.Firebrick;
            foreach (EquipDevice a in m.Devices)
                if (a != d && Same(a.Parent, d.Id) && Match(a))
                {
                    TreeNode z = new TreeNode("↳ " + a.Id + " (Zubehör)"); z.Tag = a; z.Name = "z"; z.ForeColor = Color.Gray; z.ToolTipText = m.Info(a.Id);
                    n.Nodes.Add(z);
                }
            return n;
        }

        static string DevText(EquipDevice d) { return d.ToString() + (d.Parent.Length > 0 ? "  → " + d.Parent : ""); }

        // Zubehoer-Kopien ("z") nur, wenn das Geraet sonst nirgends steht
        static TreeNode Find(TreeNodeCollection nodes, object tag)
        {
            if (tag == null) return null;
            TreeNode z = null;
            Find(nodes, tag, ref z);
            return z;
        }

        static bool Find(TreeNodeCollection nodes, object tag, ref TreeNode found)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag == tag) { if (n.Name != "z") { found = n; return true; } if (found == null) found = n; }
                if (Find(n.Nodes, tag, ref found)) return true;
            }
            return false;
        }

        void ShowFor(TreeNode n)
        {
            curNode = n; cur = n != null ? n.Tag : null;
            loading = true;
            pCls.Visible = cur is EquipClass; pSer.Visible = cur is EquipSeries; pDev.Visible = cur is EquipDevice;
            if (cur is EquipClass) LoadClass((EquipClass)cur);
            if (cur is EquipSeries) LoadGroup((EquipSeries)cur);
            if (cur is EquipDevice) LoadCard((EquipDevice)cur);
            loading = false;
        }

        void SetNodeText(string text) { if (curNode != null) curNode.Text = text; }
    }
}
