using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Platz eines Eintrags der Pruefleiste, relativ zur Leiste: Knopf "+ Puffer" (Add) oder Pflichtpunkt (Def).
    class CheckSlot { public int Def = -1; public bool Add, Freeze; public int W; public Rectangle R; }   // Freeze = Knopf "+ Freeze" (Freeze-Zeile)

    // Pruefleiste je Block: Knopf "+ Puffer" (Zeilen mit Puffer-Typen), Pflichtpunkte mit Zustand
    // (Tagesprobe auto/fehlt/entfaellt …); nur LKZ: ein grosses, zentriertes Feld "LKZ gemessen?".
    partial class PlanView
    {
        public List<CheckDef>[] Defs = new List<CheckDef>[0];   // je Zeile (MainForm: aus DutyConfig + LKZ aus der Blockstruktur)
        public Action<CellData, string> MarkClicked;            // Schluessel (CheckDef.LkzKey = LKZ-Haken)
        CheckSlot[][] slots = new CheckSlot[0][];

        List<CheckDef> DefsOf(int r) { return r < Defs.Length && Defs[r] != null ? Defs[r] : new List<CheckDef>(); }
        bool HasChecks(int r) { return DefsOf(r).Count > 0 || Rows[r].Variants.Length > 0 || Rows[r].Freeze; }
        bool Single(int r) { return DefsOf(r).Count == 1 && Rows[r].Variants.Length == 0 && !Rows[r].Freeze; }

        public static string VarLabel(string v) { return v == "stationaer" ? "stationär" : v; }

        // Beschriftung eines Pflichtpunkts mit Zustand ("Tagesprobe auto", "WFI-Woche entfällt")
        string DefLabel(CellData c, int i)
        {
            CheckDef d = DefsOf(c.RowIndex)[i];
            if (On(c, d)) return d.Short;
            if (BufMade && (d.Key == MadeRules.DayKey || d.Key == MadeRules.WeekKey))   // Probe folgt aus "hergestellt": gezogen / offen
            {
                DayState ms = StateOf(c, i);
                if (ms == DayState.Auto) return d.Short + " ✓ gezogen";
                if (ms == DayState.WeekOpen) return d.Short + " offen";
            }
            switch (StateOf(c, i))
            {
                case DayState.Auto: return d.Rule == CheckRule.WeeklyOnce ? d.Short + " ✓ stationär" : d.Short + " auto";
                case DayState.Missing: return "! " + d.Short + " fehlt";   // Symbol + Wort, nicht nur Farbe
                case DayState.NA: return d.Short + " entfällt";
                case DayState.NotNeeded: return d.Short + " KW ok";
                case DayState.WeekOpen: return d.Rule == CheckRule.WeeklyOnce ? "manuell: " + d.Short + " offen" : d.Short + " offen";
            }
            return d.Short;
        }

        // Eintraege fliessen von links nach rechts und brechen um; Breite je Pflichtpunkt = laengste Beschriftung der 7 Tage.
        // Rueckgabe = Hoehe der Leiste (0 = keine).
        int LayoutChecks(int r, int iw)
        {
            List<CheckSlot> l = new List<CheckSlot>();
            if (!HasChecks(r)) { slots[r] = l.ToArray(); return 0; }
            List<CheckDef> d = DefsOf(r);
            int x = TP, line = 0, max = iw - 2, box = Theme.Current.CheckBox;
            if (Rows[r].Variants.Length > 0) { CheckSlot s = new CheckSlot(); s.Add = true; s.W = TextW("+ Puffer", bold) + 16; l.Add(s); }
            if (Rows[r].Freeze) { CheckSlot s = new CheckSlot(); s.Freeze = true; s.W = TextW("+ Freeze", bold) + 16; l.Add(s); }
            for (int i = 0; i < d.Count; i++)
            {
                CheckSlot s = new CheckSlot(); s.Def = i;
                if (Single(r)) s.W = max - TP;
                else foreach (CellData c in Cells) if (c.RowIndex == r) s.W = Math.Max(s.W, TextW(DefLabel(c, i), Font) + (StateOf(c, i) != DayState.NA ? box + 12 : 10));
                l.Add(s);
            }
            foreach (CheckSlot s in l)
            {
                if (x + s.W > max && x > TP) { line++; x = TP; }
                s.R = new Rectangle(x, line * LkzH, Math.Min(s.W, max - x), LkzH); x += s.W + 3;
            }
            slots[r] = l.ToArray();
            return (line + 1) * LkzH;
        }

        bool HasBox(CellData c, int i) { DayState st = StateOf(c, i); return st != DayState.Auto && st != DayState.NA; }   // Auto zeigt ein Haekchen ohne Kasten

        static bool On(CellData c, CheckDef d) { return d.Key == CheckDef.LkzKey ? c.Lkz : c.Marks.Contains(d.Key); }

        DayState StateOf(CellData c, int i) { return c.States != null && i < c.States.Length ? c.States[i] : DayState.WeekOpen; }

        void PaintChecks(Graphics g, CellData c, Color back)
        {
            Theme th = Theme.Current;
            const TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            int r = c.RowIndex, bs = th.CheckBox;
            List<CheckDef> d = DefsOf(r);
            bool single = Single(r);
            Fill(g, back, c.RChecks);
            foreach (CheckSlot s in slots[r])
            {
                Rectangle sr = new Rectangle(c.RChecks.X + s.R.X, c.RChecks.Y + s.R.Y, s.R.Width, s.R.Height);
                Rectangle chip = new Rectangle(sr.X, sr.Y + 3, sr.Width - 1, sr.Height - 7);
                if (s.Add || s.Freeze)   // Knopf "+ Puffer" / "+ Freeze": Umriss in Akzentfarbe
                {
                    Fill(g, Color.White, chip); g.DrawRectangle(th.Pen(th.ChipOn, 1), chip);
                    TextRenderer.DrawText(g, s.Freeze ? "+ Freeze" : "+ Puffer", bold, chip, th.ChipOn, tf | TextFormatFlags.HorizontalCenter);
                    continue;
                }
                CheckDef def = d[s.Def];
                DayState st = StateOf(c, s.Def);
                bool on = On(c, def), box = HasBox(c, s.Def);
                Color fill = Color.Empty, fg = th.LkzText, edge = th.ChipOff;
                if (on) { fill = th.LkzOn; edge = th.CheckMark; fg = th.DoneText; }
                else if (st == DayState.Missing) { fill = th.CheckMissing; fg = th.CheckMissingText; edge = th.CheckMissingText; }
                else if (st == DayState.Auto) { fill = th.CheckAuto; fg = th.DoneText; edge = th.CheckAuto; }
                else if (st == DayState.NA || st == DayState.NotNeeded) fg = th.Muted;
                if (single)   // nur LKZ: ganze Leiste einfaerben, Text + grosse Checkbox mittig
                {
                    string text = on ? def.Name : st == DayState.NotNeeded ? def.Short + " nicht nötig" : st == DayState.Missing ? "! " + (def.Short.Length > 0 ? def.Short : def.Name) + " offen" : def.Name + "?";   // rot: zusaetzlich "!" + "offen"
                    if (!fill.IsEmpty) Fill(g, fill, c.RChecks);
                    int tw = Math.Min(TextW(text, Font), sr.Width - bs - 12), x0 = sr.X + Math.Max(2, (sr.Width - tw - (box ? bs + 8 : 0)) / 2);
                    TextRenderer.DrawText(g, text, Font, new Rectangle(x0, sr.Y, tw + 1, sr.Height), fg, tf);
                    if (box) th.DrawCheck(g, new Point(x0 + tw + 8, sr.Y + (sr.Height - bs) / 2), on, bs);
                    continue;
                }
                if (!fill.IsEmpty) Fill(g, fill, chip);
                if (st != DayState.NA) g.DrawRectangle(th.Pen(edge, 1), chip);
                TextRenderer.DrawText(g, DefLabel(c, s.Def), Font, new Rectangle(chip.X + 4, chip.Y, chip.Width - (st == DayState.NA ? 6 : bs + 8), chip.Height), fg, tf);
                Point cp = new Point(chip.Right - bs - 2, chip.Y + (chip.Height - bs) / 2 + 1);
                if (box) th.DrawCheck(g, cp, on, bs);
                else if (st == DayState.Auto) TextRenderer.DrawText(g, "✓", bold, new Rectangle(cp.X, chip.Y, bs, chip.Height), th.DoneText, tf);
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

#if TESTHOOKS
        // Selbsttest: Mitte des Pflichtpunkts (def) bzw. des Knopfs "+ Puffer" (def = -1), Klick wie mit der Maus
        public Point SlotPoint(CellData c, int def)
        {
            foreach (CheckSlot s in slots[c.RowIndex])
                if (s.Add ? def < 0 : s.Def == def) return new Point(c.RChecks.X + s.R.X + s.R.Width / 2, c.RChecks.Y + s.R.Y + s.R.Height / 2);
            return Point.Empty;
        }
        public void TestClick(Point p) { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0)); }
#endif

        static string StateText(DayState s)
        {
            switch (s)
            {
                case DayState.Done: return "erledigt";
                case DayState.Auto: return "automatisch mit der Herstellung erledigt";
                case DayState.Missing: return "fehlt";
                case DayState.Due: return "fällig";
                case DayState.NotNeeded: return "nicht nötig (in dieser KW erledigt)";
                case DayState.WeekOpen: return "in dieser KW noch offen";
            }
            return "entfällt";
        }

        // " (Di)" = Wochentag des ersten Tags der KW mit auslösender Variante (soweit angezeigt)
        string FirstDayText(CellData c, CheckDef d)
        {
            CellData best = null;
            foreach (CellData x in Cells)
                if (x.RowIndex == c.RowIndex && IsoWeek.Monday(x.Day) == IsoWeek.Monday(c.Day) && (best == null || x.Day < best.Day))
                    foreach (string v in VariantSet.Of(x.Variant)) if (CheckEval.Cond(d, '!', v)) { best = x; break; }
            return best == null ? "" : " (" + best.Day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("de-DE")).TrimEnd('.') + ")";
        }

        string CheckTip(CellData c, CheckSlot s)
        {
            if (s.Add) return "Puffer hinzufügen (Name, Typ, Text)";
            if (s.Freeze) return "Freeze-Vorgang hinzufügen (Bag-Freeze mit Zyklen oder Tank-Freeze)";
            CheckDef d = DefsOf(c.RowIndex)[s.Def];
            DayState st = StateOf(c, s.Def);
            string mt = BufMade ? MadeRules.Tip(d, st, On(c, d)) : "";
            if (mt.Length > 0) return d.Name + ": " + (On(c, d) ? "erledigt" : st == DayState.Auto ? "gezogen" : StateText(st)) + "\n" + mt;
            if (d.Rule == CheckRule.WeeklyOnce && st == DayState.Auto) return d.Name + ": erledigt durch stationäre Herstellung" + FirstDayText(c, d) + " – kein offener Punkt";
            string t = d.Name + ": " + (On(c, d) ? "erledigt" : StateText(st));
            if (d.Rule == CheckRule.WeeklyOnce && !On(c, d) && st != DayState.Done) t += " (manuell, 1× pro Woche abhaken)";
            if (On(c, d) || (st != DayState.Auto && st != DayState.NA)) return t;
            List<string> why = new List<string>();
            foreach (string v in VariantSet.Of(c.Variant)) if (CheckEval.Cond(d, st == DayState.Auto ? '+' : '!', v)) why.Add(VarLabel(v));
            if (st == DayState.NA && d.Rule == CheckRule.WeeklyOnce && why.Count == 0)
                foreach (string v in Rows[c.RowIndex].VariantList()) if (CheckEval.Cond(d, '!', v)) why.Add(VarLabel(v));
            if (why.Count == 0) return t + (st == DayState.NA ? " (Wochenende/Feiertag ohne Herstellung)" : "");
            return t + " – wegen " + string.Join(" + ", why.ToArray()) + (d.Rule == CheckRule.WeeklyOnce ? " in KW" + IsoWeek.Number(c.Day) : "");
        }
    }
}
