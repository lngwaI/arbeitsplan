#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-2: Problemtexte nennen die Beteiligten, Sortierung (Sperren zuerst), Gruppierung, Klick-Sprung mit Markierung,
    // nicht validiertes Equipment (hinzufuegen nur mit Recht PackageAdmin).
    partial class PackageForm
    {
        static string U(bool b) { return b ? "OK" : "FEHLER"; }

        static PackageStep UStep(string title, string row, int day, StepKind k, int count)
        {
            PackageStep s = new PackageStep(); s.Title = title; s.Row = row; s.Day = day; s.Kind = k; s.Count = count; s.Interval = 6; s.StartNo = 1;
            return s;
        }

        // Testbild --form problems: Unterlauf fehlt (3x, gruppiert), nicht validiertes Geraet, zu viele Puffer
        public static Package ProblemSample()
        {
            Package p = Package.SampleCampaign("Probleme");
            PackageStep sub = UStep("Splitten", "Suite 1", 3, StepKind.SubRun, 1); sub.SubNos = "1"; p.Steps.Add(sub);
            PackageStep pu = UStep("Ansatz X", "Puffer", 1, StepKind.Normal, 6); pu.Interval = 1; pu.Variant = "mobil"; p.Steps.Add(pu);
            EquipAssign a = new EquipAssign(); a.Short = "TFF"; a.Choices.Add(new EquipChoice("TFF", "H1-X-0999")); p.Steps[2].Equipment.Add(a);
            return p;
        }

        static PackageProblem UFind(List<PackageProblem> l, string code) { return l.Find(delegate(PackageProblem p) { return p.Code == code; }); }

        public static string TestUx2(IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();
            DateTime d0 = new DateTime(2033, 3, 7);   // Montag

            // Texte mit Beteiligten
            Package sd = Package.SampleCampaign("ux");
            List<RunDate> sch = sd.Schedule();
            RunDate r0 = sch.Find(delegate(RunDate x) { return x.Step == 2 && x.Run == 0; }), r1 = sch.Find(delegate(RunDate x) { return x.Step == 2 && x.Run == 1; });
            r1.Day = r0.Day;
            PackageProblem g = UFind(sd.Check(new MemoryStore(), d0, ApplyMode.FillEmpty, sch), "gleichtag");
            bool okSame = g != null && g.Text.Contains("Lauf 1 und Lauf 2 liegen beide am") && g.Text.Contains("2033") && g.Text.Contains(sd.Steps[2].Row) && g.Text.Contains("nicht erlaubt")
                && g.Action.Length > 0 && g.Step == 2 && g.Run == 1 && g.Step2 == 2 && g.Run2 == 0;
            Package pf = Package.SampleCampaign("ux-puf");
            PackageStep pfree = UStep("Ansatz X", "Puffer", 1, StepKind.Normal, 6); pfree.Interval = 1; pfree.Variant = "mobil"; pf.Steps.Add(pfree);
            PackageProblem gp = UFind(pf.Check(), "puffer");
            bool okPuf = gp != null && gp.Text.Contains("Pufferläufe (6)") && gp.Text.Contains("„Ansatz X“") && gp.Text.Contains("Produktläufe (4)") && gp.Action.Length > 0;
            Package ul = Package.SampleCampaign("ux-unter");
            PackageStep us = UStep("Splitten", "Suite 1", 3, StepKind.SubRun, 3); us.SubNos = "1, 2, 3"; ul.Steps.Add(us);
            PackageProblem gu = UFind(ul.Check(), "unterlauf");
            bool okUl = gu != null && gu.Text.StartsWith("Lauf 4 ohne Unterlauf") && gu.Text.Contains("„Splitten“") && gu.Action.Contains("Lauf 4");
            Package pb = new Package(); pb.Name = "ux-pp"; pb.ProductCode = "1234";
            pb.Steps.Add(UStep("Lauf", "Suite 1", 3, StepKind.Run, 2));
            PackageStep pbs = UStep("Ansatz Y", "Puffer", 1, StepKind.Normal, 1); pbs.Bind = StepBind.JeLauf; pbs.RunOffset = -1; pbs.Variant = "mobil"; pb.Steps.Add(pbs);
            MemoryStore mp = new MemoryStore(); Entry pe = new Entry(); BufferSet.Add(pe.Buffers, "Fremd Z", "mobil", "Lauf 55550001");
            int prow = -1; foreach (RowInfo r in mp.GetRows()) if (r.Name == "Puffer") prow = r.Id;
            mp.Save(d0.AddDays(1), prow, pe);
            PackageProblem gx = UFind(pb.Check(mp, d0, ApplyMode.FillEmpty, null), "puffer-produkt");
            bool okPp = gx != null && gx.Blocks && gx.Text.Contains("„Ansatz Y“") && gx.Text.Contains("Produkt 1234") && gx.Text.Contains("„Fremd Z“") && gx.Text.Contains("Produkt 5555") && gx.Text.Contains("am selben Tag") && gx.Action.Length > 0;
            sb.AppendLine("UX2 Problemtexte: Hauptläufe (beide Läufe, Datum, Raum, Handlung) " + U(okSame) + " Pufferläufe/Produktläufe " + U(okPuf) + " Unterlauf fehlt " + U(okUl) + " Puffer zweier Produkte " + U(okPp));

            if (!(okPuf && okUl && okPp)) sb.AppendLine("UX2 Details: " + (gp != null ? gp.Full : "-") + " # " + (gu != null ? gu.Full : "-") + " # " + (gx != null ? gx.Full : "-"));
            // Sortierung, Gruppierung, Klick-Sprung (Formular)
            Package gr = Package.SampleCampaign("ux-gruppe");
            PackageStep sub = UStep("Splitten", "Suite 1", 3, StepKind.SubRun, 1); sub.SubNos = "1"; gr.Steps.Add(sub);
            MemoryStore ms = new MemoryStore();
            PackageForm f = new PackageForm(ms, equip, start, gr, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000); f.ShowInTaskbar = false;
            bool okSort = false, okGroup = false, okOpen = false, okJump = false, okMark = false, okClear = false;
            try
            {
                f.Show(); Application.DoEvents();
                Hint head = f.hintList.Find(delegate(Hint h) { return h.Kids != null; });
                okGroup = head != null && head.Kids.Count >= 3 && head.Text.Contains(head.Kids.Count + " Probleme") && head.Blocks
                    && f.shownHints.Contains(head) && !f.shownHints.Contains(head.Kids[0]) && f.hintList.Contains(head.Kids[0]);
                List<Hint> mix = new List<Hint>(); Hint hn = new Hint("Hinweis", null, null), hs = new Hint("Sperre", null, null); hs.Blocks = true; mix.Add(hn); mix.Add(hs);
                bool seenNon = false; okSort = BlocksFirst(mix)[0] == hs;
                foreach (Hint h in f.shownHints) { if (!h.Blocks) seenNon = true; else if (seenNon) okSort = false; }
                int before = f.shownHints.Count;
                if (head != null) { f.ToggleGroup(head); Application.DoEvents(); okOpen = f.shownHints.Count == before + head.Kids.Count && f.lbHints.Items.Count == f.shownHints.Count && head.Kids[0].Indent; f.ToggleGroup(head); okOpen &= f.shownHints.Count == before; }
                PackageStep lf = f.steps[2];
                Hint jump = new Hint("x", lf, null); jump.Run = 1; jump.Step2 = lf; jump.Run2 = 0;
                f.GoToHint(jump); Application.DoEvents();
                okJump = f.cur == lf && f.curRun == 1; okMark = f.strip.HasMark;
                f.GoToHint(new Hint("y", lf, null)); Application.DoEvents();
                okClear = !f.strip.HasMark;
            }
            catch (Exception ex) { sb.AppendLine("UX2 Hinweisliste: ABBRUCH " + ex.GetType().Name + " " + ex.Message); }
            finally { f.dirty = false; f.Close(); }
            sb.AppendLine("UX2 Hinweisliste: Sperren vor Hinweisen " + U(okSort) + " Gruppe mit Zähler (ab 3) " + U(okGroup) + " auf-/zuklappen " + U(okOpen) + " Klick springt zum ersten Beteiligten " + U(okJump)
                + " zweiter im Kalender markiert " + U(okMark) + " Markierung weg " + U(okClear));

            // Nicht validiertes Equipment
            sb.Append(TestUx2Equip(equip, start));
            return sb.ToString();
        }

        static string TestUx2Equip(IEquipmentStatusProvider equip, DateTime start)
        {
            const string Id = "H1-T-0999";
            MemoryStore ms = new MemoryStore();
            PackageForm f = new PackageForm(ms, equip, start, Package.SampleCampaign("ux-equip"), true, true);
            PackageForm n = new PackageForm(ms, equip, start, Package.SampleCampaign("ux-equip2"), true, false);
            f.StartPosition = n.StartPosition = FormStartPosition.Manual; f.Location = n.Location = new Point(-4000, -4000); f.ShowInTaskbar = n.ShowInTaskbar = false;
            bool okHint = false, okAdd = false, okAfter = false, okNone = false, okNoted = false, okPersist = false;
            try
            {
                f.Show(); n.Show(); Application.DoEvents();
                string sh = f.cat.Positions[0].Short;
                EquipAssign a = new EquipAssign(); a.Short = sh; a.Choices.Add(new EquipChoice(sh, Id));
                EquipAssign a2 = new EquipAssign(); a2.Short = sh; a2.Choices.Add(new EquipChoice(sh, Id));
                f.steps[2].Equipment.Add(a); f.Bind(f.steps[2]); Application.DoEvents();
                n.steps[2].Equipment.Add(a2); n.Bind(n.steps[2]); Application.DoEvents();
                Hint h = f.hintList.Find(delegate(Hint x) { return x.UnvalId == Id; });
                okHint = h != null && !EquipRow.Known(a, f.cat, f.Product) && h.Text.Contains("Nicht in der Equipment-Liste (nicht validiert)") && h.Text.Contains("Zur Liste hinzufügen");
                Hint hn = n.hintList.Find(delegate(Hint x) { return x.UnvalId == Id; });
                okNone = hn != null && hn.Text.Contains("Bitte Admin bitten") && !n.AddDevice(new EquipDevice { Id = Id }, sh) && n.cat.Master.Device(Id) == null && ms.GetCatalog().Master.Device(Id) == null;
                n.notedIds.Add(Id); n.UpdateHints();
                okNoted = n.hintList.Exists(delegate(Hint x) { return x.UnvalId == Id && x.Text.Contains("Vorgemerkt"); });
                EquipDevice d = new EquipDevice(); d.Id = Id; d.Note = "Testgerät";
                if (f.cat.Master.Classes.Count > 0) d.Class = f.cat.Master.Classes[0].Name;
                okAdd = f.AddDevice(d, sh);
                EquipAssign shown = f.steps[2].Equipment[f.steps[2].Equipment.Count - 1];
                okAfter = EquipRow.Known(shown, f.cat, f.Product) && !f.hintList.Exists(delegate(Hint x) { return x.UnvalId == Id; });
                okPersist = ms.GetCatalog().Master.Device(Id) != null && ms.GetCatalog().Master.Device(Id).Class == d.Class;
                using (EquipAddForm af = new EquipAddForm(Id, f.cat)) { af.StartPosition = FormStartPosition.Manual; af.Location = new Point(-4000, -4000); af.TestFill(d.Class, "x"); }   // Dialog baut sich auf
            }
            catch (Exception ex) { return "UX2 Equipment: ABBRUCH " + ex.GetType().Name + " " + ex.Message + "\r\n"; }
            finally { f.dirty = n.dirty = false; f.Close(); n.Close(); }
            Hint dh = f.hintList.Find(delegate(Hint x) { return x.UnvalId == Id; });
            return "UX2 Equipment nicht validiert: Hinweis mit Link " + U(okHint) + " ohne Recht kein Hinzufügen, Vormerkung " + U(okNone && okNoted) + " hinzufügen (Admin) " + U(okAdd)
                + " danach validiert " + U(okAfter) + " gespeichert " + U(okPersist) + "\r\n";
        }
    }
}
#endif

