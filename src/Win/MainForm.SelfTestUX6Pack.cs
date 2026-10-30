#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-6: Produktwahl der Pufferpakete (ProductPickForm, ProductAsk, PufferPackAdmin.Merged) und Pufferpaket-Dialog (PufferPackForm).
    // Erfundene Daten: Produkt PRD-1 darf an "Ansatztank" nur H1-T-0103; H1-T-0102 (Positionsliste) und H1-T-0104 (gleiche Geräteart) sind bekannt, aber
    // nicht freigegeben; H1-T-0199 ist unbekannt.
    partial class MainForm
    {
        static PufferTemplate Ux6Tpl(string art, string name, string typ, params string[] shortAndId)
        {
            PufferTemplate t = new PufferTemplate(); t.Art = art; t.Name = name; t.Typ = typ;
            for (int i = 0; i + 1 < shortAndId.Length; i += 2)
            {
                PufferTemplatePos p = new PufferTemplatePos(); p.Short = shortAndId[i];
                if (shortAndId[i + 1].Length > 0) p.Ids.Add(shortAndId[i + 1]);
                t.Positions.Add(p);
            }
            return t;
        }

        static Catalog Ux6PackCat()
        {
            Catalog c = Catalog.Default();
            EquipPosition at = c.Position("Ansatztank"); at.Ids.Clear(); at.Ids.Add("H1-T-0102");
            ProductProfile p1 = new ProductProfile(); p1.Code = "PRD-1"; p1.Name = "Testprodukt";
            ProfilePos pp = new ProfilePos(); pp.Short = "Ansatztank"; pp.Own = true; pp.Ids.Add("H1-T-0103"); p1.Positions.Add(pp);
            p1.Puffer.Add(Ux6Tpl("mobiler Puffer 1", "Lösung A", "mobil", "Ansatztank", "H1-T-0103", "VT", "H1-T-0104", "WFI", ""));
            p1.Puffer.Add(Ux6Tpl("", "Lösung B", "stationaer", "Ansatztank", "H1-T-0199"));
            p1.Puffer.Add(Ux6Tpl("stationärer Puffer", "", "stationaer", "VT", ""));
            ProductProfile p2 = new ProductProfile(); p2.Code = "PRD-2"; p2.Name = "Zweitprodukt"; p2.Puffer.Add(Ux6Tpl("Fremd", "Fremd-Lösung", "mobil", "Ansatztank", "H1-T-0102"));
            ProductProfile p3 = new ProductProfile(); p3.Code = "PRD-10";
            c.Products.Add(p1); c.Products.Add(p2); c.Products.Add(p3);
            return c;
        }

        partial void Ux6FormPack(string which, ref Form f)
        {
            Catalog c = Ux6PackCat();
            if (which == "ux6prodpick") f = new ProductPickForm(c, "PRD-1", true);
            else if (which == "ux6prodpick0") f = new ProductPickForm(new Catalog(), "", true);
            else if (which == "ux6prodpick0v") f = new ProductPickForm(new Catalog(), "", false);
            else if (which == "ux6pack") f = new PufferPackForm(c, c.Product("PRD-1"));
            else if (which == "ux6pack2") { PufferPackForm p = new PufferPackForm(c, c.Product("PRD-1")); p.TestSelect(1); f = p; }   // Vorlage mit unbekanntem Gerät (orange, Status)
        }

        partial void SelfTestUX6Pack(StringBuilder sb)
        {
            // (1) Produktwahl: Liste mit Puffer-Zahl, neues Produkt, Leerzustand, Code-Regel
            Catalog c = Ux6PackCat();
            bool okPick;
            using (ProductPickForm d = new ProductPickForm(c, "PRD-2", true))
            {
                okPick = d.TestCount == 3 && d.TestSelected == "PRD-2" && d.TestText(0).Contains("3 Puffer") && d.TestText(1).Contains("1 Puffer") && d.TestText(2).Contains("– Standardpuffer gilt");
                okPick &= d.TestNew("PRD-3", "Neu") && d.TestCount == 4 && d.TestSelected == "PRD-3" && d.NewProducts.Count == 1 && d.TestText(3).Contains("Standardpuffer");
            }
            using (ProductPickForm d = new ProductPickForm(new Catalog(), "", true))
            {
                okPick &= d.TestCount == 0 && d.TestEmptyText == "Noch keine Produkte angelegt." && d.TestAccept != null && d.TestAccept.Text == "Neues Produkt…" && !d.TestOkVisible && d.Result == null;
                okPick &= d.TestNew("PRD-1", "") && d.TestCount == 1 && d.TestEmptyText == "" && d.TestOkVisible && d.TestAccept.Text.StartsWith("Pufferpaket");
            }
            using (ProductPickForm d = new ProductPickForm(new Catalog(), "", false)) okPick &= d.TestEmptyText.Contains("Der Admin legt Produkte an.") && d.TestAccept == null && !d.TestOkVisible;
            okPick &= ProductAsk.Check(c, "A", null).Length > 0 && ProductAsk.Check(c, "PRD-1", null).Contains("gibt es schon") && ProductAsk.Check(c, "PRD-4", null) == "" && ProductAsk.Check(c, "PRD-1", "PRD-1") == "";

            // (2) Speichern mit Merged: neues Produkt (auch bei leerem Pufferpaket), aufgenommenes Gerät, Änderungen anderer Admins bleiben
            MemoryStore cs = new MemoryStore(); cs.SaveCatalog(Ux6PackCat());
            Catalog mine = cs.GetCatalog();   // Dialog arbeitet auf einer Kopie
            ProductProfile np = new ProductProfile(); np.Code = "PRD-3"; np.Name = "Neu"; mine.Products.Add(np);
            DevAdd da = new DevAdd(); da.D = new EquipDevice(); da.D.Id = "H1-T-0199"; da.D.Class = "Tank"; da.Short = "Ansatztank"; da.ApplyTo(mine);
            Catalog other = cs.GetCatalog(); other.Product("PRD-2").Name = "Geändert"; other.Product("PRD-10").Puffer.Add(Ux6Tpl("Y", "Y", "mobil", "VT", "")); cs.SaveCatalog(other);   // anderer Admin
            Catalog m1 = PufferPackAdmin.Merged(cs, np, new List<ProductProfile>(new[] { np }), new List<DevAdd>(new[] { da }));
            bool okMerge = m1.Product("PRD-3") != null && m1.Product("PRD-3").Name == "Neu" && m1.Product("PRD-3").Puffer.Count == 0 && m1.Product("PRD-2").Name == "Geändert" && m1.Product("PRD-10").Puffer.Count == 1
                && m1.Master.Device("H1-T-0199") != null && m1.Position("Ansatztank").Ids.Contains("H1-T-0199");
            Catalog m2 = PufferPackAdmin.Merged(cs, null, new List<ProductProfile>(new[] { np }), null);   // Abbruch im Pufferpaket-Dialog: Produkt bleibt trotzdem
            okMerge &= m2.Product("PRD-3") != null && m2.Product("PRD-1").Puffer.Count == 3;
            ProductProfile again = new ProductProfile(); again.Code = "PRD-2"; again.Name = "andere Anlage";
            okMerge &= PufferPackAdmin.Merged(cs, np, new List<ProductProfile>(new[] { again }), null).Product("PRD-2").Name == "Geändert";   // vorhandenes Produkt gleichen Codes bleibt

            // (3) Pufferpaket-Dialog
            Catalog pc = Ux6PackCat(); ProductProfile prod = pc.Product("PRD-1");
            bool okList, okOrder, okName, okDev, okFrom;
            using (PufferPackForm d = new PufferPackForm(pc, prod))
            {
                okList = d.TestList.Count == 3 && d.TestItemText(0) == "mobiler Puffer 1 · mobil · 3 Positionen" && d.TestItemText(1) == "Lösung B · stationär · 1 Position" && d.TestItemText(2) == "stationärer Puffer · stationär · 1 Position";
                okList &= d.TestTplMenu().Contains("Kopie der markierten") && d.TestTplMenu()[0].StartsWith("Leer") && d.TestTplMenu().Count == 3;
                // Art und Name getrennt: Liste zeigt die Bezeichnung, der Plan "PRD-1 - Name"
                okName = d.TestPreview.Contains("Puffer 1: PRD-1 - Lösung A (mobil)") && !d.TestPreview.Contains("mobiler Puffer 1") && d.TestPreview.Contains("Puffer 3: PRD-1 - ");
                d.TestArt("Neue Bezeichnung");
                okName &= d.TestItemText(0).StartsWith("Neue Bezeichnung · mobil") && d.TestList[0].Name == "Lösung A" && d.TestList[0].Art == "Neue Bezeichnung" && d.TestPreview.Contains("PRD-1 - Lösung A");
                d.TestName("Lösung A2"); okName &= d.TestList[0].Art == "Neue Bezeichnung" && d.TestPreview.Contains("PRD-1 - Lösung A2");

                // Reihenfolge der Zeilen = Reihenfolge im Plantext (▲▼, Alt+↑/↓)
                d.TestSelect(0);
                okOrder = d.TestRows == 3 && d.TestShort(0) == "Ansatztank" && d.TestPreview.IndexOf("H1-T-0103", StringComparison.Ordinal) < d.TestPreview.IndexOf("H1-T-0104", StringComparison.Ordinal);
                d.TestMove(0, 1);
                okOrder &= d.TestShort(1) == "Ansatztank" && d.TestList[0].Positions[1].Short == "Ansatztank" && d.TestPreview.IndexOf("H1-T-0104", StringComparison.Ordinal) < d.TestPreview.IndexOf("H1-T-0103", StringComparison.Ordinal);
                d.TestKey(1, Keys.Alt | Keys.Up);
                okOrder &= d.TestShort(0) == "Ansatztank" && d.TestPreview.IndexOf("H1-T-0103", StringComparison.Ordinal) < d.TestPreview.IndexOf("H1-T-0104", StringComparison.Ordinal);
                d.TestMove(0, -1); okOrder &= d.TestShort(0) == "Ansatztank";   // am Rand: keine Änderung

                // Geräte: freigegeben zuerst, übrige bekannte mit Zusatz; nicht freigegeben/unbekannt orange + Status
                List<string> opt = d.TestOptions(0);
                okDev = opt.Count == 4 && opt[0] == "" && opt[1] == "H1-T-0103" && opt.Contains("H1-T-0102*") && opt.Contains("H1-T-0104*") && !d.TestOrange(0) && d.TestStatus == "|";
                d.TestSetDevice(0, "H1-T-0102");
                okDev &= d.TestOrange(0) && d.TestTip(0).Contains("nicht freigegeben") && d.TestStatus == "|1 Gerät nicht für PRD-1 freigegeben: H1-T-0102" && d.TestList[0].Positions[0].Ids[0] == "H1-T-0102";
                d.TestSetDevice(0, "H1-T-0199");
                okDev &= d.TestOrange(0) && d.TestTip(0).Contains("nicht in der Equipment-Liste") && d.TestStatus.StartsWith("1 Gerät nicht in der Equipment-Liste: H1-T-0199") && d.TestLinkVisible;
                EquipDevice nd = new EquipDevice(); nd.Id = "H1-T-0199"; nd.Class = "Tank";
                d.AddDevice(nd, "Ansatztank");   // wie "Zur Liste hinzufügen…": Katalog + Position, danach nicht mehr unbekannt
                okDev &= pc.Master.Device("H1-T-0199") != null && pc.Position("Ansatztank").Ids.Contains("H1-T-0199") && d.Added.Count == 1 && !d.TestLinkVisible && d.TestStatus.Contains("freigegeben: H1-T-0199");
                d.TestSetDevice(0, "H1-T-0103"); okDev &= !d.TestOrange(0) && d.TestStatus == "|";

                // Vorlagen: Kopie, "Aus Produkt" (unabhängig), Positionsmenü
                int n = d.TestList.Count; d.TestCopy();
                okFrom = d.TestList.Count == n + 1 && d.TestList[1].Art == "Neue Bezeichnung (Kopie)" && d.TestList[1] != d.TestList[0];
                PufferTemplate src = pc.Product("PRD-2").Puffer[0];
                d.TestFromProduct(src);
                PufferTemplate got = d.TestList[d.TestList.Count - 1];
                got.Positions[0].Note = "geändert"; got.Positions[0].Ids.Add("X");
                okFrom &= got != src && src.Positions[0].Note == "" && src.Positions[0].Ids.Count == 1 && got.Label == "Fremd";
                int rows0 = d.TestRows; d.TestAddPos("Tween"); okFrom &= d.TestRows == rows0 + 1 && d.TestShort(rows0) == "Tween" && d.TestPosMenu().Contains("Weitere Positionen");
                okFrom &= d.TestCommit() && d.Result.Count == d.TestList.Count;
            }
            using (PufferPackForm v = new PufferPackForm(pc, prod, true)) okFrom &= v.Text.Contains("nur ansehen") && !v.TestLinkVisible;
            // Merged mit aufgenommenem Gerät: anderer Admin ändert parallel einen anderen Teil des Katalogs
            Catalog m3 = PufferPackAdmin.Merged(cs, null, null, new List<DevAdd>(new[] { da }));
            okMerge &= m3.Product("PRD-2").Name == "Geändert" && m3.Position("Ansatztank").Ids.Contains("H1-T-0199");
            sb.AppendLine("UX6 Pufferpakete: Produktwahl " + Ok(okPick) + " Speichern/Merged " + Ok(okMerge) + " Vorlagenliste " + Ok(okList) + " Bezeichnung/Name " + Ok(okName)
                + " Reihenfolge " + Ok(okOrder) + " Geräte/Status " + Ok(okDev) + " Kopie/Aus Produkt " + Ok(okFrom));
        }
    }
}
#endif
