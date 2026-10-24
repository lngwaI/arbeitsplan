using System;
using System.Collections.Generic;
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
        public string Font;                 // null = Standardschrift

        public FormatSpan Clone() { return (FormatSpan)MemberwiseClone(); }
        public string Key() { return Start + "," + Length + "," + Rgb + "," + Size + "," + Bold + "," + Italic + "," + Font + ";"; }
    }

    public class Entry   // Inhalt eines Blocks (Tag x Zeile)
    {
        public string Content = "";
        public List<FormatSpan> Spans = new List<FormatSpan>();
        public string[] People = { "", "", "" };   // Kuerzel je Schicht (F, S, N)
        public bool Lkz;                            // "LKZ gemessen?" (nur Zeilen mit HasLkz)
        public int Version;                         // Konflikterkennung

        public Entry Clone()
        {
            Entry e = (Entry)MemberwiseClone();
            e.People = (string[])People.Clone();
            e.Spans = new List<FormatSpan>();
            foreach (FormatSpan s in Spans) e.Spans.Add(s.Clone());
            return e;
        }
        public bool IsEmpty { get { return Content.Length == 0 && People[0].Length == 0 && People[1].Length == 0 && People[2].Length == 0; } }
        public string SpanKey() { string k = ""; foreach (FormatSpan s in Spans) k += s.Key(); return k; }
    }

    public class RowInfo   // gilt fuer die ganze Zeile (alle Tage); wird nur ueber die Blockstruktur-Einstellungen geaendert
    {
        public int Id;                              // stabil; Reihenfolge ergibt sich aus der Listenposition
        public string Name = "";
        public string[] Labels = { "F", "S", "N" };
        public int MinLines = 3;                    // Mindesthoehe des Inhaltsfelds in Textzeilen
        public bool HasLkz;                         // Feld "LKZ gemessen?" zwischen Titel und Inhalt

        public RowInfo Clone() { RowInfo r = (RowInfo)MemberwiseClone(); r.Labels = (string[])Labels.Clone(); return r; }
    }

    public class SearchHit { public DateTime Day; public int RowId; }

    public interface IDataStore
    {
        Entry Get(DateTime day, int rowId);                  // Kopie oder null
        bool Save(DateTime day, int rowId, Entry e);         // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        List<RowInfo> GetRows();                             // Kopien in Anzeigereihenfolge
        void SaveStructure(List<RowInfo> rows);              // Reihenfolge/Namen/Anzahl; Id <= 0 = neue Zeile; keine Zeile wird geloescht
        List<SearchHit> Find(string query);                  // Inhalt und Kuerzel (nicht Raumnamen), nach Datum/Zeile sortiert
        Catalog GetCatalog();                                // Equipment-Positionen und Bausteine fuer Pakete (Kopie)
        void SaveCatalog(Catalog catalog);
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
                rows.Add(r);
            }
        }

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }
        void CheckWrite() { if (FailSaves) throw new IOException("Kein Schreibzugriff (simulierter System-PC)."); }
        int IdOf(string name) { foreach (RowInfo r in rows) if (r.Name == name) return r.Id; return -1; }

        Catalog catalog = Catalog.Default();
        public Catalog GetCatalog() { return catalog.Clone(); }
        public void SaveCatalog(Catalog c) { CheckWrite(); catalog = c.Clone(); }

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
            e.Version = (cur == null ? 0 : cur.Version) + 1;
            items[k] = e.Clone();
            return true;
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
                "Lauf 100001\nTFF: H1-X-0101\nPumpe: H1-P-0101-05\nPT: H1-T-0102\nSkid: H1-X-0103 (H1-F-0103-04)\n\nSplitten\nTank 1: H1-T-0111 (800L)",
                "Probenahme Charge 4711 – Öl-Wechsel? 5 % (ü ö ä ß) @Tank \"A\"!\nH1-P-0101-05, H1-T-0112",
                "Wartung Abfüllung (extern ab 08:00).\nTägliche WFI Probe ziehen\nLKZ MESSEN",
                "Lauf 100002\nSkid: H1-X-0103, Tank: H2-T-0201",
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
                s.Save(monday.AddDays(d), suite1, e);
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
            return s;
        }
    }
}
