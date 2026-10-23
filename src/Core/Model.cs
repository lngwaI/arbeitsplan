using System;
using System.Collections.Generic;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
namespace Arbeitsplan.Core
{
    // Eine Zelle (Tag x Zeile). Heading == null: Standardueberschrift (Datum in Zeile 0).
    public class Entry
    {
        public string Heading;
        public string Content = "";
        public string Assign = "";
        public int Version;   // fuer Konflikterkennung (Anforderung 4)

        public Entry Clone() { return (Entry)MemberwiseClone(); }
    }

    public interface IDataStore
    {
        Entry Get(DateTime day, int row);                    // Kopie oder null
        bool Save(DateTime day, int row, Entry e);           // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu
        int MaxRow(DateTime day);                            // hoechste belegte Zeile, -1 wenn leer
    }

    // Schnittstelle fuer spaeteren Equipment-Status (Anforderung 7). Zunaechst leer.
    public interface IEquipmentStatus
    {
        string GetStatus(string equipmentId);                // null = unbekannt
    }

    public class NoEquipmentStatus : IEquipmentStatus
    {
        public string GetStatus(string equipmentId) { return null; }
    }

    public class MemoryStore : IDataStore
    {
        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }

        public Entry Get(DateTime day, int row)
        {
            Entry e;
            return items.TryGetValue(Key(day, row), out e) ? e.Clone() : null;
        }

        public bool Save(DateTime day, int row, Entry e)
        {
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
            foreach (string k in items.Keys)
                if (k.StartsWith(day.ToString("yyyy-MM-dd") + "|"))
                {
                    Entry e = items[k];
                    if (e.Content.Length > 0 || e.Assign.Length > 0 || e.Heading != null)
                        max = Math.Max(max, int.Parse(k.Substring(11)));
                }
            return max;
        }

        // Erfundene Testdaten (keine echten Namen/Produkte; Repo ist oeffentlich).
        public static MemoryStore Sample(DateTime monday)
        {
            MemoryStore s = new MemoryStore();
            string[] content = {
                "Raumreinigung im Laufe des Tages.\r\nProdukt XY Lauf Z.\r\nG2-F-1234-30, G2-F-1900-12",
                "Probenahme Charge 4711.\r\nEquipment: G2-F-1750-02",
                "Wartung Abfuellung (extern ab 08:00).",
                "Produkt AB Lauf 3.\r\nG2-F-2010-01",
                "Reinigung und Ruesten.",
                "Wochenendbetrieb: Ueberwachung.",
                "Wochenendbetrieb: Ueberwachung."
            };
            string[] assign = {
                "F: abc def ghi\r\nS: jkl mno pqr\r\nN: ", "F: abc def\r\nS: ghi jkl\r\nN: mno",
                "F: pqr stu\r\nS: \r\nN: ", "F: vwx yza\r\nS: bcd efg\r\nN: hij",
                "F: klm nop\r\nS: qrs tuv\r\nN: ", "F: wxy\r\nS: zab\r\nN: cde", "F: wxy\r\nS: zab\r\nN: cde"
            };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Content = content[d]; e.Assign = assign[d];
                s.Save(monday.AddDays(d), 0, e);
            }
            Entry z = new Entry(); z.Heading = "Zusatz"; z.Content = "Schulung (Raum 2.14), 13:00"; z.Assign = "F: alle";
            s.Save(monday, 1, z);
            return s;
        }
    }
}
