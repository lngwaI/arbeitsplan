#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Testhilfen (--shot, --bench) und Selbsttests: Bearbeiten, Speichern, Kampagne/Pakete.
    partial class MainForm
    {
        public static int TestWeeks = 1;
        SecondWeek week2;

        void SetWeeks(int n)
        {
            if (n == 2 && week2 == null)
            {
                week2 = new SecondWeek(canvas, Font, view, rowInfo);
                view.LayoutDone += delegate { if (week2 != null) week2.Place(view); };
                week2.Load(store, rowInfo, start.AddDays(7));
                ComputeChecks(week2.View, week2.View.Start);   // Pruefleiste und Rundgangsleiste wie in der ersten Woche
                week2.View.RoundToggled = delegate(RoundGroup g) { g.Collapsed = !g.Collapsed; roundFold[g.Monday] = g.Collapsed; week2.View.Relayout(); };
                ResizeView();
                if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate   // Woche 2 liegt unter der ersten: dorthin scrollen, damit man etwas sieht
                {
                    if (week2 == null) return;
                    ResizeView();
                    canvas.AutoScrollPosition = new Point(0, Math.Max(0, week2.View.Top - 56 - canvas.AutoScrollPosition.Y));
                });
            }
            else if (n == 1 && week2 != null) { week2.Remove(); week2 = null; }
        }

        // ---------- Testhilfen (--shot) ----------

        public Form TestUsers()
        {
            UserAdminForm u = new UserAdminForm(users, me);
            u.StartPosition = FormStartPosition.Manual; u.Location = new Point(0, 0); u.TopMost = true;
            u.Show();
            return u;
        }

        public Form TestForm(string which)
        {
            Package demo = null;
            if (which.StartsWith("package2") || which == "campaign")   // Produkt 1357: Packen, Lauf (3x alle 3 Tage, abwechselnd), Splitten in Unterlaeufe
            {
                demo = new Package(); demo.Name = "Produkt Beispiel"; demo.ProductCode = "1357";
                Catalog cat = store.GetCatalog();
                foreach (string bn in new[] { "Packen", "Lauf", "Splitten" })
                {
                    Baustein b = cat.Baustein_(bn);
                    PackageStep s = new PackageStep(); s.Title = b.Name; s.Source = b.Name; s.Row = b.Row; s.Day = bn == "Packen" ? 1 : (bn == "Lauf" ? 2 : 3);
                    foreach (string sh in b.Positions) { EquipAssign a = new EquipAssign(); a.Short = sh; s.Equipment.Add(a); }
                    if (bn == "Splitten") { s.Kind = StepKind.SubRun; s.Count = 3; s.Interval = 3; s.StartNo = 30; s.SubNos = "4186/4187, 4188"; }
                    if (bn == "Lauf")
                    {
                        s.Kind = StepKind.Run; s.Count = 3; s.Interval = 3; s.StartNo = 30;
                        s.Equipment[0].Choices.Add(new EquipChoice("PT", "H1-T-0102")); s.Equipment[1].Choices.Add(new EquipChoice("Pumpe", "H1-P-0101-05"));
                        s.Equipment[2].Choices.Add(new EquipChoice("TFF", "H1-X-0101")); s.Equipment[2].Choices.Add(new EquipChoice("TFF", "H1-X-0102"));
                        s.Equipment[2].Alternate = true; s.Equipment[2].Fallback = "H1-X-0103";
                        s.Equipment[3].Choices.Add(new EquipChoice("MH", "H2-X-0999")); s.Equipment[3].Note = "neu validieren";   // nicht validiert -> orange
                    }
                    demo.Steps.Add(s);
                }
            }
            if (which == "package3" || which == "package4" || which == "package7" || which == "package8" || which == "package9" || which == "package14" || which == "campaign2" || which == "campaign3" || which == "campaign5") demo = Package.SampleCampaign("Testpaket");
            if (which == "campaign4")   // Einzelpaket: einfacher Modus
            {
                demo = new Package(); demo.Name = "Einzelpaket"; demo.Type = PackageType.Einzel;
                PackageStep ps = new PackageStep(); ps.Title = "Packen"; ps.Source = Package.PackenName; ps.Row = "Suite 1"; ps.Day = 1; demo.Steps.Add(ps);
            }
            if (which == "package4")   // Puffer-Schritt (Herstellung beides), "Mehr Optionen" offen
            {
                PackageStep pu = new PackageStep(); pu.Title = "Puffer"; pu.Source = "Puffer"; pu.Row = "Puffer"; pu.Day = 2; pu.Variant = "mobil|stationaer"; pu.Note = "Puffer A";
                demo.Steps.Add(pu);
            }
            if (which == "package5" || which == "package6")   // Paket 3: Lauf Mo/Mi/Fr mit verschobenem 2. Lauf; Puffer 2x mobil + 1x stationaer aus Flasche
            {
                demo = Package.SampleCampaign("Testpaket");
                PackageStep lf = demo.Steps[2]; lf.Repeat = RepeatMode.Weekdays; lf.WeekDays = 1 | 4 | 16; lf.Count = 6; lf.Shift[1] = 1;
                PackageStep pu = new PackageStep(); pu.Title = "Puffer"; pu.Source = "Puffer"; pu.Row = "Puffer"; pu.Day = 2; pu.Variant = "mobil:2|stationaer";
                pu.Ansatz = "gebinde"; pu.Gebinde = "Flasche"; pu.Menge = "5"; pu.Einheit = "L"; pu.Repeat = RepeatMode.DayList; pu.DayList = "2, 5, 9";
                demo.Steps.Add(pu);
            }
            if (which == "package10")   // PB: Puffer-Schritt mit 3 mobilen Puffern, Namen, 2 mobilen Tanks als Standard
            {
                demo = Package.SampleBuffers("Testpaket");
            }
            if (which == "package11" || which == "package12" || which == "package13")   // UX-1b: Puffer-Schritt mit zwei Karten (12: Geraet doppelt = rote Karten)
            {
                PackageForm.Ux1bPrepare(store); demo = PackageForm.Ux1bSample("Testpaket", which == "package12");
            }
            if (which.StartsWith("rt:")) demo = Package.Load(which.Substring(3));   // Roundtrip: Datei laden, im Editor unveraendert wieder speichern (.rt.apkg)
            if (which == "campaign2")   // fruehere Kampagne (Laeufe 1-4, Abstaende 5, 5, 7) sechs Wochen vorher
                Package.SampleCampaign("alt").Apply(store, start.AddDays(-42), ApplyMode.Overwrite, Package.SampleCampaign("alt").Schedule(new List<int>(new[] { 5, 5, 7 })));
            Form f;
            if (which == "campaign2") { CampaignForm cf = new CampaignForm(demo, store, start, ApplyMode.Overwrite); cf.SetRoom(7, "Suite 2", false); f = cf; }
            else if (which == "campaign3" || which == "campaign4")   // Kampagnenstart: 5 Laeufe, Packen-Nummer, Lauf 3 faellt aus / Einzelpaket
            {
                CampaignForm cf = new CampaignForm(demo, store, start, ApplyMode.FillEmpty);
                cf.TestPackNo = "12340001";
                if (which == "campaign3") { cf.TestRuns = 5; cf.SetSkip(cf.TestIndexOf(2, 2), true); }
                f = cf;
            }
            else if (which == "campaign5")   // Regelverstoss: Lauf 2 auf den Tag von Lauf 1 geschoben = zwei Hauptlaeufe am selben Tag (rote Zeile, Uebernehmen gesperrt)
            {
                CampaignForm cf = new CampaignForm(demo, store, start, ApplyMode.FillEmpty);
                cf.TestPackNo = "12340001"; cf.TestShiftFollowing = false;
                cf.Shift(cf.TestIndexOf(2, 1), -6);
                f = cf;
            }
            else if (which == "wizard" || which == "wizard3")   // Kampagnen-Assistent: Seite 1 bzw. Seite 3 mit Paketen eines Produkts
            {
                string wd = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wizard_shot"); System.IO.Directory.CreateDirectory(wd);
                Package.SampleCampaign("Musterkampagne").Save(System.IO.Path.Combine(wd, "Musterkampagne.apkg"));
                Package other = Package.SampleCampaign("Zweites Paket"); other.ProductCode = "AB-1"; other.Save(System.IO.Path.Combine(wd, "Zweites Paket.apkg"));
                CampaignWizard cw = new CampaignWizard(wd, store.GetCatalog(), which == "wizard" ? null : "1234");
                if (which == "wizard3") cw.TestPage(2, "1234", false);
                f = cw;
            }
#if DEMO
            else if (which == "demorole") f = new DemoRoleForm(users, null);
#endif
            else if (which == "structure") f = new StructureForm(store.GetRows(), true, true, true);
            else if (which == "duties") f = new DutiesForm(store.GetDuties(), store.GetRows());
            else if (which == "equipexport")
            {
                SelfTestEquipExport(null);
                f = new EquipExportForm(store, start, null);
            }
            else if (which == "openduties") { DateTime mon = ShownWeek; f = new OpenDutiesForm("Offen in " + PlanView.WeekTitle(mon), delegate(int w) { return OpenDuties.List(store, store.GetDuties(), store.GetRows(), mon, w, DateTime.Today); }); }
            else if (which == "steppicker") { List<string> nm = new List<string>(); foreach (RowInfo r in store.GetRows()) nm.Add(r.Name); StepPicker sp = new StepPicker(store.GetCatalog(), new List<PackageStep>(), nm); sp.TestSelect("Puffer"); f = sp; }
            else if (which == "buffer")   // Puffer-Dialog (vorhandener Puffer mit Entfernen/Hoch/Runter)
            {
                BufferChoice bc = new BufferChoice(); bc.Types = new[] { "mobil", "stationaer" }; bc.Number = 2; bc.CanUp = bc.CanDown = true;
                bc.Names.AddRange(new[] { "Puffer A", "Lösung B", "Packen" }); bc.Name = "Lösung B"; bc.Typ = "stationaer"; bc.Text = "Tank 2\nWFI: H1-X-0101";
                f = new BufferForm(bc);
            }
            else if (which == "catalog") { List<string> nm = new List<string>(); foreach (RowInfo r in store.GetRows()) nm.Add(r.Name); f = new CatalogForm(store.GetCatalog(), nm); }
            else if (which == "locked" || which == "takeover" || which == "notices") f = TestNoticeForm(which);
            else if (which == "history" || which == "backup") f = TestDataForm(which);
            else if (which == "quickadd" || which == "unassigned" || which == "equipfits") f = TestQuickForm(which);
            else if (which.StartsWith("stammdaten") || which == "products" || which.StartsWith("equiptable") || which.StartsWith("equipmatrix")) f = TestStammForm(which);
            else if (which == "conflict")   // Rueckfrage bei gleicher Stelle (Testbild)
            {
                Entry b = new Entry(), m = new Entry(), t = new Entry();
                b.Content = "Lauf 13570030\nTFF: H1-X-0101\nPumpe: H1-P-0101-05"; b.People[0] = "MUMA";
                m.Content = "Lauf 13570030\nTFF: H1-X-0102\nPumpe: H1-P-0101-05"; m.People[0] = "MUMA, BEER";
                t.Content = "Lauf 13570030\nTFF: H1-X-0103 (Ausweich)\nPumpe: H1-P-0101-05"; t.People[0] = "MUMA, WEPA";
                f = new ConflictForm("Suite 1, " + Ui.DayText(start), "Paula Werk", "um 09:41 Uhr", t, m, Conflict.Merge(b, m, t, new List<string>()), true);
            }
            else if (which == "pufferpack") f = PackageForm.Ux1bPackForm(store);   // UX-1b: Pufferpaket eines Produkts
            else if (which == "equipadd") f = new EquipAddForm("H1-T-0999", store.GetCatalog());   // UX-2: nicht validierte Nummer aufnehmen
            else if (which == "problems") f = new PackageForm(store, equip, start, PackageForm.ProblemSample(), true, true);   // UX-2: Prueffunde mit Beteiligten
            else if (which == "campaign") { CampaignForm cf = new CampaignForm(demo, store, start, ApplyMode.FillEmpty); cf.Shift(1, 1); f = cf; }   // Lauf 13570030 (und Folgende) einen Tag spaeter
            else f = new PackageForm(store, equip, start, demo, true, true);
            Hide();                                   // nur fuer das Testbild: Hauptfenster nicht dahinter
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0); f.TopMost = true;
            PackageForm pf = f as PackageForm;
            if (pf != null && ClientSize.Width < 1190) pf.Size = ClientSize;   // --size 1024x768: Editor wie auf einem kleinen Bildschirm
            if (pf != null && which.StartsWith("rt:")) pf.Text = pf.TestSave(which.Substring(3) + ".rt.apkg");
            f.Show();
            if (pf != null && which == "package2") pf.SelectStep(1);
            if (pf != null && which == "package2sub") pf.SelectStep(2);   // Unterlauf-Schritt (Splitten)
            if (pf != null && which == "package3") pf.SelectStep(2);
            if (pf != null && which == "problems") pf.SelectStep(2);
            if (pf != null && which == "package4") { pf.SelectStep(2); pf.TestMore(); }
            if (pf != null && which == "package5") pf.SelectStep(3);
            if (pf != null && which == "package7") { pf.SelectStep(3); pf.TestMore(); pf.TestScrollEnd(); }   // Hauptlauf, Zusaetze (Kennungstabelle) im Blick
            if (pf != null && which == "package6") pf.SelectStep(2);
            if (pf != null && which == "package10") pf.TestSelectBuffer();
            if (pf != null && (which == "package11" || which == "package12")) { pf.TestSelectBuffer(); if (which == "package12") pf.TestOpenCard(1); pf.TestScrollEnd(); }
            if (pf != null && (which == "package13" || which == "package14")) { pf.Size = ClientSize.Width < 1190 ? ClientSize : new Size(1190, 980); if (which == "package13") pf.TestSelectBuffer(); else pf.SelectStep(2); pf.TestMore(); pf.TestShrinkPane(); }   // UX-5a: hohes Fenster, "Mehr Optionen" offen (13 Puffer, 14 Lauf)
            if (pf != null && (which == "package8" || which == "package9")) pf.TestE5Shot(which);   // E5/E6: verschobene Laeufe mit Schloss; Zoom Woche + nur Auswahl
            return f;
        }

        // Paketdatei ab dem angezeigten Montag uebernehmen (vorhandene Bloecke ueberschreiben), fuer das Wochen-Testbild.
        public void TestApply(string path, int runs = 0)
        {
            Package ap = Package.Load(path);
            if (runs > 0) ap.SetRunCount(runs);
            ap.Apply(store, start, ApplyMode.Overwrite, null);
            Rebuild(true);
        }

        public void TestEdit(int col, int row)
        {
            foreach (CellData c in view.Cells) if (c.RowIndex == row && c.Col == col) { fold[rowInfo[row].Id] = false; ApplyFold(); view.Relayout(); BeginEdit(c); }
        }

        public void TestSearch(string q) { search.Text = q; }
        public void TestGo(int days) { Go(days); }

        public void TestScroll(int y) { canvas.AutoScrollPosition = new Point(0, y); }
        public void TestWeeksLate() { SetWeeks(2); }

        // Messung: n Wochenwechsel inkl. Neuzeichnen (ms je Wechsel).
        public double Bench(int n)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) { Go(i % 2 == 0 ? 7 : -7); Refresh(); }
            return sw.ElapsedMilliseconds / (double)n;
        }

        // Produkt-Paket: Laufnummern XXYYZZZZ, Unterlaeufe, Kampagnenstart mit verschobenem Lauf, Datei-Roundtrip, fruehere Kampagne.
        void SelfTestCampaign(System.Text.StringBuilder sb)
        {
            Package c = new Package(); c.Name = "Produkt Test"; c.ProductCode = "1357";
            PackageStep pk = new PackageStep(); pk.Title = "Packen"; pk.Row = "Suite 2"; pk.Day = 1;
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Row = "Suite 2"; lf.Day = 2; lf.Count = 4; lf.Interval = 2; lf.StartNo = 30; lf.Kind = StepKind.Run;
            PackageStep sp = new PackageStep(); sp.Title = "Splitten"; sp.Row = "Mix / Split"; sp.Day = 3; sp.Count = 4; sp.Interval = 2; sp.StartNo = 30; sp.Kind = StepKind.SubRun; sp.SubNos = "4186/13574187, , 4190";
            c.Steps.Add(pk); c.Steps.Add(lf); c.Steps.Add(sp);
            bool okNo = lf.Compose(0, "1357") == "Lauf 13570030" && lf.Compose(1, "") == "Lauf 31" && pk.Compose(0, "1357") == "Packen";
            string sub0 = sp.Compose(0, "1357"), sub1 = sp.Compose(1, "1357");
            bool okSub = sub0 == "Splitten\nUnterlauf 13574186 (aus 13570030)\nUnterlauf 13574187 (aus 13570030)" && sub1 == "Splitten\nUnterlauf (aus 13570031)"
                && sp.Compose(2, "1357").EndsWith("Unterlauf 13574190 (aus 13570032)");
            c.Renumber(40);
            bool okRe = lf.StartNo == 40 && sp.StartNo == 40 && pk.StartNo == 1 && lf.Label("1357", 0) == "Lauf 13570040";
            sb.AppendLine("Produkt: Nummern=" + Ok(okNo) + " Unterlauf=" + Ok(okSub) + " Umnummern=" + Ok(okRe) + " [" + sub0.Replace("\n", "|") + "]");

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selftest_produkt.apkg");
            c.Save(path);
            Package q = Package.Load(path);
            bool okRt = q.ProductCode == "1357" && q.Steps.Count == 3 && q.Steps[1].Kind == StepKind.Run && q.Steps[2].Kind == StepKind.SubRun && q.Steps[2].SubNos == sp.SubNos;
            for (int s = 0; s < 3; s++) for (int r = 0; r < c.Steps[s].Count; r++) okRt &= q.Steps[s].Compose(r, q.ProductCode) == c.Steps[s].Compose(r, c.ProductCode);
            System.IO.File.WriteAllText(path, "<Paket name=\"Alt\" version=\"2\"><Schritt titel=\"Lauf\" block=\"Suite 2\" tag=\"1\" anzahl=\"2\" abstand=\"1\" startnr=\"1\"/></Paket>");
            Package old = Package.Load(path);
            bool okOld = old.ProductCode == "" && old.Steps[0].Kind == StepKind.Normal && old.Steps[0].Compose(1, old.ProductCode) == "Lauf 2";
            sb.AppendLine("Produkt-Datei: Roundtrip=" + Ok(okRt) + " altesPaket=" + Ok(okOld));

            // Kampagnenstart-Dialog (ohne Anzeige): Zeile 1 (Lauf 13570040) +1 Tag, Folgende wandern mit, Zeile 0 nicht
            DateTime s2 = start.AddDays(35);
            bool okDlg, okRoomDlg;
            using (CampaignForm cf = new CampaignForm(q, store, s2, ApplyMode.Overwrite))
            {
                cf.Shift(1, 1);
                List<RunDate> sd = cf.Schedule;
                okDlg = sd[0].Day == 1 && sd[1].Day == 3 && sd[2].Day == 4 && sd[sd.Count - 1].Day == 10 && cf.Start == s2;
                // Raumwechsel: Lauf 13570041 nach Puffer (Splitten bleibt), ab Lauf 13570042 alle Folgelaeufe nach Lyo
                cf.SetRoom(3, "Puffer", false); cf.SetRoom(5, "Lyo", true);
                sd = cf.Schedule;
                okRoomDlg = sd[3].Row == "Puffer" && sd[4].Row == "Mix / Split" && sd[5].Row == "Lyo" && sd[7].Row == "Lyo" && sd[1].Row == "Suite 2" && sd[0].Row == "Suite 2";
            }
            // Datums-Ueberschreibung: nur der letzte Lauf (13570043) einen Tag spaeter
            List<RunDate> sched = q.Schedule();
            RunDate last = sched.Find(delegate(RunDate d) { return d.Step == 1 && d.Run == 3; });
            last.Day += 1;
            string msg = q.Apply(store, s2, ApplyMode.FillEmpty, sched);
            int suite2 = rowInfo.Find(delegate(RowInfo r) { return r.Name == "Suite 2"; }).Id, mix = rowInfo.Find(delegate(RowInfo r) { return r.Name == "Mix / Split"; }).Id;
            Entry e8 = store.Get(s2.AddDays(7), suite2), e9 = store.Get(s2.AddDays(8), suite2), m3 = store.Get(s2.AddDays(2), mix);
            bool okOver = (e8 == null || e8.Content.IndexOf("13570043") < 0) && e9 != null && e9.Content == "Lauf 13570043" && m3 != null && m3.Content.StartsWith("Splitten\nUnterlauf 13574186");
            int no, iv; DateTime lastDay;
            bool found = Package.LastRun(store, "1357", out no, out lastDay, out iv);
            bool okLast = found && no == 43 && lastDay == s2.AddDays(8) && iv == 2;
            sb.AppendLine("Kampagne: Dialog/Mitverschieben=" + Ok(okDlg) + " Überschreibung=" + Ok(okOver) + " früheKampagne=" + Ok(okLast) + " (letzter " + no + ", Abstand " + iv + ") " + msg);

            // Rhythmus aus der frueheren Kampagne (Laeufe 40-43 an Tag 2, 4, 6, 9 -> Abstaende 2, 2, 3) und Raumwechsel beim Uebernehmen
            List<int> gp = Package.PastGaps(store, "1357");
            bool okGaps = gp.Count == 3 && gp[0] == 2 && gp[1] == 2 && gp[2] == 3, okRh, okRoom;
            DateTime s3 = s2.AddDays(28);
            int lyo = rowInfo.Find(delegate(RowInfo r) { return r.Name == "Lyo"; }).Id;
            using (CampaignForm cf = new CampaignForm(q, store, s3, ApplyMode.Overwrite))
            {
                List<RunDate> a = cf.Schedule;
                RunDate l3 = a.Find(delegate(RunDate d) { return d.Step == 1 && d.Run == 3; }), p3 = a.Find(delegate(RunDate d) { return d.Step == 2 && d.Run == 3; });
                okRh = cf.UseRhythm && l3.Day == 9 && p3.Day == 10 && l3.Label == "Lauf 13570047";
                cf.UseRhythm = false;
                okRh &= cf.Schedule.Find(delegate(RunDate d) { return d.Step == 1 && d.Run == 3; }).Day == 8;
                cf.UseRhythm = true;
                cf.SetRoom(cf.Schedule.FindIndex(delegate(RunDate d) { return d.Step == 1 && d.Run == 1; }), "Lyo", false);
                cf.Result.Apply(store, s3, ApplyMode.FillEmpty, cf.Schedule);
                Entry ly = store.Get(s3.AddDays(3), lyo), su = store.Get(s3.AddDays(3), suite2), su2 = store.Get(s3.AddDays(5), suite2);
                okRoom = ly != null && ly.Content == "Lauf 13570045" && (su == null || su.Content.IndexOf("13570045") < 0) && su2 != null && su2.Content == "Lauf 13570046";
            }
            sb.AppendLine("Rhythmus/Raum: Abstände=" + Ok(okGaps) + " [" + string.Join(",", gp.ConvertAll(delegate(int g) { return g.ToString(); }).ToArray()) + "] Rhythmus=" + Ok(okRh)
                + " RaumDialog=" + Ok(okRoomDlg) + " RaumÜbernahme=" + Ok(okRoom));

            // Testpaket (Produkt 1234): Datei-Roundtrip, rote PA-Kennung als Format, Uebernahme mit Raumwechsel aller Zeilen auf die (umbenannte) erste Zeile
            Package mk = Package.SampleCampaign("Testpaket");
            string mp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selftest_testpaket.apkg");
            mk.Save(mp);
            Package mq = Package.Load(mp);
            bool okMk = mq.ProductCode == "1234" && mq.Steps.Count == mk.Steps.Count && mq.Steps[2].Ident == mk.Steps[2].Ident;
            for (int s = 0; s < mk.Steps.Count; s++) for (int r = 0; r < mk.Steps[s].Count; r++) okMk &= mq.Steps[s].Compose(r, "1234") == mk.Steps[s].Compose(r, "1234");
            string l0 = mq.Steps[2].Compose(0, "1234");
            okMk &= l0.StartsWith("Lauf – Auftrag/Netzwerk-ID 12340001\nPumpe: ") && l0.EndsWith("\nPA 40010001");
            DateTime s4 = s2.AddDays(70);
            using (CampaignForm cf = new CampaignForm(mq, store, s4, ApplyMode.Overwrite)) { cf.SetRoom(0, rowInfo[0].Name, true); msg = cf.Result.Apply(store, s4, ApplyMode.Overwrite, cf.Schedule); }
            int r0 = rowInfo[0].Id;
            Entry lauf = store.Get(s4.AddDays(2), r0), lauf2 = store.Get(s4.AddDays(8), r0), mi = store.Get(s4.AddDays(3), r0), bf = store.Get(s4.AddDays(6), r0), rest = store.Get(s4.AddDays(4), r0);
            bool okRed = false;
            if (lauf != null) foreach (FormatSpan f in lauf.Spans) okRed |= f.Rgb == 0xFF0000 && f.Bold && f.Size > 10 && lauf.Content.Substring(f.Start, f.Length) == "PA 40010001";
            bool okDays = lauf != null && lauf.Content.StartsWith("Lauf – Auftrag/Netzwerk-ID 12340001") && mi != null && mi.Content.StartsWith("Mischen 12340001")
                && bf != null && bf.Content == "Backfill 12340001\nTag 2 von 2" && (rest == null || rest.IsEmpty) && lauf2 != null && lauf2.Content.EndsWith("PA 40010002");
            sb.AppendLine("Testpaket: Roundtrip=" + Ok(okMk) + " PA rot=" + Ok(okRed) + " Tage=" + Ok(okDays) + " " + msg);
        }

        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }

        // Selbsttest (Admin): Zuklappen, Bearbeiten mit Format, LKZ, Struktur, Export/Import, Paket-Schritte.
        public string SelfTest()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            quiet = true;
