#if DEMO
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Arbeitsplan.Core
{
    // Vorfuehr-Version (Build mit Define DEMO, siehe docs/Demo.md): eigener Datenordner unterhalb von "ArbeitsplanDemo" mit erfundenen
    // Testdaten. Die Demo-exe oeffnet nie einen anderen Ordner (auch nicht per --data) und nie die echten Daten.
    public static class DemoEnv
    {
        public const string Segment = "ArbeitsplanDemo";
        public const string MarkerFile = "demo.marker";   // im Datenordner; Inhalt = Montag der Ausgangsdaten (JJJJ-MM-TT)

        public static string DefaultRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Segment); } }
        // Testbilder/Selbsttest: eigener Ordner, damit die Vorfuehrdaten nicht angefasst werden
        public static string TestRoot { get { return Path.Combine(Path.Combine(Path.GetTempPath(), Segment), "Test"); } }

        public static DateTime MondayOf(DateTime d) { d = d.Date; return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

        // Pfad liegt unterhalb eines Ordners namens ArbeitsplanDemo
        public static bool IsDemoPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string full;
            try { full = Path.GetFullPath(path); } catch (Exception) { return false; }
            foreach (string s in full.Split('\\', '/')) if (string.Equals(s, Segment, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // null = Ordner darf geoeffnet werden (Demo-Pfad; leer oder mit unserer Markierung), sonst Meldung
        public static string CheckFolder(string folder)
        {
            if (!IsDemoPath(folder))
                return "Die Vorführ-Version öffnet nur Ordner unterhalb von \"" + Segment + "\" (nie die echten Daten):\n" + folder;
            bool data = File.Exists(Path.Combine(folder, FileStore.FileName)) || File.Exists(Path.Combine(folder, FileStore.KeyFileName))
                || File.Exists(Path.Combine(folder, FileStore.FileName + ".bak1"));
            if (data && !File.Exists(Path.Combine(folder, MarkerFile)))
                return "In diesem Ordner liegen Daten, die nicht aus der Vorführ-Version stammen (Markierung " + MarkerFile + " fehlt). Sie werden nicht geöffnet:\n" + folder;
            return null;
        }

        // Einstellungen der Demo: alles unterhalb von root (Daten, Pakete, Sicherung, Cache); keine ini, keine Domaenenbindung
        public static AppConfig Config(string root)
        {
            AppConfig c = new AppConfig();
            c.DataFolder = Path.Combine(root, "daten");
            c.PackageFolder = Path.Combine(c.DataFolder, "Pakete");
            c.BackupFolder = Path.Combine(root, "sicherung");
            c.CachePath = Path.Combine(root, "cache.apdb");
            c.CacheDays = 0; c.PollSeconds = 10; c.ManualDays = 3650;
            return c;
        }

        // Daten oeffnen; fehlen sie (oder sind die Ausgangsdaten von einer frueheren Woche und kein anderes Demo-Fenster offen), neu erzeugen
        public static FileStore Open(AppConfig cfg, out string problem)
        {
            problem = CheckFolder(cfg.DataFolder);
            if (problem != null) return null;
            try
            {
                using (SeedLock l = new SeedLock())   // zwei gleichzeitig startende Demos: nur eine legt an/loescht
                {
                    bool have = File.Exists(Path.Combine(cfg.DataFolder, FileStore.FileName));
                    if (!have || (SeedMonday(cfg) != MondayOf(DateTime.Today) && OtherInstances() == 0)) { if (have) Wipe(cfg); DemoData.Seed(cfg); }
                }
            }
            catch (Exception ex) { problem = "Die Demo-Daten konnten nicht angelegt werden:\n" + ex.Message; return null; }
            FileStore fs = new FileStore(cfg.DataFolder, cfg.ReadOnly, cfg.CachePath, 0);
            if (fs.Offline) { problem = fs.Problem; return null; }
            return fs;
        }

        // Zuruecksetzen: Datenordner + Sicherungen loeschen und neu erzeugen. null = ok, sonst Meldung. Nur ohne weitere Demo-Fenster.
        public static string Reset(AppConfig cfg, bool ignoreOthers = false)
        {
            string err = CheckFolder(cfg.DataFolder) ?? (IsDemoPath(cfg.BackupFolder) ? null : "Sicherungsordner liegt nicht im Demo-Ordner.");
            if (err != null) return err;
            if (!ignoreOthers && OtherInstances() > 0) return "Es sind noch weitere Demo-Fenster offen. Bitte alle anderen Demo-Fenster schließen und erneut zurücksetzen.";
            try { using (SeedLock l = new SeedLock()) { Wipe(cfg); DemoData.Seed(cfg); } }
            catch (Exception ex) { return "Zurücksetzen fehlgeschlagen:\n" + ex.Message; }
            return null;
        }

        // Sperre (benannter Mutex, Sitzung) um Anlegen/Loeschen der Demo-Daten
        sealed class SeedLock : IDisposable
        {
            readonly System.Threading.Mutex m = new System.Threading.Mutex(false, "Local\\ArbeitsplanDemoSeed");
            bool held;
            public SeedLock()
            {
                try { held = m.WaitOne(30000); } catch (System.Threading.AbandonedMutexException) { held = true; }
            }
            public void Dispose() { if (held) { try { m.ReleaseMutex(); } catch (Exception) { } } m.Close(); }
        }

        // Nur bekannte Demo-Inhalte loeschen (nie den ganzen Ordner rekursiv); Ordner danach nur entfernen, wenn leer.
        static void Wipe(AppConfig cfg)
        {
            if (CheckFolder(cfg.DataFolder) != null || !IsDemoPath(cfg.BackupFolder)) throw new InvalidOperationException("kein Demo-Ordner");
            string d = cfg.DataFolder;
            if (Directory.Exists(d))
            {
                string[] files = { FileStore.FileName, FileStore.FileName + ".bak1", FileStore.FileName + ".bak2", FileStore.FileName + ".bak3", FileStore.KeyFileName, MarkerFile, "arbeitsplan.lock", "version" };
                foreach (string f in files) DeleteFile(Path.Combine(d, f));
                foreach (string f in Directory.GetFiles(d, "*.tmp")) DeleteFile(f);
                foreach (string sub in new[] { "Pakete", "sperren", "nachrichten" })
                    if (Directory.Exists(Path.Combine(d, sub))) Directory.Delete(Path.Combine(d, sub), true);
                TryRemoveEmpty(d);
            }
            string b = cfg.BackupFolder;
            if (Directory.Exists(b))
            {
                foreach (string f in Directory.GetFiles(b, "*.apbak")) DeleteFile(f);
                foreach (string f in Directory.GetFiles(b, "*.tmp")) DeleteFile(f);
                TryRemoveEmpty(b);
            }
        }

        static void DeleteFile(string f)
        {
            if (!File.Exists(f)) return;
            File.SetAttributes(f, FileAttributes.Normal);   // Schluessel ist Hidden+System
            File.Delete(f);
        }

        static void TryRemoveEmpty(string dir) { try { Directory.Delete(dir, false); } catch (Exception) { } }

        public static DateTime SeedMonday(AppConfig cfg)
        {
            DateTime d;
            try { if (DateTime.TryParseExact(File.ReadAllText(Path.Combine(cfg.DataFolder, MarkerFile)).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d; }
            catch (Exception) { }
            return DateTime.MinValue;
        }

        // Weitere laufende Demo-Prozesse (gleicher Prozessname wie dieser)
        public static int OtherInstances()
        {
            try
            {
                using (Process me = Process.GetCurrentProcess())
                {
                    Process[] all = Process.GetProcessesByName(me.ProcessName);
                    int n = 0;
                    foreach (Process p in all) { if (p.Id != me.Id) n++; p.Dispose(); }
                    return n;
                }
            }
            catch (Exception) { return 0; }
        }
    }
}
#endif
