using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Geraetekarte: ID, Art, Wartung, "Gehört zu", Eigenschaften (Vorlage der Art), Notiz, "Passt zu" (EquipLink) und Ersatz (EquipSubst).
    partial class EquipMasterForm
    {
        readonly TextBox dId = new TextBox(), subNote = new TextBox();
        TextBox dNote;
        readonly ComboBox dCls = new ComboBox(), dParent = new ComboBox(), linkPick = new ComboBox(), subPick = new ComboBox();
        readonly CheckBox dActive = new CheckBox(), subBoth = new CheckBox();
        readonly DataGridView dProps = new DataGridView();
        readonly ListBox lbLinks = new ListBox(), lbSubs = new ListBox();
        readonly Label dInfo = new Label();

        EquipDevice Dev { get { return cur as EquipDevice; } }

        void BuildCard()
        {
            Panel p = pDev;
            p.Controls.Add(Ui.Hint("Was ist das? Ein einzelnes Gerät mit eigener ID; die Eigenschaftsfelder gibt seine Geräteart vor.", 0, 0, 650));
            p.Controls.Add(L("Geräte-ID:", 0, 24)); dId.SetBounds(0, 42, 200, 22); Ui.SimpleMenu(dId); p.Controls.Add(dId);
            p.Controls.Add(L("Geräteart:", 215, 24)); DropList(dCls, 215, 42, 160); p.Controls.Add(dCls);
            dActive.Text = "verfügbar (nicht in Wartung)"; dActive.AutoSize = true; dActive.Location = new Point(395, 44); p.Controls.Add(dActive);
            p.Controls.Add(L("Gehört zu (Hauptgerät, z. B. Skid oder Tank):", 0, 74)); DropList(dParent, 0, 92, 300); p.Controls.Add(dParent);
            dInfo.SetBounds(330, 74, 320, 44); dInfo.ForeColor = SystemColors.GrayText; p.Controls.Add(dInfo);

            p.Controls.Add(L("Eigenschaften (Felder der Geräteart):", 0, 124));
            dProps.SetBounds(0, 142, 310, 120); dProps.RowHeadersVisible = false; dProps.AllowUserToResizeRows = false;
            dProps.BackgroundColor = SystemColors.Window; dProps.Columns.Add("n", "Eigenschaft"); dProps.Columns.Add("v", "Wert");
            dProps.Columns[0].Width = 130; dProps.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dProps.CellValueChanged += delegate { if (!loading && Dev != null) Dev.Props = ReadProps(); };
            dProps.UserDeletedRow += delegate { if (!loading && Dev != null) Dev.Props = ReadProps(); };
            p.Controls.Add(dProps);
            p.Controls.Add(L("Notiz (z. B. Unterschied zu baugleichen Geräten):", 330, 124)); dNote = Multi(330, 142, 320, 120); p.Controls.Add(dNote);

            p.Controls.Add(L("Passt zu (gilt in beide Richtungen):", 0, 274));
            lbLinks.SetBounds(0, 292, 310, 150); lbLinks.HorizontalScrollbar = true; p.Controls.Add(lbLinks);
            IdCombo(linkPick, 0, 448, 150); p.Controls.Add(linkPick);
            p.Controls.Add(Btn("Hinzufügen", 156, 447, 80, delegate { if (Dev != null) AddLink(Dev, linkPick.Text.Trim()); }));
            p.Controls.Add(Btn("Entfernen", 240, 447, 70, delegate { Item it = lbLinks.SelectedItem as Item; if (Dev != null && it != null) RemoveLink(Dev, it.Id); }));

            p.Controls.Add(L("Ersatz (diese Geräte können es vertreten, z. B. bei Wartung):", 330, 274));
            lbSubs.SetBounds(330, 292, 320, 96); lbSubs.HorizontalScrollbar = true; p.Controls.Add(lbSubs);
            lbSubs.SelectedIndexChanged += delegate
            {
                Item it = lbSubs.SelectedItem as Item; if (it == null) return;
                EquipSubst s = (EquipSubst)it.Tag; subPick.Text = it.Id; subBoth.Checked = s.Both; subNote.Text = s.Note;
            };
            IdCombo(subPick, 330, 394, 150); p.Controls.Add(subPick);
            subBoth.Text = "gleichwertig (auch umgekehrt)"; subBoth.AutoSize = true; subBoth.Location = new Point(330, 422);   // eigene Zeile, sonst rechts abgeschnitten p.Controls.Add(subBoth);
            p.Controls.Add(L("Unterschied:", 330, 450)); subNote.SetBounds(410, 447, 240, 22); Ui.SimpleMenu(subNote); p.Controls.Add(subNote);
            p.Controls.Add(Btn("Hinzufügen / Ändern", 330, 476, 140, delegate { if (Dev != null) UpsertSub(Dev, subPick.Text.Trim(), subNote.Text.Trim(), subBoth.Checked); }));
            p.Controls.Add(Btn("Entfernen", 476, 476, 80, delegate { Item it = lbSubs.SelectedItem as Item; if (it != null) { m.Substs.Remove((EquipSubst)it.Tag); LoadRelations(Dev); } }));
            Label hb = Ui.Hint("Im Paket-Editor erscheinen „Passt zu“ als Hinweis und Ersatzgeräte als Ausweich-Vorschlag (mit Unterschied).", 0, 516, 650);
            hb.AutoEllipsis = false; hb.Height = 36; p.Controls.Add(hb);   // darf umbrechen statt abgeschnitten zu werden

            dId.TextChanged += delegate
            {
                EquipDevice d = Dev; if (loading || d == null) return;
                string old = d.Id; d.Id = dId.Text.Trim(); RenameId(old, d.Id); SetNodeText(DevText(d));
            };
            dCls.SelectedIndexChanged += delegate { EquipDevice d = Dev; if (loading || d == null) return; d.Class = dCls.Text; RebuildTree(d); };
            dActive.CheckedChanged += delegate { EquipDevice d = Dev; if (loading || d == null) return; d.Active = dActive.Checked; SetNodeText(DevText(d)); };
            dParent.SelectedIndexChanged += delegate { if (!loading && Dev != null) SetParent(Dev, dParent.Text); };
            dNote.TextChanged += delegate { EquipDevice d = Dev; if (loading || d == null) return; d.Note = dNote.Text.Trim(); };
        }

        void LoadCard(EquipDevice d)
        {
            dId.Text = d.Id; dActive.Checked = d.Active; dNote.Text = d.Note;
            dCls.Items.Clear(); foreach (EquipClass c in m.Classes) dCls.Items.Add(c.Name);
            if (d.Class.Length > 0 && !dCls.Items.Contains(d.Class)) dCls.Items.Add(d.Class);
            dCls.SelectedItem = d.Class.Length > 0 ? d.Class : null;
            FillParents(dParent, d);
            if (d.Parent.Length > 0 && !dParent.Items.Contains(d.Parent)) dParent.Items.Add(d.Parent);
            dParent.SelectedItem = d.Parent.Length > 0 ? d.Parent : NoParent;
            dProps.Rows.Clear();
            foreach (EquipProp pr in d.Props) dProps.Rows.Add(pr.Name, pr.Value);
            EquipClass dc = m.Class(d.Class);
            if (dc != null) foreach (string f in dc.Fields) if (!d.Props.Exists(delegate(EquipProp x) { return x.Name == f; })) dProps.Rows.Add(f, "");
            FillIds(linkPick, d, null); FillIds(subPick, d, null);
            linkPick.Text = subPick.Text = subNote.Text = ""; subBoth.Checked = false;
            LoadRelations(d);
        }

        List<EquipProp> ReadProps()
        {
            List<EquipProp> r = new List<EquipProp>();
            foreach (DataGridViewRow row in dProps.Rows)
            {
                if (row.IsNewRow) continue;
                string n = Convert.ToString(row.Cells[0].Value).Trim(), v = Convert.ToString(row.Cells[1].Value).Trim();
                if (n.Length > 0 && v.Length > 0) r.Add(new EquipProp(n, v));
            }
            return r;
        }

        // Listen "Passt zu" und "Ersatz" sowie Infozeile (Gruppen, Zubehoer, ersetzt selbst)
        void LoadRelations(EquipDevice d)
        {
            lbLinks.Items.Clear(); lbSubs.Items.Clear();
            if (d == null) return;
            foreach (EquipLink l in m.Links)
            {
                string note = l.Note.Length > 0 ? " – " + l.Note : "";
                if (Same(l.From, d.Id)) { foreach (string t in l.To) lbLinks.Items.Add(new Item(t, t + note, l)); }
                else if (l.To.Exists(delegate(string t) { return Same(t, d.Id); })) lbLinks.Items.Add(new Item(l.From, l.From + note, l));
            }
            List<string> own = new List<string>();
            foreach (EquipSubst s in m.Substs)
            {
                string other = Same(s.Replaces, d.Id) ? s.Id : (s.Both && Same(s.Id, d.Id) ? s.Replaces : null);
                if (other != null) lbSubs.Items.Add(new Item(other, other + (s.Both ? " (gleichwertig)" : "") + (s.Note.Length > 0 ? " – " + s.Note : ""), s));
                else if (Same(s.Id, d.Id)) own.Add(s.Replaces);
            }
            StringBuilder sb = new StringBuilder();
            List<string> grp = new List<string>(); foreach (EquipSeries x in m.SeriesOf(d.Id)) grp.Add(x.Name);
            sb.Append(grp.Count > 0 ? "Gruppe: " + string.Join(", ", grp.ToArray()) : "In keiner Gerätegruppe.");
            List<string> acc = new List<string>(); foreach (EquipDevice a in m.Devices) if (Same(a.Parent, d.Id)) acc.Add(a.Id);
            if (acc.Count > 0) sb.Append("\nZubehör: ").Append(string.Join(", ", acc.ToArray()));
            if (own.Count > 0) sb.Append("\nKann selbst ersetzen: ").Append(string.Join(", ", own.ToArray()));
            dInfo.Text = sb.ToString();
        }

        void SetParent(EquipDevice d, string parent)
        {
            d.Parent = parent == NoParent || Same(parent, d.Id) ? "" : parent.Trim();
            RebuildTree(d);
        }

        bool CheckOther(EquipDevice d, string id)
        {
            if (id.Length == 0) return false;
            if (Same(id, d.Id)) { Warn("Bitte ein anderes Gerät wählen."); return false; }
            if (m.Device(id) == null) { Warn("Das Gerät \"" + id + "\" ist nicht angelegt."); return false; }
            return true;
        }

        void AddLink(EquipDevice d, string id)
        {
            if (!CheckOther(d, id)) return;
            id = m.Device(id).Id;
            foreach (Item it in lbLinks.Items) if (Same(it.Id, id)) return;
            EquipLink l = m.Links.Find(delegate(EquipLink x) { return Same(x.From, d.Id); });
            if (l == null) { l = new EquipLink(); l.From = d.Id; m.Links.Add(l); }
            l.To.Add(id);
            linkPick.Text = "";
            LoadRelations(d);
        }

        void RemoveLink(EquipDevice d, string id)
        {
            foreach (EquipLink l in m.Links)
            {
                if (Same(l.From, d.Id)) l.To.RemoveAll(delegate(string t) { return Same(t, id); });
                if (Same(l.From, id)) l.To.RemoveAll(delegate(string t) { return Same(t, d.Id); });
            }
            m.Links.RemoveAll(delegate(EquipLink l) { return l.To.Count == 0; });
            LoadRelations(d);
        }

        // Ersatzgeraet id fuer d anlegen oder aendern (vorhandene Beziehung in beiden Schreibrichtungen wird wiederverwendet)
        void UpsertSub(EquipDevice d, string id, string note, bool both)
        {
            if (!CheckOther(d, id)) return;
            id = m.Device(id).Id;
            EquipSubst s = m.Substs.Find(delegate(EquipSubst x) { return (Same(x.Id, id) && Same(x.Replaces, d.Id)) || (x.Both && Same(x.Id, d.Id) && Same(x.Replaces, id)); });
            if (s == null) { s = new EquipSubst(); s.Id = id; s.Replaces = d.Id; m.Substs.Add(s); }
            if (!both && Same(s.Id, d.Id)) { s.Id = id; s.Replaces = d.Id; }   // Richtung: id vertritt d
            s.Note = note; s.Both = both;
            LoadRelations(d);
        }
    }
}
