#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-5c am sichtbaren Paket-Editor: Bezug von Puffern zweigeteilt (zu jedem Lauf | eigener Rhythmus), "für Lauf n" in Termin-Liste,
    // Tabelle, Kalender-Tooltip und Hinweisen, Zeitpunkt je Karte (wie Schritt | eigener Versatz). Modell/Plan: MainForm.SelfTestUX5c.cs.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        // Kampagne (Laeufe an 3, 5, 7, 9) + Puffer-Schritt mit zwei Karten. own: eigener Rhythmus alle 3 Tage ab Tag 1 (Tage 1, 4, 7, 10);
        // sonst zu jedem Lauf einen Tag davor, Puffer 1 mit eigenem Versatz (2 Tage davor). Testbilder: --form package16 / package17.
        public static Package Ux5cSample(string name, bool own)
        {
            Package p = Ux1bSample(name, false);
            p.Steps[2].Interval = 2;
            PackageStep pu = p.Steps[p.Steps.Count - 1];
            if (own) { pu.Bind = StepBind.Frei; pu.Repeat = RepeatMode.Interval; pu.Day = 1; pu.Interval = 3; pu.Count = 4; }
            else { pu.Bind = StepBind.JeLauf; pu.RunOffset = -1; pu.Puffers[0].OffsetDays = -2; }
            pu.SyncPuffers();
            return p;
        }

        static bool HasHint(PackageForm f, string part) { return f.hintList.Exists(delegate(Hint h) { return h.Text.Contains(part); }); }

        public static string TestUx5c(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux5cSample("UX5c", true);
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            int stage = 0;
            try
            {
                f.Show(); Application.DoEvents();
                f.TestSelectBuffer(); Application.DoEvents();
                PackageStep pu = f.cur;

                stage = 1; // (1) eigener Rhythmus: Auswahl zweigeteilt, Termine mit "für Lauf n", Tabelle, Tooltip, Hinweis
                bool okBind = pu.Bind == StepBind.Frei && f.cbBind.Items.Count == 2 && f.cbBind.Items[0].ToString() == "zu jedem Lauf (fester Abstand)"
                    && f.cbBind.Items[1].ToString() == "eigener Rhythmus (unabhängig vom Lauf-Raster)" && f.cbBind.SelectedIndex == 1;
                bool okDates = f.lDates.Text.Contains("4 Termine · für Lauf 1–3") && f.lDates.Text.Contains("für Lauf 2") && f.lDates.Text.Contains("für Lauf 3") && f.cbRepeat.Visible && f.nInterval.Value == 3;
                string row = Row3(f, pu);
                bool okTab = row.Contains("für Lauf 1–3") && row.EndsWith("eigen: alle 3 Tage");
                PlanStrip.Item it = new PlanStrip.Item(); it.Step = pu; it.Run = 1; it.Day = 4;
                PlanStrip.Item last = new PlanStrip.Item(); last.Step = pu; last.Run = 3; last.Day = 10;
                bool okTip = f.StripTip(it).Contains("für Lauf 2") && f.StripTip(last).Contains("nach dem letzten Lauf");
                bool okHint = HasHint(f, "nach dem letzten Lauf");
                bool okNoTime = f.cards.Count == 2 && !f.cards[0].TestTime.Visible && !f.cards[0].ShowTiming;

                stage = 2; // (2) zu jedem Lauf: Auswahl, Kartenzeile "Zeitpunkt"; eigener Versatz je Karte (Puffer 1 drei, Puffer 2 ein Tag vor dem Lauf)
                f.cbBind.SelectedIndex = 0; Application.DoEvents();
                bool okJe = pu.Bind == StepBind.JeLauf && pu.RunOffset == -2 && f.cbBind.SelectedIndex == 0 && f.cbBind.Items[0].ToString() == "zu jedem Lauf (fester Abstand)";
                f.TestOpenCard(1); Application.DoEvents();
                PufferCard c0 = f.cards[0], c1 = f.cards[1];
                okJe &= c0.ShowTiming && c0.TestTime.Visible && c0.TestTime.SelectedIndex == 0 && !c0.TestNum.Visible && c1.TestTime.Visible && !f.pk.CardsSplit(pu, f.pk.Day1);
                c0.TestTime.SelectedIndex = 1; Application.DoEvents();
                okJe &= pu.Puffers[0].OffsetDays == -2 && c0.TestNum.Visible && c0.TestDir.Visible && !pu.HasCardOffsets;   // Start mit dem Versatz des Schritts
                c0.TestNum.Value = 3; Application.DoEvents();
                c1.TestTime.SelectedIndex = 1; Application.DoEvents(); c1.TestNum.Value = 1; Application.DoEvents(); c1.TestNum.Value = 2; Application.DoEvents(); c1.TestNum.Value = 1; Application.DoEvents();
                bool okCard = pu.Puffers[0].OffsetDays == -3 && pu.Puffers[1].OffsetDays == -1 && pu.RunOffset == -2 && f.pk.CardsSplit(pu, f.pk.Day1) && pu.CardDeltas().Count == 2;
                okCard &= f.lDates.Text.Contains("Puffer 1: 3 Tage vor dem Lauf") && f.lDates.Text.Contains("Puffer 2: 1 Tag vor dem Lauf");
                okCard &= c0.TestSummary.Contains("3 Tage vor dem Lauf");
                c1.TestDir.SelectedIndex = 2; Application.DoEvents();   // Puffer 2 nach dem Lauf: Hinweis, Zeit der Karte +1
                okCard &= pu.Puffers[1].OffsetDays == 1 && HasHint(f, "liegt nach dem Lauf, den er bedient");
                c1.TestTime.SelectedIndex = 0; Application.DoEvents();   // wie Schritt
                okCard &= !pu.Puffers[1].OffsetDays.HasValue && !HasHint(f, "liegt nach dem Lauf, den er bedient");
                f.cbBind.SelectedIndex = 1; Application.DoEvents();      // zurueck zu eigenem Rhythmus: Zeitpunkt-Zeile weg, Termine bleiben
                okJe &= pu.Bind == StepBind.Frei && !f.cards[0].TestTime.Visible;
               
                return "UX5c Editor: Bezug zweigeteilt " + T(okBind) + " Termine für Lauf n " + T(okDates) + " Tabelle " + T(okTab) + " Kalender-Tooltip " + T(okTip) + " Hinweis nach letztem Lauf " + T(okHint)
                    + " Karte ohne Zeitpunkt im eigenen Rhythmus " + T(okNoTime) + " zu jedem Lauf " + T(okJe) + " Zeitpunkt je Karte " + T(okCard);
            }
            catch (Exception ex) { return "UX5c Editor: Ausnahme (Abschnitt " + stage + ") " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Replace("\r", "").Replace("\n", " / "); }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
        }
    }
}
#endif
