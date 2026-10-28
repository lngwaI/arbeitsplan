#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Paket-Editor 3: Wiederholmodi (alle x Tage / Wochentage / Tagesliste, bis Tag n), Verschieben (Zieltag aus x,
    // ganzer Schritt, einzelner Lauf, Reihenfolge), Puffer-Varianten mit Anzahl (Format, Merge, Pflichtpunkte, Chips +/-),
    // Ansatz Flasche/ohne Gebinde, alte Pakete; Editor-Ablauf in PackageForm.Test3.cs.
    partial class MainForm
    {
        static string L(List<int> l) { List<string> s = new List<string>(); foreach (int i in l) s.Add(i.ToString()); return string.Join(",", s.ToArray()); }

        static PackageStep P3(string title, string row, int day)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Row = row; s.Day = day; return s;
        }

        void SelfTestPaket3(StringBuilder sb)
        {
            try { SelfTestPaket3Run(sb); }
            catch (Exception ex) { sb.AppendLine("Paket3: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine(PackageForm.TestPaket3(store, equip, start));
        }

        void SelfTestPaket3Run(StringBuilder sb)
        {
            // 1 Wiederholmodi
            PackageStep iv = P3("Probe", "Suite 1", 2); iv.Count = 3; iv.Interval = 2;
            bool okIv = L(iv.Days()) == "2,4,6" && iv.Numbered;
            iv.Until = 7; okIv &= L(iv.Days()) == "2,4,6"; iv.Until = 5; okIv &= L(iv.Days()) == "2,4" && iv.RepeatText() == "alle 2 Tage bis Tag 5";
            iv.Until = 0;
            PackageStep wd = P3("Probe", "Suite 1", 1); wd.Repeat = RepeatMode.Weekdays; wd.WeekDays = 1 | 4 | 16; wd.Count = 5;   // Mo, Mi, Fr
            bool okWd = L(wd.Days()) == "1,3,5,8,10" && L(wd.Days(DayOfWeek.Wednesday)) == "1,3,6,8,10";
            wd.Until = 12; okWd &= L(wd.Days()) == "1,3,5,8,10,12" && wd.RepeatText() == "Mo, Mi, Fr bis Tag 12"; wd.Until = 0;
            PackageStep dl = P3("Probe", "Suite 1", 1); dl.Repeat = RepeatMode.DayList; dl.DayList = "9, 1, 4, 4";
            bool okDl = L(dl.Days()) == "1,4,9" && dl.Compose(2, "") == "Probe 3" && dl.RepeatText() == "Tage 1, 4, 9";
            // Kampagne: Schedule mit Starttag Mittwoch, Uebernahme der Tagesliste in den Plan
            Package pk = new Package(); pk.Steps.Add(wd); pk.Day1 = DayOfWeek.Wednesday;
            List<RunDate> sch = pk.Schedule();
            bool okSch = sch.Count == 5 && sch[2].Day == 6 && sch[4].Day == 10;
            MemoryStore ms = new MemoryStore();
            int s1 = ms.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id;
            DateTime d0 = new DateTime(2031, 3, 3);   // Montag
            Package pa = new Package(); pa.Steps.Add(dl); pa.Apply(ms, d0, ApplyMode.Overwrite, null);
            bool okAp = ms.Get(d0, s1) != null && ms.Get(d0.AddDays(3), s1) != null && ms.Get(d0.AddDays(8), s1).Content == "Probe 3" && ms.Get(d0.AddDays(1), s1) == null;
            string path = Path.Combine(Path.GetTempPath(), "selftest_paket3.apkg");
            Package rt = new Package(); rt.Name = "Rundreise"; rt.Steps.Add(iv.Clone()); rt.Steps.Add(wd.Clone()); rt.Steps.Add(dl.Clone());
            rt.Steps[0].Until = 9; rt.Steps[1].Shift[2] = 1; rt.Save(path);
            Package rl = Package.Load(path);
            bool okFile = rl.Steps.Count == 3 && L(rl.Steps[0].Days()) == "2,4,6,8" && rl.Steps[1].Repeat == RepeatMode.Weekdays && rl.Steps[1].WeekDays == 21
                && L(rl.Steps[1].Days()) == "1,3,6,8,10" && rl.Steps[2].Repeat == RepeatMode.DayList && L(rl.Steps[2].Days()) == "1,4,9";
            sb.AppendLine("Paket3 Wiederholung: alle x Tage/bis Tag " + Ok(okIv) + " Wochentage " + Ok(okWd) + " Tagesliste " + Ok(okDl)
                + " Kampagnenstart Mi " + Ok(okSch) + " Uebernahme " + Ok(okAp) + " Datei " + Ok(okFile));

            // 2 Verschieben: Zieltag aus x (Spalten 118/24/118), ganzer Schritt, einzelner Lauf, Reihenfolge
            int[] w = { 118, 24, 118 };
            bool okX = PlanStrip.DayAt(w, -5) == 1 && PlanStrip.DayAt(w, 0) == 1 && PlanStrip.DayAt(w, 117) == 1 && PlanStrip.DayAt(w, 118) == 2
                && PlanStrip.DayAt(w, 141) == 2 && PlanStrip.DayAt(w, 142) == 3 && PlanStrip.DayAt(w, 5000) == 3
                && PackageForm.TargetOf(4, 1) == 3 && PackageForm.TargetOf(0, 2) == 0 && PackageForm.TargetOf(2, 2) == 2;
            PackageStep m = iv.Clone();
            bool okAll = m.MoveAll(3) && L(m.Days()) == "5,7,9" && !m.MoveAll(-5) && L(m.Days()) == "5,7,9";
            PackageStep mw = wd.Clone(); okAll &= mw.MoveAll(1) && L(mw.Days()) == "2,4,6,9,11" && mw.WeekDays == (2 | 8 | 32);   // Mo/Mi/Fr -> Di/Do/Sa
            PackageStep md = dl.Clone(); okAll &= md.MoveAll(2) && md.DayList == "3, 6, 11";
            bool okRun = m.MoveRun(1, 1) && L(m.Days()) == "5,8,9" && m.Shifted(1) && m.Clone().Shifted(1) && m.MoveRun(1, -1) && !m.Shifted(1) && L(m.Days()) == "5,7,9";
            PackageStep one = P3("Einmal", "Suite 1", 4); okRun &= one.MoveRun(0, 2) && one.Day == 6 && one.Shift.Count == 0;
            // Reihenfolge: A(1) B(1) C(2) D(3); mehrfach hin und her, Tag wird nur beim Ueberschreiten der Tagesgrenze uebernommen
            PackageStep a = P3("A", "", 1), b = P3("B", "", 1), c = P3("C", "", 2), dd = P3("D", "", 3);
            List<PackageStep> st = new List<PackageStep>(new[] { c, a, dd, b });
            bool okOrd = Names(st) == "ABCD";
            okOrd &= StepOrder.MoveTo(st, a, 1) && Names(st) == "BACD" && a.Day == 1;          // gleicher Tag: nur Reihenfolge
            okOrd &= StepOrder.MoveTo(st, a, 2) && Names(st) == "BCAD" && a.Day == 2;          // Tag des Nachbarn
            okOrd &= StepOrder.MoveTo(st, a, 3) && Names(st) == "BCDA" && a.Day == 3;          // bis ans Ende
            okOrd &= !StepOrder.MoveTo(st, a, 9) && StepOrder.MoveTo(st, a, 0) && Names(st) == "ABCD" && a.Day == 1;
            okOrd &= StepOrder.MoveTo(st, dd, 0) && Names(st) == "DABC" && dd.Day == 1 && StepOrder.MoveTo(st, dd, 1) && Names(st) == "ADBC";
            sb.AppendLine("Paket3 Verschieben: Zieltag aus x " + Ok(okX) + " ganzer Schritt " + Ok(okAll) + " einzelner Lauf " + Ok(okRun) + " Reihenfolge " + Ok(okOrd));

            // 3 Varianten mit Anzahl: Format (alt bleibt gueltig), Chips-Logik, Merge (beide geaendert = Maximum), Summe, Pflichtpunkte
            bool okFmt = VariantSet.Count("mobil:2|stationaer", "mobil") == 2 && VariantSet.Count("mobil|stationaer", "stationaer") == 1
                && VariantSet.With("mobil|stationaer", "mobil", 1) == "mobil|stationaer" && VariantSet.With("stationaer", "mobil", 2) == "mobil:2|stationaer"
                && VariantSet.Display("stationaer|mobil:2") == "2× mobil + stationaer" && VariantSet.Count("mobil:x", "mobil") == 1 && VariantSet.With("", "mobil", 12) == "mobil:9";
            bool okStep = VariantSet.Step("", "mobil", 0) == "mobil" && VariantSet.Step("mobil", "mobil", 1) == "mobil:2" && VariantSet.Step("mobil:2", "mobil", -1) == "mobil"
                && VariantSet.Step("mobil", "mobil", -1) == "" && VariantSet.Step("mobil:9", "mobil", 1) == "mobil:9" && VariantSet.Step("mobil:3", "mobil", 0) == ""
                && PlanView.ChipZone(3, 100, true) == -1 && PlanView.ChipZone(95, 100, true) == 1 && PlanView.ChipZone(50, 100, true) == 0 && PlanView.ChipZone(3, 100, false) == 0;
            bool okMerge = VariantSet.Merge("mobil", "mobil:2", "mobil|stationaer") == "mobil:2|stationaer" && VariantSet.Merge("mobil", "mobil:2", "mobil:3") == "mobil:3"
                && VariantSet.Merge("mobil:2", "", "mobil:2|stationaer") == "stationaer" && VariantSet.Merge("", "mobil", "stationaer") == "mobil|stationaer"
                && VariantSet.Sum("mobil", "mobil|stationaer") == "mobil:2|stationaer";
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            CheckDef probe = ms.GetDuties().Defs.Find(delegate(CheckDef x) { return x.RowId == puf && x.Key == "PROBE"; });
            DateTime tu = d0.AddDays(1);
            Entry e2 = new Entry(); e2.Content = "Puffer"; e2.Variant = "mobil:2"; ms.Save(tu, puf, e2);
            bool auto2 = probe != null && CheckEval.Day(ms, probe, tu, tu, ms.GetDuties()) == DayState.Auto;
            Entry e3 = ms.Get(tu, puf).Clone(); e3.Variant = "mobil:2|stationaer:2"; ms.Save(tu, puf, e3);
            bool okRule = auto2 && CheckEval.Day(ms, probe, tu, tu, ms.GetDuties()) == DayState.Missing && ms.Get(tu, puf).Variant == "mobil:2|stationaer:2";
            Package pv = new Package(); PackageStep ps = P3("Puffer", "Puffer", 1); ps.Variant = "mobil:2|stationaer"; pv.Steps.Add(ps); pv.Steps.Add(ps.Clone());
            pv.Save(path);
            bool okPkg = Package.Load(path).Steps[0].Variant == "mobil:2|stationaer";
            pv.Apply(ms, d0.AddDays(14), ApplyMode.Overwrite, null);
            okPkg &= ms.Get(d0.AddDays(14), puf).Variant == "mobil:4|stationaer:2";   // zwei Puffer-Schritte am selben Tag: Summe
            sb.AppendLine("Paket3 Varianten: Format " + Ok(okFmt) + " Chips-Logik " + Ok(okStep) + " Merge/Summe " + Ok(okMerge) + " Pflichtpunkte " + Ok(okRule) + " Paket " + Ok(okPkg));
            SelfTestChips(sb);

            // 4 Ansatz: Tank fehlt = Hinweis; Flasche/ohne Gebinde: kein Tank-Hinweis, Plantext mit Menge
            Catalog cat = Catalog.Default();
            PackageStep pt = P3("Puffer", "Puffer", 1); pt.Source = "Puffer";
            EquipAssign wfi = new EquipAssign(); wfi.Short = "WFI"; pt.Equipment.Add(wfi);
            bool tankHint = pt.MissingTanks(cat).Count == 1 && pt.MissingTanks(cat)[0] == "WFI";
            pt.Ansatz = "gebinde"; pt.Gebinde = "Flasche"; pt.Menge = "5"; pt.Einheit = "L";
            bool okGeb = pt.MissingTanks(cat).Count == 0 && pt.Compose(0, "") == "Puffer\nGebinde: Flasche 5 L";
            PackageStep po = pt.Clone(); po.Ansatz = "ohne"; po.Menge = "2"; po.Einheit = "kg";
            okGeb &= po.MissingTanks(cat).Count == 0 && po.AnsatzLine() == "Ohne Gebinde: 2 kg";
            PackageStep other = P3("Lauf", "Suite 1", 1); EquipAssign ptk = new EquipAssign(); ptk.Short = "PT"; other.Equipment.Add(ptk);
            bool noNoise = other.MissingTanks(cat).Count == 0 && !other.UsesAnsatz;   // Tank-Hinweis nur bei Puffer-Schritten
            Package pg = new Package(); pg.Steps.Add(pt); pg.Steps.Add(po); pg.Save(path);
            Package pgl = Package.Load(path);
            okGeb &= pgl.Steps[0].Ansatz == "gebinde" && pgl.Steps[0].AnsatzLine() == "Gebinde: Flasche 5 L" && pgl.Steps[1].AnsatzLine() == "Ohne Gebinde: 2 kg";
            sb.AppendLine("Paket3 Ansatz: Tank-Hinweis " + Ok(tankHint) + " Flasche/ohne Gebinde " + Ok(okGeb) + " nur Puffer " + Ok(noNoise));

            // 5 Alte Pakete (Version 4, ohne neue Attribute): alle x Tage, Tank, Variante je 1
            File.WriteAllText(path, "<Paket name=\"Alt\" version=\"4\"><Schritt titel=\"Puffer\" quelle=\"Puffer\" block=\"Puffer\" tag=\"2\" anzahl=\"3\" abstand=\"2\" startnr=\"1\" text=\"\" notiz=\"\" variante=\"mobil|stationaer\"/></Paket>");
            PackageStep os = Package.Load(path).Steps[0];
            bool okOld = os.Repeat == RepeatMode.Interval && L(os.Days()) == "2,4,6" && os.Until == 0 && os.Shift.Count == 0 && os.Ansatz == ""
                && VariantSet.Count(os.Variant, "mobil") == 1 && VariantSet.Count(os.Variant, "stationaer") == 1 && os.Compose(1, "") == "Puffer 2";
            sb.AppendLine("Paket3 alte Pakete: lesbar " + Ok(okOld));
        }

        static string Names(List<PackageStep> l) { string s = ""; foreach (PackageStep x in StepOrder.Sorted(l)) s += x.Title; return s; }

        // Chips in der Wochenansicht: Klick = an, "+" = 2x, 3x, "−" = 2x, anderer Chip dazu, Klick auf den Namen = weg (speichert sofort)
        void SelfTestChips(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Paket3 Chips: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            try
            {
                int pr = RowOf("Puffer");
                if (pr < 0) { sb.AppendLine("Paket3 Chips: FEHLER Zeile Puffer fehlt"); return; }
                int pid = rowInfo[pr].Id;
                DateTime wk = D(2024, 9, 16);   // Montag ohne Testdaten anderer Selbsttests
                start = wk; Rebuild(true); Expand(pr);
                CellData pc = CellOf(pr, 1);
                List<string> seen = new List<string>();
                ChipClick(pc, "mobil", 0); seen.Add(V(wk, pid));
                ChipClick(pc, "mobil", 1); seen.Add(V(wk, pid));
                ChipClick(pc, "mobil", 1); seen.Add(V(wk, pid));
                ChipClick(pc, "mobil", -1); seen.Add(V(wk, pid));
                ChipClick(pc, "stationaer", 0); seen.Add(V(wk, pid));
                ChipClick(pc, "mobil", 0); seen.Add(V(wk, pid));
                string got = string.Join(" ", seen.ToArray());
                bool ok = got == "mobil mobil:2 mobil:3 mobil:2 mobil:2|stationaer stationaer";
                sb.AppendLine("Paket3 Chips: Anzahl per +/- " + Ok(ok) + " (" + got + ")");
            }
            catch (Exception ex) { sb.AppendLine("Paket3 Chips: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally { start = keep; Rebuild(true); }
        }

        void ChipClick(CellData c, string v, int zone) { Point p = view.SlotPoint(c, v, -1, zone); if (!p.IsEmpty) view.TestClick(p); }

        string V(DateTime wk, int pid) { Entry e = store.Get(wk.AddDays(1), pid); return e != null ? e.Variant : "(leer)"; }
    }
}
#endif
