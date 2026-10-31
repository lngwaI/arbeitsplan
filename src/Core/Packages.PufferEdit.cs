using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // UX-1b: Bearbeiten der Puffer eines Schritts (Modell ohne UI): Geltungsbereich gemeinsamer Zeilen, Puffer/Zeile entfernen.
    public partial class PackageStep
    {
        public int AllMask { get { return (1 << Math.Min(Puffers.Count, 30)) - 1; } }

        // Puffer, fuer die eine gemeinsame Zeile gilt, als Bit-Maske (Scope 0 = alle)
        public int MaskOf(PufferShare g) { return g.Scope == 0 ? AllMask : g.Scope & AllMask; }

        // Puffer, in dessen Karte die gemeinsame Zeile bearbeitet wird (der erste, den sie betrifft); -1 = keiner
        public int OwnerOf(PufferShare g)
        {
            int m = MaskOf(g);
            for (int k = 0; k < Puffers.Count; k++) if (((m >> k) & 1) != 0) return k;
            return -1;
        }

        public PufferShare ShareOf(EquipAssign a) { return PufferShared.Find(delegate(PufferShare g) { return g.Eq == a; }); }

        // Alle Equipment-Zeilen des Schritts: alter Modus, eigene Zeilen der Puffer, gemeinsame Zeilen
        public List<EquipAssign> AllEquip()
        {
            List<EquipAssign> r = new List<EquipAssign>(Equipment);
            foreach (PufferSpec p in Puffers) r.AddRange(p.Lines);
            foreach (PufferShare g in PufferShared) r.Add(g.Eq);
            return r;
        }

        // Puffer entfernen; die Bits der gemeinsamen Zeilen rutschen nach, Zeilen ohne verbleibenden Puffer entfallen. Der letzte Puffer bleibt.
        public bool RemovePuffer(int k)
        {
            if (k < 0 || k >= Puffers.Count || Puffers.Count < 2) return false;
            Puffers.RemoveAt(k);
            for (int i = PufferShared.Count - 1; i >= 0; i--)
            {
                PufferShare g = PufferShared[i];
                if (g.Scope == 0) continue;
                int m = (g.Scope & ((1 << k) - 1)) | ((g.Scope >> (k + 1)) << k);
                if (m == 0) PufferShared.RemoveAt(i); else g.Scope = m;
            }
            for (int i = 0; i < Puffers.Count; i++) if (Puffers[i].NameNo > 0) Puffers[i].NameNo = i == 0 ? 0 : i + 1;   // "Titel 3" rutscht nach: Nummern bleiben fortlaufend
            SyncPuffers();
            return true;
        }

        // U4: Puffer from an die Stelle to verschieben (to = Index nach dem Verschieben). Geltungsbereiche gemeinsamer Zeilen ziehen mit (Bits
        // werden umsortiert, Scope 0 = alle bleibt), unbenannte "Titel 2"-Nummern folgen der neuen Stelle. Plan-Ausgabe/XML folgen der Liste.
        public bool MovePuffer(int from, int to)
        {
            int n = Puffers.Count;
            if (from < 0 || from >= n || to < 0 || to >= n || from == to || n > 30) return false;
            List<int> old = new List<int>(); for (int i = 0; i < n; i++) old.Add(i);
            MoveItem(old, from, to); MoveItem(Puffers, from, to);   // old[neu] = alter Index
            foreach (PufferShare g in PufferShared)
            {
                if (g.Scope == 0) continue;
                int m = 0;
                for (int i = 0; i < n; i++) if (((g.Scope >> old[i]) & 1) != 0) m |= 1 << i;
                g.Scope = m;
            }
            if (Puffers.Exists(delegate(PufferSpec p) { return p.NameNo > 1; }))
                for (int i = 0; i < n; i++) if (Puffers[i].Name.Length == 0) Puffers[i].NameNo = i == 0 ? 0 : i + 1;
            SyncPuffers();
            return true;
        }

        // Zeile entfernen ("nicht benötigt"): eigene Zeile eines Puffers, gemeinsame Zeile oder Zeile des alten Modus
        public bool RemoveEquip(EquipAssign a)
        {
            if (Equipment.Remove(a)) return true;
            foreach (PufferSpec p in Puffers) if (p.Lines.Remove(a)) return true;
            PufferShare g = ShareOf(a);
            return g != null && PufferShared.Remove(g);
        }

        // Element von from nach to verschieben (to = Index nach dem Verschieben); false = nichts zu tun
        public static bool MoveItem<T>(List<T> l, int from, int to)
        {
            if (from < 0 || from >= l.Count || to < 0 || to >= l.Count || from == to) return false;
            T x = l[from]; l.RemoveAt(from); l.Insert(to, x);
            return true;
        }

        // Anzahl eigener Zeilen und gemeinsamer Zeilen im Geltungsbereich von Puffer k (Anzeigereihenfolge der Karte: erst eigene, dann gemeinsame)
        public int OwnLines(int k) { return k >= 0 && k < Puffers.Count ? Puffers[k].Lines.Count : 0; }

        public int SharedLines(int k) { int n = 0; foreach (PufferShare g in PufferShared) if (g.Covers(k)) n++; return n; }

        // UX-5b: Zeile in der Karte von Puffer k verschieben (from/to = Index in der Anzeige: eigene Zeilen, dann gemeinsame). Eigene Zeilen bleiben
        // unter den eigenen, gemeinsame unter den gemeinsamen (to wird in die Gruppe geklemmt). Gemeinsame Zeilen aendern die Reihenfolge in PufferShared
        // nur zwischen den Zeilen, die den Puffer betreffen; Geltungsbereich und Gerate bleiben unveraendert. Gibt true zurueck, wenn sich etwas geaendert hat.
        public bool MoveEquip(int k, int from, int to)
        {
            if (k < 0 || k >= Puffers.Count) return false;
            int own = Puffers[k].Lines.Count, total = own + SharedLines(k);
            if (from < 0 || from >= total) return false;
            if (from < own) return MoveItem(Puffers[k].Lines, from, Math.Max(0, Math.Min(own - 1, to)));
            int fr = from - own, tr = Math.Max(own, Math.Min(total - 1, to)) - own;
            if (fr == tr) return false;
            List<int> slots = new List<int>(); List<PufferShare> cov = new List<PufferShare>();
            for (int i = 0; i < PufferShared.Count; i++) if (PufferShared[i].Covers(k)) { slots.Add(i); cov.Add(PufferShared[i]); }
            foreach (PufferShare g in cov) g.After = -1;   // P6: manuell sortiert = am Ende
            MoveItem(cov, fr, tr);
            for (int i = 0; i < slots.Count; i++) PufferShared[slots[i]] = cov[i];
            return true;
        }

        // Geltungsbereich einer Zeile setzen (mask: Bit k = Puffer k+1). Ein Puffer = eigene Zeile dieses Puffers, mehrere = gemeinsame Zeile.
        public bool SetScope(EquipAssign a, int mask)
        {
            mask &= AllMask;
            if (mask == 0) return false;
            PufferShare g = ShareOf(a);
            int own = -1;
            for (int k = 0; k < Puffers.Count; k++) if (Puffers[k].Lines.Contains(a)) own = k;
            if (g == null && own < 0) return false;
            if ((mask & (mask - 1)) == 0)
            {
                int j = 0; while (((mask >> j) & 1) == 0) j++;
                if (g != null) { PufferShared.Remove(g); Puffers[j].Lines.Add(a); }
                else if (own != j) { Puffers[own].Lines.Remove(a); Puffers[j].Lines.Add(a); }
            }
            else if (g != null) g.Scope = g.Scope == 0 && mask == AllMask ? 0 : mask;
            else { Puffers[own].Lines.Remove(a); g = new PufferShare(); g.Eq = a; g.Scope = mask; PufferShared.Add(g); }
            return true;
        }
    }
}
