#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests PB (Puffer im Paket-Editor erweitert): Standard-Anzahl Equipment je Position (Katalog, XML), mehrere Puffer je Termin
    // (Anzahl, Namen, Equipment reihum), Pruefregeln, Pufferpaket (Unterpaket) und Plan-Anzeige.
    partial class MainForm
    {
        static PackageStep PbStep(string title, string typ, int count, string names)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Source = "Puffer " + typ; s.Row = "Puffer"; s.Variant = typ; s.Day = 1; s.PufferCount = count; s.PufferNames = names;
            return s;
        }

        static string PbNames(List<BufferItem> l) { List<string> n = new List<string>(); foreach (BufferItem b in l) n.Add(b.Name); return string.Join("|", n.ToArray()); }

        static string PbTexts(List<BufferItem> l) { List<string> n = new List<string>(); foreach (BufferItem b in l) n.Add(b.Text.Replace("\n", "/")); return string.Join("|", n.ToArray()); }

        void SelfTestPB(StringBuilder sb)
        {
            // (a) Standard-Anzahl je Position: Katalog, Klon, XML (nur >1 wird geschrieben), alter Bestand = 1
            Catalog cat = Catalog.Default();
            Baustein bm = cat.Baustein_("Puffer mobil");
            bm.SetDefaultCount("MobT", 2);
            bool okDef = bm.DefaultCount("MobT") == 2 && bm.DefaultCount("Flasche") == 1 && bm.DefaultCount("Unbekannt") == 1 && cat.Clone().Baustein_("Puffer mobil").DefaultCount("MobT") == 2;
            MemoryStore cs = new MemoryStore(); cs.SaveCatalog(cat);
            byte[] cx = Backup.ToXml(cs.Snapshot());
            string cxs = Encoding.UTF8.GetString(cx);
            Baustein rb = Backup.FromXml(cx).Catalog.Baustein_("Puffer mobil");
            okDef &= rb != null && rb.DefaultCount("MobT") == 2 && rb.DefaultCount("Flasche") == 1 && cxs.Contains("<P v=\"MobT\" n=\"2\"") && cxs.IndexOf("v=\"Flasche\" n=", StringComparison.Ordinal) < 0;
            bm.SetDefaultCount("MobT", 1);
            okDef &= bm.DefaultCount("MobT") == 1 && bm.Counts.Count == 0;   // 1 = Standard, nichts gespeichert

            // (b) mehrere Puffer je Termin: Namen und Ersatznamen
            List<BufferItem> l = PbStep("Puffer mobil", "mobil", 3, "Lösung A|Lösung B").BuffersFor(0, "");
            bool okName = l.Count == 3 && PbNames(l) == "Lösung A|Lösung B|Puffer mobil 3" && BufferSet.Types(l) == "mobil:3" && l[2].Typ == "mobil" && l[0].Id != l[1].Id && l[1].Id != l[2].Id;
            okName &= PbNames(PbStep("Puffer mobil", "mobil", 3, "").BuffersFor(0, "")) == "Puffer mobil|Puffer mobil 2|Puffer mobil 3";
            okName &= PbNames(PbStep("Puffer mobil", "mobil", 3, "A||C").BuffersFor(0, "")) == "A|Puffer mobil 2|C";
            okName &= PbNames(PbStep("Puffer mobil", "mobil", 1, "").BuffersFor(0, "")) == "Puffer mobil" && PbNames(PbStep("Puffer mobil", "mobil", 1, "Einzel").BuffersFor(0, "")) == "Einzel";
            okName &= PbStep("P", "mobil|stationaer", 1, "").BuffersFor(0, "").Count == 2 && PbNames(PbStep("P", "mobil|stationaer", 1, "").BuffersFor(0, "")) == "P|P";   // alte Schritte unveraendert
            okName &= PbStep("P", "mobil|stationaer", 2, "").BuffersFor(0, "").Count == 4;

            // Equipment je Puffer: Zeilen gelten fuer jeden Puffer; "abwechselnd" reihum (Lauf r, Puffer k -> Geraet r*n+k)
            PackageStep alt = PbStep("Tank", "mobil", 3, ""); alt.Count = 2; alt.Interval = 1;
            EquipAssign aa = new EquipAssign(); aa.Short = "MobT"; aa.Alternate = true; aa.Choices.Add(new EquipChoice("MobT", "M1")); aa.Choices.Add(new EquipChoice("MobT", "M2")); alt.Equipment.Add(aa);
            EquipAssign fix = new EquipAssign(); fix.Short = "Flasche"; fix.Choices.Add(new EquipChoice("Flasche", "F1")); alt.Equipment.Add(fix);
            bool okEq = PbTexts(alt.BuffersFor(0, "")) == "MobT: M1/Flasche: F1|MobT: M2/Flasche: F1|MobT: M1/Flasche: F1"
                && PbTexts(alt.BuffersFor(1, "")) == "MobT: M2/Flasche: F1|MobT: M1/Flasche: F1|MobT: M2/Flasche: F1";
            PackageStep two = PbStep("Tank", "mobil", 2, "");   // Standard-Anzahl 2 = zwei Zeilen derselben Position: beide Zeilen in jedem Puffer
            for (int i = 1; i <= 2; i++) { EquipAssign a = new EquipAssign(); a.Short = "MobT"; a.Choices.Add(new EquipChoice("MobT", "M" + i)); two.Equipment.Add(a); }
            okEq &= PbTexts(two.BuffersFor(0, "")) == "MobT: M1/MobT: M2|MobT: M1/MobT: M2";

            // Plan: Anwenden, mehrere Termine, mobil und stationaer am selben Tag, Anzeige "Puffer n: Name"
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2032, 3, 1);
            Package p = new Package(); p.Name = "PB"; p.Steps.Add(alt);
            PackageStep stat = PbStep("Vorlage", "stationaer", 2, "Vorlage 1|Vorlage 2"); stat.Day = 1; p.Steps.Add(stat);
            p.Apply(ms, d0, ApplyMode.Overwrite, null);
            Entry e1 = ms.Get(d0, puf), e2 = ms.Get(d0.AddDays(1), puf);
            bool okPlan = e1 != null && e1.Buffers.Count == 5 && BufferSet.Count(e1.Buffers, "mobil") == 3 && BufferSet.Count(e1.Buffers, "stationaer") == 2 && e2 != null && e2.Buffers.Count == 3 && UniqueIds(e1)
                && e1.Buffers[0].Text == "MobT: M1\nFlasche: F1" && e2.Buffers[0].Text == "MobT: M2\nFlasche: F1";
            List<string> disp = BufferSet.DisplayLines(e1.Buffers, false);
            okPlan &= disp.Count == 5 && disp[0] == "Puffer 1: Tank 1 (mobil)" && disp[2] == "Puffer 3: Tank 1 (3) (mobil)" && disp[3] == "Puffer 4: Vorlage 1 (stationaer)" && disp[4].StartsWith("Puffer 5: Vorlage 2");

            // XML-Rundweg und Kompatibilitaet (ohne Attribute = 1 Puffer, keine Namen)
            string xml = p.ToXml();
            Package back = Package.FromXml(xml);
            bool okXml = back.Steps.Count == 2 && back.Steps[0].PufferCount == 3 && back.Steps[1].PufferNames == "Vorlage 1|Vorlage 2" && back.Steps[1].PufferCount == 2
                && PbNames(back.Steps[1].BuffersFor(0, "")) == "Vorlage 1|Vorlage 2";
            Package one = new Package(); one.Steps.Add(PbStep("Puffer mobil", "mobil", 1, ""));
            string x1 = one.ToXml();
            Package b1 = Package.FromXml(x1);
            okXml &= x1.IndexOf(" puffer=\"", StringComparison.Ordinal) < 0 && x1.IndexOf("pufnamen", StringComparison.Ordinal) < 0 && b1.Steps[0].PufferCount == 1 && b1.Steps[0].PufferNames == "" && b1.Steps[0].BuffersFor(0, "").Count == 1;
            Package big = Package.FromXml(x1.Replace("variante=\"mobil\"", "variante=\"mobil\" puffer=\"12\""));
            Package zero = Package.FromXml(x1.Replace("variante=\"mobil\"", "variante=\"mobil\" puffer=\"0\""));
            okXml &= big.Steps[0].PufferCount == 12 && zero.Steps[0].PufferCount == 1;

            // Regeln: Termine zaehlen Vorkommen (nicht Puffer); > 9 gesperrt; ueberzaehlige Namen als Hinweis
            Package rule = Package.SampleCampaign("PB-Regeln"); Package.Seen.Remove(rule);
            PackageStep fr = PbStep("Puffer mobil", "mobil", 5, ""); fr.Day = 2; fr.Count = 4; fr.Interval = 1; rule.Steps.Add(fr);
            bool okRule = !Has(rule.Check(), "puffer", Severity.Sperre) && !Has(rule.Check(), "pufzahl", Severity.Sperre);   // 4 Vorkommen bei 4 Laeufen, 5 Puffer je Termin: erlaubt
            fr.Count = 5; okRule &= Has(rule.Check(), "puffer", Severity.Sperre);
            fr.Count = 4; fr.PufferCount = 12; okRule &= Has(rule.Check(), "pufzahl", Severity.Sperre) && Has(big.Check(), "pufzahl", Severity.Sperre);
            fr.PufferCount = 2; fr.PufferNames = "A|B|C"; okRule &= Has(rule.Check(), "pufnamen", Severity.Hinweis);
            fr.PufferNames = "A|B"; okRule &= !Has(rule.Check(), "pufnamen", Severity.Hinweis) && !Has(rule.Check(), "pufzahl", Severity.Sperre);

            // Pufferpaket (Unterpaket): nur Puffer-Schritte; Kopie ohne Bezug zu Hauptlauf/Rhythmus
            Package bp = new Package(); bp.Steps.Add(Package.ForBufferPackage(fr));
            Package mixed = Package.SampleCampaign("Gemischt"); Package.Seen.Remove(mixed);
            bool okBp = bp.IsBufferPackage && !mixed.IsBufferPackage && !new Package().IsBufferPackage && bp.Steps[0].PufferCount == 2 && bp.Steps[0].PufferNames == "A|B"
                && bp.Steps[0].Day == 1 && bp.Steps[0].Count == 1 && bp.Steps[0].Bind == StepBind.Frei && fr.Count == 4 && fr.Day == 2 && bp.Steps[0] != fr;   // fr bleibt unveraendert
            Package bpBack = Package.FromXml(bp.ToXml());
            okBp &= bpBack.IsBufferPackage && bpBack.Steps[0].PufferNames == "A|B";

            sb.AppendLine("PB Puffer-Paket: Standard-Anzahl Katalog/XML=" + Ok(okDef) + " Namen=" + Ok(okName) + " Equipment je Puffer=" + Ok(okEq) + " Plan 1..n=" + Ok(okPlan)
                + " XML/alt=" + Ok(okXml) + " Regeln=" + Ok(okRule) + " Pufferpaket Core=" + Ok(okBp));
            sb.AppendLine(PackageForm.TestPB(store, equip, start));   // Editor: Anzahl/Namen, Standard-Zeilen, Pufferpaket einfuegen (PackageForm.TestPB.cs)
        }
    }
}
#endif
