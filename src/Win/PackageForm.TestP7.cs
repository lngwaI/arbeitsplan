#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P7 (Review-Funde): Rueckgaengig der Equipment-Leiste, Export ueber LaufNo, Packen-Anzeige, Tabelle je Termin, HandNoSteps, MoveStep. Nur erfundene Daten.
    partial class PackageForm
    {
        public static string TestP7(IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();

            // Core: Export ueber LaufNo (fremder Text zaehlt nicht, nur LaufNo ohne LaufRef, fremdes Praefix)
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows(); DateTime d0 = new DateTime(2031, 3, 3); int pr = PufferSetup.Row(rows).Id;
            foreach (int[] r in new[] { new[] { 10, 30 }, new[] { 12, 31 } }) { Entry e = new Entry(); e.Content = "Lauf 1357" + r[1].ToString("0000") + " Tank H1-T-0777"; s.Save(d0.AddDays(r[0]), rows[0].Id, e); }
            Entry pe = new Entry();
            BufferSet.Add(pe.Buffers, "A", "mobil", "Lauf 99990030 Tank H1-T-0911").LaufRef = 1;
            pe.Buffers[0].LaufNo = "13570030";
            BufferSet.Add(pe.Buffers, "B", "mobil", "Tank H1-T-0912").LaufNo = "13570031";
            BufferSet.Add(pe.Buffers, "C", "mobil", "Tank H1-T-0913").LaufNo = "99990030";
            s.Save(d0.AddDays(5), pr, pe);
            EquipExport.CampaignSpan c = null;
            foreach (EquipExport.CampaignSpan x in EquipExport.Campaigns(s, d0.AddDays(-30), d0.AddDays(60), "1357")) if (x.FirstNo == 30) c = x;
            DateTime f1, t1; List<string> ids = c == null ? new List<string>() : EquipExport.Ids(EquipExport.CountCampaign(s, c, 0, 0, out f1, out t1));
            sb.AppendLine("P7 Export über LaufNo: " + T(c != null && ids.Contains("H1-T-0911") && ids.Contains("H1-T-0912") && !ids.Contains("H1-T-0913")));

            // Core: HandNoSteps bei Ereignis, MoveStep prueft Abstand je Lauf
            Package ev = new Package(); ev.Name = "E7"; PackageStep ps = new PackageStep(); ps.Title = "Packen"; ps.Source = Package.PackenName; ps.Day = 1; ev.Steps.Add(ps);
            bool okHand = ev.HandNoSteps().Count == 1; ev.Type = PackageType.Ereignis; okHand &= ev.HandNoSteps().Count == 0;
            Package sm = Ux1bSample("p7", false); PackageStep pu = sm.Steps[sm.Steps.Count - 1];
            pu.Bind = StepBind.JeLauf; pu.RunOffset = 0; pu.Shift[1] = PackageStep.MaxOffset - 1;
            bool okMove = !sm.MoveStep(pu, 2) && pu.RunOffset == 0 && sm.MoveStep(pu, 1) && pu.RunOffset == 1;
            sb.AppendLine("P7 Ereignis ohne Hand-Laufnummer: " + T(okHand) + " MoveStep prüft Abstand je Lauf: " + T(okMove));

            IDataStore store = P1Store();
            string oldPrefs = PrefsFile, tmp = Path.Combine(Path.GetTempPath(), "p7_prefs_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            PackageForm f = new PackageForm(store, equip, start, null, true, true);   // Admin
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            string stage = "";
            try
            {
                f.Show(); Application.DoEvents();
                Predicate<PackageStep> isLauf = delegate(PackageStep x) { return x.Kind == StepKind.Run; }, isPk = delegate(PackageStep x) { return x.Source == Package.PackenName && !x.IsBackup; }, isMo = delegate(PackageStep x) { return x.Variant == "mobil"; };
                PackageStep pk = f.P1Step(isPk), lauf = f.P1Step(isLauf), mo = f.P1Step(isMo);

                // Rueckgaengig: Geraet stand schon in den Stammdaten, Vormerkung kommt zurueck
                stage = "undo";
                EquipPosition pos = null; foreach (EquipPosition x in store.GetCatalog().Positions) if (x.Ids.Count > 0 && x.Short != "Ansatztank") { pos = x; break; }
                string sh = pos != null ? pos.Short : "PT", id = "H1-T-0997";
                f.cat.Master.AddPending(id, sh, "x", DateTime.Now);
                f.cat.Master.Devices.Add(f.cat.Master.NewDevice(id, f.cat.Position(sh)));
                Unk u = new Unk(); u.Id = id; u.Short = sh; u.Step = pk;
                f.Bind(pk); bool okQ = f.QuickAdd(u) && f.cat.Master.PendingFor(id) == null; f.UndoDone();
                bool okUndo = okQ && f.cat.Master.Device(id) != null && f.cat.Master.PendingFor(id) != null;

                // Packen 1 Tag vor Lauf 1 (Back-Up am Lauftag): echten Wert zeigen, Eingabe behebt die Sperre
                stage = "packen";
                f.MoveBy(pk, (f.StepsView().Lauf1Day - pk.FirstDay) - 1); f.Bind(pk); Application.DoEvents();
                bool okOne = f.StepsView().Lauf1Day - pk.FirstDay == 1 && f.nDay.Minimum == 1 && f.nDay.Value == 1 && Package.HasBlock(f.StepsView().Check());
                f.nDay.Value = 2; Application.DoEvents();
                okOne &= f.StepsView().Lauf1Day - pk.FirstDay == 2 && !Package.HasBlock(f.StepsView().Check());
                // Packen nach Lauf 1: ehrliche Anzeige, Eingabe korrigiert auf "vor Lauf 1"
                f.MoveBy(pk, (f.StepsView().Lauf1Day - pk.FirstDay) + 1); f.Bind(pk); Application.DoEvents();
                bool okAfter = f.StepsView().Lauf1Day - pk.FirstDay == -1 && f.cbDir.Items.Count == 3 && f.cbDir.Enabled && f.nDay.Visible && f.nDay.Value == 1;
                f.nDay.Value = 3; Application.DoEvents();
                okAfter &= f.StepsView().Lauf1Day - pk.FirstDay == 3 && f.cbDir.Items.Count == 1;

                // Bezug/Wiederholung bei Packen: ein freier Termin ausgeblendet, mehrere sichtbar
                stage = "mehrfach";
                bool okSimple = !f.cbBind.Visible && !f.cbRepeat.Visible;
                pk.Repeat = RepeatMode.Interval; pk.Interval = 2; pk.Count = 2; f.Bind(pk); Application.DoEvents();
                bool okMulti = f.cbRepeat.Visible && f.cbBind.Visible;
                pk.Count = 1; f.Bind(pk);

                // Tabelle: nur Termine des Puffers (RunEvery 2: Lauf 1 und 3)
                stage = "tabelle";
                lauf.Count = 3; lauf.Interval = 6; mo.RunEvery = 2;
                f.Bind(mo); Application.DoEvents();
                bool okTab = f.gridPuf.Rows.Count == 2 && f.gridPuf[0, 1].Value.ToString() == "Lauf 3";
                f.SetPufDist(1, "4"); Application.DoEvents();
                okTab &= mo.RunOffset + mo.Shift[2] == -4 && !mo.Shift.ContainsKey(1);

                sb.AppendLine("P7 Rückgängig: " + T(okUndo) + " Packen 1 Tag vor Lauf 1 " + T(okOne) + " Packen nach Lauf 1 " + T(okAfter) + " Packen einfach ohne Bezug " + T(okSimple) + " Packen mehrfach mit Bezug " + T(okMulti) + " Tabelle je Termin " + T(okTab));
            }
            catch (Exception ex) { sb.AppendLine("P7 Editor: ABBRUCH (" + stage + ") " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
            finally
            {
                f.dirty = false; f.Close(); f.Dispose();
                PrefsFile = oldPrefs;
                try { File.Delete(tmp); } catch (Exception) { }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
#endif
