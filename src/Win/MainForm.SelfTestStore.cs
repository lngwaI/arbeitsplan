#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;
using System.IO;
using System.Text;

namespace Arbeitsplan.Win
{
    // Selbsttests Datendatei (FileStore), Verlauf und Sicherung; Testformular Verlauf/Sicherung.
    partial class MainForm
    {
        // Datendatei: Erststart, Neustart = Daten da, kein Klartext, zwei PCs (Refresh, Konflikt), Historie, .bak, NurLesen,
        // Manipulation/fremde Datei/unerreichbar -> OFFLINE statt Absturz, wieder erreichbar, Sperrdateien.
        void SelfTestFiles(System.Text.StringBuilder sb)
        {
            string root = Path.Combine(Path.GetTempPath(), "apdb_selftest");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string dir = Path.Combine(root, "daten"), file = Path.Combine(dir, FileStore.FileName), cA = Path.Combine(root, "cacheA.apdb"), none = Path.Combine(root, "keincache.apdb");
            DateTime day = new DateTime(2026, 3, 2);
            User u = new User(); u.Login = "test"; u.Kuerzel = "TEST";

            Directory.CreateDirectory(dir);   // der FileStore legt keinen Datenordner an
            FileStore a = new FileStore(dir, false, cA);
            bool okInit = !a.Offline && a.Problem == null && a.GetRows().Count == 12 && a.Users.Users.Count == 0 && a.Users.Packages.Count == 3 && File.Exists(file);
            RowInfo row = a.GetRows()[0];
            Entry e = new Entry(); e.Content = "Geheimlauf Öl-4711"; e.People[0] = "ZQXW";
            DateTime t0 = DateTime.Now;
            bool s1 = Audit.Save(a, day, row, u, e);
            double ms = (DateTime.Now - t0).TotalMilliseconds;
            UserChanges uc = new UserChanges(); User pr = a.Users.Add("Erika", "Probe", "eprobe", "Produktion"); pr.Kuerzel = "PRER";
            uc.Changed.Add(pr); uc.Added.Add(pr); a.SaveUsers(uc);

            FileStore b = new FileStore(dir, false, null), c = new FileStore(dir, false, null);   // "Neustart" bzw. zweiter/dritter PC
            Entry eb = b.Get(day, row.Id);
            bool okRound = s1 && eb != null && eb.Content == e.Content && eb.People[0] == "ZQXW" && b.GetAudit(day, row.Id).Count == 1
                && b.Users.FindByLogin("eprobe") != null && b.Users.FindByLogin("eprobe").Kuerzel == "PRER";
            byte[] raw = File.ReadAllBytes(file);
            bool okCipher = IndexOf(raw, Encoding.UTF8.GetBytes("Geheimlauf")) < 0 && IndexOf(raw, Encoding.Unicode.GetBytes("Geheimlauf")) < 0
                && IndexOf(raw, Encoding.UTF8.GetBytes("ZQXW")) < 0 && IndexOf(raw, Encoding.UTF8.GetBytes("eprobe")) < 0 && IndexOf(raw, Encoding.UTF8.GetBytes("<Arbeitsplan")) < 0;

            Entry stale = b.Get(day, row.Id), staleC = c.Get(day, row.Id);
            Entry ea = a.Get(day, row.Id); ea.Content = "Von A geändert"; bool s2 = Audit.Save(a, day, row, u, ea);
            bool okSync = s2 && b.Refresh() && b.Get(day, row.Id).Content == "Von A geändert" && !b.Refresh();
            stale.Content = "B veraltet"; staleC.Content = "C veraltet";
            bool okConflict = !b.Save(day, row.Id, stale) && !c.Save(day, row.Id, staleC) && c.Get(day, row.Id).Content == "Von A geändert";   // C: ohne Refresh, Neu-Einlesen vor dem Schreiben

            List<AuditEntry> au = new FileStore(dir, false, null).GetAudit(day, row.Id);
            bool okAudit = au.Count == 2 && au[0].After.Content == e.Content && au[1].Before.Content == e.Content && au[1].After.Content == "Von A geändert" && au[1].Kuerzel == "TEST";
            bool okBak = File.Exists(file + ".bak1") && File.Exists(file + ".bak3") && !File.Exists(file + ".bak4") && !File.Exists(file + ".tmp");

            FileStore r = new FileStore(dir, true, null);
            bool roThrows = false;
            try { Entry x = r.Get(day, row.Id); x.Content = "nein"; r.Save(day, row.Id, x); } catch (IOException) { roThrows = true; }
            bool okRO = r.ReadOnly && !r.Offline && r.Get(day, row.Id).Content == "Von A geändert" && roThrows;

            string dirM = Path.Combine(root, "manipuliert"); Directory.CreateDirectory(dirM);
            string keyFile = Path.Combine(dir, FileStore.KeyFileName);
            File.Copy(keyFile, Path.Combine(dirM, FileStore.KeyFileName));
            raw[raw.Length / 2] ^= 0x20; File.WriteAllBytes(Path.Combine(dirM, FileStore.FileName), raw);
            FileStore m1 = new FileStore(dirM, false, cA);   // mit lokalem Stand (Cache von A)
            bool okTamper = m1.Offline && m1.ReadOnly && m1.Problem != null && m1.Problem.Contains("nicht gelesen") && m1.Get(day, row.Id) != null && m1.Get(day, row.Id).Content == "Von A geändert";

            string dirF = Path.Combine(root, "fremd"), fF = Path.Combine(dirF, FileStore.FileName); Directory.CreateDirectory(dirF);
            File.WriteAllText(fF, "Tag;Inhalt\r\n1;Test");
            FileStore m2 = new FileStore(dirF, false, none);
            bool m2Throws = false;
            try { m2.Save(day, row.Id, new Entry()); } catch (IOException) { m2Throws = true; }
            bool okForeign = m2.Offline && m2.GetRows().Count == 12 && m2.Get(day, row.Id) == null && m2Throws && File.ReadAllText(fF) == "Tag;Inhalt\r\n1;Test";

            string blocker = Path.Combine(root, "datei.txt"); File.WriteAllText(blocker, "x");
            FileStore m3 = new FileStore(Path.Combine(blocker, "daten"), false, none);   // Ordner nicht anlegbar = nicht erreichbar
            bool okOffline = m3.Offline && m3.Problem.Contains("nicht erreichbar") && !m3.Refresh() && m3.GetRows().Count == 12;

            File.Copy(file, fF, true); File.Copy(keyFile, Path.Combine(dirF, FileStore.KeyFileName));   // Ordner wieder erreichbar/gueltig
            bool okBack = m2.Refresh() && !m2.Offline && !m2.ReadOnly && m2.Get(day, row.Id).Content == "Von A geändert";

            // Zwei PCs, B speichert direkt nach A mit altem Stand; Dateizeit wie bei veraltetem SMB-Cache zurueckgesetzt
            FileStore d1 = new FileStore(dir, false, null), d2 = new FileStore(dir, false, null);
            Entry x1 = d1.Get(day, row.Id), x2 = d2.Get(day, row.Id);
            DateTime w0 = File.GetLastWriteTimeUtc(file);
            int auN = d1.GetAudit(day, row.Id).Count, v2 = x2.Version;
            x1.Content = "A zuerst"; bool sA = Audit.Save(d1, day, row, u, x1);
            File.SetLastWriteTimeUtc(file, w0);
            x2.Content = "B direkt danach";
            bool okNoLost = sA && !Audit.Save(d2, day, row, u, x2) && x2.Version == v2 && d2.Get(day, row.Id).Content == "A zuerst"
                && d2.GetAudit(day, row.Id).Count == auN + 1 && d2.Refresh() && !d2.Refresh() && !d1.Refresh();

            FileLockService l1 = new FileLockService(dir), l2 = new FileLockService(dir);
            l1.Acquire(day, row.Id, "Paula Werk");
            bool okLock = l2.Holder(day, row.Id, "Max Muster") == "Paula Werk" && l1.Holder(day, row.Id, "Paula Werk") == null;
            l1.Release(day, row.Id, "Paula Werk");
            okLock &= l2.Holder(day, row.Id, "Max Muster") == null;

            // Nutzer: zwei Admins parallel (beide ohne Refresh) -> nach Id zusammengefuehrt, nichts geht verloren; ohne Aenderung kein Schreiben
            FileStore p1 = new FileStore(dir, false, null), p2 = new FileStore(dir, false, null);
            UserChanges c1 = new UserChanges(), c2 = new UserChanges(), c0 = new UserChanges();
            User n1 = p1.Users.Add("Neu", "Eins", "neins", "Leser"); c1.Changed.Add(n1); c1.Added.Add(n1);
            User n2 = p2.Users.Add("Neu", "Zwei", "nzwei", "Leser"); c2.Changed.Add(n2); c2.Added.Add(n2);
            User ep = p2.Users.FindByLogin("eprobe"); ep.Kuerzel = "PRXX"; c2.Changed.Add(ep);
            p1.SaveUsers(c1); p2.SaveUsers(c2);
            UserDb ud = new FileStore(dir, false, null).Users;
            bool okMerge = ud.FindByLogin("neins") != null && ud.FindByLogin("nzwei") != null && ud.FindByLogin("neins").Id != ud.FindByLogin("nzwei").Id
                && ud.FindByLogin("eprobe").Kuerzel == "PRXX" && ud.Users.Count == 3;
            byte[] bU = File.ReadAllBytes(file); p2.SaveUsers(c0);   // Verschluesselung mit Zufalls-IV: jedes Schreiben aendert die Bytes
            byte[] bU2 = File.ReadAllBytes(file);
            okMerge &= bU2.Length == bU.Length && IndexOf(bU2, bU) == 0;
            UserChanges cd = new UserChanges(); cd.Deleted.Add(ud.FindByLogin("neins").Id); p1.SaveUsers(cd);
            okMerge &= new FileStore(dir, false, null).Users.FindByLogin("neins") == null && new FileStore(dir, false, null).Users.FindByLogin("nzwei") != null;

            // Admins= nur bei Ersteinrichtung (Liste ohne Admin), sonst zaehlen nur gespeicherte Rechte
            AppConfig ac = new AppConfig(); ac.Admins.Add("kopie");
            UserDb noAdm = UserDb.Default(), withAdm = UserDb.Default(); withAdm.Add("", "", "chef", "Admin");
            bool okIni = ac.IsAdmin("kopie", noAdm) && !ac.IsAdmin("kopie", withAdm) && !ac.IsAdmin("andere", noAdm);

            // Schluesseldatei: Erststart erzeugt sie (versteckt), Neustart liest dieselbe
            string keyText = File.ReadAllText(keyFile).Trim();
            bool okKey = File.Exists(keyFile) && (File.GetAttributes(keyFile) & FileAttributes.Hidden) != 0 && Convert.FromBase64String(keyText).Length == 32
                && File.ReadAllText(keyFile).Trim() == keyText && !b.Offline && b.Get(day, row.Id) != null;
            // apdb ohne key -> OFFLINE, apdb unveraendert, kein neuer Schluessel (auch nicht nach Refresh/Speichern)
            string dirK = Path.Combine(root, "ohnekey"), fK = Path.Combine(dirK, FileStore.FileName); Directory.CreateDirectory(dirK);
            File.Copy(file, fK);
            byte[] kBytes = File.ReadAllBytes(fK);
            FileStore ks = new FileStore(dirK, false, null);
            bool ksThrows = false;
            try { ks.Save(day, row.Id, new Entry()); } catch (IOException) { ksThrows = true; }
            byte[] kNow = File.ReadAllBytes(fK);
            bool okNoKey = ks.Offline && ks.ReadOnly && ks.Problem.Contains("Schlüsseldatei fehlt") && ksThrows && !ks.Refresh() && ks.Offline
                && !File.Exists(Path.Combine(dirK, FileStore.KeyFileName)) && kNow.Length == kBytes.Length && IndexOf(kNow, kBytes) == 0 && !File.Exists(fK + ".bak1");
            // falscher Schluessel -> OFFLINE, apdb unveraendert; richtiger Schluessel zurueck -> Refresh liest wieder
            string dirR = Path.Combine(root, "falsch"), fR = Path.Combine(dirR, FileStore.FileName), kR = Path.Combine(dirR, FileStore.KeyFileName); Directory.CreateDirectory(dirR);
            File.Copy(file, fR);
            byte[] other = new byte[32]; other[0] = 1; other[31] = 9;
            File.WriteAllText(kR, Convert.ToBase64String(other));
            byte[] rel = File.ReadAllBytes(fR);
            FileStore rs = new FileStore(dirR, false, null);
            bool rsThrows = false;
            try { rs.Save(day, row.Id, new Entry()); } catch (IOException) { rsThrows = true; }
            byte[] relNow = File.ReadAllBytes(fR);
            bool okRel = rs.Offline && rs.ReadOnly && rs.Get(day, row.Id) == null && rsThrows && !rs.Refresh() && rs.Offline
                && relNow.Length == rel.Length && IndexOf(relNow, rel) == 0 && !File.Exists(fR + ".bak1");
            File.WriteAllText(kR, "kein Base64 !!");
            okRel &= new FileStore(dirR, false, null).Offline;   // unlesbare Schluesseldatei
            okRel &= File.ReadAllText(kR) == "kein Base64 !!";

            // Offline-Cache: nicht mit dem Datenschluessel lesbar, eigener DPAPI-Schluessel; abgelaufen -> ignoriert und geloescht
            bool cacheOwn = File.Exists(cA) && File.Exists(Path.Combine(root, "cache.key"));
            bool cacheBad = false;
            try { Protected.UnprotectBytes(File.ReadAllBytes(cA), keyText, FileStore.Rounds); } catch (System.Security.Cryptography.CryptographicException) { cacheBad = true; }
            cacheBad &= Protected.UnprotectBytes(File.ReadAllBytes(file), keyText, FileStore.Rounds).Length > 0;   // Gegenprobe: die apdb ist mit dem Datenschluessel lesbar
            FileStore cf = new FileStore(dirM, false, cA);
            bool okCache = cacheOwn && cacheBad && cf.Offline && cf.Get(day, row.Id) != null;
            File.SetLastWriteTime(cA, DateTime.Now.AddDays(-8));
            FileStore cx = new FileStore(dirM, false, cA);
            okCache &= cx.Offline && cx.Get(day, row.Id) == null && !File.Exists(cA);
            FileStore cz = new FileStore(dir, false, cA, 0);   // CacheTage=0: kein Cache
            okCache &= !cz.Offline && !File.Exists(cA);

            sb.AppendLine("Nutzer/Rechte: parallel zusammengeführt=" + Ok(okMerge) + " Admins= nur Ersteinrichtung=" + Ok(okIni));
            sb.AppendLine("Schlüssel: key-Datei erzeugt/gelesen=" + Ok(okKey) + " apdb ohne key->Offline=" + Ok(okNoKey) + " falsche/kaputte key->Offline=" + Ok(okRel) + " Cache eigener Schlüssel/abgelaufen=" + Ok(okCache));
            sb.AppendLine("Datendatei: Erststart=" + Ok(okInit) + " Neustart=" + Ok(okRound) + " kein Klartext=" + Ok(okCipher) + " Refresh=" + Ok(okSync) + " Konflikt=" + Ok(okConflict)
                + " Historie=" + Ok(okAudit) + " bak=" + Ok(okBak) + " (" + new FileInfo(file).Length + " Byte, Speichern+Protokoll " + ms.ToString("0") + " ms)");
            sb.AppendLine("Datendatei: NurLesen=" + Ok(okRO) + " manipuliert->Cache=" + Ok(okTamper) + " fremd->leer=" + Ok(okForeign) + " unerreichbar=" + Ok(okOffline)
                + " wieder erreichbar=" + Ok(okBack) + " Sperrdatei=" + Ok(okLock) + " kein stilles Überschreiben=" + Ok(okNoLost));
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }

