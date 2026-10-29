#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Wochenansicht 2: Varianten-Chips (Klick speichert sofort), LKZ dringend ab Sonntag, Haken an leerem Tag
    // in vergangener KW, Rundgang-Datum (am/von, Protokoll fuer alte Haken, XML), Leiste offen bei 5/5, Auswahlrahmen.
    partial class MainForm
    {
        void SelfTestAnsicht2(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Ansicht2: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            try { SelfTestAnsicht2Run(sb); }
            catch (Exception ex) { sb.AppendLine("Ansicht2: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally { if (editCell != null) EndEdit(false); start = keep; Rebuild(true); }
        }

        void SelfTestAnsicht2Run(StringBuilder sb)
        {
            DateTime wk = D(2024, 6, 3);   // Mo KW23, ohne Testdaten
            int lr = rowInfo.FindIndex(delegate(RowInfo r) { return r.HasLkz; }), pr = RowOf("Puffer");
            if (lr < 0 || pr < 0) { sb.AppendLine("Ansicht2: FEHLER Testzeilen fehlen"); return; }
            int lid = rowInfo[lr].Id, pid = rowInfo[pr].Id;

            // LKZ-Regel: offen bis Samstag, dringend ab Sonntag, ein Haken an einem leeren Tag beendet es
            MemoryStore ms = new MemoryStore();
            int rid = ms.GetRows()[0].Id;
            CheckDef lkz = CheckDef.Lkz(rid);
            bool okRule = !CheckEval.Overdue(ms, lkz, wk, wk.AddDays(5)) && CheckEval.Overdue(ms, lkz, wk, wk.AddDays(6)) && CheckEval.Overdue(ms, lkz, wk.AddDays(3), wk.AddDays(30));
            ms.Save(wk.AddDays(1), rid, E("", true, ""));
            okRule &= !CheckEval.Overdue(ms, lkz, wk, wk.AddDays(6));

            // Ansicht: vergangene KW ohne Haken = dringend; Klick auf den Haken an einem leeren Tag
            start = wk; Rebuild(true); Expand(lr);
            CellData mi = CellOf(lr, 2);
            bool urgentBefore = mi.Urgent && CellOf(lr, 0).Note.StartsWith("LKZ KW23 NICHT GEMESSEN") && CellOf(lr, 6).Urgent;
            view.TestClick(view.SlotPoint(mi, null, 0));
            bool all = true; for (int c = 0; c < 7; c++) all &= !CellOf(lr, c).Urgent;
            Entry le = store.Get(wk.AddDays(2), lid);
            bool okClick = le != null && le.Lkz && all;

            // Chips: Klick toggelt sofort und speichert (ohne Menue), beide gleichzeitig moeglich
            Expand(pr);
            CellData pc = CellOf(pr, 1);
            Point pm = view.SlotPoint(pc, "mobil", -1), ps = view.SlotPoint(pc, "stationaer", -1);
            int pi = view.Defs[pr].FindIndex(delegate(CheckDef d) { return d.Key == "PROBE"; });
            view.TestClick(pm);
            Entry v1 = store.Get(wk.AddDays(1), pid);
            bool auto = pi >= 0 && CellOf(pr, 1).States[pi] == DayState.Auto;
            pc = CellOf(pr, 1); ps = view.SlotPoint(pc, "stationaer", -1);   // aktiver Chip ist breiter (+/-): Lage neu bestimmen
            view.TestClick(ps);
            Entry v2 = store.Get(wk.AddDays(1), pid);
            pc = CellOf(pr, 1); pm = view.SlotPoint(pc, "mobil", -1);
            view.TestClick(pm);
            Entry v3 = store.Get(wk.AddDays(1), pid);
            bool okChips = !pm.IsEmpty && !ps.IsEmpty && v1 != null && v1.Types == "mobil" && auto && v2.Types == "mobil|stationaer" && v3.Types == "stationaer";

            // Rundgaenge: Datum/Kuerzel aus dem Haken, alte Haken aus dem Protokoll, Leiste bleibt bei 5/5 offen
            List<RoundDef> act = duties.Rounds.FindAll(delegate(RoundDef d) { return d.Active; });
            for (int i = 0; i < act.Count; i++)
            {
                WeekMark m = new WeekMark(); m.Monday = wk; m.Key = act[i].Key; m.Done = true;
                AuditEntry a = new AuditEntry(); a.Time = new DateTime(2024, 6, 5, 14, 0, 0); a.Login = "alt"; a.Kuerzel = "ALT";
                if (i > 0) { m.At = new DateTime(2024, 6, 4, 9, 30, 0); m.By = "TEST"; }
                store.SaveWeekMark(m, a);
            }
            Rebuild(true);
            RoundGroup g = view.Rounds.Count > 0 ? view.Rounds[0] : null;
            bool okStamp = g != null && act.Count > 1 && g.Stamp(act[0].Key) == "Mi 05.06. ALT" && g.Stamp(act[1].Key) == "Di 04.06. TEST";
            bool okOpen = g != null && g.Complete && !g.Collapsed && g.RItems.Length == act.Count;
            if (g != null && me.Has(Perm.Rounds))
            {
                ToggleRound(view.Rounds[0], act[1].Key); ToggleRound(view.Rounds[0], act[1].Key);   // ab und wieder an: neuer Stempel
                WeekMark nm = store.GetWeekMarks(wk).Find(delegate(WeekMark m) { return m.Key == act[1].Key; });
                okStamp &= nm != null && nm.Done && nm.At.Date == DateTime.Today && nm.By == (me.Kuerzel.Length > 0 ? me.Kuerzel : me.Login);
            }
            MemoryStore xs = new MemoryStore();
            WeekMark x1 = new WeekMark(); x1.Monday = wk; x1.Key = "GMP1"; x1.Done = true; x1.At = new DateTime(2024, 6, 4, 9, 30, 15); x1.By = "TEST";
            WeekMark x2 = new WeekMark(); x2.Monday = wk; x2.Key = "EHS"; x2.Done = true;
            xs.SaveWeekMark(x1, null); xs.SaveWeekMark(x2, null);
            StoreData rt = Backup.FromXml(Backup.ToXml(xs.Snapshot()));
            WeekMark r1 = rt.WeekMarks.Find(delegate(WeekMark m) { return m.Key == "GMP1"; }), r2 = rt.WeekMarks.Find(delegate(WeekMark m) { return m.Key == "EHS"; });
            bool okXml = r1 != null && r1.At == x1.At && r1.By == "TEST" && r2 != null && r2.At == DateTime.MinValue && r2.By == "";

            // Auswahlrahmen: Block-Klick waehlt, Klick auf den Kopf (Zuklappen) / freie Flaeche / Esc hebt auf
            Expand(lr);
            CellData sc = CellOf(lr, 3);
            view.TestClick(new Point(sc.RContent.X + 5, sc.RContent.Y + 5));
            bool sel1 = view.Selected == sc;
            view.TestClick(new Point(sc.RHead.X + 10, sc.RHead.Y + 5));
            bool gone = view.Selected == null && view.Collapsed[lr];
            Expand(lr); sc = CellOf(lr, 3);
            view.TestClick(new Point(sc.RContent.X + 5, sc.RContent.Y + 5));
            view.TestClick(new Point(2, sc.RContent.Y + 5));
            bool free = view.Selected == null;
            view.TestClick(new Point(sc.RContent.X + 5, sc.RContent.Y + 5));
            OnKey(this, new KeyEventArgs(Keys.Escape));
            bool esc = view.Selected == null;
            bool okSel = sel1 && gone && free && esc;

            sb.AppendLine("Ansicht2: Chips toggeln speichert=" + Ok(okChips) + " LKZ dringend ab Sonntag=" + Ok(okRule && urgentBefore) + " Haken an leerem Tag (vergangene KW)=" + Ok(okClick)
                + " Rundgang-Datum=" + Ok(okStamp) + " Leiste offen bei " + (g != null ? g.DoneCount + "/" + g.Defs.Count : "-") + "=" + Ok(okOpen) + " am/von im XML=" + Ok(okXml)
                + " Auswahl weg (Zuklappen/Fläche/Esc)=" + Ok(okSel));
        }
    }
}
#endif
