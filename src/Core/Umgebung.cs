using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;

namespace Arbeitsplan.Core
{
    // Start nur in der Firmenumgebung: Datenordner erreichbar, Programm eingerichtet, PC/Nutzer in einer erlaubten Domaene,
    // Nutzer angelegt. Eine kopierte exe (z. B. per E-Mail nach Hause) findet weder Datenordner noch Daten und beendet sich;
    // kopierte Daten oeffnen sich ausserhalb der erlaubten Domaenen nicht (Liste steht verschluesselt in der Datendatei).
    public static class Umgebung
    {
        public static string UserDomain { get { try { return Environment.UserDomainName ?? ""; } catch (Exception) { return ""; } } }
        public static string Machine { get { try { return Environment.MachineName ?? ""; } catch (Exception) { return ""; } } }
        public static string MachineDomain { get { try { return IPGlobalProperties.GetIPGlobalProperties().DomainName ?? ""; } catch (Exception) { return ""; } } }

        // Kennung dieser Umgebung fuer die Liste: Windows-Domaene des Nutzers, bei lokalem Konto die DNS-Domaene des Rechners,
        // sonst der Rechnername (dann ist die Datendatei an diesen einen PC gebunden).
        public static string Home(string userDomain, string machineDomain, string machine)
        {
            if (userDomain.Length > 0 && !string.Equals(userDomain, machine, StringComparison.OrdinalIgnoreCase)) return userDomain;
            return machineDomain.Length > 0 ? machineDomain : machine;
        }

        // Leere Liste = keine Pruefung (Erststart). Passt Nutzer-Domaene, Rechner-Domaene oder Rechnername auf einen Eintrag.
        public static bool Allowed(IList<string> allowed, string userDomain, string machineDomain, string machine)
        {
            if (allowed == null || allowed.Count == 0) return true;
            foreach (string a in allowed)
                if (Like(userDomain, a) || Like(machineDomain, a) || Like(machine, a)) return true;
            return false;
        }

        // Platzhalter * und ?, ohne Gross-/Kleinschreibung; leerer Wert oder leeres Muster passt nie.
        public static bool Like(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return false;
            return Like(value.ToUpperInvariant(), 0, pattern.Trim().ToUpperInvariant(), 0);
        }

        static bool Like(string v, int i, string p, int j)
        {
            while (j < p.Length)
            {
                if (p[j] == '*')
                {
                    for (int k = i; k <= v.Length; k++) if (Like(v, k, p, j + 1)) return true;
                    return false;
                }
                if (i >= v.Length || (p[j] != '?' && p[j] != v[i])) return false;
                i++; j++;
            }
            return i == v.Length;
        }

        public static List<string> SplitList(string s)
        {
            List<string> l = new List<string>();
            foreach (string a in (s ?? "").Split(',', ';')) if (a.Trim().Length > 0) l.Add(a.Trim());
            return l;
        }

        // Datenordner da, aber noch keine Daten (weder Datei noch Schluessel noch Altstaende): Ersteinrichtung noetig.
        public static bool NeedsSetup(string folder)
        {
            try
            {
                return Directory.Exists(folder) && !File.Exists(Path.Combine(folder, FileStore.FileName))
                    && !File.Exists(Path.Combine(folder, FileStore.KeyFileName)) && !File.Exists(Path.Combine(folder, FileStore.FileName + ".bak1"));
            }
            catch (Exception) { return false; }
        }

        // Ersteinrichtung (nur nach Rueckfrage, nur fuer Admins= aus der ini): leerer Datenordner -> Datendatei + Schluesseldatei.
        // Der Ordner wird danach versteckt (Mitarbeiter sollen dort nichts kopieren/verschieben). null = ok, sonst Meldung.
        public static string Setup(AppConfig cfg)
        {
            FileStore fs = new FileStore(cfg.DataFolder, false, cfg.CachePath, cfg.CacheDays);
            if (fs.Offline) return fs.Problem;
            try
            {
                Directory.CreateDirectory(cfg.PackageFolder); Directory.CreateDirectory(cfg.BackupFolder);
                File.SetAttributes(cfg.DataFolder, File.GetAttributes(cfg.DataFolder) | FileAttributes.Hidden);
            }
            catch (Exception) { }   // nur Komfort
            return null;
        }

