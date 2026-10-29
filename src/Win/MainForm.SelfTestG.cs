#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Block G: mehrere Puffer-Varianten je Tag + Wochenende/Feiertag (G0).
    partial class MainForm
    {
        static Entry EntryIn(StoreData d, DateTime day, int row)
        {
            foreach (StoredEntry se in d.Entries) if (se.Day == day && se.RowId == row) return se.Data;
            return null;
        }

        void SelfTestG(StringBuilder sb)
        {
            SelfTestG0(sb);
            SelfTestG1(sb);
            SelfTestG2(sb);
            SelfTestG3(sb);
            SelfTestG4(sb);
        }

        // Offene Pflichten: bekannte Testdaten KW45/2026 (heute = Mi 04.11.), leere Woche, naechste KW, Springe dorthin
        void SelfTestG4(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows();
            int s1 = rows.Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id, pid = PufferSetup.Row(rows).Id;
            DateTime mo = D(2026, 11, 2), today = D(2026, 11, 4);
            s.Save(mo, s1, E("Lauf 1", false, ""));                 // LKZ fehlt
            s.Save(mo.AddDays(1), s1, E("Lauf 2", true, ""));       // gemessen -> KW erfuellt
            s.Save(mo.AddDays(4), s1, E("Lauf 3", false, ""));      // LKZ faellig (Zukunft)
            s.Save(mo, pid, E("Herstellung", false, "mobil"));      // Tagesprobe automatisch
            s.Save(mo.AddDays(1), pid, E("Herstellung", false, "stationaer"));   // Tagesprobe fehlt, WFI entfaellt
            WeekMark gm = new WeekMark(); gm.Monday = mo; gm.Key = "GMP1"; gm.Done = true; s.SaveWeekMark(gm, null);
            DutyConfig cfg = s.GetDuties();
            List<OpenDuty> l = OpenDuties.List(s, cfg, rows, mo.AddDays(3), 1, today);
            Predicate<OpenDuty> lkzMo = delegate(OpenDuty o) { return o.RowId == s1 && o.Day == mo && o.State == "fehlt" && o.Overdue && o.When == "Mo 02.11."; };
            Predicate<OpenDuty> lkzFr = delegate(OpenDuty o) { return o.RowId == s1 && o.Day == mo.AddDays(4) && o.State == "fällig" && !o.Overdue; };
            Predicate<OpenDuty> kwOpen = delegate(OpenDuty o) { return o.Week && o.Where == "Mix / Split" && o.State == "KW offen" && o.When == "KW45"; };
            int probe = l.FindAll(delegate(OpenDuty o) { return o.RowId == pid && o.What == "Tagesprobe (täglich)"; }).Count;   // Di, Mi fehlt; Do, Fr faellig
            int rounds = l.FindAll(delegate(OpenDuty o) { return o.RowId == -1; }).Count;
            bool okList = l.Exists(lkzMo) && l.Exists(lkzFr) && l.Exists(kwOpen) && probe == 4 && rounds == 4
                && !l.Exists(delegate(OpenDuty o) { return o.What == "Manuelle Probe (wöchentlich)"; }) && l.Count == 13 && l[0].Week;
            // Wochenprobe: KW46 nur mobil, kein Haken -> manuelle Probe offen und in der Liste; mit Haken weg
            DateTime k46 = D(2026, 11, 9);
            s.Save(k46, pid, E("Herstellung", false, "mobil"));
            Predicate<OpenDuty> man = delegate(OpenDuty o) { return o.RowId == pid && o.What == "Manuelle Probe (wöchentlich)" && o.Week; };
            bool okMan = OpenDuties.List(s, cfg, rows, k46, 1, today).Exists(man);
            s.Save(k46.AddDays(1), pid, E("", false, "", "WFIW"));
            okMan &= !OpenDuties.List(s, cfg, rows, k46, 1, today).Exists(man);
            okList &= okMan;
            bool okNext = OpenDuties.List(s, cfg, rows, mo, 2, today).Exists(delegate(OpenDuty o) { return o.Day >= mo.AddDays(7); });
            // leer: nur Suite 1 mit gemessenem LKZ, alle Rundgaenge der KW abgehakt
            DateTime k47 = D(2026, 11, 16);
            s.Save(k47, s1, E("", true, ""));
            foreach (RoundDef r in cfg.Rounds) { WeekMark m = new WeekMark(); m.Monday = k47; m.Key = r.Key; m.Done = true; s.SaveWeekMark(m, null); }
            List<RowInfo> only = rows.FindAll(delegate(RowInfo r) { return r.Id == s1; });
            bool okEmpty = OpenDuties.List(s, cfg, only, k47, 1, today).Count == 0;
            // Springe dorthin: andere Woche, Zeile aufgeklappt und ausgewaehlt; Rundgang klappt die Leiste auf
            DateTime keep = start;
            OpenDuty t = new OpenDuty(); t.Day = start.AddDays(16); t.RowId = rowInfo[rowInfo.Count - 1].Id;
            JumpTo(t);
            bool okJump = start == MondayOf(t.Day) && view.Selected != null && view.Selected.Day == t.Day && rowInfo[view.Selected.RowIndex].Id == t.RowId && !view.Collapsed[view.Selected.RowIndex];
            if (view.Rounds.Count > 0 && !view.Rounds[0].Collapsed) ToggleRoundFold(view.Rounds[0]);
            OpenDuty tr = new OpenDuty(); tr.Day = start; tr.RowId = -1;
            JumpTo(tr);
            okJump &= view.Rounds.Count == 0 || !view.Rounds[0].Collapsed;
            start = keep; roundFold.Clear(); Rebuild(true);
            sb.AppendLine("Offene Pflichten: Liste (LKZ fehlt/fällig/KW offen, Tagesprobe, Rundgänge, WFI entfällt)=" + Ok(okList) + " [" + l.Count + "] nächste KW=" + Ok(okNext)
                + " leer=" + Ok(okEmpty) + " Springe dorthin=" + Ok(okJump));
        }

        // Hervorhebung von Aenderungen anderer PCs: welche Bloecke, eigene ausgenommen, Ablauf nach der Dauer, Statustext, Markierung im Plan
        void SelfTestG3(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows();
            DateTime mo = D(2026, 11, 2), t0 = new DateTime(2026, 11, 2, 8, 0, 0);
            User mine = new User(); mine.Login = "mmuster"; mine.Kuerzel = "MUMA";
            User other = new User(); other.Login = "pwerk"; other.Kuerzel = "WEPA";
            Audit.Save(s, mo, rows[0], other, E("alt", false, ""));
            List<ChangedBlock> shown = new List<ChangedBlock>();
            for (int d = 0; d < 3; d++)
                for (int r = 0; r < 2; r++)
                {
                    Entry e = s.Get(mo.AddDays(d), rows[r].Id);
                    ChangedBlock b = new ChangedBlock(); b.Day = mo.AddDays(d); b.RowId = rows[r].Id; b.Version = e == null ? 0 : e.Version;
                    shown.Add(b);
                }
            Entry a = s.Get(mo, rows[0].Id); a.Content = "neu"; Audit.Save(s, mo, rows[0], other, a);   // anderer PC aendert
            Audit.Save(s, mo.AddDays(1), rows[0], mine, E("eigen", false, ""));                           // eigene Speicherung
            Audit.Save(s, mo.AddDays(2), rows[1], other, E("neu", false, ""));                            // neuer Block von anderem PC
            List<ChangedBlock> f = ChangeHighlight.Find(shown, s, "MMUSTER", t0);
            bool okFind = f.Count == 2 && f[0].Day == mo && f[0].RowId == rows[0].Id && f[0].Kuerzel == "WEPA" && f[1].Day == mo.AddDays(2) && f[1].RowId == rows[1].Id
                && !f.Exists(delegate(ChangedBlock x) { return x.Day == mo.AddDays(1); });
            List<ChangedBlock> list = new List<ChangedBlock>();
            ChangeHighlight.Update(list, f, t0);
            bool keep = !ChangeHighlight.Update(list, new List<ChangedBlock>(), t0.AddSeconds(ChangeHighlight.Seconds - 1)) && list.Count == 2;
            ChangedBlock again = new ChangedBlock(); again.Day = mo; again.RowId = rows[0].Id; again.Since = t0.AddSeconds(15);
            ChangeHighlight.Update(list, new List<ChangedBlock>(new[] { again }), t0.AddSeconds(15));                // erneut geaendert: Dauer beginnt neu
            bool restart = ChangeHighlight.Update(list, new List<ChangedBlock>(), t0.AddSeconds(ChangeHighlight.Seconds)) && list.Count == 1 && list[0].Day == mo;
            bool gone = ChangeHighlight.Update(list, new List<ChangedBlock>(), t0.AddSeconds(15 + ChangeHighlight.Seconds)) && list.Count == 0;
            List<ChangedBlock> four = new List<ChangedBlock>(f); four.AddRange(f);
            bool okSt = ChangeHighlight.Status(f, rows) == "Geändert: Mo " + rows[0].Name + " von WEPA, Mi " + rows[1].Name + " von WEPA"
                && ChangeHighlight.Status(four, rows) == "Geändert: 4 Blöcke" && ChangeHighlight.Status(new List<ChangedBlock>(), rows) == "";
            // Markierung im Plan: bleibt ueber Neuaufbau, nur der betroffene Block
            CellData c0 = view.Cells[0];
            ChangedBlock m = new ChangedBlock(); m.Day = c0.Day; m.RowId = rowInfo[c0.RowIndex].Id; m.Since = DateTime.Now;
            changed.Add(m);
            Rebuild(false);
            bool okMark = view.Cells[0].Changed && !view.Cells[1].Changed;
            changed.Clear(); MarkChanged();
            okMark &= !view.Cells[0].Changed;
            sb.AppendLine("Hervorhebung: geänderte Blöcke ohne eigene=" + Ok(okFind) + " bleibt " + ChangeHighlight.Seconds + " s=" + Ok(keep) + " erneut=Neustart=" + Ok(restart)
                + " abgelaufen=" + Ok(gone) + " Statustext=" + Ok(okSt) + " Markierung im Plan=" + Ok(okMark));
        }

        // 2-Wochen-Ansicht: Pruefleisten-Zustaende und Rundgangsleiste je KW auch in der zweiten Woche (auch nach Neuaufbau)
        void SelfTestG2(StringBuilder sb)
        {
            bool had = week2 != null;
            SetWeeks(2);
            int n1 = 0, n2 = 0;
            foreach (CellData c in view.Cells) if (c.States != null && c.States.Length > 0) n1++;
            foreach (CellData c in week2.View.Cells) if (c.States != null && c.States.Length > 0) n2++;
            int r2 = week2.View.Rounds.Count;
            bool okRounds = r2 > 0 && week2.View.Rounds[0].Monday == IsoWeek.Monday(start.AddDays(7));
            Rebuild(false);
            int n3 = 0;
            foreach (CellData c in week2.View.Cells) if (c.States != null && c.States.Length == week2.View.Defs[c.RowIndex].Count && c.States.Length > 0) n3++;
            bool okRe = n3 == n2 && week2.View.Rounds.Count == r2;
            if (!had) SetWeeks(1);
            sb.AppendLine("2-Wochen: Prüfleiste Zellen " + n2 + "/" + n1 + "=" + Ok(n2 > 0 && n2 == n1) + " Rundgänge je KW (" + r2 + ")=" + Ok(okRounds) + " nach Neuaufbau=" + Ok(okRe));
        }

        // Migration alter Bestaende: ohne Recht nichts, mit PackageAdmin genau einmal, Vorhandenes bleibt
        static void SelfTestG1(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows(); PufferSetup.Row(rows).Variants = ""; s.SaveStructure(rows);
            s.SaveDuties(DutyConfig.Default());                                   // alter Bestand: keine Puffer-Pflichtpunkte
            User leser = new User(); leser.Rights.Add(Perm.Read);
            User admin = new User(); admin.Rights.Add(Perm.Read); admin.Rights.Add(Perm.PackageAdmin);
            bool needed = PufferSetup.Needed(s.GetRows(), s.GetDuties());
            bool noRight = !PufferSetup.Apply(s, leser) && !PufferSetup.Apply(s, null) && s.GetDuties().Defs.Count == 0 && PufferSetup.Row(s.GetRows()).Variants == "";
            bool first = PufferSetup.Apply(s, admin);
            DutyConfig d1 = s.GetDuties();
            int pid = PufferSetup.Row(s.GetRows()).Id;
            bool okSet = first && d1.Defs.Count == 2 && d1.Defs.TrueForAll(delegate(CheckDef d) { return d.RowId == pid; })
                && PufferSetup.Row(s.GetRows()).Variants == "mobil|stationaer" && !PufferSetup.Needed(s.GetRows(), d1);
            bool again = !PufferSetup.Apply(s, admin) && s.GetDuties().Defs.Count == 2;
            // teilweise vorhanden: eigene Tagesprobe und eigene Varianten bleiben, nur die Wochen-WFI kommt dazu
            MemoryStore p = new MemoryStore();
            List<RowInfo> pr = p.GetRows(); PufferSetup.Row(pr).Variants = "mobil"; p.SaveStructure(pr);
            DutyConfig pc = DutyConfig.Default(); CheckDef own = new CheckDef(); own.Key = "PROBE"; own.Name = "Eigene Probe"; own.RowId = PufferSetup.Row(pr).Id; pc.Defs.Add(own); p.SaveDuties(pc);
            bool part = PufferSetup.Apply(p, admin);
            DutyConfig pd = p.GetDuties();
            bool okKeep = part && pd.Defs.Count == 2 && pd.Defs[0].Name == "Eigene Probe" && pd.Defs[0].Cond == "" && pd.Defs[1].Key == "WFIW" && PufferSetup.Row(p.GetRows()).Variants == "mobil";
            bool okNew = !PufferSetup.Needed(new MemoryStore().GetRows(), new MemoryStore().GetDuties());   // neue Bestaende haben die Standards schon
            sb.AppendLine("Puffer-Migration: nötig erkannt=" + Ok(needed) + " ohne Recht nichts=" + Ok(noRight) + " eingerichtet=" + Ok(okSet) + " idempotent=" + Ok(again)
                + " Vorhandenes bleibt=" + Ok(okKeep) + " neuer Bestand ohne Rückfrage=" + Ok(okNew));
        }

        static void SelfTestG0(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            int pid = PufferSetup.Row(s.GetRows()).Id;
            DutyConfig cfg = s.GetDuties();
            CheckDef probe = cfg.Defs.Find(delegate(CheckDef d) { return d.Key == "PROBE"; }), wfi = cfg.Defs.Find(delegate(CheckDef d) { return d.Key == "WFIW"; });
            if (probe == null || wfi == null) { sb.AppendLine("Puffer-Varianten: FEHLER Standard-Pflichtpunkte fehlen"); return; }
            DateTime w = D(2026, 6, 1), today = D(2026, 6, 10);   // KW23, Do 04.06. Fronleichnam; heute = Mi KW24
            s.Save(w, pid, E("Herstellung", false, "mobil"));
            s.Save(w.AddDays(1), pid, E("Herstellung", false, "mobil|stationaer"));
            s.Save(w.AddDays(2), pid, E("Herstellung", false, "stationaer|mobil", "PROBE"));   // andere Reihenfolge = gleiche Menge
            s.Save(w.AddDays(3), pid, E("Herstellung", false, "stationaer"));                  // Feiertag mit Herstellung
            s.Save(w.AddDays(6), pid, E("Herstellung", false, "mobil"));                       // So nur mobil
            s.Save(w.AddDays(13), pid, E("Herstellung", false, ""));                           // So KW24: Inhalt ohne Variante
            s.Save(w.AddDays(12), pid, E("Herstellung", false, "mobil|stationaer"));           // Sa KW24 (Zukunft)
            s.Save(D(2026, 5, 23), pid, E("Herstellung", false, "stationaer", "PROBE"));       // Sa KW21 gehakt
            object[][] cases = {
                new object[] { "Mo mobil = automatisch", w, DayState.Auto },
                new object[] { "Di mobil+stationär ohne Haken", w.AddDays(1), DayState.Missing },
                new object[] { "Mi mobil+stationär mit Haken", w.AddDays(2), DayState.Done },
                new object[] { "Do Feiertag stationär", w.AddDays(3), DayState.Missing },
                new object[] { "Fr ohne Angabe (Werktag)", w.AddDays(4), DayState.Missing },
                new object[] { "Sa ohne Herstellung", w.AddDays(5), DayState.NA },
                new object[] { "So nur mobil", w.AddDays(6), DayState.Auto },
                new object[] { "Sa Zukunft mobil+stationär", w.AddDays(12), DayState.Due },
                new object[] { "So Inhalt ohne Variante", w.AddDays(13), DayState.NA },
                new object[] { "Pfingstmontag ohne Herstellung", D(2026, 5, 25), DayState.NA },
                new object[] { "Sa stationär gehakt", D(2026, 5, 23), DayState.Done } };
            bool okDays = true;
            StringBuilder tab = new StringBuilder();
            foreach (object[] c in cases)
            {
                DayState got = CheckEval.Day(s, probe, (DateTime)c[1], today, cfg);
                bool ok = got == (DayState)c[2]; okDays &= ok;
                tab.AppendLine("  " + ((string)c[0]).PadRight(32) + ((DateTime)c[1]).ToString("dd.MM.") + " -> " + got + " (" + Ok(ok) + ")");
            }
            // Wochen-WFI: KW23 enthaelt stationaer (kombiniert) -> entfaellt; KW25 nur mobil, am So gehakt -> erledigt
            DateTime k25 = D(2026, 6, 15);
            s.Save(k25, pid, E("Herstellung", false, "mobil"));
            bool wfiOpen = CheckEval.Week(s, wfi, k25, today, cfg) == DayState.WeekOpen;
            s.Save(k25.AddDays(6), pid, E("", false, "", "WFIW"));
            bool okWfi = CheckEval.Week(s, wfi, w, today, cfg) == DayState.Done && CheckEval.Day(s, wfi, w.AddDays(5), today, cfg) == DayState.Auto
                && wfiOpen && CheckEval.Week(s, wfi, k25, today, cfg) == DayState.Done && CheckEval.Day(s, wfi, k25.AddDays(6), today, cfg) == DayState.Done
                && CheckEval.Day(s, wfi, k25, today, cfg) == DayState.NotNeeded;
            // Speicherformat: mehrere Varianten im selben Attribut, alter Einzelwert bleibt gueltig
            StoreData x = Backup.FromXml(Backup.ToXml(s.Snapshot()));
            Entry e1 = EntryIn(x, w.AddDays(1), pid), e3 = EntryIn(x, w.AddDays(3), pid);
            bool okXml = e1 != null && e1.Types == "mobil|stationaer" && e3 != null && e3.Types == "stationaer"
                && VariantSet.Of("stationaer").Count == 1 && VariantSet.Of("stationaer|mobil|mobil").Count == 2;
            // Merge: Vereinigung, Entfernen einer Seite bleibt erhalten
            List<string> cl = new List<string>();
            Entry m1 = Conflict.Merge(E("x", false, ""), E("x", false, "mobil"), E("x", false, "stationaer"), cl);
            Entry m2 = Conflict.Merge(E("x", false, "mobil"), E("x", false, ""), E("x", false, "mobil|stationaer"), cl);
            bool okMerge = m1.Types == "mobil|stationaer" && m2.Types == "stationaer" && cl.Count == 0
                && BufferSet.Target(BufferSet.FromVariant("mobil"), "stationaer", 0) == 1 && BufferSet.Target(BufferSet.FromVariant("mobil|stationaer"), "mobil", 0) == 0
                && VariantSet.Display("stationaer|mobil") == "mobil + stationaer";
            sb.AppendLine("Puffer-Varianten: Tagesprobe kombiniert/Wochenende/Feiertag=" + Ok(okDays) + " Wochen-WFI entfällt bei Kombination, So hakbar=" + Ok(okWfi)
                + " XML-Rundreise=" + Ok(okXml) + " Merge-Vereinigung=" + Ok(okMerge));
            sb.Append(tab.ToString());
        }
    }
}
#endif
