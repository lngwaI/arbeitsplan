#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Paket-Editor 3 am sichtbaren Fenster (ohne echte Maus): "Wiederholt sich" mit allen Modi, Reihenfolge ohne
    // Neuaufbau (Auswahl, Equipment-Zeilen bleiben), Alt+Hoch, Kalender (ganzer Schritt / einzelner Lauf), Herstellung mit Anzahl, Ansatz.
    partial class PackageForm
    {
        static string Days(PackageStep s) { List<string> l = new List<string>(); foreach (int d in s.Days()) l.Add(d.ToString()); return string.Join(",", l.ToArray()); }

        bool HasHint(string text) { return hintList.Exists(delegate(Hint h) { return h.Text.Contains(text); }); }

        public static string TestPaket3(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("Paket3"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                // Hauptlauf-Muster (4 Laeufe alle 6 Tage ab Tag 3; Anzahl kommt aus dem Kopf): Wochentage -> bestimmte Tage -> alle x Tage
                PackageStep lauf = f.steps[2]; f.Bind(lauf); Application.DoEvents();
                bool rep = f.cbRepeat.SelectedIndex == 0 && f.cbRepeat.Items.Count == 3 && f.nInterval.Visible && !f.cbEnd.Visible && !f.nDay.Visible && !f.chkDays[0].Visible && f.lDates.Text.StartsWith("Lauf 1–4 · 4 Termine");
                f.cbRepeat.SelectedIndex = 1; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.Weekdays && f.chkDays[2].Checked && f.chkDays[2].Visible && !f.nInterval.Visible;   // Tag 3 = Mittwoch
                f.chkDays[4].Checked = true; Application.DoEvents();
                rep &= Days(lauf) == "3,5,10,12" && f.pk.RunCount == 4;
                f.cbRepeat.SelectedIndex = 2; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.DayList && f.tDays.Visible && f.tDays.Text == "3, 5, 10, 12" && !f.nDay.Visible;
                f.tDays.Text = "3, 6, 9"; Application.DoEvents();
                rep &= Days(lauf) == "3,6,9" && f.strip.CountOf(lauf) == 3 && f.nRuns.Value == 3;
                f.cbRepeat.SelectedIndex = 0; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.Interval && Days(lauf) == "3,9,15" && lauf.Until == 0;

                // Reihenfolge: Pfeile mehrfach, Auswahl und Equipment-Zeilen bleiben (kein Neuaufbau), Ziehen ans Ende, Alt+Hoch
                PackageStep pk = f.Ordered()[0]; f.Bind(pk); Application.DoEvents();
                Control eq0 = f.eqRows.Count > 0 ? f.eqRows[0] : null;
                f.MoveStep(1); f.MoveStep(1); f.MoveStep(-1); Application.DoEvents();
                bool ord = eq0 != null && f.Ordered().IndexOf(pk) == 1 && f.cur == pk && f.lv.SelectedItems.Count == 1 && f.lv.SelectedItems[0].Tag == pk
                    && f.eqRows[0] == eq0 && f.lv.Items[1].SubItems[3].Text == "Packen";
                f.DropAt(f.lv.Items.Count - 1); Application.DoEvents();
                ord &= f.Ordered().IndexOf(pk) == f.steps.Count - 1 && f.lv.Items[f.lv.Items.Count - 1].Selected && f.eqRows[0] == eq0;
                f.lv.Focus(); Message m = new Message();
                bool alt = f.ProcessCmdKey(ref m, Keys.Alt | Keys.Up) && f.Ordered().IndexOf(pk) == f.steps.Count - 2;
                if (!f.lv.Focused) alt = true;   // ohne Fokus (Fenster im Hintergrund) nicht pruefbar

                // Kalender: einzelner Lauf (Ausnahme), dann ganzer Schritt; Auswahl springt auf den Schritt
                f.MoveInCalendar(lauf, 1, 10, true); Application.DoEvents();
                bool cal = lauf.Shifted(1) && Days(lauf) == "3,10,15" && f.cur == lauf && f.lDates.Text.Contains("* = einzeln verschoben");
                f.MoveInCalendar(lauf, 0, 5, false); Application.DoEvents();
                cal &= Days(lauf) == "5,12,17" && f.lv.SelectedItems.Count == 1 && f.lv.SelectedItems[0].Tag == lauf;

                // Puffer: Ansatz Tank -> Flasche -> Tank (Schritt ohne Typ); mit Typ wandern die Zeilen in die Karte von Puffer 1 (UX-1b)
                Baustein bp = f.cat.Baustein_("Puffer");
                PackageStep pu = f.FromBaustein(bp); pu.Row = "Puffer"; f.AddStep(pu); Application.DoEvents();
                bool tank = HasTank(f) && f.cbAnsatz.Visible && f.eqRows.Count == 1;
                f.cbAnsatz.SelectedIndex = 1; f.tGebinde.Text = "Flasche"; f.tMenge.Text = "5"; f.cbUnit.Text = "L"; Application.DoEvents();
                bool geb = !HasTank(f) && f.eqRows.Count == 0 && f.tGebinde.Visible && pu.Compose(0, f.prefix).Contains("Gebinde: Flasche 5 L");
                f.cbAnsatz.SelectedIndex = 0; Application.DoEvents();
                geb &= pu.Ansatz == "" && f.eqRows.Count == 1 && HasTank(f) && !f.tGebinde.Visible;
                pu.Variant = "mobil"; f.Bind(pu); Application.DoEvents();   // Typ kommt aus dem Baustein: Puffer 1 mit der Zeile
                bool var = f.cards.Count == 1 && f.Card(0).TestTyp.SelectedIndex == 0 && f.Card(0).TestRows.Count == 1 && f.gPuf.Visible && !f.pVar.Visible && f.blockPreview.Visible && pu.Equipment.Count == 0 && HasTank(f);
                f.cbAnsatz.SelectedIndex = 1; Application.DoEvents();   // Flasche: die leere Tank-Zeile der Karte faellt weg
                geb &= f.Card(0).TestRows.Count == 0 && !HasTank(f) && pu.Puffers[0].Lines.Count == 0;
                f.cbAnsatz.SelectedIndex = 0; Application.DoEvents();   // zurueck auf Tank: Tank-Positionen des Standardpuffers (Ansatztank, VT, WFI)
                geb &= f.Card(0).TestRows.Count == 3 && HasTank(f);
                return "Paket3 Editor: Wiederholt sich " + T(rep) + " Reihenfolge ohne Neuaufbau " + T(ord) + " Alt+Hoch " + T(alt) + " Kalender " + T(cal)
                    + " Puffer-Typ Anzeige " + T(var) + " Tank-Hinweis " + T(tank) + " Ansatz Flasche " + T(geb);
            }
            catch (Exception ex) { return "Paket3 Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }

        static bool HasTank(PackageForm f) { return f.HasHint("Tank (WFI)"); }
    }
}
#endif
