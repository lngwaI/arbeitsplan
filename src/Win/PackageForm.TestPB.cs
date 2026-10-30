#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest PB am sichtbaren Paket-Editor (seit UX-1b mit Puffer-Karten): Standard-Anzahl legt Equipment-Zeilen an, Umstellung auf Karten,
    // weitere Puffer/Namen/Zeilen, Pufferpaket einfuegen.
    partial class PackageForm
    {
        public void TestSelectBuffer() { foreach (PackageStep s in Ordered()) if (s.IsBuffer) { Bind(s); return; } }

        public static string TestPB(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("PB"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                Catalog c = Catalog.Default(); c.Baustein_("Puffer mobil").SetDefaultCount("MobT", 2); f.cat = c;
                PackageStep ps = f.FromBaustein(c.Baustein_("Puffer mobil")); ps.Row = "Puffer";
                int mobT = ps.Equipment.FindAll(delegate(EquipAssign a) { return a.Short == "MobT"; }).Count;
                bool okRows = mobT == 2 && ps.Equipment.Count == 5 && ps.PufferCount == 1;   // MobT x2, Flasche, Tween, Material

                f.AddStep(ps); Application.DoEvents();
                bool okUi = ps.Puffers.Count == 1 && ps.Equipment.Count == 0 && ps.Puffers[0].Lines.Count == 5 && f.cards.Count == 1 && f.gPuf.Visible && !f.gEquip.Visible
                    && f.cards[0].TestRows.Count == 5 && f.eqRows.Count == 0;   // Umstellung beim Oeffnen, eine Karte mit den 5 Zeilen
                f.AddPuffer(); Application.DoEvents();
                okUi &= ps.Puffers.Count == 2 && f.cards.Count == 2 && ps.BuffersFor(0, f.prefix)[1].Name == "Puffer mobil 2";
                f.cards[0].TestName.Text = "Lösung A"; Application.DoEvents();
                okUi &= ps.Puffers[0].Name == "Lösung A" && ps.BuffersFor(0, f.prefix)[0].Name == "Lösung A";
                f.Bind(f.steps[0]); f.Bind(ps); Application.DoEvents();   // anderer Schritt und zurueck: Werte stehen in der Karte
                okUi &= f.cards.Count == 2 && f.cards[0].TestName.Text == "Lösung A";
                f.AddPuffer(); Application.DoEvents();
                okUi &= ps.Puffers.Count == 3 && ps.BuffersFor(0, f.prefix)[2].Name == "Puffer mobil 3";
                f.RemovePuffer(2, false); Application.DoEvents();
                okUi &= ps.Puffers.Count == 2 && f.cards.Count == 2;
                f.cards[0].TestRows[0].TestIdBox.Text = EquipRow.NoneText; Application.DoEvents(); Application.DoEvents();   // "nicht benötigt": Zeile entfällt
                okRows &= ps.Puffers[0].Lines.Count == 4 && f.cards[0].TestRows.Count == 4;
                f.cards[0].AddLine("MobT"); Application.DoEvents();
                okRows &= ps.Puffers[0].Lines.Count == 5 && f.cards[0].TestRows.Count == 5;

                // Pufferpaket: nur Puffer-Schritte, als Kopie eingesetzt
                Package bp = new Package(); bp.Steps.Add(Package.ForBufferPackage(ps));
                int before = f.steps.Count;
                int n = f.InsertBufferSteps(bp, false);
                PackageStep ins = f.cur;
                bool okPkg = bp.IsBufferPackage && n == 1 && f.steps.Count == before + 1 && ins != ps && ins.IsBuffer && ins.Puffers.Count == 2 && ins.Puffers[0].Name == "Lösung A"
                    && ins.Puffers[0].Lines.Count == ps.Puffers[0].Lines.Count && ins.Puffers[0].Lines[0] != ps.Puffers[0].Lines[0] && ins.Bind == StepBind.JeLauf;
                ps.Puffers[0].Name = "geändert"; ps.Puffers[0].Lines[0].Note = "x";
                okPkg &= ins.Puffers[0].Name == "Lösung A" && ins.Puffers[0].Lines[0].Note == "";   // Kopie, nicht verknuepft
                Package mixed = Package.SampleCampaign("Gemischt");
                okPkg &= f.InsertBufferSteps(mixed, false) == 0 && f.steps.Count == before + 1;   // ohne Puffer-Schritte nichts eingefuegt
                Package.Seen.Remove(mixed);
                return "PB Editor: Standard-Zeilen " + T(okRows) + " Anzahl/Namen " + T(okUi) + " Pufferpaket einfügen " + T(okPkg);
            }
            catch (Exception ex) { return "PB Editor: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
    }
}
#endif
