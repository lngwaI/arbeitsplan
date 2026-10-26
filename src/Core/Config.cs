using System;
using System.Collections.Generic;
using System.IO;

namespace Arbeitsplan.Core
{
    // Einstellungen aus "Arbeitsplan.ini" neben der exe (Schluessel=Wert, # Kommentar). Fehlt die Datei: Standardwerte.
    public class AppConfig
    {
        public string DataFolder = "", BackupFolder = "", CachePath = "";
        public string Note;                         // Hinweis fuer den Nutzer (z. B. Ausweichordner), sonst null
        public bool ReadOnly, Pilot;
        public List<string> Admins = new List<string>();
        public int PollSeconds = 30;

        public bool IsAdmin(string login)
        {
            foreach (string a in Admins) if (string.Equals(a, login, StringComparison.OrdinalIgnoreCase)) return true;
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
            if (v.TryGetValue("Admins", out s))
                foreach (string a in s.Split(',', ';')) if (a.Trim().Length > 0) c.Admins.Add(a.Trim());
            c.CachePath = Path.Combine(LocalDir, "cache.apdb");

            string data = v.TryGetValue("Datenordner", out s) ? s : "";
            try { c.DataFolder = Path.GetFullPath(Path.Combine(exeDir, data.Length > 0 ? data : "daten")); }
            catch (Exception) { c.DataFolder = Path.Combine(exeDir, "daten"); c.Note = "Datenordner in Arbeitsplan.ini ist ungültig: " + data; }
            // Standardordner neben der exe nicht beschreibbar (z. B. Programme-Ordner): lokaler Ausweichordner
            if (data.Length == 0 && !c.ReadOnly && !Writable(c.DataFolder))
            {
                c.DataFolder = Path.Combine(LocalDir, "daten");
                c.Note = "Der Ordner neben dem Programm ist nicht beschreibbar. Die Daten liegen nur auf diesem PC:\n" + c.DataFolder
                    + "\n\nZum Teilen mit Kollegen in Arbeitsplan.ini \"Datenordner=\" auf einen gemeinsamen Ordner setzen.";
            }
            c.BackupFolder = v.TryGetValue("Sicherungsordner", out s) && s.Length > 0 ? Path.Combine(exeDir, s) : Path.Combine(c.DataFolder, "sicherung");
            return c;
        }

        static bool Writable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, "~schreibtest.tmp");
                File.WriteAllText(probe, "x"); File.Delete(probe);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
