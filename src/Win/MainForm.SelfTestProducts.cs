#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Equipment je Produkt: Zugehoerigkeit (Zubehoer/Hauptgeraet), Produktprofile, Kopieren, XML, Paket-Editor-Zeile, Apply-Sperre.
    partial class MainForm
    {
        static EquipDevice TDev(string id, string cls, string parent) { EquipDevice d = new EquipDevice(); d.Id = id; d.Class = cls; d.Parent = parent; return d; }

        static void SelfTestProducts(StringBuilder sb)
        {
            // ---------- Zugehoerigkeit ----------
            EquipMaster m = new EquipMaster();
            m.Devices.Add(TDev("H1-T-0901", "Tank", "")); m.Devices.Add(TDev("H1-T-0902", "Tank", "")); m.Devices.Add(TDev("H1-F-0901-01", "Filtergehäuse", "H1-T-0901"));
            List<string> e1 = m.CheckBelongs(new[] { "H1-F-0901-01", "H1-T-0902" });
            bool okB = e1.Count == 1 && e1[0].Contains("H1-F-0901-01") && e1[0].Contains("H1-T-0901") && e1[0].Contains("H1-T-0902")
                && m.CheckBelongs(new[] { "H1-F-0901-01", "H1-T-0901" }).Count == 0 && m.CheckBelongs(new[] { "H1-F-0901-01" }).Count == 0
                && m.CheckBelongs(new[] { "h1-f-0901-01", "H1-T-0901", "H1-T-0902" }).Count == 0;

            // ---------- Produktprofil: erlaubte Geraete, fest, Austausch, Kopieren ----------
            Catalog cat = Catalog.Default();
            ProductProfile pr = new ProductProfile(); pr.Code = "1234"; pr.Name = "Testprodukt";
            ProfilePos tff = new ProfilePos(); tff.Short = "TFF"; tff.Own = true; tff.Fixed = true; tff.Ids.Add("H1-X-0102"); tff.Ids.Add("H1-X-0103"); pr.Positions.Add(tff);
            ProfilePos pu = new ProfilePos(); pu.Short = "Pumpe"; pr.Positions.Add(pu);   // Standard = global
            SwapGroup sg = new SwapGroup(); sg.Name = "TFF-Tausch"; sg.Ids.AddRange(new[] { "H1-X-0102", "H1-X-0103" }); pr.Swaps.Add(sg);
            cat.Products.Add(pr);
            bool okA = Join(cat.Allowed("1234", "TFF")) == "H1-X-0102,H1-X-0103" && Join(cat.Allowed("9999", "TFF")) == "H1-X-0101,H1-X-0102,H1-X-0103"
                && Join(cat.Allowed("", "TFF")) == Join(cat.Allowed("9999", "TFF")) && Join(cat.Allowed("1234", "Pumpe")) == Join(cat.Position("Pumpe").Ids)
                && cat.IsFixed("1234", "TFF") && !cat.IsFixed("1234", "Pumpe") && !cat.IsFixed("9999", "TFF")
                && Join(cat.SwapsFor("1234", "TFF", "H1-X-0102")) == "H1-X-0103" && cat.SwapsFor("9999", "TFF", "H1-X-0102").Count == 0;
            ProductProfile cp = cat.CopyProduct("1234", "5678", "Kopie");
            cp.Positions[0].Ids.Add("H1-X-0101");
            bool okC = cp != null && cat.Products.Count == 2 && cat.Product("5678").Name == "Kopie" && cat.Allowed("5678", "TFF").Count == 3
                && cat.Allowed("1234", "TFF").Count == 2 && cat.IsFixed("5678", "TFF") && cat.CopyProduct("0000", "1111", "") == null
                && cat.Clone().Product("5678").Positions[0].Ids.Count == 3;

            // ---------- XML: Rundreise und Katalog ohne Produkt ----------
            MemoryStore s = new MemoryStore();
            Catalog sc = s.GetCatalog(); sc.Products.Add(pr.Clone()); sc.Master.Device("H1-X-0102").Parent = "H1-T-0102"; s.SaveCatalog(sc);
            StoreData x = Backup.FromXml(Backup.ToXml(s.Snapshot()));
            ProductProfile xp = x.Catalog.Product("1234");
            bool okX = xp != null && xp.Name == "Testprodukt" && xp.Positions.Count == 2 && xp.Positions[0].Own && xp.Positions[0].Fixed && !xp.Positions[1].Own
                && Join(xp.Positions[0].Ids) == "H1-X-0102,H1-X-0103" && xp.Swaps.Count == 1 && xp.Swaps[0].Name == "TFF-Tausch" && xp.Swaps[0].Ids.Count == 2
                && x.Catalog.Master.Device("H1-X-0102").Parent == "H1-T-0102" && x.Catalog.Master.Device("H1-X-0101").Parent == "";
            StoreData old = Backup.FromXml(Encoding.UTF8.GetBytes("<Arbeitsplan version=\"2\"><Zeilen><Zeile id=\"1\" name=\"Suite 1\" min=\"3\"/></Zeilen><Katalog>"
                + "<Position name=\"TFF-Skid\" kurz=\"TFF\" abwechselnd=\"0\"><Id v=\"H1-X-0101\"/></Position><Stammdaten><Geraet id=\"H1-F-0101-01\" klasse=\"F\" notiz=\"\" aktiv=\"1\"/></Stammdaten></Katalog></Arbeitsplan>"));
            bool okOld = old.Catalog.Products.Count == 0 && old.Catalog.Positions.Count == 1 && old.Catalog.Master.Device("H1-F-0101-01").Parent == ""
                && Join(old.Catalog.Allowed("1234", "TFF")) == "H1-X-0101";
            sb.AppendLine("Produkte: Zugehörigkeit (A+B Fehler, A ok, allein ok)=" + Ok(okB) + " erlaubt global/Produkt/fest/Tausch=" + Ok(okA) + " Kopieren=" + Ok(okC)
                + " XML-Rundreise=" + Ok(okX) + " Katalog ohne Produkt=" + Ok(okOld));

            // ---------- Paket-Editor-Zeile: nur erlaubte Geraete, freie Eingabe = Hinweis, fest sperrt "abwechselnd" ----------
            EquipAssign a = new EquipAssign(); a.Short = "TFF"; a.Choices.Add(new EquipChoice("TFF", "H1-X-0101"));
            bool okD;
            using (EquipRow r = new EquipRow(a, cat, "1234", delegate { }, delegate(EquipAssign z) { }))
            using (EquipRow g = new EquipRow(a, cat, "", delegate { }, delegate(EquipAssign z) { }))
            {
                List<string> items = new List<string>(); foreach (object o in r.TestIdBox.Items) items.Add(o.ToString());
                okD = !items.Contains("H1-X-0101") && items.Contains("H1-X-0102") && items.Contains("H1-X-0103") && g.TestIdBox.Items.Contains("H1-X-0101")
                    && !r.TestAltBox.Enabled;
            }
            bool okF = !EquipRow.Known(a, cat, "1234") && Join(EquipRow.Unvalidated(a, cat, "1234")) == "H1-X-0101" && EquipRow.Known(a, cat, "")
                && EquipRow.Unvalidated(a, cat, "").Count == 0;
            List<string> warn = new List<string>();
            bool okT = cat.CheckText("Lauf 12340001\nTFF: H1-X-0101", warn).Count == 0 && warn.Count == 1 && warn[0].Contains("H1-X-0101")
                && cat.CheckText("Lauf 12340001\nTFF: H1-X-0102\nFilter: H1-F-0101-01", new List<string>()).Count == 1;
            warn.Clear(); okT &= cat.CheckText("TFF: H1-X-0101", warn).Count == 0 && warn.Count == 0;

            // ---------- Zugehoerigkeit je Lauf (abwechselnd) sperrt Apply ----------
            Package p = new Package(); p.Name = "Belongs"; p.ProductCode = "4321";
            PackageStep st = new PackageStep(); st.Title = "Lauf"; st.Row = "Suite 1"; st.Kind = StepKind.Run; st.Count = 2; st.Interval = 1; p.Steps.Add(st);
            EquipAssign alt = new EquipAssign(); alt.Short = "TFF"; alt.Alternate = true; alt.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); alt.Choices.Add(new EquipChoice("TFF", "H1-X-0102"));
            EquipAssign fil = new EquipAssign(); fil.Short = "FG"; fil.Choices.Add(new EquipChoice("FG", "H1-F-0101-01"));
            st.Equipment.Add(alt); st.Equipment.Add(fil);
            MemoryStore ps = new MemoryStore();
            List<string> pe = ps.GetCatalog().CheckPackage(p);
            bool blocked = false; int before = ps.Snapshot().Entries.Count, auditBefore = ps.Snapshot().Audit.Count;
            try { p.Apply(ps, new DateTime(2026, 11, 2), ApplyMode.Overwrite, null); } catch (InvalidOperationException) { blocked = true; }
            bool okP = pe.Count == 1 && pe[0].Contains("43210002") && blocked && ps.Snapshot().Entries.Count == before && ps.Snapshot().Audit.Count == auditBefore
                && ps.GetCatalog().CheckPackage(Package.SampleCampaign("Muster")).Count == 0;
            alt.Alternate = false; alt.Choices.RemoveAt(1);
            okP &= ps.GetCatalog().CheckPackage(p).Count == 0;
            sb.AppendLine("Produkte Editor: Dropdown nur erlaubte (fest sperrt Wechsel)=" + Ok(okD) + " freie Eingabe -> Hinweis=" + Ok(okF)
                + " Blocktext Fehler/Hinweis=" + Ok(okT) + " Zugehörigkeit je Lauf sperrt Apply=" + Ok(okP));
        }

        static string Join(List<string> l) { return string.Join(",", l.ToArray()); }
    }
}
#endif
