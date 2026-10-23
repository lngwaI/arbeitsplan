using System;
using System.Collections.Generic;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
namespace Arbeitsplan.Core
{
    // Ein Block (Tag x Zeile). Heading = Raumbezeichnung.
    public class Entry
    {
        public string Heading;
        public string Content = "";
        public string Assign = "";
        public int Version;   // Konflikterkennung

        public Entry Clone() { return (Entry)MemberwiseClone(); }
        public bool IsEmpty { get { return string.IsNullOrEmpty(Heading) && Content.Length == 0 && Assign.Length == 0; } }
    }

    public interface IDataStore
    {
        Entry Get(DateTime day, int row);                    // Kopie oder null
        bool Save(DateTime day, int row, Entry e);           // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        int MaxRow(DateTime day);                            // hoechste belegte Zeile, -1 wenn leer
    }

    public class MemoryStore : IDataStore
    {
        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();
        public bool FailSaves;                               // Test: simuliert PC ohne Schreibzugriff (System-PC)

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }

        public Entry Get(DateTime day, int row)
        {
            Entry e;
            return items.TryGetValue(Key(day, row), out e) ? e.Clone() : null;
        }

        public bool Save(DateTime day, int row, Entry e)
        {
            if (FailSaves) throw new IOException("Kein Schreibzugriff (simulierter System-PC).");
            string k = Key(day, row);
            Entry cur;
            items.TryGetValue(k, out cur);
            if (cur != null && cur.Version != e.Version) return false;
            e.Version = (cur == null ? 0 : cur.Version) + 1;
            items[k] = e.Clone();
            return true;
        }

        public int MaxRow(DateTime day)
        {
            int max = -1;
            string prefix = day.ToString("yyyy-MM-dd") + "|";
            foreach (KeyValuePair<string, Entry> kv in items)
                if (kv.Key.StartsWith(prefix) && !kv.Value.IsEmpty)
                    max = Math.Max(max, int.Parse(kv.Key.Substring(prefix.Length)));
            return max;
        }

        // Erfundene Testdaten (Repo ist oeffentlich).
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
            string[] b = { "F: abc def ghi\r\nS: jkl mno pqr\r\nN: ", "F: abc def\r\nS: ghi jkl\r\nN: mno",
                "F: pqr stu\r\nS: \r\nN: ", "F: vwx yza\r\nS: bcd efg\r\nN: hij",
                "F: klm nop\r\nS: qrs tuv\r\nN: ", "F: wxy\r\nS: zab\r\nN: cde", "F: wxy\r\nS: zab\r\nN: cde" };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Heading = "Raum A"; e.Content = a[d]; e.Assign = b[d];
                s.Save(monday.AddDays(d), 0, e);
            }
            // viele Kuerzel in einer Schicht: Einteilungsfeld muss mitwachsen
            Entry m = s.Get(monday, 0);
            m.Assign = "F: abc def ghi jkl mno pqr stu vwx yza bcd efg hij klm nop qrs tuv wxy zab cde fgh ijk lmn opq rst uvw xyz\r\nS: jkl mno pqr\r\nN: ";
            s.Save(monday, 0, m);
            for (int d = 0; d < 5; d++)
            {
                Entry e = new Entry(); e.Heading = "Raum B"; e.Content = "Schulung (Raum 2.14), 13:00"; e.Assign = "F: alle\r\nS: \r\nN: ";
                s.Save(monday.AddDays(d), 1, e);
            }
            return s;
        }
    }
}
