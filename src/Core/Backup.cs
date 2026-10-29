using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    public class StoredEntry { public DateTime Day; public int RowId; public Entry Data; }

    // Vollstaendiger Datenbestand (Plaene ohne Zeitgrenze, Struktur, Katalog, Protokoll).
    public class StoreData
    {
        public List<RowInfo> Rows = new List<RowInfo>();
        public List<StoredEntry> Entries = new List<StoredEntry>();
        public Catalog Catalog = new Catalog();
        public List<AuditEntry> Audit = new List<AuditEntry>();
        public DutyConfig Duties = DutyConfig.Default();
        public List<WeekMark> WeekMarks = new List<WeekMark>();
        public List<DeviceStatus> Statuses = new List<DeviceStatus>();   // manuelle Equipment-Status
        public bool StatusesKnown;          // gelesene Datei enthielt <Status> (Format >= 4); sonst bleibt beim Wiederherstellen der Stand
        public UserDb Users;                // nur in der Datendatei (FileStore), nie in Sicherungen
    }

    // Datensicherung als eine Datei (.apbak): Nutzlast GZip( "APBAK2\n" + SHA-256-Hex des XML + "\n" + XML ), als APBAK2 mit dem
    // Datenschluessel verschluesselt+signiert (Protected). APBAK1 (gleiche Nutzlast, nur GZip, ohne Schluessel) bleibt lesbar
    // und wird weiter geschrieben, wenn der Store keinen Schluessel hat (MemoryStore/Testdaten: keine Schluesseldatei vorhanden).
    // Geschuetzte Einstellungsdateien (Protected) sind nicht Teil der Sicherung.
    // Datei stammt von einer neueren Programmversion: nur Lesen mit Hinweis "neu starten" (FileStore -> OFFLINE)
    public class NewerFormatException : Exception
    {
        public NewerFormatException() : base("Die Daten wurden mit einer neueren Programmversion gespeichert. Bitte das Programm schließen und neu starten.") { }
    }

    public static partial class Backup
    {
        // Datenformat; bei inkompatibler Aenderung erhoehen (aeltere exe lesen die Datei dann nicht mehr und schreiben nie zurueck)
        public const int FormatVersion = 5;   // 5: Puffer je Block, Notizzeilen (Fmt n/oi), Baustein-Felder (puffertyp, notizvorlage); 2: Equipment-Stammdaten im Katalog; 3: Pflichtpunkte, Varianten, Wochenhaken, Produktprofile, "gehoertzu"; 4: manueller Equipment-Status, Statusuebergaenge, Regeln auf Art-/Gruppenebene ("@Art", "#Gruppe")
        public const string Ext = ".apbak", Prefix = "Arbeitsplan_";
        public const int Keep = 14;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- Ordner-Einstellung (sicherung.ini: "ordner=<Pfad>") ----------

        public static string LoadFolder(string iniPath, string defaultFolder)
        {
            try
            {
                if (File.Exists(iniPath))
                    foreach (string l in File.ReadAllLines(iniPath, Utf8))
                        if (l.StartsWith("ordner=") && l.Length > 7) return l.Substring(7).Trim();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return defaultFolder;
        }

        public static void SaveFolder(string iniPath, string folder) { SaveSetting(iniPath, "ordner", folder); }

        // "stunden=<n>": Abstand der automatischen Sicherung (hat wie "ordner=" Vorrang vor Arbeitsplan.ini)
        public static int LoadHours(string iniPath, int defaultHours)
        {
            int n;
            try
            {
                if (File.Exists(iniPath))
                    foreach (string l in File.ReadAllLines(iniPath, Utf8))
                        if (l.StartsWith("stunden=") && int.TryParse(l.Substring(8).Trim(), NumberStyles.Integer, Inv, out n)) return ClampHours(n);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return ClampHours(defaultHours);
        }

        public const int MaxHours = 720;
        public static int ClampHours(int h) { return Math.Max(1, Math.Min(MaxHours, h)); }

        // Eine Zeile "key=value" setzen, andere Zeilen bleiben erhalten
        public static void SaveSetting(string iniPath, string key, string value)
        {
            List<string> lines = new List<string>();
            if (File.Exists(iniPath)) foreach (string l in File.ReadAllLines(iniPath, Utf8)) if (!l.StartsWith(key + "=") && l.Trim().Length > 0) lines.Add(l);
            lines.Add(key + "=" + value);
            File.WriteAllText(iniPath, string.Join("\r\n", lines.ToArray()) + "\r\n", Utf8);
        }

        // ---------- Schreiben / Aufraeumen ----------

        public static string[] Files(string folder)
        {
            if (!Directory.Exists(folder)) return new string[0];
            string[] f = Directory.GetFiles(folder, Prefix + "*" + Ext);
            Array.Sort(f, StringComparer.OrdinalIgnoreCase);   // Zeitstempel im Namen: aelteste zuerst
            return f;
        }

        public static DateTime? Last(string folder)
        {
            string[] f = Files(folder);
            return f.Length == 0 ? (DateTime?)null : File.GetLastWriteTime(f[f.Length - 1]);
        }

        public static bool AutoDue(string folder, int hours) { return Due(Last(folder), DateTime.Now, hours); }

        // Automatische Sicherung faellig: noch keine oder die letzte ist mindestens "hours" Stunden alt. Liegt die letzte in der
        // Zukunft (Uhr des Servers vor), ist nichts faellig: sonst wuerde bei jeder Pruefung gesichert und die Historie verdraengt.
        public static bool Due(DateTime? last, DateTime now, int hours)
        {
            return last == null || now - last.Value >= TimeSpan.FromHours(ClampHours(hours));
        }

        // Schreibt die Sicherung (erst .tmp, dann umbenennen) und behaelt die letzten Keep Dateien. Liefert den Pfad.
        // Schluessel des Datenspeichers (Datei arbeitsplan.key); null = Store ohne Schluessel (MemoryStore/Testdaten:
        // dort gibt es keine Schluesseldatei, die Sicherung bleibt wie bisher APBAK1). FileStore ohne lesbaren Schluessel wirft.
        public static string KeyOf(IDataStore store)
        {
            FileStore fs = store as FileStore;
            return fs == null ? null : fs.BackupKey();
        }

        public static string WriteTo(IDataStore store, string folder, string tag) { return WriteTo(store, folder, tag, KeyOf(store)); }

        public static string WriteTo(IDataStore store, string folder, string tag, string key) { return WriteTo(store.Snapshot(), folder, tag, key); }

        // Schreibt einen bereits geholten Stand (auch aus einem Hintergrund-Thread: braucht den Store nicht mehr)
        public static string WriteTo(StoreData snapshot, string folder, string tag, string key)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, Prefix + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", Inv) + (tag != null ? "_" + tag : "") + Ext);
            Write(snapshot, path, key);
            string[] f = Files(folder);
            for (int i = 0; i < f.Length - Keep; i++)
                try { File.Delete(f[i]); } catch (Exception) { }   // ohne Loeschrecht (Ordnerrechte der IT) bleiben aeltere liegen
            return path;
        }

        // key != null: APBAK2 = Protected( GZip( Kopf "APBAK2" + SHA-256 + XML ) ); key == null: APBAK1 (nur GZip).
        public static void Write(StoreData d, string path, string key)
        {
            byte[] xml = ToXml(d);
            string tmp = path + ".tmp";
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    byte[] head = Utf8.GetBytes((key != null ? "APBAK2" : "APBAK1") + "\n" + Hex(Hash(xml)) + "\n");
                    gz.Write(head, 0, head.Length);
                    gz.Write(xml, 0, xml.Length);
                }
                byte[] outp = key != null ? Protected.ProtectBytes(ms.ToArray(), key, FileStore.Rounds) : ms.ToArray();
                File.WriteAllBytes(tmp, outp);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // ---------- Lesen (mit Selbstpruefung) ----------

        static InvalidDataException Bad() { return new InvalidDataException("Die Sicherung ist beschädigt oder wurde verändert und wird nicht geladen."); }

        // key: Datenschluessel (fuer APBAK2); APBAK1-Dateien brauchen ihn nicht.
        public static StoreData Read(string path, string key)
        {
            byte[] all; int len;
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                // GZip beginnt mit 1F 8B (APBAK1); alles andere ist die verschluesselte Form (APBAK2)
                bool gz = raw.Length > 2 && raw[0] == 0x1F && raw[1] == 0x8B;
                if (!gz)
                {
                    if (key == null) throw Bad();
                    try { raw = Protected.UnprotectBytes(raw, key, FileStore.Rounds); }
                    catch (CryptographicException) { throw Bad(); }
                }
                using (MemoryStream ms = Unzip(new MemoryStream(raw))) { all = ms.GetBuffer(); len = (int)ms.Length; }
            }
            catch (InvalidDataException) { throw Bad(); }
            int a = Array.IndexOf(all, (byte)'\n', 0, len), b = a < 0 ? -1 : Array.IndexOf(all, (byte)'\n', a + 1, len - a - 1);
            if (b < 0) throw Bad();
            string magic = Utf8.GetString(all, 0, a);
            if (magic != "APBAK1" && magic != "APBAK2") throw Bad();
            string hash = Utf8.GetString(all, a + 1, b - a - 1);
            if (hash != Hex(Hash(all, b + 1, len - b - 1))) throw Bad();
            try { return FromXml(new MemoryStream(all, b + 1, len - b - 1, false)); }
            catch (XmlException) { throw Bad(); }
            catch (FormatException) { throw Bad(); }
            catch (OverflowException) { throw Bad(); }
        }

        // Entpackt GZip vollstaendig (Position 0); mehr als MaxUnzip Bytes = InvalidDataException (Schutz vor "Zip-Bomben").
        public const long MaxUnzip = 200L * 1024 * 1024;
        internal static MemoryStream Unzip(Stream src)
        {
            MemoryStream ms = new MemoryStream();
            using (GZipStream gz = new GZipStream(src, CompressionMode.Decompress))
            {
                byte[] buf = new byte[65536]; int n;
                while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + n > MaxUnzip) { ms.Dispose(); throw new InvalidDataException("Entpackte Daten sind zu groß (über 200 MB)."); }
                    ms.Write(buf, 0, n);
                }
            }
            ms.Position = 0;
            return ms;
        }

        internal static byte[] Hash(byte[] b) { return Hash(b, 0, b.Length); }
        internal static byte[] Hash(byte[] b, int off, int len)
        {
            try { using (SHA256 h = new SHA256Managed()) return h.ComputeHash(b, off, len); }
            catch (InvalidOperationException) { using (SHA256 h = new SHA256CryptoServiceProvider()) return h.ComputeHash(b, off, len); }   // FIPS-Richtlinie
        }

        internal static string Hex(byte[] b) { StringBuilder sb = new StringBuilder(); foreach (byte x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }

        // ---------- XML ----------

        static string I(long v) { return v.ToString(Inv); }
        static string B(bool v) { return v ? "1" : "0"; }

        static IEnumerable<XmlElement> Kids(XmlNode n, string name)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null && e.Name == name) yield return e; }
        }
        static XmlElement Kid(XmlNode n, string name) { foreach (XmlElement e in Kids(n, name)) return e; return null; }
        static int Int(XmlElement e, string a) { return int.Parse(e.GetAttribute(a), Inv); }
        static bool Bool(XmlElement e, string a) { return e.GetAttribute(a) == "1"; }
        // fehlendes Attribut = Standard; unbekannter Wert = FormatException (wie andere Lesefehler)
        static T En<T>(XmlElement e, string a, T def) where T : struct
        {
            string s = e.GetAttribute(a);
            if (s.Length == 0) return def;
            try { return (T)Enum.Parse(typeof(T), s); }
            catch (ArgumentException) { throw new FormatException(); }
        }
        static DateTime Day(XmlElement e) { return DateTime.ParseExact(e.GetAttribute("tag"), "yyyy-MM-dd", Inv); }

    }
}
