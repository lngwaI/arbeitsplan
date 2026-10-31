using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // P3: "Block im Plan schon belegt" - eine Pruefung fuer Kampagnenstart (je Zeile, Zusammenfassung) und Paket-Editor (Info-Hinweis).
    public partial class Package
    {
        // Gleiche Definition wie in Merge: nur Notizen/LKZ zaehlen als leer; Puffer und Freeze-Vorgaenge belegen.
        public static bool Occupied(Entry e) { return e != null && !(e.IsEmpty && e.Buffers.Count == 0); }

        // Anzahl verschiedener Bloecke (Tag, Raum), in die der Plan (null = Schedule()) schreiben wuerde und die schon belegt sind
        public int CountBusy(IDataStore store, DateTime start, List<RunDate> plan)
        {
            if (plan == null) plan = Schedule();
            List<RowInfo> rows = store.GetRows();
            HashSet<string> seen = new HashSet<string>();
            int n = 0;
            foreach (RunDate d in plan)
            {
                if (d.Step < 0 || d.Step >= Steps.Count) continue;
                int id = RowId(rows, d.Row.Length > 0 ? d.Row : Steps[d.Step].Row);
                if (id < 0) continue;
                foreach (int dl in CardsSplit(Steps[d.Step], start.DayOfWeek) ? Steps[d.Step].CardDeltas() : new List<int>(new[] { 0 }))   // Karten mit eigenem Versatz liegen an anderen Tagen
                {
                    DateTime day = start.AddDays(d.Day - 1 + dl);
                    if (seen.Add(day.Ticks + "|" + id) && Occupied(store.Get(day, id))) n++;
                }
            }
            return n;
        }

        // Zusatz fuer die Schritt-Spalte eines Puffer-Termins: "für Lauf 11110002" (ohne Nummer: "für Lauf 3"); "" = kein Puffer / kein Bezug
        public string ForLaufText(RunDate rd, int day, List<RunDate> plan)
        {
            if (rd.Step < 0 || rd.Step >= Steps.Count || !Steps[rd.Step].IsBuffer) return "";
            RunDate at = new RunDate(); at.Step = rd.Step; at.Run = rd.Run; at.Day = day;
            int lauf = LaufOf(plan, at) + 1;
            if (lauf < 1) return "";
            string no = LaufNoOf(lauf, Day1);
            return "für Lauf " + (no.Length > 0 ? no : lauf.ToString());
        }
    }
}
