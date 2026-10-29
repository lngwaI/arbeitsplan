#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E1c: Wochentag von Tag 1 im Paketformat 7, Kampagnenstart (Lauf 1 am, Laeufe, Ausfall, Laufnummer Packen, belegte Bloecke, Einzelpaket)
    partial class MainForm
    {
        static string E1cText(Package p, IDataStore ms, DateTime from, int days, int rowId)
        {
            StringBuilder sb = new StringBuilder();
            for (int d = 0; d < days; d++) { Entry e = ms.Get(from.AddDays(d), rowId); if (e != null) sb.Append(e.Content).Append('|'); }
            return sb.ToString();
        }

        void E1cWeekday(StringBuilder sb)
        {
            // Normalize hat den Ursprung verschoben (Tag 1 = Samstag): Wochentage der Wiederholung muessen nach Speichern+Laden gleich sein
            Package n = new Package(); n.Name = "Wochentage"; n.Lauf1 = new DateTime(2027, 3, 10);
            PackageStep nh = E1Step(n, "Lauf", "Suite 1", 3, StepKind.Run, 2, 7), nw = E1Step(n, "Wochen", "Puffer", 1, StepKind.Normal, 3, 1);
            nw.Repeat = RepeatMode.Weekdays; nw.WeekDays = 1 | 4 | 16;
            bool okMove = n.MoveStep(nw, -2) && n.Day1 == DayOfWeek.Saturday;
            string path = Path.Combine(Path.GetTempPath(), "selftest_e1c_weekday.apkg");
            string before = E1Str(n.Schedule());
            n.Save(path);
            string xml = File.ReadAllText(path);
            Package q = Package.Load(path);
            DateTime t1 = q.StartFor(q.Lauf1.Value);
            bool okRt = okMove && xml.Contains("tag1wochentag=\"Sa\"") && q.Day1 == DayOfWeek.Saturday && E1Str(q.Schedule()) == before && q.Steps[1].WeekDays == n.Steps[1].WeekDays
                && E1Dates(q, 1, t1) == "06.03,08.03,10.03" && E1Dates(q, 0, t1) == "10.03,17.03" && !q.Normalize();
            Package m = new Package(); E1Step(m, "Lauf", "Suite 1", 1, StepKind.Run, 2, 7); m.Save(path);   // Montag: kein Attribut, Rundweg gleich
            bool okMon = !File.ReadAllText(path).Contains("tag1wochentag") && Package.Load(path).Day1 == DayOfWeek.Monday;
            sb.AppendLine("E1c Wochentag von Tag 1: Rundweg nach Normalize=" + Ok(okRt) + " Montag ohne Attribut=" + Ok(okMon));
        }

        void E1cCampaign(StringBuilder sb)
        {
            CampaignForm.Quiet = true;
            DateTime mo = D(2031, 3, 3);   // Montag
            Package sp = Package.SampleCampaign("E1c"); sp.Migrate();   // Lauf 4x, Mischen/Backfill zu jedem Lauf
            sp.Lauf1 = null;

            // Lauf 1 am, Laeufe aendern (Kopie), Bezug
            bool okRuns, okRel;
            using (CampaignForm cf = new CampaignForm(sp, new MemoryStore(), mo, ApplyMode.FillEmpty))
            {
                int rows0 = cf.TestRowCount;
                okRel = cf.TestLauf1 == mo.AddDays(2) && cf.Start == mo && cf.TestRel(0) == "2 Tage vor Lauf 1" && cf.TestRel(cf.TestIndexOf(2, 0)) == "Lauf 1" && cf.TestRel(cf.TestIndexOf(3, 0)) == "1 Tag nach Lauf 1";
                cf.TestLauf1 = D(2031, 3, 10);
                okRel &= cf.Start == D(2031, 3, 8) && cf.Schedule[0].Day == 1 && cf.TestStartField();
                cf.TestRuns = 2;
                int rows2 = cf.TestRowCount;
                cf.TestRuns = 6;
                okRuns = rows0 == 18 && rows2 == 10 && cf.TestRowCount == 26 && cf.Result.RunCount == 6 && sp.RunCount == 4 && cf.Result != sp
                    && cf.Schedule.Exists(delegate(RunDate r) { return r.Label.EndsWith(" 12340006"); });
            }

            // Ausfall: Luecke bei der Laufnummer, JeLauf-Schritte entfallen, Uebernahme in den Plan
            bool okSkip, okApply, okLast;
            MemoryStore ms = new MemoryStore(); int suite1 = ms.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id;
            using (CampaignForm cf = new CampaignForm(sp, ms, mo, ApplyMode.FillEmpty))
            {
                cf.TestPackNo = "12340001";
                int lauf3 = cf.TestIndexOf(2, 2), misch3 = cf.TestIndexOf(3, 2), n0 = cf.Schedule.Count;
                cf.SetSkip(lauf3, true);
                List<RunDate> sch = cf.Schedule;
                okSkip = cf.TestSkipped(lauf3) && cf.TestSkipped(misch3) && sch.Count == n0 - 4 && !sch.Exists(delegate(RunDate r) { return r.Label.IndexOf("12340003") >= 0; })
                    && sch.Exists(delegate(RunDate r) { return r.Label.EndsWith(" 12340004"); }) && cf.TestRel(cf.TestIndexOf(2, 3)) == "Lauf 4" && cf.Result.Steps[2].Skips(2) && !sp.Steps[2].Skips(2);
                cf.SetSkip(misch3, true);   // nur der Hauptlauf selbst laesst sich herausnehmen
                okSkip &= cf.Schedule.Count == n0 - 4;
                string msg = cf.Result.Apply(ms, cf.Start, ApplyMode.Overwrite, sch);
                string all = E1cText(sp, ms, cf.Start, 60, suite1);
                okApply = all.IndexOf("Lauf – Auftrag/Netzwerk-ID 12340002") >= 0 && all.IndexOf("12340003") < 0 && all.IndexOf("12340004") >= 0 && all.IndexOf("Packen (Nr. 12340001)") >= 0 && msg.Length > 0;
                int no, iv; DateTime day;
                okLast = Package.LastRun(ms, "1234", out no, out day, out iv) && no == 4 && Package.PastRuns(ms, "1234").Count == 3;   // 1, 2, 4: Packen (Nr.) zaehlt nicht
                cf.SetSkip(lauf3, false);
                okSkip &= cf.Schedule.Count == n0 && !cf.TestSkipped(lauf3) && !cf.TestSkipped(misch3);
            }
            sb.AppendLine("E1c Kampagnenstart: Lauf 1 am/Bezug=" + Ok(okRel) + " Läufe ändern (Kopie)=" + Ok(okRuns) + " Ausfall-Häkchen=" + Ok(okSkip) + " Apply mit Lücke=" + Ok(okApply) + " LastRun ohne Packen-Nr.=" + Ok(okLast));
        }

        void E1cPackNo(StringBuilder sb)
        {
            CampaignForm.Quiet = true;
            DateTime mo = D(2031, 3, 3);
            Package sp = Package.SampleCampaign("E1c"); sp.Migrate();
            bool okPack, okNone, okSaved;
            using (CampaignForm cf = new CampaignForm(sp, new MemoryStore(), mo, ApplyMode.FillEmpty))
            {
                bool gated = cf.TestPackVisible && !cf.TestOkEnabled && cf.PackWarning != null && cf.TestPackNo.Length == 0;
                cf.TestPackNo = "1234";
                gated &= !cf.TestOkEnabled && cf.PackWarning != null;
                cf.TestPackNo = "12340001";
                okPack = gated && cf.TestOkEnabled && cf.PackWarning == null && cf.Result.Steps[0].ManualNo == "12340001" && cf.Result.Steps[1].ManualNo == "" && sp.Steps[0].ManualNo == ""
                    && cf.Result.Steps[0].Compose(0, "1234").StartsWith("Packen (Nr. 12340001)\n");
                cf.TestPackNo = "";
                okPack &= !cf.TestOkEnabled && cf.Result.Steps[0].ManualNo == "";
            }
            Package np = new Package(); E1Step(np, "Lauf", "Suite 1", 1, StepKind.Run, 2, 3);
            using (CampaignForm cf = new CampaignForm(np, new MemoryStore(), mo, ApplyMode.FillEmpty)) okNone = !cf.TestPackVisible && cf.TestOkEnabled && cf.PackWarning == null;
            Package lp = sp.Clone(); lp.Lauf1 = D(2031, 5, 5);   // gespeichertes "Lauf 1 am" ist die Vorbelegung
            using (CampaignForm cf = new CampaignForm(lp, new MemoryStore(), mo, ApplyMode.FillEmpty)) okSaved = cf.TestLauf1 == D(2031, 5, 5) && cf.Start == D(2031, 5, 3);

            // Vorkampagne: Zeile vor Lauf 1 trifft einen belegten Block -> "belegt" und Modus "anhaengen"
            MemoryStore bs = new MemoryStore(); int s1 = bs.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id;
            Entry old = new Entry(); old.Content = "Altes Packen"; bs.Save(mo, s1, old);
            bool okBusy;
            using (CampaignForm cf = new CampaignForm(sp, bs, mo, ApplyMode.FillEmpty))
            {
                okBusy = cf.TestRel(0) == "2 Tage vor Lauf 1 · belegt" && cf.Mode == ApplyMode.Append && !cf.TestRel(cf.TestIndexOf(2, 0)).EndsWith("belegt");
            }
            using (CampaignForm cf = new CampaignForm(sp, new MemoryStore(), mo, ApplyMode.FillEmpty)) okBusy &= cf.Mode == ApplyMode.FillEmpty && cf.TestRel(0) == "2 Tage vor Lauf 1";

            // Einzelpaket: Datum, Raum, Laufnummer von Hand
            Package ee = new Package(); ee.Name = "Einzel"; ee.ProductCode = "1234"; ee.Type = PackageType.Einzel;
            PackageStep es = E1Step(ee, "Packen", "Suite 1", 1, StepKind.Normal, 1, 1); es.Source = Package.PackenName;
            MemoryStore es1 = new MemoryStore(); int r2 = es1.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 2"; }).Id;
            bool okOne;
            using (CampaignForm cf = new CampaignForm(ee, es1, mo, ApplyMode.FillEmpty))
            {
                okOne = cf.TestSimple && cf.TestPackVisible && !cf.TestOkEnabled && cf.TestRoom == "Suite 1";
                cf.TestPackNo = "12340002"; cf.TestRoom = "Suite 2"; cf.TestLauf1 = mo.AddDays(3);
                okOne &= cf.TestOkEnabled && cf.Schedule.Count == 1 && cf.Schedule[0].Row == "Suite 2" && cf.Start == mo.AddDays(3) && cf.TestSummary.IndexOf("Suite 2") >= 0;
                cf.Result.Apply(es1, cf.Start, ApplyMode.FillEmpty, cf.Schedule);
                Entry w = es1.Get(mo.AddDays(3), r2);
                okOne &= w != null && w.Content == "Packen (Nr. 12340002)";
            }
            CampaignForm.Quiet = false;
            sb.AppendLine("E1c Packen-Nummer: Pflicht (gesperrt/frei, Kopie)=" + Ok(okPack) + " ohne Packen frei=" + Ok(okNone) + " gespeichertes Lauf 1=" + Ok(okSaved) + " belegt/anhängen=" + Ok(okBusy) + " Einzelpaket=" + Ok(okOne));
        }
    }
}
#endif
