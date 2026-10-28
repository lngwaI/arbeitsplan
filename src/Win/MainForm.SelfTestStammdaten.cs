#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests und Testbilder der Pflegeoberflaechen: Equipment-Stammdaten (Baum, Karte, Gruppe) und Produkte & Equipment.
    partial class MainForm
    {
        static ProductProfile SampleProduct()
        {
            ProductProfile pr = new ProductProfile(); pr.Code = "1234"; pr.Name = "Testprodukt";
            ProfilePos tff = new ProfilePos(); tff.Short = "TFF"; tff.Own = true; tff.Fixed = true; tff.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); pr.Positions.Add(tff);
            SwapGroup g = new SwapGroup(); g.Name = "TFF-Skids"; g.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); pr.Swaps.Add(g);
            return pr;
        }

        // --form stammdaten (Geraetekarte), stammdaten2 (Gruppenseite), products (Produkt 1234, Position TFF)
        Form TestStammForm(string which)
        {
            Catalog cat = store.GetCatalog();
            if (which == "products")
            {
                if (cat.Product("1234") == null) cat.Products.Add(SampleProduct());
                ProductForm pf = new ProductForm(cat); pf.TestSelect("1234", "TFF"); return pf;
            }
            if (which.StartsWith("equiptable") || which.StartsWith("equipmatrix"))
            {
                EquipMaster em = cat.Master; string serr;
                EquipClass lf = new EquipClass(); lf.Name = "Luftfilter"; lf.Fields.AddRange(new[] { "Größe", "Datenblatt (Link)" }); em.Classes.Add(lf);
                List<EquipProp> pr = new List<EquipProp>(); pr.Add(new EquipProp("Größe", "10 Zoll")); pr.Add(new EquipProp("Datenblatt (Link)", "https://example.org/luftfilter-10.pdf"));
                em.AddSerial("Luftfilter", EquipMaster.SerialIds("H1-L-0201", "0206", out serr), "", pr, "Luftfilter 0201 bis 0206");
                em.Device("H1-L-0204").Active = false; em.Device("H1-L-0205").Note = "Gehäuse getauscht";
                em.Device("H1-L-0206").Props[0].Value = "5 Zoll";
                if (cat.Product("1234") == null) cat.Products.Add(SampleProduct());
                if (which.StartsWith("equiptable")) return new EquipTableForm(em, which == "equiptable" ? null : "Luftfilter");
                em.SetPair("H1-F-0101-04", "H1-X-0102", true); em.SetPairNote("H1-F-0101-04", "H1-X-0102", "nur mit Adapter");
                EquipMatrixForm mf = new EquipMatrixForm(cat, true, "Filtergehäuse", "Skid");
                if (which == "equipmatrix2") { mf.TestTab(1); }
                return mf;
            }
            EquipMasterForm f = new EquipMasterForm(cat);
            f.TestSelect(which == "stammdaten2" ? "Tanks 0103 bis 0104" : "H1-F-0101-01");
            return f;
        }

        static void Offscreen(Form f) { f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000); f.ShowInTaskbar = false; f.Show(); }

        static void SelfTestStammdaten(StringBuilder sb)
        {
            // ---------- Equipment-Stammdaten: Baum, Suche, Karte, Gruppe, Speichern ----------
            MemoryStore s = new MemoryStore();
            bool okTree, okFind, okCard, okGrp, okSave, okRt;
            using (EquipMasterForm f = new EquipMasterForm(s.GetCatalog()))
            {
                Offscreen(f);
                string t = f.TestTree();
                okTree = t.Contains("Tank\n") && t.Contains("Tanks 0103 bis 0104\n") && t.Contains("H1-T-0103") && t.Contains("↳ H1-F-0101-01 (Zubehör)");
                f.TestSearch("5 Zoll"); string tf = f.TestTree();
                okFind = tf.Contains("H1-F-0101-02") && tf.Contains("H1-F-0101-04") && !tf.Contains("H1-F-0101-01") && !tf.Contains("H1-T-0102");
                f.TestSearch("");
                f.TestSelect("H1-X-0102"); f.TestParent("H1-T-0102"); f.TestLink("H1-T-0104"); f.TestSubst("H1-X-0101", "anderer Typ", true);
                okCard = f.TestTree().Contains("↳ H1-X-0102 (Zubehör)");
                f.TestSelect("Tanks 0103 bis 0104"); f.TestGroupParent("H1-X-0101");
                okSave = f.TestCommit();
                if (okSave) s.SaveCatalog(f.Result);
                f.Close();
            }
            EquipMaster m = Backup.FromXml(Backup.ToXml(s.Snapshot())).Catalog.Master;
            okCard &= m.Device("H1-X-0102").Parent == "H1-T-0102" && m.CompatibleWith("H1-T-0104").Contains("H1-X-0102")
                && m.SubstitutesFor("H1-X-0102").Exists(delegate(EquipSubst x) { return x.Id == "H1-X-0101" && x.Note == "anderer Typ"; })
                && m.SubstitutesFor("H1-X-0101").Exists(delegate(EquipSubst x) { return x.Id == "H1-X-0102"; });
            okGrp = m.Device("H1-T-0103").Parent == "H1-X-0101" && m.Device("H1-T-0104").Parent == "H1-X-0101" && m.Device("H1-T-0102").Parent == "";
            okRt = m.Devices.Count == EquipMaster.Default().Devices.Count && m.Series.Count == 1 && m.Device("H1-F-0101-01").Parent == "H1-X-0101";
            sb.AppendLine("Stammdaten-Formular: Baum Art/Gruppe/Gerät/Zubehör=" + Ok(okTree) + " Suche=" + Ok(okFind) + " Karte Gehört zu/Passt zu/Ersatz=" + Ok(okCard)
                + " Gruppe Gehört zu für Markierte=" + Ok(okGrp) + " Speichern=" + Ok(okSave) + " Rundreise=" + Ok(okRt));

            // ---------- Produkte & Equipment: anlegen, eigene Liste, ganze Gruppe, Austausch, Kopieren, Speichern ----------
            MemoryStore ps = new MemoryStore();
            Catalog before = ps.GetCatalog();
            string own, grp; bool okP;
            using (ProductForm f = new ProductForm(ps.GetCatalog()))
            {
                Offscreen(f);
                f.TestAdd("2468", "Neuprodukt");
                f.TestSelect("2468", "TFF"); own = f.TestOwn(true, true, "H1-X-0102");
                f.TestSelect("2468", "PT"); f.TestOwn(true, false); f.TestCheckGroup(0);
                grp = string.Join(",", f.TestCatalog.Allowed("2468", "PT").ToArray());
                f.TestSwap("Tausch", "H1-X-0102", "H1-T-0103");
                bool copied = f.TestCopy("2469", "Kopie");
                okP = f.TestCommit() && copied;
                if (okP) ps.SaveCatalog(f.Result);
                f.Close();
            }
            Catalog c = Backup.FromXml(Backup.ToXml(ps.Snapshot())).Catalog;
            ProductProfile p1 = c.Product("2468"), p2 = c.Product("2469");
            bool okOwn = own == "H1-X-0102" && c.IsFixed("2468", "TFF") && Join(c.Allowed("2468", "TFF")) == "H1-X-0102" && !c.IsFixed("2468", "PT");
            bool okGroup = grp == "H1-T-0103,H1-T-0104" && Join(c.Allowed("2468", "PT")) == grp;
            bool okSw = p1 != null && p1.Swaps.Count == 1 && p1.Swaps[0].Name == "Tausch" && Join(p1.Swaps[0].Ids) == "H1-X-0102,H1-T-0103";
            bool okCp = p2 != null && p2.Name == "Kopie" && c.IsFixed("2469", "TFF") && Join(c.Allowed("2469", "PT")) == grp && p2.Swaps.Count == 1;
            bool okKeep = c.Positions.Count == before.Positions.Count && c.Bausteine.Count == before.Bausteine.Count && c.Master.Devices.Count == before.Master.Devices.Count
                && Join(c.Allowed("9999", "TFF")) == Join(before.Position("TFF").Ids);
            sb.AppendLine("Produkte-Formular: eigen+fest=" + Ok(okOwn) + " ganze Gruppe=" + Ok(okGroup) + " Austauschgruppe=" + Ok(okSw) + " Kopieren=" + Ok(okCp)
                + " Speichern=" + Ok(okP) + " Katalog erhalten=" + Ok(okKeep) + " [" + own + "|" + grp + "]");
        }
    }
}
#endif
