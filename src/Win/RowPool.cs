using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // UX-5e: Zeilen des Paket-Editors (EquipRow) werden wiederverwendet statt neu gebaut: Steuerelemente samt Fensterhandles anlegen (~90 ms je Zeile)
    // und Umhaengen in einen anderen Container (~25 ms) sind teuer, Umbinden auf eine andere Zeile (EquipRow.Rebind) und Aus-/Einblenden kosten fast
    // nichts. Darum gehoert jedem Container (Karte, Zeilenliste) ein eigener Pool: freigegebene Zeilen bleiben versteckt in diesem Container.
    class RowPool
    {
        const int Max = 24;
        readonly Control host;
        readonly List<EquipRow> spare = new List<EquipRow>();
        public int Created, Reused;   // Zaehler (Selbsttest)

        public RowPool(Control host) { this.host = host; }

        // Zeile fuer a: eine versteckte freie oder eine neue (im Container, noch unsichtbar angeordnet); Sichtbarkeit setzt der Aufrufer
        public EquipRow Get(EquipAssign a, Catalog cat, string product, Action changed, Action<EquipAssign> remove, bool allowNone)
        {
            spare.RemoveAll(delegate(EquipRow x) { return x.IsDisposed; });
            if (spare.Count > 0)
            {
                EquipRow r = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1);
                r.Rebind(a, cat, product, changed, remove, allowNone); Reused++;
                return r;
            }
            Created++;
            EquipRow n = new EquipRow(a, cat, product, changed, remove, allowNone);
            host.Controls.Add(n);
            return n;
        }

        // Zeile verstecken und fuer spaeter aufbewahren (bleibt im Container)
        public void Release(EquipRow r)
        {
            if (r == null || r.IsDisposed) return;
            r.Visible = false;
            if (spare.Contains(r)) return;
            if (spare.Count < Max) spare.Add(r);
            else { if (r.Parent != null) r.Parent.Controls.Remove(r); r.Dispose(); }
        }
    }
}
