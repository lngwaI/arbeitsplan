#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E1a: Zeitmodell des Pakets (Hauptlauf, JeLauf, Ausfall, Kennung, Normalize, Texte, Paketformat 7, Golden-Test der Migration).
    partial class MainForm
    {
        void SelfTestE1(StringBuilder sb)
        {
            List<Package> seen = new List<Package>(Package.Seen);   // vor den eigenen Tests: nur Pakete der bisherigen Tests
            try { E1Golden(sb, seen); } catch (Exception ex) { sb.AppendLine("E1 Golden: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { E1Model(sb); } catch (Exception ex) { sb.AppendLine("E1 Modell: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { E1File(sb); } catch (Exception ex) { sb.AppendLine("E1 Datei: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ---------- Referenz: der alte Schedule() (nur Raster der Schritte, ohne Bezug/Ausfall) ----------

        static List<RunDate> E1Old(Package p, List<int> gaps, DayOfWeek d1)
        {
            int main = -1;
            for (int i = 0; i < p.Steps.Count; i++)
                if (p.Steps[i].Kind == StepKind.Run && !p.Steps[i].IsEmpty && (main < 0 || p.Steps[i].RunCountFor(d1) > p.Steps[main].RunCountFor(d1))) main = i;
            List<int> mainDay = new List<int>(), shift = new List<int>();
            if (main >= 0)
            {
                mainDay = p.Steps[main].Days(d1);
                for (int r = 0; r < mainDay.Count; r++)
                {
                    int plan = r == 0 ? 0 : mainDay[r] - mainDay[r - 1];
                    shift.Add(r == 0 ? 0 : shift[r - 1] + (gaps != null && r - 1 < gaps.Count ? gaps[r - 1] : plan) - plan);
                }
            }
            List<RunDate> res = new List<RunDate>();
            for (int i = 0; i < p.Steps.Count; i++)
            {
                PackageStep s = p.Steps[i];
                if (s.IsEmpty) continue;
                List<int> days = s.Days(d1);
                for (int r = 0; r < days.Count; r++)
                {
                    RunDate d = new RunDate(); d.Step = i; d.Run = r; d.Day = days[r];
                    d.Label = s.Label(p.Prefix, r); d.Row = s.Row;
                    for (int k = 0; k < mainDay.Count; k++) if (mainDay[k] <= d.Day) d.Group = k;
                    if (i == main) d.Group = r;
                    if (d.Group >= 0) d.Day += shift[d.Group];
                    res.Add(d);
                }
            }
            return res;
        }

        static string E1Str(List<RunDate> l)
        {
            List<string> r = new List<string>();
            foreach (RunDate d in l) r.Add(d.Step + "/" + d.Run + "/" + d.Day + "/" + d.Group + "/" + d.Label + "/" + d.Row);
            return string.Join(";", r.ToArray());
        }

        static string E1Of(List<RunDate> l, int step)
        {
            List<string> r = new List<string>();
            foreach (RunDate d in l) if (d.Step == step) r.Add(d.Run + ":" + d.Day);
            return string.Join(",", r.ToArray());
        }

        static PackageStep E1Step(Package p, string title, string row, int day, StepKind kind, int count, int interval)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Row = row; s.Day = day; s.Kind = kind; s.Count = count; s.Interval = interval;
            p.Steps.Add(s); return s;
        }

        // JeLauf-Schritte, deren eigenes Raster (Schatten aus der Migration) nicht mehr Hauptlauf + Versatz entspricht (Test hat veraendert)
        static bool E1Consistent(Package p)
        {
            PackageStep h = p.Head;
            foreach (PackageStep s in p.Steps)
            {
                if (s.Skip.Count > 0 || s.Move.Count > 0 || s.RunEvery > 1) return false;
                if (s.Bind != StepBind.JeLauf || h == null) continue;
                for (int w = 0; w < 7; w++)
                {
                    List<int> a = s.Days((DayOfWeek)w), b = h.Days((DayOfWeek)w);
                    if (a.Count != b.Count) return false;
                    for (int i = 0; i < a.Count; i++) if (a[i] != b[i] + s.RunOffset) return false;
                }
            }
            return true;
        }

        void E1Golden(StringBuilder sb, List<Package> seen)
        {
            List<Package> all = new List<Package>(), olds = new List<Package>();   // olds: Pakete vor der Migration (Kopie), all: danach
            Package sample = Package.SampleCampaign("golden");
            olds.Add(sample.Clone()); all.Add(sample);
            bool hasTp = false;
            foreach (string c in new[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\config\Testprodukt.apkg"), @"config\Testprodukt.apkg" })
                if (File.Exists(c)) { Package t = Package.Load(c); olds.Add(null); all.Add(t); hasTp = true; break; }
            foreach (Package s in seen) { olds.Add(null); all.Add(s); }
            // Handgemachte Pakete im alten Stil: Wochentage, Tagesliste, Versatz, Unterlauf, abweichende Anzahl, Back-Up
            Package h = new Package(); h.ProductCode = "1357";
            E1Step(h, "Packen", "Suite 1", 1, StepKind.Normal, 1, 1);
            PackageStep m = E1Step(h, "Lauf", "Suite 1", 3, StepKind.Run, 5, 3); m.Shift[2] = 1;
            PackageStep w2 = E1Step(h, "Puffer", "Puffer", 2, StepKind.Normal, 5, 3); w2.Shift[2] = 1; w2.Variant = "mobil";   // Tag VOR jedem Lauf: Gruppe waere der vorige Lauf -> bleibt frei
            PackageStep sp = E1Step(h, "Splitten", "Mix / Split", 4, StepKind.SubRun, 5, 3); sp.Shift[2] = 1; sp.SubNos = "4186,4187,4188,4189,4190";
            PackageStep ex = E1Step(h, "Probe", "Suite 2", 9, StepKind.Normal, 3, 5);   // anderes Raster -> bleibt frei
            PackageStep wd = E1Step(h, "Wochen", "Suite 1", 1, StepKind.Normal, 4, 1); wd.Repeat = RepeatMode.Weekdays; wd.WeekDays = 1 | 4 | 16;
            PackageStep dl = E1Step(h, "Liste", "Suite 1", 1, StepKind.Normal, 1, 1); dl.Repeat = RepeatMode.DayList; dl.DayList = "1, 4, 9";
            PackageStep after = E1Step(h, "Danach", "Suite 1", 4, StepKind.Normal, 5, 3); after.Shift[2] = 1;   // 1 Tag nach jedem Lauf, nummeriert
            PackageStep late = E1Step(h, "Nach", "Suite 1", 10, StepKind.Normal, 5, 3); late.Shift[2] = 1;   // Versatz +7: ueber den naechsten Hauptlauf hinaus -> frei
            olds.Add(h.Clone()); all.Add(h);
            Package wk = new Package();   // Hauptlauf an Wochentagen und ein Schritt mit denselben Wochentagen
            PackageStep wm = E1Step(wk, "Lauf", "Suite 1", 1, StepKind.Run, 6, 1); wm.Repeat = RepeatMode.Weekdays; wm.WeekDays = 1 | 4 | 16;
            PackageStep wp = E1Step(wk, "Mischen", "Suite 2", 1, StepKind.Run, 6, 1); wp.Repeat = RepeatMode.Weekdays; wp.WeekDays = 1 | 4 | 16;
            olds.Add(wk.Clone()); all.Add(wk);
            // Alte Dateien (Version 6) mit Kennungen: Folge +1, Luecke, nur erster Lauf
            string path = Path.Combine(Path.GetTempPath(), "selftest_e1_v6.apkg");
            File.WriteAllText(path, "<Paket name=\"Alt\" version=\"6\" produkt=\"1357\">"
                + "<Schritt titel=\"Lauf\" block=\"Suite 1\" tag=\"2\" anzahl=\"3\" abstand=\"4\" startnr=\"5\" art=\"lauf\" kennung=\"PA 5001, PA 5002, PA 5003\"/>"
                + "<Schritt titel=\"Mischen\" block=\"Suite 2\" tag=\"3\" anzahl=\"3\" abstand=\"4\" startnr=\"5\" art=\"lauf\" kennung=\"PA 7001, , PA 7005\"/>"
                + "<Schritt titel=\"Probe\" block=\"Suite 2\" tag=\"1\" anzahl=\"1\" abstand=\"1\" startnr=\"1\" kennung=\"PA 9001\"/></Paket>");
            Package v6 = Package.Load(path);
            PackageStep l6 = v6.Steps[0], m6 = v6.Steps[1], p6 = v6.Steps[2];
            bool okV6 = l6.IdentStart == "PA 5001" && l6.IdentOver.Count == 0 && l6.Ident == "" && l6.Compose(1, "") == "Lauf 6\nPA 5002" && l6.IdentList(3) == "PA 5001, PA 5002, PA 5003"
                && m6.Bind == StepBind.JeLauf && m6.RunOffset == 1 && m6.Compose(1, "") == "Mischen 6" && m6.Compose(2, "") == "Mischen 7\nPA 7005" && m6.IdentFor(1) == ""
                && p6.Bind == StepBind.Frei && p6.Compose(0, "") == "Probe\nPA 9001";
            olds.Add(null); all.Add(v6);

            // Kennungen und Text je Lauf wie vor der Migration (Beispiel-, Hand- und Wochentagspaket)
            bool okText = true;
            for (int k = 0; k < olds.Count; k++)
            {
                if (olds[k] == null) continue;
                Package mig = olds[k].Clone(); mig.Migrate();
                for (int i = 0; i < mig.Steps.Count; i++)
                    for (int r = 0; r < olds[k].Steps[i].RunCountFor(DayOfWeek.Monday); r++)
                        okText &= mig.Steps[i].Compose(r, mig.Prefix) == olds[k].Steps[i].Compose(r, olds[k].Prefix);
                all[k] = mig;   // ab hier: die migrierte Fassung gegen den alten Rechenweg pruefen
            }

            int checkedN = 0, skippedN = 0, je = 0, diff = 0;
            List<int>[] gapSets = { null, new List<int>(new[] { 5, 5, 7 }), new List<int>(new[] { 1, 9, 2, 4 }) };
            for (int k = 0; k < all.Count; k++)
            {
                Package p = all[k];
                if (!E1Consistent(p)) { skippedN++; continue; }
                checkedN++;
                foreach (PackageStep s in p.Steps) if (s.Bind == StepBind.JeLauf) je++;
                for (int w = 0; w < 7; w++)
                    foreach (List<int> g in gapSets)
                        if (E1Str(E1Old(p, g, (DayOfWeek)w)) != E1Str(p.Schedule(g, (DayOfWeek)w))) diff++;
            }
            Package mp = Package.SampleCampaign("m"); mp.Migrate();
            bool okSample = mp.Steps[0].Bind == StepBind.Frei && mp.Steps[1].Bind == StepBind.Frei && mp.Steps[2].Bind == StepBind.Frei && mp.Steps[3].Bind == StepBind.JeLauf && mp.Steps[3].RunOffset == 1
                && mp.Steps[4].Bind == StepBind.JeLauf && mp.Steps[4].RunOffset == 3 && mp.Steps[5].RunOffset == 4 && mp.Head == mp.Steps[2];
            Package mh = h.Clone(); mh.Migrate();
            bool okHand = mh.Steps[2].Bind == StepBind.Frei && mh.Steps[3].Bind == StepBind.JeLauf && mh.Steps[3].RunOffset == 1 && !mh.Steps[3].NumberRuns && mh.Steps[4].Bind == StepBind.Frei
                && mh.Steps[5].Bind == StepBind.Frei && mh.Steps[6].Bind == StepBind.Frei && mh.Steps[7].Bind == StepBind.JeLauf && mh.Steps[7].RunOffset == 1 && mh.Steps[7].NumberRuns && mh.Steps[8].Bind == StepBind.Frei;
            sb.AppendLine("E1 Golden: Schedule vor/nach Migration gleich=" + Ok(diff == 0) + " (" + checkedN + " Pakete geprüft, " + skippedN + " verändert übersprungen, " + je + " JeLauf-Schritte, Abweichungen " + diff + ", Testprodukt.apkg " + (hasTp ? "dabei" : "nicht gefunden") + ") Text/Kennung je Lauf gleich=" + Ok(okText)
                + " Beispielpaket=" + Ok(okSample) + " Handpaket=" + Ok(okHand) + " Datei v6=" + Ok(okV6));
        }
    }
}
#endif
