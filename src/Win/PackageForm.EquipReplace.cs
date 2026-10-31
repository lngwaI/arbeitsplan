using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Equipment-Leiste: "Ersetzen durch ▾" tauscht die unbekannte Nummer in allen Zeilen des aktuellen Schritts (auch Puffer-Karten) gegen ein
    // bekanntes Geraet. Menue: erlaubte Geraete der Position (Produkt), darunter alle weiteren Geraete; aehnlichste Schreibweise zuerst.
    // Geht auch ohne Recht "Pakete einrichten" (der Katalog bleibt unveraendert).
    partial class PackageForm
    {
        const int ReplaceMore = 25;

        // Einfache Levenshtein-Distanz ohne Beachtung der Gross-/Kleinschreibung
        internal static int Lev(string a, string b)
        {
            a = a.ToUpperInvariant(); b = b.ToUpperInvariant();
            int[] p = new int[b.Length + 1], q = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) p[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                q[0] = i;
                for (int j = 1; j <= b.Length; j++) q[j] = Math.Min(Math.Min(q[j - 1], p[j]) + 1, p[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                int[] t = p; p = q; q = t;
            }
            return p[b.Length];
        }

        static List<string> BySimilar(string id, List<string> l)
        {
            List<string> r = new List<string>(l);
            r.Sort(delegate(string x, string y) { int c = Lev(id, x).CompareTo(Lev(id, y)); return c != 0 ? c : string.Compare(x, y, StringComparison.OrdinalIgnoreCase); });
            return r;
        }

        // Menueeintraege: erlaubte Geraete der Position, danach alle uebrigen Geraete (Anzahl begrenzt)
        internal void ReplaceChoices(Unk u, out List<string> allowed, out List<string> more)
        {
            allowed = BySimilar(u.Id, cat.Allowed(Product, u.Short));
            List<string> rest = new List<string>();
            foreach (EquipDevice d in cat.Master.Devices) if (!allowed.Contains(d.Id) && !string.Equals(d.Id, u.Id, StringComparison.OrdinalIgnoreCase)) rest.Add(d.Id);
            more = BySimilar(u.Id, rest);
        }

        void ReplaceMenu(Control anchor)
        {
            if (barTarget == null) return;
            Unk u = barTarget;
            List<string> allowed, more; ReplaceChoices(u, out allowed, out more);
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Ersetzen durch (" + u.Short + "):").Enabled = false;
            if (allowed.Count == 0) m.Items.Add("keine erlaubten Geräte für diese Position").Enabled = false;
            foreach (string id in allowed) { string nid = id; m.Items.Add(id, null, delegate { ReplaceId(u.Id, nid); }); }
            if (more.Count > 0)
            {
                ToolStripMenuItem sub = new ToolStripMenuItem("Weitere Geräte");
                for (int i = 0; i < more.Count && i < ReplaceMore; i++) { string nid = more[i]; sub.DropDownItems.Add(nid, null, delegate { ReplaceId(u.Id, nid); }); }
                m.Items.Add(new ToolStripSeparator()); m.Items.Add(sub);
            }
            m.Closed += delegate { BeginInvoke(new Action(m.Dispose)); };
            m.Show(anchor, new Point(0, anchor.Height));
        }

        // Nummer in allen Zeilen des aktuellen Schritts ersetzen (Auswahl und Ausweich-Gerät); true = etwas geaendert
        internal bool ReplaceId(string oldId, string newId)
        {
            if (cur == null || string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase)) return false;
            bool any = false;
            foreach (EquipAssign a in cur.AllEquip())
            {
                foreach (EquipChoice c in a.Choices) if (string.Equals(c.Id, oldId, StringComparison.OrdinalIgnoreCase)) { c.Id = newId; any = true; }
                if (string.Equals(a.Fallback, oldId, StringComparison.OrdinalIgnoreCase)) { a.Fallback = newId; any = true; }
            }
            if (!any) return false;
            dirty = true; Bind(cur); CommitUndo();
            return true;
        }
    }
}
