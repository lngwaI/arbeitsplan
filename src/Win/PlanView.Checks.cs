using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Platz eines Eintrags der Pruefleiste (Def -1 = Variante), relativ zur Leiste.
    class CheckSlot { public int Def = -1; public Rectangle R; }

    // Rundgaenge einer KW in der Leiste oberhalb der Zeilen (Spalten First..Last gehoeren zu dieser KW).
    class RoundGroup
    {
        public DateTime Monday;
        public int First, Last;
        public List<RoundDef> Defs = new List<RoundDef>();
        public HashSet<string> Done = new HashSet<string>();
        public bool Collapsed;
        public Rectangle R, RTitle;
        public Rectangle[] RItems = new Rectangle[0];

        public int DoneCount { get { int n = 0; foreach (RoundDef d in Defs) if (Done.Contains(d.Key)) n++; return n; } }
        public bool Complete { get { return DoneCount == Defs.Count; } }
        public string Title { get { return "Rundgänge KW" + IsoWeek.Number(Monday) + "  " + DoneCount + "/" + Defs.Count; } }
    }

    // Pruefleiste je Block (Variante + Pflichtpunkte, ersetzt das LKZ-Feld), Hinweis "KW offen" im Kopf, Rundgangsleiste.
    partial class PlanView
    {
        public List<CheckDef>[] Defs = new List<CheckDef>[0];   // je Zeile (MainForm: aus DutyConfig + LKZ aus der Blockstruktur)
        public List<RoundGroup> Rounds = new List<RoundGroup>();
        public Action<CellData, string> MarkClicked;            // Schluessel (CheckDef.LkzKey = LKZ-Haken)
        public Action<CellData, Point> VariantClicked;          // Bildschirmpunkt fuer das Auswahlmenue
        public Action<RoundGroup, string> RoundClicked;
        public Action<RoundGroup> RoundToggled;
        public RoundGroup MenuRound;                            // Rechtsklick auf die Rundgangsleiste (Verlauf)
        CheckSlot[][] slots = new CheckSlot[0][];
        int roundsH;

        List<CheckDef> DefsOf(int r) { return r < Defs.Length && Defs[r] != null ? Defs[r] : new List<CheckDef>(); }
        bool HasChecks(int r) { return DefsOf(r).Count > 0 || Rows[r].Variants.Length > 0; }
        bool Single(int r) { return DefsOf(r).Count == 1 && Rows[r].Variants.Length == 0; }

        // Eintraege fliessen von links nach rechts und brechen um; Rueckgabe = Hoehe der Leiste (0 = keine).
        int LayoutChecks(int r, int iw)
        {
            List<CheckSlot> l = new List<CheckSlot>();
            if (!HasChecks(r)) { slots[r] = l.ToArray(); return 0; }
            List<CheckDef> d = DefsOf(r);
            int x = TP, line = 0, max = iw - 2;
            for (int i = Rows[r].Variants.Length > 0 ? -1 : 0; i < d.Count; i++)
            {
                int w;
                if (i < 0)
                {
                    w = TextW("Herstellung?", Font);
                    foreach (string v in Rows[r].VariantList()) w = Math.Max(w, TextW(v, Font));
                    w = Math.Max(w, TextW(VariantSet.Display(Rows[r].Variants), Font));   // mehrere an einem Tag ("mobil + stationaer")
                    w += 16;
                }
                else w = TextW(Single(r) ? d[i].Name + "?" : d[i].Short, Font) + 22;
                if (x + w > max && x > TP) { line++; x = TP; }
                CheckSlot s = new CheckSlot(); s.Def = i; s.R = new Rectangle(x, line * LkzH, Math.Min(w, max - x), LkzH);
                l.Add(s); x += w + 4;
            }
            slots[r] = l.ToArray();
            return (line + 1) * LkzH;
        }

        static bool On(CellData c, CheckDef d) { return d.Key == CheckDef.LkzKey ? c.Lkz : c.Marks.Contains(d.Key); }

        DayState StateOf(CellData c, int i) { return c.States != null && i < c.States.Length ? c.States[i] : DayState.WeekOpen; }

        void PaintChecks(Graphics g, CellData c, Color back)
        {
            Theme th = Theme.Current;
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            int r = c.RowIndex;
            List<CheckDef> d = DefsOf(r);
            bool single = Single(r);
            Fill(g, back, c.RChecks);
            foreach (CheckSlot s in slots[r])
            {
                Rectangle sr = new Rectangle(c.RChecks.X + s.R.X, c.RChecks.Y + s.R.Y, s.R.Width, s.R.Height);
                if (s.Def < 0)
                {
                    bool none = c.Variant.Length == 0;
                    TextRenderer.DrawText(g, none ? "Herstellung?" : VariantSet.Display(c.Variant), Font, new Rectangle(sr.X, sr.Y, sr.Width - 12, sr.Height), none ? th.Muted : th.HeadText, tf);
                    int ax = sr.Right - 10, ay = sr.Y + sr.Height / 2 - 1;
                    g.FillPolygon(th.Brush(th.Arrow), new Point[] { new Point(ax, ay), new Point(ax + 7, ay), new Point(ax + 3, ay + 4) });
                    continue;
                }
                CheckDef def = d[s.Def];
                DayState st = StateOf(c, s.Def);
                bool on = On(c, def), box = true;
                Color fill = Color.Empty, fg = th.LkzText;
                string text = single ? def.Name + "?" : def.Short;
                if (on) { fill = th.LkzOn; if (single) text = def.Name; }
                else if (st == DayState.Missing) { fill = th.CheckMissing; fg = th.CheckMissingText; }
                else if (st == DayState.Auto) { fill = th.CheckAuto; box = false; }
                else if (st == DayState.NA) { fg = th.Muted; box = false; }
                else if (st == DayState.NotNeeded) { fg = th.Muted; if (single) text = def.Short + " nicht nötig"; }
                if (!fill.IsEmpty) Fill(g, fill, single ? c.RChecks : new Rectangle(sr.X - 2, sr.Y + 2, sr.Width + 2, sr.Height - 4));
                TextRenderer.DrawText(g, text, Font, new Rectangle(sr.X, sr.Y, sr.Width - 16, sr.Height), fg, tf);
                Point cp = new Point(sr.Right - 14, sr.Y + (sr.Height - 13) / 2);
                if (box) th.DrawCheck(g, cp, on);
                else if (st == DayState.Auto) TextRenderer.DrawText(g, "✓", bold, new Rectangle(cp.X, sr.Y, 14, sr.Height), th.DoneText, tf);
            }
            g.DrawLine(th.Pen(th.Separator, 1), c.RChecks.X, c.RChecks.Bottom - 1, c.RChecks.Right, c.RChecks.Bottom - 1);
        }

        // Eintrag der Pruefleiste unter p (null = keiner)
        CheckSlot SlotAt(CellData c, Point p)
        {
            if (Collapsed[c.RowIndex] || !c.RChecks.Contains(p)) return null;
            foreach (CheckSlot s in slots[c.RowIndex])
                if (new Rectangle(c.RChecks.X + s.R.X, c.RChecks.Y + s.R.Y, s.R.Width, s.R.Height).Contains(p)) return s;
            return null;
        }

        static string StateText(DayState s)
        {
            switch (s)
            {
                case DayState.Done: return "erledigt";
                case DayState.Auto: return "mit Herstellung erledigt";
                case DayState.Missing: return "fehlt";
                case DayState.Due: return "fällig";
                case DayState.NotNeeded: return "nicht nötig (in dieser KW erledigt)";
                case DayState.WeekOpen: return "in dieser KW noch offen";
            }
            return "entfällt";
        }

        string CheckTip(CellData c, CheckSlot s)
        {
            if (s.Def < 0) return "Herstellung: " + (c.Variant.Length > 0 ? VariantSet.Display(c.Variant) : "nicht gewählt") + " (Klick = wählen, mehrere möglich)";
            CheckDef d = DefsOf(c.RowIndex)[s.Def];
            return d.Name + ": " + (On(c, d) ? "erledigt" : StateText(StateOf(c, s.Def)));
        }

        // Hinweis "LKZ KW44 offen" rechts im Kopf (orange), kuerzer, wenn der Platz fehlt; Rueckgabe = belegte Breite.
        int PaintNote(Graphics g, CellData c, Rectangle nr)
        {
            if (c.Note.Length == 0) return 0;
            string t = c.Note;
            int room = nr.Width - Math.Min(TextW(Rows[c.RowIndex].Name, bold), 60) - 6;
            if (TextW(t, Font) > room) t = "offen";
            int w = Math.Min(TextW(t, Font), Math.Max(0, nr.Width - 20));
            TextRenderer.DrawText(g, t, Font, new Rectangle(nr.Right - w, nr.Y, w, nr.Height), Theme.Current.Dot, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            return w + 6;
        }

        // ---------- Rundgaenge ----------

        // Gruppen nebeneinander (je KW so breit wie ihre Spalten); Eintraege brechen um, schmale Gruppen stapeln dadurch senkrecht.
        int LayoutRounds(int y)
        {
            roundsH = 0;
            if (Rounds.Count == 0) return 0;
            int rh = Theme.Current.RoundH;
            foreach (RoundGroup gr in Rounds)
            {
                int x0 = ColLeft(gr.First), w = ColLeft(gr.Last) + ColW - x0, max = x0 + w - 6;
                gr.R = new Rectangle(x0, y, w, 0);
                int tw = 16 + TextW(gr.Title, bold) + 8, x = x0 + 6, line = 0;
                gr.RTitle = new Rectangle(x, y + 2, Math.Min(tw, w - 8), rh);
                x += tw + 6;
                gr.RItems = new Rectangle[gr.Collapsed ? 0 : gr.Defs.Count];
                for (int i = 0; i < gr.RItems.Length; i++)
                {
                    int iw = 18 + TextW(gr.Defs[i].Name, Font) + 6;
                    if (x + iw > max && x > x0 + 6) { line++; x = x0 + 6; }
                    gr.RItems[i] = new Rectangle(x, y + 2 + line * rh, Math.Min(iw, max - x), rh);
                    x += iw + 6;
                }
                roundsH = Math.Max(roundsH, (line + 1) * rh + 4);
            }
            foreach (RoundGroup gr in Rounds) gr.R.Height = roundsH;
            return roundsH + Gap;
        }

        void PaintRounds(Graphics g, Rectangle clip)
        {
            Theme th = Theme.Current;
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            foreach (RoundGroup gr in Rounds)
            {
                if (!gr.R.IntersectsWith(clip)) continue;
                Fill(g, th.CellBorder, gr.R);
                Fill(g, th.RowHead, new Rectangle(gr.R.X + 1, gr.R.Y + 1, gr.R.Width - 2, gr.R.Height - 2));
                Rectangle t = gr.RTitle;
                int ax = t.X, ay = t.Y + t.Height / 2 - 3;
                g.FillPolygon(th.Brush(th.Arrow), gr.Collapsed ? new Point[] { new Point(ax + 2, ay - 1), new Point(ax + 2, ay + 6), new Point(ax + 6, ay + 3) }
                                                               : new Point[] { new Point(ax, ay + 1), new Point(ax + 7, ay + 1), new Point(ax + 3, ay + 5) });
                TextRenderer.DrawText(g, gr.Title, bold, new Rectangle(t.X + 14, t.Y, t.Width - 14, t.Height), gr.Complete ? th.DoneText : th.Dot, tf);
                for (int i = 0; i < gr.RItems.Length; i++)
                {
                    Rectangle ir = gr.RItems[i];
                    bool on = gr.Done.Contains(gr.Defs[i].Key);
                    if (on) Fill(g, th.LkzOn, new Rectangle(ir.X - 2, ir.Y + 2, ir.Width + 2, ir.Height - 4));
                    th.DrawCheck(g, new Point(ir.X, ir.Y + (ir.Height - 13) / 2), on);
                    TextRenderer.DrawText(g, gr.Defs[i].Name, Font, new Rectangle(ir.X + 18, ir.Y, ir.Width - 18, ir.Height), th.HeadText, tf);
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
                    if (gr.RItems[i].Contains(p)) return gr.Defs[i].Name + " KW" + IsoWeek.Number(gr.Monday) + ": " + (gr.Done.Contains(gr.Defs[i].Key) ? "erledigt" : "offen") + " (Rechtsklick = Verlauf)";
                return gr.Collapsed ? "Klick = Rundgänge anzeigen" : "Klick auf die Überschrift = zuklappen";
            }
            return "";
        }
    }
}
