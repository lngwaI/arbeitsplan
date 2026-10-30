#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-7 (Usability-Review 2026-10-30): Regel-Schweregrad, Kampagnenstart (Standardschaltflaeche, Grund, Spaltenkoepfe),
    // Equipment-Export (Datumsformat), Pflichtpunkte (Klartext-Bedingung), Freeze (Zyklus), Formatleiste, --nosave im Testhaken.
    // Der Paket-Editor bei 1024x768 steht in PackageForm.TestUx7.cs. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        void SelfTestUX7(StringBuilder sb)
        {
            try { SelfTestUX7Core(sb); } catch (Exception ex) { sb.AppendLine("UX7 Dialoge: FEHLER " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine(PackageForm.TestUx7(store, equip, start));
        }

        static void Texts(Control c, List<string> into)
        {
            if (c.Text.Length > 0) into.Add(c.Text);
            DataGridView g = c as DataGridView;
            if (g != null) foreach (DataGridViewColumn col in g.Columns) into.Add(col.HeaderText);
            foreach (Control k in c.Controls) Texts(k, into);
        }

        void SelfTestUX7Core(StringBuilder sb)
        {
            // (1) Regel "Packen ohne Nummer": Info statt Hinweis, nie Sperre, mit Loesung
            Package p = Package.SampleCampaign("UX7");
            List<PackageProblem> pr = p.Check();
            PackageProblem pk = pr.Find(delegate(PackageProblem x) { return x.Code == "packen"; });
            bool okRule = pk != null && pk.Severity == Severity.Info && pk.IsInfo && !pk.Blocks && pk.Full.Contains("„Nr. für Packen“") && !Package.HasBlock(pr);

            // (2) Kampagnenstart: Enter/Esc, Grund neben dem gesperrten Knopf, Spaltenkoepfe mit Einheit
            bool okCamp;
            CampaignForm.Quiet = true;
            using (CampaignForm cf = new CampaignForm(Package.SampleCampaign("UX7"), new MemoryStore(), start, ApplyMode.FillEmpty))   // leerer Plan: keine Sperre durch vorhandene Laeufe
            {
                okCamp = cf.TestButtons && !cf.TestOkEnabled && cf.TestWhy.Contains("Laufnummer für Packen");
                cf.TestPackNo = "12340001";
                okCamp &= cf.TestOkEnabled && cf.TestWhy.Length == 0;
                okCamp &= cf.TestHeaders.Contains("Tage −|Tage +|entfällt") && !cf.TestHeaders.Contains("fällt aus");
            }
            CampaignForm.Quiet = false;

            // (3) Equipment-Export: TT.MM.JJJJ unabhaengig von der Region, "Produktcode", Enter/Esc
            bool okExp = false;
            using (EquipExportForm ef = new EquipExportForm(store, start, null))
            {
                int dp = 0; bool fmt = true, code = false;
                foreach (Control c in ef.Controls)
                {
                    DateTimePicker d = c as DateTimePicker;
                    if (d != null) { dp++; fmt &= d.Format == DateTimePickerFormat.Custom && d.CustomFormat == "dd.MM.yyyy" && new DateTime(2026, 3, 5).ToString(d.CustomFormat, CultureInfo.GetCultureInfo("en-US")) == "05.03.2026"; }
                    if (c is Label && c.Text == "Produktcode") code = true;
                }
                okExp = dp == 3 && fmt && code && ef.AcceptButton != null && ef.CancelButton != null;
            }

            // (4) Pflichtpunkte: Bedingung in Klartext (Daten bleiben "+mobil" / "!stationaer"), Enter/Esc
            DutyConfig dc = store.GetDuties();
            bool okDut;
            using (DutiesForm df = new DutiesForm(dc, store.GetRows()))
            {
                bool any = dc.Defs.Exists(delegate(CheckDef d) { return d.Cond.Length > 0; });
                int ix = dc.Defs.FindIndex(delegate(CheckDef d) { return d.Cond == "+mobil"; });
                okDut = any && df.TestButtons && ix >= 0 && df.TestCondCell(ix) == "automatisch erfüllt bei mobil"
                    && DutiesForm.CondText("!stationaer") == "entfällt bei stationär" && DutiesForm.CondText("") == "keine" && DutiesForm.CondText("+mobil,!stationaer") == "automatisch erfüllt bei mobil und entfällt bei stationär";
                DutyConfig back = df.TestCommit();
                okDut &= back != null && back.Defs.Count == dc.Defs.Count;
                for (int i = 0; back != null && i < dc.Defs.Count && i < back.Defs.Count; i++) okDut &= back.Defs[i].Cond == dc.Defs[i].Cond;
            }

            // (5) Freeze: "Zyklus" statt "Cycle" in Dialog und Rechnung, Datenfelder unveraendert
            FreezeChoice ch = new FreezeChoice(); ch.Item = FzBag(FzD0); ch.IsNew = true; ch.Mains.Add("13570030");
            bool okFz;
            using (FreezeForm ff = new FreezeForm(ch))
            {
                List<string> all = new List<string>(); Texts(ff, all);
                string joined = string.Join("\n", all.ToArray());
                okFz = joined.Contains("Zyklus") && joined.Contains("Zyklen") && joined.IndexOf("Cycle", StringComparison.OrdinalIgnoreCase) < 0;
            }
            okFz &= FreezeCalc.Cycles(FzBag(FzD0))[0].Label == "Z1" && FzBag(FzD0).Cycles == 14
                && FreezeSet.DisplayLines(new List<FreezeItem>(new[] { FzBag(FzD0) }), false)[0].Contains("Z1 ") && FreezeSet.DisplayLines(new List<FreezeItem>(new[] { FzBag(FzD0) }), false)[0].Contains("14 Zyklen");

            // (6) Formatleiste: ausserhalb des Bearbeitens ein Hinweis in gleicher Hoehe, im Bearbeiten die Werkzeuge
            int h0 = fmtBar.Height;
            SetFormatBar(false);
            bool okFmt = fmtHint.Visible && !cbFont.Visible && !btnBold.Visible && !cbFont.Enabled && fmtBar.Height == h0;
            SetFormatBar(true);
            okFmt &= !fmtHint.Visible && cbFont.Visible && btnBold.Visible && cbFont.Enabled == me.Has(Perm.Format) && fmtBar.Height == h0;
            SetFormatBar(false);

            // (7) --nosave: Testhaken ohne unbehandelte Ausnahme, Anzeige "stationaer" -> "stationär"
            MemoryStore ro = new MemoryStore(); ro.FailSaves = true;
            bool okNo = true;
            try { PackageForm.Ux1bPrepare(ro); } catch (Exception) { okNo = false; }
            bool okVar = PlanView.VarLabel("stationaer") == "stationär" && DutiesForm.CondText("!stationaer").Contains("stationär");

            sb.AppendLine("UX7 Dialoge: Hinweis Packen ohne Nummer = Info " + Ok(okRule) + " Kampagnenstart (Enter/Esc, Grund, Spaltenköpfe) " + Ok(okCamp) + " Equipment-Export (TT.MM.JJJJ, Produktcode) " + Ok(okExp)
                + " Pflichtpunkte Klartext " + Ok(okDut) + " Freeze Zyklus " + Ok(okFz) + " Formatleiste nur beim Bearbeiten " + Ok(okFmt) + " ohne Schreibzugriff " + Ok(okNo) + " stationär-Anzeige " + Ok(okVar));
        }
    }
}
#endif
