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
        // (bei Puffern je Puffer die Tank-Positionen des Standardpuffers, wo der Puffer noch keine hat)
        void SetAnsatz(PackageStep s, string key)
        {
            if (s.Ansatz == key) return;
            s.Ansatz = key;
            Predicate<EquipAssign> emptyTank = delegate(EquipAssign a) { return PackageStep.IsTank(cat, a.Short) && a.Line(0).Length == 0; };
            if (key.Length > 0)
            {
                s.Equipment.RemoveAll(emptyTank);
                foreach (PufferSpec p in s.Puffers) p.Lines.RemoveAll(emptyTank);
                s.PufferShared.RemoveAll(delegate(PufferShare g) { return emptyTank(g.Eq); });
            }
            else if (s.Puffers.Count > 0)
            {
                PufferTemplate std = cat.StandardPuffer();
                foreach (PufferSpec p in s.Puffers)
                    if (std != null) foreach (PufferTemplatePos tp in std.Positions)
                        if (PackageStep.IsTank(cat, tp.Short) && !p.Lines.Exists(delegate(EquipAssign a) { return a.Short == tp.Short; }) && !s.PufferShared.Exists(delegate(PufferShare g) { return g.Eq.Short == tp.Short; }))
                        { EquipAssign a = new EquipAssign(); a.Short = tp.Short; p.Lines.Add(a); }
            }
            else
            {
                Baustein b = cat.Baustein_(s.Source);
                if (b != null)
                    foreach (string p in b.Positions)
                        if (PackageStep.IsTank(cat, p) && !s.Equipment.Exists(delegate(EquipAssign a) { return a.Short == p; }))
                            for (int i = 0, n = b.DefaultCount(p); i < n; i++) { EquipAssign a = new EquipAssign(); a.Short = p; s.Equipment.Add(a); }
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


        // Puffer-Typ: nur Anzeige (kommt aus dem Baustein). Alte Schritte mit "mobil|stationaer" zeigen beide Typen und liefern weiter zwei Puffer.
        void FillVariants(PackageStep s)
        {
            pVar.SuspendLayout();
            while (pVar.Controls.Count > 0) { Control c = pVar.Controls[0]; pVar.Controls.RemoveAt(0); c.Dispose(); }
            if (s.IsBuffer)
            {
                string t = VarText(s.Variant);
                if (VariantSet.Of(s.Variant).Count > 1) t += "  (alter Schritt: zwei Puffer)";
                Label l = new Label(); l.Text = t; l.AutoSize = true; l.UseMnemonic = false;
                pVar.Controls.Add(l);
            }
            pVar.ResumeLayout();
        }

        int LayoutVariants(int y, bool show)
        {
            lVar.Visible = pVar.Visible = show;
            if (!show || pVar.Controls.Count == 0) return y;
            Control l = pVar.Controls[0];
            lVar.Top = y; pVar.SetBounds(tTitle.Left, y, Math.Max(80, tTitle.Width), IH);
            l.Location = new Point(0, (IH - l.PreferredSize.Height) / 2);
            return y + IH + 6;
        }
    }
}