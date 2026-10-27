#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Block F: Plan-Datei (.applan) Rundreise, Manipulation, Ansicht ohne Datenordner;
    // Faelligkeit/Einstellung der automatischen Sicherung; Paket-Editor beim Vergroessern.
    partial class MainForm
    {
        void SelfTestF(StringBuilder sb)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ap-plan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                SelfTestPlanFile(sb, dir);
                SelfTestBackupPlan(sb, dir);
            }
            catch (Exception ex) { sb.AppendLine("Block F: FEHLER " + ex.GetType().Name + ": " + ex.Message); }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
            sb.AppendLine(PackageForm.TestResize(store, equip, start));
        }

        static void SelfTestPlanFile(StringBuilder sb, string dir)
        {
            // eigener Speicher: Block mit Format/Umlauten/Kuerzel/LKZ in der Woche, einer danach, Protokolleintrag
            MemoryStore src = new MemoryStore();
            List<RowInfo> rows = src.GetRows();
            DateTime mo = MondayOf(DateTime.Today);
            Entry e = new Entry(); e.Content = "Plan äöü ß \"Test\" 100 %\nZeile 2"; e.People[0] = "MUMA"; e.Lkz = true;
            FormatSpan f = new FormatSpan(); f.Start = 0; f.Length = 4; f.Rgb = 0xC00000; f.Size = 12f; f.Bold = true; e.Spans.Add(f);
            AuditEntry a = new AuditEntry(); a.Login = "mmuster"; a.Time = DateTime.Now;
            src.SaveWithAudit(mo.AddDays(2), rows[0].Id, e, a);
            Entry later = new Entry(); later.Content = "außerhalb"; src.Save(mo.AddDays(7), rows[1].Id, later);

            string path = Path.Combine(dir, "woche" + PlanFile.Ext);
            PlanFile.Write(PlanFile.Take(src, mo, 7), path);
            PlanSnapshot p = PlanFile.Read(path);
            Entry back = null; bool inRange = true;
            foreach (StoredEntry s in p.Data.Entries)
            {
                if (s.Day < mo || s.Day >= mo.AddDays(7)) inRange = false;
                if (s.Day == mo.AddDays(2) && s.RowId == rows[0].Id) back = s.Data;
            }
            bool okRt = back != null && back.Content == e.Content && back.SpanKey() == e.SpanKey() && back.People[0] == "MUMA" && back.Lkz
                && p.From == mo && p.Days == 7 && p.Data.Rows.Count == rows.Count && p.Name == "woche" + PlanFile.Ext;
            bool okOnly = inRange && p.Data.Users == null && p.Data.Audit.Count == 0 && p.Data.Catalog.Positions.Count == 0;

            // Manipulation: Inhalt veraendert und neu gepackt (GZip gueltig, Hash falsch), Byte in der Datei, falsche Dateiart
            byte[] raw;
            using (FileStream fs = File.OpenRead(path)) using (MemoryStream ms = Backup.Unzip(fs)) raw = ms.ToArray();
            int at = IndexOf(raw, Encoding.UTF8.GetBytes("Zeile 2"));
            if (at > 0) raw[at] = (byte)'X';
            string bad1 = Path.Combine(dir, "inhalt" + PlanFile.Ext), bad2 = Path.Combine(dir, "byte" + PlanFile.Ext), bad3 = Path.Combine(dir, "sicherung" + PlanFile.Ext);
            using (FileStream fs = File.Create(bad1)) using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress)) gz.Write(raw, 0, raw.Length);
            byte[] file = File.ReadAllBytes(path); file[file.Length / 2] ^= 0xFF; File.WriteAllBytes(bad2, file);
            Backup.Write(src.Snapshot(), bad3, null);
            bool rej = Rejected(bad1) && Rejected(bad2) && Rejected(bad3) && at > 0;

            // Ansicht: eigenes Fenster mit Speicher aus der Datei, ohne Datenordner, nur Lesen
            bool okView;
            using (MainForm v = new MainForm("ansicht", true, null, null, p))
            {
                Entry ve = v.store.Get(mo.AddDays(2), rows[0].Id);
                bool saveFails = false;
                try { v.store.Save(mo.AddDays(2), rows[0].Id, ve); } catch (IOException) { saveFails = true; }
                okView = v.fstore == null && v.cfg == null && v.systemPc && saveFails && ve != null && ve.Content == e.Content
                    && !v.me.Has(Perm.EditText) && v.start == mo && v.Text.Contains("nur ansehen");
            }
            sb.AppendLine("Plan-Datei: Rundreise=" + Ok(okRt) + " nur Zeitraum/ohne Nutzer+Protokoll=" + Ok(okOnly) + " manipuliert abgelehnt=" + Ok(rej)
                + " Ansicht ohne Datenordner=" + Ok(okView) + " (" + new FileInfo(path).Length + " Byte)");
        }

        static bool Rejected(string path)
        {
            try { PlanFile.Read(path); return false; }
            catch (InvalidDataException) { return true; }
        }

        static void SelfTestBackupPlan(StringBuilder sb, string dir)
        {
            DateTime now = new DateTime(2026, 10, 28, 12, 0, 0);
            bool okDue = Backup.Due(null, now, 24) && !Backup.Due(now.AddHours(-23), now, 24) && Backup.Due(now.AddHours(-24), now, 24)
                && Backup.Due(now.AddHours(-2), now, 1) && !Backup.Due(now.AddMinutes(-30), now, 0)   // 0 -> 1 Stunde
                && !Backup.Due(now.AddHours(1), now, 24)                                               // Server-Uhr vor: nicht faellig
                && Backup.ClampHours(0) == 1 && Backup.ClampHours(100000) == Backup.MaxHours;

            string ini = Path.Combine(dir, "sicherung.ini");
            bool okIni = Backup.LoadHours(ini, 24) == 24 && Backup.LoadHours(ini, 0) == 1;
            Backup.SaveFolder(ini, @"C:\Sicherung"); Backup.SaveSetting(ini, "stunden", "6"); Backup.SaveFolder(ini, @"D:\Neu");
            okIni &= Backup.LoadHours(ini, 24) == 6 && Backup.LoadFolder(ini, "x") == @"D:\Neu";

            File.WriteAllText(Path.Combine(dir, "Arbeitsplan.ini"), "SicherungAlleStunden=12\r\n", Encoding.UTF8);
            bool okCfg = AppConfig.Load(dir).BackupHours == 12;
            File.WriteAllText(Path.Combine(dir, "Arbeitsplan.ini"), "# leer\r\n", Encoding.UTF8);
            okCfg &= AppConfig.Load(dir).BackupHours == 24;

            string bdir = Path.Combine(dir, "sicherung");
            bool okRun = Backup.AutoDue(bdir, 24);
            Backup.WriteTo(new MemoryStore().Snapshot(), bdir, null, null);   // wie der Timer: Stand vorher geholt
            okRun &= !Backup.AutoDue(bdir, 24) && Backup.Files(bdir).Length == 1;
            sb.AppendLine("Sicherung-Zeitplan: faellig=" + Ok(okDue) + " Einstellung sicherung.ini=" + Ok(okIni) + " SicherungAlleStunden=" + Ok(okCfg)
                + " Hintergrund-Schreiben=" + Ok(okRun));
        }
    }
}
#endif
