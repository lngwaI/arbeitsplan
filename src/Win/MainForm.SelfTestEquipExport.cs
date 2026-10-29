#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Equipment-Liste (Core/EquipExport.cs): Reihenfolge, doppelte nur einmal, Leerzeilen, Zeitraum-Grenzen.
    // sb == null: nur Testdaten in den Store schreiben (fuer --form equipexport).
    partial class MainForm
    {
        void SelfTestEquipExport(StringBuilder sb)
        {
            IDataStore s = sb == null ? store : new MemoryStore();
            List<RowInfo> rows = s.GetRows();
            DateTime d0 = sb == null ? start.Date : new DateTime(2031, 3, 3);
            Put(s, d0, rows[0].Id, "Tank H1-T-0101, Pumpe H1-P-0202 und H1-T-0101");
            Put(s, d0, rows[1].Id, "Skid H2-X-0303-01 / h1-t-0404 / H3-T-0505");
            Put(s, d0.AddDays(1), rows[0].Id, "H1-F-0606 wieder H1-P-0202");
            Put(s, d0.AddDays(8), rows[0].Id, "H1-T-0999");
            if (sb == null) return;

            List<string> day = EquipExport.Collect(s, d0, d0);
            List<string> two = EquipExport.Collect(s, d0, d0.AddDays(1));
            List<string> all = EquipExport.Collect(s, d0, d0.AddDays(8));
            List<string> none = EquipExport.Collect(s, d0.AddDays(2), d0.AddDays(7));
            bool okDay = day.Count == 3 && day[0] == "H1-T-0101" && day[1] == "H1-P-0202" && day[2] == "H2-X-0303-01";
            bool okTwo = two.Count == 4 && two[3] == "H1-F-0606";
            bool okRange = all.Count == 5 && all[4] == "H1-T-0999" && none.Count == 0;
            bool okFmt = EquipExport.Format(two) == "H1-T-0101\r\n\r\nH1-P-0202\r\n\r\nH2-X-0303-01\r\n\r\nH1-F-0606" && EquipExport.Format(none) == "";
            sb.AppendLine("Equipment-Liste: Tag=" + Ok(okDay) + " zwei Tage, doppelte einmal=" + Ok(okTwo) + " Zeitraum-Grenzen=" + Ok(okRange) + " Format=" + Ok(okFmt));
        }

        static void Put(IDataStore s, DateTime d, int row, string content)
        {
            Entry e = s.Get(d, row) ?? new Entry();
            e.Content = content;
            s.Save(d, row, e);
        }
    }
}
#endif
