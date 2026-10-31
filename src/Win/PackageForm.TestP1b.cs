#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P1b: zweites Packen mit eigenem Back-Up (PackNo) und eigener Laufnummer (Kopf, Kampagnenstart, Uebernahme). Erfundene Daten.
    partial class PackageForm
    {
        // Kampagne mit zwei Packen und je einem Back-Up (Suite 1 / Suite 2), Tag 1 und 2 vor Lauf 1 an Tag 3; auch fuer das Testbild --form campaign7
        public static Package P1bCamp(IDataStore store)
        {
            Package p = P1Camp(store, 3, 2);
            PackageStep a = p.Steps.Find(delegate(PackageStep x) { return x.IsPack; });
            a.Row = "Suite 1";
            p.Steps.Find(delegate(PackageStep x) { return x.IsBackup; }).Row = "Suite 1";
            PackageStep b = a.Clone(); b.Row = "Suite 2"; Package.AssignPackNo(p.Steps, b); p.Steps.Add(b);
            p.Steps.Add(Package.MakeBackup(b, null));
            return p;
        }

        static string P1bDays(List<RunDate> plan, int step) { RunDate d = P1Day(plan, step, 0); return d.Day + "/" + d.Row; }

        public static string TestP1b(IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();
            IDataStore store = P1Store();

            // (1) Editor ohne Admin: Packen duplizieren, einmaliger Hinweis, eigene PackNo und Nummer, zweites Kopffeld
            PackageForm f = new PackageForm(store, equip, start, null, true, false);
            f.StartPosition = FormStartPosition.Manual; f.Location = new System.Drawing.Point(0, 0);
            Package col = null;
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep pk = f.steps.Find(delegate(PackageStep x) { return x.IsPack; }), bk = f.steps.Find(delegate(PackageStep x) { return x.IsBackup; });
                bool okOne = !f.tPackNo2.Visible && !f.packShow2 && f.tPackNo.Visible;
                f.tPackNo.Text = "12340001"; Application.DoEvents();
                f.Bind(pk); f.Duplicate(); Application.DoEvents();
                PackageStep p2 = f.cur;
                PackageStep b2 = f.steps.Find(delegate(PackageStep x) { return x.BacksUp(p2); });
                bool okAdd = p2 != pk && p2.IsPack && p2.PackNo == 1 && p2.ManualNo == "" && f.stripMsg.Text == "Zweites Packen: anderen Raum und Skid wählen" && !f.UniqueBlocked(Package.PackenName);
                bool okAuto = b2 != null && b2.PackNo == 1 && p2.FirstDay == pk.FirstDay && b2.FirstDay == p2.FirstDay + 1 && !P1Any(f.StepsView().Check(), "packen-tag");
                bool okField = f.packShow2 && f.tPackNo2.Visible && f.tPackNo.Text == "12340001";
                f.tPackNo2.Text = "12340002"; Application.DoEvents();
                okField &= p2.ManualNo == "12340002" && pk.ManualNo == "12340001";

                // (2) Back-Up: allein entfernbar, wieder ans freie Packen, je Paar eigener Termin und Raum
                f.Bind(pk); Application.DoEvents();
                bool okFree = f.FreePacken() == null;
                f.Bind(b2); f.RemoveStep(false); Application.DoEvents();
                okFree &= !f.steps.Contains(b2) && f.steps.Contains(p2) && f.FreePacken() == p2;
                b2 = Package.MakeBackup(f.FreePacken(), null); f.AddStep(b2); Application.DoEvents();
                okFree &= f.FreePacken() == null && b2.PackNo == 1 && b2.BacksUp(p2) && !b2.BacksUp(pk) && bk.BacksUp(pk) && !bk.BacksUp(p2)
                    && Package.MasterOf(f.steps, b2) == p2 && Package.MasterOf(f.steps, bk) == pk;
                f.Bind(p2); f.cbRow.SelectedItem = "Suite 2"; Application.DoEvents();
                bool okRoom = p2.Row == "Suite 2" && b2.Row == "Suite 2" && bk.Row != "Suite 2";
                p2.MoveAll(1); Package.SyncBackups(f.steps);
                bool okFollow = b2.FirstDay == p2.FirstDay + 1 && bk.FirstDay == pk.FirstDay + 1 && p2.FirstDay == pk.FirstDay + 1;
                p2.MoveAll(-1); Package.SyncBackups(f.steps);

                // (3) XML-Rundlauf: Zuordnung bleibt, keine neue Meldung gegenueber einem Packen, kein Hinweis beim Oeffnen
                col = f.Collect();
                Package q = Package.FromXml(col.ToXml());
                List<PackageStep> qh = q.HandNoSteps();
                PackageStep qb2 = q.Steps.Find(delegate(PackageStep x) { return x.IsBackup && x.PackNo == 1; });
                bool okXml = col.ToXml().Contains("packnr=\"1\"") && qh.Count == 2 && qh[0].PackNo == 0 && qh[1].PackNo == 1 && qh[1].ManualNo == "12340002" && qb2 != null && qb2.BacksUp(qh[1]);
                Package solo = q.Clone(); solo.Steps.RemoveAll(delegate(PackageStep x) { return x.PackNo == 1; });
                List<PackageProblem> pq = q.Check(), ps = solo.Check();
                bool okCheck = !Package.HasBlock(pq) && !P1Any(pq, "packen") && !P1Any(pq, "packen-tag") && !P1Any(pq, "backup")
                    && pq.TrueForAll(delegate(PackageProblem x) { return P1Any(ps, x.Code); });
                using (PackageForm n = new PackageForm(store, equip, start, q, true, false))
                {
                    n.Show(); Application.DoEvents();
                    okCheck &= n.stripMsg.Text == "" && n.packShow2 && n.tPackNo.Text == "12340001" && n.tPackNo2.Text == "12340002";
                    n.dirty = false; n.Close();
                }

                // (4) Regel packen-tag gilt fuer das zweite Paar
                Package t = q.Clone(); PackageStep tp = t.HandNoSteps()[1], tb = t.Steps.Find(delegate(PackageStep x) { return x.BacksUp(tp); });
                tp.MoveAll(1); Package.SyncBackups(t.Steps);   // Back-Up am Lauftag
                int bi = t.Steps.IndexOf(tb), pi = t.Steps.IndexOf(tp);
                bool okTag = t.Check().Exists(delegate(PackageProblem x) { return x.Code == "packen-tag" && x.Blocks && x.Step == bi; });
                tp.MoveAll(1); Package.SyncBackups(t.Steps);   // Packen am Lauftag
                okTag &= t.Check().Exists(delegate(PackageProblem x) { return x.Code == "packen-tag" && x.Blocks && x.Step == pi; });

                // (5) Altes Paket: zwei Packen ohne "packnr", ein Back-Up: eindeutig, Back-Up beim ersten
                Package oldP = q.Clone(); oldP.Steps.Remove(oldP.Steps.Find(delegate(PackageStep x) { return x.IsBackup && x.PackNo == 1; }));
                Package oq = Package.FromXml(oldP.ToXml().Replace(" packnr=\"1\"", ""));
                List<PackageStep> oh = oq.HandNoSteps();
                bool okOld = oh[0].PackNo == 0 && oh[1].PackNo == 1 && Package.HasBackup(oq.Steps, oh[0]) && !Package.HasBackup(oq.Steps, oh[1]);

                // (6) Entfernen wirkt aufs Paar; Felder folgen
                f.Bind(p2); f.RemoveStep(false); Application.DoEvents();
                bool okDel = !f.steps.Contains(p2) && !f.steps.Contains(b2) && f.steps.Contains(pk) && f.steps.Contains(bk) && !f.packShow2 && !f.tPackNo2.Visible && f.tPackNo.Text == "12340001";
                f.LoadPackage(col); Application.DoEvents();
                List<PackageStep> fh = f.PackSteps();
                f.Bind(fh[0]); f.RemoveStep(false); Application.DoEvents();
                okDel &= f.PackSteps().Count == 1 && f.PackSteps()[0] == fh[1] && f.tPackNo.Text == "12340002" && !f.packShow2 && Package.BackupProblem(f.steps) == null && Package.HasBackup(f.steps, fh[1]);

                // (7) Weg ueber den Picker (AddStep): spaeter Tag wird zum Tag des ersten Packens, Paar entsteht gemeinsam
                PackageStep p3 = new PackageStep(); p3.Title = "Packen"; p3.Source = Package.PackenName; p3.Row = "Suite 1"; p3.Day = 9;
                int nBefore = f.steps.Count; f.AddStep(p3); Application.DoEvents();
                okAuto &= f.steps.Count == nBefore + 2 && p3.FirstDay == fh[1].FirstDay && Package.HasBackup(f.steps, p3) && !P1Any(f.StepsView().Check(), "packen-tag") && f.cur == p3;

                sb.Append("P1b Editor: ein Packen ein Feld " + T(okOne) + " zweites ohne Admin + Hinweis " + T(okAdd) + " Tag und Back-Up automatisch " + T(okAuto) + " Raum zieht mit " + T(okRoom) + " zweites Feld " + T(okField) + " Back-Up je Packen " + T(okFree) + " folgt " + T(okFollow)
                    + " XML " + T(okXml) + " ohne Meldung " + T(okCheck) + " packen-tag " + T(okTag) + " altes Paket " + T(okOld) + " Entfernen Paar " + T(okDel));
            }
            finally { f.dirty = false; f.Close(); f.Dispose(); }

            // (7) Kampagnenstart: zwei Pflichtnummern, Paare getrennt, Uebernahme in beide Bloecke
            Package camp = P1bCamp(store);
            DateTime s0 = camp.StartFor(P1D0);
            List<PackageStep> ch = camp.HandNoSteps();
            int s1 = camp.Steps.IndexOf(ch[0]), s2 = camp.Steps.IndexOf(ch[1]);
            int b1i = camp.Steps.FindIndex(delegate(PackageStep x) { return x.BacksUp(ch[0]); }), b2i = camp.Steps.FindIndex(delegate(PackageStep x) { return x.BacksUp(ch[1]); });
            bool okGate, okPair = false, okApply = false;
            using (CampaignForm cf = new CampaignForm(camp, store, s0, ApplyMode.FillEmpty))
            {
                okGate = cf.TestPackFields == 2 && !cf.TestOkEnabled && cf.PackWarning != null;
                cf.TestSetPackNo(0, "12340001");
                okGate &= !cf.TestOkEnabled && cf.PackWarning != null && cf.PackWarning.Contains("Packen 2") && cf.TestPackLabel(1).StartsWith("Suite 2");
                cf.TestSetPackNo(1, "123");
                okGate &= !cf.TestOkEnabled && cf.PackWarning != null;
                cf.TestSetPackNo(1, "12340002");
                okGate &= cf.TestOkEnabled && cf.PackWarning == null;

                cf.TestShiftFollowing = false;
                string d1 = P1bDays(cf.Schedule, b1i), d2 = P1bDays(cf.Schedule, b2i);
                cf.Shift(cf.TestIndexOf(s2, 0), -1); okPair = P1bDays(cf.Schedule, b1i) == d1 && P1bDays(cf.Schedule, b2i) != d2;
                cf.Shift(cf.TestIndexOf(s2, 0), 1);
                string row2 = camp.Steps[s2].Row;
                cf.SetRoom(cf.TestIndexOf(s2, 0), "Suite 3", false);
                okPair &= P1Day(cf.Schedule, b2i, 0).Row == "Suite 3" && P1Day(cf.Schedule, b1i, 0).Row == "Suite 1";
                cf.SetRoom(cf.TestIndexOf(s2, 0), row2, false);
                okPair &= P1Day(cf.Schedule, b2i, 0).Row == row2;

                cf.Result.Apply(store, cf.Start, cf.Mode, cf.Schedule);
                RunDate p1 = P1Day(cf.Schedule, s1, 0), p2 = P1Day(cf.Schedule, s2, 0);
                okApply = store.Get(cf.Start.AddDays(p1.Day - 1), P1Row(store, "Suite 1")).Content.Contains("12340001") && store.Get(cf.Start.AddDays(p2.Day - 1), P1Row(store, "Suite 2")).Content.Contains("12340002");
            }

            // (8) Ein Packen: kein Attribut, ein Feld, Meldung wie bisher
            Package one = P1Camp(store, 2, 2);
            bool okSingle = !one.ToXml().Contains("packnr");
            using (CampaignForm cf = new CampaignForm(one, store, one.StartFor(P1D0.AddDays(60)), ApplyMode.FillEmpty))
                okSingle &= cf.TestPackFields == 1 && cf.PackWarning == "Laufnummer Packen fehlt (8 Ziffern, z. B. 12340001).";

            sb.Append("\nP1b Kampagnenstart: zwei Pflichtnummern " + T(okGate) + " Paare getrennt " + T(okPair) + " Übernahme in beide Blöcke " + T(okApply) + " ein Packen unverändert " + T(okSingle));
            return sb.ToString();
        }
    }
}
#endif
