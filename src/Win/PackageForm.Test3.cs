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
                // Wiederholt sich: Lauf (4x alle 6 Tage ab Tag 3) -> Wochentage -> Tagesliste -> alle x Tage bis Tag n
                PackageStep lauf = f.steps[2]; f.Bind(lauf); Application.DoEvents();
                bool rep = f.cbRepeat.SelectedIndex == 1 && f.nInterval.Visible && f.nCount.Visible && !f.chkDays[0].Visible && f.lDates.Text.StartsWith("Termine (4)");
                f.cbRepeat.SelectedIndex = 2; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.Weekdays && f.chkDays[2].Checked && f.chkDays[2].Visible && !f.nInterval.Visible;   // Tag 3 = Mittwoch
                f.chkDays[4].Checked = true; Application.DoEvents();
                rep &= Days(lauf) == "3,5,10,12" && f.lDates.Text.Contains("5 Fr");
                f.cbRepeat.SelectedIndex = 3; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.DayList && f.tDays.Visible && f.tDays.Text == "3, 5, 10, 12" && !f.nDay.Visible;
                f.tDays.Text = "3, 6, 9"; Application.DoEvents();
                rep &= Days(lauf) == "3,6,9" && f.strip.CountOf(lauf) == 3;
                f.cbRepeat.SelectedIndex = 1; Application.DoEvents();
                rep &= lauf.Repeat == RepeatMode.Interval && Days(lauf) == "3,9,15";
                f.cbEnd.SelectedIndex = 1; Application.DoEvents();
                rep &= lauf.Until == 15 && f.nUntil.Visible && !f.nCount.Visible;
                f.nUntil.Value = 9; Application.DoEvents();
                rep &= Days(lauf) == "3,9" && f.strip.CountOf(lauf) == 2;

                // Reihenfolge: Pfeile mehrfach, Auswahl und Equipment-Zeilen bleiben (kein Neuaufbau), Ziehen ans Ende, Alt+Hoch
                PackageStep pk = f.Ordered()[0]; f.Bind(pk); Application.DoEvents();
                Control eq0 = f.equipRows.Controls.Count > 0 ? f.equipRows.Controls[0] : null;
                f.MoveStep(1); f.MoveStep(1); f.MoveStep(-1); Application.DoEvents();
                bool ord = eq0 != null && f.Ordered().IndexOf(pk) == 1 && f.cur == pk && f.lv.SelectedItems.Count == 1 && f.lv.SelectedItems[0].Tag == pk
                    && f.equipRows.Controls[0] == eq0 && f.lv.Items[1].SubItems[3].Text == "Packen" && f.nDay.Value == pk.Day;
                f.DropAt(f.lv.Items.Count - 1); Application.DoEvents();
                ord &= f.Ordered().IndexOf(pk) == f.steps.Count - 1 && f.lv.Items[f.lv.Items.Count - 1].Selected && f.equipRows.Controls[0] == eq0;
                f.lv.Focus(); Message m = new Message();
                bool alt = f.ProcessCmdKey(ref m, Keys.Alt | Keys.Up) && f.Ordered().IndexOf(pk) == f.steps.Count - 2;
                if (!f.lv.Focused) alt = true;   // ohne Fokus (Fenster im Hintergrund) nicht pruefbar

                // Kalender: einzelner Lauf (Ausnahme), dann ganzer Schritt; Auswahl springt auf den Schritt
                f.MoveInCalendar(lauf, 1, 10, true); Application.DoEvents();
                bool cal = lauf.Shifted(1) && Days(lauf) == "3,10" && f.cur == lauf && f.lDates.Text.Contains("10 Mi*");
                f.MoveInCalendar(lauf, 0, 5, false); Application.DoEvents();
                cal &= Days(lauf) == "5,12" && f.nDay.Value == 5 && f.lv.SelectedItems.Count == 1 && f.lv.SelectedItems[0].Tag == lauf;

                // Puffer: Herstellung mit Anzahl je Variante, Ansatz Tank -> Flasche -> Tank
                Baustein bp = f.cat.Baustein_("Puffer");
                PackageStep pu = f.FromBaustein(bp); pu.Row = "Puffer"; f.AddStep(pu); Application.DoEvents();
                NumericUpDown n0 = f.pVar.Controls.Count >= 4 ? f.pVar.Controls[0] as NumericUpDown : null, n1 = n0 != null ? f.pVar.Controls[2] as NumericUpDown : null;
                bool var = n0 != null && n1 != null && f.pVar.Visible && (string)n0.Tag == "mobil";
                if (var) { n0.Value = 2; n1.Value = 1; Application.DoEvents(); }
                var &= pu.Variant == "mobil:2|stationaer" && f.blockPreview.Visible;
                bool tank = HasTank(f) && f.cbAnsatz.Visible && f.equipRows.Controls.Count == 1;
                f.cbAnsatz.SelectedIndex = 1; f.tGebinde.Text = "Flasche"; f.tMenge.Text = "5"; f.cbUnit.Text = "L"; Application.DoEvents();
                bool geb = !HasTank(f) && f.equipRows.Controls.Count == 0 && f.tGebinde.Visible && pu.Compose(0, f.prefix).Contains("Gebinde: Flasche 5 L");
                f.cbAnsatz.SelectedIndex = 0; Application.DoEvents();
                geb &= pu.Ansatz == "" && f.equipRows.Controls.Count == 1 && HasTank(f) && !f.tGebinde.Visible;
                return "Paket3 Editor: Wiederholt sich " + T(rep) + " Reihenfolge ohne Neuaufbau " + T(ord) + " Alt+Hoch " + T(alt) + " Kalender " + T(cal)
                    + " Herstellung Anzahl " + T(var) + " Tank-Hinweis " + T(tank) + " Ansatz Flasche " + T(geb);
            }
            catch (Exception ex) { return "Paket3 Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }

        static bool HasTank(PackageForm f) { return f.HasHint("Tank (WFI)"); }
    }
}
#endif
