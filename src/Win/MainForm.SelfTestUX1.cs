#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-1a: Equipment je Puffer (PufferSpec), Migration alter Pakete (Golden), WFI-Geltungsbereich, Standardpuffer im Katalog,
    // Pufferpaket je Produkt, Regel "Geraet in zwei Puffern am selben Tag", XML-Rundweg Paket und Katalog.
    partial class MainForm
    {
        static EquipAssign UxEq(string sh, params string[] ids)
        {
            EquipAssign a = new EquipAssign(); a.Short = sh;
            foreach (string id in ids) a.Choices.Add(new EquipChoice(sh, id));
            return a;
        }

        static PufferSpec UxPuffer(string name, params EquipAssign[] lines)
        {
            PufferSpec p = new PufferSpec(); p.Name = name; p.Typ = "mobil"; p.Lines.AddRange(lines); return p;
        }

        static PackageStep UxStep(string title, int day)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Source = "Puffer mobil"; s.Row = "Puffer"; s.Day = day; s.Variant = "mobil";
            return s;
        }

        // Referenz: so hat der alte Code die Puffer gebildet (Zeilen aller Puffer gleich, "abwechselnd" mit Index run*n+Platz); gebaut aus Compose
        static List<BufferItem> UxReference(PackageStep s, int run, string prefix)
        {
            int n = Math.Max(1, s.PufferCount);
            List<BufferItem> res = new List<BufferItem>();
            string[] names = s.BufferNames();
            int k = 0;
            foreach (KeyValuePair<string, int> kv in VariantSet.Counts(s.Variant))
                for (int i = 0; i < kv.Value; i++)
                    for (int j = 0; j < n; j++, k++)
                    {
                        PackageStep c = s.Clone(); c.Puffers.Clear();
                        int shift = run * (n - 1) + j;
                        foreach (EquipAssign a in c.Equipment)
                            if (a.Alternate && a.Choices.Count > 1)
                            {
                                List<EquipChoice> rot = new List<EquipChoice>();
                                for (int q = 0; q < a.Choices.Count; q++) rot.Add(a.Choices[(q + shift) % a.Choices.Count]);
                                a.Choices = rot;
                            }
                        List<string> lines = new List<string>(c.Compose(run, prefix).Split('\n'));
                        string name = "";
                        if (s.Title.Length > 0 && lines.Count > 0) { name = lines[0]; lines.RemoveAt(0); }
                        if (k < names.Length && names[k].Length > 0) name = names[k];
                        else if (n > 1 && k > 0 && name.Length > 0) name += s.Numbered ? " (" + (k + 1) + ")" : " " + (k + 1);
                        BufferSet.Add(res, name, kv.Key, string.Join("\n", lines.ToArray()));
                    }
            return res;
        }

        static string UxTexts(List<BufferItem> l)
        {
            List<string> n = new List<string>();
            foreach (BufferItem b in l) n.Add(b.Typ + "/" + b.Name + "/" + BufferSet.Plain(b.Text).Replace("\n", "|"));   // Plain: Notiz-Marker (UX-5f) ist nur Darstellung
            return string.Join(";", n.ToArray());
        }

        void SelfTestUX1(StringBuilder sb)
        {
            // (1) Golden: alle geladenen Pakete + gebaute Altschritte: BuffersFor (alter Modus) == Referenz == nach MigratePuffers == nach XML-Rundweg
            List<Package> seen = new List<Package>(Package.Seen);
            List<PackageStep> legacy = new List<PackageStep>();
            PackageStep l1 = UxStep("Puffer mobil", 1); l1.PufferCount = 3; l1.PufferNames = "Lösung A|Lösung B"; l1.Count = 3; l1.Interval = 1;
            EquipAssign alt = UxEq("MobT", "M1", "M2", "M3"); alt.Alternate = true; alt.Fallback = "M9"; l1.Equipment.Add(alt); l1.Equipment.Add(UxEq("Flasche", "F1")); l1.Note = "Notiz A\nNotiz B"; l1.Text = "Freitext";
            PackageStep l2 = UxStep("Vorlage", 1); l2.Variant = "mobil:2|stationaer"; l2.PufferCount = 2; l2.Count = 4; l2.Interval = 1; l2.StartNo = 5;
            EquipAssign a2 = UxEq("VT", "V1", "V2"); a2.Alternate = true; l2.Equipment.Add(a2); l2.Ansatz = "gebinde"; l2.Gebinde = "Flasche"; l2.Menge = "5"; l2.Einheit = "L";
            PackageStep l3 = UxStep("", 1); l3.Variant = "stationaer";
            legacy.Add(l1); legacy.Add(l2); legacy.Add(l3);
            int pk = 0, st = 0, bad = 0, badMig = 0, badXml = 0;
            List<PackageStep> all = new List<PackageStep>(legacy); List<string> prefixes = new List<string>(); foreach (PackageStep x in legacy) prefixes.Add("");
            foreach (Package p in seen) { pk++; foreach (PackageStep s in p.Steps) if (s.IsBuffer) { all.Add(s); prefixes.Add(p.Prefix); } }
            Package holder = new Package();
            for (int i = 0; i < all.Count; i++)
            {
                PackageStep s = all[i]; st++;
                PackageStep c = s.Clone(); bool migrated = c.MigratePuffers();
                holder.Steps.Clear(); holder.Steps.Add(c);
                PackageStep back = Package.FromXml(holder.ToXml()).Steps[0];
                for (int run = 0; run < Math.Min(8, Math.Max(1, s.Count)); run++)
                {
                    string want = UxTexts(UxReference(s, run, prefixes[i]));
                    if (UxTexts(s.BuffersFor(run, prefixes[i])) != want) bad++;
                    if (UxTexts(c.BuffersFor(run, prefixes[i])) != want) badMig++;
                    if (UxTexts(back.BuffersFor(run, prefixes[i])) != want) badXml++;
                }
                if (!migrated || c.Puffers.Count != s.LegacyPuffers().Count || c.Equipment.Count != 0) badMig++;
            }
            bool okGold = bad == 0 && badMig == 0 && badXml == 0 && st >= 3;

            // (2) je Puffer eigene Geraete, WFI-Geltungsbereich (nur Puffer 1 / beide / keiner), Plan-Ausgabe
            PackageStep ps = UxStep("Puffer mobil", 1);
            ps.Puffers.Add(UxPuffer("Lösung A", UxEq("Ansatztank", "AT-1"), UxEq("VT", "VT-1")));
            ps.Puffers.Add(UxPuffer("Lösung B", UxEq("Ansatztank", "AT-2"), UxEq("VT", "VT-2")));
            PufferShare wfi = new PufferShare(); wfi.Eq = UxEq("WFI", "WFI-1"); ps.PufferShared.Add(wfi);
            ps.SyncPuffers();
            wfi.Scope = 3; string both = UxTexts(ps.BuffersFor(0, ""));
            wfi.Scope = 1; string only1 = UxTexts(ps.BuffersFor(0, ""));
            wfi.Scope = 0; string all0 = UxTexts(ps.BuffersFor(0, ""));
            ps.PufferShared.Clear(); string none = UxTexts(ps.BuffersFor(0, ""));
            bool okOwn = none == "mobil/Lösung A/Ansatztank: AT-1|VT: VT-1;mobil/Lösung B/Ansatztank: AT-2|VT: VT-2"
                && both == "mobil/Lösung A/Ansatztank: AT-1|VT: VT-1|WFI: WFI-1;mobil/Lösung B/Ansatztank: AT-2|VT: VT-2|WFI: WFI-1" && all0 == both
                && only1 == "mobil/Lösung A/Ansatztank: AT-1|VT: VT-1|WFI: WFI-1;mobil/Lösung B/Ansatztank: AT-2|VT: VT-2"
                && ps.Variant == "mobil:2" && ps.PufferCount == 1 && ps.IsBuffer && !ps.IsEmpty;
            wfi.Scope = 2; ps.PufferShared.Add(wfi);   // nur Puffer 2
            okOwn &= UxTexts(ps.BuffersFor(0, "")).EndsWith("Ansatztank: AT-2|VT: VT-2|WFI: WFI-1") && UxTexts(ps.BuffersFor(0, "")).IndexOf("VT-1|WFI") < 0;
            wfi.Scope = 3;
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2033, 5, 2);
            Package pp = new Package(); pp.Name = "UX1"; pp.Steps.Add(ps);
            pp.Apply(ms, d0, ApplyMode.Overwrite, null);
            Entry e = ms.Get(d0, puf);
            bool okPlan = e != null && e.Buffers.Count == 2 && e.Buffers[0].Text == "Ansatztank: AT-1\nVT: VT-1\nWFI: WFI-1" && e.Buffers[1].Text == "Ansatztank: AT-2\nVT: VT-2\nWFI: WFI-1" && e.Buffers[1].Name == "Lösung B" && BufferSet.Types(e.Buffers) == "mobil:2";
            List<List<string>> dg = ps.DeviceGroups(0);
            okPlan &= dg.Count == 2 && dg[0].Count == 3 && dg[0][2] == "WFI-1" && new Catalog().CheckPackage(pp).Count == 0;

            // (3) Regel: Geraet in zwei Puffern am selben Tag = Sperre mit Beteiligten; gemeinsame Zeile erlaubt; anderer Tag erlaubt
            Package rp = new Package(); rp.Name = "UX1-Regel";
            PackageStep r1 = UxStep("Puffer mobil", 1);
            r1.Puffers.Add(UxPuffer("A", UxEq("Ansatztank", "AT-1"))); r1.Puffers.Add(UxPuffer("B", UxEq("Ansatztank", "AT-1")));
            r1.SyncPuffers(); rp.Steps.Add(r1);
            List<PackageProblem> pr = rp.Check();
            PackageProblem dup = pr.Find(delegate(PackageProblem q) { return q.Code == "pufdoppelt"; });
            bool okRule = dup != null && dup.Blocks && dup.Text.Contains("Ansatztank AT-1 ist in Puffer 1 und Puffer 2 am selben Tag") && dup.Step == 0 && dup.Step2 == 0 && dup.Action.Length > 0 && PackageProblem.RuleTitle("pufdoppelt").Length > 0;
            try { rp.Apply(new MemoryStore(), d0, ApplyMode.Overwrite, null); okRule = false; } catch (InvalidOperationException) { }
            r1.Puffers[1].Lines.Clear(); r1.Puffers[1].Lines.Add(UxEq("Ansatztank", "AT-2"));
            PufferShare w2 = new PufferShare(); w2.Eq = UxEq("WFI", "WFI-1"); w2.Scope = 3; r1.PufferShared.Add(w2);
            okRule &= !Has(rp.Check(), "pufdoppelt", Severity.Sperre);   // verschiedene Ansatztanks, WFI gemeinsam fuer beide: erlaubt
            r1.Puffers[0].Lines.Add(UxEq("WFI", "WFI-1"));   // dasselbe Geraet zusaetzlich als eigene Zeile: Sperre (gemeinsame Zeile + eigene Zeile)
            okRule &= Has(rp.Check(), "pufdoppelt", Severity.Sperre);
            r1.Puffers[0].Lines.RemoveAt(1);
            PackageStep r2 = UxStep("Puffer 2", 1); r2.Puffers.Add(UxPuffer("C", UxEq("Ansatztank", "AT-2"))); r2.SyncPuffers(); rp.Steps.Add(r2);
            List<PackageProblem> p2 = rp.Check();
            okRule &= Has(p2, "pufdoppelt", Severity.Sperre) && p2.Find(delegate(PackageProblem q) { return q.Code == "pufdoppelt"; }).Step2 == 1;   // zwei Schritte am selben Tag
            r2.Day = 3; okRule &= !Has(rp.Check(), "pufdoppelt", Severity.Sperre);   // anderer Tag: erlaubt
            Package legacyDup = new Package(); PackageStep ld = UxStep("Puffer mobil", 1); ld.PufferCount = 2; ld.Equipment.Add(UxEq("Ansatztank", "AT-1")); legacyDup.Steps.Add(ld);
            okRule &= !Has(legacyDup.Check(), "pufdoppelt", Severity.Sperre);   // alter Modus: Regel erst nach Umstellung
            ld.MigratePuffers(); okRule &= Has(legacyDup.Check(), "pufdoppelt", Severity.Sperre);

            // (4) Standardpuffer im Katalog, Standard-Anzahl, Klon, XML-Rundweg
            Catalog cat = Catalog.Default();
            Baustein sp = cat.Baustein_(Catalog.StandardPufferName);
            PufferTemplate std = cat.StandardPuffer();
            bool okStd = sp != null && sp.Variant.Length == 0 && !sp.DefaultInCampaign && !sp.Unique && std != null && std.Positions.Count == 5 && std.Positions[0].Short == "Ansatztank" && cat.Position("Ansatztank") != null;
            sp.SetDefaultCount("Tween", 2); sp.Positions.Add("Probe");
            okStd &= cat.StandardPuffer().Positions.Count == 7 && cat.Clone().StandardPuffer().Positions.Count == 7;
            MemoryStore cs = new MemoryStore(); cs.SaveCatalog(cat);
            Catalog rc = Backup.FromXml(Backup.ToXml(cs.Snapshot())).Catalog;
            okStd &= rc.StandardPuffer() != null && rc.StandardPuffer().Positions.Count == 7 && Package.PufferFor(rc, "ZZ9").Count == 1;   // ohne Produktprofil: Standardpuffer

            // (5) Pufferpaket je Produkt: Vorlagen mit vorbelegten Geraeten, gemeinsame WFI-Zeile, Anwenden auf einen Schritt, XML im Katalog
            ProductProfile prod = new ProductProfile(); prod.Code = "AB1"; prod.Name = "Produkt A";
            prod.Puffer.Add(UxTpl("Lösung A", "AT-A", "VT-1")); prod.Puffer.Add(UxTpl("Lösung B", "AT-B", "VT-2"));
            cat.Products.Add(prod);
            cs.SaveCatalog(cat);
            Catalog rc2 = Backup.FromXml(Backup.ToXml(cs.Snapshot())).Catalog;
            ProductProfile rp1 = rc2.Product("AB1");
            bool okProd = rp1 != null && rp1.Puffer.Count == 2 && rp1.Puffer[1].Name == "Lösung B" && rp1.Puffer[0].Positions.Count == 3 && rp1.Puffer[0].Positions[2].Shared && rp1.Puffer[0].Positions[0].Ids[0] == "AT-A" && cat.Clone().Product("AB1").Puffer.Count == 2;
            List<PufferTemplate> tpls = Package.PufferFor(rc2, "AB1");
            PackageStep tp = UxStep("Puffer mobil", 1);
            Package.ApplyPufferTemplates(tp, tpls);
            okProd &= tpls.Count == 2 && tp.Puffers.Count == 2 && tp.PufferShared.Count == 1 && tp.PufferShared[0].Scope == 3 && tp.Variant == "mobil:2" && tp.Equipment.Count == 0
                && UxTexts(tp.BuffersFor(0, "")) == "mobil/Lösung A/Ansatztank: AT-A|VT: VT-1|WFI: WFI-1;mobil/Lösung B/Ansatztank: AT-B|VT: VT-2|WFI: WFI-1";
            Package.ApplyPufferTemplate(tp, UxTpl("Lösung C", "AT-C", "VT-3"));   // dritter Puffer: gemeinsame WFI-Zeile gilt weiter nur fuer 1 und 2
            okProd &= tp.Puffers.Count == 3 && tp.PufferShared[0].Scope == 7 && tp.Variant == "mobil:3";   // Vorlage C hat WFI auch als gemeinsam markiert
            PackageStep tl = UxStep("Puffer mobil", 1); tl.PufferCount = 2; tl.Equipment.Add(UxEq("MobT", "M1"));   // alter Schritt + Vorlage: erst verlustfrei umgestellt, dann dritter Puffer
            string before = UxTexts(tl.BuffersFor(0, ""));
            PufferTemplate lone = new PufferTemplate(); lone.Name = "Neu"; lone.Positions.Add(new PufferTemplatePos()); lone.Positions[0].Short = "Tween";
            Package.ApplyPufferTemplate(tl, lone);
            List<BufferItem> after = tl.BuffersFor(0, "");
            okProd &= after.Count == 3 && UxTexts(after.GetRange(0, 2)) == before && after[2].Name == "Neu" && after[2].Text == "";
            Package.ApplyPufferTemplates(tl, new List<PufferTemplate>()); okProd &= tl.Puffers.Count == 0 && !tl.IsBuffer;   // leere Liste = kein Puffer mehr
            Package prodPkg = new Package(); prodPkg.ProductCode = "AB1"; PackageStep wr = UxStep("Puffer mobil", 1);
            wr.Puffers.Add(UxPuffer("A", UxEq("WFI", "H1-T-0120"), UxEq("WFI", "X-NICHT"))); wr.SyncPuffers(); prodPkg.Steps.Add(wr);
            List<string> warn = cat.CheckPufferProduct(prodPkg);
            okProd &= warn.Count == 1 && warn[0].Contains("X-NICHT") && warn[0].Contains("Puffer 1");

            // (6) XML-Rundweg Paket mit eigenen Puffern: Version 8, gleiche Ausgabe, alte Pakete bleiben Version 7
            Package xp = new Package(); xp.Name = "UX1-XML"; xp.Steps.Add(ps); xp.Steps.Add(l1.Clone());
            string xml = xp.ToXml();
            Package xb = Package.FromXml(xml);
            bool okXml = xml.Contains("version=\"8\"") && xml.Contains("<Puffer ") && xml.Contains("<Gemeinsam") && xb.Steps[0].Puffers.Count == 2 && xb.Steps[0].PufferShared.Count == 1 && xb.Steps[0].PufferShared[0].Scope == 3
                && UxTexts(xb.Steps[0].BuffersFor(0, "")) == UxTexts(ps.BuffersFor(0, "")) && xb.Steps[0].Variant == "mobil:2" && xb.Steps[0].Equipment.Count == 0 && xb.Steps[1].Puffers.Count == 0
                && UxTexts(xb.Steps[1].BuffersFor(1, "")) == UxTexts(l1.BuffersFor(1, "")) && xb.ToXml() == xml;
            Package old = new Package(); old.Steps.Add(l1.Clone());
            okXml &= old.ToXml().Contains("version=\"7\"") && old.ToXml().IndexOf("<Gemeinsam", StringComparison.Ordinal) < 0 && old.ToXml().IndexOf("<Puffer ", StringComparison.Ordinal) < 0;
            int firstEq = xml.IndexOf("<Equipment kurz=\"Ansatztank\"", StringComparison.Ordinal), firstPuf = xml.IndexOf("<Puffer ", StringComparison.Ordinal);
            okXml &= firstEq >= 0 && firstEq < firstPuf;   // Kompat: gemeinsame Liste (Puffer 1) steht vor den Puffern fuer aeltere exe
            Package.Seen.Remove(xb);

            sb.AppendLine("UX1 Puffer je Puffer: Golden/Migration=" + Ok(okGold) + " (" + pk + " Pakete, " + st + " Puffer-Schritte; Abweichung " + bad + "/" + badMig + "/" + badXml + ") eigene Geraete+WFI-Bereich=" + Ok(okOwn)
                + " Plan=" + Ok(okPlan) + " Regel doppeltes Geraet=" + Ok(okRule) + " Standardpuffer=" + Ok(okStd) + " Produkt-Pufferpaket=" + Ok(okProd) + " XML=" + Ok(okXml));
        }

        // Vorlage mit Ansatztank (Gerät at), Vorlagetank (Gerät vt) und gemeinsamem WFI-Vorlagetank
        static PufferTemplate UxTpl(string name, string at, string vt)
        {
            PufferTemplate t = new PufferTemplate(); t.Name = name; t.Typ = "mobil";
            t.Positions.Add(UxTplPos("Ansatztank", at, false)); t.Positions.Add(UxTplPos("VT", vt, false)); t.Positions.Add(UxTplPos("WFI", "WFI-1", true));
            return t;
        }

        static PufferTemplatePos UxTplPos(string sh, string id, bool shared)
        {
            PufferTemplatePos p = new PufferTemplatePos(); p.Short = sh; p.Ids.Add(id); p.Shared = shared; return p;
        }
    }
}
#endif
