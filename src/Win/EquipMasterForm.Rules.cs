using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Reiter "Verwendbar mit" und "Ersatz" der Equipment-Stammdaten
    partial class EquipMasterForm
    {
        readonly ListBox lbLink = new ListBox(), lbSub = new ListBox();
        readonly ComboBox lFrom = new ComboBox(), uId = new ComboBox(), uRep = new ComboBox();
        TextBox lTo, lNote, uNote;
        readonly CheckBox uBoth = new CheckBox();

        void IdCombo(ComboBox c, int x, int y, int w) { c.DropDownStyle = ComboBoxStyle.DropDown; c.AutoCompleteMode = AutoCompleteMode.SuggestAppend; c.AutoCompleteSource = AutoCompleteSource.ListItems; c.SetBounds(x, y, w, 22); }

        void RefillLists()
        {
            loading = true;
            foreach (ComboBox c in new[] { lFrom, uId, uRep }) { string t = c.Text; c.Items.Clear(); foreach (EquipDevice d in m.Devices) c.Items.Add(d.Id); c.Text = t; }
            int i = lbLink.SelectedIndex; lbLink.Items.Clear(); foreach (EquipLink l in m.Links) lbLink.Items.Add(l); if (i >= 0 && i < lbLink.Items.Count) lbLink.SelectedIndex = i;
            i = lbSub.SelectedIndex; lbSub.Items.Clear(); foreach (EquipSubst s in m.Substs) lbSub.Items.Add(s); if (i >= 0 && i < lbSub.Items.Count) lbSub.SelectedIndex = i;
            loading = false;
            LoadLink(); LoadSub();
        }

        // ---------- Verwendbar mit ----------

        void BuildLinks(TabPage p)
        {
            p.Controls.Add(L("Regeln (z. B. Skid → Filtergehäuse, Tank → Zubehör):", 8, 8));
            lbLink.SetBounds(8, 28, 330, 390); lbLink.HorizontalScrollbar = true; p.Controls.Add(lbLink);
            p.Controls.Add(Btn("+ Neu", 8, 424, 70, delegate { EquipLink l = new EquipLink(); l.From = ""; m.Links.Add(l); lbLink.Items.Add(l); lbLink.SelectedIndex = lbLink.Items.Count - 1; lFrom.Focus(); }));
            p.Controls.Add(Btn("Entfernen", 82, 424, 80, delegate { int i = lbLink.SelectedIndex; if (i < 0) return; m.Links.RemoveAt(i); lbLink.Items.RemoveAt(i); if (lbLink.Items.Count > 0) lbLink.SelectedIndex = Math.Min(i, lbLink.Items.Count - 1); LoadLink(); }));
            lbLink.SelectedIndexChanged += delegate { LoadLink(); };

            p.Controls.Add(L("Gerät (z. B. Skid):", 350, 8)); IdCombo(lFrom, 350, 28, 220); p.Controls.Add(lFrom);
            p.Controls.Add(L("kann verwendet werden mit (eine ID je Zeile):", 350, 62)); lTo = Multi(350, 82, 220, 150); p.Controls.Add(lTo);
            p.Controls.Add(L("Notiz (z. B. entscheidende Eigenschaft):", 350, 242)); lNote = Multi(350, 262, 390, 80); p.Controls.Add(lNote);
            Label h = L("Die Regel gilt in beide Richtungen. Im Paket-Editor erscheint der Hinweis an den Geräten.", 350, 352); h.AutoSize = false; h.SetBounds(350, 352, 390, 40); p.Controls.Add(h);

            lFrom.TextChanged += delegate { EquipLink l = lbLink.SelectedItem as EquipLink; if (loading || l == null) return; l.From = lFrom.Text.Trim(); RefreshItem(lbLink); };
            lTo.TextChanged += delegate { EquipLink l = lbLink.SelectedItem as EquipLink; if (loading || l == null) return; l.To = Lines(lTo.Text); RefreshItem(lbLink); };
            lNote.TextChanged += delegate { EquipLink l = lbLink.SelectedItem as EquipLink; if (loading || l == null) return; l.Note = lNote.Text.Trim(); };
        }

        void LoadLink()
        {
            EquipLink l = lbLink.SelectedItem as EquipLink;
            loading = true;
            lFrom.Text = l != null ? l.From : ""; lTo.Text = l != null ? string.Join("\r\n", l.To.ToArray()) : ""; lNote.Text = l != null ? l.Note : "";
            lFrom.Enabled = lTo.Enabled = lNote.Enabled = l != null;
            loading = false;
        }

        // ---------- Ersatz ----------

        void BuildSubsts(TabPage p)
        {
            p.Controls.Add(L("Ersatz-Beziehungen (wer kann wen vertreten, z. B. bei Wartung):", 8, 8));
            lbSub.SetBounds(8, 28, 330, 390); lbSub.HorizontalScrollbar = true; p.Controls.Add(lbSub);
            p.Controls.Add(Btn("+ Neu", 8, 424, 70, delegate { EquipSubst s = new EquipSubst(); m.Substs.Add(s); lbSub.Items.Add(s); lbSub.SelectedIndex = lbSub.Items.Count - 1; uId.Focus(); }));
            p.Controls.Add(Btn("Entfernen", 82, 424, 80, delegate { int i = lbSub.SelectedIndex; if (i < 0) return; m.Substs.RemoveAt(i); lbSub.Items.RemoveAt(i); if (lbSub.Items.Count > 0) lbSub.SelectedIndex = Math.Min(i, lbSub.Items.Count - 1); LoadSub(); }));
            lbSub.SelectedIndexChanged += delegate { LoadSub(); };

            p.Controls.Add(L("Ersatzgerät:", 350, 8)); IdCombo(uId, 350, 28, 220); p.Controls.Add(uId);
            p.Controls.Add(L("kann ersetzen:", 350, 62)); IdCombo(uRep, 350, 82, 220); p.Controls.Add(uRep);
            uBoth.Text = "auch umgekehrt (gleichwertig)"; uBoth.AutoSize = true; uBoth.Location = new Point(350, 114); p.Controls.Add(uBoth);
            p.Controls.Add(L("Unterschied (z. B. 5 Zoll statt 10 Zoll, höher):", 350, 146)); uNote = Multi(350, 166, 390, 100); p.Controls.Add(uNote);
            Label h = L("Im Paket-Editor schlägt das Ausweich-Gerät (Backup) diese Ersatzgeräte samt Unterschied vor.", 350, 276); h.AutoSize = false; h.SetBounds(350, 276, 390, 40); p.Controls.Add(h);

            uId.TextChanged += delegate { EquipSubst s = lbSub.SelectedItem as EquipSubst; if (loading || s == null) return; s.Id = uId.Text.Trim(); RefreshItem(lbSub); };
            uRep.TextChanged += delegate { EquipSubst s = lbSub.SelectedItem as EquipSubst; if (loading || s == null) return; s.Replaces = uRep.Text.Trim(); RefreshItem(lbSub); };
            uBoth.CheckedChanged += delegate { EquipSubst s = lbSub.SelectedItem as EquipSubst; if (loading || s == null) return; s.Both = uBoth.Checked; RefreshItem(lbSub); };
            uNote.TextChanged += delegate { EquipSubst s = lbSub.SelectedItem as EquipSubst; if (loading || s == null) return; s.Note = uNote.Text.Trim(); };
        }

        void LoadSub()
        {
            EquipSubst s = lbSub.SelectedItem as EquipSubst;
            loading = true;
            uId.Text = s != null ? s.Id : ""; uRep.Text = s != null ? s.Replaces : ""; uBoth.Checked = s != null && s.Both; uNote.Text = s != null ? s.Note : "";
            uId.Enabled = uRep.Enabled = uBoth.Enabled = uNote.Enabled = s != null;
            loading = false;
        }

        static void RefreshItem(ListBox lb) { int i = lb.SelectedIndex; if (i >= 0) lb.Items[i] = lb.Items[i]; }
    }
}