        Form TestDataForm(string which)
        {
            if (which == "backup")
            {
                string dir = Path.Combine(Path.GetTempPath(), "apbak_form");
                if (Backup.Files(dir).Length == 0) { Backup.WriteTo(store, dir, null); Backup.WriteTo(store, dir, "vor-Wiederherstellung"); }
                return new BackupForm(store, Path.Combine(dir, "sicherung.ini"), dir);
            }
            RowInfo ri = rowInfo[0];
            string[] logins = { "pwerk", "ebeispiel", "mmuster" };
            string[] texts = { "Lauf 100001\nTFF: H1-X-0101", "Lauf 100001\nTFF: H1-X-0102 (Ausweich: H1-X-0103)", "Lauf 100001 – Öl-Wechsel prüfen\nTFF: H1-X-0102" };
            for (int i = 0; i < 3; i++)
            {
                Entry e = store.Get(start, ri.Id) ?? new Entry();
                e.Content = texts[i]; e.People[1] = i == 2 ? "MUMA" : "WEPA"; e.Lkz = i > 0;
                Audit.Save(store, start, ri, users.FindByLogin(logins[i]), e);
            }
            return new HistoryForm(ri.Name + ", " + Ui.DayText(start), store.GetAudit(start, ri.Id), ri.Labels, true);
        }

