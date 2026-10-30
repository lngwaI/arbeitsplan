#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5a: Menue Einstellungen -> "Pufferpakete…", Produktauswahl und Pufferpaket-Dialog (ansehen/aendern); die Tests am
    // Paket-Editor (Packen-Nr. im Kopf, Mehr Optionen, Kennung, Knopf) stehen in PackageForm.TestUx5a.cs. Beschriftungen nie "Fehler" nennen.
    partial class MainForm
    {
        void SelfTestUX5a(StringBuilder sb)
        {
            // Menue: Eintrag unter Einstellungen vorhanden (fuer alle, die das Menue sehen: ohne Recht nur ansehen)
            bool okMenu = false;
            foreach (Control c in Controls)
            {
                MenuStrip ms = c as MenuStrip; if (ms == null) continue;
                foreach (ToolStripItem top in ms.Items)
                {
                    ToolStripMenuItem mi = top as ToolStripMenuItem;
                    if (mi == null || mi.Text != "Einstellungen") continue;
                    foreach (ToolStripItem it in mi.DropDownItems) if (it.Text == "Pufferpakete…") okMenu = it.Enabled;
                }
            }

            // Produktauswahl: Produkte mit Anzahl Puffer, Vorauswahl = Produkt des Editors; unbekanntes Produkt nur fuer den Admin als "neu"
            Catalog cat = Catalog.Default();
            ProductProfile a = new ProductProfile(); a.Code = "AB1"; a.Name = "Testprodukt";
            PufferTemplate t = new PufferTemplate(); t.Name = "Lösung A"; t.Typ = "mobil"; t.Positions.Add(new PufferTemplatePos()); t.Positions[0].Short = "Ansatztank"; a.Puffer.Add(t);
            ProductProfile b = new ProductProfile(); b.Code = "CD2";
            cat.Products.Add(a); cat.Products.Add(b);
            bool okPick;
            using (ProductPickForm d = new ProductPickForm(cat, "CD2", true))
                okPick = d.TestCount == 2 && d.TestSelected == "CD2" && d.TestText(0).Contains("1 Puffer") && d.TestText(1).Contains("kein Pufferpaket");
            using (ProductPickForm d = new ProductPickForm(cat, "ZZ9", true)) okPick &= d.TestCount == 3 && d.TestText(0).Contains("neu") && d.TestSelected == "ZZ9";
            using (ProductPickForm d = new ProductPickForm(cat, "ZZ9", false)) okPick &= d.TestCount == 2 && d.TestSelected == "AB1";   // ohne Recht: kein neues Produkt

            // Pufferpaket-Dialog nur ansehen: Liste und Vorschau bleiben, Titel zeigt es
            bool okView;
            using (PufferPackForm v = new PufferPackForm(cat, a, true)) okView = v.Text.Contains("nur ansehen") && v.TestList.Count == 1 && v.TestPreview.Contains("Puffer 1");
            using (PufferPackForm v = new PufferPackForm(cat, a, false)) okView &= !v.Text.Contains("nur ansehen");
            sb.AppendLine("UX5a Pufferpakete: Menüeintrag " + Ok(okMenu) + " Produktauswahl " + Ok(okPick) + " ansehen/ändern " + Ok(okView));

            sb.Append(PackageForm.TestUx5a(store, equip, start));   // Editor: Packen-Nr. im Kopf, Mehr Optionen, Kennung, Knopf (PackageForm.TestUx5a.cs)
        }
    }
}
#endif
