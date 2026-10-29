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
        Label lAnsatz, lGebinde, lMenge, lPuf, lPufNames, lPufHelp;
        readonly NumericUpDown nPuf = new NumericUpDown();
        readonly TextBox tPufNames = new TextBox();

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

            // Mehrere Puffer je Termin (PB): Anzahl und Namen
            lPuf = Lbl(gWhat, "Anzahl Puffer:", 8, lw); Num(nPuf, 1, 9, x, 60, gWhat);
            lPufNames = Lbl(gWhat, "Namen:", 8, lw); lPufNames.TextAlign = ContentAlignment.TopLeft;
            tPufNames.Multiline = true; tPufNames.AcceptsReturn = true; tPufNames.ScrollBars = ScrollBars.Vertical; tPufNames.SetBounds(x, 0, w, 3 * Font.Height + 10);
            lPufHelp = Lbl(gWhat, "Eine Zeile je Puffer; leer = Titel + Nr.", x, w); lPufHelp.AutoEllipsis = true;
            gWhat.Controls.Add(tPufNames); Grow(tPufNames, lPufHelp); Gray(lPufHelp); Ui.SimpleMenu(tPufNames);
            tip.SetToolTip(nPuf, "So viele Puffer dieses Typs entstehen bei jedem Termin am selben Tag (z. B. 3 mobile Puffer). Die Equipment-Zeilen gelten für jeden Puffer, bei „abwechselnd“ reihum.");
            tip.SetToolTip(tPufNames, "Je Zeile der Name eines Puffers (1. Zeile = Puffer 1). Fehlt ein Name: Titel, ab Puffer 2 „Titel 2“ usw. Nur bei Bedarf ausfüllen.");
            nPuf.ValueChanged += delegate { if (Edit()) { cur.PufferCount = (int)nPuf.Value; Changed(); } };
            tPufNames.TextChanged += delegate { if (Edit()) { cur.PufferNames = JoinNames(tPufNames.Text); Changed(); } };
        }

        // Textfeld (je Zeile ein Name) -> "a|b|c" (ohne "|" in Namen, ohne leere Zeilen am Ende)
        static string JoinNames(string text)
        {
            List<string> l = new List<string>();
            foreach (string t in text.Replace("\r", "").Split('\n')) l.Add(t.Replace('|', '/').Trim());
            while (l.Count > 0 && l[l.Count - 1].Length == 0) l.RemoveAt(l.Count - 1);
            return string.Join("|", l.ToArray());
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
            nPuf.Maximum = Math.Max(9, s.PufferCount); nPuf.Value = Math.Max(1, s.PufferCount);   // >9 (nur aus Dateien) bleibt sichtbar, die Pruefung sperrt es
            tPufNames.Text = string.Join("\r\n", s.BufferNames());
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

        // Anzahl Puffer (immer bei Puffer-Schritten); Namen, sobald mehrere Puffer oder Namen gesetzt sind (nichts verschwindet still)
        int LayoutPuffer(int y, PackageStep s)
        {
            bool buf = s.IsBuffer, names = buf && (s.PufferCount > 1 || s.PufferNames.Length > 0);
            y = Row(y, buf, lPuf, nPuf);
            y = Row(y, names, lPufNames, tPufNames);
            return Row(y, names, lPufHelp);
        }
    }
}