#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-5e am sichtbaren Paket-Editor: Aenderungen bauen den Block nicht neu auf (gleiche Control-Instanzen, Fokus bleibt, keine
    // Bind-Aufrufe), Tausch per ▲▼ ohne Neuaufbau, Zeile hinzufuegen/entfernen nur lokal, Textfelder gebuendelt (ein UpdateView je Tippserie),
    // Resize ohne Neuaufbau, Listen-/Kalender-Verschieben, Messwerte (Stopwatch). Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        // Puffer-Schritt mit 3 Karten a 4 Zeilen (Ansatztank, VT, Tween, Pumpe)
        public static Package Ux5eSample(string name)
        {
            Package p = Ux1bSample(name, false);
            PackageStep pu = p.Steps[p.Steps.Count - 1];
            pu.Puffers.Clear(); pu.PufferShared.Clear();
            string[] typ = { "mobil", "mobil", "stationaer" };
            for (int k = 0; k < 3; k++)
                pu.Puffers.Add(UxP("Lösung " + (char)('A' + k), typ[k], UxA("Ansatztank", "AT-" + (k + 1)), UxA("VT", "VT-" + (k % 2 + 1)), UxA("Tween", k == 0 ? "TW-1" : ""), UxA("Pumpe", "")));
            pu.SyncPuffers();
            return p;
        }

        static bool SameSet(List<EquipRow> a, List<EquipRow> b)
        {
            if (a.Count != b.Count) return false;
            foreach (EquipRow r in a) if (!b.Contains(r)) return false;
            return true;
        }

        static string F1(double v) { return v.ToString("0.0"); }

        public static string TestUx5e(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux5eSample("UX5e");
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            int stage = 0;
            try
            {
                f.Show(); Application.DoEvents();
                const int N = 10;
                f.TestSelectBuffer(); Application.DoEvents();
                PackageStep pu = f.cur;
                int paints = 0, layouts = 0;   // Paint/Layout der Karten
                f.pufCards.Paint += delegate { paints++; }; f.pufCards.Layout += delegate { layouts++; };
                foreach (PufferCard pc in f.cards) pc.Paint += delegate { paints++; };
                Stopwatch sw = new Stopwatch();

                stage = 1; // (1) Schrittwechsel hin und zurueck: Zeit, Karten und Zeilen bleiben dieselben Steuerelemente
                PufferCard c0 = f.cards[0]; List<EquipRow> r0 = c0.TestRows;
                int puIx = f.Ordered().IndexOf(pu), normIx = f.Ordered().FindIndex(delegate(PackageStep x) { return x.Equipment.Count >= 3; });
                f.PfReset(); double msToNormal = 0, msToBuffer = 0;
                for (int i = 0; i < N; i++)
                {
                    sw.Restart(); f.SelectStep(normIx); Application.DoEvents(); msToNormal += sw.Elapsed.TotalMilliseconds;
                    sw.Restart(); f.SelectStep(puIx); Application.DoEvents(); msToBuffer += sw.Elapsed.TotalMilliseconds;
                }
                msToNormal /= N; msToBuffer /= N;
                bool okRound = f.cards.Count == 3 && f.cards[0] == c0 && SameSet(c0.TestRows, r0) && f.cur == pu;

                stage = 2; // (2) Feldaenderung in einer Karte (Typ): kein Bind, gleiche Instanzen, Fokus bleibt
                f.ActiveControl = f.lv;
                f.PfReset();
                int p0 = paints, l0 = layouts;
                sw.Restart();
                for (int i = 0; i < N; i++) c0.TestTyp.SelectedIndex = i % 2;
                double msField = sw.Elapsed.TotalMilliseconds / N;
                double msView = f.PfMs(PfView) / Math.Max(1, f.PfCount[PfView]), msPrev = f.PfMs(PfPreview) / Math.Max(1, f.PfCount[PfPreview]), msStrip = f.PfMs(PfStrip) / Math.Max(1, f.PfCount[PfStrip]);
                double msHints = f.PfMs(PfHints) / Math.Max(1, f.PfCount[PfHints]), msLay = f.PfMs(PfLayout) / Math.Max(1, f.PfCount[PfLayout]);
                int bindField = f.PfCount[PfBind];
                Application.DoEvents();
                bool okFieldSame = f.cards[0] == c0 && f.cards.Count == 3 && SameSet(c0.TestRows, r0);
                f.ActiveControl = c0.TestTyp; Application.DoEvents();
                bool okFocus = f.ActiveControl == c0.TestTyp;
                int paintField = paints - p0, layField = layouts - l0;
                sw.Restart();
                for (int i = 0; i < N; i++) f.strip.Refresh();
                double msStripPaint = sw.Elapsed.TotalMilliseconds / N;

                stage = 3; // (3) Tausch ▲▼ in der Karte: gleiche Zeilen-Controls, nur Reihenfolge, Fokus am Knopf, Modell tauscht, ein Undo-Schritt
                f.ActiveControl = f.lv; f.TestCommit();
                List<EquipRow> before = c0.TestRows;
                string ord0 = Shorts(pu.Puffers[0].Lines);
                int snaps = f.UndoCount;
                before[1].PinHot = true; before[1].SetHot(true);
                f.ActiveControl = before[1].TestDown;
                f.PfReset(); p0 = paints; l0 = layouts;
                sw.Restart();
                before[1].TestDown.PerformClick();
                double msSwap1 = sw.Elapsed.TotalMilliseconds;
                bool okSwapSame = f.cards[0] == c0 && SameSet(c0.TestRows, before) && c0.TestRows[2] == before[1] && c0.TestRows[1] == before[2];
                bool okSwapModel = Shorts(pu.Puffers[0].Lines) != ord0 && f.UndoCount == snaps + 1 && f.dirty;
                bool okSwapFocus = f.ActiveControl == before[1].TestDown || f.ActiveControl == before[1].TestUp;
                Application.DoEvents();
                sw.Restart();
                for (int i = 0; i < N; i++) c0.TestRows[i % 2 == 0 ? 1 : 2].TestUp.PerformClick();
                double msSwap = (sw.Elapsed.TotalMilliseconds + msSwap1) / (N + 1);
                int bindSwap = f.PfCount[PfBind];
                Application.DoEvents();
                int paintSwap = paints - p0, laySwap = layouts - l0;
                before[1].PinHot = false; before[1].SetHot(false);
                okSwapSame &= SameSet(c0.TestRows, before) && f.cards[0] == c0;

                stage = 4; // (4) Zeile hinzufuegen/entfernen (Karte und normaler Schritt): nur diese Zeile kommt/geht, die anderen bleiben, kein Bind
                f.PfReset();
                c0.AddLine("Pumpe"); Application.DoEvents();
                bool okAdd = c0.TestRows.Count == before.Count + 1 && before.TrueForAll(delegate(EquipRow x) { return c0.TestRows.Contains(x); }) && f.PfCount[PfBind] == 0 && pu.Puffers[0].Lines.Count == 5;
                c0.TestRows[4].TestDel.PerformClick(); Application.DoEvents();
                okAdd &= c0.TestRows.Count == before.Count && SameSet(c0.TestRows, before) && f.PfCount[PfBind] == 0 && pu.Puffers[0].Lines.Count == 4 && f.cards[0] == c0;
                f.SelectStep(normIx); Application.DoEvents();
                List<EquipRow> eq = new List<EquipRow>(f.eqRows); int n = eq.Count;
                f.PfReset();
                f.AddPosition("Pumpe"); Application.DoEvents();
                bool okEqAdd = f.eqRows.Count == n + 1 && eq.TrueForAll(delegate(EquipRow x) { return f.eqRows.Contains(x); }) && f.PfCount[PfBind] == 0;
                f.eqRows[n].TestDel.PerformClick(); Application.DoEvents();
                okEqAdd &= f.eqRows.Count == n && SameSet(f.eqRows, eq) && f.PfCount[PfBind] == 0;
                EquipRow e1 = f.EqRow(1); e1.PinHot = true; e1.SetHot(true);
                e1.TestUp.PerformClick(); Application.DoEvents();
                bool okEqSame = f.eqRows.Count == n && f.eqRows[0] == eq[1] && f.eqRows[1] == eq[0] && f.PfCount[PfBind] == 0;
                e1.PinHot = false; e1.SetHot(false);
                f.TestUndo(-1); f.TestSelectBuffer(); Application.DoEvents();
                pu = f.cur; c0 = f.cards[0];

                stage = 5; // (5) Tippen: Textserie wird gebuendelt (hoechstens ein UpdateView), Modell sofort aktuell; Produktfeld bindet erst nach Ruhe neu
                f.ActiveControl = c0.TestName; f.PfReset();
                for (int i = 0; i < 5; i++) c0.TestName.Text = "Lösung X" + i;
                bool okModel = pu.Puffers[0].Name == "Lösung X4";
                for (int i = 0; i < 12; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
                bool okDebounce = f.PfCount[PfView] == 1 && f.PfCount[PfBind] == 0;
                f.ActiveControl = f.tProduct; f.PfReset();
                for (int i = 0; i < 3; i++) f.tProduct.Text = "AB" + (i + 1);
                bool okProdWait = f.PfCount[PfBind] == 0;
                for (int i = 0; i < 12; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
                okDebounce &= okProdWait && f.PfCount[PfBind] == 1;
                f.ActiveControl = f.lv;

                stage = 6; // (6) Fenster verkleinern/vergroessern: kein Neuaufbau (Bind), gleiche Instanzen; beim Ziehen kein Layout der Details, am Ende einmal
                List<EquipRow> beforeR = f.cards[0].TestRows; PufferCard cardR = f.cards[0];
                f.PfReset();
                Size cs = f.ClientSize;
                sw.Restart();
                for (int i = 1; i <= 4; i++) { f.ClientSize = new Size(cs.Width - 20 * i, cs.Height - 8 * i); Application.DoEvents(); }
                for (int i = 4; i >= 0; i--) { f.ClientSize = new Size(cs.Width - 20 * i, cs.Height - 8 * i); Application.DoEvents(); }
                double msResize = sw.Elapsed.TotalMilliseconds / 9;
                bool okResize = f.PfCount[PfBind] == 0 && f.cards[0] == cardR && SameSet(f.cards[0].TestRows, beforeR);
                f.PfReset(); f.OnResizeBegin(EventArgs.Empty);
                sw.Restart();
                for (int i = 1; i <= 5; i++) { f.ClientSize = new Size(cs.Width - 15 * i, cs.Height - 6 * i); Application.DoEvents(); }
                double msDrag = sw.Elapsed.TotalMilliseconds / 5; int layDrag = f.PfCount[PfLayout];
                sw.Restart(); f.OnResizeEnd(EventArgs.Empty); Application.DoEvents();
                double msEnd = sw.Elapsed.TotalMilliseconds; int layEnd = f.PfCount[PfLayout] - layDrag;
                f.ClientSize = cs; Application.DoEvents();
                okResize &= layDrag == 0 && layEnd >= 1 && f.PfCount[PfBind] == 0 && f.cards[0] == cardR;

                stage = 7; // (7) Ablaufliste/Kalender: Schritt verschieben baut nichts neu auf (Listeneintraege bleiben, kein Bind fuer den gewaehlten Schritt)
                f.SelectStep(normIx); Application.DoEvents();
                ListViewItem it0 = f.lv.Items[0]; int items = f.lv.Items.Count;
                f.PfReset();
                sw.Restart();
                for (int i = 0; i < N; i++) { f.MoveStep(i % 2 == 0 ? 1 : -1); }
                double msList = sw.Elapsed.TotalMilliseconds / N; Application.DoEvents();
                bool okList = f.lv.Items.Count == items && f.lv.Items[0] == it0 && f.PfCount[PfBind] == 0;
                PackageStep cs0 = f.cur; List<RunDate> plan = f.Plan(); int si = f.steps.IndexOf(cs0), at = -1, run = 0;
                foreach (RunDate d in plan) if (d.Step == si) { at = d.Day; run = d.Run; break; }
                f.PfReset();
                sw.Restart();
                if (at > 0) { f.MoveInCalendar(cs0, run, at + 1, false); f.MoveInCalendar(cs0, run, at, false); }
                double msCal = sw.Elapsed.TotalMilliseconds / 2; Application.DoEvents();
                okList &= f.PfCount[PfBind] == 0 && f.lv.Items[0] == it0 && f.cur == cs0;

                bool okTime = msField < 120 && msSwap < 120 && msToBuffer < 400 && msToNormal < 400 && msList < 200 && msCal < 200;
                string nums = " [Messung ms: Schrittwechsel zum Puffer-Schritt " + F1(msToBuffer) + ", zum normalen Schritt " + F1(msToNormal) + ", Feldänderung " + F1(msField)
                    + " (UpdateView " + F1(msView) + ": Vorschau " + F1(msPrev) + ", Kalender " + F1(msStrip) + ", Hinweise " + F1(msHints) + ", Layout " + F1(msLay) + "), Kalender-Zeichnen " + F1(msStripPaint)
                    + ", Tausch ▲▼ " + F1(msSwap) + ", Ablaufliste ▲▼ " + F1(msList) + ", Kalender verschieben " + F1(msCal) + ", Resize je Schritt " + F1(msResize) + ", Ziehen je Schritt " + F1(msDrag) + ", Ende " + F1(msEnd)
                    + "; Bind-Aufrufe Feld " + bindField + " Tausch " + bindSwap + "; Paint/Layout der Karten Feld " + paintField + "/" + layField + " Tausch (" + (N + 1) + "x) " + paintSwap + "/" + laySwap + "]";
                return "UX5e Editor: Schrittwechsel gleiche Karten " + T(okRound) + " Feldänderung ohne Neuaufbau " + T(okFieldSame && bindField == 0) + " Fokus bleibt " + T(okFocus)
                    + " Tausch gleiche Controls " + T(okSwapSame && bindSwap == 0) + " Tausch Modell/Undo " + T(okSwapModel) + " Tausch Fokus " + T(okSwapFocus)
                    + " Zeile mehr/weniger lokal " + T(okAdd && okEqAdd) + " normaler Schritt gleiche Zeilen " + T(okEqSame)
                    + " Textserie gebündelt " + T(okModel && okDebounce) + " Resize ohne Neuaufbau " + T(okResize) + " Liste/Kalender ohne Neuaufbau " + T(okList)
                    + " Zeiten unter Schwelle " + T(okTime) + nums;
            }
            catch (Exception ex) { return "UX5e Editor: Ausnahme (Abschnitt " + stage + ") " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Replace("\r", "").Replace("\n", " / "); }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
        }
    }
}
#endif
