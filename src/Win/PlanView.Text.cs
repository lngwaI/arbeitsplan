using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Textstuecke eines Blocks (Schrift, Farben, Equipment-Status, Suchtreffer) und Umbruch (Build).
    partial class PlanView
    {
        // ---------- Textstuecke (Schrift, Farben, Markierungen) ----------

        static Color Rgb(int rgb) { return Ui.Rgb(rgb); }

        List<Piece> Group(string text, Color[] fore, Font[] fnt, int[] mode)
        {
            List<Piece> res = new List<Piece>();
            int n = text.Length, s = 0;
            for (int i = 1; i <= n; i++)
                if (i == n || fore[i] != fore[s] || mode[i] != mode[s] || fnt[i] != fnt[s])
                {
                    res.Add(new Piece(text.Substring(s, i - s), fnt[s], fore[s], mode[s] != 0, mode[s] == 1 ? Yellow : mode[s] == 3 ? Theme.Current.NoteBack : Green));
                    s = i;
                }
            return res;
        }

        int MarkQuery(string text, int[] mode)
        {
            int hits = 0;
            if (Query.Length == 0) return 0;
            int idx = 0;
            while ((idx = text.IndexOf(Query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                for (int k = idx; k < idx + Query.Length; k++) mode[k] = 1;
                hits++; idx += Query.Length;
            }
            return hits;
        }

        public static Font SpanFont(FormatSpan sp, Font baseFont)
        {
            FontStyle st = (sp.Bold ? FontStyle.Bold : 0) | (sp.Italic ? FontStyle.Italic : 0);
            return FontCache.Get(string.IsNullOrEmpty(sp.Font) ? baseFont.Name : sp.Font, sp.Size > 0 ? sp.Size : baseFont.SizeInPoints, st, baseFont);
        }

        // Inhalt: manuelle Formatierung, darueber Statusfarbe der Equipment-Nummern, Suchtreffer gelb.
        List<Piece> ContentPieces(CellData c, out int hits)
        {
            string text = c.Content;
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            foreach (FormatSpan sp in c.Cur.Spans)
            {
                Font f = SpanFont(sp, plain);
                for (int i = sp.Start; i < sp.Start + sp.Length && i < fore.Length; i++)
                {
                    if (sp.Rgb != 0) fore[i] = Rgb(sp.Rgb); else if (sp.Note) fore[i] = Theme.Current.NoteText;
                    fnt[i] = f;
                    if (sp.Note) mode[i] = 3;   // Notizzeile: abgesetzt (Suchtreffer gelb gewinnt unten)
                }
            }
            // Erkannte Equipment-Nummer: Statusfarbe; Status unbekannt/nicht abrufbar -> Standard schwarz (Lesbarkeit).
            foreach (Match m in EquipmentMatcher.Pattern.Matches(text))
            {
                Color col = Rgb(StatusRgb(m.Value));
                for (int i = m.Index; i < m.Index + m.Length; i++) fore[i] = col;
            }
            hits = MarkQuery(text, mode);
            return Group(text, fore, fnt, mode);
        }

        // Statusfarbe (0xRRGGBB) einer Equipment-Nummer; unbekannt/nicht abrufbar = schwarz. Auch der Editor nutzt sie.
        public int StatusRgb(string number)
        {
            EquipmentInfo info;
            if (!equipCache.TryGetValue(number, out info))   // je Nummer nur einmal je Rebuild (langsame Quelle blockiert nicht jedes Layout)
            {
                try { if (Equip != null) info = Equip.Get(number); } catch (Exception) { info = null; /* Quelle nicht erreichbar */ }
                equipCache[number] = info;
            }
            return info != null ? Colors.Get(info.Status) : 0;
        }

        // Erkannte Equipment-Nummer unter dem Mauszeiger (Anzeigemodus, Inhaltsfeld); "" wenn keine
        public string EquipAt(Point p)
        {
            CellData c = CellAt(p);
            if (c == null || c.Editing || Collapsed[c.RowIndex] || c.CB == null || !c.RContent.Contains(p)) return "";
            string word = c.CB.WordAt(p.X - (c.RContent.X + TP), p.Y - (c.RContent.Y + TV));
            Match m = word.Length > 0 ? EquipmentMatcher.Pattern.Match(word) : null;
            return m != null && m.Success ? m.Value : "";
        }

#if TESTHOOKS
        // Test: Mitte des ersten erkannten Equipment-Textes im Anzeigemodus; liefert EquipAt an dieser Stelle (oder null, wenn keine Nummer sichtbar)
        public string TestEquipAtFirst(out string expected)
        {
            expected = null;
            foreach (CellData c in Cells)
            {
                if (c.Editing || Collapsed[c.RowIndex] || c.CB == null) continue;
                int y = 0;
                for (int l = 0; l < c.CB.Lines.Count; l++)
                {
                    foreach (Item it in c.CB.Lines[l])
                    {
                        Match m = EquipmentMatcher.Pattern.Match(it.Text);
                        if (!m.Success) { continue; }
                        expected = m.Value;
                        return EquipAt(new Point(c.RContent.X + TP + it.X + 2, c.RContent.Y + TV + y + 2));
                    }
                    y += c.CB.LineH[l];
                }
            }
            return null;
        }
#endif

        // Kuerzel: eigenes gruen, Suchtreffer gelb.
        List<Piece> PeoplePieces(string text, out int hits)
        {
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            for (int i = 0; i < fore.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            if (MyKuerzel.Length > 0)
                foreach (Match m in PersonPlan.Token.Matches(text))
                    if (string.Equals(m.Value, MyKuerzel, StringComparison.OrdinalIgnoreCase))
                        for (int i = m.Index; i < m.Index + m.Length; i++) mode[i] = 2;
            hits = MarkQuery(text, mode);
            return Group(text, fore, fnt, mode);
        }

        List<Piece> Plain(string text)
        {
            List<Piece> l = new List<Piece>();
            if (text.Length > 0) l.Add(new Piece(text, Font, Color.Black, false, Color.Empty));
            return l;
        }

        void Build(CellData c, int tw, int pw)
        {
            int h = 0, h2;
            if (c.Editing)
            {
                c.CB = TextBlock.Build(Plain(c.Content), Font, tw);
                for (int s = 0; s < 3; s++) c.PB[s] = TextBlock.Build(Plain(c.People[s]), Font, pw);
            }
            else
            {
                c.CB = TextBlock.Build(ContentPieces(c, out h), Font, tw);
                for (int s = 0; s < 3; s++) { c.PB[s] = TextBlock.Build(PeoplePieces(c.People[s], out h2), Font, pw); if (Rows[c.RowIndex].HasShift(s)) h += h2; }
            }
            int hb; BuildBuffers(c, tw, out hb); h += hb;
            c.Hits = h;
            c.Dirty = false;
        }

    }
}
