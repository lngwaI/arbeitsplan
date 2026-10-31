#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P3: Kampagnenstart zeigt je Zeile und Modus, was mit einem belegten Block passiert (Farbe, Text, Tooltip, Zusammenfassung),
    // auch fuer Zeilen nach Lauf 1; Paket-Editor meldet belegte Bloecke als Info; Puffer-Zeilen nennen den bedienten Lauf. Erfundene Daten.
    partial class PackageForm
    {
        public static string TestP3(IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();
            IDataStore store = P1Store();
            Package camp = P1Camp(store, 3, 2);
            camp.Lauf1 = P1D0;
            DateTime s0 = camp.StartFor(P1D0);
            List<RunDate> plan = camp.Schedule();
            int hi = camp.Steps.IndexOf(camp.Head), bi = camp.Steps.FindIndex(delegate(PackageStep x) { return x.IsBuffer; });
            RunDate first = plan[0], lauf = P1Day(plan, hi, 0);
            P1Put(store, s0.AddDays(first.Day - 1), first.Row.Length > 0 ? first.Row : camp.Steps[first.Step].Row, "Altbestand vorn");
            P1Put(store, s0.AddDays(lauf.Day - 1), lauf.Row.Length > 0 ? lauf.Row : camp.Steps[hi].Row, "Altbestand Lauf");
            int expN = camp.CountBusy(store, s0, null);

            string busyLine = ""; bool okMode = true, okTip = false, okAfter = false, okRed = true, okLine = true, okBuf = false; string bufShown = "";
            using (CampaignForm cf = new CampaignForm(camp, store, s0, ApplyMode.FillEmpty))
            {
                if (cf.TestPackVisible) cf.TestPackNo = "12340001";
                int i0 = cf.TestIndexOf(first.Step, first.Run), iL = cf.TestIndexOf(hi, 0);
                string[] txt = { "wird übersprungen", "wird überschrieben", "wird angehängt" };
                Color[] col = { Color.FromArgb(215, 215, 215), Color.FromArgb(255, 190, 120), Color.FromArgb(255, 243, 150) };
                ApplyMode[] ms = { ApplyMode.FillEmpty, ApplyMode.Overwrite, ApplyMode.Append };
                for (int m = 0; m < 3; m++)
                {
                    cf.TestMode = ms[m];
                    okMode &= cf.TestRel(i0).EndsWith(txt[m]) && cf.TestRelColor(i0) == col[m] && cf.TestRel(iL).EndsWith(txt[m]) && cf.TestRelColor(iL) == col[m];
                    okRed &= !cf.TestRowRed(i0) && !cf.TestRowRed(iL);
                    busyLine += cf.TestBusyLine + ";"; okLine &= expN >= 2 && cf.TestBusyLine == expN + " Blöcke belegt: werden " + txt[m].Substring(5);
                }
                okTip = cf.TestRelTip(i0).Contains("Altbestand vorn") && cf.TestRelTip(iL).Contains("Altbestand Lauf");
                okAfter = cf.TestRel(iL).Contains("wird angehängt");   // Zeile des Hauptlaufs (nach Lauf 1 bzw. am Lauf 1) ist belegt gezeigt
                if (bi >= 0)
                {
                    bufShown = cf.TestShown(cf.TestIndexOf(bi, 0));
                    string no = camp.LaufNoOf(1, cf.Result.Day1);
                    okBuf = no.Length == 8 && bufShown.EndsWith(" · für Lauf " + no);
                }
            }
            sb.AppendLine("P3 Kampagnenstart: drei Modi Text/Farbe " + K3(okMode) + " Tooltip zeigt Text " + K3(okTip) + " Zeile am Hauptlauf belegt " + K3(okAfter)
                + " nicht rot " + K3(okRed) + " Zusammenfassung " + K3(okLine) + (okLine ? "" : " [" + busyLine + "|" + expN + "]") + " Puffer nennt Lauf " + K3(okBuf) + (okBuf ? "" : " [" + bufShown + "]"));

            // Paket-Editor: Info nur mit "Lauf 1 am" und belegtem Block
            string oldPrefs = PrefsFile, tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "p3_prefs_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            PackageForm f = new PackageForm(store, equip, start, null, true, false);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            bool okHint = false, okNone = false, okInfo = false;
            try
            {
                f.Show(); Application.DoEvents();
                f.LoadPackage(camp.Clone()); Application.DoEvents();
                f.UpdateHints(); Application.DoEvents();
                Hint h = f.hintList.Find(delegate(Hint x) { return x.Code == "belegt"; });
                okHint = h != null && h.Text.Contains(expN + " Blöcke im Plan schon belegt");
                okInfo = h != null && h.Info && !h.Blocks;
                f.dtLauf1.Checked = false; f.Lauf1Changed(); Application.DoEvents(); f.UpdateHints(); Application.DoEvents();
                okNone = !f.hintList.Exists(delegate(Hint x) { return x.Code == "belegt"; });
            }
            finally { f.dirty = false; f.Close(); f.Dispose(); PrefsFile = oldPrefs; try { System.IO.File.Delete(tmp); } catch (Exception) { } }
            sb.AppendLine("P3 Editor: Info bei gesetztem Lauf 1 " + K3(okHint) + " nur Info " + K3(okInfo) + " ohne Lauf 1 kein Hinweis " + K3(okNone));
            return sb.ToString().TrimEnd();
        }

        static string K3(bool b) { return b ? "OK" : "FEHLER"; }
    }
}
#endif
