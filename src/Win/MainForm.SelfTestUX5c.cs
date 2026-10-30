#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5c (Puffer-Rhythmus) im Modell: eigener Rhythmus (alle 3 Tage) unabhaengig von Laeufen (alle 2 Tage), Lauf je Termin
    // (LaufOf/LaufRef), "bis Lauf k", Zeitpunkt je Karte (Puffer 1 zwei Tage, Puffer 2 einen Tag vor dem Lauf), XML-Rundweg, Regeln
    // (Pufferlaeufe <= Produktlaeufe, "pufnach"), alter Bestand ohne Attribut. Editor: PackageForm.TestUx5c.cs.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        // Hauptlauf an den Tagen 3, 5, 7, 9 (alle 2 Tage, 4 Laeufe)
        static Package Ux5cBase(string name)
        {
            Package p = new Package(); p.Name = name;
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Day = 3; lf.StartNo = 1; lf.Count = 4; lf.Interval = 2;
            p.Steps.Add(lf);
            return p;
        }

        static string Ux5cDays(List<RunDate> plan, int step)
        {
            List<int> d = new List<int>(); foreach (RunDate r in plan) if (r.Step == step) d.Add(r.Day);
            d.Sort(); return string.Join(",", d.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray());
        }

        static string Ux5cLaufs(Package p, List<RunDate> plan, int step)
        {
            List<RunDate> l = plan.FindAll(delegate(RunDate r) { return r.Step == step; });
            l.Sort(delegate(RunDate a, RunDate b) { return a.Day.CompareTo(b.Day); });
            return string.Join(",", l.ConvertAll<string>(delegate(RunDate r) { return (p.LaufOf(plan, r) + 1).ToString(); }).ToArray());
        }

        static int Ux5cCount(Package p, string code) { return p.Check().FindAll(delegate(PackageProblem x) { return x.Code == code; }).Count; }

        void SelfTestUX5c(StringBuilder sb)
        {
           
            string ap = "";
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2036, 3, 3);

            // (1) eigener Rhythmus: Puffer alle 3 Tage (Tage 1, 4, 7, 10) unabhaengig von den Laeufen (3, 5, 7, 9); jeder Termin bedient den naechsten Lauf
            Package p1 = Ux5cBase("UX5c-Rhythmus");
            PackageStep pa = UxPuf("Pa", 0, false, 1, "AT-1"); pa.Repeat = RepeatMode.Interval; pa.Interval = 3; pa.Count = 4; p1.Steps.Add(pa);
            List<RunDate> plan = p1.Schedule();
            bool okRh = Ux5cDays(plan, 0) == "3,5,7,9" && Ux5cDays(plan, 1) == "1,4,7,10";
            bool okLauf = Ux5cLaufs(p1, plan, 1) == "1,2,3,0";   // Tag 10 liegt nach dem letzten Lauf: kein Lauf (0 = -1 + 1)
            bool okTxt = p1.RunsText(pa) == "4 Termine · für Lauf 1–3";
            try { p1.Apply(ms, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = "Ausnahme: " + ex.Message; }
            Func<int, Entry> E = delegate(int day) { return ms.Get(d0.AddDays(day - 1), puf); };
            okLauf &= Laufs(E(1)) == "Pa=1" && Laufs(E(4)) == "Pa=2" && Laufs(E(7)) == "Pa=3" && Laufs(E(10)) == "Pa=0";
            // Ende "bis Lauf 2" (Tag 5): Termine 1 und 4; Tagesliste und Wochentage verhalten sich wie bisher
            pa.Until = 5; List<RunDate> pl2 = p1.Schedule();
            okRh &= Ux5cDays(pl2, 1) == "1,4" && Ux5cLaufs(p1, pl2, 1) == "1,2";
            pa.Until = 0; pa.Repeat = RepeatMode.DayList; pa.DayList = "2, 6, 8"; pa.Count = 3;
            List<RunDate> pl3 = p1.Schedule();
            okRh &= Ux5cDays(pl3, 1) == "2,6,8" && Ux5cLaufs(p1, pl3, 1) == "1,3,4";   // naechster Lauf am oder nach dem Termin
            sb.AppendLine("UX5c Eigener Rhythmus: Termine unabhängig vom Lauf-Raster " + Ok(okRh) + " Lauf je Termin " + Ok(okLauf) + " Text \"für Lauf\" " + Ok(okTxt) + (ap.Length > 0 ? " " + ap : ""));

           
            // (2) Zeitpunkt je Karte: Schritt "zu jedem Lauf" einen Tag vor dem Lauf; Puffer 1 zwei Tage davor, Puffer 2 wie der Schritt
            Package p2 = Ux5cBase("UX5c-Karten");
            PackageStep ks = UxPuf("P1", -1, true, 1, "AT-1");
            PufferSpec sp2 = new PufferSpec(); sp2.Name = "P2"; sp2.Typ = "mobil"; sp2.Lines.Add(UxEq("Ansatztank", "AT-2")); ks.Puffers.Add(sp2);
            ks.SyncPuffers(); ks.Puffers[0].OffsetDays = -2; p2.Steps.Add(ks);
            List<RunDate> pk2 = p2.Schedule();
            bool okSch = Ux5cDays(pk2, 1) == "2,4,6,8" && ks.HasCardOffsets && ks.CardDeltas().Count == 2 && ks.CardDeltas()[0] == -1 && ks.CardDeltas()[1] == 0
                && ks.CardOffset(ks.Puffers[0]) == -2 && ks.CardOffset(ks.Puffers[1]) == -1 && p2.CardsSplit(ks, DayOfWeek.Monday);
            MemoryStore ms2 = new MemoryStore(); ap = "";
            try { p2.Apply(ms2, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = "Ausnahme: " + ex.Message; }
            Func<int, Entry> E2 = delegate(int day) { return ms2.Get(d0.AddDays(day - 1), puf); };
            // Laeufe an 3, 5, 7, 9: P1 an 1, 3, 5, 7; P2 an 2, 4, 6, 8; Lauf-Angabe je Karte = der eigene Lauf
            bool okPl = Laufs(E2(1)) == "P1=1" && Laufs(E2(2)) == "P2=1" && Laufs(E2(3)) == "P1=2" && Laufs(E2(4)) == "P2=2" && Laufs(E2(7)) == "P1=4" && Laufs(E2(8)) == "P2=4" && Laufs(E2(9)) == "";
            okPl &= E2(3) != null && E2(3).Buffers.Count == 1 && E2(3).Buffers[0].Text.Contains("AT-1");
            ks.Puffers[0].OffsetDays = null;   // beide wie der Schritt: alles an einem Tag (kein Splitten)
            MemoryStore ms3 = new MemoryStore();
            try { p2.Apply(ms3, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = "Ausnahme: " + ex.Message; }
            Entry same = ms3.Get(d0.AddDays(1), puf);
            okPl &= !ks.HasCardOffsets && same != null && same.Buffers.Count == 2 && (ms3.Get(d0, puf) == null || ms3.Get(d0, puf).Buffers.Count == 0);
            ks.Puffers[0].OffsetDays = -2;
            // Karte mit Versatz +1 (nach dem Lauf): Hinweis, Karte -2 / -1: keiner
            bool okRule = Ux5cCount(p2, "pufnach") == 0;
            ks.Puffers[1].OffsetDays = 1;
            okRule &= Ux5cCount(p2, "pufnach") == 1 && p2.Check().Exists(delegate(PackageProblem x) { return x.Code == "pufnach" && !x.Blocks && x.Text.Contains("Puffer 2"); });
            ks.Puffers[1].OffsetDays = null;
            sb.AppendLine("UX5c Zeitpunkt je Karte: Schedule/Karten " + Ok(okSch) + " Plan je Tag " + Ok(okPl) + " Hinweis nach dem Lauf " + Ok(okRule) + (ap.Length > 0 ? " " + ap : ""));

           
            // (3) Regeln: Pufferlaeufe <= Produktlaeufe weiter Sperre, Termin nach dem letzten Lauf = Hinweis, alter Bestand
            Package p3 = Ux5cBase("UX5c-Regeln");
            PackageStep r5 = UxPuf("Pr", 0, false, 1, "AT-1"); r5.Repeat = RepeatMode.Interval; r5.Interval = 2; r5.Count = 5; p3.Steps.Add(r5);
            bool okE4 = Ux5cCount(p3, "puffer") == 1 && Package.HasBlock(p3.Check());
            r5.Count = 4; r5.Interval = 1;   // Tage 1-4: alle vor dem letzten Lauf (Tag 9): kein Hinweis
            bool okAfter = Ux5cCount(p3, "puffer") == 0 && Ux5cCount(p3, "pufnach") == 0;
            r5.Day = 8; r5.Interval = 2; r5.Count = 3;   // 8, 10, 12: Tag 10 und 12 liegen nach Lauf 4 (Tag 9)
            okAfter &= Ux5cCount(p3, "pufnach") == 2 && Ux5cCount(p3, "puffer") == 0;
            okAfter &= PackageProblem.RuleTitle("pufnach").Length > 0;
            sb.AppendLine("UX5c Regeln: Pufferläufe ≤ Produktläufe " + Ok(okE4) + " Puffer nach dem letzten Lauf " + Ok(okAfter));

           
            // (4) XML-Rundweg: Attribut versatz nur bei eigenem Wert, Paketformat unveraendert, alte Pakete lesbar, Klammerung
            string xml = p2.ToXml();
            Package rt = Package.FromXml(xml); Package.Seen.Remove(rt);
            PackageStep rk = rt.Steps[1];
            bool okXml = xml.Contains(" versatz=\"-2\"") && rk.Puffers.Count == 2 && rk.Puffers[0].OffsetDays == -2 && !rk.Puffers[1].OffsetDays.HasValue && rt.ToXml() == xml;
            Package old = Package.FromXml(xml.Replace(" versatz=\"-2\"", "")); Package.Seen.Remove(old);
            okXml &= !old.Steps[1].Puffers[0].OffsetDays.HasValue && !old.Steps[1].HasCardOffsets;
            Package big = Package.FromXml(xml.Replace(" versatz=\"-2\"", " versatz=\"-40\"")); Package.Seen.Remove(big);
            okXml &= big.Steps[1].Puffers[0].OffsetDays == -PackageStep.MaxCardOffset;
            Package r1 = Package.FromXml(p1.ToXml()); Package.Seen.Remove(r1);
            okXml &= r1.Steps[1].Bind == StepBind.Frei && !p1.ToXml().Contains(" versatz=\"");   // eigener Rhythmus: nichts Neues im Format
            Package cl = p2.Clone();
            okXml &= cl.Steps[1].Puffers[0].OffsetDays == -2 && cl.Steps[1].Puffers[1].OffsetDays == null;
            sb.AppendLine("UX5c XML-Rundweg: eigener Versatz " + Ok(okXml));

           
            sb.AppendLine(PackageForm.TestUx5c(store, equip, start));   // Editor (PackageForm.TestUx5c.cs)
        }
    }
}
#endif
