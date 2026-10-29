#if DEMO
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest der Vorfuehr-Version (nur Build mit DEMO): Pfade, Schutz vor Produktivordnern, Zuruecksetzen, Rollen/Rechte.
    partial class MainForm
    {
        string SelfTestDemo()
        {
            StringBuilder sb = new StringBuilder();
            string tmp = Path.Combine(DemoEnv.TestRoot, "SelbsttestTemp");
            // 1) Pfade der laufenden Demo
            bool paths = fstore != null && DemoEnv.IsDemoPath(fstore.Folder) && DemoEnv.IsDemoPath(cfg.BackupFolder) && DemoEnv.IsDemoPath(cfg.PackageFolder)
                && DemoEnv.IsDemoPath(cfg.CachePath) && DemoEnv.IsDemoPath(BackupFolder) && AppId.Dir == "ArbeitsplanDemo" && cfg.Admins.Count == 0
                && users.Domains.Count == 0 && PackageForm.PrefsFile.IndexOf("ArbeitsplanDemo", StringComparison.OrdinalIgnoreCase) >= 0
                && NoPufferFile.IndexOf("ArbeitsplanDemo", StringComparison.OrdinalIgnoreCase) >= 0 && DemoEnv.CheckFolder(fstore.Folder) == null;
            sb.AppendLine("Demo Pfade: " + Ok(paths) + " (" + (fstore != null ? fstore.Folder : "kein Store") + ")");

            // 2) Produktivordner werden abgelehnt (auch mit Daten ohne Demo-Markierung, auch im Demo-Pfad)
            string fremd = Path.Combine(tmp, "fremd"), prod = Path.Combine(Path.GetTempPath(), "ArbeitsplanProduktiv" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fremd); File.WriteAllText(Path.Combine(fremd, FileStore.FileName), "x");
            AppConfig bad = DemoEnv.Config(Path.GetTempPath()); bad.DataFolder = prod; string prob;
            bool refuse = DemoEnv.CheckFolder(@"C:\Arbeitsplan\daten") != null && DemoEnv.CheckFolder(@"\\server\freigabe\Arbeitsplan\daten") != null
                && DemoEnv.CheckFolder(prod) != null && DemoEnv.CheckFolder(fremd) != null && DemoEnv.Open(bad, out prob) == null && prob != null
                && !Directory.Exists(prod) && !DemoEnv.IsDemoPath(prod) && DemoEnv.IsDemoPath(DemoEnv.DefaultRoot) && !DemoEnv.IsDemoPath(@"C:\ArbeitsplanDemoX\daten");
            sb.AppendLine("Demo Produktivordner abgelehnt: " + Ok(refuse));

            // 3) Ausgangsdaten und Zuruecksetzen (eigener Ordner, die laufende Demo bleibt unberuehrt)
            AppConfig rc = DemoEnv.Config(Path.Combine(tmp, "reset"));
            string err;
            FileStore a = DemoEnv.Open(rc, out err);
            DateTime mon = DemoEnv.MondayOf(DateTime.Today);
            int suite1 = -1; foreach (RowInfo r in a.GetRows()) if (r.Name == "Suite 1") suite1 = r.Id;
            Entry e0 = a.Get(mon, suite1);
            bool seeded = a != null && e0.Content.Length > 0 && a.GetCatalog().Master.Devices.Count > 8 && a.GetStatuses().Count >= 5
                && a.GetCatalog().Products.Count == 1 && a.Users.Users.Count == 4 && a.Users.Role("r1") != null
                && Directory.GetFiles(rc.PackageFolder, "*.apkg").Length == 2 && DemoEnv.SeedMonday(rc) == mon
                && a.Find("Lauf").Count > 4 && !a.GetCatalog().Master.Device("H1-X-0103").Active;
            sb.AppendLine("Demo Ausgangsdaten: " + Ok(seeded) + (seeded ? "" : " [Inhalt " + e0.Content.Length + ", Geraete " + a.GetCatalog().Master.Devices.Count + ", Status " + a.GetStatuses().Count + ", Produkte " + a.GetCatalog().Products.Count + ", Nutzer " + a.Users.Users.Count + ", Pakete " + Directory.GetFiles(rc.PackageFolder, "*.apkg").Length + ", Lauf " + a.Find("Lauf").Count + "]"));
            Entry ch = e0.Clone(); ch.Content = "GEAENDERT"; a.Save(mon, suite1, ch);
            User tmpUser = new User(); tmpUser.Id = 77; tmpUser.Login = "zzz"; tmpUser.RoleId = "leser"; UserChanges uc = new UserChanges(); uc.Added.Add(tmpUser); uc.Changed.Add(tmpUser); a.SaveUsers(uc);
            string beforeKey = File.ReadAllText(Path.Combine(rc.DataFolder, FileStore.KeyFileName));
            string rerr = DemoEnv.Reset(rc, true);
            FileStore b = DemoEnv.Open(rc, out err);
            bool reset = rerr == null && b != null && b.Get(mon, suite1).Content == e0.Content && b.Users.Users.Count == 4 && b.Users.FindByLogin("zzz") == null
                && File.Exists(Path.Combine(rc.DataFolder, DemoEnv.MarkerFile)) && beforeKey != File.ReadAllText(Path.Combine(rc.DataFolder, FileStore.KeyFileName));
            sb.AppendLine("Demo Zuruecksetzen: " + Ok(reset));
            AppConfig fc = DemoEnv.Config(Path.Combine(tmp, "fremdroot")); fc.DataFolder = fremd;
            string ferr = DemoEnv.Reset(fc, true);   // Ordner mit Daten ohne Markierung: Zuruecksetzen verweigert (Datei bleibt)
            sb.AppendLine("Demo Zuruecksetzen nur eigener Ordner: " + Ok(ferr != null && File.Exists(Path.Combine(fremd, FileStore.FileName))));

            // 4) Rollen setzen die richtigen Rechte
            UserDb db = b.Users;
            User le = db.FindByLogin("ebeispiel"), pr = db.FindByLogin("pwerk"), ad = db.FindByLogin("mmuster"), sl = db.FindByLogin("sleiter");
            bool roles = le.Has(Perm.Read) && le.Has(Perm.Export) && !le.Has(Perm.EditText) && !le.Has(Perm.PackageAdmin)
                && pr.Has(Perm.EditText) && pr.Has(Perm.ImportPackage) && !pr.Has(Perm.PackageAdmin) && !pr.Has(Perm.UserCreate) && !pr.Has(Perm.EditNames)
                && UserDb.IsAdmin(ad.Rights) && ad.Kuerzel == "MUMA"
                && sl.Has(Perm.EditText) && sl.Has(Perm.EditNames) && sl.Has(Perm.SortRows) && !sl.Has(Perm.UserCreate) && !sl.Has(Perm.PackageAdmin)
                && db.Role(sl.RoleId).Name == "Schichtleitung" && me.RoleId == ad.RoleId && UserDb.IsAdmin(me.Rights);
            sb.AppendLine("Demo Rollen: " + Ok(roles));
            try { Directory.Delete(tmp, true); } catch (Exception) { }
            return sb.ToString();
        }
    }
}
#endif
