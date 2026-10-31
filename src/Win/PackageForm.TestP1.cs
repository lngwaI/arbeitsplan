#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P1 (Fachregeln aus dem Nutzer-Feedback): Standardschritte einer neuen Kampagne, Regeln packen-tag und lauffolge (auch mit
    // Tage +/- beim Kampagnenstart und gegen den Plan), Entfernen, Reihenfolge ▲▼, Abstand je Lauf bei Puffern, LaufNo, Ansatz, Ueberschreiben.
    // Nur erfundene Daten. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        static readonly DateTime P1D0 = new DateTime(2027, 3, 1);   // Montag

        static bool P1Has(List<PackageProblem> l, string code, Severity sev) { return l.Exists(delegate(PackageProblem p) { return p.Code == code && p.Severity == sev; }); }
        static bool P1Any(List<PackageProblem> l, string code) { return l.Exists(delegate(PackageProblem p) { return p.Code == code; }); }

        static IDataStore P1Store() { IDataStore s = new MemoryStore(); Ux1bPrepare(s); return s; }

        static Package P1Camp(IDataStore store, int runs, int interval)
        {
            Package p = Package.DefaultCampaign(store.GetCatalog(), 1);
            p.Name = "P1"; p.ProductCode = "PRD-1"; p.RunPrefix = "1234"; p.Head.Count = runs; p.Head.Interval = interval;
            return p;
        }

        static int P1Row(IDataStore s, string name) { return s.GetRows().Find(delegate(RowInfo r) { return r.Name == name; }).Id; }

        static void P1Put(IDataStore s, DateTime day, string row, string text) { Entry e = new Entry(); e.Content = text; s.Save(day, P1Row(s, row), e); }

        static RunDate P1Day(List<RunDate> plan, int step, int run) { return plan.Find(delegate(RunDate d) { return d.Step == step && d.Run == run; }); }

        public static string TestP1Rules()
        {
            StringBuilder sb = new StringBuilder();
            // (1) Standardschritte, auch ohne Bausteine im Katalog
            IDataStore st = P1Store();
            Package p = P1Camp(st, 1, 6);
            bool okStd = p.Steps.Count == 5 && p.Steps[0].Source == Package.PackenName && p.Steps[1].IsBackup && p.Steps[2] == p.Head && p.Head.Kind == StepKind.Run
                && p.Steps[0].FirstDay == 1 && p.Steps[1].FirstDay == 2 && p.Head.FirstDay == 3 && p.Steps[3].Variant == "mobil" && p.Steps[4].Variant == "stationaer"
                && p.Steps[3].Bind == StepBind.JeLauf && p.Steps[3].RunOffset == -1 && p.Steps[4].Bind == StepBind.JeLauf && p.Steps[4].RunOffset == -1 && p.Type == PackageType.Kampagne
                && !Package.HasBlock(p.Check()) && !P1Any(p.Check(), "pufkeine") && Package.DefaultCampaign(st.GetCatalog(), 7).Head.StartNo == 7;
            Package bare = Package.DefaultCampaign(new Catalog(), 1);
            bool okBare = bare.Steps.Count == 5 && bare.Head != null && bare.Head.Source == Package.LaufName && bare.Steps[1].BacksUp(bare.Steps[0]) && bare.Steps[3].IsBuffer && bare.Steps[4].IsBuffer && !Package.HasBlock(bare.Check());
            sb.AppendLine("P1 Neue Kampagne: Standardschritte " + T(okStd) + " Bausteine fehlen im Katalog " + T(okBare));

            // (2) packen-tag: Packen/Back-Up am Lauftag, auch raumuebergreifend; Packen nach Lauf 1; Tage +/- beim Kampagnenstart und Apply
            Package pg = P1Camp(st, 2, 6); PackageStep pk = pg.Steps[0];
            bool okFree = !P1Any(pg.Check(), "packen-tag");
            pk.MoveAll(1); Package.SyncBackups(pg.Steps);   // Packen Tag 2, Back-Up Tag 3 = Lauf 1
            PackageProblem pb = pg.Check().Find(delegate(PackageProblem x) { return x.Code == "packen-tag"; });
            bool okBk = pb != null && pb.Blocks && pb.Step == 1 && pb.Action.Contains("2 Tage vor Lauf 1") && PackageProblem.RuleTitle("packen-tag").Length > 0;
            pk.MoveAll(1); pk.Row = "Suite 2"; Package.SyncBackups(pg.Steps);   // Packen am Lauftag in anderem Raum
            bool okRoom = pg.Check().Exists(delegate(PackageProblem x) { return x.Code == "packen-tag" && x.Blocks && x.Step == 0; });
            pk.MoveAll(2); Package.SyncBackups(pg.Steps);   // Packen nach Lauf 1
            bool okAfter = pg.Check().Exists(delegate(PackageProblem x) { return x.Code == "packen-tag" && x.Text.Contains("nach Lauf 1"); });
            Package ps = P1Camp(st, 2, 6); IDataStore mv = new MemoryStore();
            List<RunDate> sch = ps.Schedule(null, P1D0.DayOfWeek);
            bool okSch0 = !P1Any(ps.Check(mv, P1D0, ApplyMode.FillEmpty, sch), "packen-tag");
            P1Day(sch, 2, 0).Day -= 1;   // Tage -: Lauf 1 auf den Tag des Back-Ups
            bool okSch = P1Has(ps.Check(mv, P1D0, ApplyMode.FillEmpty, sch), "packen-tag", Severity.Sperre);
            bool thrown = false; try { ps.Apply(mv, P1D0, ApplyMode.FillEmpty, sch); } catch (InvalidOperationException) { thrown = true; }
            bool okNoWrite = thrown && mv.Get(P1D0.AddDays(1), P1Row(mv, "Suite 1")) == null;
            sb.AppendLine("P1 Regel packen-tag: frei " + T(okFree) + " Back-Up am Lauftag " + T(okBk) + " anderer Raum " + T(okRoom) + " nach Lauf 1 " + T(okAfter) + " Kampagnenstart Tage +/- " + T(okSch0 && okSch) + " Apply verweigert " + T(okNoWrite));

            // (3) lauffolge in der Kampagne
            Package lf = P1Camp(st, 4, 6);
            bool okNorm = !P1Any(lf.Check(), "lauffolge");
            lf.Head.Shift[2] = 7;   // Tage 3, 9, 22, 21: Reihenfolge 1 2 4 3
            PackageProblem lp = lf.Check().Find(delegate(PackageProblem x) { return x.Code == "lauffolge"; });
            bool okBad = lp != null && !lp.Blocks && lp.Severity == Severity.Hinweis && lp.Text.Contains("von Hand gesetzt") && lp.Run == 3 && lp.Run2 == 2 && lp.Text.Contains("12340004") && PackageProblem.RuleTitle("lauffolge").Length > 0;
            lf.Head.Shift.Clear(); lf.Head.SetSkip(2, true);   // 1 2 4: Luecke erlaubt
            bool okGap = !P1Any(lf.Check(), "lauffolge") && !Package.HasBlock(lf.Check());
            lf.Head.Skip.Clear();
            List<RunDate> ls = lf.Schedule(null, P1D0.DayOfWeek);
            P1Day(ls, 2, 3).Day = P1Day(ls, 2, 2).Day - 1;
            bool okStart = P1Has(lf.Check(mv, P1D0, ApplyMode.FillEmpty, ls), "lauffolge", Severity.Sperre);
            Package nb = P1Camp(st, 3, 1);   // Lauf 2 am Tag x, Lauf 3 am Tag x+1
            bool okNext = !P1Any(nb.Check(), "lauffolge");
            List<RunDate> same = lf.Schedule(null, P1D0.DayOfWeek);
            P1Day(same, 2, 1).Day = P1Day(same, 2, 0).Day; P1Day(same, 2, 1).Row = "Suite 2";   // gleicher Tag, anderer Raum: keine Bruch der Folge
            bool okSame = !P1Any(lf.Check(mv, P1D0, ApplyMode.FillEmpty, same), "lauffolge");
            sb.AppendLine("P1 Regel lauffolge: normal " + T(okNorm) + " 1 2 4 3 von Hand nur Hinweis " + T(okBad) + " 1 2 4 erlaubt " + T(okGap) + " Kampagnenstart " + T(okStart) + " Folgetag " + T(okNext) + " gleicher Tag anderer Raum " + T(okSame));

            // (4) lauffolge gegen den Plan: nur Hinweis, Ueber-Nacht-Lauf ok, zu ersetzende Bloecke zaehlen nicht
            IDataStore mp = new MemoryStore();
            Package pl = P1Camp(st, 2, 6);
            bool okEmpty = !P1Any(pl.Check(mp, P1D0, ApplyMode.FillEmpty, null), "lauffolge");
            P1Put(mp, P1D0.AddDays(-30), "Suite 1", "Lauf 12340050");   // hoehere Nummer liegt frueher
            List<PackageProblem> pp = pl.Check(mp, P1D0, ApplyMode.FillEmpty, null);
            bool okHint = P1Has(pp, "lauffolge", Severity.Hinweis) && !P1Has(pp, "lauffolge", Severity.Sperre) && !Package.HasBlock(pp) && pp.FindAll(delegate(PackageProblem x) { return x.Code == "lauffolge"; }).Count == 1;
            bool okApply = true; try { pl.Apply(mp, P1D0, ApplyMode.FillEmpty, null); } catch (Exception) { okApply = false; }
            IDataStore mo = new MemoryStore();
            P1Put(mo, P1D0.AddDays(8), "Suite 1", "Lauf 12340000");   // niedrigere Nummer spaeter als Lauf 1, im Block von Lauf 2
            bool okOverEx = P1Has(pl.Check(mo, P1D0, ApplyMode.Append, null), "lauffolge", Severity.Hinweis) && !P1Any(pl.Check(mo, P1D0, ApplyMode.Overwrite, null), "lauffolge");
            IDataStore mn = new MemoryStore();   // Lauf 5 ueber Nacht (2 Tage), der neue Lauf 6 am zweiten Tag
            P1Put(mn, P1D0.AddDays(1), "Suite 1", "Lauf 12340005"); P1Put(mn, P1D0.AddDays(2), "Suite 1", "Lauf 12340005");
            Package po = P1Camp(st, 1, 6); po.Head.StartNo = 6;
            bool okNight = !P1Any(po.Check(mn, P1D0, ApplyMode.Overwrite, null), "lauffolge") && !P1Any(po.Check(mn, P1D0, ApplyMode.Append, null), "lauffolge") && Package.PastRuns(mn, "1234")[0].Value == P1D0.AddDays(1);
            IDataStore mr = new MemoryStore(); P1Put(mr, P1D0.AddDays(-5), "Suite 1", "Lauf 12340006");   // dieselbe Nummer vorher im Plan: kein Bruch
            bool okSameNo = !P1Any(po.Check(mr, P1D0, ApplyMode.FillEmpty, null), "lauffolge");
            sb.AppendLine("P1 Lauffolge gegen den Plan: leer " + T(okEmpty) + " nur ein Hinweis " + T(okHint) + " Übernehmen geht " + T(okApply) + " ersetzte Blöcke ausgenommen " + T(okOverEx) + " Lauf über Nacht " + T(okNight) + " gleiche Nummer " + T(okSameNo));

            // (5) Abstand je Lauf bei Puffern
            Package ds = P1Camp(st, 3, 6); PackageStep mob = ds.Steps[3], lauf = ds.Head;
            bool okDrag = ds.DragRun(mob, 1, -1, true) && mob.Shift[1] == -1 && mob.RunOffset == -1;
            List<RunDate> dp = ds.Schedule();
            okDrag &= P1Day(dp, 3, 0).Day == 2 && P1Day(dp, 3, 1).Day == 7 && P1Day(dp, 3, 2).Day == 14 && P1Day(dp, 4, 1).Day == 8;
            Package back = Package.FromXml(ds.ToXml());
            bool okXml = back.Steps[3].Shift.Count == 1 && back.Steps[3].Shift[1] == -1 && back.ToXml() == ds.ToXml() && P1Day(back.Schedule(), 3, 1).Day == 7 && ds.Clone().Steps[3].Shift[1] == -1;
            PackageStep nonBuf = new PackageStep(); nonBuf.Title = "Mischen"; nonBuf.Row = "Suite 1"; nonBuf.Bind = StepBind.JeLauf; nonBuf.RunOffset = 1; ds.Steps.Add(nonBuf);
            bool okOther = ds.DragRun(nonBuf, 1, 1, true) && nonBuf.Shift.Count == 1 && nonBuf.Pinned(1) && P1Day(ds.Schedule(), 5, 1).Day == 11;   // auch ohne Puffer einzeln verschiebbar
            nonBuf.Shift[1] = 5; okOther &= P1Day(ds.Schedule(), 5, 1).Day == 15; ds.Steps.Remove(nonBuf);
            string rel = ds.RelTextOf(P1Day(dp, 3, 1), P1Day(dp, 3, 1).Day, dp), rel2 = ds.RelTextOf(P1Day(dp, 4, 2), P1Day(dp, 4, 2).Day, dp);
            bool okRel = rel == "2 Tage vor Lauf 2" && rel2 == "1 Tag vor Lauf 3" && ds.RelTextOf(P1Day(dp, 2, 1), P1Day(dp, 2, 1).Day, dp) == "Lauf 2" && ds.RelTextOf(P1Day(dp, 0, 0), 1, dp) == "2 Tage vor Lauf 1";
            bool okNorm2 = ds.DragRun(mob, 0, -3, true) && lauf.FirstDay == 5 && P1Day(ds.Schedule(), 3, 0).Day == 1 && !P1Any(ds.Check(), "pufvor");   // vor Tag 1: alles wandert nach hinten
            ds.SetRunCount(1); bool okDrop = !mob.Shift.ContainsKey(1) && mob.Shift.ContainsKey(0);
            Package old = P1Camp(st, 3, 6); PackageStep leg = new PackageStep(); leg.Title = "Puffer alt"; leg.Row = "Puffer"; leg.Variant = "mobil"; leg.Day = 4; leg.Count = 3; leg.Interval = 6;
            old.Steps.RemoveRange(3, 2); old.Steps.Add(leg); old.Head.Shift[2] = 1; leg.Shift[2] = 1; old.Migrate();
            bool okMig = leg.Bind == StepBind.JeLauf && leg.Shift.Count == 0 && P1Day(old.Schedule(), 3, 2).Day == 17;   // 15 + 1 (Versatz) + 1 (Ausnahme nur einmal gerechnet)
            sb.AppendLine("P1 Abstand je Lauf: Ziehen nur dieser Lauf " + T(okDrag) + " XML-Rundlauf " + T(okXml) + " auch ohne Puffer " + T(okOther) + " Bezug zum bedienten Lauf " + T(okRel) + (okRel ? "" : " [" + rel + " | " + rel2 + "]") + " vor Tag 1 " + T(okNorm2) + " Lauf entfällt " + T(okDrop) + " Migration " + T(okMig));

            // (6) LaufNo: im Plan, XML, Regel "Puffer verschiedener Produkte" ohne Nummer im Text
            IDataStore la = new MemoryStore(); int pr = P1Row(la, "Puffer");
            Package lp1 = P1Camp(st, 2, 6); lp1.Steps[0].ManualNo = "12349999"; lp1.Steps.RemoveAt(4);
            lp1.Apply(la, P1D0, ApplyMode.Overwrite, null);
            Entry b1 = la.Get(P1D0.AddDays(1), pr), b2 = la.Get(P1D0.AddDays(7), pr);
            bool okNo = b1 != null && b1.Buffers.Count == 1 && b1.Buffers[0].LaufNo == "12340001" && b1.Buffers[0].LaufRef == 1 && b2 != null && b2.Buffers[0].LaufNo == "12340002" && b2.Buffers[0].LaufRef == 2
                && b2.Buffers[0].LaufText == " · für 12340002" && b2.Buffers[0].Name.IndexOf("1234000") < 0 && b2.Buffers[0].Text.IndexOf("1234000") < 0 && RunNumbers.Find(b2.Buffers[0].Name + "\n" + b2.Buffers[0].Text, null).Count == 0;
            bool okRuns = Package.PastRuns(la, "1234").Count == 2;   // nur die Laeufe, keine Puffer
            BufferItem oldB = new BufferItem(); oldB.Name = "A"; oldB.LaufRef = 3;
            bool okOldT = oldB.LaufText == " · Lauf 3" && oldB.HeadText() == "A · Lauf 3";
            BufferItem k2 = b2.Buffers[0].Clone(); k2.LaufNo = "";
            bool okKey = !k2.SameCore(b2.Buffers[0]) && k2.Key() != b2.Buffers[0].Key() && !k2.SameValue(b2.Buffers[0]);
            StringBuilder xb = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(xb, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment })) { w.WriteStartElement("b"); BufferSet.WriteXml(w, b2.Buffers); w.WriteEndElement(); }
            XmlDocument xd = new XmlDocument(); xd.LoadXml(xb.ToString());
            List<BufferItem> rb = BufferSet.ReadXml(xd.DocumentElement);
            bool okRt = rb.Count == 1 && rb[0].LaufNo == "12340002" && rb[0].SameValue(b2.Buffers[0]) && xb.ToString().Contains(" laufnr=\"12340002\"");
            Package lp2 = P1Camp(st, 2, 6); lp2.RunPrefix = "5555"; lp2.ProductCode = "PRD-2"; lp2.Steps[0].ManualNo = "55559999"; lp2.Steps.RemoveAt(4);
            bool okProd = P1Has(lp2.Check(la, P1D0, ApplyMode.Append, null), "puffer-produkt", Severity.Sperre);
            bool okOwn = !P1Any(lp1.Check(la, P1D0, ApplyMode.Append, null), "puffer-produkt");
            sb.AppendLine("P1 LaufNo: Plan " + T(okNo) + " zählt nicht als Lauf " + T(okRuns) + " altes Format " + T(okOldT) + " Key/SameCore " + T(okKey) + " XML-Rundlauf " + T(okRt) + " anderes Produkt gesperrt " + T(okProd) + " eigenes frei " + T(okOwn));

            // (7) Ansatz je Typ, "ohne" wird beim Laden "gebinde"
            List<KeyValuePair<string, string>> am = PackageStep.AnsatzFor("mobil"), asn = PackageStep.AnsatzFor("stationaer");
            bool okAns = am.Count == 2 && am[0].Key == "" && am[0].Value == "Mobiler Tank" && am[1].Key == "gebinde" && am[1].Value == "Flasche/Gebinde" && asn.Count == 1 && asn[0].Key == "" && asn[0].Value.Contains("tationär");
            Package an = P1Camp(st, 1, 6); an.Steps[3].Ansatz = "gebinde"; an.Steps[3].Menge = "2"; an.Steps[3].Einheit = "kg";
            string ax = an.ToXml();
            Package ao = Package.FromXml(ax.Replace("ansatz=\"gebinde\"", "ansatz=\"ohne\""));
            bool okOhne = ax.Contains("ansatz=\"gebinde\"") && ao.Steps[3].Ansatz == "gebinde" && ao.Steps[3].Menge == "2" && ao.Steps[3].AnsatzLine() == "Gebinde: 2 kg";
            sb.AppendLine("P1 Ansatz: Auswahl je Typ " + T(okAns) + " ohne Gebinde wird Gebinde " + T(okOhne));

            // (8) Ueberschreiben ersetzt nur denselben Tag und Block
            IDataStore ow = new MemoryStore(); int s1 = P1Row(ow, "Suite 1");
            P1Put(ow, P1D0.AddDays(2), "Suite 1", "Alt Zielblock"); P1Put(ow, P1D0.AddDays(3), "Suite 1", "Alt Nachbartag"); P1Put(ow, P1D0.AddDays(1), "Suite 1", "Alt Back-Up-Tag"); P1Put(ow, P1D0.AddDays(2), "Suite 2", "Alt anderer Block");
            Package oc = P1Camp(st, 1, 6); oc.Steps[0].ManualNo = "12349999"; oc.Steps[1].Row = "Suite 2";   // Back-Up liegt woanders
            oc.Apply(ow, P1D0, ApplyMode.Overwrite, null);
            bool okOw = ow.Get(P1D0.AddDays(2), s1).Content.Contains("12340001") && !ow.Get(P1D0.AddDays(2), s1).Content.Contains("Alt") && ow.Get(P1D0.AddDays(3), s1).Content == "Alt Nachbartag"
                && ow.Get(P1D0.AddDays(1), s1).Content == "Alt Back-Up-Tag" && ow.Get(P1D0.AddDays(2), P1Row(ow, "Suite 2")).Content == "Alt anderer Block";
            sb.Append("P1 Überschreiben nur gleicher Tag und Block: " + T(okOw));
            return sb.ToString();
        }

        string P1Order() { List<RunDate> pl = Plan(); string s = ""; foreach (PackageStep x in Ordered()) s += (x.IsBackup ? "B" : x.Source == Package.PackenName ? "P" : x.Kind == StepKind.Run ? "L" : x.Variant == "mobil" ? "M" : "S") + FirstOf(x, pl) + " "; return s.Trim(); }

        PackageStep P1Step(Predicate<PackageStep> m) { return steps.Find(m); }

        public static string TestP1Editor(IEquipmentStatusProvider equip, DateTime start)
        {
            IDataStore store = P1Store();
            string oldPrefs = PrefsFile, tmp = Path.Combine(Path.GetTempPath(), "p1_prefs_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            PackageForm f = new PackageForm(store, equip, start, null, true, false);   // ohne Recht "Pakete einrichten"
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                string o0 = f.P1Order();
                bool okFresh = o0 == "P1 B2 M2 S2 L3" && !Package.HasBlock(f.StepsView().Check()) && !f.dirty && f.StepsView().Head != null;
                Predicate<PackageStep> isLauf = delegate(PackageStep x) { return x.Kind == StepKind.Run; }, isBk = delegate(PackageStep x) { return x.IsBackup; }, isPk = delegate(PackageStep x) { return x.Source == Package.PackenName && !x.IsBackup; },
                    isMo = delegate(PackageStep x) { return x.Variant == "mobil"; }, isSt = delegate(PackageStep x) { return x.Variant == "stationaer"; };

                // ▲ beim letzten Element (Lauf): Puffer wandern mit, Position bliebe gleich -> keine Datumsaenderung, sondern Meldung
                PackageStep lauf = f.P1Step(isLauf);
                f.Bind(lauf); Application.DoEvents();
                bool okEdge = f.bUp.Enabled && !f.bDown.Enabled;
                f.MoveStep(-1); Application.DoEvents();
                string say = f.stripMsg.Text;
                bool okUp = f.P1Order() == o0 && say.Contains("liegt 1 Tag vor jedem Lauf") && say.Contains("„Wann?“") && !f.dirty;
                f.MoveStep(1); okUp &= f.P1Order() == o0;   // Rand: nichts
                f.Bind(f.Ordered()[0]); Application.DoEvents(); okEdge &= !f.bUp.Enabled && f.bDown.Enabled;
                // Back-Up ▲: er bleibt beim Packen
                f.Bind(f.P1Step(isBk)); Application.DoEvents(); f.MoveStep(-1); Application.DoEvents();
                bool okBk = f.P1Order() == o0 && f.stripMsg.Text.Contains("Back-Up") && !f.dirty;
                // Puffer: am selben Tag nur die Reihenfolge, Tage bleiben
                f.Bind(f.P1Step(isSt)); Application.DoEvents(); f.MoveStep(-1); Application.DoEvents();
                bool okSameDay = f.P1Order() == "P1 B2 S2 M2 L3" && f.dirty;
                f.MoveStep(-1); Application.DoEvents(); okSameDay &= f.P1Order() == "P1 S2 B2 M2 L3";
                // Hauptlauf am Tag des Back-Ups: Tagesregel meldet, ▲ tauscht nur am selben Tag
                f.LoadPackage(null); Application.DoEvents();
                lauf = f.P1Step(isLauf); lauf.Day = 2; f.Bind(lauf); Application.DoEvents();
                bool okDay = P1Has(f.StepsView().Check(), "packen-tag", Severity.Sperre) && f.P1Order() == "P1 M1 S1 B2 L2";
                f.MoveStep(-1); Application.DoEvents(); okDay &= f.P1Order() == "P1 M1 S1 L2 B2";

                // Entfernen: Lauf gesperrt; Back-Up allein; Packen nimmt Back-Up mit (auch ohne Recht "Pakete einrichten"); Puffer; ohne Puffer ein Hinweis
                f.LoadPackage(null); Application.DoEvents();
                lauf = f.P1Step(isLauf); f.Bind(lauf); f.RemoveStep(false); Application.DoEvents();
                bool okRun = f.steps.Contains(lauf) && !f.StepsView().CanRemove(lauf) && f.StepsView().CanRemove(f.P1Step(isMo));
                PackageStep pk = f.P1Step(isPk), bk = f.P1Step(isBk);
                f.Bind(bk); f.RemoveStep(false); Application.DoEvents();
                bool okAlone = !f.steps.Contains(bk) && f.steps.Contains(pk) && Package.BackupProblem(f.steps) == null;
                f.LoadPackage(null); Application.DoEvents();
                pk = f.P1Step(isPk); bk = f.P1Step(isBk); f.Bind(pk); f.RemoveStep(false); Application.DoEvents();
                bool okBoth = !f.steps.Contains(pk) && !f.steps.Contains(bk) && f.steps.Count == 3 && f.cur != null && Package.BackupProblem(f.steps) == null;
                f.UndoStep(-1); Application.DoEvents(); okBoth &= f.steps.Count == 5 && f.P1Step(isBk) != null;
                f.Bind(f.P1Step(isMo)); f.RemoveStep(false); Application.DoEvents();
                bool okPuf = f.P1Step(isMo) == null && !P1Any(f.StepsView().Check(), "pufkeine");
                f.Bind(f.P1Step(isSt)); f.RemoveStep(false); Application.DoEvents();
                okPuf &= f.P1Step(isSt) == null && P1Has(f.StepsView().Check(), "pufkeine", Severity.Hinweis) && !Package.HasBlock(f.StepsView().Check());

                // Abstand je Lauf im Editor: Umschalt-Ziehen im Kalender; SetJe laesst ihn stehen, Wechsel Frei -> JeLauf loescht ihn
                f.LoadPackage(null); Application.DoEvents();
                lauf = f.P1Step(isLauf); lauf.Count = 3; lauf.Interval = 6; PackageStep mo = f.P1Step(isMo); f.Bind(mo); Application.DoEvents();
                int si = f.steps.IndexOf(mo);
                f.MoveInCalendar(mo, 1, P1Day(f.Plan(), si, 1).Day - 1, true); Application.DoEvents();
                bool okShift = mo.Shift.Count == 1 && mo.Shift[1] == -1 && P1Day(f.Plan(), si, 1).Day == 7 && f.RelOf(mo, P1Day(f.Plan(), si, 1)).EndsWith("2 Tage vor Lauf 2") && f.StepsView().Check().TrueForAll(delegate(PackageProblem x) { return x.Code != "gleichtag"; });
                f.SetJe(mo); okShift &= mo.Shift.Count == 1;
                f.SetFrei(mo); okShift &= mo.Shift.Count == 0 && mo.Bind == StepBind.Frei;
                mo.Shift[0] = 1; f.SetJe(mo); okShift &= mo.Shift.Count == 0 && mo.Bind == StepBind.JeLauf;

                // Ansatz-Auswahl: mobil zwei Eintraege, stationaer ausgeblendet
                f.Bind(f.P1Step(isMo)); Application.DoEvents();
                bool okAns = f.cbAnsatz.Visible && f.cbAnsatz.Items.Count == 2 && (string)f.cbAnsatz.Items[0] == "Mobiler Tank" && (string)f.cbAnsatz.Items[1] == "Flasche/Gebinde";
                f.Bind(f.P1Step(isSt)); Application.DoEvents();
                okAns &= !f.cbAnsatz.Visible && !f.lAnsatz.Visible && f.P1Step(isSt).Ansatz == "" && f.cbAnsatz.Items.Count == 1;

                return "P1 Editor: neue Kampagne " + T(okFresh) + (okFresh ? "" : " [" + o0 + "]") + " ▲▼ am Rand gesperrt " + T(okEdge) + " Lauf ▲ ohne Datumsänderung " + T(okUp) + (okUp ? "" : " [" + f.P1Order() + " | " + say + "]") + " Back-Up ▲ " + T(okBk) + " Puffer am selben Tag " + T(okSameDay)
                    + " Lauf am Back-Up-Tag " + T(okDay) + " Lauf nicht entfernbar " + T(okRun) + " Back-Up allein " + T(okAlone) + " Packen nimmt Back-Up mit " + T(okBoth) + " Puffer entfernbar, Hinweis ohne Puffer " + T(okPuf) + " Abstand je Lauf " + T(okShift) + " Ansatz-Auswahl " + T(okAns);
            }
            catch (Exception ex) { return "P1 Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace; }
            finally
            {
                f.dirty = false; f.Close(); f.Dispose();
                PrefsFile = oldPrefs;
                try { File.Delete(tmp); } catch (Exception) { }
            }
        }
    }
}
#endif
