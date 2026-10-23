using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    public enum EquipStatus
    {
        NichtGereinigt, NichtGereinigtOhneTimer, Gereinigt, Regeneriert, Sterilisiert, InProzess, Freigabe, Quarantaene
    }

    public class EquipmentInfo
    {
        public string Number;
        public EquipStatus Status;
        public DateTime Timestamp;
        public EquipmentInfo(string number, EquipStatus status, DateTime ts) { Number = number; Status = status; Timestamp = ts; }
    }

    // Statusquelle. Spaeter Anbindung an ein MES-System; Format/Schnittstelle noch unbekannt.
    public interface IEquipmentStatusProvider
    {
        EquipmentInfo Get(string number);   // null = Nummer unbekannt
    }

    // Testdaten, bis die echte Quelle existiert.
    public class MockEquipmentProvider : IEquipmentStatusProvider
    {
        readonly Dictionary<string, EquipmentInfo> table = new Dictionary<string, EquipmentInfo>();

        public MockEquipmentProvider()
        {
            Add("G2-F-1234-30", EquipStatus.Sterilisiert);
            Add("G2-F-1900-12", EquipStatus.Gereinigt);
            Add("G2-F-1750-02", EquipStatus.InProzess);
            Add("G2-F-2010-01", EquipStatus.Freigabe);
            Add("G2-F-1888-05", EquipStatus.Quarantaene);
            Add("G2-F-1777-11", EquipStatus.Regeneriert);
            Add("G2-F-1666-09", EquipStatus.NichtGereinigt);
            Add("G2-F-1555-03", EquipStatus.NichtGereinigtOhneTimer);
        }

        void Add(string n, EquipStatus s) { table[n] = new EquipmentInfo(n, s, DateTime.Now); }

        public EquipmentInfo Get(string number)
        {
            EquipmentInfo i;
            return table.TryGetValue(number, out i) ? i : null;
        }
    }

    public static class EquipmentMatcher
    {
        // Annahme: Nummer wie "G2-F-1234-30" (2 Zeichen, 1-2 Buchstaben, 3-5 Ziffern, 2 Ziffern).
        public static readonly Regex Pattern = new Regex(@"\b[A-Z0-9]{2}-[A-Z]{1,2}-\d{3,5}-\d{2}\b", RegexOptions.Compiled);
    }

    // Status -> Farbe (0xRRGGBB), ueberschreibbar per statuscolors.ini ("Gereinigt=#7030A0").
    public class StatusColors
    {
        readonly Dictionary<EquipStatus, int> map = new Dictionary<EquipStatus, int>();

        public StatusColors()
        {
            map[EquipStatus.NichtGereinigt] = 0x000000;
            map[EquipStatus.NichtGereinigtOhneTimer] = 0x000000;
            map[EquipStatus.Gereinigt] = 0x7030A0;      // violett
            map[EquipStatus.Regeneriert] = 0x000080;    // dunkelblau
            map[EquipStatus.Sterilisiert] = 0xFF0000;   // rot
            map[EquipStatus.InProzess] = 0x00A000;      // gruen
            map[EquipStatus.Freigabe] = 0x3EA9F5;       // hellblau
            map[EquipStatus.Quarantaene] = 0x8B0000;    // dunkelrot
        }

        public int Get(EquipStatus s) { return map[s]; }

        public static StatusColors Load(string path)
        {
            StatusColors c = new StatusColors();
            if (!File.Exists(path)) return c;
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq < 1 || line.TrimStart().StartsWith(";")) continue;
                try
                {
                    EquipStatus s = (EquipStatus)Enum.Parse(typeof(EquipStatus), line.Substring(0, eq).Trim(), true);
                    c.map[s] = int.Parse(line.Substring(eq + 1).Trim().TrimStart('#'), NumberStyles.HexNumber);
                }
                catch (Exception) { /* fehlerhafte Zeile ignorieren */ }
            }
            return c;
        }
    }
}
