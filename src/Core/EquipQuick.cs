using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    public enum QuickState { New, Exists, Duplicate, Invalid }

    // Eine Zeile der Schnellerfassung: ID, Notiz, vorgeschlagene Geraeteart ("" = nicht zugeordnet) und Zustand.
    public class QuickRow
    {
        public string Id = "", Note = "", Class = "";
        public QuickState State = QuickState.New;
        public string Why = "";
    }

    // Schnellerfassung (viele IDs aus Text/Excel), "Nicht zugeordnet", "Passt zu bequemer" (Mehrfachauswahl, Vorschlag nach Gruppe).
    public partial class EquipMaster
    {
        static readonly Regex Schema = new Regex(@"^H\d-([A-Za-z])-\d", RegexOptions.Compiled);

        // Zeilen "ID", "ID<Tab>Notiz" oder "ID;Notiz" (Excel-Einfuegen). Leere Zeilen werden uebergangen.
        public List<QuickRow> ParseQuick(string text)
        {
            List<QuickRow> rows = new List<QuickRow>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                if (raw.Trim().Length == 0) continue;
                string[] part = raw.IndexOf('\t') >= 0 ? raw.Split(new[] { '\t' }, 2) : raw.Split(new[] { ';' }, 2);
                QuickRow r = new QuickRow();
                r.Id = part[0].Trim().Trim('"').Trim();
                if (part.Length > 1) r.Note = part[1].Trim().Trim('"').Trim();
                if (r.Id.Length == 0 || r.Id.Length > 40 || r.Id.IndexOfAny(new[] { ' ', '\t', '|', '<', '>' }) >= 0)
                { r.State = QuickState.Invalid; r.Why = "ungültig (Leerzeichen/zu lang)"; }
                else if (Device(r.Id) != null) { r.State = QuickState.Exists; r.Why = "vorhanden, übersprungen"; r.Class = Device(r.Id).Class; }
                else if (!seen.Add(r.Id)) { r.State = QuickState.Duplicate; r.Why = "doppelt, übersprungen"; }
                else r.Class = SuggestClass(r.Id);
                rows.Add(r);
            }
            return rows;
        }

        // Kennbuchstabe der ID nach Schema H<n>-<Buchstabe>-<Nummer> (T/F/X/P), sonst ""
        public static string LetterOf(string id)
        {
            Match mt = Schema.Match(id ?? "");
            return mt.Success ? mt.Groups[1].Value.ToUpperInvariant() : "";
        }

        // Vorschlag fuer die Geraeteart: zuerst wie bereits angelegte Geraete mit gleichem Kennbuchstaben, sonst Standardname, falls es die Art gibt.
        public string SuggestClass(string id)
        {
            string l = LetterOf(id);
            if (l.Length == 0) return "";
            Dictionary<string, int> count = new Dictionary<string, int>();
            string best = ""; int bn = 0;
            foreach (EquipDevice d in Devices)
            {
                if (d.Class.Length == 0 || Class(d.Class) == null || LetterOf(d.Id) != l) continue;
                int n; count.TryGetValue(d.Class, out n); count[d.Class] = ++n;
                if (n > bn) { bn = n; best = d.Class; }
            }
            if (best.Length > 0) return best;
            string def = l == "T" ? "Tank" : l == "F" ? "Filtergehäuse" : l == "X" ? "Skid" : l == "P" ? "Pumpe" : "";
            return def.Length > 0 && Class(def) != null ? def : "";
        }

        // Legt alle neuen Zeilen als Geraete an (Art unbekannt/leer = nicht zugeordnet; fehlende Art wird angelegt). Rueckgabe: Anzahl.
        public int AddQuick(List<QuickRow> rows)
        {
            int n = 0;
            foreach (QuickRow r in rows)
            {
                if (r.State != QuickState.New || Device(r.Id) != null) continue;
                if (r.Class.Length > 0 && Class(r.Class) == null) { EquipClass c = new EquipClass(); c.Name = r.Class; Classes.Add(c); }
                EquipDevice d = new EquipDevice(); d.Id = r.Id; d.Class = r.Class; d.Note = r.Note;
                Devices.Add(d); n++;
            }
            return n;
        }

        // ---------- Nicht zugeordnet ----------

        public bool IsUnassigned(EquipDevice d) { return d.Class.Length == 0 || Class(d.Class) == null; }

        public List<EquipDevice> Unassigned()
        {
            return Devices.FindAll(delegate(EquipDevice d) { return IsUnassigned(d); });
        }

        // Weist die Geraete (nach ID) der Art zu; fehlende Art wird angelegt. Rueckgabe: Anzahl geaendert.
        public int AssignClass(IEnumerable<string> ids, string cls)
        {
            if (cls.Length > 0 && Class(cls) == null) { EquipClass c = new EquipClass(); c.Name = cls; Classes.Add(c); }
            int n = 0;
            foreach (string id in ids) { EquipDevice d = Device(id); if (d != null && d.Class != cls) { d.Class = cls; n++; } }
            return n;
        }

        // ---------- Passt zu: Mehrfachauswahl und Vorschlag ----------

        // Das Paar steht direkt als Geraete-ID-Paar in den Regeln (nicht nur ueber Art/Gruppe)
        public bool HasDirectPair(string a, string b) { bool sw; return PairLink(a, b, out sw) != null; }

        // Setzt die direkten Paare von id so, dass genau "want" zusaetzlich zu den Regeln gilt. Rueckgabe: (neu, entfernt).
        public void SetFits(string id, ICollection<string> want, out int added, out int removed)
        {
            added = removed = 0;
            foreach (EquipDevice d in Devices)
            {
                if (Same(d.Id, id)) continue;
                bool w = false; foreach (string x in want) if (Same(x, d.Id)) w = true;
                bool has = HasPair(id, d.Id), direct = HasDirectPair(id, d.Id);
                if (w && !has) { SetPair(id, d.Id, true); added++; }
                else if (!w && direct) { SetPair(id, d.Id, false); removed++; }
            }
        }

        static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        // Vorschlag: Geraete, die in einer Gruppe mit einem bereits passenden Geraet stehen, aber selbst noch nicht passen.
        public List<string> SuggestFits(string id)
        {
            List<string> have = CompatibleWith(id), r = new List<string>();
            foreach (string h in have)
                foreach (EquipSeries s in SeriesOf(h))
                    foreach (EquipDevice d in Members(s))
                        if (!Same(d.Id, id) && !Has(have, d.Id) && !Has(r, d.Id)) r.Add(d.Id);
            return r;
        }
    }
}
