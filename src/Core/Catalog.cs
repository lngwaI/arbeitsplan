using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Equipment-Position (z. B. TFF-Skid) mit den fuer diese Position validierten Geraeten. Von hoeheren Nutzern einstellbar.
    public class EquipPosition
    {
        public string Name = "";                    // z. B. "Prozesstank / Rezirkulationstank"
        public string Short = "";                   // Kurzform im Plantext, z. B. "PT"
        public List<string> Ids = new List<string>();   // validierte Geraete-IDs

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
        public List<Baustein> Bausteine = new List<Baustein>();

        public Catalog Clone()
        {
            Catalog c = new Catalog();
            foreach (EquipPosition p in Positions) c.Positions.Add(p.Clone());
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

        // Erfundene Beispieldaten (Repo ist oeffentlich).
        public static Catalog Default()
        {
            Catalog c = new Catalog();
            c.Positions.Add(Pos("Prozesstank / Rezirkulationstank", "PT", "H1-T-0102", "H1-T-0111"));
            c.Positions.Add(Pos("Pumpe", "Pumpe", "H1-P-0101-05", "H1-P-0102-05"));
            c.Positions.Add(Pos("TFF-Skid", "TFF", "H1-X-0101", "H1-X-0102", "H1-X-0103"));
            c.Positions.Add(Pos("Endverdünnungs-Skid", "EV", "H2-X-0201", "H2-X-0202"));
            c.Positions.Add(Pos("Reinigungstank", "RT", "H1-T-0112", "H1-T-0113"));
            c.Positions.Add(Pos("Verteilerstück", "VS", "H1-F-0103-04", "H1-F-0104-02"));
            c.Positions.Add(Pos("Schlauch (Bezeichnung als Notiz)", "Schlauch"));

            c.Bausteine.Add(Bau("Packen", "Suite 1", "", true, true, false));
            c.Bausteine.Add(Bau("Lauf", "Suite 1", "", false, false, false, "PT", "Pumpe", "TFF", "EV"));
            c.Bausteine.Add(Bau("Splitten", "Mix / Split", "", false, false, false, "PT", "Pumpe"));
            c.Bausteine.Add(Bau("Mischen", "Mix / Split", "", false, false, false, "PT", "Pumpe"));
            c.Bausteine.Add(Bau("TFF Flush", "Suite 1", "", false, false, true, "TFF", "Pumpe", "VS", "Schlauch"));
            return c;
        }

        static EquipPosition Pos(string name, string sh, params string[] ids)
        {
            EquipPosition p = new EquipPosition(); p.Name = name; p.Short = sh; p.Ids.AddRange(ids); return p;
        }

        static Baustein Bau(string name, string row, string text, bool unique, bool def, bool special, params string[] positions)
        {
            Baustein b = new Baustein(); b.Name = name; b.Row = row; b.Text = text; b.Unique = unique; b.DefaultInCampaign = def; b.Special = special;
            b.Positions.AddRange(positions); return b;
        }
    }
}
