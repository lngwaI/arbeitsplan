using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    // Vorgemerktes Geraet (N5): ein Nutzer ohne Recht PackageAdmin hat im Paket-Editor eine Nummer eingetragen, die nicht in der Equipment-Liste steht.
    // Liegt dauerhaft im Katalog (Stammdaten, XML "Vormerkung"); der Admin nimmt es auf oder verwirft es.
    public class EquipPending
    {
        public string Id = "", Short = "", By = "";   // Nummer, Position (Kurzform), Login des Nutzers
        public DateTime At;
        public EquipPending Clone() { return (EquipPending)MemberwiseClone(); }
    }

    public partial class EquipMaster
    {
        public List<EquipPending> Pending = new List<EquipPending>();

        // Nach dem Schema H<1|2>-<T|F|X|P>-<Ziffern>: T = Tank, F = Filter, X = Skid, P = Pumpe
        static readonly Regex IdSchema = new Regex(@"^H[12]-([TFXP])-\d{2,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public EquipPending PendingFor(string id)
        {
            foreach (EquipPending p in Pending) if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        // false = nichts getan (leer, schon in der Liste oder schon vorgemerkt)
        public bool AddPending(string id, string shortName, string by, DateTime at)
        {
            id = (id ?? "").Trim();
            if (id.Length == 0 || Device(id) != null || PendingFor(id) != null) return false;
            EquipPending p = new EquipPending(); p.Id = id; p.Short = shortName ?? ""; p.By = by ?? ""; p.At = at;
            Pending.Add(p);
            return true;
        }

        public bool RemovePending(string id) { return Pending.RemoveAll(delegate(EquipPending p) { return string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase); }) > 0; }

        // Haeufigste Geraeteart der Geraete, die match erfuellen (nur bekannte Arten); "" = keine
        string Majority(Predicate<EquipDevice> match)
        {
            Dictionary<string, int> n = new Dictionary<string, int>(); string best = ""; int max = 0;
            foreach (EquipDevice d in Devices)
            {
                if (d.Class.Length == 0 || Class(d.Class) == null || !match(d)) continue;
                int c; n.TryGetValue(d.Class, out c); n[d.Class] = ++c;
                if (c > max) { max = c; best = d.Class; }
            }
            return best;
        }

        // Geraeteart fuer eine neue Nummer raten: erst die Art der vorhandenen Geraete mit demselben Typbuchstaben (T/F/X/P), dann eine Art, deren Name
        // zum Buchstaben passt (Tank/Filter/Skid/Pump), dann die Art der Geraete der Position; sonst "" (keine)
        public string GuessClass(string id, EquipPosition pos)
        {
            Match m = IdSchema.Match(id ?? "");
            if (m.Success)
            {
                char t = char.ToUpperInvariant(m.Groups[1].Value[0]);
                string c = Majority(delegate(EquipDevice d) { Match x = IdSchema.Match(d.Id); return x.Success && char.ToUpperInvariant(x.Groups[1].Value[0]) == t; });
                if (c.Length > 0) return c;
                string key = t == 'T' ? "Tank" : t == 'F' ? "Filter" : t == 'X' ? "Skid" : "Pump";
                foreach (EquipClass k in Classes) if (k.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return k.Name;
            }
            return pos != null ? Majority(delegate(EquipDevice d) { return pos.Ids.Contains(d.Id); }) : "";
        }

        // Neues Geraet (noch nicht eingetragen) mit geratener Art
        public EquipDevice NewDevice(string id, EquipPosition pos)
        {
            EquipDevice d = new EquipDevice(); d.Id = id.Trim(); d.Class = GuessClass(d.Id, pos);
            return d;
        }
    }
}
