using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Notizzeilen: ganze Zeilen des Blockinhalts, die vollstaendig kursiv sind (Kursiv = Notiz; kein eigenes Kennzeichen).
    // Sie zaehlen nicht als Inhalt (Entry.HasText, Laufnummern); ein Block nur mit Notizen gilt als leer.
    public static class Notes
    {
        // [Start, Laenge ohne Zeilenumbruch] je Zeile (leerer Text = keine Zeile)
        public static List<int[]> Lines(string text)
        {
            List<int[]> res = new List<int[]>();
            if (string.IsNullOrEmpty(text)) return res;
            int pos = 0;
            foreach (string t in text.Split('\n')) { res.Add(new[] { pos, t.Length }); pos += t.Length + 1; }
            return res;
        }

        // Zeilen mit sichtbarem Text, deren Zeichen (ohne Leerraum) alle kursiv sind: Index in Lines(), Start, Laenge
        public static List<int[]> NoteLines(Entry e)
        {
            List<int[]> res = new List<int[]>();
            if (e == null || e.Spans.Count == 0) return res;
            bool[] it = new bool[e.Content.Length];
            foreach (FormatSpan s in e.Spans)
                if (s.Italic) for (int i = Math.Max(0, s.Start); i < s.Start + s.Length && i < it.Length; i++) it[i] = true;
            List<int[]> ls = Lines(e.Content);
            for (int i = 0; i < ls.Count; i++)
            {
                bool any = false, all = true;
                for (int k = ls[i][0]; k < ls[i][0] + ls[i][1]; k++)
                {
                    if (char.IsWhiteSpace(e.Content[k])) continue;
                    any = true; if (!it[k]) { all = false; break; }
                }
                if (any && all) res.Add(new[] { i, ls[i][0], ls[i][1] });
            }
            return res;
        }

        // Inhalt ohne Notizzeilen (Zeilen und ihre Umbrueche entfallen)
        public static string ContentText(Entry e)
        {
            if (e == null) return "";
            List<int[]> notes = NoteLines(e);
            if (notes.Count == 0) return e.Content;
            List<string> keep = new List<string>();
            string[] all = e.Content.Split('\n');
            for (int i = 0; i < all.Length; i++)
                if (!notes.Exists(delegate(int[] n) { return n[0] == i; })) keep.Add(all[i]);
            return string.Join("\n", keep.ToArray());
        }

        // Notiztexte in Reihenfolge
        public static List<string> NoteTexts(Entry e)
        {
            List<string> res = new List<string>();
            foreach (int[] n in NoteLines(e)) res.Add(e.Content.Substring(n[1], n[2]));
            return res;
        }

        // Zeilen first..last (Index in Lines(Content)) ganz kursiv setzen bzw. das Kursiv entfernen; leere Zeilen werden uebergangen.
        public static void SetNote(Entry e, int first, int last, bool on)
        {
            List<int[]> ls = Lines(e.Content);
            for (int i = Math.Max(0, first); i <= last && i < ls.Count; i++)
            {
                int a = ls[i][0], z = a + ls[i][1];
                if (z <= a || e.Content.Substring(a, z - a).Trim().Length == 0) continue;
                Paint(e, a, z, on);
            }
            Tidy(e);
        }

        // Zeilenindex (in Lines) der Position pos
        public static int LineAt(string text, int pos)
        {
            int n = 0;
            for (int i = 0; i < pos && i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        // Bereich [a, b) kursiv setzen (on) oder davon befreien; vorhandene Spans werden an den Grenzen geteilt, Luecken bekommen einen neuen Span.
        static void Paint(Entry e, int a, int b, bool on)
        {
            List<FormatSpan> sorted = new List<FormatSpan>(e.Spans);
            sorted.Sort(delegate(FormatSpan x, FormatSpan y) { return x.Start.CompareTo(y.Start); });
            List<FormatSpan> res = new List<FormatSpan>();
            int pos = a;
            foreach (FormatSpan s in sorted)
            {
                int s0 = s.Start, s1 = s.Start + s.Length;
                if (s1 <= a || s0 >= b) { res.Add(s); continue; }
                if (s0 < a) { FormatSpan l = s.Clone(); l.Length = a - s0; res.Add(l); }
                int m0 = Math.Max(s0, a), m1 = Math.Min(s1, b);
                if (on && m0 > pos) res.Add(ItalicSpan(pos, m0));
                FormatSpan m = s.Clone(); m.Start = m0; m.Length = m1 - m0; m.Italic = on;
                res.Add(m);
                pos = Math.Max(pos, m1);
                if (s1 > b) { FormatSpan r = s.Clone(); r.Start = b; r.Length = s1 - b; res.Add(r); }
            }
            if (on && pos < b) res.Add(ItalicSpan(pos, b));
            e.Spans = res;
        }

        static FormatSpan ItalicSpan(int a, int b) { FormatSpan f = new FormatSpan(); f.Start = a; f.Length = b - a; f.Italic = true; return f; }

        // Sortieren, Standard-Spans ohne Wirkung entfernen, aneinanderstossende gleiche Spans verschmelzen
        static void Tidy(Entry e)
        {
            List<FormatSpan> l = new List<FormatSpan>();
            foreach (FormatSpan s in e.Spans)
                if (s.Length > 0 && (s.Rgb != 0 || s.Size > 0 || s.Bold || s.Italic || s.Font != null)) l.Add(s);
            l.Sort(delegate(FormatSpan x, FormatSpan y) { return x.Start.CompareTo(y.Start); });
            List<FormatSpan> res = new List<FormatSpan>();
            foreach (FormatSpan s in l)
            {
                FormatSpan p = res.Count > 0 ? res[res.Count - 1] : null;
                if (p != null && p.Start + p.Length == s.Start && p.Rgb == s.Rgb && p.Size == s.Size && p.Bold == s.Bold && p.Italic == s.Italic && p.Font == s.Font)
                    p.Length += s.Length;
                else res.Add(s);
            }
            e.Spans = res;
        }
    }
}
