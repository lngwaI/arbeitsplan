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

        // Zeile entfernen ("nicht benötigt"): eigene Zeile eines Puffers, gemeinsame Zeile oder Zeile des alten Modus
        public bool RemoveEquip(EquipAssign a)
        {
            if (Equipment.Remove(a)) return true;
            foreach (PufferSpec p in Puffers) if (p.Lines.Remove(a)) return true;
            PufferShare g = ShareOf(a);
            return g != null && PufferShared.Remove(g);
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
