using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
// Zeilenumbrueche in Texten immer "\n".
namespace Arbeitsplan.Core
{
    public partial class MemoryStore : IDataStore
    {
        public static readonly string[] DefaultRows = { "Suite 1", "Mix / Split", "Suite 2", "Puffer", "Lyo", "Freeze / Thaw", "Versand", "Einwaage", "CIP / SIP", "Raum CC", "", "" };
        static readonly string[] LkzRows = { "Suite 1", "Mix / Split", "Suite 2", "Lyo" };

        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();
        readonly List<RowInfo> rows = new List<RowInfo>();
        int nextId = 1;
        public bool FailSaves;                               // Test: simuliert PC ohne Schreibzugriff (System-PC)

        public MemoryStore()
        {
            foreach (string n in DefaultRows)
            {
                RowInfo r = new RowInfo(); r.Id = nextId++; r.Name = n; r.HasLkz = Array.IndexOf(LkzRows, n) >= 0;
                if (n == "Puffer") { r.Variants = "mobil|stationaer"; duties.AddPufferDefaults(r.Id); }
                rows.Add(r);
            }
        }

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + r; }
        void CheckWrite() { if (FailSaves) throw new IOException("Kein Schreibzugriff (simulierter System-PC)."); }
        int IdOf(string name) { foreach (RowInfo r in rows) if (r.Name == name) return r.Id; return -1; }

        Catalog catalog = Catalog.Default();
        public Catalog GetCatalog() { return catalog.Clone(); }
        public void SaveCatalog(Catalog c) { CheckWrite(); catalog = c.Clone(); }

        DutyConfig duties = DutyConfig.Default();
        public DutyConfig GetDuties() { return duties.Clone(); }
        public void SaveDuties(DutyConfig d) { CheckWrite(); duties = d.Clone(); }

        readonly Dictionary<string, WeekMark> marks = new Dictionary<string, WeekMark>();
        static string MarkKey(DateTime monday, string k) { return monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + k; }
        public List<WeekMark> GetWeekMarks(DateTime monday)
        {
            List<WeekMark> l = new List<WeekMark>();
            foreach (WeekMark m in marks.Values) if (m.Monday == monday.Date) l.Add(m.Clone());
            return l;
        }
        public bool SaveWeekMark(WeekMark m, AuditEntry a)
        {
            CheckWrite();
            string k = MarkKey(m.Monday.Date, m.Key);
            WeekMark cur;
            marks.TryGetValue(k, out cur);
            if (cur != null && cur.Version != m.Version) return false;
            WeekMark c = m.Clone(); c.Monday = m.Monday.Date; c.Version = (cur == null ? 0 : cur.Version) + 1;
            marks[k] = c;
            m.Version = c.Version;
            if (a != null) { a.Day = c.Monday; a.RowId = -1; a.Mark = c.Key; a.Value = c.Done ? "1" : "0"; a.Version = c.Version; AppendAudit(a); }
            return true;
        }

        readonly Dictionary<string, DeviceStatus> statuses = new Dictionary<string, DeviceStatus>();
        public List<DeviceStatus> GetStatuses() { List<DeviceStatus> l = new List<DeviceStatus>(); foreach (DeviceStatus s in statuses.Values) l.Add(s.Clone()); return l; }
        public bool SetStatus(DeviceStatus s, AuditEntry a)
        {
            CheckWrite();
            string k = s.Id.Trim().ToUpperInvariant();
            if (s.Cleared) statuses.Remove(k); else statuses[k] = s.Clone();
            if (a != null) AppendAudit(a);
            return true;
        }

        public List<RowInfo> GetRows() { List<RowInfo> l = new List<RowInfo>(); foreach (RowInfo r in rows) l.Add(r.Clone()); return l; }

        public void SaveStructure(List<RowInfo> list)
        {
            CheckWrite();
            List<RowInfo> res = new List<RowInfo>();
            foreach (RowInfo r in list)
            {
                RowInfo c = r.Clone();
                if (c.Id <= 0) c.Id = nextId++;
                res.Add(c);
            }
            foreach (RowInfo old in rows)       // keine Zeile geht verloren: fehlende werden hinten angehaengt
                if (!res.Exists(delegate(RowInfo x) { return x.Id == old.Id; })) res.Add(old);
            rows.Clear(); rows.AddRange(res);
        }

