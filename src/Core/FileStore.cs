using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Datenspeicher als EINE verschluesselte Datei "arbeitsplan.apdb" im Datenordner (Pilot ohne Server).
    // Inhalt: GZip(XML wie die Sicherung + Nutzer/Rechte), verschluesselt und signiert mit Protected (AES-256 + HMAC-SHA256).
    // Schutz gegen Neugier (Texteditor, Excel) und unbemerkte Aenderung. KEIN Schutz gegen gezielten Angriff:
    // der Schluessel steckt verschleiert im Programm (und im Quellcode). Echter Schutz erst mit Server/Datenbank.
    // Mehrbenutzer (gemeinsamer Ordner): jedes Schreiben unter einer Sperrdatei: neu einlesen -> Aenderung anwenden
    // (Versionspruefung je Block) -> .tmp schreiben -> atomar ersetzen; die letzten 3 Staende bleiben als .bak1..3.
    public class FileStore : IDataStore
    {
        public const string FileName = "arbeitsplan.apdb";
        const int Rounds = 2000;   // Schluessel ist bereits lang und zufaellig; viele PBKDF2-Runden braechten nur Wartezeit
        static readonly byte[] K = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        static string Key() { char[] c = new char[K.Length]; for (int i = 0; i < K.Length; i++) c[i] = (char)(K[i] ^ ((0x5C + i * 7) & 0xFF)); return new string(c); }

        readonly string folder, path, cachePath;
        readonly bool readOnlyCfg;
        MemoryStore mem = new MemoryStore();
        UserDb users = UserDb.Default();
        DateTime stamp; long size = -1;   // Stand der zuletzt gelesenen/geschriebenen Datei

        public bool Offline { get; private set; }
        public string Problem { get; private set; }   // Meldung fuer den Nutzer, sonst null
        public bool ReadOnly { get { return readOnlyCfg || Offline; } }
        public string Folder { get { return folder; } }
        public UserDb Users { get { return users; } }

        // Wirft nie: Fehler fuehren zu Offline (letzter lokaler Stand aus cachePath oder leer, nur Lesen) + Problem.
        public FileStore(string folder, bool readOnly, string cachePath)
        {
            this.folder = folder; readOnlyCfg = readOnly; this.cachePath = cachePath;
            path = Path.Combine(folder, FileName);
            try { Open(); }
            catch (Exception ex) { GoOffline(ex); }
        }

        void Open()
        {
            if (!readOnlyCfg) Directory.CreateDirectory(folder);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Ordner nicht gefunden.");
            if (File.Exists(path)) { Load(); return; }
            if (File.Exists(path + ".bak1")) throw new InvalidDataException("Die Datendatei fehlt, es gibt aber ältere Stände (" + FileName + ".bak1…3). Admin: neuesten Stand in " + FileName + " umbenennen.");
            if (readOnlyCfg) { Problem = "Im Datenordner liegen noch keine Daten:\n" + folder; return; }
            WithLock(delegate { if (File.Exists(path)) Load(); else WriteFile(); });   // Erststart: Standardstruktur, keine Testdaten
        }

        void GoOffline(Exception ex)
        {
            Offline = true;
            bool bad = ex is CryptographicException || ex is InvalidDataException || ex is XmlException || ex is FormatException;
            Problem = (bad ? "Die Datendatei konnte nicht gelesen werden (beschädigt, verändert oder fehlend):\n" : "Der Datenordner ist nicht erreichbar:\n")
                + folder + "\n(" + ex.Message + ")";
            try
            {
                if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                {
                    Apply(Decode(File.ReadAllBytes(cachePath)));
                    Problem += "\n\nAngezeigt wird der letzte Stand auf diesem PC vom " + File.GetLastWriteTime(cachePath).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " (nur Lesen).";
                    return;
                }
            }
            catch (Exception) { mem = new MemoryStore(); users = UserDb.Default(); }
            Problem += "\n\nKein lokaler Stand vorhanden: leere Ansicht, nur Lesen.";
        }

        // ---------- Datei ----------

        static StoreData Decode(byte[] blob)
        {
            byte[] z = Protected.UnprotectBytes(blob, Key(), Rounds);
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(new MemoryStream(z), CompressionMode.Decompress)) gz.CopyTo(ms);
                return Backup.FromXml(ms.ToArray());
            }
        }

        static byte[] Encode(StoreData d)
        {
            byte[] xml = Backup.ToXml(d);
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true)) gz.Write(xml, 0, xml.Length);
                return Protected.ProtectBytes(ms.ToArray(), Key(), Rounds);
            }
        }

        void Apply(StoreData d)
        {
            MemoryStore m = new MemoryStore(); m.Restore(d);
            mem = m; users = d.Users ?? UserDb.Default();
        }

        void Load()
        {
            FileInfo fi = new FileInfo(path);
            DateTime t = fi.LastWriteTimeUtc; long len = fi.Length;
            byte[] blob = ReadShared();
            Apply(Decode(blob));
            stamp = t; size = len;
            WriteCache(blob);
        }

        byte[] ReadShared()
        {
            for (int i = 0; ; i++)
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (MemoryStream ms = new MemoryStream()) { fs.CopyTo(ms); return ms.ToArray(); }
                }
                catch (IOException) { if (i >= 20) throw; Thread.Sleep(100); }   // waehrend des Ersetzens kurz nicht lesbar
        }

        bool Changed() { FileInfo fi = new FileInfo(path); return fi.Exists && (fi.LastWriteTimeUtc != stamp || fi.Length != size); }

        void WriteFile()
        {
            StoreData d = mem.Snapshot(); d.Users = users;
            byte[] blob = Encode(d);
            string tmp = path + ".tmp", b1 = path + ".bak1", b2 = path + ".bak2", b3 = path + ".bak3";
            File.WriteAllBytes(tmp, blob);
            if (File.Exists(path))
            {
                if (File.Exists(b3)) File.Delete(b3);
                if (File.Exists(b2)) File.Move(b2, b3);
                if (File.Exists(b1)) File.Move(b1, b2);
                try { File.Replace(tmp, path, b1); }
                catch (IOException) { File.Copy(path, b1, true); File.Copy(tmp, path, true); File.Delete(tmp); }   // Laufwerke ohne Replace
            }
            else File.Move(tmp, path);
            FileInfo fi = new FileInfo(path); stamp = fi.LastWriteTimeUtc; size = fi.Length;
            WriteCache(blob);
        }

        void WriteCache(byte[] blob)
        {
            if (string.IsNullOrEmpty(cachePath)) return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(cachePath)); File.WriteAllBytes(cachePath, blob); }
            catch (Exception) { }   // Cache ist nur Komfort
        }

        void WithLock(Action a)
        {
            if (!Directory.Exists(folder)) throw new IOException("Der Datenordner ist nicht erreichbar: " + folder);
            FileStream l = null;
            for (int i = 0; l == null; i++)
            {
                try { l = new FileStream(Path.Combine(folder, "arbeitsplan.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (UnauthorizedAccessException) { throw new IOException("Keine Schreibrechte im Datenordner: " + folder); }
                catch (IOException) { if (i >= 50) throw new IOException("Die Daten werden gerade von einem anderen PC gespeichert. Bitte gleich noch einmal versuchen."); Thread.Sleep(100); }
            }
            using (l) a();
        }

        delegate bool Op(MemoryStore m);

        // Neu einlesen (falls geaendert), Aenderung anwenden, schreiben. false = op hat nichts geaendert (Konflikt).
        bool Write(Op op)
        {
            if (ReadOnly) throw new IOException(Offline ? "Der Datenordner ist nicht erreichbar (OFFLINE), es wird nur der letzte Stand angezeigt."
                : "Dieser PC ist auf \"nur lesen\" eingestellt (NurLesen=1 in Arbeitsplan.ini).");
            bool done = false;
            WithLock(delegate
            {
                if (Changed()) Load();
                if (!op(mem)) return;
                try { WriteFile(); done = true; }
                catch (Exception) { try { Load(); } catch (Exception) { size = -1; } throw; }   // nichts Ungespeichertes im Speicher lassen
            });
            return done;
        }

        // Liest neu, wenn sich die Datei geaendert hat (oder Offline -> wieder erreichbar). true = Daten neu.
        public bool Refresh()
        {
            try
            {
                if (Offline)
                {
                    if (!File.Exists(path)) return false;
                    Load(); Offline = false; Problem = null;
                    return true;
                }
                if (!Changed()) return false;
                Load();
                return true;
            }
            catch (Exception) { return false; }   // Netz kurz weg: alter Stand bleibt sichtbar
        }

        // ---------- IDataStore ----------

        public Entry Get(DateTime day, int rowId) { return mem.Get(day, rowId); }
        public bool Save(DateTime day, int rowId, Entry e) { return Write(delegate(MemoryStore m) { return m.Save(day, rowId, e); }); }
        public List<RowInfo> GetRows() { return mem.GetRows(); }
        public void SaveStructure(List<RowInfo> rows) { Write(delegate(MemoryStore m) { m.SaveStructure(rows); return true; }); }
        public List<SearchHit> Find(string q) { return mem.Find(q); }
        public Catalog GetCatalog() { return mem.GetCatalog(); }
        public void SaveCatalog(Catalog c) { Write(delegate(MemoryStore m) { m.SaveCatalog(c); return true; }); }
        public void AppendAudit(AuditEntry a) { Write(delegate(MemoryStore m) { m.AppendAudit(a); return true; }); }
        public List<AuditEntry> GetAudit(DateTime day, int rowId) { return mem.GetAudit(day, rowId); }
        public StoreData Snapshot() { return mem.Snapshot(); }
        public void Restore(StoreData d) { Write(delegate(MemoryStore m) { m.Restore(d); return true; }); }   // Nutzer bleiben unveraendert
        public void SaveUsers(UserDb db) { Write(delegate(MemoryStore m) { users = db; return true; }); }
    }

    // Soft-Lock als kleine Datei je Block im Datenordner ("sperren\JJJJ-MM-TT_Zeile.lock", Inhalt = Name). Verfaellt nach 30 min.
    public class FileLockService : ILockService
    {
        static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        readonly string dir;

        public FileLockService(string dataFolder) { dir = Path.Combine(dataFolder, "sperren"); }

        string FileOf(DateTime d, int r) { return Path.Combine(dir, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + r + ".lock"); }

        public string Holder(DateTime day, int row, string me)
        {
            try
            {
                string f = FileOf(day, row);
                if (!File.Exists(f) || DateTime.UtcNow - File.GetLastWriteTimeUtc(f) >= Stale) return null;
                string u = File.ReadAllText(f, Utf8).Trim();
                return u.Length > 0 && u != me ? u : null;
            }
            catch (Exception) { return null; }   // Sperren sind nur ein Hinweis
        }

        public void Acquire(DateTime day, int row, string me)
        {
            try { Directory.CreateDirectory(dir); File.WriteAllText(FileOf(day, row), me, Utf8); } catch (Exception) { }
        }

        public void Release(DateTime day, int row, string me)
        {
            try { string f = FileOf(day, row); if (File.Exists(f) && File.ReadAllText(f, Utf8).Trim() == me) File.Delete(f); } catch (Exception) { }
        }
    }
}
