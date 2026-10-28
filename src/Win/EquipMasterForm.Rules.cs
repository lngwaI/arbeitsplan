using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // "Passt zu" auf Art- und Gruppenebene (Regelseite "@Geraeteart" / "#Gruppe"), Serienanlage, Duplizieren, Tabelle und Matrix.
    partial class EquipMasterForm
    {
        // Regelliste einer Seite (Art oder Gruppe): zeigt die Partner dieser Regelseite, Auswahl aus Geraeten, "@Art" und "#Gruppe"
        class RuleBox
        {
            public readonly ListBox List = new ListBox();
            public readonly ComboBox Pick = new ComboBox();
            public Func<string> Token;
        }

        readonly RuleBox clsRules = new RuleBox(), serRules = new RuleBox();

        RuleBox BuildRules(Panel p, RuleBox b, int x, int y, int w, int listH, Func<string> token)
        {
            b.Token = token;
            p.Controls.Add(L("Passt zu (alle Geräte dieser Art/Gruppe; in beide Richtungen):", x, y));
            b.List.SetBounds(x, y + 18, w, listH); b.List.HorizontalScrollbar = true; p.Controls.Add(b.List);
            IdCombo(b.Pick, x, y + 24 + listH, w - 162); p.Controls.Add(b.Pick);
            p.Controls.Add(Btn("Hinzufügen", x + w - 156, y + 23 + listH, 80, delegate { AddRule(b); }));
            p.Controls.Add(Btn("Entfernen", x + w - 72, y + 23 + listH, 72, delegate { RemoveRule(b); }));
            return b;
        }

        void LoadRules(RuleBox b)
        {
            string tok = b.Token();
            b.List.Items.Clear(); b.Pick.Items.Clear();
            if (tok == null) return;
            foreach (EquipLink l in m.Links)
            {
                string note = l.Note.Length > 0 ? " – " + l.Note : "";
                if (l.From == tok) foreach (string t in l.To) b.List.Items.Add(new Item(t, RuleText(t) + note, l));
                else if (l.To.Contains(tok)) b.List.Items.Add(new Item(l.From, RuleText(l.From) + note, l));
            }
            foreach (EquipClass c in m.Classes) if ("@" + c.Name != tok) b.Pick.Items.Add("@" + c.Name);
            foreach (EquipSeries s in m.Series) if ("#" + s.Name != tok) b.Pick.Items.Add("#" + s.Name);
            foreach (EquipDevice d in m.Devices) b.Pick.Items.Add(d.Id);
            b.Pick.Text = "";
        }

        static string RuleText(string t) { return t.StartsWith("@") ? "alle " + t.Substring(1) + " (Art)" : t.StartsWith("#") ? "alle in " + t.Substring(1) + " (Gruppe)" : t; }

        // true, wenn die Regelseite existiert (Geraet, Art oder Gruppe)
        bool KnownSide(string t)
        {
            if (t.StartsWith("@")) return m.Class(t.Substring(1)) != null;
            if (t.StartsWith("#")) return m.Series.Exists(delegate(EquipSeries s) { return s.Name == t.Substring(1); });
            return m.Device(t) != null;
        }

        void AddRule(RuleBox b)
        {
            string tok = b.Token(), t = b.Pick.Text.Trim();
            if (tok == null || t.Length == 0) return;
            if (!EquipMaster.IsToken(t) && m.Device(t) != null) t = m.Device(t).Id;
            if (!KnownSide(t)) { Warn("\"" + t + "\" ist weder ein angelegtes Gerät noch eine Geräteart (@Name) oder Gruppe (#Name)."); return; }
            if (t == tok) { Warn("Bitte etwas anderes wählen."); return; }
            foreach (Item it in b.List.Items) if (Same(it.Id, t)) return;
            EquipLink l = m.Links.Find(delegate(EquipLink x) { return x.From == tok; });
            if (l == null) { l = new EquipLink(); l.From = tok; m.Links.Add(l); }
            l.To.Add(t);
            LoadRules(b);
        }

        void RemoveRule(RuleBox b)
        {
            Item sel = b.List.SelectedItem as Item; string tok = b.Token();
            if (sel == null || tok == null) return;
            foreach (EquipLink l in m.Links)
            {
                if (l.From == tok) l.To.RemoveAll(delegate(string x) { return x == sel.Id; });
                if (l.From == sel.Id) l.To.RemoveAll(delegate(string x) { return x == tok; });
            }
            m.Links.RemoveAll(delegate(EquipLink l) { return l.To.Count == 0; });
            LoadRules(b);
        }

        // ---------- Serie, Duplizieren, Tabelle, Matrix ----------

        void NewSerial()
        {
            string cls = SelClass();
            EquipDevice d = cur as EquipDevice;
            using (EquipSerialForm f = new EquipSerialForm(m, cls, d != null ? d.Parent : ""))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                List<string> added = m.AddSerial(f.Cls, f.Ids, f.MainDevice, f.Props, f.GroupName);
                search.Text = "";
                object pick = f.GroupName.Length > 0 ? (object)m.Series.Find(delegate(EquipSeries s) { return s.Name == f.GroupName; }) : (added.Count > 0 ? m.Device(added[0]) : null);
                RebuildTree(pick);
            }
        }

        void DuplicateDevice()
        {
            EquipDevice d = Dev; if (d == null) { Warn("Bitte zuerst ein Gerät im Baum wählen."); return; }
            string id = m.NextId(d.Id);
            if (id.Length == 0) id = d.Id + "-2";
            EquipDevice c = m.Duplicate(d, id);
            search.Text = ""; RebuildTree(c); dId.Focus(); dId.SelectAll();
        }

        void OpenTable()
        {
            using (EquipTableForm f = new EquipTableForm(m, SelClass()))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Picked == null) return;
                EquipDevice d = m.Device(f.Picked);
                if (d != null) { search.Text = ""; RebuildTree(d); }
            }
        }

        void OpenMatrix()
        {
            using (EquipMatrixForm f = new EquipMatrixForm(cat, true, null, null)) f.ShowDialog(this);
            ShowFor(curNode);   // Karte mit geaenderten Regeln neu laden
        }

        // Geraeteart-Feld "(Link)": Wert der markierten Eigenschaft oeffnen
        void OpenSelectedLink()
        {
            if (dProps.CurrentRow == null || dProps.CurrentRow.IsNewRow) return;
            string name = Convert.ToString(dProps.CurrentRow.Cells[0].Value), val = Convert.ToString(dProps.CurrentRow.Cells[1].Value);
            if (!EquipClass.IsLink(name)) { Warn("Die markierte Eigenschaft ist kein Link-Feld (Name endet mit „(Link)“)."); return; }
            EquipTableForm.OpenLink(this, val);
        }
    }
}
