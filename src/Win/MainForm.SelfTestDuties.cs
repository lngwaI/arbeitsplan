#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Pflichtpunkte/Rundgaenge (nur Core): Datenformat v3, Merge, Feiertage/ISO-KW, Regeln, Wochenhaken.
    partial class MainForm
    {
        static DateTime D(int y, int m, int d) { return new DateTime(y, m, d); }
        static Entry E(string content, bool lkz, string variant, params string[] marks)
        {
            Entry e = new Entry(); e.Content = content; e.Lkz = lkz; BufferSet.SetTypes(e, variant); e.Marks.AddRange(marks);
            return e;
        }

        static void SelfTestDuties(StringBuilder sb)
        {
            // ---------- Feiertage / ISO-KW ----------
            DutyConfig bw = DutyConfig.Default(), by = DutyConfig.Default(); by.Land = "BY";
            DutyConfig ex = DutyConfig.Default(); ex.ExtraHolidays.Add(D(2026, 7, 1)); ex.RemovedHolidays.Add(D(2026, 12, 26));
            bool okEaster = Holidays.Easter(2026) == D(2026, 4, 5) && Holidays.Easter(2027) == D(2027, 3, 28);
            bool okMov = Holidays.Is(D(2026, 4, 3), bw) && Holidays.Is(D(2026, 4, 6), bw) && Holidays.Is(D(2026, 5, 14), bw) && Holidays.Is(D(2026, 5, 25), bw)
                && Holidays.Is(D(2026, 6, 4), bw) && !Holidays.Is(D(2026, 6, 3), bw) && !Holidays.Is(D(2026, 4, 5).AddDays(-1), bw);
            bool okFix = Holidays.Is(D(2026, 1, 6), bw) && Holidays.Is(D(2026, 10, 3), bw) && Holidays.Is(D(2026, 11, 1), bw) && Holidays.Is(D(2026, 12, 26), bw) && !Holidays.Is(D(2026, 12, 24), bw);
            bool okAug = !Holidays.Is(D(2026, 8, 15), bw) && Holidays.Is(D(2026, 8, 15), by);
            bool okEx = Holidays.Is(D(2026, 7, 1), ex) && !Holidays.Is(D(2026, 12, 26), ex) && !Holidays.Is(D(2026, 7, 1), bw);
            bool okKw = IsoWeek.Number(D(2026, 12, 31)) == 53 && IsoWeek.Number(D(2027, 1, 1)) == 53 && IsoWeek.Monday(D(2027, 1, 1)) == D(2026, 12, 28)
                && IsoWeek.Monday(D(2026, 12, 28)) == D(2026, 12, 28) && D(2026, 12, 28).DayOfWeek == DayOfWeek.Monday
                && IsoWeek.Number(D(2027, 1, 4)) == 1 && IsoWeek.Number(D(2026, 1, 1)) == 1 && IsoWeek.Number(D(2026, 10, 27)) == 44;
            sb.AppendLine("Feiertage: Ostern=" + Ok(okEaster) + " beweglich (Fronleichnam 04.06.2026)=" + Ok(okMov) + " fest=" + Ok(okFix) + " BW ohne 15.8./BY mit=" + Ok(okAug)
                + " Extra/Entfernt=" + Ok(okEx) + " ISO-KW53 (31.12.2026/01.01.2027, Montag 28.12.)=" + Ok(okKw));

            // ---------- LKZ-Regel (alle Faelle) ----------
            MemoryStore s = new MemoryStore();
            int row = s.GetRows()[0].Id, row2 = s.GetRows()[1].Id;
            DateTime w43 = D(2026, 10, 19), today = D(2026, 10, 21);   // Mo KW43, heute = Mi
            s.Save(w43, row, E("Lauf 1", true, ""));
            s.Save(w43.AddDays(1), row, E("Lauf 2", false, ""));
            s.Save(w43.AddDays(2), row, E("", true, ""));
            s.Save(w43.AddDays(3), row, E("Lauf 3", false, ""));
            s.Save(w43.AddDays(8), row, E("   ", false, ""));              // KW44 Di: nur Leerzeichen = leer
            s.Save(w43.AddDays(20), row, E("", true, ""));                  // KW45 So mit Haken
            CheckDef lkz = CheckDef.Lkz(row);
            object[][] cases = {
                new object[] { "belegt + Haken", w43, DayState.Done },
                new object[] { "belegt ohne Haken, <= heute", w43.AddDays(1), DayState.Missing },
                new object[] { "leer + Haken (gemessen)", w43.AddDays(2), DayState.Done },
                new object[] { "belegt ohne Haken, Zukunft", w43.AddDays(3), DayState.Due },
                new object[] { "leer, KW erfüllt", w43.AddDays(4), DayState.NotNeeded },
                new object[] { "leer, KW offen", w43.AddDays(7), DayState.WeekOpen },
                new object[] { "nur Leerzeichen, KW offen", w43.AddDays(8), DayState.WeekOpen },
                new object[] { "leer, KW durch So erfüllt", w43.AddDays(14), DayState.NotNeeded },
                new object[] { "leer + Haken am So", w43.AddDays(20), DayState.Done } };
            bool okLkz = true;
            StringBuilder tab = new StringBuilder();
            foreach (object[] c in cases)
            {
                DayState got = CheckEval.Day(s, lkz, (DateTime)c[1], today, bw);
                bool ok = got == (DayState)c[2]; okLkz &= ok;
                tab.AppendLine("  " + ((string)c[0]).PadRight(30) + ((DateTime)c[1]).ToString("dd.MM.") + " -> " + got + " (" + Ok(ok) + ")");
            }
            bool okLkzW = CheckEval.Week(s, lkz, w43, today, bw) == DayState.Missing && CheckEval.Week(s, lkz, w43.AddDays(7), today, bw) == DayState.WeekOpen
                && CheckEval.Week(s, lkz, w43.AddDays(14), today, bw) == DayState.Done;
            sb.AppendLine("LKZ-Regel: Tage=" + Ok(okLkz) + " Wochen=" + Ok(okLkzW));
            sb.Append(tab.ToString());

            // ---------- Tagesprobe (Werktage, mobil = automatisch) und Wochen-WFI ----------
            DateTime w23 = D(2026, 6, 1), t23 = D(2026, 6, 3);   // KW23, Do 04.06. Fronleichnam, heute = Mi
            CheckDef probe = new CheckDef(); probe.Key = "PROBE"; probe.RowId = row2; probe.Rule = CheckRule.Workdays; probe.Cond = "+mobil";
            CheckDef wfi = new CheckDef(); wfi.Key = "WFIW"; wfi.RowId = row2; wfi.Rule = CheckRule.WeeklyOnce; wfi.Cond = "!stationaer";
            s.Save(w23, row2, E("Herstellung", false, "", "PROBE"));
            s.Save(w23.AddDays(1), row2, E("Herstellung", false, "mobil"));
            bool okProbe = CheckEval.Day(s, probe, w23, t23, bw) == DayState.Done && CheckEval.Day(s, probe, w23.AddDays(1), t23, bw) == DayState.Auto
                && CheckEval.Day(s, probe, w23.AddDays(2), t23, bw) == DayState.Missing && CheckEval.Day(s, probe, w23.AddDays(3), t23, bw) == DayState.NA
                && CheckEval.Day(s, probe, w23.AddDays(4), t23, bw) == DayState.Due && CheckEval.Day(s, probe, w23.AddDays(5), t23, bw) == DayState.NA
                && CheckEval.Week(s, probe, w23, t23, bw) == DayState.Missing;
            bool wfiOpen = CheckEval.Week(s, wfi, w23, t23, bw) == DayState.WeekOpen && CheckEval.Day(s, wfi, w23, t23, bw) == DayState.WeekOpen;
            s.Save(w23.AddDays(-5), row2, E("", false, "", "WFIW"));          // KW22 Mi erledigt
            bool wfiDone = CheckEval.Week(s, wfi, w23.AddDays(-7), t23, bw) == DayState.Done && CheckEval.Day(s, wfi, w23.AddDays(-7), t23, bw) == DayState.NotNeeded
                && CheckEval.Day(s, wfi, w23.AddDays(-5), t23, bw) == DayState.Done && CheckEval.Week(s, wfi, w23.AddDays(-14), t23, bw) == DayState.Missing;
            s.Save(w23.AddDays(2), row2, E("Herstellung", false, "stationaer"));
            bool wfiNA = CheckEval.Week(s, wfi, w23, t23, bw) == DayState.Done && CheckEval.Day(s, wfi, w23.AddDays(4), t23, bw) == DayState.Auto;
            sb.AppendLine("Tagesprobe: Werktage/Feiertag/mobil=" + Ok(okProbe) + "  Wochen-WFI: offen=" + Ok(wfiOpen) + " erledigt/fehlt=" + Ok(wfiDone) + " stationär entfällt=" + Ok(wfiNA));

            // ---------- Merge Marks/Variante ----------
            Entry b = E("x", false, "mobil", "A"), mi = E("x", false, "mobil", "A", "B"), th = E("x", false, "stationaer");
            List<string> cl = new List<string>();
            Entry r = Conflict.Merge(b, mi, th, cl);
            bool okM = r.Marks.Count == 1 && r.Marks[0] == "B" && r.Types == "stationaer" && cl.Count == 0;
            List<string> cl2 = new List<string>();
            Entry r2 = Conflict.Merge(E("x", false, ""), E("x", false, "mobil"), E("x", false, "stationaer"), cl2);
            Entry r3 = Conflict.Merge(E("x", false, ""), E("x", false, "mobil"), E("x", false, "stationaer"), new List<string>(), true);
            bool okV = !cl2.Contains("Herstellung") && r2.Types == "mobil|stationaer" && r3.Types == "mobil|stationaer";   // beide geaendert = Vereinigung
            sb.AppendLine("Merge: Pflichtpunkte je Schlüssel=" + Ok(okM) + " Variante beidseitig = Vereinigung=" + Ok(okV));

            // ---------- Datenformat v3 / v2 / v4 ----------
            List<RowInfo> rows = s.GetRows(); rows[0].Mode = ShiftMode.FS; rows[1].Mode = ShiftMode.Task; rows[1].Variants = "mobil|stationaer";
            s.SaveStructure(rows);
            DutyConfig dc = s.GetDuties(); dc.Land = "BY"; dc.Defs.Add(wfi); dc.ExtraHolidays.Add(D(2026, 7, 1)); dc.RemovedHolidays.Add(D(2026, 12, 26)); dc.Rounds[4].Active = false;
            s.SaveDuties(dc);
            WeekMark wm = new WeekMark(); wm.Monday = w43; wm.Key = "GMP1"; wm.Done = true;
            s.SaveWeekMark(wm, MarkAudit());
            StoreData x3 = Backup.FromXml(Backup.ToXml(s.Snapshot()));
            Entry e3 = null; foreach (StoredEntry se in x3.Entries) if (se.Day == w23 && se.RowId == row2) e3 = se.Data;
            AuditEntry a3 = x3.Audit.Find(delegate(AuditEntry a) { return a.RowId == -1; });
            bool okV3 = x3.Rows[0].Mode == ShiftMode.FS && x3.Rows[1].Mode == ShiftMode.Task && x3.Rows[1].Variants == "mobil|stationaer" && x3.Rows[2].Mode == ShiftMode.FSN
                && e3 != null && e3.Marks.Count == 1 && e3.Marks[0] == "PROBE" && x3.Entries.Exists(delegate(StoredEntry se) { return se.Data.Types == "stationaer"; })
                && x3.Duties.Land == "BY" && x3.Duties.Defs.Count == 3 && x3.Duties.Defs[2].Rule == CheckRule.WeeklyOnce && x3.Duties.Defs[2].Cond == "!stationaer"   // 2 Puffer-Standards + wfi
                && x3.Duties.Rounds.Count == 5 && !x3.Duties.Rounds[4].Active && x3.Duties.ExtraHolidays[0] == D(2026, 7, 1) && x3.Duties.RemovedHolidays[0] == D(2026, 12, 26)
                && x3.WeekMarks.Count == 1 && x3.WeekMarks[0].Done && x3.WeekMarks[0].Version == 1 && a3 != null && a3.Mark == "GMP1" && a3.Value == "1" && a3.Day == w43;
            string v2 = "<Arbeitsplan version=\"2\"><Zeilen><Zeile id=\"1\" name=\"Suite 1\" l0=\"F\" l1=\"S\" l2=\"N\" min=\"3\" lkz=\"1\"/></Zeilen>"
                + "<Bloecke><Block tag=\"2026-10-19\" zeile=\"1\" ver=\"2\" lkz=\"1\" inhalt=\"Lauf\" p0=\"MUMA\" p1=\"\" p2=\"\"/></Bloecke><Katalog/>"
                + "<Protokoll><Eintrag zeit=\"1\" login=\"mmuster\" kuerzel=\"MUMA\" tag=\"2026-10-19\" zeile=\"1\" name=\"Suite 1\" ver=\"2\"/></Protokoll></Arbeitsplan>";
            StoreData x2 = Backup.FromXml(Encoding.UTF8.GetBytes(v2));
            bool okV2 = x2.Rows[0].Mode == ShiftMode.FSN && x2.Rows[0].Variants == "" && x2.Entries[0].Data.Types == "" && x2.Entries[0].Data.Marks.Count == 0
                && x2.Entries[0].Data.Lkz && x2.Duties.Land == "BW" && x2.Duties.Rounds.Count == 5 && x2.Duties.Rounds[0].Name == "1. GMP-Rundgang"
                && x2.WeekMarks.Count == 0 && x2.Audit[0].Mark == "";
            bool okV4 = false;
            try { Backup.FromXml(Encoding.UTF8.GetBytes("<Arbeitsplan version=\"" + (Backup.FormatVersion + 1) + "\"><Zeilen><Zeile id=\"1\" name=\"A\" min=\"3\"/></Zeilen><Katalog/></Arbeitsplan>")); }
            catch (NewerFormatException) { okV4 = true; }
            XmlDocument ud = new XmlDocument();
            ud.LoadXml("<Nutzer><Paket name=\"Produktion\"><R v=\"Lesen\"/><R v=\"TextAendern\"/></Paket><Paket name=\"Leser\"><R v=\"Lesen\"/></Paket></Nutzer>");
            UserDb old = UserDb.ReadXml(ud.DocumentElement), def = UserDb.Default();
            bool okPerm = Array.IndexOf(old.Package("Produktion").Rights, Perm.Rounds) >= 0 && Array.IndexOf(old.Package("Leser").Rights, Perm.Rounds) < 0
                && Array.IndexOf(def.Package("Produktion").Rights, Perm.Rounds) >= 0 && Array.IndexOf(def.Package("Leser").Rights, Perm.Rounds) < 0
                && Array.IndexOf(def.Package("Admin").Rights, Perm.Rounds) >= 0 && Perm.Label(Perm.Rounds) == "Rundgänge abhaken";
            sb.AppendLine("Datenformat v" + Backup.FormatVersion + ": Rundreise=" + Ok(okV3) + " v2 lesbar=" + Ok(okV2) + " neuere abgelehnt=" + Ok(okV4) + " Recht Rundgänge (alt/neu)=" + Ok(okPerm));

            // ---------- Wochenhaken: zwei Stores auf derselben Datendatei ----------
            string dir = Path.Combine(Path.GetTempPath(), "apdb_duties");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            FileStore fa = new FileStore(dir, false, null), fb = new FileStore(dir, false, null);
            WeekMark ma = new WeekMark(); ma.Monday = w43; ma.Key = "GMP1"; ma.Done = true;
            WeekMark mb = ma.Clone();                                           // B kennt den Haken von A noch nicht (Version 0)
            bool sa = fa.SaveWeekMark(ma, MarkAudit()), sbConf = !fb.SaveWeekMark(mb, MarkAudit());
            List<WeekMark> lb = fb.GetWeekMarks(w43);                           // nach dem Konflikt neu gelesen
            bool reread = lb.Count == 1 && lb[0].Done && lb[0].Version == 1;
            WeekMark mb2 = lb[0]; mb2.Done = false;
            bool sb2 = fb.SaveWeekMark(mb2, MarkAudit()) && mb2.Version == 2;
            fa.Refresh();
            List<WeekMark> la = fa.GetWeekMarks(w43);
            Entry fe = E("Lauf", false, "mobil", "PROBE");
            fa.Save(w23, 1, fe);
            DutyConfig fd = fa.GetDuties(); fd.Land = "BY"; fa.SaveDuties(fd);
            FileStore fc = new FileStore(dir, false, null);
            List<AuditEntry> au = fc.GetAudit(w43, -1);
            Entry fe2 = fc.Get(w23, 1);
            bool okFile = la.Count == 1 && !la[0].Done && la[0].Version == 2 && au.Count == 2 && au[0].Value == "1" && au[1].Value == "0" && au[1].Mark == "GMP1"
                && fe2 != null && fe2.Types == "mobil" && fe2.Marks.Count == 1 && fc.GetDuties().Land == "BY" && fc.GetDuties().Rounds.Count == 5;
            sb.AppendLine("Wochenhaken: gespeichert=" + Ok(sa) + " Konflikt erkannt=" + Ok(sbConf) + " neu gelesen=" + Ok(reread) + " zweiter PC speichert=" + Ok(sb2)
                + " Datei/Protokoll/Pflichten=" + Ok(okFile));
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        static AuditEntry MarkAudit()
        {
            AuditEntry a = new AuditEntry(); a.Time = DateTime.Now; a.Login = "mmuster"; a.Kuerzel = "MUMA"; a.RowName = "Rundgang";
            return a;
        }
    }
}
#endif