        public static bool IsSetupAdmin(AppConfig cfg, string login)
        {
            foreach (string a in cfg.Admins) if (string.Equals(a, login, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Oeffnet die Daten und prueft, ob dieser Nutzer auf diesem PC starten darf. Rueckgabe: null = ok (store gesetzt),
        // sonst die Meldung fuer den Nutzer (Programm beenden). Die Ersteinrichtung fragt der Aufrufer vorher (NeedsSetup).
        public static string Open(AppConfig cfg, string login, out FileStore store)
        {
            store = null;
            string ud = UserDomain, md = MachineDomain, pc = Machine;
            if (cfg.ReadOnlyFor(login, pc)) cfg.ReadOnly = true;   // Systemrechner/Anzeige: nur ansehen
            if (NeedsSetup(cfg.DataFolder)) return NotSetUp(cfg);
            FileStore fs = new FileStore(cfg.DataFolder, cfg.ReadOnly, cfg.CachePath, cfg.CacheDays);
            if (fs.Offline && !fs.FromCache)
                return (Directory.Exists(cfg.DataFolder) ? fs.Problem : NoData(cfg)) + "\n\nDas Programm wird beendet.";
            if (!Allowed(fs.Users.Domains, ud, md, pc))
                return "Der Arbeitsplan ist auf diesem PC nicht freigegeben.\n\nEr funktioniert nur im Firmennetz (erlaubt: " + string.Join(", ", fs.Users.Domains.ToArray())
                    + ").\nDieser PC: Domäne " + Show(ud) + ", Rechner " + Show(pc) + (md.Length > 0 ? " (" + md + ")" : "")
                    + "\n\nFalls das ein Firmen-PC ist: Admin des Arbeitsplans bitten, die Domäne in der Nutzerverwaltung freizugeben.";
            if (fs.Users.FindByLogin(login) == null && !cfg.IsAdmin(login, fs.Users) && !cfg.Pilot && !cfg.ReadOnly)
                return "Sie sind im Arbeitsplan noch nicht als Nutzer angelegt.\n\nIhre Windows-Anmeldung: " + (ud.Length > 0 ? ud + "\\" : "") + login
                    + "\n\nBitte wenden Sie sich an Ihren Vorgesetzten oder einen Admin des Arbeitsplans. Er legt Sie unter"
                    + " Einstellungen → Nutzerverwaltung mit diesem Windows-Anmeldenamen an. Danach das Programm neu starten.\n\n(Strg+C kopiert diese Meldung.)";
            store = fs;
            return null;
        }

        static string Show(string s) { return s.Length > 0 ? s : "?"; }

        static string NoData(AppConfig cfg)
        {
            return "Die Arbeitsplan-Daten sind nicht erreichbar:\n" + cfg.DataFolder
                + "\n\nDas Programm funktioniert nur im Firmennetz mit Zugriff auf das Laufwerk. Bitte die Verknüpfung bzw. das Programm"
                + " auf dem Laufwerk verwenden (nicht kopieren) und prüfen, ob das Laufwerk erreichbar ist.";
        }

        static string NotSetUp(AppConfig cfg)
        {
            return "Der Arbeitsplan ist in diesem Ordner noch nicht eingerichtet:\n" + cfg.DataFolder
                + "\n\nDie Einrichtung macht ein Admin einmalig: eigenen Windows-Anmeldenamen in Arbeitsplan.ini unter \"Admins=\" eintragen"
                + " und das Programm starten.\n\nDas Programm wird beendet.";
        }
    }
}
