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
                    if (e == null) continue;
                    StringBuilder sb = new StringBuilder(Notes.ContentText(e));
                    foreach (BufferItem b in e.Buffers) sb.Append('\n').Append(b.Name).Append('\n').Append(b.Text);
                    string txt = sb.ToString();
                    if (txt.Trim().Length == 0) continue;
                    foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(txt))
                        if (seen.Add(m.Value)) res.Add(m.Value);
                }
            return res;
        }

        // Kampagne im Plan: zusammenhaengende Hauptlauf-Nummern eines Praefixes (XXYY), Tage der Laeufe von erstem bis letztem.
        public class CampaignSpan
        {
            public string Prefix = "";
            public int FirstNo, LastNo;
            public DateTime First, Last;
            public string Label
            {
                get { return Prefix + " · Lauf " + FirstNo.ToString("0000") + (LastNo != FirstNo ? "–" + LastNo.ToString("0000") : "") + " · " + First.ToString("dd.MM.yyyy") + (Last != First ? " – " + Last.ToString("dd.MM.yyyy") : ""); }
            }
        }

        // Kampagnen im Fenster from..to (Praefix leer = alle Praefixe) bzw. im ganzen Bestand (Praefix angegeben, 4 Ziffern).
        public static List<CampaignSpan> Campaigns(IDataStore store, DateTime from, DateTime to, string prefix)
        {
            Dictionary<string, SortedDictionary<int, DateTime>> byPrefix = new Dictionary<string, SortedDictionary<int, DateTime>>();
            prefix = (prefix ?? "").Trim();
            if (prefix.Length > 0)
            {
                if (!Package.IsCode4(prefix)) return new List<CampaignSpan>();
                SortedDictionary<int, DateTime> m = new SortedDictionary<int, DateTime>();
                foreach (KeyValuePair<int, DateTime> k in Package.PastRuns(store, prefix)) m[k.Key] = k.Value;
                byPrefix[prefix] = m;
            }
            else
            {
                List<RowInfo> rows = store.GetRows();
                for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                    foreach (RowInfo r in rows)
                    {
                        Entry e = store.Get(d, r.Id);
                        if (e == null || string.IsNullOrEmpty(e.Content)) continue;
                        foreach (RunRef x in RunNumbers.Find(e))
                        {
                            SortedDictionary<int, DateTime> m;
                            if (!byPrefix.TryGetValue(x.Product, out m)) { m = new SortedDictionary<int, DateTime>(); byPrefix[x.Product] = m; }
                            int n = int.Parse(x.Run);
                            if (!m.ContainsKey(n)) m[n] = d;
                        }
                    }
            }
            List<CampaignSpan> res = new List<CampaignSpan>();
            foreach (KeyValuePair<string, SortedDictionary<int, DateTime>> kv in byPrefix)
            {
                CampaignSpan c = null; int prev = -1; DateTime prevDay = DateTime.MinValue;
                foreach (KeyValuePair<int, DateTime> r in kv.Value)
                {
                    if (c == null || r.Key != prev + 1 || (r.Value - prevDay).Days > 30 || r.Value < prevDay)
                    {
                        c = new CampaignSpan(); c.Prefix = kv.Key; c.FirstNo = r.Key; c.First = r.Value; res.Add(c);
                    }
                    c.LastNo = r.Key; c.Last = r.Value; prev = r.Key; prevDay = r.Value;
                }
            }
            res.Sort(delegate(CampaignSpan a, CampaignSpan b) { return a.First != b.First ? a.First.CompareTo(b.First) : string.CompareOrdinal(a.Prefix, b.Prefix); });
            return res;
        }

        // Equipment der Kampagne: Zeitraum = erster Lauf minus "before" Tage (Puffer/Packen vor Lauf 1) bis letzter Lauf plus "after" Tage.
        public static List<string> CollectCampaign(IDataStore store, CampaignSpan c, int before, int after)
        {
            return Collect(store, c.First.AddDays(-Math.Max(0, before)), c.Last.AddDays(Math.Max(0, after)));
        }

        // ID, Leerzeile, ID ... (Zeilenende CRLF)
        public static string Format(List<string> ids)
        {
            return string.Join("\r\n\r\n", ids.ToArray());
        }
    }
}
