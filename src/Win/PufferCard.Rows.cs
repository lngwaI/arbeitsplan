using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Karte eines Puffers, Teil 2 (UX-5e): Zeilen der Karte. Zeilen kommen aus dem Pool der Karte (RowPool) und werden beim Hinzufuegen,
    // Entfernen und Verschieben einzeln angepasst, ohne die Karte neu zu bauen.
    partial class PufferCard
    {
        void AddRow(EquipAssign a, PufferShare g)
        {
            Row r = new Row(); r.A = a;
            if (g != null && step.OwnerOf(g) != Index)
            {   // gemeinsame Zeile, die in einer anderen Karte bearbeitet wird: nur anzeigen
                r.Info = new Label(); r.Info.AutoEllipsis = true; r.Info.TextAlign = ContentAlignment.MiddleLeft; r.Info.UseMnemonic = false; r.Info.ForeColor = Section.HelpColor;
                Controls.Add(r.Info); rows.Add(r); return;
            }
            r.R = pool.Get(a, cat, product, changed, RemoveLine, true);
            reorder.Add(r.R);
            r.R.AddBelow = g == null ? (Action<EquipAssign>)AddLineBelow : null;   // gemeinsame Zeilen: kein "+" (Setter blendet ihn aus, auch bei neuer Zeile)
            if (a.Short.Equals(ShareShort, StringComparison.OrdinalIgnoreCase) || g != null)
            {
                r.ScopeLbl = new Label(); r.ScopeLbl.Text = "gilt für:"; r.ScopeLbl.TextAlign = ContentAlignment.MiddleRight; r.ScopeLbl.UseMnemonic = false;
                r.ScopeBtn = new Button(); r.ScopeBtn.TextAlign = ContentAlignment.MiddleLeft; Theme.Current.StyleButton(r.ScopeBtn);
                tip.SetToolTip(r.ScopeBtn, "Für welche Puffer dieses Gerät gilt (z. B. ein gemeinsamer WFI-Tank für beide Puffer). Mehrere Puffer = eine gemeinsame Zeile.");
                EquipAssign ca = a; Button cb = r.ScopeBtn;
                r.ScopeBtn.Click += delegate { ShowScope(ca, cb); };
                Controls.Add(r.ScopeLbl); Controls.Add(r.ScopeBtn);
            }
            rows.Add(r);
        }

        // Bereich einer Zeile samt "gilt für"-Zeile darunter (fuer die Einfuegelinie beim Ziehen)
        Rectangle SlotBounds(int i)
        {
            Row r = rows[i]; Control c = r.R != null ? (Control)r.R : r.Info;
            return new Rectangle(c.Left, c.Top, c.Width, (r.ScopeBtn != null ? r.ScopeBtn.Bottom : c.Bottom) - c.Top);
        }

        // Zeile der Karte entfernen: nur diese Zeile verschwindet (gemeinsame Zeilen stehen in mehreren Karten: dann alles neu)
        void RemoveLine(EquipAssign a)
        {
            bool shared = step.ShareOf(a) != null;
            step.RemoveEquip(a);
            int i = rows.FindIndex(delegate(Row x) { return x.A == a; });
            if (shared || i < 0) { rebuild(); return; }
            DropRow(rows[i]); rows.RemoveAt(i);
            Resync(); structure();
        }

        void DropRow(Row r)
        {
            if (r.R != null) pool.Release(r.R);
            foreach (Control c in new Control[] { r.Info, r.ScopeLbl, r.ScopeBtn }) if (c != null) { Controls.Remove(c); c.Dispose(); }
        }

        // Reihenfolge der Zeilen im RowReorder wieder wie in rows
        void Resync() { reorder.Clear(); foreach (Row r in rows) if (r.R != null) reorder.Add(r.R); }

        // Zeile von from nach to verschieben: Modell, dann nur die Reihenfolge der vorhandenen Steuerelemente (keine neuen)
        void MoveRow(int from, int to)
        {
            int own = Spec.Lines.Count, total = rows.Count;
            if (from < 0 || from >= total) return;
            to = from < own ? Math.Max(0, Math.Min(own - 1, to)) : Math.Max(own, Math.Min(total - 1, to));
            if (to == from || !step.MoveEquip(Index, from, to)) return;
            if (from >= own) { rebuild(); return; }   // N7: gemeinsame Zeile = globale Reihenfolge, andere Karten zeigen sie auch: alles neu binden
            Row x = rows[from]; rows.RemoveAt(from); rows.Insert(to, x);
            Resync(); structure();
        }

        // "+": dieselbe Position direkt unter der Zeile (eigene Zeilen stehen in rows an derselben Stelle wie in Spec.Lines)
        internal void AddLineBelow(EquipAssign src)
        {
            int i = Spec.Lines.IndexOf(src);
            if (i < 0) return;
            EquipAssign a = EquipRow.Below(src, cat); Spec.Lines.Insert(i + 1, a);
            AddRow(a, null);
            Row nr = rows[rows.Count - 1]; rows.RemoveAt(rows.Count - 1); rows.Insert(i + 1, nr);
            Resync(); structure();
            if (nr.R != null) nr.R.FocusNew();
        }

        public void AddLine(string shortName)
        {
            EquipAssign a = new EquipAssign(); a.Short = shortName; Spec.Lines.Add(a);
            AddRow(a, null);
            Row nr = rows[rows.Count - 1]; rows.RemoveAt(rows.Count - 1); rows.Insert(Spec.Lines.Count - 1, nr);   // eigene Zeilen stehen vor den gemeinsamen
            Resync(); structure();
        }

    }
}
