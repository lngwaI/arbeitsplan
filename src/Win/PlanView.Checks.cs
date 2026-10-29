using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Platz eines Eintrags der Pruefleiste, relativ zur Leiste: Variante als Umschalt-Chip (Var) oder Pflichtpunkt (Def).
    class CheckSlot { public int Def = -1; public string Var; public int W; public Rectangle R; }

    // Pruefleiste je Block: Varianten-Chips [mobil] [stationaer] (Klick toggelt sofort), Pflichtpunkte mit Zustand
    // (Tagesprobe auto/fehlt/entfaellt …); nur LKZ: ein grosses, zentriertes Feld "LKZ gemessen?".
    partial class PlanView
    {
        public List<CheckDef>[] Defs = new List<CheckDef>[0];   // je Zeile (MainForm: aus DutyConfig + LKZ aus der Blockstruktur)
        public Action<CellData, string> MarkClicked;            // Schluessel (CheckDef.LkzKey = LKZ-Haken)
        public Action<CellData, string, int> VariantClicked;    // Variante: 0 = an/aus, +1/-1 = Anzahl (BufferSet.Target)
        CheckSlot[][] slots = new CheckSlot[0][];
        static readonly Color[] ChipColors = { Color.FromArgb(0, 120, 180), Color.FromArgb(115, 80, 175), Color.FromArgb(0, 135, 110) };

        List<CheckDef> DefsOf(int r) { return r < Defs.Length && Defs[r] != null ? Defs[r] : new List<CheckDef>(); }
        bool HasChecks(int r) { return DefsOf(r).Count > 0 || Rows[r].Variants.Length > 0; }
        bool Single(int r) { return DefsOf(r).Count == 1 && Rows[r].Variants.Length == 0; }

        public static string VarLabel(string v) { return v == "stationaer" ? "stationär" : v; }

        // Beschriftung eines Pflichtpunkts mit Zustand ("Tagesprobe auto", "WFI-Woche entfällt")
        string DefLabel(CellData c, int i)
        {
            CheckDef d = DefsOf(c.RowIndex)[i];
            if (On(c, d)) return d.Short;
            switch (StateOf(c, i))
            {
                case DayState.Auto: return d.Rule == CheckRule.WeeklyOnce ? d.Short + " ✓ stationär" : d.Short + " auto";
                case DayState.Missing: return d.Short + " fehlt";
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
            foreach (string v in Rows[r].VariantList())
            {   // Breite = breitester Zustand der 7 Tage (aktiv: Anzahl + "−"/"+"), damit die Leiste schmal bleibt, solange nichts aktiv ist
                CheckSlot s = new CheckSlot(); s.Var = v; s.W = TextW(VarLabel(v), bold) + 12;
                foreach (CellData c in Cells)
                {
                    int n = c.RowIndex == r ? VariantSet.Count(c.Variant, v) : 0;
                    if (n > 0) s.W = Math.Max(s.W, TextW((n > 1 ? n + "× " : "") + VarLabel(v), bold) + 8 + 2 * ZoneW);
                }
                l.Add(s);
            }
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
            int r = c.RowIndex, bs = th.CheckBox, vi = 0;
            List<CheckDef> d = DefsOf(r);
            bool single = Single(r);
            Fill(g, back, c.RChecks);
            foreach (CheckSlot s in slots[r])
            {
                Rectangle sr = new Rectangle(c.RChecks.X + s.R.X, c.RChecks.Y + s.R.Y, s.R.Width, s.R.Height);
                Rectangle chip = new Rectangle(sr.X, sr.Y + 3, sr.Width - 1, sr.Height - 7);
                if (s.Var != null)   // Umschalt-Chip: aktiv = gefuellt mit Anzahl und "−"/"+" an den Raendern, inaktiv = Umriss
                {
                    Color col = ChipColors[vi++ % ChipColors.Length];
                    int n = VariantSet.Count(c.Variant, s.Var);
                    if (n == 0) { Fill(g, Color.White, chip); g.DrawRectangle(th.Pen(th.ChipOff, 1), chip); }
                    else
                    {
                        Fill(g, col, chip);
                        Rectangle lz = new Rectangle(chip.X, chip.Y, ZoneW, chip.Height), rz = new Rectangle(chip.Right - ZoneW, chip.Y, ZoneW, chip.Height);
                        Fill(g, Color.FromArgb(70, 0, 0, 0), lz); Fill(g, Color.FromArgb(70, 0, 0, 0), rz);
                        TextRenderer.DrawText(g, "−", bold, lz, Color.White, tf | TextFormatFlags.HorizontalCenter);
                        TextRenderer.DrawText(g, "+", bold, rz, Color.White, tf | TextFormatFlags.HorizontalCenter);
                    }
                    Rectangle mid = n == 0 ? chip : new Rectangle(chip.X + ZoneW, chip.Y, chip.Width - 2 * ZoneW, chip.Height);
                    TextRenderer.DrawText(g, (n > 1 ? n + "× " : "") + VarLabel(s.Var), bold, mid, n > 0 ? Color.White : th.Muted, tf | TextFormatFlags.HorizontalCenter);
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
                    string text = on ? def.Name : st == DayState.NotNeeded ? def.Short + " nicht nötig" : def.Name + "?";
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

        const int ZoneW = 14;   // Breite der "−"/"+"-Felder am aktiven Varianten-Chip

        // Klick auf einen Varianten-Chip (x relativ zum Chip, Breite w): aktiv links = -1, rechts = +1, sonst 0 (an/aus)
        public static int ChipZone(int x, int w, bool active)
        {
            if (!active) return 0;
            return x < ZoneW ? -1 : (x >= w - ZoneW ? 1 : 0);
        }

        // Variante unter dem Mauspunkt: Anzahl -1/+1 an den Raendern, sonst an/aus; onlyCount (Doppelklick) = nur "+"/"−"
        void ClickVariant(CellData c, CheckSlot s, int x, bool onlyCount)
        {
            int z = ChipZone(x - c.RChecks.X - s.R.X, s.R.Width - 1, VariantSet.Contains(c.Variant, s.Var));
            if (VariantClicked != null && (z != 0 || !onlyCount)) VariantClicked(c, s.Var, z);
        }

#if TESTHOOKS
        // Selbsttest: Mitte eines Chips (var) bzw. Pflichtpunkts (def), Klick wie mit der Maus; zone -1/+1 = "−"/"+" am Chip
        public Point SlotPoint(CellData c, string var, int def, int zone = 0)
        {
            foreach (CheckSlot s in slots[c.RowIndex])
                if (var != null ? s.Var == var : s.Var == null && s.Def == def)
                {
                    int x = zone < 0 ? ZoneW / 2 : zone > 0 ? s.R.Width - ZoneW / 2 - 1 : s.R.Width / 2;
                    return new Point(c.RChecks.X + s.R.X + x, c.RChecks.Y + s.R.Y + s.R.Height / 2);
                }
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

        // Wirkung einer Variante auf die Pflichtpunkte der Zeile ("mobil: Tagesprobe automatisch")
        string VarEffect(int r, string v)
        {
            List<string> l = new List<string>();
            foreach (CheckDef d in DefsOf(r))
            {
                if (CheckEval.Cond(d, '+', v)) l.Add(d.Name + " automatisch");
                else if (CheckEval.Cond(d, '!', v)) l.Add(d.Rule == CheckRule.WeeklyOnce ? d.Name + " erledigt für die ganze KW" : d.Name + " entfällt");
                else if (d.Rule == CheckRule.Workdays) l.Add(d.Name + " manuell ziehen");
            }
            return VarLabel(v) + (l.Count > 0 ? ": " + string.Join(", ", l.ToArray()) : "");
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
            if (s.Var != null)
            {
                int n = VariantSet.Count(c.Variant, s.Var);
                return VarEffect(c.RowIndex, s.Var) + "\n" + (n > 0 ? "Aktiv: " + n + "× – „+“/„−“ ändert die Anzahl, Klick auf den Namen schaltet ab" : "Nicht aktiv – Klick schaltet ein")
                    + " (mehrere Varianten gleichzeitig möglich)";
            }
            CheckDef d = DefsOf(c.RowIndex)[s.Def];
            DayState st = StateOf(c, s.Def);
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
