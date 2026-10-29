using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
// Zeilenumbrueche in Texten immer "\n".
namespace Arbeitsplan.Core
{
    // Manuelle Formatierung eines Textbereichs im Inhalt. Standardwerte (Rgb 0 = schwarz, Size 0, Font null) werden nicht gespeichert.
    public class FormatSpan
    {
        public int Start, Length, Rgb;      // Rgb 0xRRGGBB
        public float Size;                  // Punkt; 0 = Standardgroesse
        public bool Bold, Italic;
        public bool Note;                   // Notizzeile (immer ganze Zeilen, zusaetzlich Italic), zaehlt nicht als Inhalt
        public bool NoteWasItalic;          // nur bei Note: Zeile war vor der Markierung manuell kursiv (Aufheben stellt es wieder her)
        public string Font;                 // null = Standardschrift

        public FormatSpan Clone() { return (FormatSpan)MemberwiseClone(); }
        public string Key() { return Start + "," + Length + "," + Rgb + "," + Size + "," + Bold + "," + Italic + "," + Font + (Note ? ",N" : "") + (Note && NoteWasItalic ? "o" : "") + ";"; }
    }

    public class Entry   // Inhalt eines Blocks (Tag x Zeile)
    {
        public string Content = "";
        public List<FormatSpan> Spans = new List<FormatSpan>();
        public string[] People = { "", "", "" };   // Kuerzel je Schicht (F, S, N)
        public bool Lkz;                            // "LKZ gemessen?" (nur Zeilen mit HasLkz)
        public List<BufferItem> Buffers = new List<BufferItem>();   // Puffer (Zeile mit RowInfo.Variants); einzige Datenquelle der Herstellungstypen
        public List<string> Marks = new List<string>();   // gesetzte Pflichtpunkte (CheckDef.Key, ausser LKZ)
        public int Version;                         // Konflikterkennung

        // Typen der Puffer als VariantSet-String ("mobil:2|stationaer"), "" = keine; Eingabe fuer CheckEval
        public string Types { get { return BufferSet.Types(Buffers); } }

        public Entry Clone()
        {
            Entry e = (Entry)MemberwiseClone();
            e.People = (string[])People.Clone();
            e.Marks = new List<string>(Marks);
            e.Buffers = BufferSet.CloneAll(Buffers);
            e.Spans = new List<FormatSpan>();
            foreach (FormatSpan s in Spans) e.Spans.Add(s.Clone());
            return e;
        }
        // Block nur mit Notizzeilen gilt als leer
        public bool IsEmpty { get { return (Content.Length == 0 || (HasNotes && Notes.ContentText(this).Trim().Length == 0)) && People[0].Length == 0 && People[1].Length == 0 && People[2].Length == 0; } }
        // Leer UND ohne Haken/Variante (Zuklappen, Hinweispunkt); IsEmpty bleibt fuer Rechte/Pakete wie bisher
        public bool IsBlank { get { return !HasText && People[0].Trim().Length == 0 && People[1].Trim().Length == 0 && People[2].Trim().Length == 0 && !Lkz && Buffers.Count == 0 && Marks.Count == 0; } }
        // Inhalt mit sichtbarem Text ohne Notizzeilen (nur Leerzeichen/Tabs/Zeilenumbrueche = leer); Daten bleiben unveraendert
        public bool HasText { get { return Notes.ContentText(this).Trim().Length > 0; } }
        // Notizzeilen vorhanden (Block mit nur Notizen gilt als leer)
        public bool HasNotes { get { return Notes.NoteLines(this).Count > 0; } }
        public string MarksKey() { return string.Join("|", Marks.ToArray()); }
        public string SpanKey() { string k = ""; foreach (FormatSpan s in Spans) k += s.Key(); return k; }
    }

    public enum ShiftMode { FSN, FS, Task }

    public class RowInfo   // gilt fuer die ganze Zeile (alle Tage); wird nur ueber die Blockstruktur-Einstellungen geaendert
    {
        public ShiftMode Mode = ShiftMode.FSN;
        public string Variants = "";                // z. B. "mobil|stationaer", "" = keine
        public int Id;                              // stabil; Reihenfolge ergibt sich aus der Listenposition
        public string Name = "";
        public string[] Labels = { "F", "S", "N" };
        public int MinLines = 3;                    // Mindesthoehe des Inhaltsfelds in Textzeilen
        public bool HasLkz;                         // Feld "LKZ gemessen?" zwischen Titel und Inhalt

        public RowInfo Clone() { RowInfo r = (RowInfo)MemberwiseClone(); r.Labels = (string[])Labels.Clone(); return r; }
        // Schichtfeld s (0=F, 1=S, 2=N) sichtbar? FS ohne N, Tagesaufgabe nur ein Feld (F = "Erledigt von"); Daten bleiben erhalten.
        public bool HasShift(int s) { return s == 0 || (s == 1 && Mode != ShiftMode.Task) || (s == 2 && Mode == ShiftMode.FSN); }
        public string[] VariantList() { return Variants.Length == 0 ? new string[0] : Variants.Split('|'); }
    }

