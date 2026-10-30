using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Karte eines Puffers, Teil 3: "gilt für" einer Zeile (kleiner Dialog mit Übernehmen/Abbrechen, U3; Esc verwirft) und Menü "+ Position".
    partial class PufferCard
    {
        // "nur dieser Puffer" / "gilt auch für Puffer 2" / "alle Puffer" (aus Sicht dieser Karte)
        string ScopeText(EquipAssign a)
        {
            PufferShare g = step.ShareOf(a);
            if (g == null) return "nur dieser Puffer";
            int m = step.MaskOf(g);
            if (m == step.AllMask && step.Puffers.Count > 2) return "alle Puffer";
            List<string> o = new List<string>();
            for (int k = 0; k < step.Puffers.Count; k++) if (k != Index && ((m >> k) & 1) != 0) o.Add((k + 1).ToString());
            return o.Count == 0 ? "nur dieser Puffer" : "gilt auch für Puffer " + string.Join(", ", o.ToArray());
        }

        List<string> PufferNames()
        {
            List<string> l = new List<string>();
            for (int k = 0; k < step.Puffers.Count; k++) l.Add(PufferName(k) + (step.Puffers[k].Name.Length > 0 ? ": " + step.Puffers[k].Name : ""));
            return l;
        }

        // Nur "Übernehmen" setzt den Geltungsbereich; Abbrechen, Esc und Schließen lassen alles, wie es war
        void ShowScope(EquipAssign a, Button anchor)
        {
            PufferShare g = step.ShareOf(a);
            int mask = g != null ? step.MaskOf(g) : 1 << Index;
            using (PufferScopeForm d = new PufferScopeForm(ShareName(a), PufferNames(), mask))
            {
                Point p = anchor.PointToScreen(new Point(0, anchor.Height));
                d.StartPosition = FormStartPosition.Manual; d.Location = p;
                if (d.ShowDialog(this) == DialogResult.OK && d.Mask != 0 && d.Mask != mask) SetScope(a, d.Mask);
            }
        }

        public void SetScope(EquipAssign a, int mask) { if (step.SetScope(a, mask)) rebuild(); }

        void ShowPosMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            List<string> shorts = new List<string>();
            Baustein b = cat.Baustein_(Catalog.StandardPufferName);
            if (b != null) shorts.AddRange(b.Positions);
            foreach (EquipAssign a in Spec.Lines) if (!shorts.Contains(a.Short)) shorts.Add(a.Short);
            foreach (string sh in shorts)
            {
                string s = sh; EquipPosition ep = cat.Position(sh);
                m.Items.Add((ep != null ? ep.Name + "  (" + sh + ")" : sh), null, delegate { AddLine(s); });
            }
            m.Closed += delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(m.Dispose)); else m.Dispose(); };   // N7: nach dem Klick freigeben
            m.Show(bPos, new Point(0, bPos.Height));
        }
    }

    // U3: "gilt für" als kleiner Dialog: je Puffer ein Haken, Übernehmen (mind. ein Puffer) oder Abbrechen/Esc (nichts ändert sich)
    class PufferScopeForm : Form
    {
        readonly List<CheckBox> boxes = new List<CheckBox>();
        readonly Button ok = new Button();

        public PufferScopeForm(string what, List<string> puffers, int mask)
        {
            Text = "Gilt für";
            int rowH = Theme.Current.ButtonH, h = 46 + puffers.Count * (rowH + 2) + 50;
            Ui.Dialog(this, 300, h, false);
            Label l = new Label(); l.Text = "Für welche Puffer gilt „" + what + "“? Mehrere Puffer = eine gemeinsame Zeile."; l.UseMnemonic = false;
            l.SetBounds(10, 8, 280, 2 * Font.Height + 4); Controls.Add(l);
            int y = 14 + l.Height;
            for (int k = 0; k < puffers.Count; k++)
            {
                CheckBox c = new CheckBox(); c.Text = puffers[k]; c.UseMnemonic = false; c.AutoEllipsis = true; c.Checked = ((mask >> k) & 1) != 0;
                c.SetBounds(14, y, 270, rowH); y += rowH + 2;
                c.CheckedChanged += delegate { ok.Enabled = Mask != 0; };
                boxes.Add(c); Controls.Add(c);
            }
            ok.Text = "Übernehmen"; ok.SetBounds(100, y + 10, 100, 28); ok.DialogResult = DialogResult.OK;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(206, y + 10, 84, 28); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            ClientSize = new Size(300, y + 48);
            ok.Enabled = Mask != 0;
        }

        // Angehakte Puffer als Bit-Maske (Bit k = Puffer k+1)
        public int Mask { get { int m = 0; for (int k = 0; k < boxes.Count; k++) if (boxes[k].Checked) m |= 1 << k; return m; } }

#if TESTHOOKS
        internal List<CheckBox> TestBoxes { get { return boxes; } }
        internal bool TestOkEnabled { get { return ok.Enabled; } }
        internal void TestEsc() { ProcessDialogKey(Keys.Escape); }
#endif
    }
}