        // Protokoll schreiben/lesen, Version wiederherstellen, Sicherung -> Wiederherstellen, manipulierte Sicherung.
        void SelfTestData(System.Text.StringBuilder sb)
        {
            Rebuild(true);
            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 0 && x.Col == 6) c = x;
            RowInfo ri = rowInfo[0]; DateTime day = c.Day;
            int before = store.GetAudit(day, ri.Id).Count;
            BeginEdit(c); edContent.Text = "Audit A"; EndEdit(true);
            BeginEdit(c); edContent.Text = "Audit B"; edPpl[2].Text = "ABCD"; EndEdit(true);
            List<AuditEntry> au = store.GetAudit(day, ri.Id);
            AuditEntry last = au[au.Count - 1];
            bool okA = au.Count == before + 2 && last.Before.Content == "Audit A" && last.After.Content == "Audit B" && last.After.People[2] == "ABCD"
                && last.Login == me.Login && last.Kuerzel == me.Kuerzel && last.Version == store.Get(day, ri.Id).Version && last.RowName == ri.Name;
            store.GetAudit(day, ri.Id)[0].After.Content = "X";   // Kopie: Aenderung darf das Protokoll nicht beruehren
            bool okRo = store.GetAudit(day, ri.Id)[0].After.Content != "X";
            RestoreVersion(c, au[au.Count - 2].After);
            string p2 = au[au.Count - 2].After.People[2];
            bool inEdit = editCell == c && edContent.Text == "Audit A" && edPpl[2].Text == p2;
            EndEdit(true);
            Entry now = store.Get(day, ri.Id);
            bool okR = inEdit && now.Content == "Audit A" && now.People[2] == p2 && p2 != "ABCD" &&store.GetAudit(day, ri.Id).Count == before + 3;
            sb.AppendLine("Protokoll: schreiben/lesen=" + Ok(okA) + " unveränderlich=" + Ok(okRo) + " Version wiederherstellen=" + Ok(okR) + " (Einträge " + store.GetAudit(day, ri.Id).Count + ")");

