#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5a im Paket-Editor: Packen-Nr. im Kopf (uebernehmen, nachziehen, Abweichung, Kampagnenstart vorbelegt),
    // "Mehr Optionen" (Zusaetze ueber den Karten), Kennung (nur Lauf-/Unterlauf, Beschriftung), Knopf "Pufferpakete verwalten…".
    // Aufgerufen von MainForm.SelfTestUX5a.cs. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        internal void TestShrinkPane() { paneWant = PaneMin; ClampPane(); }

        static List<PackageStep> PackenSteps(Package p) { return p.HandNoSteps(); }

        public static string TestUx5a(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();
            PackageForm.Ux1bPrepare(store);
            Package sample = Ux1bSample("UX5a", false);
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            PackageForm n = null;
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                // (1) Packen-Nr. im Kopf: sichtbar, Eingabe geht in alle Packen-Schritte, Aenderung zieht nach, Abweichung bleibt
                List<PackageStep> pack = f.PackSteps();
                bool okHead = pack.Count >= 1 && f.packShow && f.tPackNo.Visible && f.lPackNo.Visible && f.lPackNo.Text == "Nr. für Packen:" && f.tPackNo.Text == "" && pack[0].ManualNo == "";
                f.tPackNo.Text = "12340001"; Application.DoEvents();
                bool okTake = pack.TrueForAll(delegate(PackageStep s) { return s.ManualNo == "12340001"; }) && f.dirty;
                f.tPackNo.Text = "12340002"; Application.DoEvents();
                okTake &= pack.TrueForAll(delegate(PackageStep s) { return s.ManualNo == "12340002"; });
                f.Bind(pack[0]); Application.DoEvents();   // Schritt zeigt den Kopfwert, Hinweis "wie Kopf"
                bool okStep = f.tManualNo.Text == "12340002" && f.lManualHint.Text.Contains("wie Nr. für Packen im Kopf") && f.lManualHint.Visible;
                f.tManualNo.Text = "12349999"; Application.DoEvents();   // Ueberschreibung im Schritt
                okStep &= pack[0].ManualNo == "12349999" && f.lManualHint.Text.StartsWith("weicht vom Kopf ab") && f.tPackNo.Text == "12340002";
                f.tPackNo.Text = "12340003"; Application.DoEvents();
                okStep &= pack[0].ManualNo == "12349999" && f.tManualNo.Text == "12349999";   // Abweichung bleibt
                f.tManualNo.Text = ""; Application.DoEvents();   // leer = wie Kopf
                okStep &= pack[0].ManualNo == "12340003" && f.lManualHint.Text.Contains("wie Nr. für Packen");
                PackageStep np = new PackageStep(); np.Title = "Packen"; np.Source = Package.PackenName; np.Row = "Suite 2"; np.Day = 2;   // vor Lauf 1 (Regel packen-tag)
                f.AddStep(np); Application.DoEvents();
                okTake &= np.ManualNo == "12340003";
                f.tPackNo.Text = "1234"; Application.DoEvents();   // ungueltig: Hinweis, Schritte unveraendert
                bool okBad = f.PackNoError() != null && f.hintList.Exists(delegate(Hint h) { return h.Field == f.tPackNo; }) && np.ManualNo == "12340003" && f.tPackNo.ForeColor == Color.Firebrick;
                f.tPackNo.Text = "12340004"; Application.DoEvents();
                okBad &= f.PackNoError() == null && np.ManualNo == "12340004" && !f.hintList.Exists(delegate(Hint h) { return h.Field == f.tPackNo; });

                // (2) Speichern/Laden: der Kopfwert steckt in den Packen-Schritten und kommt wieder; Kampagnenstart vorbelegt, ohne Zusatzfrage
                Package col = f.Collect();
                bool okRound = col != null && PackenSteps(col).TrueForAll(delegate(PackageStep s) { return s.ManualNo.Length > 0; });
                if (col != null)
                {
                    n = new PackageForm(store, equip, start, col, true, true); n.Show(); Application.DoEvents();
                    okRound &= n.tPackNo.Text == "12340004" && n.packShow;
                }
                bool okCamp = false;
                if (col != null)
                {
                    bool q = CampaignForm.Quiet; CampaignForm.Quiet = true;
                    using (CampaignForm cf = new CampaignForm(col, store, start, ApplyMode.FillEmpty))
                    {
                        okCamp = cf.TestPackVisible && cf.TestPackNo == "12340004" && cf.PackWarning == null && cf.TestOkEnabled;   // sofort uebernehmbar
                        cf.TestPackNo = "12340077";
                        okCamp &= cf.TestOkEnabled && PackenSteps(cf.Result).TrueForAll(delegate(PackageStep s) { return s.ManualNo == "12340077"; });
                        cf.TestPackNo = "";
                        okCamp &= !cf.TestOkEnabled && cf.PackWarning != null;   // Pflicht nur, wenn leer
                    }
                    CampaignForm.Quiet = q;
                }

                // (3) Einzelpaket: Laufnummer im Kopf
                Package ep = new Package(); ep.Name = "Einzel"; ep.Type = PackageType.Einzel;
                PackageStep es = new PackageStep(); es.Title = "Packen"; es.Source = Package.PackenName; es.Row = "Suite 1"; es.Day = 1; ep.Steps.Add(es);
                using (PackageForm e = new PackageForm(store, equip, start, ep, true, true))
                {
                    e.Show(); Application.DoEvents();
                    bool okEinzel = e.packShow && e.lPackNo.Text == "Laufnummer:";
                    e.tPackNo.Text = "12340005"; Application.DoEvents();
                    okEinzel &= e.PackSteps()[0].ManualNo == "12340005";
                    e.dirty = false; e.Close();
                    sb.AppendLine("UX5a Packen-Nr.: Kopffeld " + T(okHead) + " übernehmen/nachziehen " + T(okTake) + " Schritt (wie Kopf/Abweichung) " + T(okStep) + " ungültig " + T(okBad)
                        + " Speichern/Laden " + T(okRound) + " Kampagnenstart vorbelegt " + T(okCamp) + " Einzelpaket " + T(okEinzel));
                }

                // (4) Mehr Optionen: Zusaetze direkt unter dem Knopf, ueber den Puffer-/Equipment-Karten
                f.TestSelectBuffer(); f.TestMore(); Application.DoEvents();
                bool okMore = f.cur != null && f.cur.IsBuffer && f.gExtra.Visible && f.gPuf.Visible && f.bMore.Bottom <= f.gExtra.Top && f.gExtra.Bottom <= f.gPuf.Top && f.body.Height >= f.gPuf.Bottom;
                f.SelectStep(f.Ordered().FindIndex(delegate(PackageStep s) { return s.Kind == StepKind.Run; })); f.TestMore(); Application.DoEvents();
                okMore &= f.gExtra.Visible && f.gEquip.Visible && f.bMore.Bottom <= f.gExtra.Top && f.gExtra.Bottom <= f.gEquip.Top && f.body.Height >= f.gEquip.Bottom;
                f.SetMore(false); Application.DoEvents();
                okMore &= !f.gExtra.Visible && f.gEquip.Top >= f.bMore.Bottom;

                // (5) Kennung: nur Lauf-/Unterlauf, nie bei Puffern; Tabelle nur beim Lauf; Beschriftung und Hinweis
                f.SetMore(true); f.TestSelectBuffer(); Application.DoEvents();
                bool okId = !f.lIdent.Visible && !f.tIdent.Visible && !f.lIdentHelp.Visible && !f.gridId.Visible;
                PackageStep run = f.Ordered().Find(delegate(PackageStep s) { return s.Kind == StepKind.Run; });
                f.Bind(run); Application.DoEvents();
                okId &= f.lIdent.Visible && f.tIdent.Visible && f.lIdentHelp.Visible && f.lIdent.Text == "PA-/Kennungsnummer:" && f.lIdentHelp.Text.Contains("PA 40010001")
                    && f.lIdentHelp.Text.Contains("rote letzte Zeile") && f.lIdentHelp.Text.Contains("+1") && f.lIdentHelp.Text.Contains("Lücke") && f.gridId.Visible;
                PackageStep other = f.Ordered().Find(delegate(PackageStep s) { return s.Kind == StepKind.Normal && !s.IsBuffer && s.Title.Length > 0 && s.IdentStart.Length == 0; });
                if (other != null) { f.Bind(other); Application.DoEvents(); okId &= !f.gridId.Visible && !f.tIdent.Visible; }
                else okId = false;
                f.cbKind.SelectedIndex = (int)StepKind.SubRun; Application.DoEvents();   // Art Unterlauf: Kennung da, Tabelle (nur Lauf-Schritte) nicht
                okId &= f.tIdent.Visible && !f.gridId.Visible;
                f.dirty = false;

                // (6) Knopf "Pufferpakete verwalten…" im Puffer-Abschnitt neben "Puffer für Produkt…"
                f.TestSelectBuffer(); Application.DoEvents();
                bool okBtn = f.bPufMgr.Visible && f.bPufMgr.Text == "Pufferpakete verwalten…" && f.bPufProd.Visible && f.bPufMgr.Parent == f.gPuf && f.bPufMgr.Bottom <= f.pufCards.Top && f.bPufMgr.Left >= 0 && f.bPufMgr.Right <= f.gPuf.Width;
                sb.AppendLine("UX5a Schritt-Details: Mehr Optionen über den Karten " + T(okMore) + " Kennung (nur Lauf/Unterlauf, Beschriftung, Tabelle) " + T(okId) + " Knopf Pufferpakete verwalten " + T(okBtn));
            }
            catch (Exception ex) { sb.AppendLine("UX5a Editor: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample);
                if (n != null) { n.dirty = false; n.Close(); n.Dispose(); }
            }
            return sb.ToString();
        }
    }
}
#endif
