#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-6 (Puffer-/Produkt-Dialoge, docs/UX-Puffer-Produkte.md). Core hier; die Dialog-Teile haengen sich als partielle Methoden
    // in eigenen Dateien ein (fehlt eine, entfaellt der Aufruf). Schnelllauf: AP_ONLY=ux6. Beschriftungen nie "Fehler" nennen.
    partial class MainForm
    {
        partial void SelfTestUX6Editor(StringBuilder sb);    // "Pufferpaket übernehmen", "+ Puffer ▾" (Paket-Editor)
        partial void SelfTestUX6Pack(StringBuilder sb);      // Produktwahl, Pufferpaket-Dialog
        partial void SelfTestUX6Products(StringBuilder sb);  // Produkte & Equipment

        // Testbilder "--form ux6…": jede Dialog-Datei setzt f, wenn sie den Namen kennt (z. B. ux6pick, ux6add, ux6prodpick, ux6pack, ux6products)
        partial void Ux6FormEditor(string which, ref System.Windows.Forms.Form f);
        partial void Ux6FormPack(string which, ref System.Windows.Forms.Form f);
        partial void Ux6FormProducts(string which, ref System.Windows.Forms.Form f);

        void SelfTestUX6(StringBuilder sb)
        {
            bool okName = Package.PlanBufferName("PRD-1", "Puffer-1") == "PRD-1 - Puffer-1" && Package.PlanBufferName("PRD-1", "PRD-1 - Puffer-1") == "PRD-1 - Puffer-1"
                && Package.PlanBufferName("", "Puffer-1") == "Puffer-1" && Package.PlanBufferName("PRD-1", "") == "";
            okName &= Package.PlanBufferName("PRD-1", "PRD-1 Lösung A") == "PRD-1 Lösung A" && Package.PlanBufferName("PRD-1", "PRD-1: Lösung A") == "PRD-1: Lösung A"   // alte Namen: kein doppelter Präfix
                && Package.PlanBufferName("PRD-1", "prd-1 - x") == "prd-1 - x" && Package.PlanBufferName("PRD-1", "PRD-1") == "PRD-1" && Package.PlanBufferName("PRD-1", "PRD-10 x") == "PRD-1 - PRD-10 x"
                && Package.PlanBufferName("PRD-1", "Lösung PRD-1") == "PRD-1 - Lösung PRD-1";
            PufferTemplate t = new PufferTemplate(); t.Name = "Puffer-1"; t.Art = "mobiler Puffer 1";
            bool okArt = t.Label == "mobiler Puffer 1" && t.Clone().Art == "mobiler Puffer 1";
            t.Art = ""; okArt &= t.Label == "Puffer-1";
            Catalog c = new Catalog(); ProductProfile pr = new ProductProfile(); pr.Code = "PRD-1"; c.Products.Add(pr);
            PufferTemplate a = new PufferTemplate(); a.Name = "Puffer-1"; a.Art = "mobiler Puffer 1"; pr.Puffer.Add(a);
            PufferTemplate b = new PufferTemplate(); b.Name = "Puffer-2"; pr.Puffer.Add(b);
            MemoryStore cs = new MemoryStore(); cs.SaveCatalog(c);
            Catalog back = Backup.FromXml(Backup.ToXml(cs.Snapshot())).Catalog;
            ProductProfile bp = back != null ? back.Product("PRD-1") : null;
            okArt &= bp != null && bp.Puffer.Count == 2 && bp.Puffer[0].Art == "mobiler Puffer 1" && bp.Puffer[1].Art == "" && bp.Puffer[1].Label == "Puffer-2";
            sb.AppendLine("UX6 Core: Name im Plan mit Produktcode " + Ok(okName) + " Bezeichnung/XML " + Ok(okArt));
            SelfTestUX6Editor(sb);
            SelfTestUX6Pack(sb);
            SelfTestUX6Products(sb);
        }
    }
}
#endif
