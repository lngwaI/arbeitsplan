using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Equipment-Bedarf: alle erkannten Equipment-Nummern eines Zeitraums, jede einmal (Datum, dann Zeilenreihenfolge).
    public static partial class EquipExport
    {
        // Durchsuchter Text eines Blocks: Inhalt ohne Notizzeilen plus Name und Text der Puffer
        static string ScanText(Entry e)
        {
            StringBuilder sb = new StringBuilder(Notes.ContentText(e));
            foreach (BufferItem b in e.Buffers) sb.Append('\n').Append(b.Name).Append('\n').Append(BufferSet.Plain(b.Text));
            foreach (FreezeItem f in e.Freezes) sb.Append('\n').Append(f.Tanks);   // Tanks der Freeze-Vorgaenge
            return sb.ToString();
        }

        // Text fuer "nicht erkannt": kursive Teile (Notizzeilen, Notiz hinter dem Kurznamen, UX-5f) und Notizteile der Puffer zaehlen nicht
        static string LineText(Entry e)
        {
            char[] t = e.Content.ToCharArray();
            foreach (FormatSpan f in e.Spans)
                if (f.Italic) for (int i = Math.Max(0, f.Start); i < f.Start + f.Length && i < t.Length; i++) if (t[i] != '\n') t[i] = ' ';
            StringBuilder sb = new StringBuilder(new string(t));
            foreach (BufferItem b in e.Buffers) sb.Append('\n').Append(BufferLines(b));
            return sb.ToString();
        }

        static string BufferLines(BufferItem b)
        {
            return System.Text.RegularExpressions.Regex.Replace(b.Text ?? "", BufferSet.NoteMark + "[^\n]*", "");
        }

        // Nummern in Reihenfolge des ersten Vorkommens; Anzahl = Bloecke (Tag/Zeile), in denen die Nummer vorkommt (mehrfach im selben Block zaehlt einmal)
        class Counter
        {
            public readonly List<string> Order = new List<string>();
            readonly Dictionary<string, int> n = new Dictionary<string, int>();
            readonly IdMatcher ids;
            readonly List<string> unknown;
            public Counter(IDataStore store, List<string> unknown) { ids = new IdMatcher(store.GetCatalog()); this.unknown = unknown; }
            // lines: Text fuer "nicht erkannt" (ohne Notizteile)
            public void Block(string txt, string lines)
            {
                HashSet<string> inBlock = new HashSet<string>();
                foreach (string id in ids.Find(txt))
                    if (inBlock.Add(id)) { int c; n.TryGetValue(id, out c); if (c == 0) Order.Add(id); n[id] = c + 1; }
                if (unknown != null) ids.Unknown(lines, unknown);
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
                    if (txt.Trim().Length > 0) c.Block(txt, LineText(e));
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
        public static List<KeyValuePair<string, int>> Count(IDataStore store, DateTime from, DateTime to, List<string> unknown = null)
        {
            Counter c = new Counter(store, unknown); ScanRange(store, from, to, c);
            return c.Result();
        }

        // Kampagne im Plan: zusammenhaengende Hauptlauf-Nummern eines Praefixes (XXYY). Dieselbe Nummer an mehreren Tagen = ein Lauf (ueber Nacht).
        public class CampaignSpan
        {
            public string Prefix = "";
            public string ProductCode = "", ProductName = "";   // aus ProductMap.Apply; leer = Produkt unbekannt
            public int FirstNo, LastNo;
            public DateTime First, Last;                        // erster Tag des ersten Laufs, letzter Tag des letzten Laufs
            public SortedDictionary<int, DateTime> Runs = new SortedDictionary<int, DateTime>();   // Laufnummer (ZZZZ) -> erster Tag
            public List<DateTime> RunDays = new List<DateTime>();                                  // alle Tage mit einem Lauf
            public string ProductText
            {
                get { return ProductCode.Length == 0 ? "Produkt unbekannt (" + Prefix + ")" : ProductName.Length > 0 ? ProductName + " (" + ProductCode + ")" : ProductCode; }
            }
            public string Label
            {
                get { return ProductText + " · Lauf " + FirstNo + (LastNo != FirstNo ? "–" + LastNo : "") + " · " + First.ToString("dd.MM.yyyy") + (Last != First ? " – " + Last.ToString("dd.MM.yyyy") : ""); }
            }
        }

        static void SeeRun(Dictionary<string, SortedDictionary<int, List<DateTime>>> all, RunRef x, DateTime day)
        {
            SortedDictionary<int, List<DateTime>> m; List<DateTime> days; int n = int.Parse(x.Run);
            if (!all.TryGetValue(x.Product, out m)) { m = new SortedDictionary<int, List<DateTime>>(); all[x.Product] = m; }
            if (!m.TryGetValue(n, out days)) { days = new List<DateTime>(); m[n] = days; }
            if (!days.Contains(day)) days.Add(day);
        }

        // Kampagnen im Fenster from..to (Praefix leer = alle Praefixe) bzw. im ganzen Bestand (Praefix angegeben, 4 Ziffern).
        // Ohne Praefix zaehlt auch Puffertext; RunDays nennt nur Tage mit dem Lauf im Blockinhalt (Puffertext nennt den bedienten Lauf, ist aber nicht der Lauf).
        public static List<CampaignSpan> Campaigns(IDataStore store, DateTime from, DateTime to, string prefix)
        {
            Dictionary<string, SortedDictionary<int, List<DateTime>>> byPrefix = new Dictionary<string, SortedDictionary<int, List<DateTime>>>();
            HashSet<string> main = new HashSet<string>();   // Praefix + Tag mit Lauf im Blockinhalt
            prefix = (prefix ?? "").Trim();
            if (prefix.Length > 0)
            {
                if (!Package.IsCode4(prefix)) return new List<CampaignSpan>();
                foreach (SearchHit h in store.Find(prefix))
                {
                    Entry e = store.Get(h.Day, h.RowId);
                    if (e != null) foreach (RunRef x in RunNumbers.Find(e, prefix)) { SeeRun(byPrefix, x, h.Day.Date); main.Add(x.Product + h.Day.Date.Ticks); }
                }
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
                        foreach (RunRef x in RunNumbers.Find(e)) main.Add(x.Product + d.Ticks);
                        foreach (RunRef x in RunNumbers.Find(txt)) SeeRun(byPrefix, x, d);
                    }
            }
            List<CampaignSpan> res = new List<CampaignSpan>();
            foreach (KeyValuePair<string, SortedDictionary<int, List<DateTime>>> kv in byPrefix)
            {
                CampaignSpan c = null; int prev = -1; DateTime prevFirst = DateTime.MinValue, prevLast = DateTime.MinValue;
                foreach (KeyValuePair<int, List<DateTime>> r in kv.Value)
                {
                    r.Value.Sort();
                    DateTime first = r.Value[0], last = r.Value[r.Value.Count - 1];
                    if (c == null || r.Key != prev + 1 || (first - prevLast).Days > 30 || first < prevFirst)
                    {
                        c = new CampaignSpan(); c.Prefix = kv.Key; c.FirstNo = r.Key; c.First = first; c.Last = last; res.Add(c);
                    }
                    c.LastNo = r.Key; if (last > c.Last) c.Last = last;
                    c.Runs[r.Key] = first;
                    foreach (DateTime day in r.Value) if (main.Contains(kv.Key + day.Ticks) && !c.RunDays.Contains(day)) c.RunDays.Add(day);
                    prev = r.Key; prevFirst = first; prevLast = last;
                }
            }
            SortSpans(res);
            return res;
        }

        static void SortSpans(List<CampaignSpan> l)
        {
            l.Sort(delegate(CampaignSpan a, CampaignSpan b) { return a.First != b.First ? a.First.CompareTo(b.First) : string.CompareOrdinal(a.Prefix, b.Prefix); });
        }

        // Equipment der Kampagne: Zeitraum = erster Lauf minus "before" Tage (Vorlauf, Fallback fuer Puffer ohne Lauf-Angabe) bis letzter Lauf plus "after" Tage.
        public static List<string> CollectCampaign(IDataStore store, CampaignSpan c, int before, int after)
        {
            DateTime a, b; return Ids(CountCampaign(store, c, before, after, out a, out b));
        }

        // Zusaetzlich: Puffer mit Lauf-Angabe (BufferItem.LaufRef = Nummer des Laufs in der Kampagne, 1 = erster) bis 30 Tage vor dem bedienten Lauf gehoeren dazu,
        // auch wenn sie vor dem Vorlauf liegen (nur deren Text; fremde Bloecke desselben Tages nicht). from = frueheste Stelle, to = Ende.
        public static List<KeyValuePair<string, int>> CountCampaign(IDataStore store, CampaignSpan c, int before, int after, out DateTime from, out DateTime to, List<string> unknown = null)
        {
            from = c.First.AddDays(-Math.Max(0, before)); to = c.Last.AddDays(Math.Max(0, after));
            Counter cnt = new Counter(store, unknown); ScanRange(store, from, to, cnt);
            List<RowInfo> rows = store.GetRows();
            DateTime early = from;
            for (DateTime d = c.First.AddDays(-30); d < from; d = d.AddDays(1))
                foreach (RowInfo r in rows)
                {
                    Entry e = store.Get(d, r.Id);
                    if (e == null || e.Buffers.Count == 0) continue;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(), lines = new System.Text.StringBuilder();
                    foreach (BufferItem b in e.Buffers) if (Serves(c, b, d)) { sb.Append(b.Name).Append('\n').Append(BufferSet.Plain(b.Text)).Append('\n'); lines.Append(BufferLines(b)).Append('\n'); }
                    if (sb.Length == 0) continue;
                    cnt.Block(sb.ToString(), lines.ToString());
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
