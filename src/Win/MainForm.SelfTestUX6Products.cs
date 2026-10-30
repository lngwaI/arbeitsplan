#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-6: "Produkte & Equipment" (ProductForm). Erfundene Daten, nur im Speicher (Katalog-Klon), nichts wird gespeichert.
    // PRD-1: eigene feste Liste an TFF, Austauschgruppe "TFF-Skids", FZ1 eigene Liste (FZ2/FZ-E Standard = Positionen der Gruppe weichen ab).
    partial class MainForm
    {
        static Catalog Ux6ProdCat()
        {
            Catalog c = Catalog.Default();
            foreach (EquipGroup g in c.Groups) if (g.Name == "Freezetanks") g.Shorts.Add("FZ-E");   // drei Rollen in einer Gruppe
            ProductProfile p1 = new ProductProfile(); p1.Code = "PRD-1"; p1.Name = "Testprodukt";
            ProfilePos tff = new ProfilePos(); tff.Short = "TFF"; tff.Own = true; tff.Fixed = true; tff.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); p1.Positions.Add(tff);
            ProfilePos fz1 = new ProfilePos(); fz1.Short = "FZ1"; fz1.Own = true; fz1.Ids.Add("H2-T-0251"); p1.Positions.Add(fz1);
            SwapGroup sg = new SwapGroup(); sg.Name = "TFF-Skids"; sg.Ids.AddRange(new[] { "H1-X-0101", "H1-X-0102" }); p1.Swaps.Add(sg);
            p1.Puffer.Add(Ux6Tpl("", "Lösung A", "mobil", "Ansatztank", ""));
            ProductProfile p2 = new ProductProfile(); p2.Code = "PRD-2"; p2.Name = "Zweitprodukt";
            c.Products.Add(p1); c.Products.Add(p2);
            return c;
        }

        static ProductForm Ux6ProdForm(string which)
        {
            ProductForm f = new ProductForm(Ux6ProdCat(), new List<string>(new[] { "Suite 1", "Suite 2" }));
            if (which == "ux6productsswap0") { f.TestSelect("PRD-2", "TFF"); f.TestShowSwaps(); }
            else if (which == "ux6productsgrp") { f.TestSelect("PRD-1", "TFF"); f.TestSelectGroup("Freezetanks"); }
            else { f.TestSelect("PRD-1", "TFF"); if (which == "ux6productsswap") f.TestShowSwaps(); }
            return f;
        }

        partial void Ux6FormProducts(string which, ref System.Windows.Forms.Form f)
        {
            if (which == "ux6products" || which == "ux6productsswap" || which == "ux6productsswap0" || which == "ux6productsgrp") f = Ux6ProdForm(which);
        }

        partial void SelfTestUX6Products(StringBuilder sb)
        {
            bool okCnt, okEdit, okSwap, okGrp, okStd, okSave;
            string detail = "";
            using (ProductForm f = Ux6ProdForm("ux6products"))
            {
                Offscreen(f);
                okCnt = f.TestChanges == 0 && f.TestStatus == "Keine Änderungen" && !f.TestNeedsAsk && f.TestHead == "PRD-1 · Testprodukt" && f.TestProductRow(0) == "PRD-1|Testprodukt|1" && f.TestProductRow(1) == "PRD-2|Zweitprodukt|–";

                // Bearbeiten: Code/Name mit Prüfung, Hinweis zum alten Code, Zähler
                okEdit = !f.TestEdit("A", "x") && !f.TestEdit("PRD-2", "x") && !f.TestEdit("", "x") && f.TestChanges == 0;
                okEdit &= f.TestEdit("PRD-9", "Neu benannt") && f.TestCatalog.Product("PRD-9") != null && f.TestCatalog.Product("PRD-1") == null && f.TestHead == "PRD-9 · Neu benannt"
                    && f.TestStatus.Contains("alten Code") && f.TestProductRow(0) == "PRD-9|Neu benannt|1" && f.TestChanges == 1 && f.TestStatus.EndsWith("Nicht gespeichert: 1 Änderung") && f.TestNeedsAsk;

                // Austauschgruppen per Häkchen: Ids in Listenreihenfolge, Zähler, Reitertitel, Leergruppe fällt beim Speichern weg
                f.TestSelect("PRD-9", "TFF");
                f.TestSwap("Tausch", "H1-X-0102", "H1-X-0101");
                okSwap = f.TestSwapIds == "H1-X-0101,H1-X-0102" && f.TestSwapCount == "2 Geräte angehakt" && f.TestSwapTab == "Austauschgruppen (2)" && f.TestSwapList.Contains("Tausch (2 Geräte)");
                f.TestSwap("Eins", "H1-X-0101");
                okSwap &= f.TestSwapCount.Contains("mindestens 2 Geräte anhaken") && f.TestSwapTab == "Austauschgruppen (3)" && f.TestSwapList.Contains("Eins (1 Gerät)");
                f.TestSwap("Leer");
                okSwap &= f.TestSwapCount == "mindestens 2 Geräte anhaken" && f.TestSwapTab == "Austauschgruppen (4)" && !f.TestSwapEmptyShown;
                detail += "[" + f.TestSwapIds + "|" + f.TestSwapCount + "|" + f.TestSwapTab + "]";

                // Gruppeneintrag Freezetanks: eigene Liste in alle Positionen der Gruppe, Vereinheitlichen
                f.TestShowPos(); f.TestSelectGroup("Freezetanks");
                string items = f.TestPosList;
                okGrp = items.Contains("▸ Freezetanks (FZ1, FZ2, FZ-E) – gemeinsame Liste") && items.Contains("Freezetank 1 (FZ1)") && items.Contains("Freezetank (Ersatz) (FZ-E)")
                    && f.TestUnifyVisible && f.TestPosInfo.Contains("unterschiedliche Listen");
                f.TestUnify();
                okGrp &= !f.TestUnifyVisible && Join(f.TestCatalog.Allowed("PRD-9", "FZ2")) == "H2-T-0251" && Join(f.TestCatalog.Allowed("PRD-9", "FZ-E")) == "H2-T-0251";
                string own = f.TestOwn(true, false, "H2-T-0251", "H2-T-0252");
                okGrp &= own == "H2-T-0251,H2-T-0252" && Join(f.TestCatalog.Allowed("PRD-9", "FZ2")) == own && Join(f.TestCatalog.Allowed("PRD-9", "FZ-E")) == own && !f.TestUnifyVisible;
                f.TestOwn(false, false);
                okGrp &= f.TestCatalog.Product("PRD-9").Pos("FZ1") == null && f.TestCatalog.Product("PRD-9").Pos("FZ2") == null && Join(f.TestCatalog.Allowed("PRD-9", "FZ2")) == "H2-T-0252";
                detail += "[" + own + "]";

                // Speichern: Gruppe mit 0 Geräten entfällt
                okSave = f.TestCommit() && f.Result.Product("PRD-9").Swaps.Count == 3 && f.Result.Product("PRD-9").Swaps.TrueForAll(delegate(SwapGroup g) { return g.Ids.Count > 0; });

                // Übernahme aus "Standardliste bearbeiten…": Standard-Teile ersetzt, Produkte bleiben
                Catalog mod = f.TestCatalog.Clone(); EquipPosition np = new EquipPosition(); np.Name = "Neue Position"; np.Short = "NEU"; np.Ids.Add("H1-T-0102");
                mod.Positions.Add(np); mod.Groups.Clear(); mod.Products.Clear();
                int n0 = f.TestCatalog.Products.Count;
                f.TestApplyStandard(mod);
                okStd = f.TestCatalog.Position("NEU") != null && f.TestCatalog.Products.Count == n0 && f.TestCatalog.Product("PRD-9") != null && f.TestCatalog.Product("PRD-9").Swaps.Count == 3
                    && f.TestPosList.Contains("Neue Position (NEU)") && !f.TestPosList.Contains("gemeinsame Liste") && f.TestStatus.Contains("Standardliste geändert – wird mit „Speichern“ gesichert.");
                f.TestNoAsk(); f.Close();
            }
            sb.AppendLine("UX6 Produkte: Zähler/Liste=" + Ok(okCnt) + " Bearbeiten=" + Ok(okEdit) + " Austauschgruppen=" + Ok(okSwap) + " Gruppeneintrag=" + Ok(okGrp)
                + " Speichern=" + Ok(okSave) + " Standardliste=" + Ok(okStd) + " " + detail);
        }
    }
}
#endif