        public Entry Get(DateTime day, int rowId)
        {
            Entry e;
            return items.TryGetValue(Key(day, rowId), out e) ? e.Clone() : null;
        }

        public bool Save(DateTime day, int rowId, Entry e)
        {
            CheckWrite();
            string k = Key(day, rowId);
            Entry cur;
            items.TryGetValue(k, out cur);
            if (cur != null && cur.Version != e.Version) return false;
            Entry c = e.Clone(); c.Version = (cur == null ? 0 : cur.Version) + 1;
            items[k] = c;
            e.Version = c.Version;   // erst nach dem Schreiben
            return true;
        }

        public bool SaveWithAudit(DateTime day, int rowId, Entry e, AuditEntry a)
        {
            Entry before = Get(day, rowId);
            if (!Save(day, rowId, e)) return false;
            a.Before = before; a.After = e.Clone(); a.Version = e.Version;
            AppendAudit(a);
            return true;
        }

        public int SaveManyWithAudit(IList<SaveItem> list)
        {
            CheckWrite();   // vor der ersten Aenderung: alles oder nichts
            int n = 0;
            foreach (SaveItem it in list)
            {
                it.Saved = it.Audit == null ? Save(it.Day, it.RowId, it.Data) : SaveWithAudit(it.Day, it.RowId, it.Data, it.Audit);
                if (it.Saved) n++;
            }
            return n;
        }

        readonly List<AuditEntry> audit = new List<AuditEntry>();
        public void AppendAudit(AuditEntry a) { audit.Add(a.Clone()); }
        public List<AuditEntry> GetAudit(DateTime day, int rowId)
        {
            List<AuditEntry> l = new List<AuditEntry>();
            foreach (AuditEntry a in audit) if (a.Day == day.Date && a.RowId == rowId) l.Add(a.Clone());
            return l;
        }

        public StoreData Snapshot() { return Snapshot(true); }

        // copy=false: teilt die Objekte (nur zum sofortigen Serialisieren, z. B. FileStore beim Schreiben)
        internal StoreData Snapshot(bool copy)
        {
            StoreData d = new StoreData();
            d.Rows = copy ? GetRows() : new List<RowInfo>(rows); d.Catalog = copy ? GetCatalog() : catalog;
            d.Duties = copy ? GetDuties() : duties;
            foreach (WeekMark m in marks.Values) d.WeekMarks.Add(copy ? m.Clone() : m);
            foreach (DeviceStatus st in statuses.Values) d.Statuses.Add(copy ? st.Clone() : st);
            foreach (KeyValuePair<string, Entry> kv in items)
            {
                StoredEntry s = new StoredEntry(); string[] p = kv.Key.Split('|');
                s.Day = DateTime.ParseExact(p[0], "yyyy-MM-dd", CultureInfo.InvariantCulture); s.RowId = int.Parse(p[1], CultureInfo.InvariantCulture); s.Data = copy ? kv.Value.Clone() : kv.Value;
                d.Entries.Add(s);
            }
            if (copy) foreach (AuditEntry a in audit) d.Audit.Add(a.Clone());
            else d.Audit.AddRange(audit);
            return d;
        }

        // Frisch gelesene Daten uebernehmen (leerer Store, ohne Kopie/Zusammenfuehren); d darf danach nicht mehr genutzt werden.
        internal void Adopt(StoreData d)
        {
            items.Clear();
            foreach (StoredEntry s in d.Entries) items[Key(s.Day, s.RowId)] = s.Data;
            rows.Clear(); nextId = 1;
            foreach (RowInfo r in d.Rows) { rows.Add(r); nextId = Math.Max(nextId, r.Id + 1); }
            catalog = d.Catalog; duties = d.Duties;
            marks.Clear();
            foreach (WeekMark m in d.WeekMarks) marks[MarkKey(m.Monday, m.Key)] = m;
            statuses.Clear(); foreach (DeviceStatus st in d.Statuses) statuses[st.Id.Trim().ToUpperInvariant()] = st;
            audit.Clear(); audit.AddRange(d.Audit);
        }

