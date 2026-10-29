using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;   // ProtectedData: System.Security.dll
using System.Text;
using System.Threading;
using System.Xml;

namespace Arbeitsplan.Core
{
    public partial class FileStore : IDataStore
    {

        // ---------- IDataStore ----------

        public Entry Get(DateTime day, int rowId) { return mem.Get(day, rowId); }
        public bool Save(DateTime day, int rowId, Entry e) { return SaveWithAudit(day, rowId, e, null); }
        // Block + Protokoll in EINEM Schreibvorgang; e.Version steigt erst, wenn die Datei geschrieben ist.
        public bool SaveWithAudit(DateTime day, int rowId, Entry e, AuditEntry a)
        {
            Entry c = e.Clone();
            if (!Write(delegate(MemoryStore m) { return a == null ? m.Save(day, rowId, c) : m.SaveWithAudit(day, rowId, c, a); })) return false;
            e.Version = c.Version;
            return true;
        }
        // Alle Bloecke (z. B. Paket-Uebernahme) in EINEM Schreibvorgang; Ergebnis/Version erst nach dem Schreiben uebernehmen.
        public int SaveManyWithAudit(IList<SaveItem> items)
        {
            foreach (SaveItem it in items) it.Saved = false;
            if (items.Count == 0) return 0;
            List<SaveItem> cs = new List<SaveItem>();
            foreach (SaveItem it in items)
            {
                SaveItem c = new SaveItem(); c.Day = it.Day; c.RowId = it.RowId; c.Data = it.Data.Clone(); c.Audit = it.Audit;
                cs.Add(c);
            }
            int n = 0;
            if (!Write(delegate(MemoryStore m) { n = m.SaveManyWithAudit(cs); return n > 0; })) return 0;
            for (int i = 0; i < items.Count; i++) if (cs[i].Saved) { items[i].Saved = true; items[i].Data.Version = cs[i].Data.Version; }
            return n;
        }
        public List<RowInfo> GetRows() { return mem.GetRows(); }
        public void SaveStructure(List<RowInfo> rows) { Write(delegate(MemoryStore m) { m.SaveStructure(rows); return true; }); }
        public List<SearchHit> Find(string q) { return mem.Find(q); }
        public Catalog GetCatalog() { return mem.GetCatalog(); }
        public void SaveCatalog(Catalog c) { Write(delegate(MemoryStore m) { m.SaveCatalog(c); return true; }); }
        public DutyConfig GetDuties() { return mem.GetDuties(); }
        public void SaveDuties(DutyConfig c) { Write(delegate(MemoryStore m) { m.SaveDuties(c); return true; }); }
        public List<WeekMark> GetWeekMarks(DateTime monday) { return mem.GetWeekMarks(monday); }
        // Wie SaveWithAudit: neu einlesen, Versionspruefung, Haken + Protokoll in einem Schreibvorgang
        public bool SaveWeekMark(WeekMark w, AuditEntry a)
        {
            WeekMark c = w.Clone();
            if (!Write(delegate(MemoryStore m) { return m.SaveWeekMark(c, a); })) return false;
            w.Version = c.Version;
            return true;
        }
        public List<DeviceStatus> GetStatuses() { return mem.GetStatuses(); }
        public bool SetStatus(DeviceStatus s, AuditEntry a) { DeviceStatus c = s.Clone(); return Write(delegate(MemoryStore m) { return m.SetStatus(c, a); }); }
        public void AppendAudit(AuditEntry a) { Write(delegate(MemoryStore m) { m.AppendAudit(a); return true; }); }
        public List<AuditEntry> GetAudit(DateTime day, int rowId) { return mem.GetAudit(day, rowId); }
        // Fuer die Sicherung erst den aktuellen Dateistand holen (unter der Sperre); Fehler -> keine veraltete Sicherung
        public StoreData Snapshot()
        {
            if (!Offline)
            {
                if (readOnlyCfg) { if (File.Exists(path)) Load(); }
                else WithLock(delegate { if (File.Exists(path)) Load(); });
            }
            return mem.Snapshot();
        }
        public void Restore(StoreData d) { Write(delegate(MemoryStore m) { m.Restore(d); return true; }); }   // Nutzer bleiben unveraendert
        // Unter der Sperre in den frisch gelesenen Stand einarbeiten (nicht die ganze Liste ersetzen); nichts geaendert = nichts schreiben
        public void SaveUsers(UserChanges c) { if (c.Any) Write(delegate(MemoryStore m) { users.Merge(c); return true; }); }
    }
}
