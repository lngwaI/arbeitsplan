using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Textstueck mit Farbe und optionaler Hintergrundmarkierung.
    class Piece
    {
        public string Text; public Color Fore; public bool HasBack; public Color Back;
        public Piece(string text, Color fore, bool hasBack, Color back) { Text = text; Fore = fore; HasBack = hasBack; Back = back; }
    }

    class Item { public string Text; public int X, W; public Color Fore; public bool HasBack; public Color Back; }

    // Eigener Zeilenumbruch fuer mehrfarbigen Text (schnell, ohne Steuerelemente). Umbruch nur an Leerzeichen;
    // zu lange Woerter werden zeichenweise umbrochen (nie abgeschnitten).
    class TextBlock
    {
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top;
        static readonly Regex Tok = new Regex(@"\n|[ \t]+|[^\s]+", RegexOptions.Compiled);
        static readonly Dictionary<string, int> widths = new Dictionary<string, int>();
        static Font widthFont;
        static int spaceW;

        public readonly List<List<Item>> Lines = new List<List<Item>>();
        public int LineH, Height;

        static int pad;   // MeasureText addiert je Messung einen festen Rand; er wird herausgerechnet

        static int Raw(Font f, string s)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, 100), Flags | TextFormatFlags.SingleLine).Width;
        }

        static int Width(Font f, string s)
        {
            if (f != widthFont)
            {
                widthFont = f; widths.Clear();
                int a = Raw(f, "a");
                pad = a - (Raw(f, "aa") - a);              // Rand = Breite("a") - Vorschub("a")
                spaceW = Raw(f, " ") - pad;
            }
            int w;
            if (!widths.TryGetValue(s, out w))
            {
                w = Raw(f, s) - pad;
                widths[s] = w;
            }
            return w;
        }

        class T { public int Kind; public string Text; public Piece P; public int W; }   // Kind: 0 Wort, 1 Leerraum, 2 Zeilenumbruch

        public static TextBlock Build(List<Piece> pieces, Font f, int maxW)
        {
            TextBlock b = new TextBlock();
            b.LineH = f.Height;
            Width(f, "a");                                        // initialisiert spaceW
            List<T> toks = new List<T>();
            foreach (Piece p in pieces)
                foreach (Match m in Tok.Matches(p.Text))
                {
                    T t = new T(); t.Text = m.Value; t.P = p;
                    char c = m.Value[0];
                    t.Kind = c == '\n' ? 2 : (c == ' ' || c == '\t' ? 1 : 0);
                    t.W = t.Kind == 0 ? Width(f, m.Value) : (t.Kind == 1 ? spaceW * m.Value.Length : 0);
                    toks.Add(t);
                }

            List<Item> cur = new List<Item>();
            b.Lines.Add(cur);
            int x = 0;
            bool skipSpace = false;
            int i = 0;
            while (i < toks.Count)
            {
                T t = toks[i];
                if (t.Kind == 2) { cur = new List<Item>(); b.Lines.Add(cur); x = 0; skipSpace = false; i++; }
                else if (t.Kind == 1)
                {
                    i++;
                    if (skipSpace && x == 0) continue;
                    if (x + t.W > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = 0; skipSpace = true; continue; }
                    Add(cur, t, t.Text, x, t.W); x += t.W;
                }
                else
                {
                    int j = i, total = 0;
                    while (j < toks.Count && toks[j].Kind == 0) { total += toks[j].W; j++; }
                    if (x > 0 && x + total > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = 0; }
                    skipSpace = false;
                    if (total <= maxW) { for (; i < j; i++) { Add(cur, toks[i], toks[i].Text, x, toks[i].W); x += toks[i].W; } }
                    else
                        for (; i < j; i++)                          // Wort laenger als die Spalte: zeichenweise
                            foreach (char ch in toks[i].Text)
                            {
                                int cw = Width(f, ch.ToString());
                                if (x > 0 && x + cw > maxW) { cur = new List<Item>(); b.Lines.Add(cur); x = 0; }
                                Add(cur, toks[i], ch.ToString(), x, cw); x += cw;
                            }
                }
            }
            b.Height = b.Lines.Count * b.LineH;
            return b;
        }

        static void Add(List<Item> line, T t, string text, int x, int w)
        {
            Item it = new Item();
            it.Text = text; it.X = x; it.W = w; it.Fore = t.P.Fore; it.HasBack = t.P.HasBack; it.Back = t.P.Back;
            line.Add(it);
        }

        public void Draw(Graphics g, Font f, int x, int y)
        {
            for (int l = 0; l < Lines.Count; l++)
                foreach (Item it in Lines[l])
                {
                    int ty = y + l * LineH;
                    if (it.HasBack) using (SolidBrush br = new SolidBrush(it.Back)) g.FillRectangle(br, x + it.X, ty, it.W, LineH);
                    if (it.Text.Trim().Length > 0) TextRenderer.DrawText(g, it.Text, f, new Point(x + it.X, ty), it.Fore, Flags);
                }
        }
    }
}
