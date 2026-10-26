using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Datenquelle (FileStore im Datenordner): Nutzer/Rechte, Titel, Abfrage auf Aenderungen anderer PCs.
    partial class MainForm
    {
        bool offlineStart;

        // Meldung nach dem Start (Datenordner nicht erreichbar, Ausweichordner …), sonst null
        public string StartMessage
        {
            get
            {
                string m = cfg != null ? cfg.Note : null;
                if (fstore != null && fstore.Problem != null) m = (m != null ? m + "\n\n" : "") + fstore.Problem;
                return m;
            }
        }

        User ResolveUser(string login)
        {
            User u = users.FindByLogin(login);
            if (cfg == null)
            {
                if (u != null) return u;
                // Annahme (Prototyp): unbekannter Windows-Nutzer = Produktion mit Windows-Namen.
                u = new User(); u.Login = login;
                UserDb.ApplyPackage(u.Rights, users.Package("Produktion"));
                return u;
            }
            bool admin = cfg.IsAdmin(login);
            if (u == null && admin && !fstore.ReadOnly)
            {
                u = users.Add("", "", login, "Admin");   // Admin aus Arbeitsplan.ini einmalig in die Nutzerliste (Kuerzel dort eintragen)
                try { fstore.SaveUsers(users); } catch (Exception) { }
            }
            if (u == null) { u = new User(); u.Login = login; }
            string pkg = admin ? "Admin" : users.FindByLogin(login) != null ? null : (cfg.Pilot ? "Produktion" : "Leser");
            if (fstore.ReadOnly) { pkg = "Leser"; offlineStart = fstore.Offline; }   // Systemrechner / OFFLINE: nur ansehen
            if (pkg != null) UserDb.ApplyPackage(u.Rights, users.Package(pkg));
            return u;
        }

        void UpdateTitle()
        {
            string src;
            if (fstore == null)
#if TESTHOOKS
                src = "Prototyp, lokale Testdaten" + (systemPc ? ", System-PC, kein Speichern" : "");
#else
                src = "Testdaten, keine Speicherung";
#endif
            else
            {
                string f = fstore.Folder;
                if (f.Length > 45) f = f.Substring(0, 20) + "…" + f.Substring(f.Length - 22);
                src = "Daten: " + f + (fstore.Offline ? " - OFFLINE" : "") + (cfg.ReadOnly ? " - NUR LESEN" : "")
                    + (offlineStart && !fstore.Offline ? " - wieder verbunden, zum Bearbeiten neu starten" : "");
            }
            Text = "Arbeitsplan (" + src + ") - Oberfläche: " + Theme.Current.Name
                + " - Nutzer: " + me.Display + " (" + (users.PackageOf(me) != null ? users.PackageOf(me).Name : "benutzerdefiniert") + ")";
        }

        // Vor der Nutzerverwaltung: aktuelle Nutzerliste der Datendatei; der eigene Eintrag bleibt dasselbe Objekt (kein Selbstloeschen)
        void RefreshUsers()
        {
            if (fstore == null) return;
            if (fstore.Refresh() && editCell == null) Rebuild(false);
            users = fstore.Users;
            int i = users.Users.FindIndex(delegate(User x) { return string.Equals(x.Login, me.Login, StringComparison.OrdinalIgnoreCase); });
            if (i >= 0) users.Users[i] = me;
        }

        void SaveUsers()
        {
            if (fstore == null) return;
            try { fstore.SaveUsers(users); }
            catch (Exception ex) { MessageBox.Show("Die Nutzerdaten konnten nicht gespeichert werden.\n" + ex.Message, "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Aenderungen anderer PCs uebernehmen; nie waehrend eines Bearbeitungsmodus oder offenen Dialogs.
        void StartPolling()
        {
            if (fstore == null) return;
            Timer t = new Timer(); t.Interval = cfg.PollSeconds * 1000;
            t.Tick += delegate
            {
                if (editCell != null || Application.OpenForms.Count > 1) return;
                bool wasOffline = fstore.Offline;
                if (!fstore.Refresh()) return;
                Rebuild(false);
                if (wasOffline) UpdateTitle();
            };
            t.Start();
            FormClosed += delegate { t.Stop(); };
        }

#if TESTHOOKS
        // Datendatei: Erststart, Neustart = Daten da, kein Klartext, zwei PCs (Refresh, Konflikt), Historie, .bak, NurLesen,
        // Manipulation/fremde Datei/unerreichbar -> OFFLINE statt Absturz, wieder erreichbar, Sperrdateien.
        void SelfTestFiles(System.Text.StringBuilder sb)
        {
            string root = Path.Combine(Path.GetTempPath(), "apdb_selftest");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string dir = Path.Combine(root, "daten"), file = Path.Combine(dir, FileStore.FileName), cA = Path.Combine(root, "cacheA.apdb"), none = Path.Combine(root, "keincache.apdb");
            DateTime day = new DateTime(2026, 3, 2);
            User u = new User(); u.Login = "test"; u.Kuerzel = "TEST";

            FileStore a = new FileStore(dir, false, cA);
            bool okInit = !a.Offline && a.Problem == null && a.GetRows().Count == 12 && a.Users.Users.Count == 0 && a.Users.Packages.Count == 3 && File.Exists(file);
            RowInfo row = a.GetRows()[0];
            Entry e = new Entry(); e.Content = "Geheimlauf Öl-4711"; e.People[0] = "ZQXW";
            DateTime t0 = DateTime.Now;
            bool s1 = Audit.Save(a, day, row, u, e);
            double ms = (DateTime.Now - t0).TotalMilliseconds;
            a.Users.Add("Erika", "Probe", "eprobe", "Produktion").Kuerzel = "PRER"; a.SaveUsers(a.Users);

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

            File.Copy(file, fF, true);   // Ordner wieder erreichbar/gueltig
            bool okBack = m2.Refresh() && !m2.Offline && !m2.ReadOnly && m2.Get(day, row.Id).Content == "Von A geändert";

            FileLockService l1 = new FileLockService(dir), l2 = new FileLockService(dir);
            l1.Acquire(day, row.Id, "Paula Werk");
            bool okLock = l2.Holder(day, row.Id, "Max Muster") == "Paula Werk" && l1.Holder(day, row.Id, "Paula Werk") == null;
            l1.Release(day, row.Id, "Paula Werk");
            okLock &= l2.Holder(day, row.Id, "Max Muster") == null;

            sb.AppendLine("Datendatei: Erststart=" + Ok(okInit) + " Neustart=" + Ok(okRound) + " kein Klartext=" + Ok(okCipher) + " Refresh=" + Ok(okSync) + " Konflikt=" + Ok(okConflict)
                + " Historie=" + Ok(okAudit) + " bak=" + Ok(okBak) + " (" + new FileInfo(file).Length + " Byte, Speichern+Protokoll " + ms.ToString("0") + " ms)");
            sb.AppendLine("Datendatei: NurLesen=" + Ok(okRO) + " manipuliert->Cache=" + Ok(okTamper) + " fremd->leer=" + Ok(okForeign) + " unerreichbar=" + Ok(okOffline)
                + " wieder erreichbar=" + Ok(okBack) + " Sperrdatei=" + Ok(okLock));
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
#endif
    }
}