            // Sicherung: alte Woche (1 Jahr zurueck) bleibt erhalten, Roundtrip, Protokoll wird zusammengefuehrt
            string dir = Path.Combine(Path.GetTempPath(), "apbak_selftest");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            DateTime old = day.AddDays(-364);
            Entry oe = new Entry(); oe.Content = "Altlauf vor einem Jahr"; Audit.Save(store, old, ri, me, oe);
            Entry fmtE = store.Get(start.AddDays(4), ri.Id);
            StoreData s0 = store.Snapshot();
            string path = Backup.WriteTo(store, dir, null);
            Entry ch = store.Get(day, ri.Id); ch.Content = "Nach Sicherung"; Audit.Save(store, day, ri, me, ch);
            List<RowInfo> rows = store.GetRows(); rows.Add(new RowInfo()); store.SaveStructure(rows);
            int auditBefore = store.Snapshot().Audit.Count;
            store.Restore(Backup.Read(path, Backup.KeyOf(store)));
            Rebuild(true);
            StoreData s1 = store.Snapshot();
            Entry fmtR = store.Get(start.AddDays(4), ri.Id);
            bool okB = store.Get(day, ri.Id).Content == "Audit A" && s1.Entries.Count == s0.Entries.Count && s1.Rows.Count == s0.Rows.Count
                && s1.Catalog.Positions.Count == s0.Catalog.Positions.Count && s1.Catalog.Bausteine.Count == s0.Catalog.Bausteine.Count
                && fmtE != null && fmtR.SpanKey() == fmtE.SpanKey() && fmtR.Content == fmtE.Content;
            bool okVer = fmtR.Version == fmtE.Version + 1 && store.Get(day, ri.Id).Version == ch.Version + 1;   // max(alt, Sicherung) + 1
            bool okLog = s1.Audit.Count == auditBefore;
            List<SearchHit> hits = store.Find("Altlauf");
            bool okOld = hits.Count == 1 && hits[0].Day == old && store.GetAudit(old, ri.Id).Count == 1;
            sb.AppendLine("Sicherung: Roundtrip=" + Ok(okB) + " Version weiterzählen=" + Ok(okVer) + " Protokoll erhalten=" + Ok(okLog) + " Jahr zurück=" + Ok(okOld) + " (" + new FileInfo(path).Length + " Byte)");

