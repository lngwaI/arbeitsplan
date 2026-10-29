#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Equipment-Status aus Datei (Core/FileStatus.cs): Standard, Eintraege, Ablauf, alte Datei, kaputte Zeilen, Vorrang manuell, ini.
    partial class MainForm
    {
        static void SelfTestEquipFile(StringBuilder sb)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ap-equipstatus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string f = Path.Combine(dir, "equipstatus.csv");
                DateTime now = DateTime.Now;

                // Datei fehlt: alle nicht gereinigt
                FileStatusProvider none = new FileStatusProvider(f, 24);
                bool okMiss = none.Lookup("H1-T-0001", now).Status == EquipStatus.NichtGereinigt && none.ListNumbers().Count == 0;

                // Eintraege, Gross/Klein, BOM, Leerzeile, kaputte Zeilen, Ablauf
                string csv = "﻿ID;Status;Zeitstempel;gueltig_bis\r\n"
                    + "H1-T-0001;Gereinigt;2026-10-29 06:00;\r\n"
                    + "h1-t-0002; STERILISIERT ;29.10.2026 06:30;2099-12-31 23:59\r\n"
                    + "H1-T-0003;In Prozess;2026-10-29 07:00:15;2020-01-01 00:00\r\n"
                    + "\r\n"
                    + "H1-T-0004;Quatsch;2026-10-29 07:00;\r\n"
                    + "H1-T-0005;Freigabe;gestern;\r\n"
                    + "nur eine Spalte\r\n"
                    + "H1-T-0006;Quarantäne;2026-10-29 08:00;\r\n"
                    + "H1-T-0007;Gereinigt\r\n"
                    + "H1-T-0008;Regeneriert;2026-10-29 08:00;morgen\r\n";
                File.WriteAllText(f, csv, new UTF8Encoding(true));
                FileStatusProvider p = new FileStatusProvider(f, 24);
                bool okEntry = p.Lookup("H1-T-0001", now).Status == EquipStatus.Gereinigt && p.Lookup("H1-T-0001", now).Timestamp == new DateTime(2026, 10, 29, 6, 0, 0)
                    && p.Lookup("H1-T-0002", now).Status == EquipStatus.Sterilisiert && p.Lookup("h1-t-0001", now).Status == EquipStatus.Gereinigt
                    && p.Lookup("H1-T-0006", now).Status == EquipStatus.Quarantaene && p.Lookup("H1-T-0007", now).Status == EquipStatus.Gereinigt
                    && p.Lookup("H1-T-0099", now).Status == EquipStatus.NichtGereinigt;
                bool okExp = p.Lookup("H1-T-0003", now).Status == EquipStatus.NichtGereinigt && p.Lookup("H1-T-0003", new DateTime(2019, 1, 1)).Status == EquipStatus.InProzess
                    && new FileStatusProvider(f, 8760).Lookup("H1-T-0001", now.AddDays(100)).Status == EquipStatus.Gereinigt;   // ohne gueltig_bis nie abgelaufen
                EquipStatus ts;
                bool okBad = p.Lookup("H1-T-0004", now).Status == EquipStatus.NichtGereinigt && p.Lookup("H1-T-0005", now).Status == EquipStatus.NichtGereinigt
                    && p.Lookup("H1-T-0008", now).Status == EquipStatus.NichtGereinigt && p.ListNumbers().Count == 5
                    && FileStatusProvider.TryStatus("Nicht gereinigt (ohne Timer)", out ts) && ts == EquipStatus.NichtGereinigtOhneTimer;

                // zu alte Datei: alles nicht gereinigt; mit groesserem Maximum wieder gueltig
                File.SetLastWriteTime(f, now.AddHours(-25));
                bool okOld = new FileStatusProvider(f, 24).Lookup("H1-T-0001", now).Status == EquipStatus.NichtGereinigt
                    && new FileStatusProvider(f, 48).Lookup("H1-T-0001", now).Status == EquipStatus.Gereinigt;

                // Neu laden nur bei geaenderter Zeit/Groesse
                File.SetLastWriteTime(f, now);
                p.Reload();
                bool okNew = p.Lookup("H1-T-0001", now).Status == EquipStatus.Gereinigt;
                File.WriteAllText(f, "H1-T-0001;Freigabe;2026-10-29 09:00;\r\n");
                p.Reload();
                okNew &= p.Lookup("H1-T-0001", now).Status == EquipStatus.Freigabe && p.Lookup("H1-T-0006", now).Status == EquipStatus.NichtGereinigt;
                File.Delete(f);
                p.Reload();
                okNew &= p.Lookup("H1-T-0001", now).Status == EquipStatus.NichtGereinigt;
                p.Get("H1-T-0001");   // Hintergrundabfrage anstossen, blockiert nicht

                // Vorrang manuell: <= 14 Tage (ManuellTage) vor der Datei, danach die Datei
                File.WriteAllText(f, "H1-T-0001;Gereinigt;2026-10-29 06:00;\r\n");
                FileStatusProvider fp = new FileStatusProvider(f, 24);
                MemoryStore ms = new MemoryStore();
                ManualStatusProvider mp = new ManualStatusProvider(ms, fp);
                ms.SetStatus(Dev("H1-T-0001", EquipStatus.Freigabe, DateTime.Now.AddDays(-3)), null); mp.Reload();
                bool okMan = mp.Get("H1-T-0001").Status == EquipStatus.Freigabe && mp.IsManual("H1-T-0001");
                ms.SetStatus(Dev("H1-T-0001", EquipStatus.Freigabe, DateTime.Now.AddDays(-15)), null); mp.Reload();
                okMan &= mp.Get("H1-T-0001").Status == EquipStatus.Gereinigt;
                mp.ManualDays = 20;
                okMan &= mp.Get("H1-T-0001").Status == EquipStatus.Freigabe;
                okMan &= mp.Get("H1-T-0777").Status == EquipStatus.NichtGereinigt;   // ohne Eintrag: Standard

                // ini
                File.WriteAllText(Path.Combine(dir, "Arbeitsplan.ini"), "Statusdatei=x.csv\r\nStatusdateiMaxAlter=48\r\nManuellTage=7\r\n");
                AppConfig c = AppConfig.Load(dir);
                AppConfig d = AppConfig.Load(Path.Combine(dir, "leer"));
                bool okIni = c.StatusFile == "x.csv" && c.StatusMaxHours == 48 && c.ManualDays == 7 && c.StatusPath == Path.Combine(c.DataFolder, "x.csv")
                    && d.StatusFile == "equipstatus.csv" && d.StatusMaxHours == 24 && d.ManualDays == 14;

                sb.AppendLine("Status aus Datei: Datei fehlt=" + Ok(okMiss) + " Eintrag/BOM/Gross-klein=" + Ok(okEntry) + " Ablauf=" + Ok(okExp) + " kaputte Zeilen=" + Ok(okBad)
                    + " zu alte Datei=" + Ok(okOld) + " Neu laden=" + Ok(okNew) + " Vorrang manuell=" + Ok(okMan) + " ini=" + Ok(okIni));
            }
            catch (Exception ex) { sb.AppendLine("Status aus Datei: Ausnahme " + ex.GetType().Name + " " + ex.Message); }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        }

        static DeviceStatus Dev(string id, EquipStatus st, DateTime at)
        {
            DeviceStatus d = new DeviceStatus(); d.Id = id; d.Status = st; d.At = at; d.By = "test";
            return d;
        }
    }
}
#endif
