using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Seiten "Geräteart" (EquipClass: Name, Eigenschaftsfelder der Vorlage) und "Gerätegruppe" (EquipSeries: Bereich, Mitglieder, Notiz,
    // "Gehört zu" fuer mehrere Geraete auf einmal ueber Haken, weil der Baum keine Mehrfachauswahl kennt).
    partial class EquipMasterForm
    {
        readonly TextBox cName = new TextBox();
        TextBox cFields;
        readonly Label cInfo = new Label();

        readonly TextBox sName = new TextBox(), sFrom = new TextBox(), sTo = new TextBox();
        TextBox sNote;
        readonly ComboBox sCls = new ComboBox(), sAdd = new ComboBox(), sParent = new ComboBox();
        readonly CheckedListBox sMembers = new CheckedListBox();
        readonly Label sInfo = new Label();

        void BuildClass()
        {
            Panel p = pCls;
            p.Controls.Add(Ui.Hint("Was ist das? Eine Geräteart (z. B. Tank, Skid, Filtergehäuse) gibt die Eigenschaftsfelder für alle ihre Geräte vor.", 0, 0, 650));
            p.Controls.Add(L("Name der Geräteart:", 0, 24)); cName.SetBounds(0, 42, 300, 22); Ui.SimpleMenu(cName); p.Controls.Add(cName);
            p.Controls.Add(L("Eigenschaftsfelder (eines je Zeile, z. B. Volumen, Höhe, Größe):", 0, 74)); cFields = Multi(0, 92, 300, 200); p.Controls.Add(cFields);
            cInfo.SetBounds(320, 92, 330, 60); cInfo.ForeColor = SystemColors.GrayText; p.Controls.Add(cInfo);
            cName.TextChanged += delegate
            {
                EquipClass c = cur as EquipClass; if (loading || c == null) return;
                string old = c.Name; c.Name = cName.Text.Trim();
                foreach (EquipDevice d in m.Devices) if (d.Class == old) d.Class = c.Name;
                foreach (EquipSeries s in m.Series) if (s.Class == old) s.Class = c.Name;
                SetNodeText(c.Name);
            };
            cFields.TextChanged += delegate { EquipClass c = cur as EquipClass; if (loading || c == null) return; c.Fields = Lines(cFields.Text); };
        }

        void LoadClass(EquipClass c)
        {
            cName.Text = c.Name; cFields.Text = string.Join("\r\n", c.Fields.ToArray());
            int nd = m.Devices.FindAll(delegate(EquipDevice d) { return d.Class == c.Name; }).Count, ns = m.Series.FindAll(delegate(EquipSeries s) { return s.Class == c.Name; }).Count;
            cInfo.Text = nd + " Geräte, " + ns + " Gerätegruppen.\nNeue Felder erscheinen an jedem Gerät dieser Art als leere Zeile.";
        }

        void BuildGroup()
        {
            Panel p = pSer;
            p.Controls.Add(Ui.Hint("Was ist das? Geräte einer Art, die zusammengehören (z. B. „Luftfilter 10 Zoll“, „Tanks 1–4“), mit gemeinsamer Notiz.", 0, 0, 650));
            p.Controls.Add(L("Name der Gruppe:", 0, 24)); sName.SetBounds(0, 42, 300, 22); Ui.SimpleMenu(sName); p.Controls.Add(sName);
            p.Controls.Add(L("Geräteart:", 320, 24)); DropList(sCls, 320, 42, 160); p.Controls.Add(sCls);
            p.Controls.Add(L("Nummernbereich von:", 0, 74)); sFrom.SetBounds(0, 92, 140, 22); Ui.SimpleMenu(sFrom); p.Controls.Add(sFrom);
            p.Controls.Add(L("bis:", 160, 74)); sTo.SetBounds(160, 92, 140, 22); Ui.SimpleMenu(sTo); p.Controls.Add(sTo);
            p.Controls.Add(L("Gruppen-Notiz (gilt für alle Geräte der Gruppe):", 320, 74)); sNote = Multi(320, 92, 330, 80); p.Controls.Add(sNote);

            p.Controls.Add(L("Mitglieder (Haken = für die Knöpfe rechts markiert):", 0, 124));
            sMembers.SetBounds(0, 142, 300, 340); sMembers.CheckOnClick = true; sMembers.IntegralHeight = false; p.Controls.Add(sMembers);
            p.Controls.Add(Btn("Alle markieren", 0, 488, 120, delegate { for (int i = 0; i < sMembers.Items.Count; i++) sMembers.SetItemChecked(i, true); }));

            p.Controls.Add(L("Gerät aufnehmen:", 320, 184)); IdCombo(sAdd, 320, 202, 200); p.Controls.Add(sAdd);
            p.Controls.Add(Btn("Aufnehmen", 526, 201, 90, delegate { AddMember(sAdd.Text.Trim()); }));
            p.Controls.Add(Btn("Markierte aus der Gruppe nehmen", 320, 240, 220, delegate { RemoveMarked(); }));
            p.Controls.Add(L("Gehört zu (Hauptgerät):", 320, 284)); DropList(sParent, 320, 302, 200); p.Controls.Add(sParent);
            p.Controls.Add(Btn("Gehört zu für Markierte setzen", 320, 330, 220, delegate { SetParentMarked(sParent.Text); }));
            sInfo.SetBounds(320, 372, 330, 60); sInfo.ForeColor = SystemColors.GrayText; p.Controls.Add(sInfo);

            sName.TextChanged += delegate { EquipSeries s = cur as EquipSeries; if (loading || s == null) return; s.Name = sName.Text.Trim(); SetNodeText(s.Name); };
            sCls.SelectedIndexChanged += delegate { EquipSeries s = cur as EquipSeries; if (loading || s == null) return; s.Class = sCls.Text; RebuildTree(s); };
            sFrom.TextChanged += delegate { EquipSeries s = cur as EquipSeries; if (loading || s == null) return; s.From = sFrom.Text.Trim(); FillMembers(s); };
            sTo.TextChanged += delegate { EquipSeries s = cur as EquipSeries; if (loading || s == null) return; s.To = sTo.Text.Trim(); FillMembers(s); };
            EventHandler later = delegate { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { if (!IsDisposed && Visible) RebuildTree(cur); }); };   // Baum erst nach dem Klick neu aufbauen
            sFrom.Leave += later; sTo.Leave += later;
            sNote.TextChanged += delegate { EquipSeries s = cur as EquipSeries; if (loading || s == null) return; s.Note = sNote.Text.Trim(); };
        }

        void LoadGroup(EquipSeries s)
        {
            sName.Text = s.Name; sFrom.Text = s.From; sTo.Text = s.To; sNote.Text = s.Note;
            sCls.Items.Clear(); foreach (EquipClass c in m.Classes) sCls.Items.Add(c.Name);
            sCls.SelectedItem = s.Class.Length > 0 && sCls.Items.Contains(s.Class) ? s.Class : null;
            FillMembers(s);
            FillIds(sAdd, null, s.Class.Length > 0 ? s.Class : null); sAdd.Text = "";
            FillParents(sParent, null); sParent.SelectedIndex = 0;
            sInfo.Text = "";
        }

        void FillMembers(EquipSeries s)
        {
            bool l = loading; loading = true;
            sMembers.Items.Clear();
            foreach (EquipDevice d in m.Members(s)) sMembers.Items.Add(new Item(d.Id, DevText(d), d));
            loading = l;
        }

        List<EquipDevice> Marked()
        {
            List<EquipDevice> r = new List<EquipDevice>();
            foreach (object o in sMembers.CheckedItems) r.Add((EquipDevice)((Item)o).Tag);
            return r;
        }

        void AddMember(string id)
        {
            EquipSeries s = cur as EquipSeries; if (s == null || id.Length == 0) return;
            EquipDevice d = m.Device(id);
            if (d == null) { Warn("Das Gerät \"" + id + "\" ist nicht angelegt."); return; }
            if (s.Class.Length > 0 && d.Class != s.Class) { Warn("Das Gerät gehört zur Geräteart \"" + d.Class + "\", die Gruppe zu \"" + s.Class + "\"."); return; }
            if (!s.Contains(d.Id)) s.Ids.Add(d.Id);
            RebuildTree(s);
        }

        void RemoveMarked()
        {
            EquipSeries s = cur as EquipSeries; if (s == null) return;
            List<string> stay = new List<string>();
            foreach (EquipDevice d in Marked())
            {
                s.Ids.RemoveAll(delegate(string x) { return Same(x, d.Id); });
                if (s.Contains(d.Id)) stay.Add(d.Id);
            }
            RebuildTree(s);
            if (stay.Count > 0) sInfo.Text = "Bleibt über den Nummernbereich in der Gruppe: " + string.Join(", ", stay.ToArray());
        }

        void SetParentMarked(string parent)
        {
            EquipSeries s = cur as EquipSeries; if (s == null) return;
            string p = parent == NoParent ? "" : parent;
            int n = 0;
            foreach (EquipDevice d in Marked()) if (!Same(d.Id, p)) { d.Parent = p; n++; }
            RebuildTree(s);
            sInfo.Text = n == 0 ? "Keine Geräte markiert." : n + " Geräte: " + (p.Length > 0 ? "gehören jetzt zu " + p + "." : "jetzt eigenständig.");
        }
    }
}
