using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Zeitmodell-Sicht und Ablauftabelle "Nr | Wann | Raum | Schritt | Läufe/Termine | Wiederholung".
    // Alle Termine kommen aus Package.Schedule() (nie aus den Raster-Feldern eines Schritts); Texte aus Package.RelText/DateRangeText/RunsText.
    // Anzeige relativ zu Lauf 1 ("3 Tage vor Lauf 1"), mit Datum, sobald "Lauf 1 am" im Kopf gesetzt ist; nie "Tag n".
    partial class PackageForm
    {
        Package pk = new Package();   // Sicht auf steps + Kopf (Lauf 1 am, Typ, Wochentag von Tag 1); gleiche Schritt-Objekte

        Package StepsView() { pk.Steps = steps; pk.RunPrefix = prefix; return pk; }

        List<RunDate> Plan() { return StepsView().Schedule(); }

        // Folgt der Schritt jedem Lauf des Hauptlaufs (Bezug "zu jedem Lauf")?
        bool IsJe(PackageStep s) { PackageStep h = StepsView().Head; return s.Bind == StepBind.JeLauf && h != null && s != h; }

        // Erster Termin eines Schritts (Paket-Tag); ohne Termin (leerer Schritt) das Raster
        int FirstOf(PackageStep s, List<RunDate> plan)
        {
            int si = steps.IndexOf(s), m = int.MaxValue;
            foreach (RunDate d in plan) if (d.Step == si && d.Day < m) m = d.Day;
            return m == int.MaxValue ? s.FirstDay : m;
        }

        // Wie im Plan: nach erstem Datum; am selben Tag gilt die vom Nutzer gesetzte Reihenfolge (Index in der Schrittliste).
        List<PackageStep> Ordered()
        {
            List<RunDate> plan = Plan();
            Dictionary<PackageStep, int> first = new Dictionary<PackageStep, int>();
            foreach (PackageStep s in steps) first[s] = FirstOf(s, plan);
            List<PackageStep> o = new List<PackageStep>(steps);
            o.Sort(delegate(PackageStep a, PackageStep b) { return first[a] != first[b] ? first[a].CompareTo(first[b]) : steps.IndexOf(a).CompareTo(steps.IndexOf(b)); });
            return o;
        }

        int No(PackageStep s) { return Ordered().IndexOf(s) + 1; }
        static string TitleOf(PackageStep s) { return s.Title.Length > 0 ? s.Title : "(ohne Titel)"; }

        // Naechster freier Tag nach allen Terminen (fuer neue Schritte mit eigenem Datum)
        int NextDay() { int d = 0; foreach (RunDate r in Plan()) d = Math.Max(d, r.Day); return d + 1; }

        // ---------- Texte ----------

        static string DText(DateTime d) { return PackageStep.DayNames[((int)d.DayOfWeek + 6) % 7] + " " + d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); }

        static string DayW(int n) { return n + (n == 1 ? " Tag" : " Tage"); }

        // "3 Tage vor jedem Lauf", "am Tag jedes Laufs", "1 Tag nach jedem Lauf"
        static string OffsetText(int off)
        {
            return off == 0 ? "am Tag jedes Laufs" : DayW(Math.Abs(off)) + (off < 0 ? " vor" : " nach") + " jedem Lauf";
        }

        // "2 Tage vor dem Lauf", "am Tag des Laufs"
        static string CardOffText(int off) { return PufferCard.OffText(off); }

        // Bezug eines Termins: JeLauf zum eigenen Lauf ("1 Tag vor Lauf 2"), sonst RelText (mit Datum, wenn Lauf 1 bekannt)
        string RelOf(PackageStep s, RunDate d) { return RelOf(s, d, null); }

        // Puffer mit eigenem Rhythmus: welchen Lauf der Termin bedient (naechster Lauf am oder nach dem Termin), "" bei anderen Schritten
        string ForLauf(PackageStep s, RunDate d, List<RunDate> plan)
        {
            if (!IsPuffer(s) || IsJe(s) || StepsView().Head == null) return "";
            int l = StepsView().LaufOf(plan ?? Plan(), d);
            return l >= 0 ? " · für Lauf " + (l + 1) : " · nach dem letzten Lauf";
        }

        string RelOf(PackageStep s, RunDate d, List<RunDate> plan) { return RelOf(s, d, plan, 0); }

        // delta: Puffer-Karte mit eigenem Versatz (Tage gegenueber dem Termin des Schritts)
        string RelOf(PackageStep s, RunDate d, List<RunDate> plan, int delta)
        {
            Package p = StepsView();
            if (!IsJe(s)) return p.RelText(d.Day) + ForLauf(s, d, plan);
            int off = s.RunOffset + delta;
            string rel = off == 0 ? "Lauf " + (d.Run + 1) : DayW(Math.Abs(off)) + (off < 0 ? " vor" : " nach") + " Lauf " + (d.Run + 1);
            DateTime? dt = p.DateOf(d.Day + delta);
            return dt.HasValue ? DText(dt.Value) + " · " + rel : rel;
        }

        // Spalte "Wann": Datum bzw. Datumsbereich, sonst der Bezug zu Lauf 1
        string WhenText(PackageStep s)
        {
            Package p = StepsView();
            if (IsJe(s) && !p.Lauf1.HasValue) return OffsetText(s.RunOffset);
            string t = p.DateRangeText(s);
            return t.Length > 0 ? t : "–";
        }

        // Spalte "Wiederholung" im Klartext
        string RepeatPlain(PackageStep s)
        {
            string t;
            if (IsJe(s)) t = s.RunEvery > 1 ? "zu jedem " + s.RunEvery + ". Lauf" : "zu jedem Lauf";
            else if (s.Repeat == RepeatMode.DayList) t = "an festen Terminen";
            else if (s.Repeat == RepeatMode.Weekdays) t = (s.WeekDays & 127) == 0 ? "Wochentage fehlen" : PackageStep.WeekDayText(s.WeekDays).Replace(",", ", ");
            else if (StepsView().RunCountOf(s) <= 1) t = "einmalig";
            else { int iv = Math.Max(1, s.Interval); t = iv == 1 ? "täglich" : "alle " + iv + " Tage"; }
            if (IsPuffer(s) && !IsJe(s) && StepsView().Head != null && StepsView().RunCountOf(s) > 1) t = "eigen: " + t;   // UX-5c: eigener Rhythmus, unabhaengig vom Lauf-Raster
            return s.Shift.Count > 0 && !IsJe(s) ? t + " · " + s.Shift.Count + (s.Shift.Count == 1 ? " Ausnahme" : " Ausnahmen") : t;
        }

        // Kopf einer Kalenderspalte: Datum ("Fr 12.03."), ohne Datum der Bezug zu Lauf 1; schmale leere Spalten nur der Monatstag
        string StripHead(int day, bool wide)
        {
            Package p = StepsView();
            DateTime? d = p.DateOf(day);
            if (!wide) return d.HasValue ? d.Value.Day.ToString() : "";
            if (d.HasValue) return PackageStep.DayNames[((int)d.Value.DayOfWeek + 6) % 7] + " " + d.Value.ToString("dd.MM.", CultureInfo.InvariantCulture);
            string rel = p.RelText(day);   // schmale Spalte: "2 Tage vor Lauf 1" -> "2 T. vor L1" (der Kasten zeigt den Schritt, Tooltip den Volltext)
            return rel.Replace(" Tage vor Lauf ", " T. vor L").Replace(" Tag vor Lauf ", " T. vor L").Replace(" Tage nach Lauf ", " T. nach L").Replace(" Tag nach Lauf ", " T. nach L");
        }

        // ---------- Tabelle ----------

        void FillList()
        {
            bool was = loading; loading = true;
            Package p = StepsView();
            List<PackageStep> o = Ordered();
            lv.BeginUpdate();
            while (lv.Items.Count > o.Count) lv.Items.RemoveAt(lv.Items.Count - 1);
            for (int i = 0; i < o.Count; i++)
            {
                PackageStep s = o[i];
                string when = WhenText(s), runs = p.RunsText(s), rep = RepeatPlain(s);
                string[] t = { (i + 1).ToString(), when, s.Row.Length > 0 ? s.Row : "– fehlt –", TitleOf(s), runs, rep };
                if (i >= lv.Items.Count) { ListViewItem n = new ListViewItem(t); n.UseItemStyleForSubItems = false; lv.Items.Add(n); }
                ListViewItem it = lv.Items[i];
                for (int k = 0; k < t.Length; k++) if (it.SubItems[k].Text != t[k]) it.SubItems[k].Text = t[k];
                it.SubItems[2].ForeColor = s.Row.Length > 0 ? SystemColors.WindowText : Color.Firebrick;
                it.Tag = s;
                it.ToolTipText = when + " · " + (s.Row.Length > 0 ? s.Row : "Raum fehlt") + " · " + TitleOf(s) + " · " + runs + " · " + rep;
                if (it.Selected != (s == cur)) it.Selected = s == cur;
                if (s == cur) it.EnsureVisible();
            }
            lv.EndUpdate();
            FitColumns();
            loading = was;
        }

        public void SelectStep(int i) { List<PackageStep> o = Ordered(); if (i >= 0 && i < o.Count) Bind(o[i]); }

        void FitColumns()
        {
            // Nr, Raum, Wiederholung fest; Wann, Schritt, Laeufe/Termine teilen den Rest (volle Texte stehen im Tooltip)
            int rest = lv.ClientSize.Width - 26 - 56 - 92;
            lv.Columns[0].Width = 26; lv.Columns[2].Width = 56; lv.Columns[5].Width = 92;
            lv.Columns[1].Width = Math.Max(60, rest * 40 / 100); lv.Columns[3].Width = Math.Max(60, rest * 28 / 100); lv.Columns[4].Width = Math.Max(60, rest * 32 / 100);
        }
    }
}
