using System;
using System.Collections.Generic;
using System.IO;

namespace Arbeitsplan.Core
{
    // Einstellungen aus "Arbeitsplan.ini" neben der exe (Schluessel=Wert, # Kommentar). Fehlt die Datei: Standardwerte.
    public class AppConfig
    {
        public string DataFolder = "", BackupFolder = "", PackageFolder = "", CachePath = "";
        public string Note;                         // Hinweis fuer den Nutzer (z. B. ungueltiger Datenordner), sonst null
        public bool ReadOnly, Pilot;
        public List<string> Admins = new List<string>();
        public List<string> ReadOnlyPatterns = new List<string>();   // NurLesenFuer=: Windows-Logins oder Rechnernamen (Platzhalter *), die nur ansehen
        public int PollSeconds = 30;
        public int CacheDays = 7;                   // Offline-Cache auf diesem PC; 0 = kein Cache
        public int BackupHours = 24;                // SicherungAlleStunden=: automatische Sicherung im laufenden Programm und beim Beenden

        // Admins= wirkt nur bei Ersteinrichtung: solange die Nutzerliste der Datendatei noch keinen Admin hat.
        // Danach zaehlen nur die gespeicherten Rechte (eine Kopie mit eigener ini verleiht keine Rechte).
        public bool IsAdmin(string login, UserDb db)
        {
            if (db.AdminCount(null) > 0) return false;
            foreach (string a in Admins) if (string.Equals(a, login, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Systemrechner/Anzeige-PC: Login oder Rechnername passt auf NurLesenFuer= (auch ohne Nutzerkonto, nur Lesen)
        public bool ReadOnlyFor(string login, string machine)
        {
            foreach (string p in ReadOnlyPatterns) if (Umgebung.Like(login, p) || Umgebung.Like(machine, p)) return true;
            return false;
        }

        static string LocalDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arbeitsplan"); } }

        public static AppConfig Load(string exeDir)
        {
            AppConfig c = new AppConfig();
            Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string ini = Path.Combine(exeDir, "Arbeitsplan.ini");
                if (File.Exists(ini))
                    foreach (string raw in File.ReadAllLines(ini))
                    {
                        string l = raw.Trim(); int eq = l.IndexOf('=');
                        if (l.StartsWith("#") || l.StartsWith(";") || eq <= 0) continue;
                        v[l.Substring(0, eq).Trim()] = l.Substring(eq + 1).Trim();
                    }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            string s;
            c.ReadOnly = v.TryGetValue("NurLesen", out s) && s == "1";
            c.Pilot = v.TryGetValue("Pilot", out s) && s == "1";
            int n;
            if (v.TryGetValue("Abfrage", out s) && int.TryParse(s, out n)) c.PollSeconds = Math.Max(5, Math.Min(3600, n));
            if (v.TryGetValue("CacheTage", out s) && int.TryParse(s, out n)) c.CacheDays = Math.Max(0, Math.Min(365, n));
            if (v.TryGetValue("SicherungAlleStunden", out s) && int.TryParse(s, out n)) c.BackupHours = Backup.ClampHours(n);
            if (v.TryGetValue("Admins", out s)) c.Admins = Umgebung.SplitList(s);
            if (v.TryGetValue("NurLesenFuer", out s)) c.ReadOnlyPatterns = Umgebung.SplitList(s);
            c.CachePath = Path.Combine(LocalDir, "cache.apdb");

            string data = v.TryGetValue("Datenordner", out s) ? s : "";
            // Ohne Angabe: liegt die exe in "Programm" neben einem Ordner "daten" (Laufwerk-Aufbau), wird dieser verwendet,
            // sonst "daten" neben der exe. Angelegt wird hier nie etwas: eine allein kopierte exe findet keine Daten (Umgebung.Open).
            if (data.Length == 0 && Directory.Exists(Path.Combine(exeDir, Path.Combine("..", "daten")))) data = Path.Combine("..", "daten");
            try { c.DataFolder = Path.GetFullPath(Path.Combine(exeDir, data.Length > 0 ? data : "daten")); }
            catch (Exception) { c.DataFolder = Path.Combine(exeDir, "daten"); c.Note = "Datenordner in Arbeitsplan.ini ist ungültig: " + data; }
            c.ownBackup = v.TryGetValue("Sicherungsordner", out s) && s.Length > 0;
            c.BackupFolder = c.ownBackup ? Path.Combine(exeDir, s) : Path.Combine(c.DataFolder, "sicherung");
            c.ownPackages = v.TryGetValue("Paketordner", out s) && s.Length > 0;
            c.PackageFolder = c.ownPackages ? Path.Combine(exeDir, s) : Path.Combine(c.DataFolder, "Pakete");
            return c;
        }

        bool ownBackup, ownPackages;   // in der ini angegeben (sonst Unterordner des Datenordners)

        // Anderer Datenordner (gemerkt bzw. im Dialog gewaehlt); Sicherung/Pakete ziehen mit, wenn die ini sie nicht festlegt
        public void UseDataFolder(string folder)
        {
            DataFolder = folder; Note = null;
            if (!ownBackup) BackupFolder = Path.Combine(folder, "sicherung");
            if (!ownPackages) PackageFolder = Path.Combine(folder, "Pakete");
        }
    }
}
