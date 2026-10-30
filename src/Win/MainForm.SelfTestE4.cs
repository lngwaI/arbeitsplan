#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E4: Regelpruefung (Core/Packages.Rules.cs): jede Sperre/jeder Hinweis einzeln, Apply verweigert bei Sperre,
    // Kampagnenstart (rote Zeile), Puffer verschiedener Produkte gegen den Plan, gueltige Pakete ohne Sperre.
    partial class MainForm
    {
        static bool Has(List<PackageProblem> l, string code, Severity s) { return l.Exists(delegate(PackageProblem p) { return p.Code == code && p.Severity == s; }); }

        static PackageStep E4Step(string title, string row, int day, StepKind k, int count)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Row = row; s.Day = day; s.Kind = k; s.Count = count; s.Interval = 6; s.StartNo = 1;
            return s;
        }

        static int E4Row(IDataStore st, string name) { foreach (RowInfo r in st.GetRows()) if (r.Name == name) return r.Id; return -1; }

        void SelfTestE4(StringBuilder sb)
        {
            DateTime d0 = new DateTime(2033, 3, 7);   // Montag
            Package good = Package.SampleCampaign("E4");
            bool okValid = !Package.HasBlock(good.Check());
            bool hasTp = false, okTp = true;
            foreach (string c in new[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\config\Testprodukt.apkg"), @"config\Testprodukt.apkg" })
                if (File.Exists(c)) { okTp = !Package.HasBlock(Package.Load(c).Check()); hasTp = true; break; }
            MemoryStore mv = new MemoryStore();
            bool okAp = true; try { Package.SampleCampaign("E4b").Apply(mv, d0, ApplyMode.FillEmpty, null, me); } catch (Exception) { okAp = false; }
            sb.AppendLine("E4 Gültige Pakete: Beispiel ohne Sperre " + K(okValid) + " Testprodukt " + (hasTp ? K(okTp) : "nicht gefunden") + " Übernehmen geht " + K(okAp));

            // Struktur: Hauptlauf-Anzahl, Laufanzahl, Laufnummer, Kennung, Pakettypen
            Package k0 = new Package(); k0.Type = PackageType.Kampagne; k0.Steps.Add(E4Step("Packen", "Suite 1", 1, StepKind.Normal, 1));
            Package k2 = Package.SampleCampaign("zwei"); k2.Steps.Add(E4Step("Lauf B", "Suite 2", 2, StepKind.Run, 2));
            Package kn = Package.SampleCampaign("anzahl"); kn.Steps[2].Count = 500;   // wird auf 99 begrenzt: Regel 1..99 ist nur Absicherung
            Package kz = Package.SampleCampaign("nummer"); kz.Steps[2].StartNo = 9998;
            Package ki = Package.SampleCampaign("kennung"); ki.Steps[2].Ident = ""; ki.Steps[2].IdentStart = "PA XYZ";
            Package ke = new Package(); ke.Type = PackageType.Einzel; ke.Steps.Add(E4Step("Lauf", "Suite 1", 1, StepKind.Run, 1));
            Package kv = new Package(); kv.Type = PackageType.Ereignis; PackageStep ev = E4Step("Spülen", "Suite 1", 1, StepKind.Normal, 1); ev.ManualNo = "12340001"; kv.Steps.Add(ev);
            bool a = Has(k0.Check(), "kopf", Severity.Sperre), b = Has(k2.Check(), "kopf", Severity.Sperre), c2 = !Has(kn.Check(), "laeufe", Severity.Sperre) && kn.RunCount == PackageStep.MaxRuns;
            bool d = Has(kz.Check(), "number", Severity.Sperre), e = Has(ki.Check(), "ident", Severity.Sperre), f = Has(ke.Check(), "einzel", Severity.Sperre), g = Has(kv.Check(), "ereignis", Severity.Sperre);
            sb.AppendLine("E4 Sperren Struktur: ohne Hauptlauf " + K(a) + " zwei Hauptläufe " + K(b) + " Laufanzahl höchstens 99 " + K(c2) + " Nummer über 9999 " + K(d)
                + " Kennung ohne Endziffern " + K(e) + " Einzelpaket mit Lauf " + K(f) + " Ereignis mit Nummer " + K(g));

            // Zwei Hauptlaeufe am selben Tag (je Raum); Unterlauf/Puffer am selben Tag erlaubt
            Package sd = Package.SampleCampaign("gleich");
            List<RunDate> sch = sd.Schedule();
            RunDate r0 = sch.Find(delegate(RunDate x) { return x.Step == 2 && x.Run == 0; }), r1 = sch.Find(delegate(RunDate x) { return x.Step == 2 && x.Run == 1; });
            r1.Day = r0.Day;
            bool sameBad = Has(sd.Check(mv, d0, ApplyMode.FillEmpty, sch), "gleichtag", Severity.Sperre);
            r1.Row = "Suite 2";
            bool sameRoom = !Has(sd.Check(mv, d0, ApplyMode.FillEmpty, sch), "gleichtag", Severity.Sperre);
            Package sx = Package.SampleCampaign("erlaubt");
            PackageStep sub = E4Step("Splitten", "Suite 1", 1, StepKind.SubRun, 1); sub.Bind = StepBind.JeLauf; sub.RunOffset = 0; sub.SubNos = "1, 2, 3, 4";
            PackageStep pu = E4Step("Ansatz", "Puffer", 1, StepKind.Normal, 1); pu.Bind = StepBind.JeLauf; pu.RunOffset = 0; pu.Variant = "mobil";
            sx.Steps.Add(sub); sx.Steps.Add(pu);
            List<PackageProblem> px = sx.Check();
            bool okSub = !Has(px, "gleichtag", Severity.Sperre) && !Has(px, "puffer", Severity.Sperre) && !Has(px, "unterlauf", Severity.Sperre) && !Package.HasBlock(px);
            sb.AppendLine("E4 Zwei Hauptläufe am selben Tag: gleicher Block gesperrt " + K(sameBad) + " anderer Block erlaubt " + K(sameRoom) + " Unterlauf/Puffer am selben Tag erlaubt " + K(okSub));

            // Puffer > Produktlaeufe, Unterlaeufe < Hauptlaeufe
            Package pf = Package.SampleCampaign("puffer");
            PackageStep pfree = E4Step("Ansatz", "Puffer", 1, StepKind.Normal, 6); pfree.Interval = 1; pfree.Variant = "mobil"; pf.Steps.Add(pfree);
            PackageStep pje = E4Step("Ansatz", "Puffer", 1, StepKind.Normal, 1); pje.Bind = StepBind.JeLauf; pje.RunOffset = -1; pje.RunEvery = 2; pje.Variant = "stationaer";
            Package pj = Package.SampleCampaign("puffer2"); pj.Steps.Add(pje);
            bool okPuf = Has(pf.Check(), "puffer", Severity.Sperre) && !Has(pj.Check(), "puffer", Severity.Sperre);
            Package ul = Package.SampleCampaign("unter");
            PackageStep us = E4Step("Splitten", "Suite 1", 3, StepKind.SubRun, 3); us.SubNos = "1, 2, 3"; ul.Steps.Add(us);
            List<PackageProblem> pu2 = ul.Check();
            bool okUl = Has(pu2, "unterlauf", Severity.Sperre) && pu2.Exists(delegate(PackageProblem p) { return p.Text.StartsWith("Lauf 4 ohne Unterlauf"); });
            Package ul2 = Package.SampleCampaign("unter4"); PackageStep us4 = E4Step("Splitten", "Suite 1", 3, StepKind.SubRun, 4); us4.SubNos = "1, 2, 3, 4"; ul2.Steps.Add(us4);
            okUl &= !Has(ul2.Check(), "unterlauf", Severity.Sperre);
            sb.AppendLine("E4 Puffer: mehr Termine als Läufe gesperrt, „zu jedem Lauf“ frei " + K(okPuf) + " Unterläufe: „Lauf 4 ohne Unterlauf“ " + K(okUl));

            // Hinweise
            Package hn = Package.SampleCampaign("hinweis");
            bool hPack = Has(hn.Check(), "packen", Severity.Info);
            hn.Steps[0].ManualNo = "12340001";
            bool hPack2 = !Has(hn.Check(), "packen", Severity.Info);
            Package ht = Package.SampleCampaign("frueh"); ht.Steps[2].Day = 70;
            bool hFrueh = Has(ht.Check(), "frueh", Severity.Hinweis) && !Has(good.Check(), "frueh", Severity.Hinweis);
            Package he = Package.SampleCampaign("every"); he.Steps[3].RunEvery = 2;
            bool hEvery = Has(he.Check(), "every", Severity.Hinweis) && !Has(pj.Check(), "every", Severity.Hinweis);
            sb.AppendLine("E4 Hinweise: Packen ohne Nummer " + K(hPack && hPack2) + " mehr als 60 Tage vor Lauf 1 " + K(hFrueh) + " jeder n-te Lauf bei Nicht-Puffer " + K(hEvery)
                + " (Hinweise sperren nicht " + K(!Package.HasBlock(hn.Check()) && !Package.HasBlock(ht.Check())) + ")");

            // Apply verweigert bei Sperre (auch fuer den Admin), Plan bleibt leer
            MemoryStore ma = new MemoryStore(); int suite = E4Row(ma, "Suite 1");
            r1.Row = "Suite 1";
            bool refused = false, plain = false;
            try { sd.Apply(ma, d0, ApplyMode.FillEmpty, sch, me); } catch (InvalidOperationException ex) { refused = ex.Message.Contains("selben Tag"); }
            Entry none = ma.Get(d0.AddDays(2), suite);
            plain = none == null || (none.IsEmpty && none.Buffers.Count == 0);
            bool refused2 = false; try { k2.Apply(ma, d0, ApplyMode.FillEmpty, null, me); } catch (InvalidOperationException) { refused2 = true; }
            sb.AppendLine("E4 Apply: verweigert bei Sperre " + K(refused && refused2) + " nichts geschrieben " + K(plain));

            // Kampagnenstart: rote Zeile, Uebernehmen gesperrt, Rueckgang hebt es auf
            bool okCf = true, okRed = false, okLift = false;
            using (CampaignForm cf = new CampaignForm(Package.SampleCampaign("Rot"), new MemoryStore(), d0, ApplyMode.FillEmpty))
            {
                cf.TestPackNo = "12340001"; cf.TestShiftFollowing = false;
                okCf = cf.TestOkEnabled && !Package.HasBlock(cf.Problems);
                int i1 = cf.TestIndexOf(2, 1);
                cf.Shift(i1, -6);
                okRed = !cf.TestOkEnabled && cf.TestRowRed(i1) && Has(cf.Problems, "gleichtag", Severity.Sperre) && cf.RuleText.StartsWith("Gesperrt");
                cf.Shift(i1, 6);
                okLift = cf.TestOkEnabled && !cf.TestRowRed(i1) && !cf.RuleText.StartsWith("Gesperrt");   // Testkampagne ohne Puffer: Hinweis bleibt
            }
            sb.AppendLine("E4 Kampagnenstart: ohne Verstoß frei " + K(okCf) + " Zeile rot, Übernehmen gesperrt " + K(okRed) + " nach Korrektur wieder frei " + K(okLift));

            // Gegen den Plan: Hauptlauf im selben Block/Tag
            MemoryStore mp = new MemoryStore(); int sp = E4Row(mp, "Suite 1");
            Entry pl = new Entry(); pl.Content = "Lauf 55550001"; mp.Save(d0.AddDays(2), sp, pl);
            Package pp = Package.SampleCampaign("plan");
            bool planBad = Has(pp.Check(mp, d0, ApplyMode.FillEmpty, null), "planlauf", Severity.Sperre);
            bool planOver = !Has(pp.Check(mp, d0, ApplyMode.Overwrite, null), "planlauf", Severity.Sperre);
            bool planRefused = false; try { pp.Apply(mp, d0, ApplyMode.FillEmpty, null, me); } catch (InvalidOperationException) { planRefused = true; }
            sb.AppendLine("E4 Hauptlauf im Plan am selben Tag: gesperrt " + K(planBad) + " Überschreiben ersetzt ihn " + K(planOver) + " Übernehmen verweigert " + K(planRefused));

            // Puffer verschiedener Produkte: gleicher Tag Sperre (anderes Produkt erkennbar), Vortag Hinweis, unsicher nur Hinweis
            Package pb = new Package(); pb.Name = "PufferProdukt"; pb.ProductCode = "1234";
            pb.Steps.Add(E4Step("Lauf", "Suite 1", 3, StepKind.Run, 2));
            PackageStep pbs = E4Step("Ansatz", "Puffer", 1, StepKind.Normal, 1); pbs.Bind = StepBind.JeLauf; pbs.RunOffset = -1; pbs.Variant = "mobil"; pb.Steps.Add(pbs);
            // Puffer von Lauf 1 liegt an Tag 2 (= d0 + 1)
            bool bSame = BufCheck(d0.AddDays(1), "Lauf 55550001", pb, d0, "puffer-produkt", Severity.Sperre);
            bool bPrev = BufCheck(d0, "Lauf 55550001", pb, d0, "puffer-produkt", Severity.Hinweis) && !BufCheck(d0, "Lauf 55550001", pb, d0, "puffer-produkt", Severity.Sperre);
            bool bNext = BufCheck(d0.AddDays(2), "Lauf 55550001", pb, d0, "puffer-produkt", Severity.Hinweis);
            bool bUnk = BufCheck(d0.AddDays(1), "Charge 7", pb, d0, "puffer-produkt", Severity.Hinweis) && !BufCheck(d0.AddDays(1), "Charge 7", pb, d0, "puffer-produkt", Severity.Sperre);
            bool bUnkPrev = !BufCheck(d0, "Charge 7", pb, d0, "puffer-produkt", Severity.Hinweis);
            bool bOwn = !BufCheck(d0.AddDays(1), "Lauf 12340005", pb, d0, "puffer-produkt", Severity.Hinweis) && !BufCheck(d0.AddDays(1), "Lauf 12340005", pb, d0, "puffer-produkt", Severity.Sperre);
            sb.AppendLine("E4 Puffer verschiedener Produkte gegen den Plan: anderes Produkt am selben Tag gesperrt " + K(bSame) + " Vortag Hinweis " + K(bPrev) + " Folgetag Hinweis " + K(bNext)
                + " unsicher am selben Tag nur Hinweis " + K(bUnk) + " unsicher am Vortag still " + K(bUnkPrev) + " eigenes Produkt frei " + K(bOwn));
        }

        // Plan mit einem Puffer-Block (Text) am Tag day; Prueft Package.Check gegen den Plan auf Code/Schwere
        bool BufCheck(DateTime day, string text, Package p, DateTime start, string code, Severity sev)
        {
            MemoryStore m = new MemoryStore(); int pr = E4Row(m, "Puffer");
            Entry e = new Entry(); BufferSet.Add(e.Buffers, "Fremd", "mobil", text); m.Save(day, pr, e);
            return Has(p.Check(m, start, ApplyMode.FillEmpty, null), code, sev);
        }
    }
}
#endif
