using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // UX-1: Equipment je Puffer. Ein Puffer-Schritt kann mehrere PufferSpec haben (je Puffer eigene Zeilen); Zeilen, die fuer einen oder
    // mehrere Puffer gemeinsam gelten (z. B. ein WFI-Vorlagetank), stehen in PackageStep.PufferShared mit Geltungsbereich-Maske.

    // Zeile, die fuer mehrere Puffer eines Schritts gilt. Scope: Bit k = Puffer k+1; 0 = alle Puffer des Schritts.
    public class PufferShare
    {
        public EquipAssign Eq = new EquipAssign();
        public int Scope;

        public bool Covers(int k) { return Scope == 0 || (k >= 0 && k < 31 && ((Scope >> k) & 1) != 0); }
        public PufferShare Clone() { PufferShare c = new PufferShare(); c.Eq = Eq.Clone(); c.Scope = Scope; return c; }
    }

    // Ein Puffer des Schritts: Name (leer = aus dem Schritt-Titel abgeleitet), Typ, eigene Equipment-Zeilen.
    public class PufferSpec
    {
        public string Name = "", Typ = "mobil";
        public int NameNo;                      // > 1: Nummer hinter dem abgeleiteten Namen ("Titel 2"); 0/1 = ohne
        public int AltMul = 1, AltAdd;          // Index fuer "abwechselnd": Lauf * AltMul + AltAdd (alte Pakete: Anzahl Puffer / Platz)
        public int? OffsetDays;                 // UX-5c: eigener Tag zum Lauf (nur "zu jedem Lauf"): -2 = 2 Tage davor; null = wie der Schritt (RunOffset)
        public List<EquipAssign> Lines = new List<EquipAssign>();

        public PufferSpec Clone()
        {
            PufferSpec c = (PufferSpec)MemberwiseClone();
            c.Lines = new List<EquipAssign>();
            foreach (EquipAssign a in Lines) c.Lines.Add(a.Clone());
            return c;
        }
    }

    public partial class PackageStep
    {
        public const int MaxPuffer = 9;
        public List<PufferSpec> Puffers = new List<PufferSpec>();       // leer = alter Modus (PufferCount/PufferNames, gemeinsame Zeilen in Equipment)
        public List<PufferShare> PufferShared = new List<PufferShare>();

        void ClonePuffers(PackageStep s)
        {
            s.Puffers = new List<PufferSpec>(); foreach (PufferSpec p in Puffers) s.Puffers.Add(p.Clone());
            s.PufferShared = new List<PufferShare>(); foreach (PufferShare g in PufferShared) s.PufferShared.Add(g.Clone());
        }

        // Puffer, wie sie der alte Modus ergibt (PufferCount je Typ, Namen, alle Zeilen in jedem Puffer; "abwechselnd" reihum Lauf*n+Platz)
        public List<PufferSpec> LegacyPuffers()
        {
            int n = Math.Max(1, PufferCount);
            List<PufferSpec> res = new List<PufferSpec>();
            string[] names = BufferNames();
            int k = 0;
            foreach (KeyValuePair<string, int> kv in VariantSet.Counts(Variant))
                for (int i = 0; i < kv.Value; i++)
                    for (int j = 0; j < n; j++, k++)
                    {
                        PufferSpec sp = new PufferSpec(); sp.Typ = kv.Key;
                        sp.Name = k < names.Length ? names[k] : "";
                        sp.NameNo = n > 1 && k > 0 ? k + 1 : 0;
                        sp.AltMul = n; sp.AltAdd = j;
                        foreach (EquipAssign a in Equipment) sp.Lines.Add(a.Clone());
                        res.Add(sp);
                    }
            return res;
        }

        // Alten Modus in eigene Puffer umstellen (verlustfrei: gleicher Plantext). false = nichts zu tun (kein Puffer-Schritt oder schon umgestellt).
        public bool MigratePuffers()
        {
            if (Puffers.Count > 0 || !IsBuffer) return false;
            Puffers = LegacyPuffers(); PufferShared.Clear();
            SyncPuffers();
            return true;
        }

        // Alte Felder aus den Puffern ableiten (Typ-Menge) und die nicht mehr genutzten leeren
        public void SyncPuffers()
        {
            Variant = PufferVariant(); PufferCount = 1; PufferNames = ""; Equipment.Clear();
        }

        // Typ-Menge der Puffer als VariantSet-Text ("mobil:2|stationaer"); ohne Puffer die alte Variante
        public string PufferVariant()
        {
            if (Puffers.Count == 0) return Variant;
            string v = "";
            foreach (PufferSpec p in Puffers) if (p.Typ.Length > 0) v = VariantSet.With(v, p.Typ, VariantSet.Count(v, p.Typ) + 1);
            return v;
        }

        // Plantext-Zeilen der Equipment-Zeilen eines Puffers: eigene, dann gemeinsame im Geltungsbereich
        List<string> PufferEquipLines(PufferSpec sp, int k, int run)
        {
            List<string> r = new List<string>();
            int alt = run * sp.AltMul + sp.AltAdd;
            foreach (EquipAssign a in sp.Lines) { string l = a.LineM(alt); if (l.Length > 0) r.Add(l); }   // mit Notiz-Marker (BufferSet.NoteMark)
            foreach (PufferShare g in PufferShared) if (g.Covers(k)) { string l = g.Eq.LineM(run); if (l.Length > 0) r.Add(l); }
            return r;
        }

        // Alle Zeilen eines Puffers (eigene, dann gemeinsame im Geltungsbereich); je Eintrag der Wert fuer "abwechselnd" in dieser Zeile
        public List<EquipChoice> PufferChoices(int k, int run)
        {
            List<EquipChoice> r = new List<EquipChoice>();
            List<PufferSpec> specs = Puffers.Count > 0 ? Puffers : LegacyPuffers();
            if (k < 0 || k >= specs.Count) return r;
            int alt = run * specs[k].AltMul + specs[k].AltAdd;
            foreach (EquipAssign a in specs[k].Lines) { EquipChoice c = a.ChoiceFor(alt); if (c != null) r.Add(c); }
            if (Puffers.Count > 0) foreach (PufferShare g in PufferShared) if (g.Covers(k)) { EquipChoice c = g.Eq.ChoiceFor(run); if (c != null) r.Add(c); }
            return r;
        }

        // Geraete-IDs je Gruppe, die zusammen geprueft werden (Zugehoerigkeit/"Passt zu"): alter Modus eine Gruppe, sonst je Puffer eine
        public List<List<string>> DeviceGroups(int run)
        {
            List<List<string>> res = new List<List<string>>();
            if (Puffers.Count == 0)
            {
                List<string> ids = new List<string>();
                foreach (EquipAssign a in Equipment) { EquipChoice c = a.ChoiceFor(run); if (c != null) ids.Add(c.Id); }
                res.Add(ids); return res;
            }
            for (int k = 0; k < Puffers.Count; k++)
            {
                List<string> ids = new List<string>();
                foreach (EquipChoice c in PufferChoices(k, run)) ids.Add(c.Id);
                res.Add(ids);
            }
            return res;
        }

        // Zeilen fuer aeltere exe (gemeinsame Liste): im alten Modus Equipment, sonst Zeilen von Puffer 1 plus die gemeinsamen
        public List<EquipAssign> CompatEquipment()
        {
            if (Puffers.Count == 0) return Equipment;
            List<EquipAssign> r = new List<EquipAssign>(Puffers[0].Lines);
            foreach (PufferShare g in PufferShared) r.Add(g.Eq);
            return r;
        }
    }

    public partial class Package
    {
        // Neuen Puffer aus der Vorlage hinten anhaengen (alter Modus wird vorher verlustfrei umgestellt). Vorbelegte Geraete, Name und Typ kommen
        // aus der Vorlage; Positionen mit "Shared" werden gemeinsame Zeilen (Geltungsbereich = Puffer, die sie in ihrer Vorlage haben).
        public static PufferSpec ApplyPufferTemplate(PackageStep s, PufferTemplate t)
        {
            s.MigratePuffers();
            PufferSpec sp = new PufferSpec(); sp.Name = t.Name; sp.Typ = t.Typ.Length > 0 ? t.Typ : "mobil";
            int k = s.Puffers.Count;
            foreach (PufferTemplatePos pp in t.Positions)
            {
                EquipAssign a = pp.ToAssign();
                if (!pp.Shared) { sp.Lines.Add(a); continue; }
                // Zusammenlegen nur mit gleicher Zeile (Position, Geraete in Reihenfolge, Wechsel, Notiz); sonst eigene Zeile fuer den neuen Puffer
                foreach (PufferShare x in s.PufferShared)   // "alle Puffer" mit anderem Geraet darf den neuen nicht mehr abdecken
                    if (k > 0 && x.Scope == 0 && SameShort(x.Eq, a) && !SameLine(x.Eq, a)) x.Scope = k < 31 ? (1 << k) - 1 : int.MaxValue;
                PufferShare g = s.PufferShared.Find(delegate(PufferShare x) { return SameLine(x.Eq, a); });
                if (g != null && g.Scope == 0) continue;   // N7: "alle Puffer" gilt schon fuer den neuen
                if (g == null) { g = new PufferShare(); g.Eq = a; s.PufferShared.Add(g); }
                if (k < 31) g.Scope |= 1 << k;
            }
            if (sp.Name.Length == 0 && k > 0 && sp.NameNo == 0) sp.NameNo = k + 1;   // unbenannt: "Titel 2", "Titel 3" … (zentral)
            s.Puffers.Add(sp);
            s.SyncPuffers();
            return sp;
        }

        static bool SameShort(EquipAssign x, EquipAssign y) { return string.Equals(x.Short, y.Short, StringComparison.OrdinalIgnoreCase); }

        // Gleiche gemeinsame Zeile: Position, Geraete (Ids in Reihenfolge), Wechsel, Ausweich und Notiz; y (aus der Vorlage) ohne Geraet/Notiz bringt nichts
        // Eigenes mit und passt deshalb zu jeder Zeile der Position (nichts geht verloren)
        static bool SameLine(EquipAssign x, EquipAssign y)
        {
            if (SameShort(x, y) && y.Choices.Count == 0 && y.Note.Length == 0 && y.Fallback.Length == 0 && !y.Alternate) return true;
            if (!SameShort(x, y) || x.Alternate != y.Alternate || x.Choices.Count != y.Choices.Count || x.Note != y.Note || x.Fallback != y.Fallback) return false;
            for (int i = 0; i < x.Choices.Count; i++) if (x.Choices[i].Id != y.Choices[i].Id) return false;
            return true;
        }

        // Voreinstellung eines Produkts uebernehmen: die bisherigen Puffer des Schritts werden durch die Vorlagen ersetzt.
        public static void ApplyPufferTemplates(PackageStep s, List<PufferTemplate> list)
        {
            s.Puffers.Clear(); s.PufferShared.Clear(); s.Equipment.Clear(); s.Variant = "";   // Variante kommt aus den Puffern
            foreach (PufferTemplate t in list) ApplyPufferTemplate(s, t);
        }

        // Puffer-Vorlagen des Produkts: Pufferpaket aus dem Produktprofil, sonst der Standardpuffer des Katalogs (ohne Geraete), sonst leer.
        public static List<PufferTemplate> PufferFor(Catalog cat, string product)
        {
            List<PufferTemplate> r = new List<PufferTemplate>();
            ProductProfile pr = cat != null ? cat.Product(product) : null;
            if (pr != null && pr.Puffer.Count > 0) { foreach (PufferTemplate t in pr.Puffer) r.Add(t.Clone()); return r; }
            PufferTemplate std = cat != null ? cat.StandardPuffer() : null;
            if (std != null) r.Add(std);
            return r;
        }
    }
}