#if DEMO
            if (cfg != null) return SelfTestDemo();   // Vorfuehr-Version: eigene Tests (MainForm.SelfTestDemo.cs)
#endif
            try { SelfTestSave(sb); }
            catch (System.IO.IOException ex)
            {
                if (!systemPc) throw;
                if (editCell != null) EndEdit(false);
                sb.AppendLine("Speichern erwartungsgemäß fehlgeschlagen (Systemrechner): " + ex.Message);
            }
            Entry o = new Entry(); o.Content = "abc"; o.People = new[] { "AB, CD", "", "" };
            Entry n1 = o.Clone(); n1.Content = "ab"; Entry n2 = o.Clone(); n2.People[0] = "AB"; Entry n3 = o.Clone(); n3.Content = "abd"; n3.People[0] = "ab, cd, EF";
            Entry w = new Entry(); w.Content = "Lauf 1234 A"; Entry w1 = w.Clone(); w1.Content = "           "; Entry w2 = w.Clone(); w2.Content = "Lauf 1234AB";
            Entry w3 = w.Clone(); w3.Content = "Lauf 1235 B";
            sb.AppendLine("Löschen erkannt: kürzer=" + Ok(RemovesText(o, n1)) + " Kürzel=" + Ok(RemovesText(o, n2)) + " nurErgänzt=" + Ok(!RemovesText(o, n3))
                + " gleichlang überschrieben=" + Ok(RemovesText(w, w1)) + " zusammengezogen=" + Ok(RemovesText(w, w2)) + " Wort ersetzt=" + Ok(!RemovesText(w, w3))
                + " Recht Produktion=" + Ok(users.Package("Produktion") != null && Array.IndexOf(users.Package("Produktion").Rights, Perm.DeleteText) >= 0));
            byte[] blob = Protected.Protect("Geheim äöü", "pw1");
            bool wrong = false, tampered = false;
            try { Protected.Unprotect(blob, "pw2"); } catch (System.Security.Cryptography.CryptographicException) { wrong = true; }
            blob[40] ^= 1;
            try { Protected.Unprotect(blob, "pw1"); } catch (System.Security.Cryptography.CryptographicException) { tampered = true; }
            blob[40] ^= 1;
            sb.AppendLine("Protected: ok=" + (Protected.Unprotect(blob, "pw1") == "Geheim äöü") + " falschesPw=" + wrong + " manipuliert=" + tampered);
            sb.AppendLine(UserAdminForm.SelfTest());
            if (systemPc) sb.AppendLine("Protokoll/Sicherung: übersprungen (Systemrechner, Speichern nicht möglich)"); else SelfTestData(sb);
            SelfTestFiles(sb);
            SelfTestSite(sb);
            if (!systemPc) SelfTestMulti(sb);
            SelfTestCampaignStart(sb);
            SelfTestEquipment(sb);
            SelfTestDuties(sb);
            SelfTestProducts(sb);
            SelfTestStammdaten(sb);
            SelfTestRoles(sb);
            SelfTestPlan(sb);
            SelfTestF(sb);   // Plan-Datei, Sicherung nach Zeitplan, Paket-Editor-Groesse (MainForm.SelfTestF.cs)
            SelfTestBuffers(sb);   // Puffer-Liste: XML, Migration, Merge, Paket (MainForm.SelfTestBuffers.cs)
            SelfTestNotes(sb);   // Notizzeilen: leer-Regel, XML, Konflikt, Paket-Modi, Laufnummern, Protokoll (MainForm.SelfTestNotes.cs)
            SelfTestNotesUi(sb);   // Notizzeilen in Ansicht und Editor
            SelfTestG(sb);   // Puffer-Varianten, Migration, 2 Wochen, Hervorhebung, Offene Pflichten (MainForm.SelfTestG.cs)
            SelfTestA(sb);
            SelfTestAnsicht2(sb);   // Chips, LKZ dringend, Rundgang-Datum, Auswahlrahmen (MainForm.SelfTestAnsicht2.cs)
            sb.AppendLine(PackageForm.TestFlow(store, equip, start));
            SelfTestPaket2(sb);   // Produktcode frei, Laufnummer 8-stellig, Hinweis-Klick, Puffer-Variante, Bausteine (MainForm.SelfTestPaket2.cs)
            SelfTestBackup(sb);   // Packen (Back-Up), Kampagnen-Assistent (MainForm.SelfTestBackup.cs)
            SelfTestEquip(sb);    // Equipment-Uebersicht (Tabelle, Matrix, Serie) und manueller Status (MainForm.SelfTestEquip.cs)
            SelfTestEquipExport(sb);
            SelfTestEquipFile(sb);   // Status aus Datei, Vorrang manuell (MainForm.SelfTestEquipFile.cs)
            SelfTestEquipQuick(sb);   // Schnellerfassung, Nicht zugeordnet, Passt zu, Felder (MainForm.SelfTestEquipQuick.cs)
            SelfTestPaket3(sb);   // Wiederholmodi, Verschieben, Varianten mit Anzahl, Ansatz, alte Pakete (MainForm.SelfTestPaket3.cs)
            SelfTestTakeOver(sb);   // Soft-Lock: Uebernahme, Nachrichten, Sperrenwechsel (MainForm.SelfTestTakeOver.cs)
            SelfTestE1(sb);   // Zeitmodell des Pakets: Golden-Test, JeLauf, Ausfall, Kennung, Normalize, Paketformat 7 (MainForm.SelfTestE1*.cs)
            SelfTestE3(sb); SelfTestE2(sb); SelfTestE4(sb); SelfTestV(sb); SelfTestE5(sb);   // Kennungstabelle, Notizen aus Paketen, Materialvorlage (MainForm.SelfTestE2.cs)
            SelfTestPB(sb);   // Puffer im Paket: Standard-Anzahl, mehrere Puffer je Termin, Namen, Pufferpaket (MainForm.SelfTestPB.cs)
            SelfTestUX1(sb);   // UX-1a: Equipment je Puffer, Migration, Standardpuffer, Pufferpaket je Produkt (MainForm.SelfTestUX1.cs)
            SelfTestUX1b(sb);  // UX-1b: Puffer-Karten im Paket-Editor, Geltungsbereich, Pufferpaket-Dialog (MainForm.SelfTestUX1b.cs)
            sb.Append(PackageForm.TestUx2(equip, start));   // UX-2: Problemtexte mit Beteiligten, Gruppen, nicht validiertes Equipment (PackageForm.TestUx2.cs)
            SelfTestUX5a(sb);   // UX-5a: Packen-Nr. im Kopf, Mehr Optionen, Kennung, Pufferpakete verwalten (MainForm.SelfTestUX5a.cs)
            using (MainForm mx = new MainForm("ux4", true, null, null)) { mx.FitToCursorScreen(); sb.AppendLine("UX4 Hauptfenster startet maximiert: " + Ok(mx.WindowState == FormWindowState.Maximized)); }
            quiet = false;
            return sb.ToString();
        }

        void SelfTestSave(System.Text.StringBuilder sb)
        {
            int n = rowInfo.Count;
            string coll = ""; for (int r = 0; r < n; r++) coll += (view.Collapsed[r] ? "z" : "o");
            sb.AppendLine("Zeilen=" + n + " Standard offen(o)/zu(z): " + coll + " | " + rowInfo[4].Name + " vor " + rowInfo[5].Name);

            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 0 && x.Col == 4) c = x;
            int rowId = rowInfo[0].Id; DateTime day = c.Day;
            BeginEdit(c);
            bool dClean = !HasUnsavedChanges;
            string t0 = edContent.Text; edContent.Text = t0 + "x"; bool dText = HasUnsavedChanges; edContent.Text = t0;
            bool dBack = !HasUnsavedChanges;
            edContent.Select(0, Math.Min(2, edContent.TextLength)); ApplyColor(Swatches[1]); bool dFmt = edContent.TextLength == 0 || HasUnsavedChanges;
            sb.AppendLine("Geändert-Flag: sauber=" + Ok(dClean) + " Text=" + Ok(dText) + " zurück=" + Ok(dBack) + " Format=" + Ok(dFmt));
            edContent.SelectAll(); ClearFormat();
            edContent.Text = "Neuer Text LKZ ABC"; edContent.Select(11, 3);
            edContent.SelectionColor = Color.Red; edContent.SelectionFont = MakeFont("Arial", 14f, FontStyle.Bold | FontStyle.Italic);
            edPpl[0].Text = "TEST, ABCD";
            c.Lkz = true;
            bool ok = EndEdit(true);
            Entry e = store.Get(day, rowId);
            sb.AppendLine("Save=" + ok + " [" + e.Content + "] Spans=" + e.SpanKey() + " F=[" + e.People[0] + "] Lkz=" + e.Lkz + " Ver=" + e.Version);

            // LKZ-Haken ohne Bearbeitungsmodus
            Rebuild(false);
            CellData c2 = view.Cells[4]; ToggleLkz(c2);
            sb.AppendLine("Lkz nach Klick: " + store.Get(day, rowId).Lkz);
            Entry other = store.Get(day, rowId).Clone(); other.Content = "Von anderem PC"; store.Save(day, rowId, other);   // Block ist jetzt veraltet
            bool lk0 = c2.Lkz; ToggleLkz(c2); Entry aft = store.Get(day, rowId);
            sb.AppendLine("Lkz-Konflikt: Inhalt behalten=" + Ok(aft.Content == "Von anderem PC" && aft.Lkz == !lk0 && c2.Content == aft.Content && c2.Lkz == aft.Lkz));

            // Struktur: Zeile umbenennen, neue Zeile, Reihenfolge
            List<RowInfo> rows = store.GetRows();
            rows[0].Name = "Suite 1A"; rows[0].MinLines = 5;
            RowInfo nr = new RowInfo(); nr.Name = "Neu"; rows.Insert(1, nr);
            store.SaveStructure(rows);
            Rebuild(false);
            sb.AppendLine("Struktur: " + rowInfo[0].Name + "/" + rowInfo[0].MinLines + ", " + rowInfo[1].Name + " (Id " + rowInfo[1].Id + "), Zeilen=" + rowInfo.Count);

            // Paket: Woche exportieren, Schritte anwenden
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selftest.apkg");
            Package p = Package.FromWeek(store, start, "Test");
            PackageStep st = new PackageStep(); st.Title = "Lauf"; st.Day = 2; st.Row = "Neu"; st.Count = 3; st.Interval = 1; st.Note = "nur wenn frei";
            EquipAssign tff = new EquipAssign(); tff.Short = "TFF"; tff.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); tff.Choices.Add(new EquipChoice("TFF", "H1-X-0102")); tff.Alternate = true; tff.Fallback = "H1-X-0103";
            EquipAssign pt = new EquipAssign(); pt.Short = "PT"; pt.Choices.Add(new EquipChoice("PT", "H1-T-0102")); pt.Note = "Charge 7 mit einer sehr langen Notiz, die umbrechen muss und eingerückt weiterlaufen soll";
            EquipAssign sl = new EquipAssign(); sl.Short = "Schlauch"; sl.Note = "S-12 (grün)";
            st.Equipment.Add(pt); st.Equipment.Add(tff); st.Equipment.Add(sl);
            p.Steps.Add(st);
            // Austausch-Gruppe: Freezetank 1 und 2 wechseln sich ab (Positions-Kurzname folgt dem gewaehlten Geraet)
            PackageStep fz = new PackageStep(); fz.Title = "Freeze"; fz.Day = 6; fz.Row = "Neu"; fz.Count = 3;
            EquipAssign fa = new EquipAssign(); fa.Short = "FZ1"; fa.Alternate = true;
            fa.Choices.Add(new EquipChoice("FZ1", "H2-T-0251")); fa.Choices.Add(new EquipChoice("FZ2", "H2-T-0252"));
            fz.Equipment.Add(fa); p.Steps.Add(fz);
            sb.AppendLine("Austausch FZ1: " + store.GetCatalog().Interchangeable("FZ1").Count + " Geräte, Positionen=" + store.GetCatalog().Positions.Count);
            sb.AppendLine("Platzhalter TFF: " + store.GetCatalog().Position("TFF").Placeholder() + " | Pumpe: " + store.GetCatalog().Position("Pumpe").Placeholder());
            p.Save(path);
            Package q = Package.Load(path);
            PackageBlock fr = q.Blocks.Find(delegate(PackageBlock b) { return b.Row == "Suite 1A" && b.Day == 5; });
            sb.AppendLine("Paket: Blöcke=" + q.Blocks.Count + " Schritte=" + q.Steps.Count + " Format erhalten=" + (fr == null ? -1 : fr.Data.Spans.Count));
            sb.AppendLine("Apply: " + q.Apply(store, start.AddDays(7), ApplyMode.FillEmpty, null));
            for (int k = 0; k < 3; k++)
            {
                Entry ne = store.Get(start.AddDays(8 + k), rowInfo[1].Id);
                sb.AppendLine("Lauf-Tag " + (k + 1) + ": [" + (ne == null ? "-" : ne.Content.Replace("\n", "|")) + "]");
            }
            SelfTestCampaign(sb);
            Rebuild(true);
            sb.AppendLine("Find 'TEST': " + store.Find("TEST").Count);
        }
    }
}
#endif
