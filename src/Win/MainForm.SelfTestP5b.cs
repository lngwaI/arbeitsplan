#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests P5b: Freeze-Vorlage im Paket, Zeit offen, offene Pflichten, Versatz Z2/Z3, FZ-E-Vorschlag. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        void SelfTestP5b(StringBuilder sb)
        {
            try { P5bTemplate(sb); } catch (Exception ex) { sb.AppendLine("P5b Vorlage: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P5bOpen(sb); } catch (Exception ex) { sb.AppendLine("P5b Zeit offen: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P5bOffset(sb); } catch (Exception ex) { sb.AppendLine("P5b Versatz: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P5bSpare(sb); } catch (Exception ex) { sb.AppendLine("P5b FZ-E: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P5bEditor(sb); } catch (Exception ex) { sb.AppendLine("P5b Editor: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        static readonly DateTime P5D0 = new DateTime(2031, 3, 3);   // Montag
        static int P5Row(MemoryStore s, string name) { return s.GetRows().Find(delegate(RowInfo r) { return r.Name == name; }).Id; }

        // Kampagne: Lauf ab 13570030 (3x alle 6 Tage), Freeze 1 Tag nach jedem Lauf
        static Package P5bPkg(FreezeItem tpl, string row)
        {
            Package p = new Package(); p.Name = "P5b"; p.ProductCode = "1357"; p.Type = PackageType.Kampagne;
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Day = 1; lf.Count = 3; lf.Interval = 6; lf.StartNo = 30; p.Steps.Add(lf);
            PackageStep fz = new PackageStep(); fz.Title = "Freeze / Thaw"; fz.Source = "Freeze / Thaw"; fz.Row = row; fz.Bind = StepBind.JeLauf; fz.RunOffset = 1; fz.Freeze = tpl; p.Steps.Add(fz);
            return p;
        }

        static FreezeItem P5bTpl() { FreezeItem t = new FreezeItem(); t.Id = "f1"; t.TimeOpen = true; t.Cycles = 6; t.DauerMin = 300; t.AbstandMin = 400; t.CycleNames = "A|B"; t.Tanks = "H2-T-0251"; return t; }

        void P5bTemplate(StringBuilder sb)
        {
            Package p = P5bPkg(P5bTpl(), "Freeze / Thaw");
            Package b = Package.FromXml(p.ToXml());
            FreezeItem r = b.Steps[1].Freeze;
            bool okXml = r != null && r.TimeOpen && r.Cycles == 6 && r.DauerMin == 300 && r.AbstandMin == 400 && r.CycleNames == "A|B" && r.Tanks == "H2-T-0251" && r.Start1to3[0] == DateTime.MinValue && b.Steps[0].Freeze == null;
            FreezeItem t2 = P5bTpl(); t2.TimeOpen = false; t2.Start1to3[0] = new DateTime(2000, 1, 1, 7, 30, 0); t2.Art = FreezeArt.Tank;
            FreezeItem r2 = Package.FromXml(P5bPkg(t2, "Freeze / Thaw").ToXml()).Steps[1].Freeze;
            okXml &= r2 != null && !r2.TimeOpen && r2.Start1to3[0].Hour == 7 && r2.Start1to3[0].Minute == 30 && r2.Art == FreezeArt.Tank;
            bool okClone = false; PackageStep c = p.Steps[1].Clone(); c.Freeze.Tanks = "x"; okClone = p.Steps[1].Freeze.Tanks == "H2-T-0251";
            Package old = P5bPkg(null, "Freeze / Thaw");
            bool okOld = !old.ToXml().Contains("<freeze") && Package.FromXml(old.ToXml()).Steps[1].Freeze == null;
            sb.AppendLine("P5b Vorlage: XML-Rundlauf=" + Ok(okXml) + " Kopie unabhängig=" + Ok(okClone) + " ohne Vorlage=" + Ok(okOld));
        }

        void P5bOpen(StringBuilder sb)
        {
            MemoryStore st = new MemoryStore(); int fz = P5Row(st, "Freeze / Thaw");
            DateTime start0 = P5D0, day2 = start0.AddDays(7);   // Lauf 2 = Tag 7, Freeze 1 Tag danach = Tag 8
            string msg = P5bPkg(P5bTpl(), "Freeze / Thaw").Apply(st, start0, ApplyMode.FillEmpty, null);
            Entry e = st.Get(day2, fz);
            FreezeItem f = e != null && e.Freezes.Count == 1 ? e.Freezes[0] : null;
            bool okNew = f != null && f.TimeOpen && f.MainRef == "13570031" && f.Start1to3[0] == day2 && FreezeCalc.Cycles(f).Count == 0 && f.Tanks == "H2-T-0251"
                && FreezeSet.CyclesOn(e, day2).Count == 0 && FreezeSet.Continuations(st, day2.AddDays(1), fz).Count == 0 && !e.Content.Contains("Freeze / Thaw") && f.CycleName(1) == "A";
            bool okHead = f != null && PlanView.FreezeHead(f).StartsWith("Bag-Freeze · Zeit offen") && FreezeSet.DisplayLines(e.Freezes, false)[0].Contains("Zeit offen");
            bool okExport = EquipExport.Collect(st, day2, day2).Contains("H2-T-0251");

            DutyConfig cfg = new DutyConfig(); List<RowInfo> rows = st.GetRows();
            Predicate<OpenDuty> isOpen = delegate(OpenDuty o) { return o.State == "Zeit offen" && o.Day == day2 && o.RowId == fz && o.What.Contains("13570031"); };
            bool okDuty = OpenDuties.List(st, cfg, rows, day2, 1, day2).Exists(isOpen);

            // Dialog: Speichern ohne Eintrag laesst offen, Zeit eintragen hebt auf
            FreezeChoice ch = new FreezeChoice(); ch.Item = f.Clone();
            bool okKeep, okEnter;
            using (FreezeForm d = new FreezeForm(ch))
            {
                okKeep = d.TestRows == 0 && d.TestOpen && d.TestItem.SameValue(f) && d.TestItem.Start1to3[0] == day2;
                d.TestSetStart(day2.AddHours(9));
                FreezeItem now = d.TestItem;
                okEnter = !now.TimeOpen && now.Start1to3[0] == day2.AddHours(9) && d.TestRows == 6 && !d.TestOpen;
                e.Freezes[0] = now.Clone(); st.Save(day2, fz, e);
            }
            Entry e2 = st.Get(day2, fz);
            bool okCycles = FreezeSet.CyclesOn(e2, day2).Count > 0 && !PlanView.FreezeHead(e2.Freezes[0]).Contains("Zeit offen");
            bool okGone = !OpenDuties.List(st, cfg, rows, day2, 1, day2).Exists(isOpen);

            // Vorgabe-Uhrzeit: normal berechnet, im Dialog aenderbar
            MemoryStore s2 = new MemoryStore(); FreezeItem t2 = P5bTpl(); t2.TimeOpen = false; t2.Start1to3[0] = new DateTime(2000, 1, 1, 7, 30, 0);
            P5bPkg(t2, "Freeze / Thaw").Apply(s2, start0, ApplyMode.FillEmpty, null);
            FreezeItem g = s2.Get(start0.AddDays(1), fz).Freezes[0];
            bool okPreset = !g.TimeOpen && g.Start1to3[0] == start0.AddDays(1).AddHours(7).AddMinutes(30) && g.MainRef == "13570030" && FreezeCalc.Cycles(g).Count == 6;

            // Keine Freeze-Zeile: bleibt Text
            MemoryStore s3 = new MemoryStore(); P5bPkg(P5bTpl(), "Suite 1").Apply(s3, start0, ApplyMode.Append, null);
            Entry tx = s3.Get(start0.AddDays(1), P5Row(s3, "Suite 1"));
            bool okText = tx != null && tx.Freezes.Count == 0 && tx.Content.Contains("Freeze / Thaw");

            // XML und Zusammenfuehren: offene Zeit bleibt erhalten; einseitig eingetragen gewinnt
            StoreData back = Backup.FromXml(Backup.ToXml(s3.Snapshot()));
            MemoryStore s4 = new MemoryStore(); Entry en = new Entry(); en.Freezes.Add(f.Clone()); s4.Save(day2, fz, en);
            Entry rt = EntryIn(Backup.FromXml(Backup.ToXml(s4.Snapshot())), day2, fz);
            bool okXml = back != null && rt != null && rt.Freezes[0].TimeOpen && rt.Freezes[0].Start1to3[0] == day2 && rt.Freezes[0].SameValue(f);
            Entry mine = en.Clone(); mine.Freezes[0].TimeOpen = false; mine.Freezes[0].Start1to3[0] = day2.AddHours(9); Entry theirs = en.Clone(); theirs.Freezes[0].Note = "andere";
            List<string> cl = new List<string>(); Entry mg = Conflict.Merge(en, mine, theirs, cl);
            bool okMerge = cl.Count == 0 && !mg.Freezes[0].TimeOpen && mg.Freezes[0].Note == "andere";

            sb.AppendLine("P5b Zeit offen: Übernahme=" + Ok(okNew) + " Kopf=" + Ok(okHead) + " Equipment-Export=" + Ok(okExport) + " offene Pflicht=" + Ok(okDuty) + " Speichern ohne Eintrag=" + Ok(okKeep)
                + " Zeit eintragen=" + Ok(okEnter && okCycles) + " Pflicht weg=" + Ok(okGone) + " Vorgabe-Uhrzeit=" + Ok(okPreset) + " keine Freeze-Zeile=" + Ok(okText) + " XML=" + Ok(okXml) + " Zusammenführen=" + Ok(okMerge) + " (" + msg + ")");
        }

        void P5bOffset(StringBuilder sb)
        {
            DateTime t0 = P5D0.AddHours(12);
            FreezeItem f = new FreezeItem(); f.Start1to3[0] = t0; f.Offset2Min = 90; f.Offset3Min = 200;
            List<FreezeCycle> cy = FreezeCalc.Cycles(f);
            bool okCalc = cy[1].Actual == t0.AddMinutes(90) && cy[2].Actual == t0.AddMinutes(200) && cy[1].Estimated && cy[4].Actual == t0.AddMinutes(90 + 420);
            MemoryStore st = new MemoryStore(); int fz = P5Row(st, "Freeze / Thaw"); Entry e = new Entry(); e.Freezes.Add(f.Clone()); FreezeItem std = new FreezeItem(); std.Id = "f2"; std.Start1to3[0] = t0; e.Freezes.Add(std); st.Save(P5D0, fz, e);
            byte[] xml = Backup.ToXml(st.Snapshot());
            Entry rt = EntryIn(Backup.FromXml(xml), P5D0, fz);
            bool okXml = rt.Freezes[0].Offset2Min == 90 && rt.Freezes[0].Offset3Min == 200 && rt.Freezes[1].Offset2Min == 60;
            // alter Vorgang ohne Attribute rechnet wie bisher
            System.Xml.XmlDocument d = new System.Xml.XmlDocument(); d.LoadXml("<b><freeze id=\"f1\" art=\"bag\" dauer=\"360\" abstand=\"420\" cycles=\"14\"><s c=\"1\" t=\"2031-03-03T12:00\"/></freeze></b>");
            FreezeItem alt = FreezeSet.ReadXml(d.DocumentElement)[0]; List<FreezeCycle> ca = FreezeCalc.Cycles(alt);
            bool okAlt = !alt.TimeOpen && alt.Offset2Min == 60 && alt.Offset3Min == 120 && ca[1].Actual == t0.AddHours(1) && ca[2].Actual == t0.AddHours(2) && ca[3].Actual == t0.AddHours(7) && ca.Count == 14;
            // Dialog editierbar, Katalog-Standard kommt mit
            bool okDlg; FreezeChoice ch = new FreezeChoice(); ch.Item = FzBag(t0);
            using (FreezeForm df = new FreezeForm(ch))
            {
                df.TestSetOffsets(1.5m, 3m); FreezeItem x = df.TestItem;
                okDlg = x.Offset2Min == 90 && x.Offset3Min == 180 && df.TestCell(2, 4) == FzSt(t0.AddMinutes(90)) && df.TestCell(3, 4) == FzSt(t0.AddHours(3));
            }
            Catalog cat = st.GetCatalog(); FreezeConfig cfg;
            using (CatalogForm cf = new CatalogForm(cat, new List<string>())) cfg = cf.TestFzOffsets(0.5m, 1.75m);
            cat.FreezeDefaults = cfg; st.SaveCatalog(cat);
            StoreData back = Backup.FromXml(Backup.ToXml(st.Snapshot()));
            bool okCat = cfg.Offset2Min == 30 && cfg.Offset3Min == 105 && back.Catalog.FreezeDefaults.Offset2Min == 30 && back.Catalog.FreezeDefaults.Offset3Min == 105 && new FreezeConfig().Offset2Min == 60;
            sb.AppendLine("P5b Versatz: Rechnung=" + Ok(okCalc) + " XML=" + Ok(okXml) + " alter Vorgang=" + Ok(okAlt) + " Dialog=" + Ok(okDlg) + " Katalog=" + Ok(okCat));
        }

        void P5bSpare(StringBuilder sb)
        {
            MemoryStore st = new MemoryStore(); int su = P5Row(st, "Suite 1"), fz = P5Row(st, "Freeze / Thaw");
            Entry e = new Entry(); e.Content = "Lauf 13570030\nFZ-E: H2-T-0253"; st.Save(P5D0, su, e);
            Catalog cat = st.GetCatalog(); List<RowInfo> rows = st.GetRows();
            bool okFind = FreezeSet.SpareTank(st, rows, "13570031", P5D0.AddDays(6), cat) == "H2-T-0253";
            bool okNone = FreezeSet.SpareTank(st, rows, "13570030", P5D0.AddDays(6), cat) == "" && FreezeSet.SpareTank(st, rows, "", P5D0, cat) == "" && FreezeSet.SpareTank(st, rows, "13570033", P5D0.AddDays(6), cat) == "";
            // Tank im Freeze-Vorgang des Vorlaufs, Geraet der Position FZ-E
            MemoryStore s2 = new MemoryStore(); Entry e2 = new Entry(); FreezeItem v = new FreezeItem(); v.MainRef = "13570030"; v.Tanks = "H2-T-0251, H2-T-0253"; v.Start1to3[0] = P5D0.AddHours(8); FreezeSet.Add(e2.Freezes, v); s2.Save(P5D0, fz, e2);
            bool okTank = FreezeSet.SpareTank(s2, s2.GetRows(), "13570031", P5D0.AddDays(6), s2.GetCatalog()) == "H2-T-0253";
            FreezeChoice ch = new FreezeChoice(); ch.Item = FzBag(P5D0.AddHours(8)); ch.Tanks.Add("H2-T-0251"); ch.Tanks.Add("H2-T-0253"); ch.SpareTank = "H2-T-0253";
            bool okDlg;
            using (FreezeForm d = new FreezeForm(ch))
            {
                okDlg = d.TestPickText(1) == "H2-T-0253 (Ersatz aus Lauf n−1)" && d.TestPickText(2) == "H2-T-0251";
                okDlg &= d.TestItem.Tanks == "";   // nichts erzwungen
                d.TestPick(1); okDlg &= d.TestItem.Tanks == "H2-T-0253";
                d.TestPick(2); okDlg &= d.TestItem.Tanks == "H2-T-0253, H2-T-0251";
            }
            sb.AppendLine("P5b FZ-E-Vorschlag: gefunden=" + Ok(okFind) + " kein Vorlauf=" + Ok(okNone) + " aus Freeze-Tanks=" + Ok(okTank) + " Auswahl ohne Klammer=" + Ok(okDlg));
        }

        void P5bEditor(StringBuilder sb)
        {
            bool okShow, okOn, okSet, okTime, okOff;
            using (PackageForm f = PackageForm.P5bForm(store, equip, start))
            {
                okShow = f.TestFzVisible(1) && !f.TestFzVisible(0);
                PackageStep s = f.TestFzOn(1, true);
                okOn = s.Freeze != null && s.Freeze.TimeOpen && s.Freeze.Cycles == store.GetCatalog().FreezeDefaults.Cycles;
                f.TestFzSet(true, 2.5m, "X|Y", "H2-T-0252");
                okSet = s.Freeze.Art == FreezeArt.Tank && s.Freeze.DauerMin == 150 && s.Freeze.CycleNames == "X|Y" && s.Freeze.Tanks == "H2-T-0252";
                f.TestFzTime(9);
                okTime = !s.Freeze.TimeOpen && s.Freeze.Start1to3[0].Hour == 9;
                f.TestFzOn(1, false); okOff = s.Freeze == null;
                f.TestFzClean();
            }
            sb.AppendLine("P5b Editor: nur beim Baustein=" + Ok(okShow) + " Haken legt Vorlage an=" + Ok(okOn) + " Felder=" + Ok(okSet) + " Vorgabe-Uhrzeit=" + Ok(okTime) + " Haken weg=" + Ok(okOff));
        }
    }
}
#endif
