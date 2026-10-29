#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest PB am sichtbaren Paket-Editor: Standard-Anzahl legt Equipment-Zeilen an, Anzahl/Namen der Puffer im Schritt, Pufferpaket einfuegen.
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
                bool okUi = f.nPuf.Visible && !f.tPufNames.Visible && f.nPuf.Value == 1 && f.equipRows.Controls.Count == 5;
                f.nPuf.Value = 3; Application.DoEvents();
                okUi &= ps.PufferCount == 3 && f.tPufNames.Visible;
                f.tPufNames.Text = "Lösung A\r\nLösung B"; Application.DoEvents();
                okUi &= ps.PufferNames == "Lösung A|Lösung B" && ps.BuffersFor(0, f.prefix).Count == 3 && ps.BuffersFor(0, f.prefix)[2].Name == "Puffer mobil 3";
                f.Bind(f.steps[0]); f.Bind(ps); Application.DoEvents();   // anderer Schritt und zurueck: Werte stehen im Feld
                okUi &= f.nPuf.Value == 3 && f.tPufNames.Text == "Lösung A\r\nLösung B";
                f.nPuf.Value = 1; Application.DoEvents();
                okUi &= ps.PufferCount == 1 && f.tPufNames.Visible;   // Namen verschwinden nicht still
                f.nPuf.Value = 3; Application.DoEvents();
                f.RemoveAssign(ps.Equipment[0]); Application.DoEvents();   // Zeilen entfernen wie bisher
                okRows &= ps.Equipment.Count == 4 && f.equipRows.Controls.Count == 4;
                f.AddPosition("MobT"); Application.DoEvents();
                okRows &= ps.Equipment.Count == 5;

                // Pufferpaket: nur Puffer-Schritte, als Kopie eingesetzt
                Package bp = new Package(); bp.Steps.Add(Package.ForBufferPackage(ps));
                int before = f.steps.Count;
                int n = f.InsertBufferSteps(bp, false);
                PackageStep ins = f.cur;
                bool okPkg = bp.IsBufferPackage && n == 1 && f.steps.Count == before + 1 && ins != ps && ins.IsBuffer && ins.PufferCount == 3 && ins.PufferNames == "Lösung A|Lösung B"
                    && ins.Equipment.Count == ps.Equipment.Count && ins.Equipment[0] != ps.Equipment[0] && ins.Bind == StepBind.JeLauf;
                ps.PufferNames = "geändert"; ps.Equipment[0].Note = "x";
                okPkg &= ins.PufferNames == "Lösung A|Lösung B" && ins.Equipment[0].Note == "";   // Kopie, nicht verknuepft
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
