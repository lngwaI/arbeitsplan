#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E5/E6 am Paket-Editor: Ziehen im Kalender (dieser + Folgelaeufe / Umschalt nur dieser), Ablehnung bei zwei Hauptlaeufen am
    // Tag, feste Laeufe (Schloss) bei Regelaenderung, Zuruecksetzen, Tooltip, Rueckgaengig/Wiederholen (Rundweg, 50er Grenze, Strg+Z),
    // Zoom (Spaltenbreite), "nur Auswahl" mit ◀ ▶.
    partial class PackageForm
    {
        static string DaysOf2(PackageStep s) { return string.Join(",", s.Days().ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()); }

        PlanStrip.Item ItemOf(PackageStep s, int run) { return strip.Items.Find(delegate(PlanStrip.Item i) { return i.Step == s && i.Run == run; }); }

        public static string TestE5(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("TestE5"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep lauf = f.steps[2], misch = f.steps[3];
                f.Bind(lauf); Application.DoEvents();
                string baseDays = DaysOf2(lauf);   // 3,9,15,21

                // Ziehen: Lauf 2 auf Tag 11 = dieser und alle folgenden (Move), Schloss; Umschalt = nur dieser Lauf (Shift)
                f.MoveInCalendar(lauf, 1, 11, false); Application.DoEvents();
                bool drag = DaysOf2(lauf) == "3,11,17,23" && lauf.Move.Count == 1 && lauf.Shift.Count == 0 && f.cur == lauf
                    && f.ItemOf(lauf, 1).Shifted && !f.ItemOf(lauf, 0).Shifted && !f.ItemOf(lauf, 2).Shifted && f.StepsView().Check().TrueForAll(delegate(PackageProblem p) { return p.Code != "gleichtag"; });
                RunDate m1 = f.Plan().Find(delegate(RunDate d) { return d.Step == 3 && d.Run == 1; });
                drag &= m1 != null && f.ItemOf(misch, 1).Day == m1.Day && m1.Day == 11 + misch.RunOffset;   // JeLauf-Schritt zieht mit
                f.MoveInCalendar(lauf, 3, 22, true); Application.DoEvents();
                bool shift = DaysOf2(lauf) == "3,11,17,22" && lauf.Shift.Count == 1 && lauf.Move.Count == 1 && f.ItemOf(lauf, 3).Shifted && f.stripMsg.Text.Length == 0;

                // Tooltip: voller Titel, Datum/Bezug, Lauf, Schloss, Bedienung
                string tipText = f.StripTip(f.ItemOf(lauf, 3));
                bool tipOk = tipText.Contains(TitleOf(lauf)) && tipText.Contains("Lauf 4 von 4") && tipText.Contains("Ziehen: nur dieser Block · Umschalt: dieser und alle folgenden") && tipText.Contains("Schloss")
                    && !f.StripTip(f.ItemOf(lauf, 0)).Contains("Schloss") && f.StripTip(f.ItemOf(misch, 1)).Contains("alle Läufe") && f.strip.TipFor != null;

                // Zuruecksetzen: ein Lauf (Menue-Weg), dann alle
                f.ResetRun(lauf, 3); Application.DoEvents();
                bool reset = DaysOf2(lauf) == "3,11,17,23" && lauf.Shift.Count == 0 && lauf.Move.Count == 1 && !f.ItemOf(lauf, 3).Shifted;
                f.ResetRun(lauf, -1); Application.DoEvents();
                reset &= DaysOf2(lauf) == baseDays && lauf.Move.Count == 0 && !f.ItemOf(lauf, 1).Shifted;

                // Gleicher Tag von Hand: wird verschoben, die Pruefliste zeigt nur einen Hinweis ("von Hand gesetzt"), keine Sperre
                f.MoveInCalendar(lauf, 1, 3, false); Application.DoEvents();
                List<PackageProblem> clash = f.StepsView().Check();
                bool reject = DaysOf2(lauf) == "3,3,9,15" && lauf.Move.Count == 1 && f.stripMsg.Text.Length == 0 && P1Has(clash, "gleichtag", Severity.Hinweis)
                    && clash.Exists(delegate(PackageProblem cp) { return cp.Code == "gleichtag" && cp.Text.Contains("von Hand gesetzt"); }) && !Package.HasBlock(clash);
                f.ResetRun(lauf, -1);
                f.MoveInCalendar(lauf, 1, 15, true); Application.DoEvents();   // nur dieser Lauf auf den Tag von Lauf 3: ebenfalls Hinweis
                reject &= DaysOf2(lauf) == "3,15,15,21" && lauf.Shift.Count == 1 && P1Has(f.StepsView().Check(), "gleichtag", Severity.Hinweis) && !Package.HasBlock(f.StepsView().Check());
                f.ResetRun(lauf, -1);
                reject &= DaysOf2(lauf) == baseDays;
                f.MoveInCalendar(lauf, 1, 10, false); Application.DoEvents();
                reject &= DaysOf2(lauf) == "3,10,16,22" && f.stripMsg.Text.Length == 0;
                f.ResetRun(lauf, -1);

                // Feste Laeufe: Regel (Abstand) aendert sich, der von Hand verschobene Lauf bleibt an seinem Tag
                f.MoveInCalendar(lauf, 1, 11, true); Application.DoEvents();   // Lauf 2 am Tag 11 (Shift +2)
                lauf.Interval = 5; f.Changed(); Application.DoEvents();
                bool pin = DaysOf2(lauf) == "3,11,13,18" && lauf.Shift[1] == 3 && f.ItemOf(lauf, 1).Shifted;
                lauf.Interval = 6; f.Changed(); Application.DoEvents();
                pin &= DaysOf2(lauf) == "3,11,15,21" && lauf.Shift[1] == 2;
                f.ResetRun(lauf, -1);
                pin &= DaysOf2(lauf) == baseDays;

                // Rueckgaengig / Wiederholen: Rundweg ueber Hinzufuegen, Titel, Verschieben, Entfernen; Auswahl kommt zurueck
                f.ActiveControl = f.lv;
                int n0 = f.steps.Count, u0 = f.UndoCount;
                string xml0 = f.StepsView().ToXml();
                PackageStep x = new PackageStep(); x.Title = "Zusatz"; x.Row = "Suite 2"; x.Day = 2; f.AddStep(x); Application.DoEvents();
                f.tTitle.Text = "Zusatz 2"; Application.DoEvents();
                f.MoveInCalendar(lauf, 2, 17, false); Application.DoEvents();
                string xml3 = f.StepsView().ToXml(); int sel3 = f.steps.IndexOf(f.cur);
                bool undo = f.UndoCount >= u0 + 3 && f.steps.Count == n0 + 1;
                f.TestUndo(-1); undo &= DaysOf2(f.steps[2]) == baseDays && f.steps[n0].Title == "Zusatz 2";
                f.TestUndo(-1); undo &= f.steps[n0].Title == "Zusatz";
                f.TestUndo(-1); undo &= f.steps.Count == n0;
                undo &= f.StepsView().ToXml() == xml0 && f.UndoAt == u0 - 1 && f.bRedo.Enabled;
                f.TestUndo(1); f.TestUndo(1); f.TestUndo(1);
                undo &= f.StepsView().ToXml() == xml3 && f.steps.IndexOf(f.cur) == sel3 && f.bUndo.Enabled && !f.bRedo.Enabled && f.lv.SelectedItems.Count == 1;
                Message msg = new Message();
                undo &= f.ProcessCmdKey(ref msg, Keys.Control | Keys.Z) && f.steps[2].Days().Count == 4 && DaysOf2(f.steps[2]) == baseDays;   // Strg+Z
                undo &= f.ProcessCmdKey(ref msg, Keys.Control | Keys.Y) && DaysOf2(f.steps[2]) == "3,9,17,23";                              // Strg+Y: Move ab Lauf 3
                f.TestUndo(-1);
                f.MoveStep(1); Application.DoEvents();                                    // Umsortieren ist ein Schritt
                undo &= !f.bRedo.Enabled && f.bUndo.Enabled;
                f.TestUndo(-1); undo &= f.bRedo.Enabled;

                // 50er Grenze: nach vielen Aenderungen bleiben 50 Staende, der aelteste faellt weg
                f.TestUndo(1);
                PackageStep t = f.cur;
                for (int i = 0; i < 60; i++) { t.Title = "T" + i; f.Changed(); }
                bool lim = f.UndoCount == MaxUndo && f.UndoAt == MaxUndo - 1;
                for (int i = 0; i < 70; i++) f.TestUndo(-1);
                lim &= f.UndoAt == 0 && f.cur != null && f.cur.Title == "T" + (60 - MaxUndo) && !f.bUndo.Enabled;
                for (int i = 0; i < 70; i++) f.TestUndo(1);
                lim &= f.UndoAt == MaxUndo - 1 && f.cur.Title == "T59" && !f.bRedo.Enabled;

                // Zoom: Spaltenbreite je Stufe; Knoepfe und Beschriftung
                int[] w = new int[PlanStrip.ZoomDays.Length];
                for (int z = 0; z < w.Length; z++) { f.strip.Zoom = z; w[z] = f.strip.DayWidth; }
                bool zoom = w[0] > w[1] && w[1] > w[2] && w[2] >= w[3] && w[3] >= 40 && w[0] > 2 * w[2];
                f.strip.Zoom = 2; f.bZoomIn.PerformClick(); zoom &= f.strip.Zoom == 1 && f.lZoom.Text == PlanStrip.ZoomNames[1] && f.strip.DayWidth == w[1];
                f.bZoomOut.PerformClick(); f.bZoomOut.PerformClick(); zoom &= f.strip.Zoom == 3 && !f.bZoomOut.Enabled && f.bZoomIn.Enabled;
                f.strip.Zoom = 0; zoom &= !f.bZoomIn.Enabled;
                f.strip.Zoom = 1;

                // nur Auswahl: nur die Zeile des gewaehlten Schritts, ◀ ▶ in Planreihenfolge
                List<PackageStep> o = f.Ordered(); int allRows = f.strip.RowNames().Count;
                f.Bind(o[0]); Application.DoEvents();
                bool only = !f.chOnly.Checked && !f.bStepPrev.Enabled && !f.bStepNext.Enabled && allRows >= 2;
                f.chOnly.Checked = true; Application.DoEvents();
                only &= f.strip.RowNames().Count == 1 && f.strip.Items.TrueForAll(delegate(PlanStrip.Item i) { return i.Step == o[0]; }) && f.strip.RowNames()[0] == o[0].Row;
                only &= !f.bStepPrev.Enabled && f.bStepNext.Enabled;
                f.bStepNext.PerformClick(); Application.DoEvents();
                List<PackageStep> o2 = f.Ordered();
                only &= f.cur == o2[1] && f.strip.RowNames().Count == 1 && f.strip.RowNames()[0] == o2[1].Row && f.bStepPrev.Enabled;
                for (int i = 0; i < o2.Count; i++) f.bStepNext.PerformClick();
                only &= f.cur == o2[o2.Count - 1] && !f.bStepNext.Enabled;
                f.bStepPrev.PerformClick(); only &= f.cur == o2[o2.Count - 2];
                f.chOnly.Checked = false; Application.DoEvents();
                only &= f.strip.RowNames().Count == allRows && !f.bStepPrev.Enabled;

                return "Paket-Editor Kalender: Ziehen ab Lauf (Move) mit Schloss " + T(drag) + ", Umschalt nur dieser Lauf " + T(shift) + ", Tooltip " + T(tipOk) + ", Zurücksetzen " + T(reset)
                    + ", gleicher Tag nur Hinweis " + T(reject) + ", feste Läufe bei Regeländerung " + T(pin) + ", Rückgängig/Wiederholen " + T(undo) + ", 50er Grenze " + T(lim)
                    + ", Zoom " + T(zoom) + ", nur Auswahl " + T(only);
            }
            catch (Exception ex) { return "Paket-Editor Kalender: FEHLER " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }

        // Testbilder: package8 = verschobene Laeufe mit Schloss (Zoom ½ Woche), package9 = Zoom Woche und "nur Auswahl"
        public void TestE5Shot(string which)
        {
            PackageStep lauf = steps[2];
            Bind(lauf);
            MoveInCalendar(lauf, 1, 11, false);
            MoveInCalendar(lauf, 3, 23, true);
            if (which == "package9") { strip.Zoom = 2; chOnly.Checked = true; }
            Application.DoEvents();
        }
    }
}
#endif
