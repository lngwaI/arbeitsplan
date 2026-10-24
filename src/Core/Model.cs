using System;
using System.Collections.Generic;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
// Zeilenumbrueche in Texten immer "\n".
namespace Arbeitsplan.Core
{
    public class ColorSpan { public int Start, Length, Rgb; }   // manuelle Schriftfarbe (0xRRGGBB) im Inhaltstext

    public class Entry   // Inhalt eines Blocks (Tag x Zeile)
    {
        public string Content = "";
        public List<ColorSpan> Spans = new List<ColorSpan>();
        public string[] People = { "", "", "" };   // Kuerzel je Schicht (F, S, N)
        public int Version;                         // Konflikterkennung

        public Entry Clone()
        {
            Entry e = (Entry)MemberwiseClone();
            e.People = (string[])People.Clone();
            e.Spans = new List<ColorSpan>();
            foreach (ColorSpan s in Spans) { ColorSpan c = new ColorSpan(); c.Start = s.Start; c.Length = s.Length; c.Rgb = s.Rgb; e.Spans.Add(c); }
            return e;
        }
        public bool IsEmpty { get { return Content.Length == 0 && People[0].Length == 0 && People[1].Length == 0 && People[2].Length == 0; } }
        public string SpanKey() { string k = ""; foreach (ColorSpan s in Spans) k += s.Start + "," + s.Length + "," + s.Rgb + ";"; return k; }
    }

    public class RowInfo   // gilt fuer die ganze Zeile (alle Tage): Blockname, Schichtbezeichnung, Mindesthoehe
    {
        public int Id;                              // stabil; Reihenfolge ergibt sich aus der Listenposition
        public string Name = "";
        public string[] Labels = { "F", "S", "N" };
        public int MinLines = 3;                    // Mindesthoehe des Inhaltsfelds in Textzeilen

        public RowInfo Clone() { RowInfo r = (RowInfo)MemberwiseClone(); r.Labels = (string[])Labels.Clone(); return r; }
        public bool SameAs(RowInfo o)
        {
            return Name == o.Name && MinLines == o.MinLines && Labels[0] == o.Labels[0] && Labels[1] == o.Labels[1] && Labels[2] == o.Labels[2];
        }
    }

    public class SearchHit { public DateTime Day; public int RowId; }

    public interface IDataStore
    {
        Entry Get(DateTime day, int rowId);                  // Kopie oder null
        bool Save(DateTime day, int rowId, Entry e);         // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        List<RowInfo> GetRows();                             // Kopien in Anzeigereihenfolge
        void SaveRow(RowInfo info);                          // Exception = nicht speicherbar
        void AddRow();
        void MoveRow(int rowId, int delta);                  // -1 = nach oben, +1 = nach unten
        List<SearchHit> Find(string query);                  // Inhalt und Kuerzel (nicht Raumnamen), nach Datum/Zeile sortiert
    }

    public class MemoryStore : IDataStore
    {
        public static readonly string[] DefaultRows = { "Suite 1", "Mix / Split", "Suite 2", "Puffer", "Freeze / Thaw", "Einwaage", "CIP / SIP", "Raum CC", "", "" };

        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();
        readonly List<RowInfo> rows = new List<RowInfo>();
        int nextId = 1;
        public bool FailSaves;                               // Test: simuliert PC ohne Schreibzugriff (System-PC)

        public MemoryStore()
        {
            foreach (string n in DefaultRows) { RowInfo r = new RowInfo(); r.Id = nextId++; r.Name = n; rows.Add(r); }
        }

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }
        void CheckWrite() { if (FailSaves) throw new IOException("Kein Schreibzugriff (simulierter System-PC)."); }

        public List<RowInfo> GetRows() { List<RowInfo> l = new List<RowInfo>(); foreach (RowInfo r in rows) l.Add(r.Clone()); return l; }
        public void AddRow() { CheckWrite(); RowInfo r = new RowInfo(); r.Id = nextId++; rows.Add(r); }

        public void SaveRow(RowInfo info)
        {
            CheckWrite();
            for (int i = 0; i < rows.Count; i++) if (rows[i].Id == info.Id) rows[i] = info.Clone();
        }

        public void MoveRow(int rowId, int delta)
        {
            CheckWrite();
            int i = rows.FindIndex(delegate(RowInfo r) { return r.Id == rowId; });
            int j = i + delta;
            if (i < 0 || j < 0 || j >= rows.Count) return;
            RowInfo t = rows[i]; rows[i] = rows[j]; rows[j] = t;
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
            string[] a = {
                "Lauf 100001\nTFF: H1-X-0101\nPumpe: H1-P-0101-05\nPT: H1-T-0102\nSkid: H1-X-0103 (H1-F-0103-04)\n\nSplitten\nTank 1: H1-T-0111 (800L)",
                "Probenahme Charge 4711\nH1-P-0101-05 in Prozess, H1-T-0112 gesperrt",
                "Wartung Abfuellung (extern ab 08:00).\nTaegliche WFI Probe ziehen\nLKZ MESSEN",
                "Lauf 100002\nSkid: H1-X-0103, Tank: H2-T-0201",
                "Reinigung und Ruesten\nH1-T-0113, H2-F-0202-01",
                "Wochenendbetrieb: Ueberwachung", "Wochenendbetrieb: Ueberwachung" };
            string[][] p = {
                new[] { "MUMA, WEPA, BEER", "ABCD, EFGH, IJKL", "" }, new[] { "MUMA, WEPA", "BEER, ABCD", "EFGH" }, new[] { "IJKL, MNOP", "", "" },
                new[] { "QRST, UVWX", "YZAB, CDEF", "GHIJ" }, new[] { "KLMN, OPQR", "STUV, WXYZ", "" }, new[] { "ABCD", "EFGH", "IJKL" }, new[] { "ABCD", "EFGH", "IJKL" } };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Content = a[d]; e.People = p[d];
                if (d == 2)   // Beispiel manuelle Schriftfarbe (rot): Erinnerung
                {
                    ColorSpan sp = new ColorSpan(); sp.Start = e.Content.IndexOf("LKZ"); sp.Length = 10; sp.Rgb = 0xC00000; e.Spans.Add(sp);
                }
                s.Save(monday.AddDays(d), 1, e);
            }
            // viele Kuerzel in einer Schicht: Feld waechst, alle Spalten der Zeile bleiben gleich hoch
            Entry m = s.Get(monday, 1);
            m.People[0] = "MUMA, WEPA, BEER, ABCD, EFGH, IJKL, MNOP, QRST, UVWX, YZAB, CDEF, GHIJ, KLMN, OPQR, STUV, WXYZ";
            s.Save(monday, 1, m);
            for (int d = 0; d < 5; d++)
            {
                Entry e = new Entry(); e.Content = "Schulung (Raum 2.14), 13:00"; e.People = new[] { "alle", "", "" };
                s.Save(monday.AddDays(d), 2, e);
            }
            Entry cc = new Entry(); cc.Content = "Raumreinigung"; cc.People = new[] { "BEER, WEPA", "", "" };
            s.Save(monday.AddDays(2), 8, cc);
            return s;
        }
    }
}
