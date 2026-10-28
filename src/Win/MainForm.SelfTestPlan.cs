#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Oberflaeche: Schichtmodus (Hoehen, Editoren, Daten bleiben), Pruefleiste (Zustaende im Fenster Sa-Fr,
    // mobil/stationaer, Abhaken mit Konflikt, Variante im Editor) und Rundgaenge (Audit, anderer PC, Leser).
    partial class MainForm
    {
        CellData CellOf(int row, int col) { return view.Cells.Find(delegate(CellData c) { return c.RowIndex == row && c.Col == col; }); }
        int RowOf(string name) { return rowInfo.FindIndex(delegate(RowInfo r) { return r.Name == name; }); }
        void Expand(int row) { fold[rowInfo[row].Id] = false; ApplyFold(); view.Relayout(); }

        void SelfTestPlan(StringBuilder sb)
        {
            sb.AppendLine(DutiesForm.SelfTest());
            if (systemPc) { sb.AppendLine("Schichtmodus: übersprungen (Systemrechner)\nPruefleiste: übersprungen\nRundgaenge-UI: übersprungen"); return; }
            DateTime keep = start;
            try { SelfTestModes(sb); SelfTestChecks(sb); }
            finally { if (editCell != null) EndEdit(false); start = keep; Rebuild(true); }
        }

        void SelfTestModes(StringBuilder sb)
        {
            Rebuild(true);
            int vs = RowOf("Versand"), cc = RowOf("Raum CC");
            if (vs < 0 || cc < 0) { sb.AppendLine("Schichtmodus: FEHLER Testzeilen fehlen"); return; }
            Expand(vs); Expand(cc);
            CellData v = CellOf(vs, 1), t = CellOf(cc, 2);
            bool okH = v.RPpl[0].Height > 0 && v.RPpl[1].Height > 0 && v.RPpl[2].Height == 0
                && t.RPpl[0].Height > 0 && t.RPpl[1].Height == 0 && t.RPpl[2].Height == 0 && t.RLbl[0].Width == 0 && t.RPpl[0].Height < v.RPpl[0].Height;
            // N-Kuerzel im F/S-Block: bleibt beim Bearbeiten/Speichern erhalten und ist nach dem Zurueckschalten wieder da
            int vid = rowInfo[vs].Id; DateTime vd = v.Day;
            Entry e = store.Get(vd, vid) ?? new Entry(); e.People[2] = "NNNN"; store.Save(vd, vid, e);
            Rebuild(false); Expand(vs);
            v = CellOf(vs, 1);
            BeginEdit(v);
            bool okEd = editCell == v && edContent.Visible && edPpl[0].Visible && edPpl[1].Visible && !edPpl[2].Visible;
            edContent.Text = edContent.Text + " geprüft";
            EndEdit(true);
            CellData tc = CellOf(cc, 2);
            BeginEdit(tc);
            okEd &= editCell == tc && edPpl[0].Visible && !edPpl[1].Visible && !edPpl[2].Visible && edPpl[0].Bottom <= tc.R.Bottom;
            EndEdit(false);
            Entry saved = store.Get(vd, vid);
            bool kept = saved.People[2] == "NNNN" && saved.Content.EndsWith("geprüft");
            List<RowInfo> rs = store.GetRows(); rs[vs].Mode = ShiftMode.FSN; store.SaveStructure(rs);
            Rebuild(false); Expand(vs);
            v = CellOf(vs, 1);
            bool back = v.People[2] == "NNNN" && v.RPpl[2].Height > 0;
            rs = store.GetRows(); rs[vs].Mode = ShiftMode.FS; store.SaveStructure(rs);
            Rebuild(false);
            sb.AppendLine("Schichtmodus: Höhen F/S + Tagesaufgabe=" + Ok(okH) + " Editoren nur über sichtbaren Feldern=" + Ok(okEd)
                + " N-Kürzel bleibt beim Speichern=" + Ok(kept) + " nach Zurückschalten da=" + Ok(back));
        }

        void SelfTestChecks(StringBuilder sb)
        {
            DateTime w = new DateTime(2025, 9, 1);   // Mo KW36; Fenster ab Sa 06.09.: Sa/So = KW36, Mo-Fr = KW37 (keine Feiertage)
            int lr = rowInfo.FindIndex(delegate(RowInfo r) { return r.HasLkz; }), pr = RowOf("Puffer");
            if (lr < 0 || pr < 0) { sb.AppendLine("Pruefleiste: FEHLER Testzeilen fehlen"); return; }
            int lid = rowInfo[lr].Id, pid = rowInfo[pr].Id;
            store.Save(w.AddDays(5), lid, E("Lauf", false, ""));
            store.Save(w.AddDays(7), lid, E("Lauf", true, ""));
            store.Save(w.AddDays(9), lid, E("Lauf", false, ""));
            store.Save(w.AddDays(7), pid, E("Herstellung", false, "mobil"));
            store.Save(w.AddDays(8), pid, E("Herstellung", false, "stationaer"));
            store.Save(w.AddDays(9), pid, E("Herstellung", false, ""));
            store.Save(w.AddDays(10), pid, E("Herstellung", false, ""));
            start = w.AddDays(5); Rebuild(true);

            int li = view.Defs[lr].FindIndex(delegate(CheckDef d) { return d.Key == CheckDef.LkzKey; });
            CellData sa = CellOf(lr, 0), so = CellOf(lr, 1), mo = CellOf(lr, 2), di = CellOf(lr, 3), mi = CellOf(lr, 4);
            bool okLkz = li >= 0 && sa.States[li] == DayState.Missing && so.States[li] == DayState.WeekOpen && mo.States[li] == DayState.Done
                && di.States[li] == DayState.NotNeeded && mi.States[li] == DayState.Missing
                && so.Note == "LKZ KW36 NICHT GEMESSEN – dringend messen" && so.Urgent && sa.Note == so.Note && mo.Note.Length == 0 && !mo.Urgent;   // KW36 liegt zurueck: dringend
            bool okGroups = view.Rounds.Count == 2 && view.Rounds[0].First == 0 && view.Rounds[0].Last == 1 && view.Rounds[0].Monday == w
                && view.Rounds[1].First == 2 && view.Rounds[1].Last == 6 && view.Rounds[1].Monday == w.AddDays(7) && !view.Rounds[0].Collapsed;

            List<CheckDef> pd = view.Defs[pr];
            int pi = pd.FindIndex(delegate(CheckDef d) { return d.Key == "PROBE"; }), wi = pd.FindIndex(delegate(CheckDef d) { return d.Key == "WFIW"; });
            bool okP = pi >= 0 && wi >= 0 && CellOf(pr, 2).States[pi] == DayState.Auto && CellOf(pr, 3).States[pi] == DayState.Missing
                && CellOf(pr, 0).States[pi] == DayState.NA && CellOf(pr, 2).States[wi] == DayState.NA && CellOf(pr, 0).States[wi] == DayState.WeekOpen
                && CellOf(pr, 0).Note == "WFI-Woche KW36 offen";
            if (pi < 0) { sb.AppendLine("Pruefleiste: FEHLER Puffer-Pflichtpunkte fehlen"); return; }

            // Abhaken ohne Bearbeitungsmodus; danach Konflikt: anderer PC hat den Block inzwischen geaendert
            CellData pm = CellOf(pr, 4);
            ToggleMark(pm, "PROBE");
            bool okMark = store.Get(w.AddDays(9), pid).Marks.Contains("PROBE") && pm.States[pi] == DayState.Done;
            CellData pt = CellOf(pr, 5);
            Entry other = store.Get(w.AddDays(10), pid).Clone(); other.Content = "Von anderem PC"; store.Save(w.AddDays(10), pid, other);
            ToggleMark(pt, "PROBE");
            Entry te = store.Get(w.AddDays(10), pid);
            bool okConf = te.Content == "Von anderem PC" && te.Marks.Contains("PROBE") && pt.Content == te.Content && pt.Marks.Contains("PROBE");
            // Variante im Bearbeitungsmodus: wird mit dem Block gespeichert
            Expand(pr);
            CellData pv = CellOf(pr, 2);
            BeginEdit(pv);
            SetVariant(pv, "stationaer");
            bool dirty = HasUnsavedChanges;
            EndEdit(true);
            bool okVar = dirty && store.Get(w.AddDays(7), pid).Variant == "stationaer" && CellOf(pr, 2).States[pi] == DayState.Missing;
            sb.AppendLine("Pruefleiste: LKZ Sa-Fr (fehlt/KW offen/gemessen/nicht nötig)=" + Ok(okLkz) + " KW-Gruppen=" + Ok(okGroups)
                + " Puffer mobil/stationär/WFI=" + Ok(okP) + " abhaken=" + Ok(okMark) + " Konflikt Inhalt behalten=" + Ok(okConf) + " Variante im Editor=" + Ok(okVar));

            // Rundgaenge: Haken mit Protokoll, anderer PC hat schon gehakt, wieder entfernen, Leser darf nicht, Zuklappen
            DateTime m2 = w.AddDays(7);
            ToggleRound(view.Rounds[1], "GMP1");
            List<AuditEntry> au = store.GetAudit(m2, -1);
            AuditEntry last = au.Count > 0 ? au[au.Count - 1] : null;
            bool okR = view.Rounds[1].Done.Contains("GMP1") && last != null && last.Mark == "GMP1" && last.Value == "1" && last.Login == me.Login && last.RowName == "1. GMP-Rundgang";
            WeekMark ow = new WeekMark(); ow.Monday = m2; ow.Key = "EHS"; ow.Done = true; store.SaveWeekMark(ow, null);   // anderer PC, Ansicht veraltet
            int n0 = store.GetAudit(m2, -1).Count;
            ToggleRound(view.Rounds[1], "EHS");
            bool okOther = view.Rounds[1].Done.Contains("EHS") && store.GetAudit(m2, -1).Count == n0;
            ToggleRound(view.Rounds[1], "EHS");
            au = store.GetAudit(m2, -1);
            bool okOff = !view.Rounds[1].Done.Contains("EHS") && au.Count == n0 + 1 && au[au.Count - 1].Value == "0";
            bool had = me.Rights.Remove(Perm.Rounds);
            try { ToggleRound(view.Rounds[1], "GMP2"); }
            finally { if (had) me.Rights.Add(Perm.Rounds); }
            bool okLeser = !store.GetWeekMarks(m2).Exists(delegate(WeekMark m) { return m.Key == "GMP2"; }) && store.GetAudit(m2, -1).Count == n0 + 1;
            ToggleRoundFold(view.Rounds[0]);
            bool okFold = view.Rounds[0].Collapsed && view.Rounds[0].RItems.Length == 0 && view.Rounds[1].RItems.Length == view.Rounds[1].Defs.Count;
            bool okWeek = view.Rounds[0].DoneCount == 0;   // KW36 zaehlt nur ihre eigenen Haken
            sb.AppendLine("Rundgaenge-UI: Haken+Protokoll=" + Ok(okR) + " anderer PC schon gehakt=" + Ok(okOther) + " entfernen=" + Ok(okOff)
                + " Leser darf nicht=" + Ok(okLeser) + " zuklappen=" + Ok(okFold) + " KW getrennt=" + Ok(okWeek));
        }
    }
}
#endif
