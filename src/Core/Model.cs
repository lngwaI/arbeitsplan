using System;
using System.Collections.Generic;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
namespace Arbeitsplan.Core
{
    public class Entry   // Inhalt eines Blocks (Tag x Zeile)
    {
        public string Content = "";
        public string[] People = { "", "", "" };   // Kuerzel je Schicht (F, S, N)
        public int Version;                         // Konflikterkennung

        public Entry Clone() { Entry e = (Entry)MemberwiseClone(); e.People = (string[])People.Clone(); return e; }
        public bool IsEmpty { get { return Content.Length == 0 && People[0].Length == 0 && People[1].Length == 0 && People[2].Length == 0; } }
    }

    public class RowInfo   // gilt fuer die ganze Zeile (alle Tage): Blockname, Schichtbezeichnung, Mindesthoehe
    {
        public string Name = "";
        public string[] Labels = { "F", "S", "N" };
        public int MinLines = 3;                    // Mindesthoehe des Inhaltsfelds in Zeilen

        public RowInfo Clone() { RowInfo r = (RowInfo)MemberwiseClone(); r.Labels = (string[])Labels.Clone(); return r; }
        public bool SameAs(RowInfo o)
        {
            return Name == o.Name && MinLines == o.MinLines && Labels[0] == o.Labels[0] && Labels[1] == o.Labels[1] && Labels[2] == o.Labels[2];
        }
    }

    public interface IDataStore
    {
        Entry Get(DateTime day, int row);                    // Kopie oder null
        bool Save(DateTime day, int row, Entry e);           // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        int RowCount { get; }
        RowInfo GetRow(int row);                             // Kopie
        void SaveRow(int row, RowInfo info);                 // Exception = nicht speicherbar
        void AddRow();
    }

    public class MemoryStore : IDataStore
    {
        public static readonly string[] DefaultRows = { "Suite 1", "Mix / Split", "Suite 2", "Puffer", "Freeze / Thaw", "Einwaage", "CIP / SIP", "Raum CC", "", "" };

        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();
        readonly List<RowInfo> rows = new List<RowInfo>();
        public bool FailSaves;                               // Test: simuliert PC ohne Schreibzugriff (System-PC)

        public MemoryStore()
        {
            foreach (string n in DefaultRows) { RowInfo r = new RowInfo(); r.Name = n; rows.Add(r); }
        }

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }

        public int RowCount { get { return rows.Count; } }
        public RowInfo GetRow(int row) { return rows[row].Clone(); }
        public void AddRow() { CheckWrite(); rows.Add(new RowInfo()); }
        public void SaveRow(int row, RowInfo info) { CheckWrite(); rows[row] = info.Clone(); }

        void CheckWrite() { if (FailSaves) throw new IOException("Kein Schreibzugriff (simulierter System-PC)."); }

        public Entry Get(DateTime day, int row)
        {
            Entry e;
            return items.TryGetValue(Key(day, row), out e) ? e.Clone() : null;
        }

        public bool Save(DateTime day, int row, Entry e)
        {
            CheckWrite();
            string k = Key(day, row);
            Entry cur;
            items.TryGetValue(k, out cur);
            if (cur != null && cur.Version != e.Version) return false;
            e.Version = (cur == null ? 0 : cur.Version) + 1;
            items[k] = e.Clone();
            return true;
        }

        // Erfundene Testdaten (Repo ist oeffentlich). Kuerzel passen zu den Testnutzern (abc, def, ghi).
        public static MemoryStore Sample(DateTime monday)
        {
            MemoryStore s = new MemoryStore();
            string[] a = {
                "Raumreinigung im Laufe des Tages.\r\nProdukt XY Lauf Z.\r\nG2-F-1234-30, G2-F-1900-12",
                "Probenahme Charge 4711.\r\nG2-F-1750-02 in Prozess, G2-F-1888-05 gesperrt",
                "Wartung Abfuellung (extern ab 08:00).",
                "Produkt AB Lauf 3.\r\nG2-F-2010-01, G2-F-1777-11",
                "Reinigung und Ruesten.\r\nG2-F-1666-09, G2-F-1555-03",
                "Wochenendbetrieb: Ueberwachung.", "Wochenendbetrieb: Ueberwachung." };
            string[][] p = {
                new[] { "abc def ghi", "jkl mno pqr", "" }, new[] { "abc def", "ghi jkl", "mno" }, new[] { "pqr stu", "", "" },
                new[] { "vwx yza", "bcd efg", "hij" }, new[] { "klm nop", "qrs tuv", "" }, new[] { "wxy", "zab", "cde" }, new[] { "wxy", "zab", "cde" } };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Content = a[d]; e.People = p[d];
                s.Save(monday.AddDays(d), 0, e);
            }
            // viele Kuerzel in einer Schicht: Schichtfeld waechst, alle Spalten der Zeile bleiben gleich hoch
            Entry m = s.Get(monday, 0);
            m.People[0] = "abc def ghi jkl mno pqr stu vwx yza bcd efg hij klm nop qrs tuv wxy zab cde fgh ijk lmn opq rst uvw xyz";
            s.Save(monday, 0, m);
            for (int d = 0; d < 5; d++)
            {
                Entry e = new Entry(); e.Content = "Schulung (Raum 2.14), 13:00"; e.People = new[] { "alle", "", "" };
                s.Save(monday.AddDays(d), 1, e);
            }
            Entry cc = new Entry(); cc.Content = "Raumreinigung"; cc.People = new[] { "ghi", "def", "" };
            s.Save(monday.AddDays(2), 7, cc);
            return s;
        }
    }
}
