#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests: Packen (Back-Up) im Paket (Kopplung, Datei, alte Pakete, Apply, Kampagnenstart) und Kampagnen-Assistent (Produktfilter, freie Codes).
    partial class MainForm
    {
        void SelfTestBackup(StringBuilder sb)
        {
            Catalog cat = Catalog.Default();
            Baustein bb = cat.Baustein_(Package.BackupName);
            Package p = new Package(); p.Name = "Backup"; p.ProductCode = "AB1";
            PackageStep pk = new PackageStep(); pk.Title = "Packen"; pk.Source = Package.PackenName; pk.Row = "Suite 1"; pk.Day = 1;
            EquipAssign a = new EquipAssign(); a.Short = "TFF"; a.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); a.Fallback = "H1-X-0102"; pk.Equipment.Add(a);
            PackageStep bk = Package.MakeBackup(pk, bb);
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Day = 4; lf.Kind = StepKind.Run;
            p.Steps.Add(pk); p.Steps.Add(bk); p.Steps.Add(lf);
            bool okMake = bb != null && bk.Day == 2 && bk.Row == "Suite 1" && bk.BackupOf == "Packen" && bk.Equipment.Count == 1 && bk.Equipment[0].Choices[0].Id == "H1-X-0102"
                && bk.Compose(0, "").Contains("TFF: H1-X-0102") && Package.BackupProblem(p.Steps) == null;
            string path = Path.Combine(Path.GetTempPath(), "selftest_backup.apkg");
            p.Save(path);
            Package q = Package.Load(path);
            bool okRt = q.Steps[1].BackupOf == "Packen" && q.Steps[1].BacksUp(q.Steps[0]) && q.Steps[1].Day == 2 && q.Steps[0].BackupOf.Length == 0;
            // Regel in Load/Apply (nicht nur im Editor): Back-Up ohne Packen wird abgelehnt
            Package bad = new Package(); bad.Steps.Add(bk.Clone());
            bool okRule = Package.BackupProblem(bad.Steps) != null;
            bad.Save(path);
            try { Package.Load(path); okRule = false; } catch (InvalidDataException) { }
            try { bad.Apply(new MemoryStore(), new DateTime(2031, 3, 3), ApplyMode.Overwrite, null); okRule = false; } catch (InvalidOperationException) { }
            sb.AppendLine("Back-Up Paket: Kopplung " + Ok(okMake) + " Datei " + Ok(okRt) + " Regel in Load/Apply " + Ok(okRule));

            // Folgen: Packen verschoben -> Back-Up Tag +1 (Sync, Apply); Apply schreibt beide Tage
            pk.MoveAll(2);
            bool okSync = Package.SyncBackups(p.Steps) && bk.FirstDay == 4 && !Package.SyncBackups(p.Steps);
            MemoryStore ms = new MemoryStore(); DateTime d0 = new DateTime(2031, 3, 3);
            p.Apply(ms, d0, ApplyMode.Overwrite, null);
            int s1 = ms.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id;
            Entry e3 = ms.Get(d0.AddDays(2), s1), e4 = ms.Get(d0.AddDays(3), s1);
            bool okApply = e3 != null && e3.Content.StartsWith("Packen") && e4 != null && e4.Content.Contains("Packen (Back-Up)") && e4.Content.Contains("H1-X-0102");

            // Kampagnenstart: Packen einzeln verschoben (ohne "Folgende") -> Back-Up folgt; Raumwechsel gilt auch fuer den Back-Up
            using (CampaignForm cf = new CampaignForm(p, ms, d0.AddDays(14), ApplyMode.Overwrite))
            {
                cf.TestShiftFollowing = false; cf.Shift(0, 2);
                List<RunDate> sc = cf.Schedule;
                RunDate rp = sc.Find(delegate(RunDate r) { return r.Step == 0; }), rb = sc.Find(delegate(RunDate r) { return r.Step == 1; }), rl = sc.Find(delegate(RunDate r) { return r.Step == 2; });
                bool okCf = rb.Day == rp.Day + 1 && rp.Day == 5 && rl.Day == 4;
                cf.SetRoom(0, "Suite 2", false);
                sc = cf.Schedule;
                okCf &= sc.Find(delegate(RunDate r) { return r.Step == 1; }).Row == "Suite 2";
                okSync &= okCf;
            }
            sb.AppendLine("Back-Up Folgen: Verschieben " + Ok(okSync) + " Apply " + Ok(okApply));

            // Alte Pakete (Version 5, ohne "backup"; auch das Testpaket mit Schritt "Packen Backup" ohne Kopplung) bleiben lesbar
            File.WriteAllText(path, "<Paket name=\"Alt\" version=\"5\" produkt=\"1357\"><Schritt titel=\"Packen\" quelle=\"Packen\" block=\"Suite 1\" tag=\"1\"/><Schritt titel=\"Packen Backup\" block=\"Suite 1\" tag=\"2\"/></Paket>");
            Package old = Package.Load(path);
            bool okOld = old.Steps.Count == 2 && !old.Steps[1].IsBackup && Package.BackupProblem(old.Steps) == null;
            Package sc2 = Package.SampleCampaign("alt"); sc2.Save(path);
            okOld &= Package.Load(path).Steps.Count == sc2.Steps.Count;
            sb.AppendLine("Back-Up alte Pakete: lesbar " + Ok(okOld));

            // Kampagnen-Assistent: Picker liest freie Produktcodes, Filter nach Produkt/Art
            string dir = Path.Combine(Path.GetTempPath(), "selftest_wizard_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Package w1 = Package.SampleCampaign("Alpha"); w1.ProductCode = "AB1"; w1.Save(Path.Combine(dir, "alpha.apkg"));
                Package w2 = Package.SampleCampaign("Beta"); w2.ProductCode = "XY-9/2"; w2.Save(Path.Combine(dir, "beta.apkg"));
                Package w3 = new Package(); w3.Name = "Flush"; w3.ProductCode = "AB1";
                PackageStep fl = new PackageStep(); fl.Title = "TFF Flush"; fl.Source = "TFF Flush"; fl.Row = "Suite 1"; w3.Steps.Add(fl); w3.Save(Path.Combine(dir, "flush.apkg"));
                bool okPick = PackagePicker.Product(Path.Combine(dir, "alpha.apkg")) == "AB1" && PackagePicker.Product(Path.Combine(dir, "beta.apkg")) == "XY-9/2";
                Catalog wc = Catalog.Default(); ProductProfile pp = new ProductProfile(); pp.Code = "KAT-1"; pp.Name = "Nur im Katalog"; wc.Products.Add(pp);
                using (CampaignWizard w = new CampaignWizard(dir, wc, null))
                {
                    List<string> codes = w.TestProducts();
                    bool okList = codes.Contains("AB1") && codes.Contains("XY-9/2") && codes.Contains("KAT-1");
                    w.TestPage(2, "AB1", false);
                    List<string> camp = w.TestItems();
                    w.TestPage(2, "AB1", true);
                    List<string> spec = w.TestItems();
                    w.TestPage(2, "XY-9/2", false);
                    List<string> other = w.TestItems();
                    w.TestPage(2, "QQ", false);
                    bool okFilter = camp.Count == 1 && camp[0] == "alpha" && spec.Count == 1 && spec[0] == "flush" && other.Count == 1 && other[0] == "beta" && w.TestItems().Count == 0;
                    sb.AppendLine("Assistent: Produktcodes frei " + Ok(okPick) + " Liste " + Ok(okList) + " Filter Produkt/Art " + Ok(okFilter));
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
            sb.AppendLine(PackageForm.TestBackup(store, equip, start));
        }
    }
}
#endif
