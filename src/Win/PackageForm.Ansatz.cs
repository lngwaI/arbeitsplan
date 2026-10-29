using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Schritt-Details: Herstellung (Anzahl je Variante der Zeile, RowInfo.Variants) und Ansatz (Tank / Flasche-Gebinde / ohne).
    partial class PackageForm
    {
        readonly ComboBox cbAnsatz = new ComboBox(), cbUnit = new ComboBox();
        readonly TextBox tGebinde = new TextBox(), tMenge = new TextBox();
        readonly Panel pVar = new Panel();
        Label lAnsatz, lGebinde, lMenge;

        // ---------- Herstellung (Anzahl je Variante) und Ansatz ----------

        void BuildAnsatz(int lw, int x, int w)
        {
            pVar.Tag = "keep"; gWhat.Controls.Add(pVar);
            lAnsatz = Lbl(gWhat, "Ansatz:", 8, lw); cbAnsatz.SetBounds(x, 0, w, IH); cbAnsatz.DropDownStyle = ComboBoxStyle.DropDownList; cbAnsatz.Items.AddRange(PackageStep.AnsatzNames);
            lGebinde = Lbl(gWhat, "Gebinde:", 8, lw); tGebinde.SetBounds(x, 0, w, IH);
            lMenge = Lbl(gWhat, "Menge:", 8, lw); tMenge.SetBounds(x, 0, 60, IH); cbUnit.SetBounds(x + 66, 0, 70, IH); cbUnit.Items.AddRange(new object[] { "L", "mL", "kg", "g", "Stk" });
            gWhat.Controls.AddRange(new Control[] { cbAnsatz, tGebinde, tMenge, cbUnit });
            Grow(cbAnsatz, tGebinde); Ui.SimpleMenu(tGebinde); Ui.SimpleMenu(tMenge);
            tip.SetToolTip(cbAnsatz, "Tank: Vorlagetank/WFI-Tank als Equipment. Flasche/Gebinde oder ohne Gebinde: kein Tank nötig, Menge angeben.");
            tip.SetToolTip(tGebinde, "Bezeichnung, z. B. Flasche, Kanister, Tween-Gebinde");
            cbAnsatz.SelectedIndexChanged += delegate { if (Edit() && cbAnsatz.SelectedIndex >= 0) SetAnsatz(cur, PackageStep.AnsatzKeys[cbAnsatz.SelectedIndex]); };
            tGebinde.TextChanged += delegate { if (Edit()) { cur.Gebinde = tGebinde.Text.Trim(); Changed(); } };
            tMenge.TextChanged += delegate { if (Edit()) { cur.Menge = tMenge.Text.Trim(); Changed(); } };
            cbUnit.TextChanged += delegate { if (Edit()) { cur.Einheit = cbUnit.Text.Trim(); Changed(); } };
        }

        // Ohne Tank: leere Tank-Zeilen fallen weg (keine Hinweise); zurueck auf Tank: Tank-Positionen des Bausteins wieder anbieten
        void SetAnsatz(PackageStep s, string key)
        {
            if (s.Ansatz == key) return;
            s.Ansatz = key;
            if (key.Length > 0) s.Equipment.RemoveAll(delegate(EquipAssign a) { return PackageStep.IsTank(cat, a.Short) && a.Line(0).Length == 0; });
            else
            {
                Baustein b = cat.Baustein_(s.Source);
                if (b != null)
                    foreach (string p in b.Positions)
                        if (PackageStep.IsTank(cat, p) && !s.Equipment.Exists(delegate(EquipAssign a) { return a.Short == p; })) { EquipAssign a = new EquipAssign(); a.Short = p; s.Equipment.Add(a); }
            }
            dirty = true;
            Bind(s);
        }

        void BindAnsatz(PackageStep s)
        {
            cbAnsatz.SelectedIndex = Math.Max(0, Array.IndexOf(PackageStep.AnsatzKeys, s.Ansatz));
            tGebinde.Text = s.Gebinde; tMenge.Text = s.Menge; cbUnit.Text = s.Einheit;
        }

        int LayoutAnsatz(int y, PackageStep s)
        {
            bool show = s.UsesAnsatz;
            y = Row(y, show, lAnsatz, cbAnsatz);
            y = Row(y, show && s.Ansatz == "gebinde", lGebinde, tGebinde);
            return Row(y, show && s.Ansatz.Length > 0, lMenge, tMenge, cbUnit);
        }

        // Herstellung: je Variante der Zeile ein Feld "Anzahl" (0 = nicht); gesetzte, an der Zeile unbekannte Varianten bleiben sichtbar
        void FillVariants(PackageStep s)
        {
            string[] vs; if (!rowVars.TryGetValue(s.Row, out vs)) vs = new string[0];
            List<string> names = new List<string>(vs);
            foreach (string v in VariantSet.Of(s.Variant)) if (!names.Contains(v)) names.Add(v);
            pVar.SuspendLayout();
            while (pVar.Controls.Count > 0) { Control c = pVar.Controls[0]; pVar.Controls.RemoveAt(0); c.Dispose(); }
            foreach (string v in names)
            {
                string name = v;
                NumericUpDown n = new NumericUpDown(); n.Minimum = 0; n.Maximum = VariantSet.Max; n.Width = 44; n.Height = IH; n.TextAlign = HorizontalAlignment.Right;
                n.Value = VariantSet.Count(s.Variant, v); n.Tag = v;
                n.ValueChanged += delegate { if (Edit()) { cur.Variant = VariantSet.With(cur.Variant, name, (int)n.Value); Changed(); } };
                Label l = new Label(); l.Text = "× " + VarText(v); l.AutoSize = true; l.UseMnemonic = false;
                tip.SetToolTip(n, "Wie oft „" + VarText(v) + "“ an diesem Tag hergestellt wird (0 = nicht)");
                pVar.Controls.Add(n); pVar.Controls.Add(l);
            }
            pVar.ResumeLayout();
        }

        // Felder nebeneinander, bei Platzmangel umbrechen; liefert die naechste y-Position
        int LayoutVariants(int y, bool show)
        {
            lVar.Visible = pVar.Visible = show;
            if (!show) return y;
            int x = tTitle.Left, w = Math.Max(80, tTitle.Width), px = 0, py = 0;
            for (int i = 0; i + 1 < pVar.Controls.Count; i += 2)
            {
                Control n = pVar.Controls[i], l = pVar.Controls[i + 1];
                int pw = n.Width + 4 + l.PreferredSize.Width + 10;
                if (px > 0 && px + pw > w) { px = 0; py += IH + 4; }
                n.Location = new Point(px, py); l.Location = new Point(px + n.Width + 4, py + (IH - l.PreferredSize.Height) / 2);
                px += pw;
            }
            lVar.Top = y; pVar.SetBounds(x, y, w, py + IH + 1);
            return y + py + IH + 6;
        }
    }
}
