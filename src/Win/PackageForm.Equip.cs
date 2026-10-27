using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Zeile fuer eine Equipment-Position im Paket-Editor: Position | Geraet | abwechselnd | Ausweich-Geraet | Notiz | entfernen.
    class EquipRow : Panel
    {
        const int Gap = 6, AltW = 132, DelW = 24;
        readonly EquipAssign a;
        readonly EquipPosition pos;
        readonly Catalog cat;
        readonly Action changed;
        readonly Label name = new Label();
        readonly ComboBox cbId = new ComboBox(), cbFb = new ComboBox();
        readonly CheckBox chkAlt = new CheckBox();
        readonly Button btnAlt = new Button(), del = new Button();
        readonly TextBox note = new TextBox();
        bool loading;

        // x-Positionen der Spalten (Position, Geraet, abwechselnd, Ausweich, Notiz, Entfernen, Ende) fuer die Breite w; auch fuer die Spaltenkoepfe.
        public static int[] Cols(int w)
        {
            int rest = Math.Max(420, w - AltW - DelW - 5 * Gap);
            int p = rest * 27 / 100, id = rest * 25 / 100, fb = rest * 23 / 100, nt = rest - p - id - fb;
            int[] x = new int[7];
            x[1] = p + Gap; x[2] = x[1] + id + Gap; x[3] = x[2] + AltW + Gap; x[4] = x[3] + fb + Gap; x[5] = x[4] + nt + Gap; x[6] = x[5] + DelW;
            return x;
        }

        public EquipRow(EquipAssign a, Catalog cat, Action changed, Action<EquipAssign> remove)
        {
            this.a = a; this.cat = cat; this.pos = cat.Position(a.Short); this.changed = changed;
            name.Text = pos != null && pos.Name != a.Short ? pos.Name + " (" + a.Short + ")" : a.Short; name.AutoEllipsis = true;

            List<string> own = pos != null ? pos.Ids : new List<string>();
            string ph = pos != null ? pos.Placeholder() : "";
            cbId.DropDownStyle = ComboBoxStyle.DropDown;   // frei eintippbar (Notfall-/Ausweichgeraet)
            cbId.Items.AddRange(own.ToArray()); if (ph.Length > 0) cbId.Items.Add(ph);
            cbFb.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (EquipChoice c in cat.Interchangeable(a.Short)) cbFb.Items.Add(c.Short == a.Short ? c.Id : c.Short + " " + c.Id);
            chkAlt.Text = "abwechselnd";
            bool rot = pos != null && pos.AllowRotation;      // nur wo der Admin Rotation hinterlegt hat (z. B. Abfüllskid)
            chkAlt.Visible = btnAlt.Visible = rot || a.Alternate;   // bereits gesetzte Rotation (Import) bleibt sicht- und aenderbar, nichts wird still veraendert
            btnAlt.Text = "…";
            Ui.SimpleMenu(note);
            del.Text = "x"; del.Click += delegate { remove(a); };
            Controls.AddRange(new Control[] { name, cbId, chkAlt, btnAlt, cbFb, note, del });
            foreach (Button b in new[] { btnAlt, del }) Theme.Current.StyleButton(b);
            ToolTip tip = new ToolTip();
            tip.SetToolTip(name, name.Text);
            tip.SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange).");
            tip.SetToolTip(chkAlt, "Läufe wechseln reihum zwischen den gewählten Geräten (Lauf 1 → A, Lauf 2 → B, Lauf 3 → wieder A …). Zur Auswahl stehen alle Geräte, die laut Admin gleich eingesetzt werden können.");
            tip.SetToolTip(btnAlt, "Geräte für den Wechsel auswählen");
            tip.SetToolTip(cbFb, "Ausweich-Gerät (Backup), falls das geplante ausfällt.");
            tip.SetToolTip(note, "Notiz/Bezeichnung zu diesem Gerät (steht im Plan hinter der Nummer).");
            tip.SetToolTip(del, "Position aus diesem Schritt entfernen");

            loading = true;
            chkAlt.Checked = a.Alternate; cbFb.Text = a.Fallback; note.Text = a.Note;
            Refill();
            loading = false;

            cbId.TextChanged += delegate
            {
                if (loading || chkAlt.Checked) return;
                a.Choices.Clear();
                if (cbId.Text.Trim().Length > 0) a.Choices.Add(new EquipChoice(a.Short, cbId.Text.Trim()));
                Warn(); changed();
            };
            cbFb.TextChanged += delegate { if (!loading) { a.Fallback = cbFb.Text.Trim(); changed(); } };
            // Stammdaten: Ersatzgeraete des gewaehlten Geraets zuerst vorschlagen; Hinweise (Reihen-Notiz, Wartung, Regeln) als Tooltip
            cbFb.DropDown += delegate { SuggestFallbacks(); };
            cbId.TextChanged += delegate { ShowInfo(tip); };
            ShowInfo(tip);
            note.TextChanged += delegate { if (!loading) { a.Note = note.Text.Trim(); changed(); } };
            chkAlt.CheckedChanged += delegate
            {
                if (loading) return;
                a.Alternate = chkAlt.Checked;
                if (a.Alternate && a.Choices.Count < 2) PickAlternatives();
                if (!a.Alternate && a.Choices.Count > 1) a.Choices.RemoveRange(1, a.Choices.Count - 1);
                loading = true; Refill(); loading = false; changed();
            };
            btnAlt.Click += delegate { PickAlternatives(); loading = true; Refill(); loading = false; changed(); };
        }

        // Ist das eingetragene Geraet validiert (oder leer/abwechselnd)? Fuer die Pruefhinweise.
        public static bool Known(EquipAssign a, Catalog cat)
        {
            if (a.Alternate || a.Choices.Count == 0) return true;
            EquipPosition p = cat.Position(a.Short);
            string t = a.Choices[0].Id;
            return p != null && (p.Ids.Contains(t) || t == p.Placeholder());
        }

        // Frei eingetippte (nicht validierte) Geraete einer Zeile: Geraet, Wechselgeraete, Ausweich-Geraet (Austausch-Gruppe)
        public static List<string> Unvalidated(EquipAssign a, Catalog cat)
        {
            List<string> r = new List<string>();
            if (!Known(a, cat)) r.Add(a.Choices[0].Id);
            List<EquipChoice> grp = cat.Interchangeable(a.Short);
            EquipPosition p = cat.Position(a.Short);
            List<string> more = new List<string>();
            if (a.Alternate) foreach (EquipChoice c in a.Choices) more.Add(c.Id);
            if (a.Fallback.Length > 0) more.Add(a.Fallback);
            foreach (string id in more)
                if (id.Length > 0 && !r.Contains(id) && (p == null || id != p.Placeholder()) && cat.Master.Device(id) == null && !grp.Exists(delegate(EquipChoice c) { return c.Id == id; })) r.Add(id);
            return r;
        }

        public void Arrange(int w)
        {
            int[] x = Cols(w);
            int h = cbId.Height;
            Size = new Size(x[6], h + 6);
            name.SetBounds(2, 3, x[1] - Gap - 2, h);
            name.TextAlign = ContentAlignment.MiddleLeft;
            cbId.SetBounds(x[1], 3, x[2] - x[1] - Gap, h);
            chkAlt.SetBounds(x[2], 3, AltW - 30, h);
            btnAlt.SetBounds(x[2] + AltW - 28, 3, 28, h);
            cbFb.SetBounds(x[3], 3, x[4] - x[3] - Gap, h);
            note.SetBounds(x[4], 3, x[5] - x[4] - Gap, h);
            del.SetBounds(x[5], 3, DelW, h);
            foreach (ComboBox c in new[] { cbId, cbFb }) if (!c.Focused) c.SelectionLength = 0;   // Groessenaenderung markiert sonst den ganzen Text
        }

        string Display(EquipChoice c) { return c.Short == a.Short ? c.Id : c.ToString(); }

        void Refill()
        {
            cbId.Enabled = !chkAlt.Checked; btnAlt.Enabled = chkAlt.Checked;
            if (chkAlt.Checked) { List<string> d = new List<string>(); foreach (EquipChoice c in a.Choices) d.Add(Display(c)); cbId.Text = string.Join(" / ", d.ToArray()); }
            else cbId.Text = a.Choices.Count > 0 ? a.Choices[0].Id : "";
            Warn();
        }

        void Warn()
        {
            EquipDevice dev = a.Alternate || a.Choices.Count == 0 ? null : cat.Master.Device(a.Choices[0].Id);
            cbId.ForeColor = dev != null && !dev.Active ? Color.Firebrick : Known(a, cat) ? SystemColors.WindowText : Color.DarkOrange;   // rot = in Wartung
        }

        string CurrentId() { return a.Alternate || a.Choices.Count == 0 ? "" : a.Choices[0].Id; }

        void SuggestFallbacks()
        {
            string id = CurrentId(); if (id.Length == 0) return;
            string text = cbFb.Text;
            List<string> first = new List<string>();
            foreach (EquipSubst s in cat.Master.SubstitutesFor(id)) first.Add(s.Id);
            foreach (string c in cat.Master.CompatibleWith(id)) { EquipDevice d = cat.Master.Device(c); if (d != null && d.Class == (cat.Master.Device(id) != null ? cat.Master.Device(id).Class : d.Class) && !first.Contains(c)) first.Add(c); }
            if (first.Count == 0) return;
            loading = true;
            for (int i = cbFb.Items.Count - 1; i >= 0; i--) if (first.Contains(cbFb.Items[i].ToString())) cbFb.Items.RemoveAt(i);
            for (int i = first.Count - 1; i >= 0; i--) cbFb.Items.Insert(0, first[i]);
            cbFb.Text = text; loading = false;
        }

        void ShowInfo(ToolTip tip)
        {
            string id = CurrentId(), info = id.Length > 0 ? cat.Master.Info(id) : "";
            foreach (EquipSubst s in id.Length > 0 ? cat.Master.SubstitutesFor(id) : new List<EquipSubst>())
                info += (info.Length > 0 ? "\n" : "") + "Ersatz: " + s.Id + (s.Note.Length > 0 ? " (" + s.Note + ")" : "");
            tip.SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange)." + (info.Length > 0 ? "\n\n" + info : ""));
        }

        void PickAlternatives()
        {
            using (AlternativesPicker p = new AlternativesPicker(cat.Interchangeable(a.Short), a.Choices, a.Short))
                if (p.ShowDialog(this) == DialogResult.OK) a.Choices = p.Result;
            if (a.Choices.Count < 2) { a.Alternate = false; loading = true; chkAlt.Checked = false; loading = false; }
        }
    }

    // Reihum-Verteilung: Geraete ankreuzen, die sich abwechseln. Angeboten werden alle Geraete, die laut Admin-Hinterlegung
    // (Austausch-Gruppen) gleich eingesetzt werden koennen; eigene Eintraege sind moeglich.
    class AlternativesPicker : Form
    {
        readonly CheckedListBox list = new CheckedListBox();
        readonly TextBox own = new TextBox();
        readonly List<EquipChoice> offered;
        readonly string ownShort;
        public List<EquipChoice> Result = new List<EquipChoice>();

        public AlternativesPicker(List<EquipChoice> offered, List<EquipChoice> current, string ownShort)
        {
            this.offered = offered; this.ownShort = ownShort;
            Text = "Abwechselnd verwenden";
            Ui.Dialog(this, 360, 400);
            Label l1 = new Label(); l1.Text = "Geräte ankreuzen, die sich abwechseln (A, B, A, B …). Angeboten werden alle Geräte, die laut Admin gleich eingesetzt werden können:"; l1.SetBounds(10, 8, 340, 48);
            list.SetBounds(10, 60, 340, 220); list.CheckOnClick = true;
            List<string> extras = new List<string>();
            foreach (EquipChoice c in offered) list.Items.Add(c.ToString(), current.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; }));
            foreach (EquipChoice c in current) if (!offered.Exists(delegate(EquipChoice x) { return x.Short == c.Short && x.Id == c.Id; })) extras.Add(c.Id);
            Label l2 = new Label(); l2.Text = "Weitere (Komma getrennt):"; l2.SetBounds(10, 290, 340, 18);
            own.SetBounds(10, 310, 340, 22); own.Text = string.Join(", ", extras.ToArray()); Ui.SimpleMenu(own);
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(150, 356, 95, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(255, 356, 95, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, own, ok, cancel });
        }

        void Commit()
        {
            Result = new List<EquipChoice>();
            for (int i = 0; i < offered.Count; i++) if (list.GetItemChecked(i)) Result.Add(offered[i].Clone());
            foreach (string t in own.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string v = t.Trim();
                if (v.Length > 0 && !Result.Exists(delegate(EquipChoice x) { return x.Id == v; })) Result.Add(new EquipChoice(ownShort, v));
            }
            DialogResult = DialogResult.OK;
        }
    }
}
