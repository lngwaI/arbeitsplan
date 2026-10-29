#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-1b: Modell-Operationen der Puffer-Karten (Puffer entfernen, Geltungsbereich, Zeile entfernen), Pufferpaket-Dialog des
    // Produkts und der Paket-Editor mit Karten (PackageForm.TestUx1b.cs). Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static PackageStep UxThree()
        {
            PackageStep s = UxStep("Puffer mobil", 1);
            s.Puffers.Add(UxPuffer("A", UxEq("Ansatztank", "AT-1"))); s.Puffers.Add(UxPuffer("B", UxEq("Ansatztank", "AT-2"))); s.Puffers.Add(UxPuffer("C", UxEq("Ansatztank", "AT-3")));
            s.Puffers[1].NameNo = 2; s.Puffers[2].NameNo = 3;
            s.SyncPuffers();
            return s;
        }

        void SelfTestUX1b(StringBuilder sb)
        {
            // (1) Puffer entfernen: Bits der gemeinsamen Zeilen rutschen nach, Zeile nur fuer den entfernten Puffer entfaellt, Scope 0 (alle) bleibt, letzter bleibt
            PackageStep s = UxThree();
            PufferShare only3 = new PufferShare(); only3.Eq = UxEq("WFI", "W-3"); only3.Scope = 4;
            PufferShare p23 = new PufferShare(); p23.Eq = UxEq("VT", "V-23"); p23.Scope = 6;
            PufferShare all = new PufferShare(); all.Eq = UxEq("Tween", "TW-0"); all.Scope = 0;
            s.PufferShared.AddRange(new[] { only3, p23, all });
            bool okRem = s.RemovePuffer(2) && s.Puffers.Count == 2 && !s.PufferShared.Contains(only3) && p23.Scope == 2 && all.Scope == 0 && s.Variant == "mobil:2";
            s = UxThree(); s.PufferShared.Add(p23); p23.Scope = 6;
            okRem &= s.RemovePuffer(0) && p23.Scope == 3 && s.Puffers[0].Name == "B" && s.Puffers[0].NameNo == 0 && s.Puffers[1].NameNo == 2;   // "Titel 3" rutscht auf "Titel 2"
            okRem &= s.RemovePuffer(0) && s.Puffers.Count == 1 && p23.Scope == 1 && !s.RemovePuffer(0) && s.Puffers.Count == 1 && !s.RemovePuffer(5) && !s.RemovePuffer(-1);

            // (2) Geltungsbereich: ein Puffer = eigene Zeile, mehrere = gemeinsame Zeile, 0 = ungueltig
            s = UxThree(); EquipAssign w = UxEq("WFI", "W-1"); s.Puffers[0].Lines.Add(w);
            bool okScope = s.SetScope(w, 3) && s.PufferShared.Count == 1 && s.PufferShared[0].Scope == 3 && !s.Puffers[0].Lines.Contains(w) && s.OwnerOf(s.PufferShared[0]) == 0;
            okScope &= s.SetScope(w, 6) && s.PufferShared[0].Scope == 6 && s.OwnerOf(s.PufferShared[0]) == 1 && s.PufferChoices(0, 0).Count == 1 && s.PufferChoices(2, 0).Count == 2;
            okScope &= s.SetScope(w, 4) && s.PufferShared.Count == 0 && s.Puffers[2].Lines.Contains(w) && s.SetScope(w, 1) && s.Puffers[0].Lines.Contains(w) && !s.Puffers[2].Lines.Contains(w);
            okScope &= !s.SetScope(w, 0) && !s.SetScope(UxEq("X", "1"), 1) && s.SetScope(w, 7) && s.MaskOf(s.PufferShared[0]) == 7 && s.AllEquip().Contains(w);
            all.Scope = 0; s.PufferShared.Add(all); okScope &= s.MaskOf(all) == 7;
            EquipAssign own = s.Puffers[1].Lines[0];
            okScope &= s.RemoveEquip(own) && s.Puffers[1].Lines.Count == 0 && s.RemoveEquip(w) && s.PufferShared.Count == 1 && !s.RemoveEquip(w);

            // (3) Plan-Ausgabe und XML-Rundweg nach dem Bearbeiten unveraendert
            s = UxThree(); EquipAssign w2 = UxEq("WFI", "W-9"); s.Puffers[1].Lines.Add(w2); s.SetScope(w2, 3);
            Package pk = new Package(); pk.Steps.Add(s);
            Package back = Package.FromXml(pk.ToXml());
            okScope &= UxTexts(back.Steps[0].BuffersFor(0, "")) == UxTexts(s.BuffersFor(0, "")) && UxTexts(s.BuffersFor(0, "")).Contains("W-9") && s.BuffersFor(0, "")[2].Text.IndexOf("W-9", StringComparison.Ordinal) < 0 && s.BuffersFor(0, "")[1].Text.Contains("W-9");
            Package.Seen.Remove(pk); Package.Seen.Remove(back);

            // (4) Pufferpaket-Dialog eines Produkts: Vorlage aus dem Standardpuffer, Geraet, gemeinsam, Name, Vorschau; Ergebnis -> Package.PufferFor
            Catalog cat = Catalog.Default();
            ProductProfile pr = new ProductProfile(); pr.Code = "AB1"; cat.Products.Add(pr);
            bool okDlg;
            using (PufferPackForm d = new PufferPackForm(cat, pr))
            {
                okDlg = d.TestList.Count == 0 && d.TestPreview.StartsWith("Kein Puffer");
                d.TestAdd(); okDlg &= d.TestList.Count == 1 && d.TestRows == 5 && d.TestList[0].Positions[0].Short == "Ansatztank";
                d.TestName("Lösung A"); d.TestSetDevice(0, "AT-1"); d.TestSetDevice(1, "VT-1"); d.TestShared(2, true);
                okDlg &= d.TestList[0].Name == "Lösung A" && d.TestList[0].Positions[0].Ids[0] == "AT-1" && d.TestList[0].Positions[2].Shared && d.TestPreview.Contains("Puffer 1: Lösung A (mobil)") && d.TestPreview.Contains("Ansatztank: AT-1");
                d.TestAdd(); okDlg &= d.TestList.Count == 2 && d.TestPreview.Contains("Puffer 2:");
                okDlg &= d.TestCommit() && d.Result.Count == 2;
                pr.Puffer = d.Result;
            }
            List<PufferTemplate> got = Package.PufferFor(cat, "AB1");
            okDlg &= got.Count == 2 && got[0].Name == "Lösung A" && got[0].Positions[1].Ids[0] == "VT-1" && Package.PufferFor(cat, "ZZ").Count == 1;   // ohne Pufferpaket: Standardpuffer
            using (PufferPackForm d2 = new PufferPackForm(cat, pr)) okDlg &= d2.TestList.Count == 2 && d2.TestPreview.Contains("Puffer 2:");   // erneut geoeffnet: gleicher Stand
            using (ProductForm pf = new ProductForm(cat)) { pf.TestSelect("AB1", "VT"); okDlg &= pf.TestPufButton == "Pufferpaket (2)…|True"; }
            sb.AppendLine("UX1b Modell: Puffer entfernen " + Ok(okRem) + " Geltungsbereich/Zeilen " + Ok(okScope) + " Pufferpaket-Dialog " + Ok(okDlg));
            sb.AppendLine(PackageForm.TestUx1b(store, equip, start));   // Karten im Paket-Editor (PackageForm.TestUx1b.cs)
        }
    }
}
#endif
