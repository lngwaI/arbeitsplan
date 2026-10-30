#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-6 (B1/B2/B6) im Paket-Editor: "Pufferpaket übernehmen" (PufferPickForm), "+ Puffer ▾", Plan-Name mit Produktcode.
    // Testbilder: --form ux6pick (Paket mit Produktcode), ux6pick0 (ohne), ux6add (Editor mit Puffer-Schritt). Erfundene Daten (PRD-1, PRD-2).
    partial class PackageForm
    {
        // PRD-1 mit drei Puffern (ein unbekanntes Gerät ZZ-77), PRD-2 ohne Pufferpaket; nur im Speicher (Katalog wird nicht gespeichert)
        static void Ux6Products(Catalog c)
        {
            c.AddStandardBausteine();
            foreach (string id in new[] { "AT-1", "AT-2", "AT-3", "VT-1", "VT-2", "H1-T-0120" }) if (c.Master.Device(id) == null) { EquipDevice d = new EquipDevice(); d.Id = id; c.Master.Devices.Add(d); }
            ProductProfile p1 = new ProductProfile(); p1.Code = "PRD-1"; p1.Name = "Musterlösung";
            PufferTemplate a = Tpl("Lösung A", "mobil", Pos("Ansatztank", "AT-1", false), Pos("VT", "VT-1", false), Pos("WFI", "H1-T-0120", true)); a.Art = "mobiler Puffer 1";
            PufferTemplate b = Tpl("Lösung B", "mobil", Pos("Ansatztank", "AT-2", false), Pos("VT", "VT-2", false), Pos("WFI", "H1-T-0120", true)); b.Art = "mobiler Puffer 2";
            PufferTemplate s = Tpl("Lösung C", "stationaer", Pos("Ansatztank", "AT-3", false), Pos("Tween", "ZZ-77", false)); s.Art = "stationärer Puffer";
            p1.Puffer.Add(a); p1.Puffer.Add(b); p1.Puffer.Add(s);
            ProductProfile p2 = new ProductProfile(); p2.Code = "PRD-2"; p2.Name = "Zweitprodukt";
            c.Products.Add(p1); c.Products.Add(p2);
        }

        public static Form Ux6PickForm(IDataStore store, bool withCode)
        {
            Catalog c = store.GetCatalog(); Ux6Products(c);
            return new PufferPickForm(c, withCode ? "PRD-1" : "", 2, true, false, null);
        }

        // Editor mit Puffer-Schritt, ohne Produktcode, Pufferpaket PRD-1 im Katalog (Knopfzeile "Pufferpaket übernehmen… | + Puffer ▾ | …")
        public static Form Ux6AddForm(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package p = Ux1bSample("Testpaket", false); p.ProductCode = "";
            PackageForm f = new PackageForm(store, equip, start, p, true, true);
            Ux6Products(f.cat);
            f.Shown += delegate { f.Size = new Size(1190, 980); f.TestSelectBuffer(); f.TestShrinkPane(); };
            return f;
        }

        PufferCard C6(int k) { return cards[k]; }
        internal Package TestCollect() { return Collect(); }
        string Id6(int p, int row) { return cards[p].TestRows[row].TestIdBox.Text; }

        public static string TestUx6Editor(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux1bSample("UX6", false); sample.ProductCode = "";
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                Ux6Products(f.cat);
                f.TestSelectBuffer(); Application.DoEvents();
                PackageStep ps = f.cur; ProductProfile pr = f.cat.Product("PRD-1");
                bool okBtn = f.bPufProd.Text == "Pufferpaket übernehmen…" && f.bPufAdd.Text == "+ Puffer ▾" && f.Product == "";

                // (1) Dialog ohne Produktcode: Produkt mit Pufferpaket vorgewählt, Hinweis, Geräte und Warnzeichen, Zähler, Knopftext
                List<PufferTemplate> picked; string code;
                bool okPick;
                using (PufferPickForm d = new PufferPickForm(f.cat, f.Product, ps.Puffers.Count, true, false, null))
                {
                    d.Show(); Application.DoEvents();
                    okPick = d.Text == "Pufferpaket übernehmen" && d.lblHint.Visible && d.TestSourceText == "PRD-1 · Musterlösung (3 Puffer)" && d.TestSourceCount == 3 && d.lv.Items.Count == 3
                        && d.lv.Items[0].Text == "mobiler Puffer 1" && d.lv.Items[0].SubItems[1].Text == "mobil" && d.lv.Items[0].SubItems[2].Text == "Ansatztank AT-1, VT VT-1, WFI H1-T-0120"
                        && d.lv.Items[2].SubItems[1].Text == "stationär" && d.lv.Items[2].SubItems[2].Text.StartsWith("⚠ ") && !d.lv.Items[0].SubItems[2].Text.StartsWith("⚠")
                        && d.lblCount.Text == "3 von 3 Puffern angehakt" && d.bOk.Text == "3 Puffer übernehmen" && d.rbReplace.Checked && d.rbReplace.Text == "Bisherige 2 Puffer ersetzen" && d.rbAppend.Text == "Hinten anhängen (noch 7 frei)";
                    d.lv.Items[2].Checked = false; Application.DoEvents();
                    okPick &= d.lblCount.Text == "2 von 3 Puffern angehakt" && d.bOk.Text == "2 Puffer übernehmen" && d.bOk.Enabled;
                    d.lv.Items[0].Checked = d.lv.Items[1].Checked = false; Application.DoEvents();
                    okPick &= !d.bOk.Enabled && d.bOk.Text == "Puffer übernehmen";
                    d.lv.Items[0].Checked = d.lv.Items[1].Checked = true; Application.DoEvents();
                    d.TestCommit(); picked = d.Result; code = d.ResultCode;
                    okPick &= d.DialogResult == DialogResult.OK && picked.Count == 2 && code == "PRD-1" && !d.Append;
                }
                // Ersetzen: Geräte der Vorlage stehen in den Karten, der Produktcode des Pakets bleibt leer
                f.ApplyPicked(picked, code, false); Application.DoEvents();
                bool okRep = ps.Puffers.Count == 2 && f.cards.Count == 2 && f.Id6(0, 0) == "AT-1" && f.Id6(1, 0) == "AT-2" && f.Id6(1, 1) == "VT-2" && ps.PufferShared.Count == 1 && ps.PufferShared[0].Scope == 3
                    && f.Product == "" && f.dirty && f.stripMsg.Text == "2 Puffer aus Pufferpaket PRD-1 übernommen (bisherige ersetzt) – Rückgängig mit Strg+Z";

                // (2) Plan: mit Produktcode im Paket heißen die Puffer "PRD-1 - Name" (Package.Apply in einen eigenen Speicher)
                f.tProduct.Text = "PRD-1"; Application.DoEvents();
                bool okPlan = false;
                try
                {
                    MemoryStore ms = new MemoryStore(); ms.SaveCatalog(f.cat);
                    f.TestCollect().Apply(ms, new DateTime(2035, 1, 1), ApplyMode.Overwrite, null);
                    int prow = -1; foreach (RowInfo r in ms.GetRows()) if (r.Name == "Puffer") prow = r.Id;
                    List<string> names = new List<string>();
                    for (int i = 0; i < 28 && prow >= 0; i++) { Entry e = ms.Get(new DateTime(2035, 1, 1).AddDays(i), prow); if (e != null) foreach (BufferItem b in e.Buffers) names.Add(b.Name); }
                    okPlan = names.Contains("PRD-1 - Lösung A") && names.Contains("PRD-1 - Lösung B") && !names.Contains("Lösung A");
                }
                catch (Exception) { okPlan = false; }
                f.tProduct.Text = ""; Application.DoEvents();

                // (3) Anhängen: Reihenfolge erhalten, Maximum 9
                f.ApplyPicked(Package.PufferFor(f.cat, "PRD-1"), "PRD-1", true); Application.DoEvents();
                bool okApp = ps.Puffers.Count == 5 && f.Id6(2, 0) == "AT-1" && f.Id6(4, 0) == "AT-3" && ps.Puffers[4].Typ == "stationaer" && ps.Puffers[4].Name == "Lösung C" && f.stripMsg.Text.StartsWith("3 Puffer aus Pufferpaket PRD-1 übernommen (hinten angehängt)") && f.Card(2).Open;
                f.ApplyPicked(Package.PufferFor(f.cat, "PRD-1"), "PRD-1", true); Application.DoEvents();
                bool okMax = ps.Puffers.Count == 8 && f.cards.Count == 8;
                f.ApplyPicked(Package.PufferFor(f.cat, "PRD-1"), "PRD-1", true); Application.DoEvents();
                okMax &= ps.Puffers.Count == 9 && f.cards.Count == 9 && !f.bPufAdd.Enabled && f.stripMsg.Text.StartsWith("1 Puffer aus Pufferpaket PRD-1");
                f.AddPuffer(); f.AddPufferCopy(0); f.AddPufferFromTemplate(pr.Puffer[0]); Application.DoEvents();
                okMax &= ps.Puffers.Count == 9;
                using (PufferPickForm d = new PufferPickForm(f.cat, "PRD-1", 9, true, true, null))
                {
                    d.Show(); Application.DoEvents();
                    okMax &= !d.rbAppend.Enabled && d.rbReplace.Checked && d.rbAppend.Text.StartsWith("Hinten anhängen (kein Platz frei");
                    d.TestCommit(); okMax &= !d.Append && d.Result.Count == 3;
                }

                // (4) zurück auf zwei Puffer; Kopie einer Karte: unabhängig, gemeinsame Zeile deckt die Kopie ab, Name wie das Original
                f.ApplyPicked(picked, "PRD-1", false); Application.DoEvents();
                int snaps = f.UndoCount; f.AddPufferCopy(0); Application.DoEvents();
                bool okCopy = ps.Puffers.Count == 3 && f.UndoCount == snaps + 1 && ps.Puffers[2].Name == "Lösung A" && f.Id6(2, 0) == "AT-1" && ps.PufferShared[0].Covers(2) && ps.PufferShared[0].Scope == 7
                    && ps.Puffers[2].Lines[0] != ps.Puffers[0].Lines[0] && f.Card(2).Open && f.stripMsg.Text == "Puffer 3 als Kopie von Puffer 1 angehängt – Rückgängig mit Strg+Z";
                f.Card(2).TestRows[0].TestIdBox.Text = "AT-2"; Application.DoEvents(); Application.DoEvents();
                okCopy &= f.Id6(0, 0) == "AT-1" && ps.Puffers[0].Lines[0].ChoiceFor(0).Id == "AT-1" && ps.Puffers[2].Lines[0].ChoiceFor(0).Id == "AT-2";
                ps.Puffers[1].Name = ""; ps.Puffers[1].NameNo = 2; f.AddPufferCopy(1); Application.DoEvents();
                okCopy &= ps.Puffers.Count == 4 && ps.Puffers[3].Name == "" && ps.Puffers[3].NameNo == 4 && ps.PufferShared[0].Scope == 15;
                f.TestUndo(-1); Application.DoEvents(); ps = f.cur;   // Rückgängig ersetzt den Schritt durch eine Kopie
                okCopy &= ps.Puffers.Count == 3 && f.cards.Count == 3;

                // (5) "+ Puffer ▾": Menü ohne/mit Produktcode, aus Vorlage anhängen
                f.ShowAddMenu(); Application.DoEvents();
                List<string> m = Ux6Items(f.addMenu); f.addMenu.Close();
                bool okMenu = m.Count == 7 && m[0] == "Leerer Puffer (Positionen des Standardpuffers)" && m[1] == "Kopie von Puffer 1: Lösung A" && m[3] == "Kopie von Puffer 3: Lösung A" && m[4] == "-" && m[5] == "Kein Produktcode im Paket" && m[6] == "Andere Vorlage…";
                f.tProduct.Text = "PRD-1"; Application.DoEvents();
                f.ShowAddMenu(); Application.DoEvents();
                m = Ux6Items(f.addMenu); f.addMenu.Close();
                okMenu &= m.Count == 10 && m[5] == "Vorlage aus Pufferpaket PRD-1:" && m[6] == "mobiler Puffer 1 · mobil" && m[8] == "stationärer Puffer · stationär" && m[9] == "Andere Vorlage…";
                snaps = f.UndoAt; f.AddPufferFromTemplate(pr.Puffer[1]); Application.DoEvents();
                bool okTpl = ps.Puffers.Count == 4 && f.UndoAt == snaps + 1 && f.Id6(3, 0) == "AT-2" && ps.Puffers[3].Name == "Lösung B" && f.Card(3).Open && ps.PufferShared[0].Scope == 15
                    && f.stripMsg.Text == "Puffer 4 aus Vorlage „mobiler Puffer 2“ angehängt – Rückgängig mit Strg+Z";
                f.AddPuffer(); Application.DoEvents();
                okTpl &= ps.Puffers.Count == 5 && ps.Puffers[4].Lines.Count == 5 && ps.Puffers[4].NameNo == 5 && f.stripMsg.Text == "Puffer 5 angehängt – Rückgängig mit Strg+Z";
                f.tProduct.Text = ""; Application.DoEvents();

                // (6) Leerzustand: Produkt ohne Pufferpaket (mit/ohne Recht), Standardpuffer, "Andere Vorlage…" = Anhängen vorgewählt
                bool okEmpty;
                using (PufferPickForm d = new PufferPickForm(f.cat, "PRD-2", 1, true, true, null))
                {
                    d.Show(); Application.DoEvents();
                    okEmpty = d.TestSourceText == "PRD-2 · Zweitprodukt (kein Pufferpaket)" && !d.lblHint.Visible && !d.lv.Visible && d.lblEmpty.Text == "Für PRD-2 ist kein Pufferpaket hinterlegt." && d.link.Visible
                        && d.link.Text == "Pufferpaket für PRD-2 anlegen…" && !d.bOk.Enabled && d.rbAppend.Checked;
                    d.TestCreate();
                    okEmpty &= d.DialogResult == DialogResult.Retry && d.CreateFor == "PRD-2" && d.Append;
                }
                using (PufferPickForm d = new PufferPickForm(f.cat, "PRD-2", 1, false, false, null))
                {
                    d.Show(); Application.DoEvents();
                    okEmpty &= !d.link.Visible && d.lblEmpty.Text.Contains("Der Admin legt Pufferpakete an.");
                }
                using (PufferPickForm d = new PufferPickForm(f.cat, "PRD-1", 1, true, false, "PRD-2"))
                {
                    d.Show(); Application.DoEvents();
                    okEmpty &= d.TestSourceText.StartsWith("PRD-2");
                    d.TestPickSource("");
                    okEmpty &= d.TestSourceText == "Standardpuffer (ohne Geräte)" && d.lv.Items.Count == 1 && d.lv.Items[0].SubItems[2].Text.StartsWith("Ansatztank, ") && d.bOk.Text == "1 Puffer übernehmen" && d.lv.Items[0].SubItems[1].Text == "mobil";
                }
                using (PufferPickForm d = new PufferPickForm(f.cat, "", 1, true, false, null))   // ohne Code: genau ein Produkt mit Pufferpaket -> dieses
                {
                    d.Show(); Application.DoEvents();
                    okEmpty &= d.TestSourceText.StartsWith("PRD-1") && d.lblHint.Visible;
                }
                f.dirty = false;
                return "UX6 Editor: Knöpfe " + T(okBtn) + " Dialog " + T(okPick) + " Ersetzen " + T(okRep) + " Plan-Name " + T(okPlan) + " Anhängen " + T(okApp) + " Maximum " + T(okMax) + " Kopie " + T(okCopy)
                    + " Menü " + T(okMenu) + " Vorlage " + T(okTpl) + " Leerzustand " + T(okEmpty);
            }
            catch (Exception ex) { return "UX6 Editor: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace; }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
        }

        static List<string> Ux6Items(ContextMenuStrip m)
        {
            List<string> r = new List<string>();
            foreach (ToolStripItem i in m.Items) r.Add(i is ToolStripSeparator ? "-" : i.Text);
            return r;
        }
    }
}
#endif
