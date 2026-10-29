#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E1a (Teil 2): JeLauf, Ausfall, Kennung, Normalize, Texte, ManualNo; Paketformat 7 (Rundweg, aufgeloeste Attribute)
    partial class MainForm
    {
        static string E1Dates(Package p, int step, DateTime tag1)
        {
            List<string> r = new List<string>();
            foreach (RunDate d in p.Schedule()) if (d.Step == step) r.Add(tag1.AddDays(d.Day - 1).ToString("dd.MM"));
            return string.Join(",", r.ToArray());
        }

        void E1Model(StringBuilder sb)
        {
            // JeLauf mit Versatz -1 (Puffer einen Tag vor jedem Lauf); Hauptlauf an Tag 1 -> Normalize schiebt alles um 1, Rhythmus aus frueheren Kampagnen
            Package p = new Package(); p.ProductCode = "1357"; p.RunPrefix = "1357";
            PackageStep head = E1Step(p, "Lauf", "Suite 1", 1, StepKind.Run, 3, 4); head.StartNo = 30;
            PackageStep buf = E1Step(p, "Puffer", "Puffer", 1, StepKind.Normal, 1, 1); buf.Variant = "mobil"; buf.Bind = StepBind.JeLauf; buf.RunOffset = -1;
            string before = E1Of(p.Schedule(), 1);
            bool changed = p.Normalize(), again = p.Normalize();
            List<RunDate> sch = p.Schedule(), rh = p.Schedule(new List<int>(new[] { 9, 2 }));
            RunDate b2 = sch.Find(delegate(RunDate d) { return d.Step == 1 && d.Run == 2; });
            bool okJe = before == "0:0,1:4,2:8" && changed && !again && E1Of(sch, 0) == "0:2,1:6,2:10" && E1Of(sch, 1) == "0:1,1:5,2:9" && p.Day1 == DayOfWeek.Sunday && p.Lauf1Day == 2
                && b2.Group == 2 && b2.Label == "Puffer" && head.Compose(0, p.Prefix) == "Lauf 13570030" && E1Of(rh, 0) == "0:2,1:11,2:13" && E1Of(rh, 1) == "0:1,1:10,2:12";
            Package gp = new Package();   // Luecke vorn schliessen
            PackageStep gk = E1Step(gp, "Packen", "Suite 1", 3, StepKind.Normal, 1, 1); PackageStep gh = E1Step(gp, "Lauf", "Suite 1", 5, StepKind.Run, 2, 4);
            bool okGap = gp.Normalize() && gk.Day == 1 && gh.Day == 3 && gp.Day1 == DayOfWeek.Wednesday && gp.Lauf1Day == 3 && !gp.Normalize();

            // Ausfall: Luecke bei Laufnummer UND Kennung, JeLauf-Schritte des Laufs entfallen; manuelle Kennung bleibt moeglich
            Package a = new Package(); a.ProductCode = "1357"; a.RunPrefix = "1357";
            PackageStep ah = E1Step(a, "Lauf", "Suite 1", 1, StepKind.Run, 4, 2); ah.StartNo = 30; ah.IdentStart = "PA 40010001"; ah.SetSkip(2, true);
            PackageStep am = E1Step(a, "Mischen", "Suite 2", 1, StepKind.Run, 1, 1); am.StartNo = 30; am.Bind = StepBind.JeLauf; am.RunOffset = 1;
            List<RunDate> sa = a.Schedule();
            bool okSkip = E1Of(sa, 0) == "0:1,1:3,3:7" && E1Of(sa, 1) == "0:2,1:4,3:8" && sa.Find(delegate(RunDate d) { return d.Step == 0 && d.Run == 3; }).Label == "Lauf 13570033"
                && a.RunsText(ah) == "Lauf 1, 2, 4 · 3 Termine" && a.RunsText(am) == "Lauf 1, 2, 4 · 3 Termine" && a.RunCount == 4
                && ah.IdentFor(3) == "PA 40010004" && ah.EntryFor(3, a.Prefix).Content == "Lauf 13570033\nPA 40010004";
            MemoryStore ms = new MemoryStore(); DateTime start = D(2030, 3, 4);
            a.Apply(ms, start, ApplyMode.FillEmpty, null);
            List<RowInfo> rows = ms.GetRows();
            int r1 = rows.Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id, r2 = rows.Find(delegate(RowInfo r) { return r.Name == "Suite 2"; }).Id;
            bool gapNo = false;
            for (int d = 0; d < 12; d++)
                foreach (int rid in new[] { r1, r2 })
                {
                    Entry e = ms.Get(start.AddDays(d), rid);
                    if (e != null && (e.Content.IndexOf("13570032") >= 0 || e.Content.IndexOf("PA 40010003") >= 0)) gapNo = true;
                }
            Entry e7 = ms.Get(start.AddDays(6), r1), e5 = ms.Get(start.AddDays(4), r1), m8 = ms.Get(start.AddDays(7), r2), m6 = ms.Get(start.AddDays(5), r2);
            bool okApply = !gapNo && e7 != null && e7.Content == "Lauf 13570033\nPA 40010004" && (e5 == null || e5.IsEmpty) && m8 != null && m8.Content == "Mischen 13570033" && (m6 == null || m6.IsEmpty);
            ah.IdentOver[1] = "PA 40010099"; ah.IdentOver[3] = PackageStep.IdentOff;
            bool okOver = ah.IdentFor(1) == "PA 40010099" && ah.IdentFor(2) == "PA 40010003" && ah.IdentFor(3) == "" && ah.EntryFor(1, a.Prefix).Content == "Lauf 13570031\nPA 40010099"
                && ah.EntryFor(3, a.Prefix).Content == "Lauf 13570033" && ah.IdentList(4) == "PA 40010001, PA 40010099, PA 40010003";

            // jeder 2. Lauf (zaehlt nur stattfindende), Laufanzahl setzen
            Package ev = new Package();
            PackageStep eh = E1Step(ev, "Lauf", "Suite 1", 1, StepKind.Run, 5, 3);
            PackageStep eb = E1Step(ev, "Puffer", "Puffer", 1, StepKind.Normal, 1, 1); eb.Variant = "mobil"; eb.Bind = StepBind.JeLauf; eb.RunEvery = 2;
            bool okEvery = E1Of(ev.Schedule(), 1) == "0:1,2:7,4:13" && ev.RunsText(eb) == "Lauf 1, 3, 5 · 3 Termine";
            eh.SetSkip(1, true);
            okEvery &= E1Of(ev.Schedule(), 1) == "0:1,3:10" && ev.RunsText(eb) == "Lauf 1, 4 · 2 Termine";
            eh.Until = 12; ev.RunCount = 3;
            PackageStep dl = new PackageStep(); dl.Repeat = RepeatMode.DayList; dl.DayList = "1, 4"; dl.RunCount = 4;
            okEvery &= eh.Until == 0 && ev.RunCount == 3 && eh.Days().Count == 3 && dl.RunCount == 4 && dl.DayList == "1, 4, 7, 10" && ev.Type == PackageType.Kampagne && new Package().Type == PackageType.Ereignis;

            // Texte relativ zu Lauf 1
            Package t = new Package(); t.Lauf1 = new DateTime(2027, 3, 15);
            PackageStep tp = E1Step(t, "Packen", "Suite 1", 1, StepKind.Normal, 1, 1), th = E1Step(t, "Lauf", "Suite 1", 4, StepKind.Run, 10, 2);
            bool okRel = t.Lauf1Day == 4 && t.RelText(1) == "Fr 12.03.2027 · 3 Tage vor Lauf 1" && t.RelText(3) == "So 14.03.2027 · 1 Tag vor Lauf 1" && t.RelText(4) == "Mo 15.03.2027 · Lauf 1"
                && t.RelText(5) == "Di 16.03.2027 · 1 Tag nach Lauf 1" && t.RelText(6) == "Mi 17.03.2027 · Lauf 2" && t.RelText(1, new DateTime(2027, 3, 12)) == t.RelText(1)
                && t.DateRangeText(tp) == "Fr 12.03.2027" && t.DateRangeText(th) == "Mo 15.03.–Fr 02.04.2027" && t.RunsText(th) == "Lauf 1–10 · 10 Termine" && t.RunsText(tp) == "1 Termin";
            th.SetSkip(2, true);
            okRel &= t.RunsText(th) == "Lauf 1, 2, 4–10 · 9 Termine";
            t.Lauf1 = null;
            okRel &= t.RelText(1) == "3 Tage vor Lauf 1" && t.RelText(4) == "Lauf 1" && t.DateRangeText(tp) == "3 Tage vor Lauf 1" && t.DateRangeText(th) == "Lauf 1 – Lauf 10";
            Package yr = new Package(); yr.Lauf1 = new DateTime(2026, 12, 30); E1Step(yr, "Lauf", "Suite 1", 1, StepKind.Run, 3, 3);
            okRel &= yr.DateRangeText(yr.Steps[0]) == "Mi 30.12.2026–Di 05.01.2027";

            // Normalize mit Wochentagsmuster vor Lauf 1: Mo/Mi/Fr, 2 Tage vor -> Sa/Mo/Mi, Datum bleibt, Tag 1 wandert; zurueck schliesst die Luecke
            Package n = new Package(); n.Lauf1 = new DateTime(2027, 3, 10);
            PackageStep nh = E1Step(n, "Lauf", "Suite 1", 3, StepKind.Run, 2, 7), nw = E1Step(n, "Wochen", "Puffer", 1, StepKind.Normal, 3, 1);
            nw.Repeat = RepeatMode.Weekdays; nw.WeekDays = 1 | 4 | 16;
            bool okN = E1Dates(n, 1, D(2027, 3, 8)) == "08.03,10.03,12.03" && E1Dates(n, 0, D(2027, 3, 8)) == "10.03,17.03";
            okN &= n.MoveStep(nw, -2);
            DateTime t1 = n.StartFor(n.Lauf1.Value);
            okN &= t1 == D(2027, 3, 6) && n.Day1 == DayOfWeek.Saturday && nw.WeekDays == (32 | 1 | 4) && n.Lauf1Day == 5 && E1Dates(n, 1, t1) == "06.03,08.03,10.03" && E1Dates(n, 0, t1) == "10.03,17.03";
            okN &= n.MoveStep(nw, 2);
            t1 = n.StartFor(n.Lauf1.Value);
            okN &= t1 == D(2027, 3, 8) && n.Day1 == DayOfWeek.Monday && nw.WeekDays == (1 | 4 | 16) && E1Dates(n, 1, t1) == "08.03,10.03,12.03" && E1Dates(n, 0, t1) == "10.03,17.03" && !n.Normalize();

            // Nummer von Hand: Klammerformat zaehlt nicht als Kampagnenlauf
            PackageStep mn = new PackageStep(); mn.Title = "Packen"; mn.ManualNo = "12340001";
            string line = mn.Compose(0, "1234");
            MemoryStore m2 = new MemoryStore(); int sid = m2.GetRows()[0].Id;
            Entry en = new Entry(); en.Content = line + "\n\nLauf 12340002"; m2.Save(D(2030, 6, 3), sid, en);
            List<KeyValuePair<int, DateTime>> past = Package.PastRuns(m2, "1234");
            int lastNo; DateTime lastDay; int iv;
            bool okNo = line == "Packen (Nr. 12340001)" && !mn.IsEmpty && RunNumbers.Find(line).Count == 0 && past.Count == 1 && past[0].Key == 2
                && Package.LastRun(m2, "1234", out lastNo, out lastDay, out iv) && lastNo == 2 && PackageStep.IsManualNo("12340001") && !PackageStep.IsManualNo("1234");
            Package cl = p.Clone(); cl.Steps[0].Title = "x"; cl.Steps[0].SetSkip(0, true);
            bool okClone = p.Steps[0].Title == "Lauf" && !p.Steps[0].Skips(0) && cl.Day1 == p.Day1;

            sb.AppendLine("E1 Zeitmodell: JeLauf Versatz -1 + Normalize + Rhythmus=" + Ok(okJe) + " Lücke vorn schließen=" + Ok(okGap) + " Ausfall (Lücke Nummer/Kennung, JeLauf entfällt)=" + Ok(okSkip) + " Übernahme=" + Ok(okApply)
                + " Kennung von Hand=" + Ok(okOver) + " jeder 2. Lauf/Anzahl=" + Ok(okEvery) + " RelText/Datumsbereich/Läufe=" + Ok(okRel) + " Wochentage vor Lauf 1=" + Ok(okN)
                + " Nr. von Hand (Klammer)=" + Ok(okNo) + " Kopie=" + Ok(okClone));
        }

        static string E1Fields(PackageStep s)
        {
            List<string> ov = new List<string>(); List<int> k = new List<int>(s.IdentOver.Keys); k.Sort();
            foreach (int i in k) ov.Add(i + "=" + s.IdentOver[i]);
            return string.Join("|", new[] { s.Title, s.Row, s.Kind.ToString(), s.StartNo.ToString(), s.Variant, s.Bind.ToString(), s.RunOffset.ToString(), s.RunEvery.ToString(), s.NumberRuns.ToString(), s.ManualNo,
                string.Join(",", s.Skip.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()), PackageStep.MapText(s.Move), s.ShiftText(), s.IdentStart, string.Join(";", ov.ToArray()), s.Ident });
        }

        void E1File(StringBuilder sb)
        {
            Package r = new Package(); r.Name = "Rund"; r.ProductCode = "AB1"; r.RunPrefix = "1234"; r.Lauf1 = new DateTime(2027, 3, 15); r.Type = PackageType.Kampagne;
            PackageStep rp = E1Step(r, "Packen", "Suite 1", 1, StepKind.Normal, 1, 1); rp.ManualNo = "12340001";
            PackageStep rh = E1Step(r, "Lauf", "Suite 1", 4, StepKind.Run, 5, 3); rh.StartNo = 7; rh.SetSkip(1, true); rh.Move[3] = 1; rh.Shift[4] = 1;
            rh.IdentStart = "PA 40010001"; rh.IdentOver[2] = "PA 40010099"; rh.IdentOver[3] = PackageStep.IdentOff;
            PackageStep rb = E1Step(r, "Puffer", "Puffer", 1, StepKind.Normal, 1, 1); rb.Variant = "mobil"; rb.Bind = StepBind.JeLauf; rb.RunOffset = -1; rb.RunEvery = 2;
            PackageStep rm = E1Step(r, "Mischen", "Suite 2", 1, StepKind.Run, 1, 1); rm.StartNo = 7; rm.Bind = StepBind.JeLauf; rm.RunOffset = 1;
            string path = Path.Combine(Path.GetTempPath(), "selftest_e1_v7.apkg");
            string sch0 = E1Str(r.Schedule());
            r.Save(path);
            string xml = File.ReadAllText(path);
            Package q = Package.Load(path);
            bool okRt = q.Type == PackageType.Kampagne && q.Lauf1 == r.Lauf1 && q.Steps.Count == r.Steps.Count && E1Str(q.Schedule()) == sch0 && xml.Contains("version=\"7\"")
                && E1Of(q.Schedule(), 1) == "0:4,2:10,3:14,4:18" && E1Of(q.Schedule(), 2) == "0:3,3:13" && E1Of(q.Schedule(), 3) == "0:5,2:11,3:15,4:19" && q.Head == q.Steps[1];
            for (int i = 0; i < r.Steps.Count; i++) okRt &= E1Fields(q.Steps[i]) == E1Fields(r.Steps[i]);
            foreach (RunDate d in r.Schedule()) okRt &= q.Steps[d.Step].Compose(d.Run, q.Prefix) == r.Steps[d.Step].Compose(d.Run, r.Prefix);
            okRt &= q.Steps[1].Compose(3, q.Prefix) == "Lauf 12340010" && q.Steps[1].Compose(2, q.Prefix) == "Lauf 12340009\nPA 40010099" && q.Steps[0].Compose(0, q.Prefix) == "Packen (Nr. 12340001)";

            // aeltere exe: nur die aufgeloesten Attribute lesen (ohne Ausfall); Tage und Kennungen muessen gleich sein
            Package r2 = r.Clone(); r2.Steps[1].SetSkip(1, false);
            r2.Save(path);
            XmlDocument doc = new XmlDocument(); doc.Load(path);
            List<RunDate> s2 = r2.Schedule();
            bool okOld = true; int si = 0;
            foreach (XmlElement e in doc.DocumentElement.ChildNodes)
            {
                if (e.Name != "Schritt") continue;
                PackageStep o = new PackageStep();
                o.Day = int.Parse(e.GetAttribute("tag")); o.Count = int.Parse(e.GetAttribute("anzahl")); o.Interval = int.Parse(e.GetAttribute("abstand"));
                string wh = e.GetAttribute("wiederholung"); o.Repeat = wh == "wochentage" ? RepeatMode.Weekdays : (wh == "tage" ? RepeatMode.DayList : RepeatMode.Interval);
                o.WeekDays = PackageStep.ParseWeekDays(e.GetAttribute("wochentage")); o.DayList = e.GetAttribute("tage");
                string bis = e.GetAttribute("bis"); o.Until = bis.Length > 0 ? int.Parse(bis) : 0; o.ParseShift(e.GetAttribute("versatz")); o.Ident = e.GetAttribute("kennung");
                List<string> mine = new List<string>(), theirs = new List<string>();
                foreach (int d in o.Days()) mine.Add(d.ToString());
                List<int> runs = new List<int>();
                foreach (RunDate d in s2) if (d.Step == si) { theirs.Add(d.Day.ToString()); runs.Add(d.Run); }
                okOld &= string.Join(",", mine.ToArray()) == string.Join(",", theirs.ToArray());
                if (si == 1) for (int k = 0; k < runs.Count; k++) okOld &= o.IdentFor(k) == r2.Steps[1].IdentFor(runs[k]);
                si++;
            }
            okOld &= si == 4 && doc.DocumentElement.GetAttribute("version") == "7";

            // Typ: ohne Angabe abgeleitet (keine typ-Zeile), Einzelpaket bleibt Einzelpaket
            Package ee = new Package(); ee.Name = "Einzel"; ee.Type = PackageType.Einzel; E1Step(ee, "Packen", "Suite 1", 1, StepKind.Normal, 1, 1).ManualNo = "12340002";
            ee.Save(path); string xe = File.ReadAllText(path); Package qe = Package.Load(path);
            Package ev = new Package(); ev.Name = "Ereignis"; E1Step(ev, "Flush", "Suite 1", 1, StepKind.Normal, 1, 1);
            ev.Save(path); bool noTyp = !File.ReadAllText(path).Contains("typ="); Package qv = Package.Load(path);
            bool okTyp = qe.Type == PackageType.Einzel && xe.Contains("typ=\"einzel\"") && qe.Steps[0].ManualNo == "12340002" && noTyp && qv.Type == PackageType.Ereignis && qv.Head == null;
            sb.AppendLine("E1 Paketformat 7: Rundweg=" + Ok(okRt) + " aufgelöste Attribute für ältere exe=" + Ok(okOld) + " Typ=" + Ok(okTyp));
        }
    }
}
#endif
