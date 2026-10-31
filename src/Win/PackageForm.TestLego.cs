#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests "Lego": Puffer-Muster (Parse/Fill), Rhythmus -> Tage, Speichern/Laden mit Einzelverschiebung bei JeLauf, Herabstufung der Regeln
    // (von Hand gesetzt = Hinweis, sonst Sperre; bleibende Sperren), Rhythmus-Fenster, grosse Vorschau (umhaengen und zurueck). Nur erfundene Daten.
    partial class PackageForm
    {
        static string LegoJoin(List<int> l) { return l == null ? "null" : string.Join(",", l.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()); }

        // Regelpruefung mit ueberschriebenem Tag eines Termins (wie der Kampagnenstart mit von Hand gesetzten Tagen)
        static List<PackageProblem> LegoCheck(Package q, int step, int run, int day)
        {
            List<RunDate> sch = q.Schedule();
            P1Day(sch, step, run).Day = day;
            return q.Check(null, default(DateTime), ApplyMode.Overwrite, sch);
        }

        static bool LegoSoft(List<PackageProblem> l, string code)
        {
            return P1Has(l, code, Severity.Hinweis) && !P1Has(l, code, Severity.Sperre) && l.Exists(delegate(PackageProblem x) { return x.Code == code && x.Text.Contains("von Hand gesetzt") && !x.Blocks; });
        }

        public static string TestLego(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder(); int seen = Package.Seen.Count;   // eigene Pakete nicht in den Golden-Test
            try { LegoCore(sb); } catch (Exception ex) { sb.AppendLine("Lego Kern: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]); }
            sb.AppendLine(LegoForm(store, equip, start));
            if (Package.Seen.Count > seen) Package.Seen.RemoveRange(seen, Package.Seen.Count - seen);
            return sb.ToString();
        }

        static void LegoCore(StringBuilder sb)
        {
            // (1) Muster: gueltig, ungueltig, mob + stat am selben Tag, Fill
            string e1, e2, e3, e4, e5;
            List<int> a = PufferPattern.Parse("mob, mob/stat, stat, frei", out e1), b = PufferPattern.Parse("M S/M f  STAT;stat/mob", out e2);
            bool okGood = LegoJoin(a) == "1,3,2,0" && e1.Length == 0 && LegoJoin(b) == "1,3,0,2,3" && LegoJoin(PufferPattern.Parse("mob/stat", out e3)) == "3";
            bool okBad = PufferPattern.Parse("mob, foo", out e3) == null && e3.Contains("foo") && PufferPattern.Parse("  ", out e4) == null && e4.Length > 0 && PufferPattern.Parse(null, out e5) == null && e5.Length > 0;
            List<int> mob = new List<int>(), stat = new List<int>();
            PufferPattern.Fill(a, 2, 9, mob, stat);
            bool okFill = LegoJoin(mob) == "2,3,6,7" && LegoJoin(stat) == "3,4,7,8";
            PufferPattern.Fill(a, 5, 4, mob, stat); okFill &= mob.Count == 0 && stat.Count == 0;
            sb.AppendLine("Lego Puffer-Muster: gültig " + T(okGood) + " ungültig " + T(okBad) + " mob/stat am selben Tag und Einfüllen " + T(okFill));

            // (2) Rhythmus -> Schedule-Tage; Regel "puffer" bei Tagesliste nur Hinweis, sonst Sperre; neuer Hinweis, wenn der Rhythmus den letzten Lauf nicht abdeckt
            Package p = Package.SampleBuffers("Lego"); PackageStep pu = p.Steps[p.Steps.Count - 1]; int pi = p.Steps.Count - 1;
            PufferPattern.Fill(a, 2, 21, mob, stat);
            pu.Bind = StepBind.Frei; pu.Repeat = RepeatMode.DayList; pu.DayList = PackageStep.JoinDays(mob); pu.Shift.Clear(); pu.Move.Clear(); pu.Skip.Clear();
            List<int> got = new List<int>(); foreach (RunDate d in p.Schedule()) if (d.Step == pi) got.Add(d.Day);
            bool okSched = LegoJoin(got) == LegoJoin(mob) && p.Head.RunCountFor(p.Day1) == 4;
            List<PackageProblem> ck = p.Check();
            bool okMore = P1Has(ck, "puffer", Severity.Hinweis) && !P1Has(ck, "puffer", Severity.Sperre) && !Package.HasBlock(ck) && !P1Any(ck, "pufrhythmus");
            pu.DayList = "2, 3"; ck = p.Check();
            okMore &= P1Has(ck, "pufrhythmus", Severity.Hinweis) && PackageProblem.RuleTitle("pufrhythmus").Length > 0 && !Package.HasBlock(ck) && !P1Any(ck, "puffer");
            pu.Repeat = RepeatMode.Interval; pu.Count = 5; pu.Interval = 1; pu.Day = 2;
            okMore &= P1Has(p.Check(), "puffer", Severity.Sperre);
            sb.AppendLine("Lego Rhythmus: Tage im Schedule " + T(okSched) + " Tagesliste nur Hinweis, Intervall gesperrt, Rhythmus endet früh " + T(okMore));

            // (3) Speichern/Laden unveraendert, auch Einzelverschiebung eines nicht-Puffer-JeLauf-Schritts
            Package q = Package.SampleBuffers("Lego2"); PackageStep misch = q.Steps[3], rp = q.Steps[q.Steps.Count - 1];
            rp.Repeat = RepeatMode.DayList; rp.DayList = "2, 3, 6, 7";
            bool okRound = q.DragRun(misch, 1, 1, true) && misch.Shift.Count == 1 && misch.Pinned(1) && !misch.Pinned(0);
            Package back = Package.FromXml(q.ToXml());
            okRound &= back.ToXml() == q.ToXml() && back.Steps[3].Shift.Count == 1 && back.Steps[3].Shift[1] == 1 && LegoSchedule(back) == LegoSchedule(q) && LegoSchedule(q.Clone()) == LegoSchedule(q);
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lego_test.apkg");
            try { q.Save(tmp); Package load = Package.Load(tmp); okRound &= load.ToXml() == q.ToXml() && load.Steps[3].Shift[1] == 1; }
            finally { try { System.IO.File.Delete(tmp); } catch (System.IO.IOException) { } }
            sb.AppendLine("Lego Speichern/Laden: Rundreise mit Einzelverschiebung " + T(okRound));

            // (4) Regeln: von Hand gesetzt = Hinweis, sonst Sperre; die uebrigen bleiben gesperrt
            Package g1 = Package.SampleCampaign("L"); string c0 = "gleichtag";
            bool okG = P1Has(LegoCheck(g1, 2, 1, 3), c0, Severity.Sperre);
            g1.Steps[2].Shift[1] = 1; okG &= LegoSoft(LegoCheck(g1, 2, 1, 3), c0);
            g1.Steps[2].Shift.Clear(); g1.Steps[2].Shift[0] = 0; okG &= LegoSoft(LegoCheck(g1, 2, 1, 3), c0);   // reicht einer der beiden Laeufe
            Package g2 = Package.SampleCampaign("L");
            bool okP = P1Has(LegoCheck(g2, 0, 0, 3), "packen-tag", Severity.Sperre);
            g2.Steps[0].Shift[0] = 1; okP &= LegoSoft(LegoCheck(g2, 0, 0, 3), "packen-tag");
            Package g3 = Package.SampleCampaign("L");
            bool okF = P1Has(LegoCheck(g3, 2, 2, 5), "lauffolge", Severity.Sperre);
            g3.Steps[2].Shift[2] = 1; okF &= LegoSoft(LegoCheck(g3, 2, 2, 5), "lauffolge");
            Package g4 = Package.SampleBuffers("L"); int g4i = g4.Steps.Count - 1;
            bool okV = P1Has(LegoCheck(g4, g4i, 0, 0), "pufvor", Severity.Sperre);
            g4.Steps[g4i].Shift[0] = 1; okV &= LegoSoft(LegoCheck(g4, g4i, 0, 0), "pufvor");
            IDataStore st = P1Store(); Package g5 = P1Camp(st, 2, 6); IDataStore mp = new MemoryStore();
            P1Put(mp, P1D0.AddDays(2), "Suite 1", "Lauf 12349999");   // Lauf 1 (Tag 3) liegt auf einem belegten Hauptlauf im Plan
            List<RunDate> s5 = g5.Schedule(null, P1D0.DayOfWeek); g5.Head.Shift[0] = 1;
            bool okKeep = P1Has(g5.Check(mp, P1D0, ApplyMode.FillEmpty, s5), "planlauf", Severity.Sperre);
            Package g6 = Package.SampleCampaign("L"); PackageStep sr = new PackageStep(); sr.Title = "Splitten"; sr.Row = "Suite 1"; sr.Kind = StepKind.SubRun; sr.Day = 4; sr.Count = 2; sr.Interval = 6; g6.Steps.Add(sr);
            g6.Head.Shift[0] = 1; sr.Shift[0] = 1;
            okKeep &= P1Has(g6.Check(), "unterlauf", Severity.Sperre);
            Package g7 = Ux1bSample("L", true); Ux1bPrepare(st);
            okKeep &= P1Has(g7.Check(), "pufdoppelt", Severity.Sperre);
            foreach (PackageStep x in g7.Steps) x.Shift[0] = 1;
            okKeep &= P1Has(g7.Check(), "pufdoppelt", Severity.Sperre);
            sb.AppendLine("Lego Regeln: gleichtag " + T(okG) + " packen-tag " + T(okP) + " lauffolge " + T(okF) + " pufvor " + T(okV) + " bleibende Sperren (planlauf, unterlauf, pufdoppelt) " + T(okKeep));
        }

        static string LegoSchedule(Package p)
        {
            StringBuilder s = new StringBuilder();
            foreach (RunDate d in p.Schedule()) s.Append(d.Step).Append(':').Append(d.Run).Append('=').Append(d.Day).Append(' ');
            return s.ToString();
        }

        static string LegoForm(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleBuffers("LegoForm"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            PufferRhythmForm rf = null, rf2 = null; string why = "";
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep misch = f.steps[3];

                // Einzelverschieben JeLauf (nicht-Puffer): nur dieser Lauf, Schloss, Zuruecksetzen ueber ResetRun; Ziehen mit Umschalt = alle
                int day1 = f.ItemOf(misch, 1).Day;
                f.MoveInCalendar(misch, 1, day1 + 1, true); Application.DoEvents();
                bool okOne = misch.Shift.Count == 1 && misch.Pinned(1) && f.ItemOf(misch, 1).Shifted && !f.ItemOf(misch, 0).Shifted && f.ItemOf(misch, 1).Day == day1 + 1 && f.stripMsg.Text.Length == 0 && f.dirty;
                f.ResetRun(misch, 1); Application.DoEvents();
                okOne &= misch.Shift.Count == 0 && f.ItemOf(misch, 1).Day == day1 && !f.ItemOf(misch, 1).Shifted;
                int off = misch.RunOffset;
                f.MoveInCalendar(misch, 1, day1 + 1, false); Application.DoEvents();
                okOne &= misch.RunOffset == off + 1 && misch.Shift.Count == 0;
                f.TestUndo(-1);
                okOne &= f.steps[3].RunOffset == off;   // Rueckgaengig baut die Schritte neu auf

                // Rhythmus-Fenster: Muster fuellt den ersten Puffer je Typ, fehlender Typ wird erst bei OK angelegt, Abbrechen aendert nichts
                int n0 = f.steps.Count; string xml0 = f.StepsView().ToXml();
                rf = f.BuildRhythm();
                PackageStep mob = f.steps[f.steps.Count - 1];   // nach Rueckgaengig neu gebaut
                bool okRh = W(ref why, 0, rf.Rows.Count == 1 && rf.Rows[0].Step == mob && rf.Rows[0].Mob);
                rf.TestPattern("mob, mob/stat, stat, frei"); rf.TestFrom(2); rf.TestFill();
                int last = 0; foreach (RunDate d in f.Plan()) if (d.Step == 2) last = Math.Max(last, d.Day);
                List<int> em = new List<int>(), es = new List<int>(); PufferPattern.Fill(new List<int>(new[] { 1, 3, 2, 0 }), 2, last, em, es);
                okRh &= W(ref why, 1, rf.Rows.Count == 2 && rf.Rows[1].IsNew && rf.Rows[1].Step == null && rf.Rows[0].Days.SetEquals(em) && rf.Rows[1].Days.SetEquals(es) && rf.TestMsg.Length == 0);
                okRh &= W(ref why, 2, f.steps.Count == n0 && f.StepsView().ToXml() == xml0); // vor OK nichts geaendert
                rf.TestToggle(1, es[0]); okRh &= W(ref why, 7, !rf.Rows[1].Days.Contains(es[0])); rf.TestToggle(1, es[0]); okRh &= W(ref why, 8, rf.Rows[1].Days.Contains(es[0]));
                f.ApplyRhythm(rf.Rows); Application.DoEvents();
                PackageStep ns = f.steps[f.steps.Count - 1];
                okRh &= W(ref why, 9, f.steps.Count == n0 + 1 && ns.Source == "Puffer stationär" && ns.Bind == StepBind.Frei && ns.Repeat == RepeatMode.DayList && ns.DayList == PackageStep.JoinDays(es)
                    && mob.Bind == StepBind.Frei && mob.Repeat == RepeatMode.DayList && mob.DayList == PackageStep.JoinDays(em) && mob.Shift.Count == 0 && mob.Move.Count == 0 && f.dirty);
                List<int> sched = new List<int>(); int ni = f.steps.IndexOf(ns); foreach (RunDate d in f.Plan()) if (d.Step == ni) sched.Add(d.Day);
                okRh &= W(ref why, 3, LegoJoin(sched) == LegoJoin(es));
                f.TestUndo(-1); okRh &= W(ref why, 10, f.steps.Count == n0 && f.StepsView().ToXml() == xml0);
                // falsches Muster: Meldung, Tabelle bleibt; geaenderter Puffer ohne Tag: kein OK
                rf2 = f.BuildRhythm(); rf2.TestPattern("mob, xx"); rf2.TestFill();
                okRh &= W(ref why, 4, rf2.TestMsg.Contains("xx") && rf2.Rows.Count == 1 && !rf2.Rows[0].Changed);
                foreach (int d0 in new List<int>(rf2.Rows[0].Days)) rf2.TestToggle(0, d0);
                rf2.TestCommit(); okRh &= W(ref why, 11, rf2.TestMsg.Contains("Mindestens ein Tag") && rf2.DialogResult == DialogResult.None);
                rf.Dispose(); rf2.Dispose(); rf = rf2 = null;

                // Grosse Vorschau: Kalender und Hinweise wandern ins Fenster und zurueck (Eltern, Reihenfolge, Dock), Strg+Z geht an den Editor
                Control home = f.TestStripParent; int si0 = f.TestStripIndex, hi0 = f.TestHintsIndex;
                bool okBig = W(ref why, 12, home != null && f.TestHintsParent == home && home.Controls.Count >= 3);
                f.MoveInCalendar(f.steps[3], 1, day1 + 1, true); Application.DoEvents();
                int u0 = f.UndoAt;
                Form big = f.TestBig(true);
                okBig &= W(ref why, 5, big != null && f.stripScroll.Parent == big && f.hintsCol.Parent == big && f.stripBar.Parent == f.stripScroll && f.bBig.Text == "Klein" && f.TestBig(true) == big);
                okBig &= W(ref why, 6, f.TestBigKey(Keys.Control | Keys.Z) && f.UndoAt == u0 - 1 && f.steps[3].Shift.Count == 0);
                f.TestBigKey(Keys.Control | Keys.Y); okBig &= W(ref why, 13, f.UndoAt == u0 && f.steps[3].Shift.Count == 1);
                big.Close(); Application.DoEvents();
                okBig &= W(ref why, 14, f.TestStripParent == home && f.TestHintsParent == home && f.TestStripIndex == si0 && f.TestHintsIndex == hi0 && f.stripScroll.Dock == DockStyle.Fill && f.hintsCol.Dock == DockStyle.Right
                    && f.bBig.Text == "Groß" && f.TestBig(false) == null && f.big == null);
                f.RestoreBig(); okBig &= W(ref why, 15, f.TestStripParent == home);   // zweimal zurueck ist harmlos

                return "Lego Editor: Einzelverschieben JeLauf " + T(okOne) + " Rhythmus-Fenster und Anwenden " + T(okRh) + " große Vorschau hängt um und zurück " + T(okBig) + (okRh && okBig ? "" : " [" + why + "]");
            }
            catch (Exception ex) { return "Lego Editor: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally
            {
                if (rf != null) rf.Dispose();
                if (rf2 != null) rf2.Dispose();
                f.TestBig(false); f.dirty = false; f.Close(); f.Dispose();
            }
        }

        // Testbilder: --form bigview (grosse Vorschau, nicht modal), --form rhythm / rhythm2 / rhythm3 (Rhythmus-Fenster mit Muster)
        public Form TestLegoForm(string which)
        {
            StartPosition = FormStartPosition.Manual; Location = new Point(-4000, 0); Show(); Application.DoEvents();   // Editor bleibt ausser Sicht
            if (which == "bigview") { Bind(steps[2]); return TestBig(true); }
            if (which == "rhythm2") pk.Lauf1 = pk.SyncDay1(P1D0);   // mit Datum im Spaltenkopf
            PufferRhythmForm rf = BuildRhythm();
            string[] pat = { "mob", "mob, mob/stat, stat", "stat" };
            rf.TestPattern(pat[which == "rhythm" ? 0 : which == "rhythm2" ? 1 : 2]); rf.TestFrom(2); rf.TestFill();
            return rf;
        }
    }
}
#endif
