using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Standardablauf einer neuen Kampagne: Packen (Tag 1), Back-Up (Tag 2), Lauf (Tag 3) und je ein Puffer mobil und stationaer 1 Tag vor jedem Lauf.
    public partial class Package
    {
        public const string LaufName = "Lauf", PufferMobilName = "Puffer mobil", PufferStationaerName = "Puffer stationär";

        // Schritt aus einem Baustein (Standard-Anzahl je Position). Lauf/Splitten bekommen die Laufnummer runNo.
        public static PackageStep FromBaustein(Baustein b, int day, int runNo)
        {
            PackageStep s = new PackageStep();
            s.Title = b.Name; s.Source = b.Name; s.Row = b.Row; s.Text = b.Text; s.Day = day;
            s.Variant = b.Variant;   // Puffer mobil / stationaer: ein Schritt = ein Puffer mit diesem Typ
            if (b.Name == LaufName) s.Kind = StepKind.Run; else if (b.Name == "Splitten") s.Kind = StepKind.SubRun;   // Vorbelegung, im Schritt aenderbar
            if (s.Kind != StepKind.Normal) s.StartNo = runNo;
            foreach (string sh in b.Positions)
                for (int i = 0, n = b.DefaultCount(sh); i < n; i++) { EquipAssign a = new EquipAssign(); a.Short = sh; s.Equipment.Add(a); }   // Standard-Anzahl (z. B. 2 mobile Tanks)
            return s;
        }

        // Baustein aus dem Katalog; fehlt er, ein minimaler Schritt (Raum waehlt der Nutzer, Puffer haben ihre feste Zeile)
        static PackageStep StdStep(Catalog cat, string name, string row, int day, int runNo)
        {
            Baustein b = cat != null ? cat.Baustein_(name) : null;
            if (b != null) return FromBaustein(b, day, runNo);
            Baustein m = new Baustein(); m.Name = name; m.Row = row;
            return FromBaustein(m, day, runNo);
        }

        static PackageStep StdPuffer(Catalog cat, string name, string typ, int runNo)
        {
            PackageStep s = StdStep(cat, name, PufferSetup.RowName, 1, runNo);
            if (s.Variant.Length == 0) s.Variant = typ;
            s.Bind = StepBind.JeLauf; s.RunOffset = -1; s.RunEvery = 1; s.NumberRuns = false;
            return s;
        }

        // Neue Kampagne; firstNo = hintere 4 Stellen der ersten Laufnummer. Vom Nutzer als Standard markierte weitere Bausteine kommen dahinter.
        public static Package DefaultCampaign(Catalog cat, int firstNo = 1)
        {
            Package p = new Package();
            PackageStep pack = StdStep(cat, PackenName, "", 1, firstNo);
            p.Steps.Add(pack);
            p.Steps.Add(MakeBackup(pack, cat != null ? cat.Baustein_(BackupName) : null));
            p.Steps.Add(StdStep(cat, LaufName, "", 3, firstNo));
            p.Steps.Add(StdPuffer(cat, PufferMobilName, "mobil", firstNo));
            p.Steps.Add(StdPuffer(cat, PufferStationaerName, "stationaer", firstNo));
            if (cat != null)
                foreach (Baustein b in cat.Bausteine)
                    if (b.DefaultInCampaign && !p.Steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; })) p.Steps.Add(FromBaustein(b, 4, firstNo));
            return p;
        }

        // Darf der Schritt weg? Nein nur beim letzten Hauptlauf einer Kampagne (Puffer, Packen und alle anderen Schritte ja)
        public bool CanRemove(PackageStep s)
        {
            if (s != Head || Type != PackageType.Kampagne) return true;
            return Steps.Exists(delegate(PackageStep x) { return x != s && x.Kind == StepKind.Run && x.Bind == StepBind.Frei && !x.IsEmpty; });
        }
    }
}
