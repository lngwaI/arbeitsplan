using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Notizzeilen: ganze Zeilen des Blockinhalts, markiert ueber FormatSpan.Note (immer zusaetzlich Italic).
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

        static bool HasNoteSpan(Entry e)
        {
            foreach (FormatSpan s in e.Spans) if (s.Note) return true;
            return false;
        }

        // Zeilen mit sichtbarem Text, die von einer Notiz-Markierung beruehrt werden: Index in Lines(), Start, Laenge
        public static List<int[]> NoteLines(Entry e)
        {
            List<int[]> res = new List<int[]>();
            if (e == null || !HasNoteSpan(e)) return res;
            List<int[]> ls = Lines(e.Content);
            for (int i = 0; i < ls.Count; i++)
            {
                int a = ls[i][0], z = a + ls[i][1];
                if (z <= a || e.Content.Substring(a, z - a).Trim().Length == 0) continue;
                foreach (FormatSpan s in e.Spans)
                    if (s.Note && s.Start < z && s.Start + s.Length > a) { res.Add(new[] { i, a, ls[i][1] }); break; }
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

        // Notiztexte (ohne Markierung) in Reihenfolge
        public static List<string> NoteTexts(Entry e)
        {
            List<string> res = new List<string>();
            foreach (int[] n in NoteLines(e)) res.Add(e.Content.Substring(n[1], n[2]));
            return res;
        }

        // Markierung nur auf ganzen, nicht leeren Zeilen und immer kursiv; Formate sonst unveraendert, zusammenhaengende gleiche Spans verschmolzen.
        public static void Normalize(Entry e)
        {
            if (!HasNoteSpan(e)) return;
            List<int[]> notes = NoteLines(e);
            int len = e.Content.Length;
            List<FormatSpan> res = new List<FormatSpan>();
            foreach (FormatSpan s in e.Spans)
            {
                FormatSpan c = s.Clone();
                if (c.Start < 0) { c.Length += c.Start; c.Start = 0; }
                if (c.Start + c.Length > len) c.Length = len - c.Start;
                if (c.Length <= 0) continue;
                if (c.Note) { c.Note = false; c.Italic = c.NoteWasItalic; c.NoteWasItalic = false; }
                res.Add(c);
            }
            e.Spans = res;
            foreach (int[] n in notes) Paint(e, n[1], n[1] + n[2], true);
            Tidy(e);
        }

        // Zeilen first..last (Index in Lines(Content)) als Notiz markieren bzw. aufheben; leere Zeilen werden uebergangen.
        public static void SetNote(Entry e, int first, int last, bool on)
        {
            List<int[]> ls = Lines(e.Content);
            Normalize(e);
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

        // Bereich [a, b) mit Note+Italic belegen (on) oder davon befreien; vorhandene Spans werden an den Grenzen geteilt.
        static void Paint(Entry e, int a, int b, bool on)
        {
            if (b <= a) return;
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
                if (on && m0 > pos) res.Add(NoteSpan(pos, m0));
                FormatSpan m = s.Clone(); m.Start = m0; m.Length = m1 - m0; bool was = s.Note ? s.NoteWasItalic : s.Italic;
                m.Note = on; m.NoteWasItalic = on && was; m.Italic = on || was;
                res.Add(m);
                pos = Math.Max(pos, m1);
                if (s1 > b) { FormatSpan r = s.Clone(); r.Start = b; r.Length = s1 - b; res.Add(r); }
            }
            if (on && pos < b) res.Add(NoteSpan(pos, b));
            e.Spans = res;
        }

        static FormatSpan NoteSpan(int a, int b) { FormatSpan f = new FormatSpan(); f.Start = a; f.Length = b - a; f.Italic = true; f.Note = true; return f; }

        // Sortieren, Standard-Spans ohne Wirkung entfernen, aneinanderstossende gleiche Spans verschmelzen
        static void Tidy(Entry e)
        {
            List<FormatSpan> l = new List<FormatSpan>();
            foreach (FormatSpan s in e.Spans)
                if (s.Length > 0 && (s.Rgb != 0 || s.Size > 0 || s.Bold || s.Italic || s.Note || s.Font != null)) l.Add(s);
            l.Sort(delegate(FormatSpan x, FormatSpan y) { return x.Start.CompareTo(y.Start); });
            List<FormatSpan> res = new List<FormatSpan>();
            foreach (FormatSpan s in l)
            {
                FormatSpan p = res.Count > 0 ? res[res.Count - 1] : null;
                if (p != null && p.Start + p.Length == s.Start && p.Rgb == s.Rgb && p.Size == s.Size && p.Bold == s.Bold && p.Italic == s.Italic && p.Note == s.Note && p.NoteWasItalic == s.NoteWasItalic && p.Font == s.Font)
                    p.Length += s.Length;
                else res.Add(s);
            }
            e.Spans = res;
        }
    }
}
