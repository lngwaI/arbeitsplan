using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Equipment-Bedarf: alle erkannten Equipment-Nummern eines Zeitraums, jede einmal (Datum, dann Zeilenreihenfolge).
    public static class EquipExport
    {
        // Durchsuchter Text eines Blocks: Inhalt ohne Notizzeilen plus Name und Text der Puffer
        static string ScanText(Entry e)
        {
            StringBuilder sb = new StringBuilder(Notes.ContentText(e));
            foreach (BufferItem b in e.Buffers) sb.Append('\n').Append(b.Name).Append('\n').Append(BufferSet.Plain(b.Text));
            return sb.ToString();
        }

        // Nummern in Reihenfolge des ersten Vorkommens; Anzahl = Bloecke (Tag/Zeile), in denen die Nummer vorkommt (mehrfach im selben Block zaehlt einmal)
        class Counter
        {
            public readonly List<string> Order = new List<string>();
            readonly Dictionary<string, int> n = new Dictionary<string, int>();
            public void Block(string txt)
            {
                HashSet<string> inBlock = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(txt))
                    if (inBlock.Add(m.Value)) { int c; n.TryGetValue(m.Value, out c); if (c == 0) Order.Add(m.Value); n[m.Value] = c + 1; }
            }
            public List<KeyValuePair<string, int>> Result()
            {
                List<KeyValuePair<string, int>> r = new List<KeyValuePair<string, int>>();
                foreach (string k in Order) r.Add(new KeyValuePair<string, int>(k, n[k]));
                return r;
            }
        }

        static void ScanRange(IDataStore store, DateTime from, DateTime to, Counter c)
        {
            List<RowInfo> rows = store.GetRows();
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                foreach (RowInfo r in rows)
                {
                    Entry e = store.Get(d, r.Id);
                    if (e == null) continue;
                    string txt = ScanText(e);
                    if (txt.Trim().Length > 0) c.Block(txt);
                }
        }

        public static List<string> Ids(List<KeyValuePair<string, int>> l)
        {
            List<string> r = new List<string>();
            foreach (KeyValuePair<string, int> kv in l) r.Add(kv.Key);
            return r;
        }

        public static List<string> Collect(IDataStore store, DateTime from, DateTime to) { return Ids(Count(store, from, to)); }

        // Nummer + Anzahl der Bloecke (Option "mit Anzahl")
        public static List<KeyValuePair<string, int>> Count(IDataStore store, DateTime from, DateTime to)
        {
            Counter c = new Counter(); ScanRange(store, from, to, c);
            return c.Result();
        }

        // Kampagne im Plan: zusammenhaengende Hauptlauf-Nummern eines Praefixes (XXYY), Tage der Laeufe von erstem bis letztem.
        public class CampaignSpan
        {
            public string Prefix = "";
            public int FirstNo, LastNo;
            public DateTime First, Last;
            public SortedDictionary<int, DateTime> Runs = new SortedDictionary<int, DateTime>();   // Laufnummer (ZZZZ) -> Tag
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
                        if (e == null) continue;
                        string txt = ScanText(e);
                        if (txt.Trim().Length == 0) continue;
                        foreach (RunRef x in RunNumbers.Find(txt))
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
                    c.LastNo = r.Key; c.Last = r.Value; c.Runs[r.Key] = r.Value; prev = r.Key; prevDay = r.Value;
                }
            }
            res.Sort(delegate(CampaignSpan a, CampaignSpan b) { return a.First != b.First ? a.First.CompareTo(b.First) : string.CompareOrdinal(a.Prefix, b.Prefix); });
            return res;
        }

        // Equipment der Kampagne: Zeitraum = erster Lauf minus "before" Tage (Vorlauf, Fallback fuer Puffer ohne Lauf-Angabe) bis letzter Lauf plus "after" Tage.
        public static List<string> CollectCampaign(IDataStore store, CampaignSpan c, int before, int after)
        {
            DateTime a, b; return Ids(CountCampaign(store, c, before, after, out a, out b));
        }

        // Zusaetzlich: Puffer mit Lauf-Angabe (BufferItem.LaufRef = Nummer des Laufs in der Kampagne, 1 = erster) bis 30 Tage vor dem bedienten Lauf gehoeren dazu,
        // auch wenn sie vor dem Vorlauf liegen (nur deren Text; fremde Bloecke desselben Tages nicht). from = frueheste Stelle, to = Ende.
        public static List<KeyValuePair<string, int>> CountCampaign(IDataStore store, CampaignSpan c, int before, int after, out DateTime from, out DateTime to)
        {
            from = c.First.AddDays(-Math.Max(0, before)); to = c.Last.AddDays(Math.Max(0, after));
            Counter cnt = new Counter(); ScanRange(store, from, to, cnt);
            List<RowInfo> rows = store.GetRows();
            DateTime early = from;
            for (DateTime d = c.First.AddDays(-30); d < from; d = d.AddDays(1))
                foreach (RowInfo r in rows)
                {
                    Entry e = store.Get(d, r.Id);
                    if (e == null || e.Buffers.Count == 0) continue;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (BufferItem b in e.Buffers) if (Serves(c, b, d)) sb.Append(b.Name).Append('\n').Append(BufferSet.Plain(b.Text)).Append('\n');
                    if (sb.Length == 0) continue;
                    cnt.Block(sb.ToString());
                    if (d < early) early = d;
                }
            from = early;
            return cnt.Result();
        }

        // Bedient der Puffer b (am Tag d) einen Lauf der Kampagne c? Lauf-Angabe 1..n, Tag hoechstens 30 Tage vor dem Lauf und nicht danach,
        // ein Produkt im Puffertext (Laufnummer) darf der Kampagne nicht widersprechen.
        static bool Serves(CampaignSpan c, BufferItem b, DateTime d)
        {
            if (b.LaufRef <= 0) return false;
            int run = c.FirstNo + b.LaufRef - 1; DateTime rd;
            if (run > c.LastNo || !c.Runs.TryGetValue(run, out rd) || d > rd || (rd - d).Days > 30) return false;
            foreach (RunRef x in RunNumbers.Find(b.Name + "\n" + BufferSet.Plain(b.Text))) if (x.Product != c.Prefix) return false;
            return true;
        }

        // ID, Leerzeile, ID ... (Zeilenende CRLF)
        public static string Format(List<string> ids)
        {
            return string.Join("\r\n\r\n", ids.ToArray());
        }

        // Nummer, Tab, Anzahl je Zeile (Zeilenende CRLF; tabellarisch, ohne Leerzeilen)
        public static string FormatCounts(List<KeyValuePair<string, int>> l)
        {
            List<string> r = new List<string>();
            foreach (KeyValuePair<string, int> kv in l) r.Add(kv.Key + "\t" + kv.Value);
            return string.Join("\r\n", r.ToArray());
        }
    }
}