    public class SearchHit { public DateTime Day; public int RowId; }

    // Ein Block fuer IDataStore.SaveManyWithAudit. Audit null = ohne Protokoll. Saved/Data.Version erst nach erfolgreichem Schreiben.
    public class SaveItem
    {
        public DateTime Day; public int RowId; public Entry Data; public AuditEntry Audit;
        public bool Saved;                  // false = Konflikt (Data.Version veraltet)
    }

    public interface IDataStore
    {
        Entry Get(DateTime day, int rowId);                  // Kopie oder null
        bool Save(DateTime day, int rowId, Entry e);         // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        List<RowInfo> GetRows();                             // Kopien in Anzeigereihenfolge
        void SaveStructure(List<RowInfo> rows);              // Reihenfolge/Namen/Anzahl; Id <= 0 = neue Zeile; keine Zeile wird geloescht
        List<SearchHit> Find(string query);                  // Inhalt und Kuerzel (nicht Raumnamen), nach Datum/Zeile sortiert
        Catalog GetCatalog();                                // Equipment-Positionen und Bausteine fuer Pakete (Kopie)
        void SaveCatalog(Catalog catalog);
        DutyConfig GetDuties();                              // Pflichtpunkte, Rundgaenge, Feiertage (Kopie)
        void SaveDuties(DutyConfig duties);
        List<WeekMark> GetWeekMarks(DateTime monday);        // Kopien
        // Wochenhaken + Protokoll atomar; false = Konflikt (m.Version veraltet); setzt sonst m.Version neu. a darf null sein.
        bool SaveWeekMark(WeekMark m, AuditEntry a);
        List<DeviceStatus> GetStatuses();                    // manuell gesetzte Equipment-Status (Kopien)
        // Status + Protokoll atomar; Cleared = Eintrag entfernen. Letzter Schreiber gewinnt je Nummer. a darf null sein.
        bool SetStatus(DeviceStatus s, AuditEntry a);
        // Historie ohne Obergrenze: alte Wochen und Protokolleintraege werden nie geloescht ("ein Jahr zurueck" und mehr).
        void AppendAudit(AuditEntry a);                      // append-only, kein Aendern/Loeschen
        // Save + Protokolleintrag atomar (beides oder nichts); setzt a.Before/After/Version auf den Stand beim Speichern
        bool SaveWithAudit(DateTime day, int rowId, Entry e, AuditEntry a);
        // Viele Bloecke in EINEM Schreibvorgang, Versionspruefung je Block (Saved je Item); Rueckgabe = Anzahl gespeichert.
        // Exception = nichts gespeichert.
        int SaveManyWithAudit(IList<SaveItem> items);
        List<AuditEntry> GetAudit(DateTime day, int rowId);  // Kopien, chronologisch
        StoreData Snapshot();                                // vollstaendige Kopie aller Daten (Sicherung)
        void Restore(StoreData data);                        // ersetzt Plaene/Struktur/Katalog; Protokoll wird zusammengefuehrt
    }

    public class MemoryStore : IDataStore
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

