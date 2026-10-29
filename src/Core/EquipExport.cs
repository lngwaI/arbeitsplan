using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Equipment-Bedarf: alle erkannten Equipment-Nummern eines Zeitraums, jede einmal (Datum, dann Zeilenreihenfolge).
    public static class EquipExport
    {
        public static List<string> Collect(IDataStore store, DateTime from, DateTime to)
        {
            List<string> res = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            List<RowInfo> rows = store.GetRows();
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                foreach (RowInfo r in rows)
                {
                    Entry e = store.Get(d, r.Id);
                    if (e == null || string.IsNullOrEmpty(e.Content)) continue;
                    foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(e.Content))
                        if (seen.Add(m.Value)) res.Add(m.Value);
                }
            return res;
        }

        // ID, Leerzeile, ID ... (Zeilenende CRLF)
        public static string Format(List<string> ids)
        {
            return string.Join("\r\n\r\n", ids.ToArray());
        }
    }
}
