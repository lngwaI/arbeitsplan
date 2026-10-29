#if DEMO
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Arbeitsplan.Core
{
    // Erfundene Ausgangsdaten der Vorfuehr-Version (Repo ist oeffentlich: nur erfundene Namen, Kuerzel, Nummern).
    // Relativ zu heute, damit die aktuelle Woche befuellt ist. Baut auf MemoryStore.Sample und Package.SampleCampaign auf.
    static class DemoData
    {
        public static void Seed(AppConfig cfg)
        {
            Directory.CreateDirectory(cfg.DataFolder); Directory.CreateDirectory(cfg.PackageFolder); Directory.CreateDirectory(cfg.BackupFolder);
            DateTime monday = DemoEnv.MondayOf(DateTime.Today);
            // Markierung zuerst: bricht das Anlegen ab, bleibt der Ordner als Demo-Ordner erkennbar (DemoEnv.CheckFolder)
            File.WriteAllText(Path.Combine(cfg.DataFolder, DemoEnv.MarkerFile), monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            MemoryStore ms = MemoryStore.Sample(monday);
            User by = new User(); by.Login = "mmuster"; by.Kuerzel = "MUMA"; by.First = "Max"; by.Last = "Mustermann"; by.RoleText = "Admin";
            Package old = Package.SampleCampaign("Demo-Kampagne");   // frueherer Durchlauf: Laeufe 1-4, drei Wochen vorher
            old.Apply(ms, monday.AddDays(-21), ApplyMode.FillEmpty, null, by);
            Package next = Package.SampleCampaign("Demo-Kampagne");   // naechster Durchlauf: Laeufe 5-8, in zwei Wochen
            next.Steps[2].StartNo = 5; next.Steps[2].IdentStart = "PA 40010005";
            next.Apply(ms, monday.AddDays(14), ApplyMode.FillEmpty, null, by);
            ms.SaveCatalog(DemoCatalog(ms.GetCatalog()));
            Status(ms, "H1-T-0102", EquipStatus.Gereinigt); Status(ms, "H1-X-0101", EquipStatus.InProzess); Status(ms, "H1-X-0102", EquipStatus.NichtGereinigt);
            Status(ms, "H1-T-0112", EquipStatus.Sterilisiert); Status(ms, "H2-T-0201", EquipStatus.Freigabe); Status(ms, "H1-P-0101-05", EquipStatus.Gereinigt);

            FileStore fs = new FileStore(cfg.DataFolder, false, cfg.CachePath, 0);
            if (fs.Offline) throw new IOException(fs.Problem);
            StoreData sd = ms.Snapshot(); sd.StatusesKnown = true;   // sonst bleiben die Status des leeren Stands
            fs.Restore(sd);
            fs.SaveUsers(MakeUsers(fs.Users));

            Package.SampleCampaign("Demo-Kampagne Produkt 1234").Save(Path.Combine(cfg.PackageFolder, "Demo-Kampagne Produkt 1234.apkg"));
            Package one = new Package(); one.Name = "Demo-Einzelpaket Rüsten"; one.Type = PackageType.Einzel;
            PackageStep ps = new PackageStep(); ps.Title = "Rüsten"; ps.Source = "Backfill"; ps.Row = "Suite 1"; ps.Day = 1; ps.Text = "Rüsten Suite 1, Filter prüfen"; one.Steps.Add(ps);
            one.Save(Path.Combine(cfg.PackageFolder, "Demo-Einzelpaket Rüsten.apkg"));
        }

        static void Status(MemoryStore ms, string id, EquipStatus st)
        {
            DeviceStatus s = new DeviceStatus(); s.Id = id; s.Status = st; s.At = DateTime.Now; s.By = "MUMA";
            ms.SetStatus(s, null);
        }

        // Eigene Rolle "Schichtleitung" und vier Demo-Nutzer (kein Passwort; die Rolle waehlt der Startdialog)
        static UserChanges MakeUsers(UserDb db)
        {
            UserChanges ch = new UserChanges();
            RightsPackage prod = db.Role("produktion");
            List<string> sl = new List<string>(prod.Rights);
            foreach (string r in new[] { Perm.EditNames, Perm.EditHeight, Perm.SortRows }) if (!sl.Contains(r)) sl.Add(r);
            RightsPackage role = new RightsPackage(db.FreeRoleId(), "Schichtleitung", sl.ToArray());
            ch.Roles.Add(role); ch.AddedRoles.Add(role);
            string[][] u = { new[] { "Max", "Mustermann", "mmuster", "MUMA", UserDb.AdminId }, new[] { "Erika", "Beispiel", "ebeispiel", "BEER", "leser" },
                             new[] { "Paula", "Werk", "pwerk", "WEPA", "produktion" }, new[] { "Sabine", "Leiter", "sleiter", "LESA", role.Id } };
            for (int i = 0; i < u.Length; i++)
            {
                User x = new User(); x.Id = i + 1; x.First = u[i][0]; x.Last = u[i][1]; x.Login = u[i][2]; x.Kuerzel = u[i][3]; x.RoleId = u[i][4];
                ch.Added.Add(x); ch.Changed.Add(x);
            }
            return ch;
        }

        // Eigener Equipment-Satz der Demo: erfundene Geraete, Gruppen, Ersatz, ein Geraet in Wartung, ein Produktprofil
        static Catalog DemoCatalog(Catalog cat)
        {
            EquipMaster m = cat.Master;
            if (m.Class("Pumpe") == null) { EquipClass pc = new EquipClass(); pc.Name = "Pumpe"; pc.Fields.Add("Typ"); m.Classes.Add(pc); }
            Dev(m, "H1-P-0101-05", "Pumpe", "Typ", "Schlauchpumpe", "");
            Dev(m, "H1-P-0102-05", "Pumpe", "Typ", "Schlauchpumpe", "Reservepumpe");
            Dev(m, "H1-T-0111", "Tank", "Volumen", "800 L", "");
            Dev(m, "H1-T-0112", "Tank", "Volumen", "250 L", "Reinigungstank");
            Dev(m, "H2-T-0201", "Tank", "Volumen", "500 L", "Endverdünnung");
            Dev(m, "H1-X-0103", "Skid", "Typ", "TFF", "Dichtung wird getauscht").Active = false;   // in Wartung
            EquipSeries s = new EquipSeries(); s.Name = "Pumpen Schlauch"; s.Class = "Pumpe"; s.Ids.AddRange(new[] { "H1-P-0101-05", "H1-P-0102-05" });
            s.Note = "Schlauchsatz vor Gebrauch prüfen"; m.Series.Add(s);
            EquipSubst x = new EquipSubst(); x.Id = "H1-X-0102"; x.Replaces = "H1-X-0103"; x.Note = "gleiche Membranfläche, andere Steuerung"; m.Substs.Add(x);

            ProductProfile p = new ProductProfile(); p.Code = "1234"; p.Name = "Demo-Produkt A";
            ProfilePos tff = new ProfilePos(); tff.Short = "TFF"; tff.Own = true; tff.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); p.Positions.Add(tff);
            SwapGroup g = new SwapGroup(); g.Name = "TFF-Skids"; g.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); p.Swaps.Add(g);
            cat.Products.Add(p);
            return cat;
        }

        static EquipDevice Dev(EquipMaster m, string id, string cls, string prop, string val, string note)
        {
            EquipDevice d = m.Device(id);
            if (d == null) { d = new EquipDevice(); d.Id = id; m.Devices.Add(d); }
            d.Class = cls; d.Note = note; d.Props.Clear(); d.Props.Add(new EquipProp(prop, val));
            return d;
        }
    }
}
#endif
