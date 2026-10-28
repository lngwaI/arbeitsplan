using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Schriften werden gecacht (Font-Objekte werden nie freigegeben, es gibt nur wenige Kombinationen).
    static class FontCache
    {
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static Font Get(string name, float size, FontStyle style, Font fallback)
        {
            string key = name + "|" + size + "|" + (int)style;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = Theme.Try(name, size, style);   // Schrift nicht vorhanden: Ersatzschrift in gewuenschter Groesse
                if (f == null)
                    try { f = new Font(fallback.FontFamily, size, style, GraphicsUnit.Point); }
                    catch (Exception) { f = new Font(fallback, style); }
                fonts[key] = f;
            }
            return f;
        }
    }

    // Textstueck mit Schrift, Farbe und optionaler Hintergrundmarkierung.
    class Piece
    {
        public string Text; public Font Font; public Color Fore; public bool HasBack; public Color Back;
        public Piece(string text, Font font, Color fore, bool hasBack, Color back) { Text = text; Font = font; Fore = fore; HasBack = hasBack; Back = back; }
    }

    class Item { public string Text; public Font Font; public int X, W; public Color Fore; public bool HasBack; public Color Back; }

    // Eigener Zeilenumbruch fuer mehrfarbigen/mehrgroessigen Text (schnell, ohne Steuerelemente). Umbruch nur an Leerzeichen;
    // zu lange Woerter werden zeichenweise umbrochen (nie abgeschnitten). Zeilenhoehe = hoechste Schrift der Zeile.
    // Absaetze, die mit "Kurzname:" beginnen (z. B. "PT: H1-T-0102 Notiz ..."), werden haengend eingerueckt: Fortsetzungszeilen
    // beginnen unter dem Text nach dem Doppelpunkt, damit lange Notizen uebersichtlich bleiben.
    class TextBlock
    {
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
        static readonly Regex Tok = new Regex(@"\n|[ \t]+|[^\s]+", RegexOptions.Compiled);

        class Metrics { public int Pad, SpaceW; public Dictionary<string, int> W = new Dictionary<string, int>(); }
        static readonly Dictionary<Font, Metrics> metrics = new Dictionary<Font, Metrics>();

        public readonly List<List<Item>> Lines = new List<List<Item>>();
        public readonly List<int> LineH = new List<int>();
        public int Height;

        static int Raw(Font f, string s)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, 200), Flags | TextFormatFlags.SingleLine | TextFormatFlags.Top).Width;
        }

        // MeasureText addiert je Messung einen festen Rand; er wird herausgerechnet.
        static Metrics M(Font f)
        {
            Metrics m;
            if (!metrics.TryGetValue(f, out m))
            {
                m = new Metrics();
                int a = Raw(f, "a");
                m.Pad = a - (Raw(f, "aa") - a);
                m.SpaceW = Raw(f, " ") - m.Pad;
                metrics[f] = m;
            }
            return m;
        }

        static int Width(Font f, string s)
        {
            Metrics m = M(f);
            int w;
            if (!m.W.TryGetValue(s, out w))
            {
                if (m.W.Count > 5000) m.W.Clear();   // begrenzt (viele verschiedene Woerter ueber lange Laufzeit)
                w = Raw(f, s) - m.Pad; m.W[s] = w;
            }
            return w;
        }

        class T { public int Kind; public string Text; public Piece P; public int W; }   // Kind: 0 Wort, 1 Leerraum, 2 Zeilenumbruch

        // Einrueckung eines Absatzes, der bei Token i beginnt: Breite von "Kurzname:" plus Leerzeichen, sonst 0.
        static int HangingIndent(List<T> toks, int i, int maxW)
        {
            if (i >= toks.Count || toks[i].Kind != 0) return 0;
            string w = toks[i].Text;
            if (w.Length < 2 || w.Length > 16 || w[w.Length - 1] != ':' || char.IsDigit(w[0])) return 0;
            int ind = toks[i].W + M(toks[i].P.Font).SpaceW;
            return ind < maxW / 2 ? ind : 0;
        }

        public static TextBlock Build(List<Piece> pieces, Font baseFont, int maxW)
        {
            TextBlock b = new TextBlock();
            List<T> toks = new List<T>();
            foreach (Piece p in pieces)
                foreach (Match m in Tok.Matches(p.Text))
                {
                    T t = new T(); t.Text = m.Value; t.P = p;
                    char c = m.Value[0];
                    t.Kind = c == '\n' ? 2 : (c == ' ' || c == '\t' ? 1 : 0);
                    t.W = t.Kind == 0 ? Width(p.Font, m.Value) : (t.Kind == 1 ? M(p.Font).SpaceW * m.Value.Length : 0);
                    toks.Add(t);
                }

            List<Item> cur = new List<Item>();
            b.Lines.Add(cur);
            int i = 0, indent = HangingIndent(toks, 0, maxW), x = 0;
            bool skipSpace = false;
            while (i < toks.Count)
            {
                T t = toks[i];
                if (t.Kind == 2) { cur = new List<Item>(); b.Lines.Add(cur); x = 0; skipSpace = false; i++; indent = HangingIndent(toks, i, maxW); }
                else if (t.Kind == 1)
                {
                    i++;
                    if (skipSpace && cur.Count == 0) continue;
                    if (x + t.W > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = indent; skipSpace = true; continue; }
                    Add(cur, t, t.Text, x, t.W); x += t.W;
                }
                else
                {
                    int j = i, total = 0;
                    while (j < toks.Count && toks[j].Kind == 0) { total += toks[j].W; j++; }
                    if (cur.Count > 0 && x + total > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = indent; }
                    skipSpace = false;
                    if (cur.Count == 0 && x > 0 && x + total > maxW) x = 0;   // Wort passt nicht hinter die Einrueckung: ohne Einrueckung setzen
                    if (x + total <= maxW || (cur.Count == 0 && total <= maxW)) { for (; i < j; i++) { Add(cur, toks[i], toks[i].Text, x, toks[i].W); x += toks[i].W; } }
                    else
                        for (; i < j; i++)                          // Wort laenger als die Spalte: zeichenweise
                            foreach (char ch in toks[i].Text)
                            {
                                int cw = Width(toks[i].P.Font, ch.ToString());
                                if (cur.Count > 0 && x + cw > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = indent; }
                                Add(cur, toks[i], ch.ToString(), x, cw); x += cw;
                            }
                }
            }
            foreach (List<Item> line in b.Lines)
            {
                int h = baseFont.Height;
                foreach (Item it in line) h = Math.Max(h, it.Font.Height);
                b.LineH.Add(h); b.Height += h;
            }
            return b;
        }

        static void Add(List<Item> line, T t, string text, int x, int w)
        {
            Item it = new Item();
            it.Text = text; it.Font = t.P.Font; it.X = x; it.W = w; it.Fore = t.P.Fore; it.HasBack = t.P.HasBack; it.Back = t.P.Back;
            line.Add(it);
        }

        // Wort (zusammenhaengende Textstuecke ohne Leerraum) an der Stelle (Koordinaten relativ zum Textursprung); "" bei Leerraum/ausserhalb
        public string WordAt(int px, int py)
        {
            int y = 0;
            for (int l = 0; l < Lines.Count; l++)
            {
                if (py >= y && py < y + LineH[l])
                {
                    List<Item> line = Lines[l];
                    int k = line.FindIndex(delegate(Item it) { return px >= it.X && px < it.X + it.W; });
                    if (k < 0 || line[k].Text.Trim().Length == 0) return "";
                    int a = k, b = k;
                    while (a > 0 && line[a - 1].Text.Trim().Length > 0) a--;
                    while (b < line.Count - 1 && line[b + 1].Text.Trim().Length > 0) b++;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = a; i <= b; i++) sb.Append(line[i].Text);
                    return sb.ToString();
                }
                y += LineH[l];
            }
            return "";
        }

        public void Draw(Graphics g, int x, int y)
        {
            for (int l = 0; l < Lines.Count; l++)
            {
                foreach (Item it in Lines[l])
                {
                    if (it.HasBack) g.FillRectangle(Theme.Current.Brush(it.Back), x + it.X, y, it.W, LineH[l]);
                    if (it.Text.Trim().Length > 0)
                        TextRenderer.DrawText(g, it.Text, it.Font, new Rectangle(x + it.X, y, it.W + 4, LineH[l]), it.Fore, Flags | TextFormatFlags.Bottom | TextFormatFlags.SingleLine);
                }
                y += LineH[l];
            }
        }
    }
}
