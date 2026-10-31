using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Schritt-Details "Wann?". Standard: Bezug "zu jedem Lauf" (Bind = JeLauf): "[1] Tag(e) vor/am Tag/nach jedem Lauf"; die
    // Anzahl folgt der Laufanzahl im Kopf, kein eigenes "nach Anzahl". Mehr Optionen (nur Puffer): "reicht fuer [n] Laeufe" (RunEvery).
    // Sonderfall "eigenes Datum" (Bind = Frei): "[3] Tage vor/nach Lauf 1" statt Tagesnummer; Wiederholung wie bisher, Ende "fuer alle
    // Laeufe" / "genau n-mal" / "bis Lauf k". Der Hauptlauf behaelt sein Wiederholungsmuster (alle x Tage / Wochentage / bestimmte Tage),
    // seine Anzahl ist die Laufanzahl aus dem Kopf. Termine: DatesText (Datum bzw. "3 Tage vor Lauf 1"), nie "Tag n".
    partial class PackageForm
    {
        readonly ComboBox cbRepeat = new ComboBox(), cbEnd = new ComboBox(), cbBind = new ComboBox(), cbDir = new ComboBox();
        readonly NumericUpDown nUntil = new NumericUpDown(), nEvery = new NumericUpDown();
        readonly CheckBox[] chkDays = new CheckBox[7];
        readonly TextBox tDays = new TextBox();
        Label lFollow, lDateNow, lRepeat, lInterval, lIntervalUnit, lEndUnit, lDaysList, lDaysHelp, lDates, lStartNo, lNoEx, lEvery, lEveryUnit;
        int[] repKinds = new int[0], endKinds = new int[0], dirKinds = new int[0];   // Art je Eintrag der Auswahllisten
        int[] bindKinds = new int[0];
        string dirMode = "", repMode = "", endMode = "", bindMode = "";
        static readonly string[] RepeatNames = { "einmalig", "alle x Tage", "an Wochentagen", "an bestimmten Tagen" };

        void BuildWhen()
        {
            gWhen = Group("Wann?", "Bezug zu den Läufen, Termine siehe unten", 270);
            int lw = LabelW("Wiederholt sich:", "Laufnummer ab:", "reicht für:", "Abstand:", "Tage:"), x = 8 + lw, w = 270 - 8 - x;
            cbBind.SetBounds(8, 0, 254, IH); cbBind.DropDownStyle = ComboBoxStyle.DropDownList;
            cbBind.Items.AddRange(new object[] { "zu jedem Lauf (Standard)", "eigenes Datum (Sonderfall)" }); gWhen.Controls.Add(cbBind);
            lFollow = Lbl(gWhen, "", 8, 254); lFollow.Height = 2 * Font.Height + 2; lFollow.TextAlign = ContentAlignment.TopLeft;
            Num(nDay, 1, 366, 8, 52, gWhen); cbDir.SetBounds(66, 0, 196, IH); cbDir.DropDownStyle = ComboBoxStyle.DropDownList; gWhen.Controls.Add(cbDir);
            lDateNow = Lbl(gWhen, "", 8, 254);
            lEvery = Lbl(gWhen, "reicht für:", 8, lw); Num(nEvery, 1, 99, x, 52, gWhen); lEveryUnit = Lbl(gWhen, "Läufe", x + 58, w - 58);
            lRepeat = Lbl(gWhen, "Wiederholt sich:", 8, lw); cbRepeat.SetBounds(x, 0, w, IH); cbRepeat.DropDownStyle = ComboBoxStyle.DropDownList; gWhen.Controls.Add(cbRepeat);
            lInterval = Lbl(gWhen, "Abstand:", 8, lw); Num(nInterval, 1, 30, x, 60, gWhen); lIntervalUnit = Lbl(gWhen, "Tage", x + 66, w - 66);
            for (int i = 0; i < 7; i++)
            {
                CheckBox c = new CheckBox(); c.Text = PackageStep.DayNames[i]; c.Appearance = Appearance.Button; c.TextAlign = ContentAlignment.MiddleCenter;
                c.SetBounds(8 + i * 35, 0, 33, IH); int bit = 1 << i;
                c.CheckedChanged += delegate { if (Edit()) { cur.WeekDays = c.Checked ? cur.WeekDays | bit : cur.WeekDays & ~bit; Changed(); } };
                chkDays[i] = c; gWhen.Controls.Add(c);
            }
            lDaysList = Lbl(gWhen, "Tage:", 8, lw); tDays.SetBounds(x, 0, w, IH); gWhen.Controls.Add(tDays); Ui.SimpleMenu(tDays);
            lDaysHelp = Lbl(gWhen, "Tage nach Beginn des Ablaufs, Komma-getrennt: 1 = erster Termin-Tag (z. B. 1, 4, 9)", x, w); lDaysHelp.Height = 2 * Font.Height + 2; lDaysHelp.TextAlign = ContentAlignment.TopLeft;
            int ex = 8 + lw + 22;
            cbEnd.SetBounds(8, 0, lw + 14, IH); cbEnd.DropDownStyle = ComboBoxStyle.DropDownList; gWhen.Controls.Add(cbEnd);
            Num(nCount, 1, PackageStep.MaxRuns, ex, 56, gWhen); Num(nUntil, 1, PackageStep.MaxRuns, ex, 56, gWhen); lEndUnit = Lbl(gWhen, "mal", ex + 62, 60);
            lDates = Lbl(gWhen, "", 8, 254); lDates.TextAlign = ContentAlignment.TopLeft;
            lStartNo = Lbl(gWhen, "Laufnummer ab:", 8, lw); Num(nStartNo, 0, 99999, x, 66, gWhen); lNoEx = Lbl(gWhen, "", x + 72, w - 72);
            lNoEx.AutoEllipsis = true;
            Grow(cbBind, lFollow, cbDir, lDateNow, cbRepeat, tDays, lDaysHelp, lIntervalUnit, lEndUnit, lDates, lNoEx, lEveryUnit); Gray(lFollow, lDateNow, lDaysHelp, lDates, lNoEx);
            tip.SetToolTip(cbBind, "Zu jedem Lauf: der Schritt folgt jedem Lauf des Hauptlaufs (Anzahl = Laufanzahl im Kopf). Eigenes Datum: fester Termin, z. B. einmalig vor Lauf 1.");
            tip.SetToolTip(cbDir, "Abstand zum Lauf (nicht zum Start): vor, am selben Tag oder nach dem Lauf");
            tip.SetToolTip(nEvery, "Ein Ansatz reicht für mehrere Läufe: 2 = Läufe 1, 3, 5 … (nur Puffer)");
            tip.SetToolTip(nStartNo, "Hintere 4 Stellen (ZZZZ) des ersten Laufs dieses Schritts; normalerweise wie „Erste Laufnummer“ oben.");
            tip.SetToolTip(cbRepeat, "Wann sich der Schritt wiederholt; die Termine stehen darunter und im Kalender unten.");
            tip.SetToolTip(cbEnd, "Ende der Wiederholung: für alle Läufe der Kampagne, genau n-mal oder bis zu einem Lauf");
            tip.SetToolTip(tDays, "Tage nach Beginn des Ablaufs, an denen der Schritt stattfindet (1 = erster Termin-Tag); die Termine stehen darunter");

            BuildWhenPuf();
            nDay.ValueChanged += delegate { OffsetEdited(); };
            cbDir.SelectedIndexChanged += delegate { OffsetEdited(); };
            cbBind.SelectedIndexChanged += delegate { BindEdited(); };
            nEvery.ValueChanged += delegate { if (Edit()) { cur.RunEvery = (int)nEvery.Value; Changed(); } };
            nCount.ValueChanged += delegate { if (Edit()) { cur.Count = (int)nCount.Value; Changed(); } };
            nUntil.ValueChanged += delegate
            {
                PackageStep h = StepsView().Head;
                if (!Edit() || h == null) return;
                List<int> hd = h.BaseDays(pk.Day1);   // Rohtage: Until wird gegen die Regel des Hauptlaufs (ohne Move/Shift) ausgewertet
                cur.Until = hd[Math.Max(0, Math.Min(hd.Count, (int)nUntil.Value) - 1)]; Changed();
            };
            nInterval.ValueChanged += delegate { if (Edit()) { cur.Interval = (int)nInterval.Value; Changed(); } };
            nStartNo.ValueChanged += delegate { if (Edit()) { cur.StartNo = (int)nStartNo.Value; Changed(); } };
            tDays.TextChanged += delegate { if (Edit()) { cur.DayList = tDays.Text.Trim(); Changed(); } };
            cbRepeat.SelectedIndexChanged += delegate { if (Edit() && cbRepeat.SelectedIndex >= 0 && cbRepeat.SelectedIndex < repKinds.Length) SetRepeat(cur, repKinds[cbRepeat.SelectedIndex]); };
            cbEnd.SelectedIndexChanged += delegate { if (Edit() && cbEnd.SelectedIndex >= 0 && cbEnd.SelectedIndex < endKinds.Length) SetEnd(cur, endKinds[cbEnd.SelectedIndex]); };
        }

        // Nur die Wann?-Felder neu fuellen und alles nachziehen (kein Neuaufbau der Equipment-Zeilen)
        void Rebind() { BindWhen(cur); dirty = true; UpdateView(false); CommitUndo(); }

        static int MonIdx(DayOfWeek d) { return ((int)d + 6) % 7; }

        bool IsPuffer(PackageStep s) { return s.IsBuffer || s.UsesAnsatz; }

        // Tage zum Bezug: JeLauf = RunOffset, sonst erster Termin minus Lauf 1 (ohne Hauptlauf: minus Beginn)
        int OffOf(PackageStep s)
        {
            if (IsJe(s)) return s.RunOffset;
            return FirstOf(s, Plan()) - (StepsView().Head != null ? pk.Lauf1Day : 1);
        }

        // 0 einmalig, 1 alle x Tage, 2 Wochentage, 3 bestimmte Tage; der Hauptlauf hat kein "einmalig"
        int RepeatKindOf(PackageStep s)
        {
            int k = s.Repeat == RepeatMode.Weekdays ? 2 : s.Repeat == RepeatMode.DayList ? 3 : (s.RunCountFor(pk.Day1) > 1 || s.Until > 0 ? 1 : 0);
            return s == StepsView().Head ? Math.Max(1, k) : k;
        }

        // 0 fuer alle Laeufe, 1 genau n-mal, 2 bis Lauf k
        int EndKindOf(PackageStep s)
        {
            PackageStep h = StepsView().Head;
            if (s.Until > 0) return 2;
            return h != null && pk.RunCount > 1 && s.Count == pk.RunCount ? 0 : 1;
        }

        int RunOfDay(int day)
        {
            PackageStep h = StepsView().Head; int k = 1;
            if (h == null) return 1;
            List<int> hd = h.BaseDays(pk.Day1);
            for (int i = 0; i < hd.Count; i++) if (hd[i] <= day) k = i + 1;
            return k;
        }

        // ---------- Aenderungen ----------

        // "[n] Tage vor/nach Lauf" bzw. "jedem Lauf" geaendert
        void OffsetEdited()
        {
            if (!Edit() || cur.IsBackup || cur == StepsView().Head || cbDir.SelectedIndex < 0 || cbDir.SelectedIndex >= dirKinds.Length) return;
            int want = IsPacken(cur) ? -Math.Max((int)nDay.Value, Package.HasBackup(steps, cur) ? 2 : 1) : dirKinds[cbDir.SelectedIndex] * (int)nDay.Value, delta = want - OffOf(cur);
            if (delta == 0) { BindWhen(cur); LayoutDetails(); return; }
            if (!MoveBy(cur, delta)) { BindWhen(cur); return; }
            AfterMove(cur);
        }

        void BindEdited()
        {
            if (!Edit() || cbBind.SelectedIndex < 0) return;
            bool je = cbBind.SelectedIndex == 0;
            if (je == IsJe(cur) || StepsView().Head == null) return;
            if (je) SetJe(cur); else SetFrei(cur);
            AfterMove(cur);
        }

        // Bezug "zu jedem Lauf": Versatz = bisheriger Abstand zu Lauf 1
        void SetJe(PackageStep s)
        {
            Package p = StepsView();
            int off = Math.Max(-PackageStep.MaxOffset, Math.Min(PackageStep.MaxOffset, FirstOf(s, Plan()) - p.Lauf1Day));
            bool numbered = s.Kind == StepKind.Normal && p.RunCountOf(s) > 1;
            if (s.Bind != StepBind.JeLauf) s.Shift.Clear();   // nur beim echten Wechsel: Abstand je Lauf eines Puffers bleibt sonst
            s.Bind = StepBind.JeLauf; s.RunOffset = off; s.RunEvery = 1; s.NumberRuns = numbered;
            s.Repeat = RepeatMode.Interval; s.Count = 1; s.Until = 0; s.WeekDays = 0; s.DayList = ""; s.Move.Clear();
        }

        // Eigenes Datum: die bisherigen Termine bleiben (eine Tagesliste)
        void SetFrei(PackageStep s)
        {
            int si = steps.IndexOf(s);
            List<int> days = new List<int>();
            foreach (RunDate d in Plan()) if (d.Step == si) days.Add(d.Day);
            days.Sort();
            s.Bind = StepBind.Frei; s.RunOffset = 0; s.RunEvery = 1; s.Shift.Clear(); s.Move.Clear(); s.Until = 0;
            if (days.Count <= 1) { s.Repeat = RepeatMode.Interval; s.Count = 1; if (days.Count == 1) s.Day = days[0]; }
            else { s.Repeat = RepeatMode.DayList; s.DayList = PackageStep.JoinDays(days); s.Count = days.Count; s.Day = days[0]; }
        }

        // Wiederholmodus wechseln: Termine bleiben moeglichst erhalten (Tagesliste = bisherige Tage, Wochentag = Wochentag des ersten Termins)
        void SetRepeat(PackageStep s, int kind)
        {
            if (kind == RepeatKindOf(s)) return;
            bool head = s == StepsView().Head;
            List<int> old = s.Days(pk.Day1);
            int first = old[0]; foreach (int d in old) first = Math.Min(first, d);
            if (s.Repeat == RepeatMode.DayList && kind != 3) s.Day = first;   // Tagesliste kennt den Starttag nicht
            if (kind == 0) { s.Repeat = RepeatMode.Interval; s.Count = 1; s.Until = 0; s.Shift.Clear(); }
            else if (kind == 1) { s.Repeat = RepeatMode.Interval; if (head) { s.Count = old.Count; s.Until = 0; } else if (s.Until == 0) s.Count = Math.Max(2, old.Count); }
            else if (kind == 2)
            {
                s.Repeat = RepeatMode.Weekdays;
                if ((s.WeekDays & 127) == 0) s.WeekDays = 1 << ((MonIdx(pk.Day1) + first - 1) % 7);
                if (head) { s.Count = old.Count; s.Until = 0; } else if (s.Until == 0) s.Count = Math.Max(2, old.Count);
            }
            else
            {
                s.Repeat = RepeatMode.DayList; s.Shift.Clear();
                if (PackageStep.ParseDays(s.DayList).Count == 0) s.DayList = PackageStep.JoinDays(new List<int>(old));
            }
            Rebind();
        }

        // Ende der Wiederholung: fuer alle Laeufe (Anzahl = Laufanzahl), genau n-mal, bis Lauf k
        void SetEnd(PackageStep s, int kind)
        {
            PackageStep h = StepsView().Head;
            if (kind == EndKindOf(s)) return;
            int runs = Math.Max(1, h != null ? pk.RunCount : s.Count);
            if (kind == 0) { s.Until = 0; s.Count = runs; }
            else if (kind == 1) { if (s.Until > 0) s.Count = Math.Max(1, s.Days(pk.Day1).Count); s.Until = 0; }
            else if (h != null) { List<int> hd = h.BaseDays(pk.Day1); s.Until = hd[Math.Min(hd.Count, runs) - 1]; }
            Rebind();
        }

        // ---------- anzeigen ----------

        void FillCombo(ComboBox c, ref string mode, string key, string[] names, int[] kinds, ref int[] map)
        {
            if (mode == key) return;
            mode = key; map = kinds;
            c.Items.Clear(); c.Items.AddRange(names);
        }

        void BindWhen(PackageStep s)
        {
            Repin();
            bool was = loading; loading = true;
            Package p = StepsView(); PackageStep head = p.Head;
            bool isHead = s == head, je = IsJe(s);
            bool pk1 = IsPacken(s);                  // Packen: nur "[x] Tag(e) vor Lauf 1", mindestens 2 mit Back-Up
            bool pf = IsPuffer(s) && head != null;   // UX-5c: Puffer klar zweigeteilt: fester Abstand zu jedem Lauf | eigener Rhythmus
            FillCombo(cbBind, ref bindMode, pf ? "puf" : "std", pf ? new[] { "zu jedem Lauf (fester Abstand)", "eigener Rhythmus (unabhängig vom Lauf-Raster)" } : new[] { "zu jedem Lauf (Standard)", "eigenes Datum (Sonderfall)" }, new[] { 0, 1 }, ref bindKinds);
            tip.SetToolTip(cbBind, pf ? "Zu jedem Lauf: jeder Lauf bekommt seinen Puffer im festen Abstand (Anzahl = Laufanzahl). Eigener Rhythmus: die Puffer haben ein eigenes Raster (alle x Tage, Wochentage, Liste), unabhängig von den Läufen; jeder Termin bedient den nächsten Lauf."
                : "Zu jedem Lauf: der Schritt folgt jedem Lauf des Hauptlaufs (Anzahl = Laufanzahl im Kopf). Eigenes Datum: fester Termin, z. B. einmalig vor Lauf 1.");
            cbBind.SelectedIndex = je ? 0 : 1;
            int off = isHead || s.IsBackup ? 0 : OffOf(s);
            bool pkBad = pk1 && off >= 0;            // altes Paket: Packen am/nach Lauf 1 -> ehrlich anzeigen, jede Eingabe korrigiert auf "vor Lauf 1"
            nDay.Minimum = pk1 && Package.HasBackup(steps, s) && off <= -2 ? 2 : 1;   // erst beim Aendern begrenzen (OffsetEdited), sonst klemmt die Anzeige 1 -> 2
            if (isHead) FillCombo(cbDir, ref dirMode, "head", new string[0], new int[0], ref dirKinds);
            else if (pkBad) FillCombo(cbDir, ref dirMode, "packenx", new[] { "Tag(e) vor Lauf 1", "am Tag von Lauf 1", "Tag(e) nach Lauf 1" }, new[] { -1, 0, 1 }, ref dirKinds);
            else if (pk1) FillCombo(cbDir, ref dirMode, "packen", new[] { "Tag(e) vor Lauf 1" }, new[] { -1 }, ref dirKinds);
            else if (je) FillCombo(cbDir, ref dirMode, "je", new[] { "Tag(e) vor jedem Lauf", "am Tag jedes Laufs", "Tag(e) nach jedem Lauf" }, new[] { -1, 0, 1 }, ref dirKinds);
            else if (head != null) FillCombo(cbDir, ref dirMode, "lauf", new[] { "Tag(e) vor Lauf 1", "am Tag von Lauf 1", "Tag(e) nach Lauf 1" }, new[] { -1, 0, 1 }, ref dirKinds);
            else FillCombo(cbDir, ref dirMode, "beginn", new[] { "am Beginn", "Tag(e) nach Beginn" }, new[] { 0, 1 }, ref dirKinds);
            int di = Array.IndexOf(dirKinds, off < 0 ? -1 : off > 0 ? 1 : 0);
            if (cbDir.Items.Count > 0) cbDir.SelectedIndex = di < 0 ? 0 : di;
            nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, Math.Abs(off)));

            if (isHead) FillCombo(cbRepeat, ref repMode, "head", new[] { RepeatNames[1], RepeatNames[2], RepeatNames[3] }, new[] { 1, 2, 3 }, ref repKinds);
            else FillCombo(cbRepeat, ref repMode, "frei", RepeatNames, new[] { 0, 1, 2, 3 }, ref repKinds);
            cbRepeat.SelectedIndex = Math.Max(0, Array.IndexOf(repKinds, RepeatKindOf(s)));
            if (head != null) FillCombo(cbEnd, ref endMode, "lauf", new[] { "für alle Läufe", "genau", "bis Lauf" }, new[] { 0, 1, 2 }, ref endKinds);
            else FillCombo(cbEnd, ref endMode, "ohne", new[] { "genau" }, new[] { 1 }, ref endKinds);
            cbEnd.SelectedIndex = Math.Max(0, Array.IndexOf(endKinds, EndKindOf(s)));
            nCount.Value = Math.Max(1, Math.Min(PackageStep.MaxRuns, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
            nUntil.Value = Math.Max(1, Math.Min(PackageStep.MaxRuns, RunOfDay(s.Until)));
            nEvery.Value = Math.Max(1, Math.Min(99, s.RunEvery));
            for (int i = 0; i < 7; i++) chkDays[i].Checked = (s.WeekDays & (1 << i)) != 0;
            if (tDays.Text.Trim() != s.DayList) tDays.Text = s.DayList;
            nStartNo.Value = Math.Max(0, Math.Min(nStartNo.Maximum, s.StartNo));
            if (IsJe(s) && s.IsBuffer) FillPuf(s);
            loading = was;
        }

        // Termine des Schritts: Laeufe, die ersten Termine mit Datum bzw. Bezug zu Lauf 1, "*" = einzeln verschoben
        string DatesText(PackageStep s)
        {
            int si = steps.IndexOf(s);
            List<RunDate> plan = Plan();
            List<RunDate> mine = plan.FindAll(delegate(RunDate d) { return d.Step == si; });
            mine.Sort(delegate(RunDate a, RunDate b) { return a.Day.CompareTo(b.Day); });
            if (mine.Count == 0) return "Kein Termin";
            List<string> l = new List<string>();
            l.Add(StepsView().RunsText(s));
            bool je = IsJe(s);
            for (int i = 0; i < mine.Count && i < 3; i++) l.Add(RelOf(s, mine[i], plan) + (!je && s.Shifted(mine[i].Run) ? "*" : ""));
            if (StepsView().CardsSplit(s, pk.Day1))   // UX-5c: eigener Versatz je Karte
                foreach (int dl in s.CardDeltas()) l.Add(StepsView().CardNames(s, dl) + ": " + CardOffText(s.RunOffset + dl));
            if (mine.Count > 3) l.Add("… " + (mine.Count - 3) + " weitere (siehe Kalender)");
            if (!je && s.Shift.Count > 0) l.Add("* = einzeln verschoben");
            return string.Join("\n", l.ToArray());
        }

        // Packen mit genau einem freien Termin: Bezug/Wiederholung entfallen; alles andere (alte Daten) bleibt bearbeitbar
        bool PkSimple(PackageStep s) { return IsPacken(s) && !IsJe(s) && s.RunCountFor(pk.Day1) == 1; }

        int LayoutWhen(PackageStep s)
        {
            Package p = StepsView(); PackageStep head = p.Head;
            bool isHead = s == head, je = IsJe(s), bk = s.IsBackup, locked = bk || Package.HasBackup(steps, s);
            int rk = RepeatKindOf(s);
            bool own = !je && !bk && !PkSimple(s);                       // eigenes Raster: Hauptlauf oder eigenes Datum
            bool offs = !isHead && !bk;
            bool end = own && !isHead && (rk == 1 || rk == 2);
            int y = gWhen.ContentTop;
            y = Row(y, head != null && !isHead && (IsPacken(s) ? !PkSimple(s) : !locked), cbBind);
            lFollow.Text = bk ? "Der Back-Up folgt dem Packen (1 Tag danach)." : (isHead ? "Hauptlauf: gibt den Takt vor. Läufe und Datum von Lauf 1 stehen oben im Kopf."
                : (head != null && IsPuffer(s) ? (je ? "Jeder Lauf bekommt seinen Puffer; eigener Zeitpunkt je Puffer in der Karte." : "Jeder Termin bedient den nächsten Lauf (siehe „für Lauf n“ unten).") : ""));
            y = Row(y, lFollow.Text.Length > 0, lFollow);
            int dk = cbDir.SelectedIndex >= 0 && cbDir.SelectedIndex < dirKinds.Length ? dirKinds[cbDir.SelectedIndex] : 0;
            y = Row(y, offs, nDay, cbDir);
            nDay.Visible = offs && dk != 0;
            cbDir.Left = nDay.Visible ? 66 : 8; cbDir.Width = Math.Max(60, gWhen.Width - 8 - cbDir.Left);
            DateTime? first = p.DateOf(FirstOf(s, Plan()));
            lDateNow.Text = !je && first.HasValue ? "→ " + DText(first.Value) : "";
            y = Row(y, offs && !je && first.HasValue, lDateNow);
            y = Row(y, je && more && IsPuffer(s), lEvery, nEvery, lEveryUnit);
            cbDir.Enabled = !IsPacken(s) || dirKinds.Length > 1;
            y = Row(y, own, lRepeat, cbRepeat);
            y = Row(y, own && rk == 1, lInterval, nInterval, lIntervalUnit);
            y = Row(y, own && rk == 2, chkDays);
            y = Row(y, own && rk == 3, lDaysList, tDays); y = Row(y, own && rk == 3, lDaysHelp) - (own && rk == 3 ? 4 : 0);
            y = Row(y, end, cbEnd, nCount, nUntil, lEndUnit);
            int ek = cbEnd.SelectedIndex >= 0 && cbEnd.SelectedIndex < endKinds.Length ? endKinds[cbEnd.SelectedIndex] : 1;
            nCount.Visible = end && ek == 1; nUntil.Visible = end && ek == 2; lEndUnit.Visible = end && ek != 0;
            lEndUnit.Text = ek == 2 ? "" : "mal";
            string dt = DatesText(s);
            lDates.Text = dt; lDates.Height = (dt.Split('\n').Length) * Font.Height + 2; tip.SetToolTip(lDates, dt);
            y = Row(y, true, lDates);
            y = LayoutPufTable(y, s);
            y = Row(y, more && s.Numbered, lStartNo, nStartNo, lNoEx);
            lStartNo.Text = s.Kind != StepKind.Normal ? "Laufnummer ab:" : "Erste Nummer:";
            lNoEx.Text = s.Numbered ? "→ " + s.RunNo(prefix, 0) : "";
            tip.SetToolTip(lNoEx, lNoEx.Text);
            return y + 2;
        }
    }
}
