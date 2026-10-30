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
        public string NoteTemplate = "";                       // mehrzeilige Notiz-Vorlage (z. B. Puffer-Material), je Zeile eine Notiz
        public List<string> Positions = new List<string>();   // Kurzformen der Equipment-Positionen, die der Schritt abfragt
        public bool Unique;                                    // nur einmal je Kampagne (z. B. Packen); weiteres Einfuegen/Entfernen nur mit Recht
        public bool DefaultInCampaign;                         // in jeder neuen Kampagne vorhanden
        public bool Special;                                   // Sonderereignis (z. B. TFF Flush, Zwischenreinigung): von Hand eingeplant, nie Teil einer Kampagnen-Automatik
        public string Variant = "";                            // Puffer-Baustein: Typ des erzeugten Puffers ("mobil" / "stationaer"), sonst ""

        public Dictionary<string, int> Counts = new Dictionary<string, int>();   // Standard-Anzahl Equipment-Zeilen je Position (Kurzform); fehlt = 1

        // Standard-Anzahl der Equipment-Zeilen, die ein neuer Schritt dieses Bausteins je Position anlegt (1..9)
        public int DefaultCount(string shortName) { int n; return Counts.TryGetValue(shortName, out n) ? Math.Max(1, Math.Min(9, n)) : 1; }
        public void SetDefaultCount(string shortName, int n) { if (n <= 1) Counts.Remove(shortName); else Counts[shortName] = Math.Min(9, n); }

        public Baustein Clone() { Baustein b = (Baustein)MemberwiseClone(); b.Positions = new List<string>(Positions); b.Counts = new Dictionary<string, int>(Counts); return b; }
        public override string ToString() { return Name; }
    }

    public partial class Catalog
    {
        public const string OldBufferName = "Puffer";   // alter Sammel-Baustein (beides): bleibt fuer alte Pakete, neue Schritte nehmen "Puffer mobil" / "Puffer stationär"

        public List<EquipPosition> Positions = new List<EquipPosition>();
        public List<EquipGroup> Groups = new List<EquipGroup>();
        public List<Baustein> Bausteine = new List<Baustein>();
        public EquipMaster Master = new EquipMaster();     // Equipment-Stammdaten (Klassen, Reihen, Geraete, Regeln)

        public Catalog Clone()
        {
            Catalog c = new Catalog();
            c.Master = Master.Clone();
            foreach (EquipPosition p in Positions) c.Positions.Add(p.Clone());
            foreach (EquipGroup g in Groups) c.Groups.Add(g.Clone());
            foreach (Baustein b in Bausteine) c.Bausteine.Add(b.Clone());
            foreach (ProductProfile p in Products) c.Products.Add(p.Clone());
            c.FreezeDefaults = FreezeDefaults.Clone();
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

        // Notiz-Vorlage fuer einen Puffer-Schritt: die seines Bausteins, sonst die des ersten "Puffer…"-Bausteins mit Vorlage; sonst ""
        public string NoteTemplateFor(PackageStep s)
        {
            if (s == null || !s.IsBuffer) return "";
            Baustein b = Baustein_(s.Source);
            if (b != null && b.NoteTemplate.Length > 0) return b.NoteTemplate;
            foreach (Baustein x in Bausteine)
                if (x.NoteTemplate.Length > 0 && x.Name.StartsWith("Puffer", StringComparison.OrdinalIgnoreCase)) return x.NoteTemplate;
            return "";
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
            c.Master = EquipMaster.Default();
            c.Positions.Add(Pos("TFF-Skid", "TFF", "H1-X-0101", "H1-X-0102", "H1-X-0103"));
            c.Positions.Add(Pos("Membranhalter", "MH", "H1-X-0111", "H1-X-0112"));
            c.Positions.Add(Pos("Pumpe", "Pumpe", "H1-P-0101-05", "H1-P-0102-05"));
            c.Positions.Add(Pos("Prozesstank/Rezirkulationstank", "PT", "H1-T-0102", "H1-T-0111"));
            c.Positions.Add(Pos("Endverdünnungstank", "EVT", "H2-T-0201"));
            c.Positions.Add(Pos("Endverdünnungsskid", "EV", "H2-X-0201", "H2-X-0202"));
            c.Positions.Add(Pos("Mischtank", "MT", "H2-T-0210"));
            c.Positions.Add(Pos("Split Tank 1", "ST1", "H2-T-0211"));
            c.Positions.Add(Pos("Split Tank 2", "ST2", "H2-T-0212"));
            c.Positions.Add(Pos("Reds-Tank (Rez. Tank)", "Reds", "H1-T-0131", "H1-T-0132"));
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


            c.Bausteine.Add(Bau("Packen", "Suite 1", true, true, false, "TFF", "Pumpe", "VS"));   // Standard: Suite 1 (Suite 2 nur als Sonderaktion)
            c.Bausteine.Add(Bau("Lauf", "Suite 1", false, false, false, "PT", "Pumpe", "TFF", "MH"));
            c.Bausteine.Add(Bau("Splitten", "Mix / Split", false, false, false, "ST1", "ST2", "Pumpe"));
            c.Bausteine.Add(Bau("Mischen", "Mix / Split", false, false, false, "MT", "MS", "Pumpe"));
            c.Bausteine.Add(Bau("Endverdünnung", "", false, false, false, "EVT", "EV", "Pumpe"));
            c.Bausteine.Add(Bau("Abfüllen", "", false, false, false, "AA", "AS", "BDS", "Schlauch"));
            c.Bausteine.Add(Bau("Backfill", "Suite 1", false, false, false));
            c.Bausteine.Add(Bau("Freeze","Freeze / Thaw", false, false, false, "FZ1", "FZ2", "FA", "Cycle"));
            c.Bausteine.Add(Bau("TFF Flush", "Suite 1", false, false, true, "TFF", "MH", "Pumpe", "VS", "Schlauch"));
            c.AddStandardBausteine();
            c.Groups.Add(Grp("Freezetanks", "FZ1", "FZ2"));
            return c;
        }

        // Standard-Bausteine je Raum (Puffer, Freeze / Thaw, CIP / SIP, Einwaage). Alte Bestaende: der Paket-Editor ergaenzt fehlende
        // beim Oeffnen in seiner Kopie (gespeichert erst mit "Katalog einrichten"); vorhandene gleichen Namens bleiben unveraendert.
        // Liefert die Anzahl ergaenzter Bausteine.
        public int AddStandardBausteine()
        {
            foreach (EquipPosition np in new EquipPosition[] { Pos("Mobiler Tank", "MobT"), Pos("Glasflasche/Gebinde", "Flasche"), Pos("Tween", "Tween"),
                Pos("Materialnotiz", "Material"), Pos("Vorlagetank", "VT"), Pos("WFI-Vorlagetank", "WFI", "H1-T-0120"), Pos("Ansatztank", "Ansatztank") })
                if (Position(np.Short) == null) Positions.Add(np);
            Baustein[] std = {
                Bau("Puffer", "Puffer", false, false, false, "WFI"),   // alt: in Paketen weiter nutzbar, im Schritt-Dialog ausgeblendet (OldBufferName)
                Bau("Puffer mobil", "Puffer", false, false, false, "MobT", "Flasche", "Tween", "Material"),
                Bau("Puffer stationär", "Puffer", false, false, false, "VT", "WFI", "Material"),
                Bau("Zwischenreinigung", "Suite 1", false, false, true, "RT", "VS", "Pumpe", "Schlauch"),
                Bau("Freeze / Thaw", "Freeze / Thaw", false, false, false, "FZ1", "FZ2", "FA"),
                Bau("CIP / SIP", "CIP / SIP", false, false, false, "RT", "VS"),
                Bau("Einwaage", "Einwaage", false, false, false),
                Bau(Package.BackupName, "Suite 1", false, false, false, "TFF", "Pumpe", "VS"),   // Reservetag nach Packen, Equipment = Ausweich des Packens
                Bau(StandardPufferName, "Puffer", false, false, false, "Ansatztank", "VT", "WFI", "Tween", "Material") };   // UX-1: Vorlage eines Puffers (kein Schritt; Standardanzahl je Position im Baustein, erweiterbar)
            std[1].Variant = "mobil"; std[2].Variant = "stationaer";
            std[7].Text = "Reservetag, falls Packen nicht abgeschlossen";
            int n = 0;
            foreach (Baustein b in std)
                if (!Bausteine.Exists(delegate(Baustein x) { return string.Equals(x.Name, b.Name, StringComparison.OrdinalIgnoreCase); })) { Bausteine.Add(b); n++; }
            return n;
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
