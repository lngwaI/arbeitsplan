using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arbeitsplan.Core
{
    // Hauptlauf -> Unterlaeufe/Tanks, rueckwaerts aufgebaut (nie am Hauptlauf gespeichert): aus FreezeItem.MainRef/SubNo/Tanks
    // und aus Plantext-Zeilen "Unterlauf X (aus Y)" (wie RunNumbers; Notizzeilen zaehlen nicht).
    public class SplitInfo
    {
        public string Main = "";
        public List<string> Subs = new List<string>(), Tanks = new List<string>();

        // "gesplittet in X1, X2 · Tanks T1, T2"
        public string Text
        {
            get
            {
                string s = Subs.Count > 0 ? "gesplittet in " + string.Join(", ", Subs.ToArray()) : "gesplittet";
                return Tanks.Count > 0 ? s + " · Tanks " + string.Join(", ", Tanks.ToArray()) : s;
            }
        }
    }

    public class SplitIndex
    {
        static readonly Regex SubRx = new Regex(@"^Unterlauf(?:\s+(\S+))?\s+\(aus\s+(\d{8})\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        readonly Dictionary<string, SplitInfo> byMain = new Dictionary<string, SplitInfo>();

        public SplitInfo Get(string main) { SplitInfo i; return main != null && byMain.TryGetValue(main, out i) ? i : null; }
        public int Count { get { return byMain.Count; } }

        SplitInfo Info(string main)
        {
            SplitInfo i;
            if (!byMain.TryGetValue(main, out i)) { i = new SplitInfo(); i.Main = main; byMain[main] = i; }
            return i;
        }

        static bool IsMain(string s) { return s != null && Regex.IsMatch(s, @"^\d{8}$"); }

        // Alle Bloecke von..bis (Tage x Zeilen) einlesen
        public static SplitIndex Build(IDataStore store, DateTime from, DateTime to)
        {
            SplitIndex ix = new SplitIndex();
            List<RowInfo> rows = store.GetRows();
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                foreach (RowInfo r in rows) ix.Add(store.Get(d, r.Id));
            return ix;
        }

        public void Add(Entry e)
        {
            if (e == null) return;
            foreach (FreezeItem f in e.Freezes)
            {
                string main = f.MainRef.Trim();
                if (!IsMain(main)) continue;
                if (f.SubNo.Trim().Length > 0) Put(Info(main).Subs, f.SubNo.Trim());
                foreach (string t in f.Tanks.Split(',')) if (t.Trim().Length > 0) Put(Info(main).Tanks, t.Trim());
            }
            foreach (string line in Notes.ContentText(e).Split('\n'))
            {
                Match m = SubRx.Match(line.Trim());
                if (m.Success && m.Groups[1].Success) Put(Info(m.Groups[2].Value).Subs, m.Groups[1].Value);
            }
        }

        static void Put(List<string> l, string v) { if (!l.Contains(v)) l.Add(v); }

        // Abgeleitete Zeilen am Hauptlauf-Block: je darin erkannter Laufnummer mit Unterlaeufen/Tanks "gesplittet in …"
        // (bei mehreren Laeufen im Block mit Vorsatz "Lauf <Nr>: ")
        public List<string> LinesFor(Entry e)
        {
            List<string> r = new List<string>();
            List<RunRef> runs = RunNumbers.Find(e);
            foreach (RunRef x in runs)
            {
                string main = x.Product + x.Run;
                SplitInfo i = Get(main);
                if (i != null) r.Add((runs.Count > 1 ? "Lauf " + main + ": " : "") + i.Text);
            }
            return r;
        }
    }
}
