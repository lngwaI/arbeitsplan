using System;
using System.Collections.Generic;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Positionsliste von "Produkte & Equipment": jede Position, davor je Equipment-Gruppe (cat.Groups, z. B. "Freezetanks": FZ1, FZ2) ein Eintrag
    // "▸ Freezetanks (FZ1, FZ2) – gemeinsame Liste" (Positionsgruppe). Ein Gruppeneintrag bearbeitet EINE Liste, die in die ProfilePos aller Positionen der Gruppe
    // geschrieben wird (kein eigenes Datenfeld).
    partial class ProductForm
    {
        class PosItem
        {
            public List<EquipPosition> Ps = new List<EquipPosition>(); public ProductProfile Pr; public string GroupName = "";
            public bool IsGroup { get { return GroupName.Length > 0; } }
            public string Key { get { return IsGroup ? "g:" + GroupName : Ps[0].Short; } }
            public string Shorts { get { return string.Join(", ", Ps.ConvertAll(delegate(EquipPosition x) { return x.Short; }).ToArray()); } }
            public override string ToString()
            {
                if (IsGroup) return "▸ " + GroupName + " (" + Shorts + ") – gemeinsame Liste";
                ProfilePos pp = Pr != null ? Pr.Pos(Ps[0].Short) : null;
                string tag = pp == null ? "" : (pp.Own ? "  ● eigen" : "") + (pp.Fixed ? "  ● fest" : "");
                return Ps[0].Name + " (" + Ps[0].Short + ")" + tag;
            }
        }

        // Positionen der Gruppe in Katalogreihenfolge (nur vorhandene)
        List<EquipPosition> Members(EquipGroup eg)
        {
            List<EquipPosition> r = new List<EquipPosition>();
            foreach (EquipPosition e in cat.Positions) if (eg.Shorts.Exists(delegate(string s) { return string.Equals(s, e.Short, StringComparison.OrdinalIgnoreCase); })) r.Add(e);
            return r;
        }

        List<PosItem> BuildItems(ProductProfile p)
        {
            List<PosItem> r = new List<PosItem>();
            foreach (EquipPosition e in cat.Positions)
            {
                foreach (EquipGroup eg in cat.Groups)
                {
                    List<EquipPosition> m = Members(eg);
                    if (m.Count < 2 || m[0] != e) continue;
                    PosItem gi = new PosItem(); gi.Ps = m; gi.Pr = p; gi.GroupName = eg.Name.Length > 0 ? eg.Name : "Positionsgruppe"; r.Add(gi);
                }
                PosItem pi = new PosItem(); pi.Ps.Add(e); pi.Pr = p; r.Add(pi);
            }
            return r;
        }

        // Standardgeraete aller Positionen des Eintrags (Vereinigung)
        List<string> StdUnion()
        {
            List<string> r = new List<string>();
            foreach (EquipPosition e in cur.Ps) foreach (string id in e.Ids) if (!r.Contains(id)) r.Add(id);
            return r;
        }

        // Gruppeneintrag: weichen die Standardlisten der Positionen voneinander ab?
        bool StdDiffers()
        {
            if (cur == null || !cur.IsGroup) return false;
            List<string> a = cur.Ps[0].Ids;
            foreach (EquipPosition e in cur.Ps) if (e.Ids.Count != a.Count || !e.Ids.TrueForAll(delegate(string x) { return a.Contains(x); })) return true;
            return false;
        }

        static bool SameProfile(ProfilePos a, ProfilePos b)
        {
            bool ao = a != null && a.Own, bo = b != null && b.Own;
            if (ao != bo || (a != null && a.Fixed) != (b != null && b.Fixed)) return false;
            if (!ao) return true;
            return a.Ids.Count == b.Ids.Count && a.Ids.TrueForAll(delegate(string x) { return b.Ids.Contains(x); });
        }

        // Gruppeneintrag: weichen die Positionen beim Produkt voneinander ab?
        bool GroupDiffers()
        {
            if (prod == null || cur == null || !cur.IsGroup) return false;
            ProfilePos r = prod.Pos(cur.Ps[0].Short);
            foreach (EquipPosition e in cur.Ps) if (!SameProfile(r, prod.Pos(e.Short))) return true;
            return false;
        }

        // Zustand (eigen/fest/Ids) in alle Positionen des Eintrags schreiben; Standard ohne "fest" entfernt das ProfilePos
        void WriteAll(bool own, bool fix, List<string> ids)
        {
            ids = new List<string>(ids ?? new List<string>());   // Kopie: die Quelle kann selbst eine der Positionen sein
            foreach (EquipPosition e in cur.Ps)
            {
                ProfilePos pp = prod.Pos(e.Short);
                if (pp == null && !own && !fix) continue;
                if (pp == null) { pp = new ProfilePos(); pp.Short = e.Short; prod.Positions.Add(pp); }
                pp.Own = own; pp.Fixed = fix; pp.Ids.Clear();
                if (own) pp.Ids.AddRange(ids);
                if (!pp.Own && !pp.Fixed) prod.Positions.Remove(pp);
            }
        }

        void Unify()
        {
            if (prod == null || cur == null) return;
            ProfilePos r = prod.Pos(cur.Ps[0].Short);
            WriteAll(r != null && r.Own, r != null && r.Fixed, r != null ? r.Ids : new List<string>());
            Changed("Positionsgruppe " + cur.GroupName + " vereinheitlicht (Liste von " + cur.Ps[0].Short + ").");
        }
    }
}
