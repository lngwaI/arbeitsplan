using System;
using System.Collections.Generic;

// Von anderen PCs geaenderte Bloecke kurz hervorheben (nur Ansicht, nichts wird gespeichert). Rein, testbar ("jetzt" wird uebergeben).
namespace Arbeitsplan.Core
{
    public class ChangedBlock
    {
        public DateTime Day;
        public int RowId, Version;
        public string Kuerzel = "";
        public DateTime Since;
    }

    public static class ChangeHighlight
    {
        public const int Seconds = 20;   // Dauer der Hervorhebung in Sekunden (hier aendern)
        static readonly string[] DayNames = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };

        // shown = angezeigter Stand (Tag, Zeile, Version) vor dem Nachladen. Geaendert = Version im Bestand anders.
        // Ausgenommen: letzter Protokolleintrag vom eigenen Login (eigene Speicherung, auch von einem anderen PC).
        public static List<ChangedBlock> Find(List<ChangedBlock> shown, IDataStore s, string myLogin, DateTime now)
        {
            List<ChangedBlock> r = new List<ChangedBlock>();
            foreach (ChangedBlock b in shown)
            {
                Entry e = s.Get(b.Day, b.RowId);
                int v = e == null ? 0 : e.Version;
                if (v == b.Version) continue;
                List<AuditEntry> a = s.GetAudit(b.Day, b.RowId);
                AuditEntry last = a.Count > 0 ? a[a.Count - 1] : null;
                if (last != null && string.Equals(last.Login, myLogin, StringComparison.OrdinalIgnoreCase)) continue;
                ChangedBlock c = new ChangedBlock(); c.Day = b.Day; c.RowId = b.RowId; c.Version = v; c.Since = now;
                c.Kuerzel = last != null ? last.Kuerzel : "";
                r.Add(c);
            }
            return r;
        }

        // Neue Treffer uebernehmen (gleicher Block: Dauer beginnt neu), Abgelaufene entfernen; true = Liste geaendert
        public static bool Update(List<ChangedBlock> list, List<ChangedBlock> add, DateTime now)
        {
            int n = list.Count;
            foreach (ChangedBlock a in add) list.RemoveAll(delegate(ChangedBlock x) { return x.Day == a.Day && x.RowId == a.RowId; });
            list.AddRange(add);
            int gone = list.RemoveAll(delegate(ChangedBlock x) { return (now - x.Since).TotalSeconds >= Seconds; });
            return add.Count > 0 || gone > 0 || list.Count != n;
        }

        // Statusleiste: "Geändert: Di Suite 1 von WEPA, …" (bis 3 Bloecke), sonst "Geändert: n Blöcke"; leer = nichts
        public static string Status(List<ChangedBlock> l, List<RowInfo> rows)
        {
            if (l.Count == 0) return "";
            if (l.Count > 3) return "Geändert: " + l.Count + " Blöcke";
            List<string> p = new List<string>();
            foreach (ChangedBlock c in l)
            {
                RowInfo r = rows.Find(delegate(RowInfo x) { return x.Id == c.RowId; });
                string name = r != null && r.Name.Length > 0 ? r.Name : "Zeile";
                p.Add(DayNames[(int)c.Day.DayOfWeek] + " " + name + (c.Kuerzel.Length > 0 ? " von " + c.Kuerzel : ""));
            }
            return "Geändert: " + string.Join(", ", p.ToArray());
        }
    }
}
