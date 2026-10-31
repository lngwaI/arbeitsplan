#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Paket-Editor E1b (am sichtbaren Fenster): Laufanzahl im Kopf, "zu jedem Lauf" (Puffer 1 Tag davor), "eigenes Datum" mit
    // Ende "fuer alle Laeufe" / "bis Lauf", Tabelle Wann / Laeufe-Termine / Wiederholung, Lauf 1 am, keine "Tag n"-Texte, Art-Namen,
    // Nummer von Hand, mehrzeiliges Notizfeld, Kopf (Typ, Lauf 1 am) im Paket.
    partial class PackageForm
    {
        static readonly Regex DayNo = new Regex(@"\bTag\s+-?\d", RegexOptions.None);

        // Alle sichtbaren Zeit-Texte (Tabelle, Termine, Kalenderkoepfe, Hinweise, Markierung): enthaelt einer "Tag <Zahl>"?
        string DayNoText()
        {
            List<string> all = new List<string>();
            foreach (ListViewItem it in lv.Items) { foreach (ListViewItem.ListViewSubItem sub in it.SubItems) all.Add(sub.Text); all.Add(it.ToolTipText); }
            all.Add(lDates.Text); all.Add(lDateNow.Text); all.Add(marker.Text); all.Add(lPreview.Text); all.Add(lFollow.Text);
            for (int d = 1; d <= 30; d++) { all.Add(StripHead(d, true)); all.Add(StripHead(d, false)); }
            foreach (Hint h in hintList) all.Add(h.Text);
            foreach (string t in all) if (t != null && DayNo.IsMatch(t)) return t;
            return null;
        }

        static bool W(ref string why, int n, bool ok) { if (!ok) why += n + ","; return ok; }

        static string Row3(PackageForm f, PackageStep s) { foreach (ListViewItem it in f.lv.Items) if (it.Tag == s) return it.SubItems[1].Text + "|" + it.SubItems[4].Text + "|" + it.SubItems[5].Text; return ""; }

        public static string TestE1b(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("E1b"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            string tmp = System.IO.Path.GetTempFileName();
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep packen = f.steps[0], lauf = f.steps[2], misch = f.steps[3];
                // Kopf: Laufanzahl setzt den Hauptlauf einmal, JeLauf-Schritte folgen
                bool okHead = f.pk.Head == lauf && misch.Bind == StepBind.JeLauf && f.pk.RunCountOf(misch) == 4 && f.nRuns.Value == 4 && f.nRuns.Enabled;
                f.nRuns.Value = 6; Application.DoEvents();
                okHead &= f.pk.RunCount == 6 && lauf.Until == 0 && f.pk.RunsText(misch) == "Lauf 1–6 · 6 Termine" && Row3(f, misch).Contains("Lauf 1–6 · 6 Termine") && Row3(f, lauf).Contains("Lauf 1–6 · 6 Termine");
                // Tabelle: Wann relativ zu Lauf 1, Laeufe/Termine, Wiederholung im Klartext, Reihenfolge wie im Plan
                bool okTab = Row3(f, packen) == "2 Tage vor Lauf 1|1 Termin|einmalig" && Row3(f, misch) == "1 Tag nach jedem Lauf|Lauf 1–6 · 6 Termine|zu jedem Lauf" && Row3(f, lauf) == "Lauf 1 – Lauf 6|Lauf 1–6 · 6 Termine|alle 6 Tage"
                    && f.lv.Items[0].SubItems[2].Text == "Packen" && f.lv.Columns[1].Text == "Wann" && f.lv.Columns.Count == 3 && f.lv.Items[0].SubItems[3].Text.Length > 0 && f.lv.Items[0].ToolTipText.Contains(f.lv.Items[0].SubItems[3].Text);   // Raum und Wiederholung nur im Tooltip
                lauf.SetSkip(2, true); f.Changed(); Application.DoEvents();
                okTab &= Row3(f, misch).Contains("Lauf 1, 2, 4–6 · 5 Termine");
                lauf.SetSkip(2, false); f.Changed();
                // Lauf 1 am: Daten statt Bezug, Wochentag von Tag 1 folgt
                f.dtLauf1.Checked = true; f.dtLauf1.Value = new DateTime(2027, 3, 15); f.Lauf1Changed(); Application.DoEvents();
                bool okDate = Row3(f, packen).StartsWith("Sa 13.03.2027|") && f.pk.Lauf1 == new DateTime(2027, 3, 15) && f.pk.Day1 == DayOfWeek.Saturday && Row3(f, misch).StartsWith("Di 16.03.–")
                    && f.StripHead(f.pk.Lauf1Day, true) == "Mo 15.03.";
                f.dtLauf1.Checked = false; f.Lauf1Changed(); Application.DoEvents();
                okDate &= f.pk.Lauf1 == null && Row3(f, packen).StartsWith("2 Tage vor Lauf 1|");
                // zu jedem Lauf: Puffer 1 Tag davor, Anzahl = Laufanzahl, "reicht fuer n Laeufe" nur unter Mehr Optionen
                Baustein bp = f.cat.Baustein_("Puffer");
                PackageStep pu = f.FromBaustein(bp); pu.Row = "Puffer"; f.DefaultBind(pu, bp); f.AddStep(pu); Application.DoEvents();
                List<RunDate> plan = f.Plan(); int pi = f.steps.IndexOf(pu), ok1 = 0, ok2 = 0;
                foreach (RunDate d in plan) if (d.Step == pi) { ok1++; RunDate hd = plan.Find(delegate(RunDate q) { return q.Step == 2 && q.Run == d.Run; }); if (hd != null && hd.Day - d.Day == 1) ok2++; }
                bool okJe = pu.Bind == StepBind.JeLauf && pu.RunOffset == -1 && ok1 == 6 && ok2 == 6 && f.cbBind.SelectedIndex == 0 && f.cbDir.SelectedIndex == 0 && f.nDay.Value == 1 && f.nDay.Visible && !f.nEvery.Visible && !f.cbEnd.Visible && !f.cbRepeat.Visible;
                f.TestMore(); Application.DoEvents();
                okJe &= f.nEvery.Visible;
                f.nEvery.Value = 2; Application.DoEvents();
                okJe &= pu.RunEvery == 2 && f.pk.RunsText(pu) == "Lauf 1, 3, 5 · 3 Termine";
                f.nEvery.Value = 1; f.SetMore(false);
                f.cbDir.SelectedIndex = 2; Application.DoEvents();   // nach jedem Lauf
                okJe &= pu.RunOffset == 1;
                f.nDay.Value = 2; Application.DoEvents();
                okJe &= pu.RunOffset == 2 && f.OffOf(pu) == 2;
                f.cbDir.SelectedIndex = 0; f.nDay.Value = 5; Application.DoEvents();   // 5 Tage vor jedem Lauf: schiebt Tag 1 nach vorn
                int min = int.MaxValue; foreach (RunDate d in f.Plan()) min = Math.Min(min, d.Day);
                okJe &= pu.RunOffset == -5 && min == 1 && f.OffOf(pu) == -5;
                f.nDay.Value = 1; Application.DoEvents();
                // Schritt aus dem Kalender verschieben: JeLauf ueber den Versatz, einzelner Lauf nicht moeglich
                int pd = -1; foreach (RunDate d in f.Plan()) if (d.Step == pi && d.Run == 0) pd = d.Day;
                f.MoveInCalendar(pu, 0, pd - 1, false); Application.DoEvents();
                bool okMove = pu.RunOffset == -2;
                f.MoveInCalendar(pu, 0, pd + 5, true); Application.DoEvents();   // JeLauf einzeln: nur dieser Lauf, Versatz bleibt
                okMove &= pu.RunOffset == -2 && pu.Shifted(0);
                f.ResetRun(pu, 0); Application.DoEvents();
                okMove &= !pu.Shifted(0) && pu.RunOffset == -2;
                // eigenes Datum: Wiederholung mit Ende "fuer alle Laeufe" / "bis Lauf k"; folgt der Laufanzahl
                PackageStep x = new PackageStep(); x.Title = "Zwischen"; x.Row = "Suite 1"; x.Day = f.NextDay(); f.AddStep(x); Application.DoEvents();
                f.cbRepeat.SelectedIndex = 1; f.nInterval.Value = 2; f.cbDir.SelectedIndex = 0; f.nDay.Value = 3; Application.DoEvents();   // alle 2 Tage, ab 3 Tage vor Lauf 1
                string why = ""; bool okFrei = W(ref why, 0, x.Repeat == RepeatMode.Interval && x.Count >= 2 && f.cbBind.Visible && f.cbBind.SelectedIndex == 1 && f.cbEnd.Visible && f.cbDir.Items.Count == 3);
                f.cbEnd.SelectedIndex = 0; Application.DoEvents();     // fuer alle Laeufe
                okFrei &= W(ref why, 1, x.Count == 6 && x.Until == 0 && !f.nCount.Visible && !f.nUntil.Visible);
                f.nRuns.Value = 3; Application.DoEvents();
                okFrei &= W(ref why, 2, f.pk.RunCount == 3 && x.Count == 3 && f.pk.RunsText(misch) == "Lauf 1–3 · 3 Termine");
                f.cbEnd.SelectedIndex = 2; Application.DoEvents();     // bis Lauf k
                okFrei &= W(ref why, 3, x.Until == lauf.Days(f.pk.Day1)[2] && f.nUntil.Visible && f.nUntil.Value == 3);
                f.nUntil.Value = 2; Application.DoEvents();
                okFrei &= W(ref why, 4, x.Until == lauf.Days(f.pk.Day1)[1] && f.nUntil.Value == 2 && f.strip.CountOf(x) >= 2);
                f.cbEnd.SelectedIndex = 1; f.nCount.Value = 4; Application.DoEvents();   // genau 4-mal
                okFrei &= W(ref why, 5, x.Until == 0 && x.Count == 4 && f.strip.CountOf(x) == 4);
                okFrei &= W(ref why, 6, f.OffOf(x) == -3 && f.FirstOf(x, f.Plan()) == f.pk.Lauf1Day - 3 && Row3(f, x).StartsWith("3 Tage vor Lauf 1"));
                // Bezug wechseln: eigenes Datum <-> zu jedem Lauf, Termine bleiben
                string before = Row3(f, misch); List<int> dm = new List<int>(); int mi = f.steps.IndexOf(misch); foreach (RunDate d in f.Plan()) if (d.Step == mi) dm.Add(d.Day);
                f.Bind(misch); f.cbBind.SelectedIndex = 1; Application.DoEvents();
                List<int> dm2 = new List<int>(); foreach (RunDate d in f.Plan()) if (d.Step == mi) dm2.Add(d.Day);
                bool okBind = misch.Bind == StepBind.Frei && PackageStep.JoinDays(dm) == PackageStep.JoinDays(dm2);
                f.cbBind.SelectedIndex = 0; Application.DoEvents();
                okBind &= misch.Bind == StepBind.JeLauf && misch.RunOffset == 1 && Row3(f, misch) == before;
                // Art-Namen, Nummer von Hand, mehrzeiliges Notizfeld
                f.Bind(lauf); bool okKind = f.cbKind.Items[0].ToString() == "Sonstiges (ohne Laufnummer)" && f.cbKind.Items[1].ToString() == "Hauptlauf" && f.cbKind.Items[2].ToString().StartsWith("Unterlauf") && !f.tManualNo.Visible;
                f.Bind(misch); okKind &= f.cbKind.Items[1].ToString() == "Lauf-Schritt mit Laufnummer";
                f.Bind(packen); f.TestMore(); Application.DoEvents();
                bool okNo = f.tManualNo.Visible && f.lManual.Text == "Nr. von Hand:";
                f.tManualNo.Text = "12340001"; Application.DoEvents();
                okNo &= packen.ManualNo == "12340001" && packen.Compose(0, f.prefix).Contains("(Nr. 12340001)");
                f.tManualNo.Text = "123"; Application.DoEvents();
                okNo &= packen.ManualNo == "" && f.HasHint("Laufnummer von Hand");
                f.tManualNo.Text = "";
                f.tNote.Text = "Erste Notiz\r\nZweite Notiz"; Application.DoEvents();
                Entry ne = packen.EntryFor(0, f.prefix);
                bool okNote = f.tNote.Multiline && f.tNote.AcceptsReturn && packen.NoteTexts().Count == 2 && Notes.NoteTexts(ne).Count == 2 && f.lNote.Text.StartsWith("Notizen – stehen bei jedem Lauf");
                f.tNote.Text = ""; f.SetMore(false);
                // Kein "Tag <Zahl>" in den Zeit-Texten (ohne und mit Datum)
                string bad = f.DayNoText();
                f.dtLauf1.Checked = true; f.dtLauf1.Value = new DateTime(2027, 3, 15); f.Lauf1Changed(); Application.DoEvents();
                bool okTxt = bad == null && f.DayNoText() == null;
                // Kopf (Typ, Lauf 1 am) im Paket
                Package col = f.Collect();
                bool okPkg = col != null && col.Lauf1 == new DateTime(2027, 3, 15) && col.Type == PackageType.Kampagne && col.RunCount == 3;
                if (col != null) { f.cbType.SelectedIndex = 1; col = f.Collect(); col.Save(tmp); Package back = Package.Load(tmp); okPkg &= col.Type == PackageType.Einzel && back.Type == PackageType.Einzel && back.Lauf1 == col.Lauf1 && back.RunCount == 3; Package.Seen.Remove(back); }   // nicht in den Golden-Test (nur alte Pakete) aufnehmen
                f.dirty = false;
                return "E1b Paket-Editor: Kopf Laufanzahl=" + T(okHead) + " Tabelle=" + T(okTab) + " Lauf 1 am=" + T(okDate) + " zu jedem Lauf=" + T(okJe) + " Kalender verschieben=" + T(okMove)
                    + " eigenes Datum/Ende=" + T(okFrei) + (okFrei ? "" : " [" + why + "]") + " Bezug wechseln=" + T(okBind) + " Art-Namen=" + T(okKind) + " Nummer von Hand=" + T(okNo) + " Notizen mehrzeilig=" + T(okNote)
                    + " keine Tag-Nummern=" + T(okTxt) + (bad != null ? " [" + bad + "]" : "") + " Typ/Lauf 1 im Paket=" + T(okPkg);
            }
            catch (Exception ex) { return "E1b Paket-Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { f.dirty = false; f.Close(); f.Dispose(); try { System.IO.File.Delete(tmp); } catch { } }
        }
    }
}
#endif
