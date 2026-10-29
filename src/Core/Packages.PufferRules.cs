using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // UX-1 Regel "pufdoppelt": dasselbe Geraet in zwei verschiedenen Puffern am selben Tag = Sperre. Ein Geraet, das als gemeinsame Zeile
    // (PufferShare) fuer mehrere Puffer gilt (z. B. WFI-Vorlagetank), ist ein Eintrag und daher erlaubt. Nur Schritte mit eigenen Puffern
    // (Puffers.Count > 0); der alte Modus (alle Zeilen in jedem Puffer) wird erst nach der Umstellung auf eigene Puffer geprueft.
    public partial class Package
    {
        class PufferUse { public int Step, Run, Kind, Index; public string Short, Id; }   // Kind 0 = eigene Zeile von Puffer Index, 1 = gemeinsame Zeile Index

        void CheckPufferDup(List<PackageProblem> res, List<RunDate> plan, DateTime start, bool known)
        {
            Dictionary<string, PufferUse> seen = new Dictionary<string, PufferUse>();
            HashSet<string> done = new HashSet<string>();
            foreach (RunDate d in plan)
            {
                if (d.Step < 0 || d.Step >= Steps.Count) continue;
                PackageStep s = Steps[d.Step];
                if (s.IsEmpty || s.Puffers.Count == 0) continue;
                for (int k = 0; k < s.Puffers.Count; k++)
                {
                    PufferSpec sp = s.Puffers[k];
                    foreach (EquipAssign a in sp.Lines) PufferUseOf(res, seen, done, d, s, a.ChoiceFor(d.Run * sp.AltMul + sp.AltAdd), 0, k, start, known);
                }
                for (int g = 0; g < s.PufferShared.Count; g++) PufferUseOf(res, seen, done, d, s, s.PufferShared[g].Eq.ChoiceFor(d.Run), 1, g, start, known);
            }
        }

        void PufferUseOf(List<PackageProblem> res, Dictionary<string, PufferUse> seen, HashSet<string> done, RunDate d, PackageStep s, EquipChoice c, int kind, int index, DateTime start, bool known)
        {
            if (c == null || c.Id.Trim().Length == 0) return;
            PufferUse u = new PufferUse(); u.Step = d.Step; u.Run = d.Run; u.Kind = kind; u.Index = index; u.Short = c.Short; u.Id = c.Id.Trim();
            string key = d.Day + "|" + u.Id.ToLowerInvariant();
            PufferUse o;
            if (!seen.TryGetValue(key, out o)) { seen[key] = u; return; }
            if (o.Step == u.Step && o.Run == u.Run && o.Kind == u.Kind && o.Index == u.Index) return;   // dieselbe Zeile / derselbe Puffer
            string pair = key + "|" + o.Step + "." + o.Run + "." + o.Kind + "." + o.Index + "|" + u.Step + "." + u.Run + "." + u.Kind + "." + u.Index;
            if (!done.Add(pair)) return;
            bool cross = o.Step != u.Step || o.Run != u.Run;
            PackageProblem p = Add(res, Severity.Sperre, "pufdoppelt", (u.Short.Length > 0 ? u.Short + " " : "") + u.Id + " ist in " + PufferLabel(o, cross) + " und " + PufferLabel(u, cross)
                + " am selben Tag (" + DayLabel(d.Day, start, known) + ")", o.Step, o.Run);
            p.Step2 = u.Step; p.Run2 = u.Run;
            p.Action = "für jeden Puffer ein eigenes Gerät wählen – oder das Gerät als gemeinsame Zeile für beide Puffer eintragen";
        }

        string PufferLabel(PufferUse u, bool cross)
        {
            string l;
            if (u.Kind == 0) l = "Puffer " + (u.Index + 1);
            else
            {
                PufferShare g = Steps[u.Step].PufferShared[u.Index];
                List<string> nr = new List<string>();
                for (int k = 0; k < Steps[u.Step].Puffers.Count; k++) if (g.Covers(k)) nr.Add((k + 1).ToString());
                l = "Puffer " + string.Join("+", nr.ToArray()) + " (gemeinsame Zeile)";
            }
            return cross ? l + " von „" + StepName(u.Step) + "“ (Lauf " + (u.Run + 1) + ")" : l;
        }
    }
}
