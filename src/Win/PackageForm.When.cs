using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Schritt-Details "Wann?": Tag und "Wiederholt sich" (einmalig / alle x Tage / an Wochentagen / an Tagen der
    // Kampagne) mit Ende nach Anzahl oder "bis Tag n"; darunter die Termine (sofort aktualisiert). In "Was? Wo im Plan?":
    // Herstellung mit Anzahl je Variante (Varianten der Zeile, RowInfo.Variants) und Ansatz (Tank / Flasche-Gebinde / ohne).
    partial class PackageForm
    {
        readonly ComboBox cbRepeat = new ComboBox(), cbEnd = new ComboBox(), cbAnsatz = new ComboBox(), cbUnit = new ComboBox();
        readonly NumericUpDown nUntil = new NumericUpDown();
        readonly CheckBox[] chkDays = new CheckBox[7];
        readonly TextBox tDays = new TextBox(), tGebinde = new TextBox(), tMenge = new TextBox();
        readonly Panel pVar = new Panel();
        Label lDay, lRepeat, lInterval, lIntervalUnit, lEnd, lEndUnit, lDaysList, lDaysHelp, lDates, lStartNo, lNoEx, lAnsatz, lGebinde, lMenge;
        static readonly string[] RepeatNames = { "nicht (einmalig)", "alle x Tage", "an bestimmten Wochentagen", "an bestimmten Tagen der Kampagne" };

        void BuildWhen()
        {
            gWhen = Group("Wann?", "Tag 1 = Startdatum der Kampagne", 270);
            int lw = LabelW("Tag der Kampagne:", "Wiederholt sich:", "Abstand:", "Laufnummer ab:", "Ende:", "Tage:"), x = 8 + lw, w = 270 - 8 - x;
            lDay = Lbl(gWhen, "Tag der Kampagne:", 8, lw); Num(nDay, 1, 366, x, 60, gWhen);
            lRepeat = Lbl(gWhen, "Wiederholt sich:", 8, lw); cbRepeat.SetBounds(x, 0, w, IH); cbRepeat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbRepeat.Items.AddRange(RepeatNames); gWhen.Controls.Add(cbRepeat);
            lInterval = Lbl(gWhen, "Abstand:", 8, lw); Num(nInterval, 1, 30, x, 60, gWhen); lIntervalUnit = Lbl(gWhen, "Tage", x + 66, w - 66);
            for (int i = 0; i < 7; i++)
            {
                CheckBox c = new CheckBox(); c.Text = PackageStep.DayNames[i]; c.Appearance = Appearance.Button; c.TextAlign = ContentAlignment.MiddleCenter;
                c.SetBounds(8 + i * 35, 0, 33, IH); int bit = 1 << i;
                c.CheckedChanged += delegate { if (Edit()) { cur.WeekDays = c.Checked ? cur.WeekDays | bit : cur.WeekDays & ~bit; Changed(); } };
                chkDays[i] = c; gWhen.Controls.Add(c);
            }
            lDaysList = Lbl(gWhen, "Tage:", 8, lw); tDays.SetBounds(x, 0, w, IH); gWhen.Controls.Add(tDays); Ui.SimpleMenu(tDays);
            lDaysHelp = Lbl(gWhen, "Komma-getrennt, z. B. 1, 4, 9", x, w);
            lEnd = Lbl(gWhen, "Ende:", 8, lw); cbEnd.SetBounds(x, 0, 92, IH); cbEnd.DropDownStyle = ComboBoxStyle.DropDownList; cbEnd.Items.AddRange(new object[] { "nach Anzahl", "bis Tag" });
            Num(nCount, 1, PackageStep.MaxRuns, x + 98, 56, gWhen); Num(nUntil, 1, 999, x + 98, 56, gWhen); lEndUnit = Lbl(gWhen, "mal", x + 160, Math.Max(10, w - 160));
            gWhen.Controls.Add(cbEnd);
            lDates = Lbl(gWhen, "", 8, 254); lDates.Height = 2 * Font.Height + 2; lDates.TextAlign = ContentAlignment.TopLeft; lDates.AutoEllipsis = true;
            lStartNo = Lbl(gWhen, "Laufnummer ab:", 8, lw); Num(nStartNo, 0, 99999, x, 66, gWhen); lNoEx = Lbl(gWhen, "", x + 72, w - 72);
            lNoEx.AutoEllipsis = true;
            Grow(cbRepeat, tDays, lDaysHelp, lIntervalUnit, lEndUnit, lDates, lNoEx); Gray(lDaysHelp, lDates, lNoEx);
            tip.SetToolTip(nStartNo, "Hintere 4 Stellen (ZZZZ) des ersten Laufs dieses Schritts; normalerweise wie „Erste Laufnummer“ oben.");
            tip.SetToolTip(cbRepeat, "Wann sich der Schritt wiederholt; die Termine stehen darunter und im Kalender unten.");
            tip.SetToolTip(tDays, "Tage der Kampagne, an denen der Schritt stattfindet (Tag 1 = Startdatum)");

            nDay.ValueChanged += delegate { if (Edit()) { cur.Day = (int)nDay.Value; Changed(); } };
            nCount.ValueChanged += delegate { if (Edit()) { cur.Count = (int)nCount.Value; Changed(); } };
            nUntil.ValueChanged += delegate { if (Edit()) { cur.Until = (int)nUntil.Value; Changed(); } };
            nInterval.ValueChanged += delegate { if (Edit()) { cur.Interval = (int)nInterval.Value; Changed(); } };
            nStartNo.ValueChanged += delegate { if (Edit()) { cur.StartNo = (int)nStartNo.Value; Changed(); } };
            tDays.TextChanged += delegate { if (Edit()) { cur.DayList = tDays.Text.Trim(); Changed(); } };
            cbRepeat.SelectedIndexChanged += delegate { if (Edit() && cbRepeat.SelectedIndex >= 0) SetRepeat(cur, cbRepeat.SelectedIndex); };
            cbEnd.SelectedIndexChanged += delegate
            {
                if (!Edit() || cbEnd.SelectedIndex < 0 || (cbEnd.SelectedIndex == 1) == (cur.Until > 0)) return;
                List<int> d = cur.Days();
                cur.Until = cbEnd.SelectedIndex == 1 ? Math.Max(cur.Day, d[d.Count - 1]) : 0;
                if (cur.Until == 0) cur.Count = Math.Max(cur.Count, d.Count);
                Rebind();
            };
        }

        static int RepeatIndex(PackageStep s)
        {
            if (s.Repeat == RepeatMode.Weekdays) return 2;
            if (s.Repeat == RepeatMode.DayList) return 3;
            return s.RunCount > 1 || s.Until > 0 ? 1 : 0;
        }

        // Wiederholmodus wechseln: Termine bleiben moeglichst erhalten (Tagesliste = bisherige Tage, Wochentag = Tag 1)
        void SetRepeat(PackageStep s, int i)
        {
            if (i == RepeatIndex(s)) return;
            List<int> old = s.Days();
            if (i == 0) { s.Repeat = RepeatMode.Interval; s.Count = 1; s.Until = 0; s.Shift.Clear(); }
            else if (i == 1) { s.Repeat = RepeatMode.Interval; if (s.Until == 0) s.Count = Math.Max(2, old.Count); }
            else if (i == 2)
            {
                s.Repeat = RepeatMode.Weekdays;
                if ((s.WeekDays & 127) == 0) s.WeekDays = 1 << (((int)weekStart.AddDays(s.Day - 1).DayOfWeek + 6) % 7);
                if (s.Until == 0) s.Count = Math.Max(2, old.Count);
            }
            else
            {
                s.Repeat = RepeatMode.DayList; s.Shift.Clear();
                List<string> l = new List<string>(); foreach (int d in old) l.Add(d.ToString());
                if (PackageStep.ParseDays(s.DayList).Count == 0) s.DayList = string.Join(", ", l.ToArray());
            }
            Rebind();
        }

        // Nur die Wann?-Felder neu fuellen und alles nachziehen (kein Neuaufbau der Equipment-Zeilen)
        void Rebind() { BindWhen(cur); dirty = true; UpdateView(false); }

        void BindWhen(PackageStep s)
        {
            bool was = loading; loading = true;
            nDay.Value = Math.Max(nDay.Minimum, Math.Min(nDay.Maximum, s.Day));
            cbRepeat.SelectedIndex = RepeatIndex(s);
            nCount.Value = Math.Max(1, Math.Min(PackageStep.MaxRuns, s.Count)); nInterval.Value = Math.Max(1, Math.Min(30, s.Interval));
            nUntil.Value = Math.Max(1, Math.Min(999, s.Until > 0 ? s.Until : s.Day));
            cbEnd.SelectedIndex = s.Until > 0 ? 1 : 0;
            for (int i = 0; i < 7; i++) chkDays[i].Checked = (s.WeekDays & (1 << i)) != 0;
            if (tDays.Text.Trim() != s.DayList) tDays.Text = s.DayList;
            nStartNo.Value = Math.Max(0, Math.Min(nStartNo.Maximum, s.StartNo));
            loading = was;
        }

        // Termine des Schritts mit Wochentag (Vorschau-Start = Montag der Woche)
        string DatesText(PackageStep s)
        {
            List<int> d = s.Days();
            List<string> l = new List<string>();
            for (int i = 0; i < d.Count && i < 14; i++) l.Add(d[i] + " " + weekStart.AddDays(d[i] - 1).ToString("ddd", Ui.De) + (s.Shifted(i) ? "*" : ""));
            return (d.Count == 1 ? "Termin: Tag " : "Termine (" + d.Count + "): Tag ") + string.Join(", ", l.ToArray()) + (d.Count > 14 ? " …" : "")
                + (s.Shift.Count > 0 ? "  (* = einzeln verschoben)" : "");
        }

        int LayoutWhen(PackageStep s)
        {
            int ri = RepeatIndex(s), y = gWhen.ContentTop;
            bool end = ri == 1 || ri == 2, num = s.Numbered;
            y = Row(y, ri != 3, lDay, nDay);
            y = Row(y, true, lRepeat, cbRepeat);
            y = Row(y, ri == 1, lInterval, nInterval, lIntervalUnit);
            y = Row(y, ri == 2, chkDays);
            y = Row(y, ri == 3, lDaysList, tDays); y = Row(y, ri == 3, lDaysHelp) - (ri == 3 ? 4 : 0);
            y = Row(y, end, lEnd, cbEnd, nCount, nUntil, lEndUnit);
            nCount.Visible = end && s.Until == 0; nUntil.Visible = end && s.Until > 0;
            lEndUnit.Text = s.Until > 0 ? "" : "mal";
            lDates.Text = DatesText(s); tip.SetToolTip(lDates, lDates.Text);
            y = Row(y, true, lDates);
            y = Row(y, more && num, lStartNo, nStartNo, lNoEx);
            lStartNo.Text = s.Kind != StepKind.Normal ? "Laufnummer ab:" : "Erste Nummer:";
            lNoEx.Text = num ? "→ " + s.RunNo(prefix, 0) : "";
            tip.SetToolTip(lNoEx, lNoEx.Text);
            return y + 2;
        }

        // ---------- Herstellung (Anzahl je Variante) und Ansatz ----------

        void BuildAnsatz(int lw, int x, int w)
        {
            pVar.Tag = "keep"; gWhat.Controls.Add(pVar);
            lAnsatz = Lbl(gWhat, "Ansatz:", 8, lw); cbAnsatz.SetBounds(x, 0, w, IH); cbAnsatz.DropDownStyle = ComboBoxStyle.DropDownList; cbAnsatz.Items.AddRange(PackageStep.AnsatzNames);
            lGebinde = Lbl(gWhat, "Gebinde:", 8, lw); tGebinde.SetBounds(x, 0, w, IH);
            lMenge = Lbl(gWhat, "Menge:", 8, lw); tMenge.SetBounds(x, 0, 60, IH); cbUnit.SetBounds(x + 66, 0, 70, IH); cbUnit.Items.AddRange(new object[] { "L", "mL", "kg", "g", "Stk" });
            gWhat.Controls.AddRange(new Control[] { cbAnsatz, tGebinde, tMenge, cbUnit });
            Grow(cbAnsatz, tGebinde); Ui.SimpleMenu(tGebinde); Ui.SimpleMenu(tMenge);
            tip.SetToolTip(cbAnsatz, "Tank: Vorlagetank/WFI-Tank als Equipment. Flasche/Gebinde oder ohne Gebinde: kein Tank nötig, Menge angeben.");
            tip.SetToolTip(tGebinde, "Bezeichnung, z. B. Flasche, Kanister, Tween-Gebinde");
            cbAnsatz.SelectedIndexChanged += delegate { if (Edit() && cbAnsatz.SelectedIndex >= 0) SetAnsatz(cur, PackageStep.AnsatzKeys[cbAnsatz.SelectedIndex]); };
            tGebinde.TextChanged += delegate { if (Edit()) { cur.Gebinde = tGebinde.Text.Trim(); Changed(); } };
            tMenge.TextChanged += delegate { if (Edit()) { cur.Menge = tMenge.Text.Trim(); Changed(); } };
            cbUnit.TextChanged += delegate { if (Edit()) { cur.Einheit = cbUnit.Text.Trim(); Changed(); } };
        }

        // Ohne Tank: leere Tank-Zeilen fallen weg (keine Hinweise); zurueck auf Tank: Tank-Positionen des Bausteins wieder anbieten
        void SetAnsatz(PackageStep s, string key)
        {
            if (s.Ansatz == key) return;
            s.Ansatz = key;
            if (key.Length > 0) s.Equipment.RemoveAll(delegate(EquipAssign a) { return PackageStep.IsTank(cat, a.Short) && a.Line(0).Length == 0; });
            else
            {
                Baustein b = cat.Baustein_(s.Source);
                if (b != null)
                    foreach (string p in b.Positions)
                        if (PackageStep.IsTank(cat, p) && !s.Equipment.Exists(delegate(EquipAssign a) { return a.Short == p; })) { EquipAssign a = new EquipAssign(); a.Short = p; s.Equipment.Add(a); }
            }
            dirty = true;
            Bind(s);
        }

        void BindAnsatz(PackageStep s)
        {
            cbAnsatz.SelectedIndex = Math.Max(0, Array.IndexOf(PackageStep.AnsatzKeys, s.Ansatz));
            tGebinde.Text = s.Gebinde; tMenge.Text = s.Menge; cbUnit.Text = s.Einheit;
        }

        int LayoutAnsatz(int y, PackageStep s)
        {
            bool show = s.UsesAnsatz;
            y = Row(y, show, lAnsatz, cbAnsatz);
            y = Row(y, show && s.Ansatz == "gebinde", lGebinde, tGebinde);
            return Row(y, show && s.Ansatz.Length > 0, lMenge, tMenge, cbUnit);
        }

        // Herstellung: je Variante der Zeile ein Feld "Anzahl" (0 = nicht); gesetzte, an der Zeile unbekannte Varianten bleiben sichtbar
        void FillVariants(PackageStep s)
        {
            string[] vs; if (!rowVars.TryGetValue(s.Row, out vs)) vs = new string[0];
            List<string> names = new List<string>(vs);
            foreach (string v in VariantSet.Of(s.Variant)) if (!names.Contains(v)) names.Add(v);
            pVar.SuspendLayout();
            while (pVar.Controls.Count > 0) { Control c = pVar.Controls[0]; pVar.Controls.RemoveAt(0); c.Dispose(); }
            foreach (string v in names)
            {
                string name = v;
                NumericUpDown n = new NumericUpDown(); n.Minimum = 0; n.Maximum = VariantSet.Max; n.Width = 44; n.Height = IH; n.TextAlign = HorizontalAlignment.Right;
                n.Value = VariantSet.Count(s.Variant, v); n.Tag = v;
                n.ValueChanged += delegate { if (Edit()) { cur.Variant = VariantSet.With(cur.Variant, name, (int)n.Value); Changed(); } };
                Label l = new Label(); l.Text = "× " + VarText(v); l.AutoSize = true; l.UseMnemonic = false;
                tip.SetToolTip(n, "Wie oft „" + VarText(v) + "“ an diesem Tag hergestellt wird (0 = nicht)");
                pVar.Controls.Add(n); pVar.Controls.Add(l);
            }
            pVar.ResumeLayout();
        }

        // Felder nebeneinander, bei Platzmangel umbrechen; liefert die naechste y-Position
        int LayoutVariants(int y, bool show)
        {
            lVar.Visible = pVar.Visible = show;
            if (!show) return y;
            int x = tTitle.Left, w = Math.Max(80, tTitle.Width), px = 0, py = 0;
            for (int i = 0; i + 1 < pVar.Controls.Count; i += 2)
            {
                Control n = pVar.Controls[i], l = pVar.Controls[i + 1];
                int pw = n.Width + 4 + l.PreferredSize.Width + 10;
                if (px > 0 && px + pw > w) { px = 0; py += IH + 4; }
                n.Location = new Point(px, py); l.Location = new Point(px + n.Width + 4, py + (IH - l.PreferredSize.Height) / 2);
                px += pw;
            }
            lVar.Top = y; pVar.SetBounds(x, y, w, py + IH + 1);
            return y + py + IH + 6;
        }
    }
}