        public void Restore(StoreData d)
        {
            CheckWrite();
            // Ueber vorhandene Daten: Version = max(alt, Sicherung) + 1, damit sich Versionsnummern nicht wiederholen;
            // Bloecke, die es in der Sicherung nicht gibt, werden leer mit weiterlaufender Version (Laden einer Datei: unveraendert).
            Dictionary<string, Entry> old = new Dictionary<string, Entry>(items);
            items.Clear();
            foreach (StoredEntry s in d.Entries)
            {
                Entry e = s.Data.Clone(), o;
                if (old.TryGetValue(Key(s.Day, s.RowId), out o)) e.Version = Math.Max(o.Version, e.Version) + 1;
                items[Key(s.Day, s.RowId)] = e;
            }
            foreach (KeyValuePair<string, Entry> kv in old)
                if (!items.ContainsKey(kv.Key)) { Entry e = new Entry(); e.Version = kv.Value.Version + 1; items[kv.Key] = e; }
            rows.Clear(); nextId = 1;
            foreach (RowInfo r in d.Rows) { rows.Add(r.Clone()); nextId = Math.Max(nextId, r.Id + 1); }
            catalog = d.Catalog.Clone(); duties = d.Duties.Clone();
            Dictionary<string, WeekMark> oldMarks = new Dictionary<string, WeekMark>(marks);   // Versionen wie bei den Bloecken
            marks.Clear();
            foreach (WeekMark m in d.WeekMarks)
            {
                WeekMark c = m.Clone(), o; string k = MarkKey(c.Monday, c.Key);
                if (oldMarks.TryGetValue(k, out o)) c.Version = Math.Max(o.Version, c.Version) + 1;
                marks[k] = c;
            }
            foreach (KeyValuePair<string, WeekMark> kv in oldMarks)
                if (!marks.ContainsKey(kv.Key)) { WeekMark c = kv.Value.Clone(); c.Done = false; c.Version++; marks[kv.Key] = c; }
            if (d.StatusesKnown) { statuses.Clear(); foreach (DeviceStatus st in d.Statuses) statuses[st.Id.Trim().ToUpperInvariant()] = st.Clone(); }   // Sicherung ohne Status (aelter): Stand bleibt
            // Protokoll bleibt append-only: Eintraege nach der Sicherung gehen nicht verloren
            Dictionary<string, AuditEntry> all = new Dictionary<string, AuditEntry>();
            foreach (AuditEntry a in d.Audit) all[a.Key()] = a.Clone();
            foreach (AuditEntry a in audit) if (!all.ContainsKey(a.Key())) all[a.Key()] = a;
            audit.Clear(); audit.AddRange(all.Values);
            audit.Sort(delegate(AuditEntry x, AuditEntry y) { int c = x.Time.CompareTo(y.Time); return c != 0 ? c : x.Version.CompareTo(y.Version); });
        }

        public List<SearchHit> Find(string q)
        {
            List<SearchHit> res = new List<SearchHit>();
            if (string.IsNullOrEmpty(q)) return res;
            foreach (KeyValuePair<string, Entry> kv in items)
            {
                Entry e = kv.Value;
                bool hit = e.Content.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (string p in e.People) if (p.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) hit = true;
                if (BufferSet.Matches(e.Buffers, q)) hit = true;   // Puffer: Name und Text
                if (!hit) continue;
                string[] parts = kv.Key.Split('|');
                SearchHit h = new SearchHit();
                h.Day = DateTime.ParseExact(parts[0], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                h.RowId = int.Parse(parts[1]);
                res.Add(h);
            }
            res.Sort(delegate(SearchHit a, SearchHit b)
            {
                int c = a.Day.CompareTo(b.Day);
                return c != 0 ? c : rows.FindIndex(delegate(RowInfo r) { return r.Id == a.RowId; }).CompareTo(rows.FindIndex(delegate(RowInfo r) { return r.Id == b.RowId; }));
            });
            return res;
        }
    }
}
