#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests "Lego3": Korrekturen aus dem Review (Kampagnen-Zeilen bei RunEvery, Packen von Hand, belegte Karten-Tage, gaps, Handverschiebungen,
    // Laufzahl, Back-Up). Nur erfundene Daten.
    partial class PackageForm
    {
        public static string TestLego3()
        {
            try { return Lego3Core(); }
            catch (Exception ex) { return "Lego3: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
        }

        static string Lego3Core()
        {
            // (1) Kampagne: 4 Laeufe, Puffer "alle 2. Lauf", Lauf 2 faellt aus -> jeder echte Termin hat eine Zeile (Ansicht wie CampaignForm.Recalc)
            Package p = Package.SampleBuffers("L3"); PackageStep pu = p.Steps[p.Steps.Count - 1], head = p.Head;
            pu.Bind = StepBind.JeLauf; pu.RunOffset = 0; pu.RunEvery = 2; head.Skip.Add(1);
            List<RunDate> real = p.Schedule(); Package view = p.Clone();
            foreach (PackageStep s in view.Steps) { s.Skip.Clear(); s.RunEvery = 1; }
            List<RunDate> rows = view.Schedule();
            bool ok1 = real.FindAll(delegate(RunDate d) { return d.Step == p.Steps.Count - 1; }).Count == 2;
            foreach (RunDate d in real) ok1 &= rows.Exists(delegate(RunDate r) { return r.Key() == d.Key(); });

            // (2) Packen: gezogen = von Hand (Schloss, Hinweis); Lauf von Hand = Hinweis fuer Packen am Lauftag
            Package g = Package.SampleCampaign("L3"); PackageStep pk = g.Steps[0];
            bool ok2 = P1Has(g.Check(), "packen-tag", Severity.Sperre) == false;
            ok2 &= g.DragRun(pk, 0, 2, false) && pk.Pinned(0) && pk.Days(g.Day1)[0] == 3 && LegoSoft(g.Check(), "packen-tag");
            Package gb = Package.FromXml(g.ToXml()); ok2 &= gb.Steps[0].Pinned(0) && gb.Steps[0].Days(gb.Day1)[0] == 3 && LegoSoft(gb.Check(), "packen-tag");   // bleibt nach Speichern/Laden
            ok2 &= g.DragRun(pk, 0, -2, false) && !pk.Pinned(0) && pk.Days(g.Day1)[0] == 1;   // zurueckgezogen = wieder Regel
            Package g2 = Package.SampleCampaign("L3"); g2.Steps[0].Day = 3; ok2 &= P1Has(g2.Check(), "packen-tag", Severity.Sperre);
            g2.Head.Shift[0] = 0; ok2 &= LegoSoft(g2.Check(), "packen-tag");

            // (3) Karte -2 auf belegtem Block wird als belegt gemeldet
            IDataStore st = P1Store(); Package b = Package.SampleBuffers("L3"); PackageStep bu = b.Steps[b.Steps.Count - 1];
            bu.Bind = StepBind.JeLauf; bu.RunOffset = 0; bu.Puffers.Add(new PufferSpec()); bu.Puffers.Add(new PufferSpec()); bu.Puffers[0].OffsetDays = -2;
            int n0 = b.CountBusy(st, P1D0, null);
            P1Put(st, P1D0, "Puffer", "Besetzt");   // Tag 1 = Lauf 1 (Tag 3) minus 2
            bool ok3 = n0 == 0 && b.CountBusy(st, P1D0, null) == 1;

            // (4) gaps: Handverschiebung hat Vorrang (Tage 3, 11 (Hand +2), 15), gaps 6, 6
            Package h = Package.SampleCampaign("L3"); h.Head.Shift[1] = 2;
            List<int> hd = new List<int>(); foreach (RunDate d in h.Schedule(new List<int>(new[] { 6, 6 }))) if (d.Step == 2) hd.Add(d.Day);
            bool ok4 = LegoJoin(hd) == "3,11,15,21";

            // (5) pufvor: ein Lauf von Hand verdeckt die Sperre eines anderen Laufs nicht
            Package v = Package.SampleBuffers("L3"); PackageStep vp = v.Steps[v.Steps.Count - 1]; int vi = v.Steps.Count - 1;
            vp.Repeat = RepeatMode.DayList; vp.DayList = "2, 3"; vp.Shift[0] = 0;
            List<RunDate> vs = v.Schedule(); P1Day(vs, vi, 0).Day = 0; P1Day(vs, vi, 1).Day = -1;
            bool ok5 = P1Has(v.Check(null, default(DateTime), ApplyMode.Overwrite, vs), "pufvor", Severity.Sperre);

            // (6) Folgelaeufe eines Umschalt-Verschiebens (Move) gelten als von Hand
            Package m = Package.SampleCampaign("L3"); m.Steps[2].Move[0] = 0;
            bool ok6 = m.Steps[2].ByHand(2) && !m.Steps[2].Pinned(2) && LegoSoft(LegoCheck(m, 2, 2, 5), "lauffolge");

            // (7) Laufzahl runter und rauf (Kampagnenstart) behaelt Handverschiebungen; Paket-Editor entfernt sie; kein Hinweis fuer nicht vorhandene Laeufe
            Package rc = Package.SampleCampaign("L3"); rc.Head.Shift[3] = 1; rc.SetRunCount(2, true); bool ok7 = rc.Head.Shift.ContainsKey(3);
            rc.SetRunCount(4, true); ok7 &= LegoSchedule(rc).Contains("2:3=22");
            Package r2 = Package.SampleCampaign("L3"); r2.Head.Shift[3] = 1; r2.SetRunCount(2); ok7 &= !r2.Head.Shift.ContainsKey(3);
            Package r3 = Package.SampleBuffers("L3"); PackageStep rp = r3.Steps[r3.Steps.Count - 1]; rp.Bind = StepBind.JeLauf; rp.RunOffset = 0; rp.Shift[3] = 1;
            r3.SetRunCount(2, true); ok7 &= !P1Any(r3.Check(), "pufnach");

            // (8) Back-Up folgt bei Laufzahl-Aenderung
            Package k = Package.SampleCampaign("L3"); PackageStep kp = k.Steps[0]; kp.Count = 4; kp.Interval = 6;
            PackageStep kb = Package.MakeBackup(kp, null); k.Steps.Add(kb); Package.SyncBackups(k.Steps); k.SetRunCount(2);
            bool ok8 = kp.Count == 2 && kb.Count == 2 && LegoJoin(kb.Days(k.Day1)) == "2,8";

            StringBuilder sb = new StringBuilder();
            sb.Append("Lego3 Korrekturen: Kampagnen-Zeilen bei Läufe-Rhythmus " + T(ok1) + " Packen von Hand " + T(ok2) + " belegt bei Karten-Versatz " + T(ok3) + " gaps mit Handverschiebung " + T(ok4)
                + " pufvor " + T(ok5) + " Umschalt-Verschiebung als Hand " + T(ok6) + " Laufzahl behält Hand " + T(ok7) + " Back-Up bei Laufzahl " + T(ok8));
            return sb.ToString();
        }
    }
}
#endif
