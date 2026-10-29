#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Abschnitt "3 Vorschau & Pruefung" (Task V): Kalender-Zeilen in Planreihenfolge, Vorschau blaettert einen Eintrag,
    // Kalender und Blaettern synchron, Hoehe verstellbar/gemerkt (Datei in %TEMP%), Mindesthoehe.
    partial class PackageForm
    {
        public static string TestV(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            string oldPrefs = PrefsFile, tmp = Path.Combine(Path.GetTempPath(), "apv_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("TestV"), true, true), g = null;
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                // Kalender: Zeilen in Planreihenfolge (Suite 1 vor Suite 2 vor Puffer, obwohl der Puffer frueher kommt), leere Zeilen ausgeblendet
                PackageStep pu = f.FromBaustein(f.cat.Baustein_("Puffer")); pu.Row = "Puffer"; pu.Day = 1; f.AddStep(pu);
                PackageStep s2 = new PackageStep(); s2.Title = "Zweiter Raum"; s2.Row = "Suite 2"; s2.Day = 3; f.AddStep(s2);
                Application.DoEvents();
                List<string> rows = f.strip.RowNames();
                bool order = rows.Count == 3 && rows[0] == "Suite 1" && rows[1] == "Suite 2" && rows[2] == "Puffer" && f.rowNames.IndexOf("Suite 2") < f.rowNames.IndexOf("Puffer");

                // Blaettern: ein Eintrag, Lauf 1..4 von 4, Knoepfe und Pfeiltasten
                PackageStep lauf = f.steps[2]; f.Bind(lauf); Application.DoEvents();
                bool page = f.lPage.Text == "Lauf 1 von 4" && f.blockPreview.Count == 1 && !f.bPrev.Enabled && f.bNext.Enabled && f.curRun == 0 && f.strip.SelectedRun == 0;
                f.bNext.PerformClick(); f.bNext.PerformClick(); f.bNext.PerformClick();
                page &= f.lPage.Text == "Lauf 4 von 4" && f.curRun == 3 && f.strip.SelectedRun == 3 && !f.bNext.Enabled && f.bPrev.Enabled && f.blockPreview.Count == 1;
                f.GoPage(f.PageIndex() + 1); page &= f.lPage.Text == "Lauf 4 von 4";   // am Ende bleibt es stehen
                f.blockPreview.TestKey(Keys.Left); page &= f.lPage.Text == "Lauf 3 von 4" && f.curRun == 2;
                f.blockPreview.TestKey(Keys.Right); f.blockPreview.TestKey(Keys.Down); page &= f.lPage.Text == "Lauf 4 von 4";
                for (int i = 0; i < 4; i++) f.bPrev.PerformClick();
                page &= f.lPage.Text == "Lauf 1 von 4" && f.strip.SelectedRun == 0;

                // Synchron: Klick auf anderen Lauf, Klick in anderen Schritt, Listenwahl beginnt wieder bei 1, Verschieben behaelt den Lauf
                f.StripClicked(new PlanStrip.Item { Step = lauf, Run = 2 });
                bool sync = f.lPage.Text == "Lauf 3 von 4" && f.curRun == 2 && f.strip.SelectedRun == 2 && f.cur == lauf;
                PackageStep misch = f.steps[3];
                f.StripClicked(new PlanStrip.Item { Step = misch, Run = 3 });
                sync &= f.cur == misch && f.curRun == 3 && f.lPage.Text == "Lauf 4 von 4" && f.strip.SelectedRun == 3 && f.lv.SelectedItems.Count == 1 && f.lv.SelectedItems[0].Tag == misch;
                f.Bind(lauf); sync &= f.curRun == 0 && f.lPage.Text == "Lauf 1 von 4";
                f.GoPage(1); f.MoveInCalendar(lauf, 1, f.curDates[1].Day + 1, true); Application.DoEvents();
                sync &= f.curRun == 1 && f.strip.SelectedRun == 1 && f.lPage.Text == "Lauf 2 von 4";

                // Hoehe: Standard, verstellen (gemerkt), Mindesthoehe, Obergrenze, neuer Editor liest den Wert; Felder wachsen mit
                int h0 = f.paneWrap.Height, hh0 = f.lbHints.Height, sh0 = f.strip.Height, ph0 = f.previewScroll.Height;
                bool noFile = !File.Exists(tmp) && h0 == f.PaneMin + 24;
                f.TestPaneMoved(h0 + 60); Application.DoEvents();
                bool grow = f.paneWrap.Height == h0 + 60 && f.lbHints.Height == hh0 + 60 && f.strip.Height == sh0 + 60 && f.previewScroll.Height == ph0 + 60 && Pref("Hoehe3") == (h0 + 60).ToString();
                f.TestPaneMoved(10); bool min = f.paneWrap.Height == f.PaneMin && Pref("Hoehe3") == f.PaneMin.ToString();
                f.TestPaneMoved(4000); bool max = f.paneWrap.Height == f.PaneMax && f.PaneMax >= f.PaneMin && f.paneWrap.Height < f.ClientSize.Height;
                f.TestPaneMoved(h0 + 60); Application.DoEvents();
                g = new PackageForm(store, equip, start, Package.SampleCampaign("TestV2"), true, true);
                g.StartPosition = FormStartPosition.Manual; g.Location = new Point(0, 0); g.Show(); Application.DoEvents();
                bool load = g.paneWrap.Height == Math.Min(h0 + 60, g.PaneMax) && g.paneWrap.Height > g.PaneMin;
                return "Paket-Editor Vorschau: Kalender-Zeilen in Planreihenfolge " + T(order) + ", Blättern 1 bis n " + T(page) + ", Kalender und Vorschau synchron " + T(sync)
                    + ", Höhe ändern, Felder wachsen " + T(noFile && grow) + ", Mindesthöhe " + T(min) + ", Obergrenze " + T(max) + ", Höhe gemerkt und geladen " + T(load);
            }
            catch (Exception ex) { return "Paket-Editor Vorschau: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally
            {
                f.dirty = false; f.Close(); f.Dispose();
                if (g != null) { g.dirty = false; g.Close(); g.Dispose(); }
                PrefsFile = oldPrefs;
                try { File.Delete(tmp); } catch (Exception) { }
            }
        }

        void TestPaneMoved(int h) { paneWrap.Height = h; PaneMoved(); }
    }
}
#endif
