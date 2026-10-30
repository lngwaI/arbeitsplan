using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Farben je Freeze-Anlage (Plan und Dialog): kraeftig fuer Text, blass fuer Zeilenhintergrund.
    static class FreezeStyle
    {
        static readonly Color[] strong = { Color.FromArgb(0, 105, 180), Color.FromArgb(0, 128, 88), Color.FromArgb(138, 72, 168) };
        static readonly Color[] tint = { Color.FromArgb(230, 240, 251), Color.FromArgb(230, 246, 238), Color.FromArgb(242, 235, 249) };
        static readonly Color[] tints = { Color.FromArgb(228, 243, 245), Color.FromArgb(239, 237, 249) };   // Hintergrund der Vorgaenge im Plan (wechselt)
        public static Color Anlage(int a) { return strong[Math.Abs(a - 1) % strong.Length]; }
        public static Color AnlageTint(int a) { return tint[Math.Abs(a - 1) % tint.Length]; }
        public static Color Tint(int i) { return tints[i % tints.Length]; }
        public static readonly Color Delay = Color.FromArgb(214, 100, 0);
    }

    // Textstueck eines Cycle-Eintrags, Eintrag (Cycle) und ein angezeigter Freeze-Vorgang; Y/H relativ zum Freeze-Bereich.
    class FrzRun { public string Text; public Font Font; public Color Color; public int X, W; }
    class FrzSeg { public List<FrzRun> Runs = new List<FrzRun>(); public int X, Y, W; }
    class FrzPart
    {
        public TextBlock Head, Note; public List<FrzSeg> Segs = new List<FrzSeg>();
        public int Y, H, BodyY, NoteY, SegH; public string Id = "", Tip = ""; public DateTime Origin; public bool Cont; public Color Stripe;
    }

    // Freeze-Bereich eines Blocks (Zeile mit RowInfo.Freeze): je Vorgang eine Kopfzeile "Bag-Freeze · Unterlauf X (aus Y) · Tanks ..." und darunter
    // die Cycles des Tages kompakt je Anlage ("C1 · FA1 · 12:00   C4 · FA1 · 19:00", Verzoegerung orange, geschaetzte Zeiten kursiv).
    // Fortsetzungen frueherer Tage kursiv grau "(aus Mo)" (nur berechnet, nie gespeichert). Unter dem Inhalt jedes Hauptlauf-Blocks die abgeleitete
    // graue Zeile "gesplittet in X1, X2 · Tanks ..." (SplitIndex, einmal je Wochenaufbau).
    partial class PlanView
    {
        public Action<CellData, string, DateTime> FreezeClicked;   // Block, Vorgangs-Id ("" = neuer Vorgang), Tag des Blocks mit dem Vorgang (bei Fortsetzung der Ursprung)
        public SplitIndex Splits = new SplitIndex();
        const int FrzIndent = 10, FrzGap = BufStyle.Gap, FrzPad = BufStyle.Pad, SegGap = 14;

        // Fortsetzungen (je Freeze-Zeile die letzten FreezeSet.LookBack Tage vor der Woche einmal laden) und Split-Index der Woche +-14 Tage
        void LoadFreeze(IDataStore store, DateTime start)
        {
            foreach (CellData c in Cells) c.Conts = new List<FreezeCycle>();
            Splits = SplitIndex.Build(store, start.AddDays(-14), start.AddDays(6 + 14));
            for (int r = 0; r < Rows.Count; r++)
            {
                if (!Rows[r].Freeze) continue;
                Dictionary<DateTime, Entry> es = new Dictionary<DateTime, Entry>();
                for (int k = -FreezeSet.LookBack; k <= 6; k++) es[start.AddDays(k)] = store.Get(start.AddDays(k), Rows[r].Id);
                foreach (CellData c in Cells)
                {
                    if (c.RowIndex != r) continue;
                    for (int k = 1; k <= FreezeSet.LookBack; k++)
                    {
                        DateTime o = c.Day.AddDays(-k);
                        foreach (FreezeCycle cy in FreezeSet.CyclesOn(es[o], c.Day)) { cy.OriginDay = o; c.Conts.Add(cy); }
                    }
                }
            }
        }

        static string Wd(DateTime d) { return d.ToString("ddd", Ui.De).TrimEnd('.'); }

        // "Bag-Freeze · Unterlauf X (aus Y) · Tanks ..." bzw. "Tank-Freeze · GWK1 K2 · 12:00–18:00 · Tanks ..."
        public static string FreezeHead(FreezeItem f) { return FreezeHead(f, false); }

        // brief = Fortsetzung: ohne "(aus Hauptlauf)" und Tanks (stehen im Tooltip und im Ursprungsblock)
        public static string FreezeHead(FreezeItem f, bool brief)
        {
            List<string> bits = new List<string>();
            if (f.Art == FreezeArt.Tank)
            {
                bits.Add("GWK" + f.Anlage + " K" + f.Kreislauf);
                if (f.Valid)
                {
                    FreezeCycle c0 = FreezeCalc.Cycles(f)[0];
                    bits.Add(c0.Actual.ToString("HH:mm") + "–" + c0.End.ToString("HH:mm") + (c0.End.Date != c0.Actual.Date ? " (+" + (int)(c0.End.Date - c0.Actual.Date).TotalDays + " T)" : ""));
                }
            }
            if (f.SubNo.Length > 0) bits.Add("Unterlauf " + f.SubNo + (f.MainRef.Length > 0 && !brief ? " (aus " + f.MainRef + ")" : ""));
            else if (f.MainRef.Length > 0) bits.Add("Lauf " + f.MainRef);
            if (f.Tanks.Length > 0 && !brief) bits.Add("Tanks " + f.Tanks);
            return (f.Art == FreezeArt.Tank ? "Tank-Freeze" : "Bag-Freeze") + (bits.Count > 0 ? " · " + string.Join(" · ", bits.ToArray()) : "");
        }

        // Tooltip: Eckdaten, verspaetete Cycles, Hinweise
        static string FreezeTip(FreezeItem f, List<FreezeCycle> today, bool cont, DateTime origin)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(FreezeHead(f));
            if (f.Valid)
                sb.Append("\nStart ").Append(FreezeCalc.Short(f.Start1to3[0])).Append(", Dauer ").Append(FreezeCalc.Dur(f.DauerMin))
                  .Append(f.Art == FreezeArt.Bag ? ", " + FreezeCalc.Count(f) + " Cycles, Abstand " + FreezeCalc.Dur(f.AbstandMin) : "");
            foreach (FreezeCycle c in today)
                if (c.DelayMin != 0) sb.Append("\n").Append(c.Label).Append(" ").Append(FreezeCalc.DelayText(c.DelayMin)).Append(c.HasIst ? " (eingetragen)" : " (folgt aus früherem Cycle)");
            foreach (FreezeCycle c in today) if (c.Estimated) { sb.Append("\nC2/C3 ohne Eingabe: geschätzt (kursiv)"); break; }
            foreach (string h in FreezeCalc.Hints(f)) sb.Append("\nHinweis: ").Append(h);
            sb.Append(cont ? "\nFortsetzung aus " + Ui.DayText(origin) + " – Klick öffnet den Vorgang." : "\nKlick: Vorgang bearbeiten");
            return sb.ToString();
        }

        // Breite wie TextBlock: die feste Zusatzbreite jeder Messung (MeasureText) wird herausgerechnet
        FrzRun Run(string text, Font f, Color col) { FrzRun r = new FrzRun(); r.Text = text; r.Font = f; r.Color = col; r.W = TextW(text, f) - (2 * TextW("a", f) - TextW("aa", f)); return r; }

        // Ein Cycle als Eintrag "C1 · FA1 · 12:00"; zu breit fuer die Spalte: kompakt ohne Punkte "C1 FA1 12:00"
        FrzSeg Seg(FreezeCycle c, bool cont, int maxW)
        {
            Theme th = Theme.Current;
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font), ital = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Italic, Font);
            Font f = cont || c.Estimated ? ital : plain, fa = cont ? ital : bold;
            Color dark = cont ? th.Muted : Color.Black, ac = cont ? th.Muted : FreezeStyle.Anlage(c.Anlage);
            int sp = TextW("a a", plain) - TextW("aa", plain);
            for (int dots = 1; dots >= 0; dots--)
            {
                FrzSeg s = new FrzSeg(); int x = 0;
                List<FrzRun> rs = new List<FrzRun>();
                rs.Add(Run(c.Label, cont ? ital : bold, dark)); if (dots > 0) rs.Add(Run("·", plain, th.Muted));
                rs.Add(Run("FA" + c.Anlage, fa, ac)); if (dots > 0) rs.Add(Run("·", plain, th.Muted));
                rs.Add(Run(c.Actual.ToString("HH:mm"), f, dark));
                foreach (FrzRun r in rs) { r.X = x; x += r.W + sp; s.Runs.Add(r); }
                s.W = x - sp;
                if (s.W <= maxW || dots == 0) return s;
            }
            return null;
        }

        // Verzoegerung "(+7 h)" als eigener Eintrag direkt hinter dem Cycle (passt er nicht mehr in die Zeile, steht er darunter)
        FrzSeg DelaySeg(FreezeCycle c, bool cont)
        {
            FrzSeg s = new FrzSeg(); FrzRun r = Run("(" + FreezeCalc.DelayText(c.DelayMin) + ")", cont ? FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Italic, Font) : bold, FreezeStyle.Delay);
            s.Runs.Add(r); s.W = r.W;
            return s;
        }

        // Eintraege je Anlage (aufsteigend) in Zeilen umbrechen; Rueckgabe = Hoehe
        int FlowSegs(FrzPart p, List<FreezeCycle> cys, bool cont, int maxW)
        {
            int lh = Font.Height + 1, y = 0, x = 0, gap = TextW("a a", Font) - TextW("aa", Font);
            for (int a = 1; a <= FreezeCalc.Anlagen; a++)
            {
                bool first = true;
                List<FreezeCycle> l = cys.FindAll(delegate(FreezeCycle c) { return c.Anlage == a; });
                l.Sort(delegate(FreezeCycle u, FreezeCycle v) { return u.Actual.CompareTo(v.Actual); });
                foreach (FreezeCycle c in l)
                {
                    FrzSeg s = Seg(c, cont, maxW);
                    if (first) { if (x > 0) { y += lh; x = 0; } first = false; }
                    else if (x + SegGap + s.W > maxW && x > 0) { y += lh; x = 0; }
                    else if (x > 0) x += SegGap;
                    s.X = x; s.Y = y; x += s.W; p.Segs.Add(s);
                    if (c.DelayMin == 0) continue;
                    FrzSeg d = DelaySeg(c, cont);
                    if (x + gap + d.W > maxW) { y += lh; x = 0; d.X = 0; } else d.X = x + gap;
                    d.Y = y; x = d.X + d.W; p.Segs.Add(d);
                }
            }
            return p.Segs.Count == 0 ? 0 : y + lh;
        }

        List<Piece> FreezePieces(string text, bool cont, bool search, bool boldFont, out int hits)
        {
            Color[] fore = new Color[text.Length];
            Font[] fnt = new Font[text.Length];
            int[] mode = new int[text.Length];
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font), ital = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Italic, Font);
            for (int i = 0; i < text.Length; i++) { fore[i] = cont ? Theme.Current.Muted : Color.Black; fnt[i] = cont ? ital : boldFont ? bold : plain; }
            hits = search && !cont ? MarkQuery(text, mode) : 0;
            return Group(text, fore, fnt, mode);
        }

        FrzPart MakePart(CellData c, FreezeItem f, List<FreezeCycle> cys, bool cont, DateTime origin, int aw, ref int hits)
        {
            int h2;
            FrzPart p = new FrzPart(); p.Id = f.Id; p.Origin = cont ? origin : c.Day; p.Cont = cont;
            string head = (cont ? "(aus " + Wd(origin) + ") " : "") + FreezeHead(f, cont);
            p.Head = TextBlock.Build(FreezePieces(head, cont, !c.Editing, true, out h2), Font, aw - 2); hits += h2;
            p.BodyY = FrzPad + p.Head.Height;
            p.SegH = f.Art == FreezeArt.Bag ? FlowSegs(p, cys, cont, aw - FrzIndent - 2) : 0;
            int h = p.BodyY + p.SegH;
            if (!cont && f.Note.Length > 0)
            {
                p.Note = TextBlock.Build(FreezePieces(f.Note, false, !c.Editing, false, out h2), Font, aw - FrzIndent - 2); hits += h2;
                p.NoteY = h; h += p.Note.Height;
            }
            p.H = h + FrzPad;
            p.Stripe = cont ? Theme.Current.Muted : FreezeStyle.Anlage(f.Art == FreezeArt.Tank ? 2 : 1);
            p.Tip = FreezeTip(f, cys, cont, origin);
            return p;
        }

        void BuildFreeze(CellData c, int tw, out int hits)
        {
            hits = 0;
            c.FrzParts.Clear(); c.FrzH = 0; c.SplitTB = null; c.SplitH = 0;
            int aw = tw - StripeW;
            if (Rows[c.RowIndex].Freeze && (c.Cur.Freezes.Count > 0 || c.Conts.Count > 0))
            {
                List<FrzPart> parts = new List<FrzPart>(); List<DateTime> first = new List<DateTime>();
                foreach (FreezeItem f in c.Cur.Freezes)
                {
                    List<FreezeCycle> cys = FreezeCalc.Cycles(f).FindAll(delegate(FreezeCycle x) { return x.Day == c.Day.Date; });
                    parts.Add(MakePart(c, f, cys, false, c.Day, aw, ref hits)); first.Add(cys.Count > 0 ? cys[0].Actual : c.Day);
                }
                List<string> seen = new List<string>();
                foreach (FreezeCycle x in c.Conts)
                {
                    string key = x.OriginDay.Ticks + "|" + x.Item.Id;
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    List<FreezeCycle> cys = c.Conts.FindAll(delegate(FreezeCycle q) { return q.OriginDay == x.OriginDay && q.Item.Id == x.Item.Id; });
                    parts.Add(MakePart(c, x.Item, cys, true, x.OriginDay, aw, ref hits)); first.Add(cys[0].Actual);
                }
                // nach erster Zeit des Tages sortieren (Fortsetzungen meist zuerst)
                int[] ix = new int[parts.Count]; for (int i = 0; i < ix.Length; i++) ix[i] = i;
                Array.Sort(ix, delegate(int a, int b) { int d = first[a].CompareTo(first[b]); return d != 0 ? d : a.CompareTo(b); });
                int y = TV;
                foreach (int i in ix) { FrzPart p = parts[i]; p.Y = y; c.FrzParts.Add(p); y += p.H + FrzGap; }
                y -= FrzGap;
                c.FrzH = y + TV + 1;   // + Trennlinie
            }
            if (Splits.Count > 0)
            {
                List<string> ls = Splits.LinesFor(c.Cur);
                if (ls.Count > 0)
                {
                    Font ital = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Italic, Font);
                    c.SplitTB = TextBlock.Build(new List<Piece>(new[] { new Piece(string.Join("\n", ls.ToArray()), ital, Theme.Current.Muted, false, Color.Empty) }), Font, tw);
                    c.SplitH = c.SplitTB.Height + 4;
                }
            }
        }

        void PaintFreeze(Graphics g, CellData c, Color back)
        {
            Theme th = Theme.Current;
            if (c.RFrz.Height > 0)
            {
                Fill(g, back, c.RFrz);
                Region oldClip = g.Clip; g.SetClip(c.RFrz, System.Drawing.Drawing2D.CombineMode.Intersect);   // nichts ragt in die Nachbarspalte
                int x = c.RFrz.X + StripeW + 3, own = 0;
                for (int i = 0; i < c.FrzParts.Count; i++)
                {
                    FrzPart p = c.FrzParts[i];
                    int y = c.RFrz.Y + p.Y;
                    if (!p.Cont) Fill(g, FreezeStyle.Tint(own++), new Rectangle(c.RFrz.X, y, c.RFrz.Width, p.H));
                    Fill(g, p.Stripe, new Rectangle(c.RFrz.X, y, StripeW, p.H));
                    p.Head.Draw(g, x, y + FrzPad);
                    int bx = x + FrzIndent, by = y + p.BodyY;
                    foreach (FrzSeg s in p.Segs)
                        foreach (FrzRun r in s.Runs)
                            TextRenderer.DrawText(g, r.Text, r.Font, new Rectangle(bx + s.X + r.X, by + s.Y, r.W + 6, Font.Height + 1), r.Color,
                                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Bottom);
                    if (p.Note != null) p.Note.Draw(g, bx, y + p.NoteY);
                    if (i < c.FrzParts.Count - 1) g.DrawLine(th.Pen(BufStyle.Line, 1), c.RFrz.X + 2, y + p.H + FrzGap / 2, c.RFrz.Right - 3, y + p.H + FrzGap / 2);
                }
                g.DrawLine(th.Pen(th.Separator, 1), c.RFrz.X, c.RFrz.Bottom - 1, c.RFrz.Right, c.RFrz.Bottom - 1);
                g.Clip = oldClip; oldClip.Dispose();
            }
            if (c.RSplit.Height > 0 && c.SplitTB != null)   // abgeleitet (nicht gespeichert): grau kursiv unter dem Inhalt
            {
                Fill(g, back, c.RSplit);
                g.DrawLine(th.Pen(th.Separator, 1), c.RSplit.X + 2, c.RSplit.Y, c.RSplit.Right - 3, c.RSplit.Y);
                c.SplitTB.Draw(g, c.RSplit.X + TP, c.RSplit.Y + 3);
            }
        }

        // Index des Vorgangs unter p (-1 = keiner)
        int FrzAt(CellData c, Point p)
        {
            if (Collapsed[c.RowIndex] || c.FrzParts.Count == 0 || !c.RFrz.Contains(p)) return -1;
            for (int i = 0; i < c.FrzParts.Count; i++)
            {
                FrzPart b = c.FrzParts[i];
                if (p.Y >= c.RFrz.Y + b.Y && p.Y < c.RFrz.Y + b.Y + b.H + FrzGap) return i;
            }
            return -1;
        }

#if TESTHOOKS
        // Selbsttest: Punkt im Vorgang index (Klick wie mit der Maus) bzw. auf dem Knopf "+ Freeze"
        public Point FreezePoint(CellData c, int index)
        {
            if (index < 0 || index >= c.FrzParts.Count) return Point.Empty;
            return new Point(c.RFrz.X + 12, c.RFrz.Y + c.FrzParts[index].Y + 2);
        }
        public Point FreezeAddPoint(CellData c)
        {
            foreach (CheckSlot s in slots[c.RowIndex]) if (s.Freeze) return new Point(c.RChecks.X + s.R.X + s.R.Width / 2, c.RChecks.Y + s.R.Y + s.R.Height / 2);
            return Point.Empty;
        }
#endif
    }
}
