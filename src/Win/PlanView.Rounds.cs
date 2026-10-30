using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Rundgaenge einer KW in der Leiste oberhalb der Zeilen (Spalten First..Last gehoeren zu dieser KW).
    class RoundGroup
    {
        public DateTime Monday;
        public int First, Last;
        public List<RoundDef> Defs = new List<RoundDef>();
        public HashSet<string> Done = new HashSet<string>();
        public Dictionary<string, WeekMark> Marks = new Dictionary<string, WeekMark>();   // erledigte: wann/von wem (At/By)
        public bool Collapsed;
        public Rectangle R, RTitle;
        public Rectangle[] RItems = new Rectangle[0];

        public int DoneCount { get { int n = 0; foreach (RoundDef d in Defs) if (Done.Contains(d.Key)) n++; return n; } }
        public bool Complete { get { return DoneCount == Defs.Count; } }
        public string Title { get { return "Rundgänge KW" + IsoWeek.Number(Monday) + "   " + DoneCount + "/" + Defs.Count + (Complete ? " erledigt" : ""); } }

        // "Di 27.10. MUMA" hinter einem erledigten Rundgang ("" = unbekannt/offen)
        public string Stamp(string key)
        {
            WeekMark m;
            if (!Done.Contains(key) || !Marks.TryGetValue(key, out m) || m.At == DateTime.MinValue) return "";
            return (m.At.ToString("ddd dd.MM.", Ui.De) + " " + m.By).Trim();
        }
    }

    // Rundgangsleiste: gross, immer sichtbar (lokal zuklappbar), vollstaendig = gruener Rahmen.
    partial class PlanView
    {
        public List<RoundGroup> Rounds = new List<RoundGroup>();
        public Action<RoundGroup, string> RoundClicked;
        public Action<RoundGroup> RoundToggled;
        public RoundGroup MenuRound;                            // Rechtsklick auf die Rundgangsleiste (Verlauf)
        int roundsH;
        Font roundFont;
        Font RoundFont { get { if (roundFont == null) roundFont = new Font(Font.FontFamily, Theme.Current.RoundFontSize); return roundFont; } }

        // Gruppen nebeneinander (je KW so breit wie ihre Spalten); Eintraege brechen um, schmale Gruppen stapeln dadurch senkrecht.
        int LayoutRounds(int y)
        {
            roundsH = 0;
            if (Rounds.Count == 0) return 0;
            int rh = Theme.Current.RoundH, bs = Theme.Current.CheckBox;
            foreach (RoundGroup gr in Rounds)
            {
                int x0 = ColLeft(gr.First), w = ColLeft(gr.Last) + ColW - x0, max = x0 + w - 8;
                gr.R = new Rectangle(x0, y, w, 0);
                int tw = 16 + TextW(gr.Title, HeadFont) + 10, x = x0 + 8, line = 0;
                gr.RTitle = new Rectangle(x, y + 3, Math.Min(tw, w - 10), rh);
                x += tw + 8;
                gr.RItems = new Rectangle[gr.Collapsed ? 0 : gr.Defs.Count];
                int stw = 6 + Math.Max(TextW("Mo 28.10. WWWW", Font), TextW("Mo 28.10. MMMMM", Font));   // Platz fuer "Tag Datum Kuerzel" immer reservieren: Hoehe aendert sich beim Anklicken nicht
                for (int i = 0; i < gr.RItems.Length; i++)
                {
                    int iw = bs + 6 + TextW(gr.Defs[i].Name, RoundFont) + stw + 10;
                    if (x + iw > max && x > x0 + 8) { line++; x = x0 + 8; }
                    gr.RItems[i] = new Rectangle(x, y + 3 + line * rh, Math.Min(iw, max - x), rh);
                    x += iw + 8;
                }
                roundsH = Math.Max(roundsH, (line + 1) * rh + 6);
            }
            foreach (RoundGroup gr in Rounds) gr.R.Height = roundsH;
            return roundsH + Gap;
        }

        void PaintRounds(Graphics g, Rectangle clip)
        {
            Theme th = Theme.Current;
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            int bs = th.CheckBox;
            foreach (RoundGroup gr in Rounds)
            {
                if (!gr.R.IntersectsWith(clip)) continue;
                int bw = gr.Complete ? 2 : 1;   // vollstaendig: gruener 2-px-Rahmen
                Fill(g, gr.Complete ? th.DoneText : th.HeadEdge, gr.R);
                Fill(g, gr.Complete ? th.CheckAuto : th.PlanHead, new Rectangle(gr.R.X + bw, gr.R.Y + bw, gr.R.Width - 2 * bw, gr.R.Height - 2 * bw));
                Rectangle t = gr.RTitle;
                int ax = t.X, ay = t.Y + t.Height / 2 - 3;
                g.FillPolygon(th.Brush(th.Arrow), gr.Collapsed ? new Point[] { new Point(ax + 2, ay - 1), new Point(ax + 2, ay + 6), new Point(ax + 6, ay + 3) }
                                                               : new Point[] { new Point(ax, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 5) });
                TextRenderer.DrawText(g, gr.Title, HeadFont, new Rectangle(t.X + 14, t.Y, t.Width - 14, t.Height), gr.Complete ? th.DoneText : th.Dot, tf);
                for (int i = 0; i < gr.RItems.Length; i++)
                {
                    Rectangle ir = gr.RItems[i];
                    bool on = gr.Done.Contains(gr.Defs[i].Key);
                    Rectangle chip = new Rectangle(ir.X - 3, ir.Y + 2, ir.Width + 1, ir.Height - 4);
                    Fill(g, on ? th.LkzOn : Color.White, chip);
                    g.DrawRectangle(th.Pen(on ? th.CheckMark : th.ChipOff, 1), chip);
                    th.DrawCheck(g, new Point(ir.X + 1, ir.Y + (ir.Height - bs) / 2), on, bs);
                    int nw = TextW(gr.Defs[i].Name, RoundFont) + 2, tx = ir.X + bs + 6;
                    TextRenderer.DrawText(g, gr.Defs[i].Name, RoundFont, new Rectangle(tx, ir.Y, Math.Min(nw, ir.Right - tx - 4), ir.Height), th.HeadText, tf);
                    string st = gr.Stamp(gr.Defs[i].Key);
                    if (st.Length > 0 && tx + nw + 6 < ir.Right - 4)
                        TextRenderer.DrawText(g, st, Font, new Rectangle(tx + nw + 4, ir.Y, ir.Right - tx - nw - 8, ir.Height), th.DoneText, tf);
                }
            }
        }

        // Klick in der Rundgangsleiste: true = behandelt
        bool RoundsMouse(Point p, MouseButtons b)
        {
            MenuRound = null;
            foreach (RoundGroup gr in Rounds)
            {
                if (!gr.R.Contains(p)) continue;
                MenuRound = gr;
                if (b != MouseButtons.Left) return true;
                if (gr.RTitle.Contains(p)) { if (RoundToggled != null) RoundToggled(gr); return true; }
                for (int i = 0; i < gr.RItems.Length; i++)
                    if (gr.RItems[i].Contains(p) && RoundClicked != null) { RoundClicked(gr, gr.Defs[i].Key); return true; }
                return true;
            }
            return false;
        }

        string RoundTip(Point p)
        {
            foreach (RoundGroup gr in Rounds)
            {
                if (!gr.R.Contains(p)) continue;
                for (int i = 0; i < gr.RItems.Length; i++)
                {
                    if (!gr.RItems[i].Contains(p)) continue;
                    string k = gr.Defs[i].Key, st = "offen";
                    WeekMark m;
                    if (gr.Done.Contains(k))
                        st = gr.Marks.TryGetValue(k, out m) && m.At != DateTime.MinValue ? "erledigt am " + m.At.ToString("ddd dd.MM.yyyy HH:mm", Ui.De) + (m.By.Length > 0 ? " von " + m.By : "") : "erledigt";
                    return gr.Defs[i].Name + " KW" + IsoWeek.Number(gr.Monday) + ": " + st + " (Rechtsklick = Verlauf)" + (BufMade && k == MadeRules.RoundKey ? "\n" + MadeRules.RoundTipText : "");
                }
                return gr.Collapsed ? "Klick = Rundgänge anzeigen" : "Klick auf die Überschrift = zuklappen (nur an diesem PC)";
            }
            return "";
        }
    }
}
