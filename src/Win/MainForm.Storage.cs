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
            bool admin = cfg.IsAdmin(login, users);   // nur solange die Nutzerliste keinen Admin hat (Ersteinrichtung)
            if (admin && !fstore.ReadOnly)
            {   // Admin aus Arbeitsplan.ini einmalig in die Nutzerliste (Kuerzel dort eintragen)
                UserChanges ch = new UserChanges();
                if (u == null) { u = users.Add("", "", login, "Admin"); ch.Added.Add(u); }
                else UserDb.ApplyPackage(u.Rights, users.Package("Admin"));
                ch.Changed.Add(u);
                try { fstore.SaveUsers(ch); users = fstore.Users; } catch (Exception) { }
            }
            if (u == null) { u = new User(); u.Login = login; }
            string pkg = admin ? "Admin" : users.FindByLogin(login) != null ? null : (cfg.Pilot ? "Produktion" : "Leser");
            if (fstore.ReadOnly) { pkg = "Leser"; offlineStart = fstore.Offline; }   // Systemrechner / OFFLINE: nur ansehen
            if (pkg != null) UserDb.ApplyPackage(u.Rights, users.Package(pkg));
            if (!fstore.ReadOnly && users.Domains.Count == 0 && UserDb.IsAdmin(u.Rights))
            {   // erster Admin bindet die Daten an diese Umgebung (Umgebung.Allowed); aenderbar in der Nutzerverwaltung
                UserChanges dc = new UserChanges();
                dc.Domains = new List<string>(new[] { Umgebung.Home(Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine) });
                try { fstore.SaveUsers(dc); users = fstore.Users; } catch (Exception) { }
            }
            return u;
        }

        // Menue Hilfe > Info: was die IT/der Admin bei Problemen wissen muss
        void ShowInfo()
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            string ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).FileVersion;
            string exe = Application.ExecutablePath, state = "Testdaten (keine Speicherung)";
            if (fstore != null) state = fstore.Folder + (fstore.Offline ? "  (OFFLINE, letzter Stand dieses PCs)" : fstore.ReadOnly ? "  (nur lesen)" : "");
            MessageBox.Show(this, "Arbeitsplan " + ver + "  (Stand " + File.GetLastWriteTime(exe).ToString("dd.MM.yyyy HH:mm") + ")"
                + "\n\nProgramm: " + Shortcut.Unc(exe) + (Shortcut.OnNetwork(exe) ? "" : "  (lokale Kopie)")
                + "\nDaten: " + state
                + "\n\nNutzer: " + me.Display + (me.Kuerzel.Length > 0 ? " (" + me.Kuerzel + ")" : "") + ", Rechte: " + PackageName()
                + "\nWindows: " + Umgebung.UserDomain + "\\" + Environment.UserName + " auf " + Umgebung.Machine + (Umgebung.MachineDomain.Length > 0 ? " (" + Umgebung.MachineDomain + ")" : "")
                + "\nFreigegebene Domänen: " + (users.Domains.Count > 0 ? string.Join(", ", users.Domains.ToArray()) : "(noch keine)")
                + (newVersion ? "\n\nAuf dem Laufwerk liegt eine neuere Programmversion: bitte das Programm neu starten." : ""),
                "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (newVersion) src += " - NEUE VERSION: bitte neu starten";
            Text = "Arbeitsplan (" + src + ") - Oberfläche: " + Theme.Current.Name
                + " - Nutzer: " + me.Display + " (" + PackageName() + ")";
        }

        // Name des Rechtepakets des Nutzers; ohne exakt passendes Paket "benutzerdefiniert".
        string PackageName() { return users.PackageOf(me) != null ? users.PackageOf(me).Name : "benutzerdefiniert"; }

        // Vor der Nutzerverwaltung: aktuelle Nutzerliste der Datendatei; der eigene Eintrag bleibt dasselbe Objekt (kein Selbstloeschen)
        void RefreshUsers()
        {
            if (fstore == null) return;
            if (fstore.Refresh() && editCell == null) Rebuild(false);
            users = fstore.Users;
            int i = users.Users.FindIndex(delegate(User x) { return string.Equals(x.Login, me.Login, StringComparison.OrdinalIgnoreCase); });
            if (i >= 0) users.Users[i] = me;
        }

        // Nur bei tatsaechlicher Aenderung; der FileStore fuehrt sie nach Id mit dem aktuellen Dateistand zusammen
        void SaveUsers(UserChanges ch)
        {
            if (fstore == null || !ch.Any) return;
            try { fstore.SaveUsers(ch); RefreshUsers(); }
            catch (Exception ex) { MessageBox.Show("Die Nutzerdaten konnten nicht gespeichert werden.\n" + ex.Message, "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Aenderungen anderer PCs uebernehmen; nie waehrend eines Bearbeitungsmodus oder offenen Dialogs.
        // Die Dateipruefung (Zeit/Groesse; haengt bei Netzausfall) laeuft im Hintergrund. Nur wenn sie eine Aenderung meldet,
        // liest der FileStore im UI-Thread neu (er ist nicht thread-sicher); ein Lauf zur Zeit, nach dem Schliessen nichts mehr.
        bool newVersion;   // exe auf dem Laufwerk wurde ersetzt (Update): Hinweis im Titel
        MethodInvoker pollNow;   // sofort pruefen (bei Navigation, wie im alten Programm), sonst alle Abfrage= Sekunden

        void StartPolling()
        {
            if (fstore == null) return;
            string path = Path.Combine(fstore.Folder, FileStore.FileName), exe = Application.ExecutablePath;
            DateTime exeTime = DateTime.MinValue; long exeLen = -1;
            try { FileInfo xi = new FileInfo(exe); exeTime = xi.LastWriteTimeUtc; exeLen = xi.Length; } catch (Exception) { }
            bool busy = false, closed = false;
            DateTime seenTime = DateTime.MinValue; long seenLen = -1;
            int tries = 0;   // Refresh-Versuche nach einer erkannten Aenderung (zweiter Versuch, falls das Lesen kurz scheitert)
            Timer t = new Timer(); t.Interval = cfg.PollSeconds * 1000;
            EventHandler tick = delegate
            {
                if (busy || editCell != null || Application.OpenForms.Count > 1) return;
                busy = true;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    DateTime ft = DateTime.MinValue; long fl = -1;   // -1 = nicht erreichbar/fehlt
                    try { FileInfo fi = new FileInfo(path); if (fi.Exists) { ft = fi.LastWriteTimeUtc; fl = fi.Length; } }
                    catch (Exception) { }
                    bool upd = false;
                    try { FileInfo xi = new FileInfo(exe); upd = exeLen >= 0 && xi.Exists && (xi.LastWriteTimeUtc != exeTime || xi.Length != exeLen); }
                    catch (Exception) { }
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            busy = false;
                            if (closed || IsDisposed) return;
                            if (upd && !newVersion) { newVersion = true; UpdateTitle(); }
                            if (editCell != null || Application.OpenForms.Count > 1) return;
                            if (fl >= 0 && (ft != seenTime || fl != seenLen)) { seenTime = ft; seenLen = fl; tries = 2; }
                            bool wasOffline = fstore.Offline, fresh = false;
                            if (fl >= 0 && tries > 0)
                            {
                                int g0 = fstore.Generation;
                                fresh = fstore.Refresh();
                                tries = fresh || fstore.Generation != g0 ? 0 : tries - 1;
                            }
                            if (!fresh && fstore.Generation == viewGen) return;   // auch still beim Speichern/Bearbeiten nachgeladene Daten
                            Rebuild(false);
                            if (wasOffline) UpdateTitle();
                        });
                    }
                    catch (Exception) { }   // Fenster bereits geschlossen
                });
            };
            t.Tick += tick;
            pollNow = delegate { tick(t, EventArgs.Empty); };
            t.Start();
            FormClosed += delegate { closed = true; t.Stop(); t.Dispose(); };
        }
    }
}