        // Erfundene Testdaten (Repo ist oeffentlich; keine echten Nummern/Namen). Kuerzel passen zu den Testnutzern.
        public static MemoryStore Sample(DateTime monday)
        {
            MemoryStore s = new MemoryStore();
            int suite1 = s.IdOf("Suite 1"), mix = s.IdOf("Mix / Split"), cc = s.IdOf("Raum CC");
            string[] a = {
                "Lauf 22040030\nTFF: H1-X-0101\nPumpe: H1-P-0101-05\nPT: H1-T-0102\nSkid: H1-X-0103 (H1-F-0103-04)\n\nSplitten\nTank 1: H1-T-0111 (800L)",
                "Probenahme Charge 4711 – Öl-Wechsel? 5 % (ü ö ä ß) @Tank \"A\"!\nH1-P-0101-05, H1-T-0112",
                "Wartung Abfüllung (extern ab 08:00).\nTägliche WFI Probe ziehen\nLKZ MESSEN",
                "Lauf 22040031 (nach Lauf 33070007)\nSkid: H1-X-0103, Tank: H2-T-0201",
                "Reinigung und Rüsten\nH1-T-0113, H2-F-0202-01",
                "Wochenendbetrieb: Überwachung", "Wochenendbetrieb: Überwachung" };
            string[][] p = {
                new[] { "MUMA, WEPA, BEER", "ABCD, EFGH, IJKL", "" }, new[] { "MUMA, WEPA", "BEER, ABCD", "EFGH" }, new[] { "IJKL, MNOP", "", "" },
                new[] { "QRST, UVWX", "YZAB, CDEF", "GÜAL, SÖNß" }, new[] { "KLMN, OPQR", "STUV, WXYZ", "" }, new[] { "ABCD", "EFGH", "IJKL" }, new[] { "ABCD", "EFGH", "IJKL" } };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Content = a[d]; e.People = p[d]; e.Lkz = d == 0 || d == 3;
                if (d == 2)   // Beispiel manuelle Formatierung: rot, fett, groesser (Erinnerung)
                {
                    FormatSpan sp = new FormatSpan(); sp.Start = e.Content.IndexOf("LKZ"); sp.Length = 10; sp.Rgb = 0xC00000; sp.Bold = true; sp.Size = 11;
                    e.Spans.Add(sp);
                }
                if (d == 1)   // Beispiel Notizzeilen (kursiv, zaehlen nicht als Inhalt)
                {
                    e.Content += "\nProbe bis 10:00 zur QC bringen\nBei Abweichung Schichtleiter informieren";
                    int nl = e.Content.Split('\n').Length;
                    Notes.SetNote(e, nl - 2, nl - 1, true);
                }
                s.Save(monday.AddDays(d), suite1, e);
            }
            // Folgewoche (fuer die Testansicht "2 Wochen")
            string[] nxt = { "Rüsten Suite 1, Filter prüfen", "Wartung Pumpe H1-P-0101-05", "Probenahme laut Plan" };
            for (int d = 0; d < nxt.Length; d++)
            {
                Entry e = new Entry(); e.Content = nxt[d]; e.People = new[] { "ABCD, EFGH", "IJKL", "" };
                s.Save(monday.AddDays(7 + d), suite1, e);
            }
            // viele Kuerzel in einer Schicht: Feld waechst, alle Spalten der Zeile bleiben gleich hoch
            Entry m = s.Get(monday, suite1);
            m.People[0] = "MUMA, WEPA, BEER, ABCD, EFGH, IJKL, MNOP, QRST, UVWX, YZAB, CDEF, GHIJ, KLMN, OPQR, STUV, WXYZ";
            s.Save(monday, suite1, m);
            for (int d = 0; d < 5; d++)
            {
                Entry e = new Entry(); e.Content = "Schulung (Raum 2.14), 13:00"; e.People = new[] { "alle", "", "" };
                s.Save(monday.AddDays(d), mix, e);
            }
            Entry c = new Entry(); c.Content = "Raumreinigung"; c.People = new[] { "BEER, WEPA", "", "" };
            s.Save(monday.AddDays(2), cc, c);
            Entry cn = new Entry(); cn.Content = "Schlüssel liegt beim Pförtner"; Notes.SetNote(cn, 0, 0, true);   // Block nur mit Notiz = leer
            s.Save(monday.AddDays(4), cc, cn);
            // Schichtmodus: Raum CC = Tagesaufgabe ("Erledigt von"), Versand = nur Frueh/Spaet
            List<RowInfo> rs = s.GetRows();
            foreach (RowInfo r in rs) { if (r.Name == "Raum CC") r.Mode = ShiftMode.Task; if (r.Name == "Versand") r.Mode = ShiftMode.FS; }
            s.SaveStructure(rs);
            Entry v = new Entry(); v.Content = "Versand Kühlware"; v.People = new[] { "WEPA", "BEER", "" };
            s.Save(monday.AddDays(1), s.IdOf("Versand"), v);
            // Puffer: Herstellung mobil (Tagesprobe automatisch), stationaer mit Tagesprobe, ohne Angabe, beides an einem Tag
            string[][] pf = { new[] { "Herstellung Puffer A", "Puffer A|mobil", "" }, new[] { "Herstellung Puffer B", "Puffer B|stationaer", "PROBE" }, new[] { "Puffer B abfüllen", "", "" },
                              new[] { "Herstellung Puffer C, D und E", "Puffer C|mobil|Puffer D|stationaer|Puffer E|mobil", "PROBE" } };
            for (int d = 0; d < pf.Length; d++)
            {
                Entry e = new Entry(); e.Content = pf[d][0]; e.People = new[] { "WEPA", "", "" };
                string[] bp = pf[d][1].Split('|');
                for (int k = 0; k + 1 < bp.Length; k += 2) BufferSet.Add(e.Buffers, bp[k], bp[k + 1], "");
                if (d == 3) { e.Buffers[0].Text = "Ansatz 500 L\nWFI: H1-T-0102"; e.Buffers[1].Text = "Flasche 5 L"; }
                if (pf[d][2].Length > 0) e.Marks.Add(pf[d][2]);
                s.Save(monday.AddDays(d), s.IdOf("Puffer"), e);
            }
            WeekMark gmp = new WeekMark(); gmp.Monday = monday; gmp.Key = "GMP1"; gmp.Done = true; gmp.At = monday.AddHours(9.2); gmp.By = "MUMA";
            s.SaveWeekMark(gmp, null);
            return s;
        }
    }
}
