#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-1b am sichtbaren Paket-Editor: Karten je Puffer, eigene Geraete, WFI-Geltungsbereich, Puffer entfernen, "Puffer fuer Produkt",
    // nicht validiertes Geraet, doppeltes Geraet (rote Karte), Produktpassung. Testbilder: --form package11 / package12.
    partial class PackageForm
    {
        static EquipAssign UxA(string sh, string id)
        {
            EquipAssign a = new EquipAssign(); a.Short = sh; if (id.Length > 0) a.Choices.Add(new EquipChoice(sh, id)); return a;
        }

        static PufferSpec UxP(string name, string typ, params EquipAssign[] lines)
        {
            PufferSpec p = new PufferSpec(); p.Name = name; p.Typ = typ; p.Lines.AddRange(lines); return p;
        }

        // Geraetelisten fuer das Testbild/den Test (Ansatztank, Vorlagetank, Tween haben im Standardkatalog keine Nummern)
        public static void Ux1bPrepare(IDataStore store)
        {
            Catalog c = store.GetCatalog();
            c.AddStandardBausteine();
            string[][] ids = { new[] { "Ansatztank", "AT-1", "AT-2", "AT-3" }, new[] { "VT", "VT-1", "VT-2" }, new[] { "Tween", "TW-1" } };
            foreach (string[] row in ids)
            {
                EquipPosition p = c.Position(row[0]);
                for (int i = 1; i < row.Length; i++) if (p != null && !p.Ids.Contains(row[i])) p.Ids.Add(row[i]);
            }
            try { store.SaveCatalog(c); }
            catch (System.IO.IOException) { }   // --nosave (simulierter System-PC): Testbild ohne gespeicherten Katalog statt unbehandelter Ausnahme
        }

        // Kampagne + Puffer-Schritt mit zwei Puffern (Loesung A: Ansatztank, Vorlagetank, Tween; Loesung B: Ansatztank, Vorlagetank) und gemeinsamem WFI-Tank
        public static Package Ux1bSample(string name, bool duplicate)
        {
            Package p = Package.SampleCampaign(name);
            PackageStep pu = new PackageStep(); pu.Title = "Puffer mobil"; pu.Source = "Puffer mobil"; pu.Row = "Puffer"; pu.Day = 2;
            pu.Puffers.Add(UxP("Lösung A", "mobil", UxA("Ansatztank", "AT-1"), UxA("VT", "VT-1"), UxA("Tween", "TW-1")));
            pu.Puffers.Add(UxP("Lösung B", "mobil", UxA("Ansatztank", duplicate ? "AT-1" : "AT-2"), UxA("VT", "VT-2")));
            PufferShare w = new PufferShare(); w.Eq = UxA("WFI", "H1-T-0120"); w.Scope = 3; pu.PufferShared.Add(w);
            pu.SyncPuffers(); p.Steps.Add(pu);
            return p;
        }

        // Testbild --form pufferpack: Pufferpaket-Dialog eines Produkts mit zwei Puffern
        public static Form Ux1bPackForm(IDataStore store)
        {
            Ux1bPrepare(store);
            ProductProfile pr = new ProductProfile(); pr.Code = "AB1"; pr.Name = "Testprodukt";
            pr.Puffer.Add(Tpl("Lösung A", "mobil", Pos("Ansatztank", "AT-1", false), Pos("VT", "VT-1", false), Pos("Tween", "TW-1", false), Pos("WFI", "H1-T-0120", true)));
            pr.Puffer.Add(Tpl("Lösung B", "mobil", Pos("Ansatztank", "AT-2", false), Pos("VT", "VT-2", false), Pos("WFI", "H1-T-0120", true)));
            return new PufferPackForm(store.GetCatalog(), pr);
        }

        internal void TestOpenCard(int k) { if (k < cards.Count && !cards[k].Open) TogglePuffer(k); }

        PufferCard Card(int k) { return cards[k]; }

        public static string TestUx1b(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux1bSample("UX1b", false);
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                f.TestSelectBuffer(); Application.DoEvents();
                PackageStep ps = f.cur;
                // (1) Eine Karte je Puffer, eigene Geraete je Karte, zweite Karte kompakt, gemeinsame Zeile in der ersten
                bool okCards = ps != null && f.cards.Count == 2 && f.gPuf.Visible && !f.gEquip.Visible
                    && f.Card(0).TestRows.Count == 4 && f.Card(1).TestRows.Count == 2 && f.Card(1).TestInfoRows == 1 && f.Card(0).Open && !f.Card(1).Open;
                bool okOwn = f.Card(0).TestRows[0].TestIdBox.Text == "AT-1" && f.Card(1).TestRows[0].TestIdBox.Text == "AT-2" && f.Card(0).TestRows[1].TestIdBox.Text == "VT-1" && f.Card(1).TestRows[1].TestIdBox.Text == "VT-2";
                okCards &= f.Card(1).TestSummary == "Puffer 2: Lösung B · mobil · Ansatztank AT-2 · Vorlagetank VT-2 · WFI-Vorlagetank H1-T-0120";
                int h1 = f.Card(1).Height; f.TogglePuffer(1); Application.DoEvents();
                okCards &= f.Card(1).Open && f.Card(1).Height > h1 && f.cards.Count == 2;   // aufklappen aendert nichts am Modell
                f.TogglePuffer(1); Application.DoEvents();
                okCards &= !f.Card(1).Open && f.Card(1).Height == h1;

                // (2) WFI-Geltungsbereich: beide / nur Puffer 1 / nur Puffer 2
                EquipAssign wfi = ps.PufferShared[0].Eq;
                okOwn &= f.Card(0).TestScopeOf(wfi) == "gilt auch für Puffer 2";
                f.Card(0).SetScope(wfi, 1); Application.DoEvents();
                bool okScope = ps.PufferShared.Count == 0 && ps.Puffers[0].Lines.Contains(wfi) && ps.PufferChoices(0, 0).Exists(delegate(EquipChoice x) { return x.Short == "WFI"; })
                    && !ps.PufferChoices(1, 0).Exists(delegate(EquipChoice x) { return x.Short == "WFI"; }) && f.Card(0).TestScopeOf(wfi) == "nur dieser Puffer" && f.Card(1).TestInfoRows == 0;
                f.Card(0).SetScope(wfi, 2); Application.DoEvents();
                okScope &= ps.Puffers[1].Lines.Contains(wfi) && !ps.Puffers[0].Lines.Contains(wfi) && ps.PufferChoices(1, 0).Exists(delegate(EquipChoice x) { return x.Short == "WFI"; });
                f.Card(1).SetScope(wfi, 3); Application.DoEvents();
                okScope &= ps.PufferShared.Count == 1 && ps.PufferShared[0].Scope == 3 && f.Card(1).TestInfoRows == 1 && f.Card(0).TestRows.Count == 4;

                // (3) Puffer entfernen zieht den Geltungsbereich nach (3 Puffer, WFI fuer Puffer 2+3, ersten entfernen -> Puffer 1+2)
                f.AddPuffer(); Application.DoEvents();
                bool okRemove = ps.Puffers.Count == 3 && f.cards.Count == 3 && f.Card(2).Open && f.Card(2).TestRows.Count == 5 && ps.Puffers[2].NameNo == 3;   // Standardpuffer: Ansatztank, VT, WFI, Tween, Material
                f.Card(1).SetScope(wfi, 6); Application.DoEvents();
                okRemove &= ps.PufferShared.Count == 1 && ps.PufferShared[0].Scope == 6;
                f.RemovePuffer(0, false); Application.DoEvents();
                okRemove &= ps.Puffers.Count == 2 && f.cards.Count == 2 && ps.PufferShared[0].Scope == 3 && ps.Puffers[0].Name == "Lösung B" && ps.Variant == "mobil:2";
                f.RemovePuffer(0, false); Application.DoEvents();
                okRemove &= ps.Puffers.Count == 1 && f.cards.Count == 1 && !f.Card(0).TestRemove.Enabled;   // der letzte Puffer bleibt
                f.RemovePuffer(0, false);
                okRemove &= ps.Puffers.Count == 1;

                // (4) Puffer fuer Produkt: Pufferpaket des Produkts bzw. Standardpuffer fuellt die Karten
                ProductProfile pr = new ProductProfile(); pr.Code = "1234";
                pr.Puffer.Add(Tpl("Lösung A", "mobil", Pos("Ansatztank", "AT-1", false), Pos("VT", "VT-1", false), Pos("WFI", "H1-T-0120", true)));
                pr.Puffer.Add(Tpl("Lösung B", "stationaer", Pos("Ansatztank", "AT-3", false), Pos("WFI", "H1-T-0120", true)));
                f.cat.Products.Add(pr);
                f.ApplyProductPuffer(Package.PufferFor(f.cat, f.Product)); Application.DoEvents();
                bool okProd = ps.Puffers.Count == 2 && f.cards.Count == 2 && f.Card(0).TestRows[0].TestIdBox.Text == "AT-1" && f.Card(1).TestRows[0].TestIdBox.Text == "AT-3"
                    && ps.Puffers[1].Typ == "stationaer" && ps.PufferShared.Count == 1 && ps.PufferShared[0].Scope == 3 && f.Card(0).Open && ps.Variant == "mobil|stationaer" && f.dirty;
                f.ApplyProductPuffer(Package.PufferFor(f.cat, "ZZ9")); Application.DoEvents();   // ohne Pufferpaket: Standardpuffer ohne Geraete
                okProd &= ps.Puffers.Count == 1 && f.cards.Count == 1 && f.Card(0).TestRows.Count == 5 && f.Card(0).TestRows[0].TestIdBox.Text == "" && ps.PufferShared.Count == 0;
                f.ApplyProductPuffer(Package.PufferFor(f.cat, f.Product)); Application.DoEvents();

                // (5) nicht validiertes Geraet -> Hinweis mit "Zur Liste hinzufuegen"; Hinweis verschwindet mit dem Geraet
                f.Card(0).TestRows[0].TestIdBox.Text = "ZZ-9"; Application.DoEvents();
                bool okUnval = f.hintList.Exists(delegate(Hint h) { return h.UnvalId == "ZZ-9" && h.Text.Contains("Puffer 1"); });
                f.Card(0).TestRows[0].TestIdBox.Text = "AT-1"; Application.DoEvents();
                okUnval &= !f.hintList.Exists(delegate(Hint h) { return h.UnvalId == "ZZ-9"; });

                // (6) doppeltes Geraet: Sperre mit Beteiligten, beide Karten rot; danach wieder frei
                f.Card(1).TestRows[0].TestIdBox.Text = "AT-1"; Application.DoEvents();
                bool okDup = f.hintList.Exists(delegate(Hint h) { return h.Code == "pufdoppelt" && h.Blocks && h.Text.Contains("AT-1 ist in Puffer 1 und Puffer 2"); }) && f.Card(0).Bad && f.Card(1).Bad;
                f.Card(1).TestRows[0].TestIdBox.Text = "AT-3"; Application.DoEvents();
                okDup &= !f.hintList.Exists(delegate(Hint h) { return h.Code == "pufdoppelt"; }) && !f.Card(0).Bad && !f.Card(1).Bad;

                // (7) Produktpassung: Geraet nicht in der Liste des Produkts -> Hinweis, Karte rot
                ProfilePos vp = new ProfilePos(); vp.Short = "VT"; vp.Own = true; vp.Ids.Add("VT-1"); pr.Positions.Add(vp);
                f.Card(0).TestRows[1].TestIdBox.Text = "VT-2"; Application.DoEvents();
                bool okFit = f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("VT-2"); }) && f.Card(0).Bad && !f.Card(1).Bad;
                f.Card(0).TestRows[1].TestIdBox.Text = "VT-1"; Application.DoEvents();
                okFit &= !f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("VT-2"); }) && !f.Card(0).Bad;

                // (8) Rueckgaengig: Puffer anhaengen und zuruecknehmen
                int snaps = f.UndoCount; f.AddPuffer(); Application.DoEvents();
                bool okUndo = f.UndoCount == snaps + 1 && f.cur.Puffers.Count == 3;
                f.TestUndo(-1); Application.DoEvents();
                okUndo &= f.cur.Puffers.Count == 2 && f.cards.Count == 2;
                return "UX1b Editor: Karten " + T(okCards) + " eigene Geräte " + T(okOwn) + " WFI-Bereich " + T(okScope) + " Puffer entfernen " + T(okRemove) + " Puffer für Produkt " + T(okProd)
                    + " nicht validiert " + T(okUnval) + " doppelt " + T(okDup) + " Produktpassung " + T(okFit) + " Rückgängig " + T(okUndo);
            }
            catch (Exception ex) { return "UX1b Editor: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
        }

        static PufferTemplatePos Pos(string sh, string id, bool shared)
        {
            PufferTemplatePos p = new PufferTemplatePos(); p.Short = sh; p.Shared = shared; if (id.Length > 0) p.Ids.Add(id); return p;
        }

        static PufferTemplate Tpl(string name, string typ, params PufferTemplatePos[] pos)
        {
            PufferTemplate t = new PufferTemplate(); t.Name = name; t.Typ = typ; t.Positions.AddRange(pos); return t;
        }
    }
}
#endif
