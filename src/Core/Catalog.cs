using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Ein konkretes Geraet an einer Position (z. B. "TFF" + "H1-X-0101").
    public class EquipChoice
    {
        public string Short = "", Id = "";
        public EquipChoice() { }
        public EquipChoice(string sh, string id) { Short = sh; Id = id; }
        public override string ToString() { return Short + ": " + Id; }
        public EquipChoice Clone() { return new EquipChoice(Short, Id); }
    }

    // Equipment-Position (z. B. TFF-Skid) mit den fuer diese Position validierten Geraeten. Von hoeheren Nutzern einstellbar.
    public class EquipPosition
    {
        public string Name = "";                    // z. B. "Prozesstank/Rezirkulationstank"
        public string Short = "";                   // Kurzform im Plantext, z. B. "PT"
        public List<string> Ids = new List<string>();   // validierte Geraete-IDs (leer = frei eintragen)
        public bool AllowRotation;                  // "abwechselnd" (reihum) nur, wenn der Admin es vorsieht (in der Praxis fast nur Abfuellskids)

        public EquipPosition Clone() { EquipPosition c = (EquipPosition)MemberwiseClone(); c.Ids = new List<string>(Ids); return c; }
        public override string ToString() { return Name; }

        // Platzhalter bei mehreren validierten Geraeten: nur abweichende Stellen werden zu X (H1-X-0201/0202/0203 -> H1-X-020X).
        // Leer, wenn nicht eindeutig bildbar (zu wenige Geraete oder unterschiedliche Laenge).
        public string Placeholder()
        {
            if (Ids.Count < 2) return "";
            int len = Ids[0].Length;
            foreach (string id in Ids) if (id.Length != len) return "";
            StringBuilder sb = new StringBuilder();
            bool any = false;
            for (int i = 0; i < len; i++)
            {
                bool same = true;
                foreach (string id in Ids) if (id[i] != Ids[0][i]) same = false;
                if (same) sb.Append(Ids[0][i]); else { sb.Append('X'); any = true; }
            }
            return any ? sb.ToString() : "";
        }
    }

    // Austausch-Gruppe: Geraete dieser Positionen koennen sich gegenseitig vertreten bzw. abwechselnd fuer denselben Zweck
    // genutzt werden (z. B. Freezetank 1 / 2 / Ersatz). Vom Admin hinterlegt.
    public class EquipGroup
    {
        public string Name = "";
        public List<string> Shorts = new List<string>();
        public EquipGroup Clone() { EquipGroup g = (EquipGroup)MemberwiseClone(); g.Shorts = new List<string>(Shorts); return g; }
        public override string ToString() { return Name; }
    }

    // Wiederverwendbarer Ablaufschritt ("Block" im Paket-Editor): z. B. Packen, Lauf, TFF Flush.
    public class Baustein
    {
        public string Name = "", Row = "", Text = "";
        public List<string> Positions = new List<string>();   // Kurzformen der Equipment-Positionen, die der Schritt abfragt
        public bool Unique;                                    // nur einmal je Kampagne (z. B. Packen); weiteres Einfuegen/Entfernen nur mit Recht
        public bool DefaultInCampaign;                         // in jeder neuen Kampagne vorhanden
        public bool Special;                                   // Sonderereignis (z. B. TFF Flush), eher als eigenes Paket

        public Baustein Clone() { Baustein b = (Baustein)MemberwiseClone(); b.Positions = new List<string>(Positions); return b; }
        public override string ToString() { return Name; }
    }

    public class Catalog
    {
        public List<EquipPosition> Positions = new List<EquipPosition>();
        public List<EquipGroup> Groups = new List<EquipGroup>();
        public List<Baustein> Bausteine = new List<Baustein>();

        public Catalog Clone()
        {
            Catalog c = new Catalog();
            foreach (EquipPosition p in Positions) c.Positions.Add(p.Clone());
            foreach (EquipGroup g in Groups) c.Groups.Add(g.Clone());
            foreach (Baustein b in Bausteine) c.Bausteine.Add(b.Clone());
            return c;
        }

        public EquipPosition Position(string shortName)
        {
            foreach (EquipPosition p in Positions) if (string.Equals(p.Short, shortName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public Baustein Baustein_(string name)
        {
            foreach (Baustein b in Bausteine) if (b.Name == name) return b;
            return null;
        }

        // Alle Geraete, die statt der Position eingesetzt werden koennen: eigene validierte Geraete plus die der Positionen
        // aus gemeinsamen Austausch-Gruppen (Admin-Vorgabe). Ohne Gruppe nur die eigenen.
        public List<EquipChoice> Interchangeable(string shortName)
        {
            List<string> shorts = new List<string>();
            shorts.Add(shortName);
            foreach (EquipGroup g in Groups)
                if (g.Shorts.Exists(delegate(string s) { return string.Equals(s, shortName, StringComparison.OrdinalIgnoreCase); }))
                    foreach (string s in g.Shorts) if (!shorts.Contains(s)) shorts.Add(s);
            List<EquipChoice> res = new List<EquipChoice>();
            foreach (string s in shorts)
            {
                EquipPosition p = Position(s);
                if (p != null) foreach (string id in p.Ids) res.Add(new EquipChoice(p.Short, id));
            }
            return res;
        }

        // Erfundene Beispieldaten (Repo ist oeffentlich). Standardliste der Positionen; vom Admin erweiterbar.
        public static Catalog Default()
        {
            Catalog c = new Catalog();
            c.Positions.Add(Pos("TFF-Skid", "TFF", "H1-X-0101", "H1-X-0102", "H1-X-0103"));
            c.Positions.Add(Pos("Membranhalter", "MH", "H1-X-0111", "H1-X-0112"));
            c.Positions.Add(Pos("Pumpe", "Pumpe", "H1-P-0101-05", "H1-P-0102-05"));
            c.Positions.Add(Pos("Prozesstank/Rezirkulationstank", "PT", "H1-T-0102", "H1-T-0111"));
            c.Positions.Add(Pos("Endverdünnungstank", "EVT", "H2-T-0201"));
            c.Positions.Add(Pos("Endverdünnungsskid", "EV", "H2-X-0201", "H2-X-0202"));
            c.Positions.Add(Pos("Mischtank", "MT", "H2-T-0210"));
            c.Positions.Add(Pos("Splitttank 1", "ST1", "H2-T-0211"));
            c.Positions.Add(Pos("Splitttank 2", "ST2", "H2-T-0212"));
            c.Positions.Add(Pos("Mischskid", "MS", "H2-X-0210"));
            c.Positions.Add(Pos("Abfüllanlage", "AA", "H2-X-0220"));
            c.Positions.Add(Pos("BDS-Tank", "BDS", "H2-T-0230"));
            c.Positions.Add(Pos("Filtrationstank", "FT", "H2-T-0240"));
            c.Positions.Add(Pos("Freezetank 1", "FZ1", "H2-T-0251"));
            c.Positions.Add(Pos("Freezetank 2", "FZ2", "H2-T-0252"));
            c.Positions.Add(Pos("Freezetank (Ersatz)", "FZ-E", "H2-T-0253"));
            EquipPosition skid = Pos("Abfüllskid", "AS", "H2-X-0221", "H2-X-0222"); skid.AllowRotation = true;   // Rotation (A, B, A ...) nur hier vorgesehen
            c.Positions.Add(skid);
            c.Positions.Add(Pos("Reinigungstank", "RT", "H1-T-0112", "H1-T-0113"));
            c.Positions.Add(Pos("Verteilerstück", "VS", "H1-F-0103-04", "H1-F-0104-02"));
            c.Positions.Add(Pos("Schläuche (Bezeichnung als Notiz)", "Schlauch"));
            c.Positions.Add(Pos("PVT", "PVT"));
            c.Positions.Add(Pos("PAT", "PAT"));
            c.Positions.Add(Pos("WFI-Vorlagetank", "WFI", "H1-T-0120"));
            c.Positions.Add(Pos("Cycle", "Cycle"));
            c.Positions.Add(Pos("Freezeanlage", "FA", "H2-X-0250"));


            c.Bausteine.Add(Bau("Packen", "Suite 1", true, true, false));                       // Standard: Suite 1 (Suite 2 nur als Sonderaktion)
            c.Bausteine.Add(Bau("Lauf", "Suite 1", false, false, false, "PT", "Pumpe", "TFF", "MH"));
            c.Bausteine.Add(Bau("Splitten", "Mix / Split", false, false, false, "ST1", "ST2", "Pumpe"));
            c.Bausteine.Add(Bau("Mischen", "Mix / Split", false, false, false, "MT", "MS", "Pumpe"));
            c.Bausteine.Add(Bau("Endverdünnung", "", false, false, false, "EVT", "EV", "Pumpe"));
            c.Bausteine.Add(Bau("Abfüllen", "", false, false, false, "AA", "AS", "BDS", "Schlauch"));
            c.Bausteine.Add(Bau("Freeze", "Freeze / Thaw", false, false, false, "FZ1", "FZ2", "FA", "Cycle"));
            c.Bausteine.Add(Bau("TFF Flush", "Suite 1", false, false, true, "TFF", "MH", "Pumpe", "VS", "Schlauch"));
            return c;
        }

        static EquipPosition Pos(string name, string sh, params string[] ids)
        {
            EquipPosition p = new EquipPosition(); p.Name = name; p.Short = sh; p.Ids.AddRange(ids); return p;
        }

        static EquipGroup Grp(string name, params string[] shorts)
        {
            EquipGroup g = new EquipGroup(); g.Name = name; g.Shorts.AddRange(shorts); return g;
        }

        static Baustein Bau(string name, string row, bool unique, bool def, bool special, params string[] positions)
        {
            Baustein b = new Baustein(); b.Name = name; b.Row = row; b.Unique = unique; b.DefaultInCampaign = def; b.Special = special;
            b.Positions.AddRange(positions); return b;
        }
    }
}