            // Manipulation: (a) Byte in der Datei, (b) Text im XML bei unveraendertem Hash
            byte[] raw = File.ReadAllBytes(path);
            string bad1 = Path.Combine(dir, "kaputt1.bin"), bad2 = Path.Combine(dir, "kaputt2.bin");
            raw[raw.Length / 2] ^= 0x55; File.WriteAllBytes(bad1, raw);
            string text;
            using (FileStream fs = File.OpenRead(path))
            using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            using (StreamReader rd = new StreamReader(gz, new System.Text.UTF8Encoding(false))) text = rd.ReadToEnd();
            byte[] mod = new System.Text.UTF8Encoding(false).GetBytes(text.Replace("Audit A", "Audit X"));
            using (FileStream fs = File.Create(bad2))
            using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Compress)) gz.Write(mod, 0, mod.Length);
            bool rej1 = false, rej2 = false;
            try { Backup.Read(bad1, Backup.KeyOf(store)); } catch (InvalidDataException) { rej1 = true; }
            try { Backup.Read(bad2, Backup.KeyOf(store)); } catch (InvalidDataException) { rej2 = true; }

            // APBAK2 (verschluesselt): Round-Trip, falscher Schluessel, Altformat APBAK1, Entpackgrenze
            StoreData snap = store.Snapshot();
            string k1 = Convert.ToBase64String(new byte[32]), k2 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 });
            string pk2 = Path.Combine(dir, "v2.apbak"), pk1 = Path.Combine(dir, "v1.apbak");
            Backup.Write(snap, pk2, k1); Backup.Write(snap, pk1, null);
            byte[] r2 = File.ReadAllBytes(pk2);
            bool v2plain = r2.Length > 1 && !(r2[0] == 0x1F && r2[1] == 0x8B) && IndexOf(r2, System.Text.Encoding.ASCII.GetBytes("APBAK")) < 0;   // kein GZip-Kopf, Klartext nicht sichtbar
            StoreData g2 = Backup.Read(pk2, k1);
            bool okV2 = v2plain && g2.Entries.Count == snap.Entries.Count && g2.Rows.Count == snap.Rows.Count && g2.Audit.Count == snap.Audit.Count && g2.Catalog.Bausteine.Count == snap.Catalog.Bausteine.Count;
            bool wrongKey = false, noKey = false, flip = false;
            try { Backup.Read(pk2, k2); } catch (InvalidDataException) { wrongKey = true; }
            try { Backup.Read(pk2, null); } catch (InvalidDataException) { noKey = true; }
            r2[r2.Length / 2] ^= 0x55; File.WriteAllBytes(bad1, r2);
            try { Backup.Read(bad1, k1); } catch (InvalidDataException) { flip = true; }
            StoreData g1 = Backup.Read(pk1, null);
            bool okV1 = g1.Entries.Count == snap.Entries.Count && Backup.Read(pk1, k1).Rows.Count == snap.Rows.Count;   // APBAK1 mit und ohne Schluessel lesbar
            bool bomb = false;
            using (MemoryStream zs = new MemoryStream())
            {
                using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(zs, System.IO.Compression.CompressionMode.Compress, true))
                {
                    byte[] zero = new byte[1024 * 1024];
                    for (int i = 0; i < 201; i++) gz.Write(zero, 0, zero.Length);
                }
                zs.Position = 0;
                try { Backup.Unzip(zs); } catch (InvalidDataException) { bomb = true; }
            }
            sb.AppendLine("Sicherung APBAK2: Roundtrip verschlüsselt=" + Ok(okV2) + " falscher Schlüssel abgelehnt=" + Ok(wrongKey && noKey) + " Byte verändert abgelehnt=" + Ok(flip)
                + " APBAK1 lesbar=" + Ok(okV1) + " 200-MB-Grenze=" + Ok(bomb));
            for (int i = 0; i < Backup.Keep + 2; i++) Backup.WriteTo(store, dir, "t" + i.ToString("00"));
            sb.AppendLine("Sicherung manipuliert abgelehnt: Byte=" + Ok(rej1) + " Inhalt=" + Ok(rej2) + " | behalten=" + Backup.Files(dir).Length + " (soll " + Backup.Keep + ")");
        }
    }
}
#endif
