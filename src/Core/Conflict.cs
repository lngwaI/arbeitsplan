using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Gleichzeitige Aenderung desselben Blocks auf zwei PCs (Versionskonflikt beim Speichern): Dreiwege-Zusammenfuehrung.
    // Basis = Stand beim Beginn der Bearbeitung, mine = eigene Fassung, theirs = inzwischen gespeicherter Stand.
    // Was nur eine Seite geaendert hat, wird uebernommen (Inhalt zeilenweise samt Format, F/S/N-Kuerzel, LKZ-Haken).
    // Nur wenn beide dieselbe Stelle unterschiedlich geaendert haben, meldet Merge einen Konflikt; das Ergebnis enthaelt dann
    // an dieser Stelle beide Fassungen (zuerst die gespeicherte, darunter die eigene), es geht also nichts verloren.
    public static class Conflict
    {
        // Ergebnis hat die Version von theirs (damit es danach gespeichert werden kann); clashes = Namen der strittigen Felder.
        public static Entry Merge(Entry b, Entry mine, Entry theirs, List<string> clashes)
        {
            if (b == null) b = new Entry();
            if (theirs == null) theirs = new Entry();
            Entry r = theirs.Clone();
            bool clash;
            List<Line> lines = MergeLines(Lines(b), Lines(mine), Lines(theirs), out clash);
            Join(lines, r);
            if (clash) clashes.Add("Inhalt");
            string[] names = { "F", "S", "N" };
            for (int s = 0; s < 3; s++)
            {
                string bs = b.People[s], ms = mine.People[s], ts = theirs.People[s];
                if (ms == ts || ms == bs) r.People[s] = ts;
                else if (ts == bs) r.People[s] = ms;
                else { r.People[s] = JoinPeople(ts, ms); clashes.Add(names[s]); }
            }
            r.Lkz = mine.Lkz == b.Lkz ? theirs.Lkz : mine.Lkz;   // Haken: geaendert gewinnt (beide geaendert = gleicher Wert)
            return r;
        }

        // Kuerzel beider Fassungen ohne Doppelte (Reihenfolge: gespeicherte, dann eigene neue)
        static string JoinPeople(string theirs, string mine)
        {
            List<string> all = new List<string>();
            foreach (string p in (theirs + "," + mine).Split(',', '\n'))
                if (p.Trim().Length > 0 && !all.Exists(delegate(string x) { return string.Equals(x, p.Trim(), StringComparison.OrdinalIgnoreCase); })) all.Add(p.Trim());
            return string.Join(", ", all.ToArray());
        }

        // Eine Inhaltszeile mit ihren Formaten (Start relativ zum Zeilenanfang)
        class Line
        {
            public string Text = "";
            public List<FormatSpan> Spans = new List<FormatSpan>();
            string key;
            public string Key { get { if (key == null) { key = Text + "\u0001"; foreach (FormatSpan s in Spans) key += s.Key(); } return key; } }
        }

        static List<Line> Lines(Entry e)
        {
            List<Line> l = new List<Line>();
            int pos = 0;
            foreach (string t in e.Content.Split('\n'))
            {
                Line ln = new Line(); ln.Text = t;
                foreach (FormatSpan s in e.Spans)
                {
                    int a = Math.Max(s.Start, pos), z = Math.Min(s.Start + s.Length, pos + t.Length);
                    if (z <= a) continue;
                    FormatSpan c = s.Clone(); c.Start = a - pos; c.Length = z - a;
                    ln.Spans.Add(c);
                }
                l.Add(ln); pos += t.Length + 1;
            }
            if (e.Content.Length == 0) l.Clear();
            return l;
        }

        static void Join(List<Line> lines, Entry r)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            r.Spans = new List<FormatSpan>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                foreach (FormatSpan s in lines[i].Spans) { FormatSpan c = s.Clone(); c.Start += sb.Length; r.Spans.Add(c); }
                sb.Append(lines[i].Text);
            }
            r.Content = sb.ToString();
        }

        // diff3: Anker = Basiszeilen, die in beiden Fassungen unveraendert vorkommen (LCS); dazwischen je Abschnitt
        // die geaenderte Seite nehmen, bei beidseitiger Aenderung beide Fassungen (Konflikt).
        static List<Line> MergeLines(List<Line> b, List<Line> m, List<Line> t, out bool clash)
        {
            clash = false;
            int[] mm = Match(b, m), mt = Match(b, t);
            List<Line> r = new List<Line>();
            int i = 0, im = 0, it = 0;
            for (int k = 0; k <= b.Count; k++)
            {
                bool anchor = k < b.Count && mm[k] >= 0 && mt[k] >= 0;
                if (!anchor && k < b.Count) continue;
                int em = k < b.Count ? mm[k] : m.Count, et = k < b.Count ? mt[k] : t.Count;
                List<Line> cb = b.GetRange(i, k - i), cm = m.GetRange(im, em - im), ct = t.GetRange(it, et - it);
                if (Same(cm, cb)) r.AddRange(ct);
                else if (Same(ct, cb) || Same(cm, ct)) r.AddRange(cm);
                else if (cm.Count == cb.Count && ct.Count == cb.Count)
                    for (int x = 0; x < cb.Count; x++)   // gleich viele Zeilen (z. B. Nachbarzeilen geaendert): Zeile fuer Zeile
                    {
                        if (cm[x].Key == cb[x].Key || cm[x].Key == ct[x].Key) r.Add(ct[x]);
                        else if (ct[x].Key == cb[x].Key) r.Add(cm[x]);
                        else { clash = true; r.Add(ct[x]); r.Add(cm[x]); }
                    }
                else
                {
                    clash = true;
                    r.AddRange(ct);
                    foreach (Line x in cm) if (!ct.Exists(delegate(Line y) { return y.Key == x.Key; })) r.Add(x);
                }
                if (k < b.Count) r.Add(m[em]);
                i = k + 1; im = em + 1; it = et + 1;
            }
            return r;
        }

        static bool Same(List<Line> x, List<Line> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++) if (x[i].Key != y[i].Key) return false;
            return true;
        }

        // Laengste gemeinsame Teilfolge: fuer jede Zeile von a der Index in c oder -1 (monoton steigend)
        static int[] Match(List<Line> a, List<Line> c)
        {
            int n = a.Count, k = c.Count;
            int[,] d = new int[n + 1, k + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = k - 1; j >= 0; j--)
                    d[i, j] = a[i].Key == c[j].Key ? d[i + 1, j + 1] + 1 : Math.Max(d[i + 1, j], d[i, j + 1]);
            int[] r = new int[n];
            for (int x = 0; x < n; x++) r[x] = -1;
            int p = 0, q = 0;
            while (p < n && q < k)
            {
                if (a[p].Key == c[q].Key) { r[p] = q; p++; q++; }
                else if (d[p + 1, q] >= d[p, q + 1]) p++;
                else q++;
            }
            return r;
        }
    }
}
