#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests N7 (Befunde aus dem Review Paket-Editor + Puffer): Puffer-Karte vor dem Start (Normalize/Sperre "pufvor"), Kennung mit ";",
    // Vorlage mit "alle"-Zeile, Puffer umsortieren, Pufferpaket ohne fremde Aenderungen zu ueberschreiben; Editor: Tippen gebuendelt,
    // Anordnung nach Ausnahme, "gilt für"-Dialog, Hauptlauf-Rueckfrage, Timer, gemeinsame Zeilen, Esc beim Ziehen, ▲▼ je Karte, Hinweise.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        void SelfTestN7(StringBuilder sb)
        {
            try { SelfTestN7Core(sb); } catch (Exception ex) { sb.AppendLine("N7 Modell: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            sb.Append(PackageForm.TestN7(store, equip, start));
        }

        void SelfTestN7Core(StringBuilder sb)
        {
            // (1) Lauf 1 an Tag 1, Puffer "zu jedem Lauf" 1 Tag davor, Karte P1 "3 Tage vor dem Lauf": ohne Normalize Sperre, nie Schreiben vor Start
            Package p = new Package(); p.Name = "N7-vor";
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Day = 1; lf.StartNo = 1; lf.Count = 2; lf.Interval = 6;
            p.Steps.Add(lf);
            PackageStep pu = UxPuf("P1", -1, true, 1, "AT-1");
            PufferSpec s2 = new PufferSpec(); s2.Name = "P2"; s2.Typ = "mobil"; s2.Lines.Add(UxEq("Ansatztank", "AT-2")); pu.Puffers.Add(s2); pu.SyncPuffers();
            pu.Puffers[0].OffsetDays = -3; p.Steps.Add(pu);
            MemoryStore ms = new MemoryStore(); int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2036, 6, 2);
            List<PackageProblem> pr = p.Check();
            bool okBlock = pr.Exists(delegate(PackageProblem x) { return x.Code == "pufvor" && x.Blocks && x.Text.Contains("Puffer 1") && x.Text.Contains("3 Tage vor dem Kampagnen-Start") && x.Action.Length > 0; })
                && PackageProblem.RuleTitle("pufvor").Length > 0 && p.MinCardDelta(pu, p.Day1) == -2;
            bool thrown = false;
            try { p.Apply(ms, d0, ApplyMode.Overwrite, null); } catch (InvalidOperationException) { thrown = true; }
            bool none = true; for (int k = -5; k <= 10; k++) none &= ms.Get(d0.AddDays(k), puf) == null;
            okBlock &= thrown && none;
            bool moved = p.Normalize();
            List<RunDate> plan = p.Schedule();
            int head = int.MaxValue; foreach (RunDate d in plan) if (d.Step == 0) head = Math.Min(head, d.Day);
            bool okNorm = moved && head == 4 && !p.Check().Exists(delegate(PackageProblem x) { return x.Code == "pufvor"; });
            string ap = "";
            try { p.Apply(ms, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = " Ausnahme: " + ex.Message; }
            okNorm &= ap.Length == 0 && ms.Get(d0.AddDays(-1), puf) == null && ms.Get(d0, puf) != null && ms.Get(d0, puf).Buffers.Count == 1 && ms.Get(d0, puf).Buffers[0].Name == "P1"
                && ms.Get(d0.AddDays(2), puf) != null && ms.Get(d0.AddDays(2), puf).Buffers[0].Name == "P2";
            // Kampagnenstart: Lauf 1 von Hand auf den Starttag gelegt -> Karte laege davor -> Sperre (Kampagnen-Vorschau rot)
            List<RunDate> sched = p.Schedule();
            foreach (RunDate d in sched) d.Day -= 3;
            okNorm &= p.Check(new MemoryStore(), d0, ApplyMode.Overwrite, sched).Exists(delegate(PackageProblem x) { return x.Code == "pufvor" && x.Blocks && x.Run == 0; });
            sb.AppendLine("N7 Karte vor dem Start: Sperre ohne Normalize, nichts geschrieben " + Ok(okBlock) + " Normalize schiebt, Schreiben ab Start " + Ok(okNorm) + ap);

            // (11) Kennung mit ";" bleibt beim Speichern unveraendert; alte Datei ohne ";;" liest sich wie bisher
            Package kp = new Package(); kp.Name = "N7-Kennung";
            PackageStep ks = new PackageStep(); ks.Title = "Lauf"; ks.Row = "Suite 1"; ks.Kind = StepKind.Run; ks.Day = 1; ks.Count = 3; ks.Interval = 2; ks.StartNo = 1; ks.IdentStart = "PA 40010001";
            ks.IdentOver[1] = "PA 4001;A;"; ks.IdentOver[2] = ";B"; kp.Steps.Add(ks);
            PackageStep kr = Package.FromXml(kp.ToXml()).Steps[0];
            bool okId = kr.IdentOver.Count == 2 && kr.IdentOver[1] == "PA 4001;A;" && kr.IdentOver[2] == ";B";
            List<string> old = Package.SplitIdents("1=PA 1, 2;3=X");
            okId &= old.Count == 2 && old[0] == "1=PA 1, 2" && old[1] == "3=X";

            // (12) Vorlage mit gemeinsamer Position: vorhandene "alle"-Zeile wird nicht doppelt angelegt
            PackageStep ts = UxPuf("T", 0, false, 1, "AT-1");
            PufferShare all = new PufferShare(); all.Eq = UxEq("WFI", "W-1"); all.Scope = 0; ts.PufferShared.Add(all);
            PufferTemplate tpl = new PufferTemplate(); tpl.Name = "T2"; tpl.Typ = "mobil";
            PufferTemplatePos tp = new PufferTemplatePos(); tp.Short = "WFI"; tp.Shared = true; tpl.Positions.Add(tp);
            Package.ApplyPufferTemplate(ts, tpl);
            bool okTpl = ts.Puffers.Count == 2 && ts.PufferShared.Count == 1 && ts.PufferShared[0].Scope == 0 && ts.PufferChoices(1, 0).Exists(delegate(EquipChoice c) { return c.Id == "W-1"; });

            // (U4) Puffer umsortieren: Bits der gemeinsamen Zeilen ziehen mit, "alle" bleibt, Plan und XML folgen der Reihenfolge
            PackageStep mv = UxPuf("A", 0, false, 1, "AT-1");
            foreach (string n in new[] { "B", "C" }) { PufferSpec x = new PufferSpec(); x.Name = n; x.Typ = "mobil"; x.Lines.Add(UxEq("Ansatztank", "AT-" + n)); mv.Puffers.Add(x); }
            PufferShare g1 = new PufferShare(); g1.Eq = UxEq("WFI", "W-1"); g1.Scope = 3; PufferShare g2 = new PufferShare(); g2.Eq = UxEq("VT", "V-1"); g2.Scope = 4;
            PufferShare g3 = new PufferShare(); g3.Eq = UxEq("Tween", "TW-1"); g3.Scope = 0;
            mv.PufferShared.Add(g1); mv.PufferShared.Add(g2); mv.PufferShared.Add(g3); mv.SyncPuffers();
            bool okMove = mv.MovePuffer(0, 2) && mv.Puffers[0].Name == "B" && mv.Puffers[2].Name == "A" && g1.Scope == 5 && g2.Scope == 2 && g3.Scope == 0 && !mv.MovePuffer(0, 3) && !mv.MovePuffer(1, 1);
            List<BufferItem> bi = mv.BuffersFor(0, "");
            okMove &= bi.Count == 3 && bi[0].Name == "B" && bi[1].Name == "C" && bi[2].Name == "A";
            Package mp = new Package(); mp.Name = "N7-Reihe"; mp.Steps.Add(mv);
            PackageStep mr = Package.FromXml(mp.ToXml()).Steps[0];
            okMove &= mr.Puffers.Count == 3 && mr.Puffers[0].Name == "B" && mr.Puffers[2].Name == "A" && mr.PufferShared.Count == 3 && mr.PufferShared[0].Scope == 5;

            // (8) Pufferpaket speichern: Katalog frisch geladen, nur das Pufferpaket des Produkts ersetzt
            MemoryStore cs = new MemoryStore();
            Catalog c0 = cs.GetCatalog();
            ProductProfile a1 = new ProductProfile(); a1.Code = "7101"; a1.Name = "Alt"; ProductProfile a2 = new ProductProfile(); a2.Code = "7102"; a2.Name = "Zwei";
            c0.Products.Add(a1); c0.Products.Add(a2); cs.SaveCatalog(c0);
            ProductProfile edit = cs.GetCatalog().Clone().Product("7101");   // Dialog arbeitet auf einer Kopie
            Catalog other = cs.GetCatalog().Clone(); other.Product("7102").Name = "Geändert"; other.Product("7101").Name = "Neu"; cs.SaveCatalog(other);   // anderer Admin
            PufferTemplate pt = new PufferTemplate(); pt.Name = "Lsg"; pt.Typ = "mobil"; edit.Puffer = new List<PufferTemplate>(new[] { pt });
            cs.SaveCatalog(PufferPackAdmin.Merged(cs, edit));
            Catalog after = cs.GetCatalog();
            bool okCat = after.Product("7102").Name == "Geändert" && after.Product("7101").Name == "Neu" && after.Product("7101").Puffer.Count == 1 && after.Product("7101").Puffer[0].Name == "Lsg";
            ProductProfile nw = new ProductProfile(); nw.Code = "7103"; okCat &= PufferPackAdmin.Merged(cs, nw).Product("7103") != null;
            sb.AppendLine("N7 Kennung mit Semikolon " + Ok(okId) + " Vorlage ohne doppelte alle-Zeile " + Ok(okTpl) + " Puffer umsortieren " + Ok(okMove) + " Pufferpaket ohne fremde Änderungen zu überschreiben " + Ok(okCat));
        }
    }

    partial class PackageForm
    {
        public static string TestN7(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux5cSample("N7", false);   // Laeufe an 3, 5, 7, 9; Puffer 1 Tag davor, Karte 1 zwei Tage davor
            PackageStep pz = sample.Steps[sample.Steps.Count - 1];
            PufferShare w2 = new PufferShare(); w2.Eq = UxA("WFI", "H1-T-0121"); w2.Scope = 3; pz.PufferShared.Add(w2);
            int live0 = RowReorder.LiveCount;
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0); f.Size = new Size(1024, 768);
            int stage = 0; string res = "";
            try
            {
                f.Show(); Application.DoEvents();
                f.TestSelectBuffer(); Application.DoEvents();
                PackageStep pu = f.cur, head = f.StepsView().Head;
                f.TestOpenCard(0); Application.DoEvents();

                stage = 1; // (1) Karte 1 auf "3 Tage vor dem Lauf": Ursprung wandert, keine Karte vor Tag 1, nichts rot
                PufferCard c0 = f.cards[0];
                c0.TestDir.SelectedIndex = 0; c0.TestNum.Value = 3; Application.DoEvents();
                List<RunDate> plan = f.Plan(); int min = int.MaxValue;
                foreach (RunDate d in plan) min = Math.Min(min, d.Day + f.StepsView().MinCardDelta(f.steps[d.Step], f.pk.Day1));
                bool okCard = pu.Puffers[0].OffsetDays == -3 && min == 1 && f.FirstOf(head, plan) == 4 && !f.strip.TestItems.Exists(delegate(PlanStrip.Item i) { return i.Bad; }) && !HasHint(f, "vor dem Kampagnen-Start");
                MemoryStore ms = new MemoryStore(); int puf = PufferSetup.Row(ms.GetRows()).Id; DateTime d0 = new DateTime(2036, 6, 2);
                f.StepsView().Clone().Apply(ms, d0, ApplyMode.Overwrite, null);
                okCard &= ms.Get(d0.AddDays(-1), puf) == null && ms.Get(d0, puf) != null && ms.Get(d0, puf).Buffers.Count == 1;
                pu.Puffers[0].OffsetDays = -7; f.UpdateView(false); Application.DoEvents();   // ohne Normalize (z. B. alte Datei): rot und gesperrt
                okCard &= f.strip.TestItems.Exists(delegate(PlanStrip.Item i) { return i.Bad && i.Step == pu; }) && f.hintList.Exists(delegate(Hint h) { return h.Blocks && h.Text.Contains("vor dem Kampagnen-Start"); });
                f.PufferChanged(); Application.DoEvents();   // naechste Aenderung normalisiert
                okCard &= !HasHint(f, "vor dem Kampagnen-Start");
                pu.Puffers[0].OffsetDays = -3; f.PufferChanged(); Application.DoEvents();

                stage = 2; // (2)/(U8) Tippen im Geraete-Feld: Ansicht nach Ruhe, kein Schnappschuss je Zeichen; Tooltip erklaert Strg+Z
                EquipRow er = f.cards[0].TestRows[0];
                int snap0 = f.snapAt;
                f.ActiveControl = er.TestIdBox; er.TestIdBox.Text = "AT-3";
                bool okType = f.Typing() && f.viewDue && f.snapAt == snap0 && pu.Puffers[0].Lines[0].Choices[0].Id == "AT-3";
                f.ActiveControl = f.cbRow; okType &= !f.Typing();
                f.CommitUndo(); okType &= f.snapAt == snap0 + 1 && !f.viewDue;
                okType &= er.TestTip.GetToolTip(er.TestIdBox).Contains("Strg+Z") && er.TestTip.GetToolTip(er.TestFbBox).Contains("Strg+Z");

                stage = 3; // (3) Ausnahme beim Anordnen sperrt nicht dauerhaft; (U7) kurzer Knopftext, Zusammenfassung im Tooltip
                Catalog keep = f.cat; f.cat = null;
                try { f.LayoutDetails(); } catch (NullReferenceException) { }
                f.cat = keep; bool okLay = !f.laying;
                f.LayoutDetails();
                okLay &= f.bMore.Text.StartsWith("▸ Mehr Optionen") && !f.bMore.Text.Contains("(eigene Nummer") && f.tip.GetToolTip(f.bMore).StartsWith("Mehr Optionen:")
                    && TextRenderer.MeasureText("▸ Mehr Optionen", f.bMore.Font).Width < f.bMore.Width;

                stage = 4; // (4)/(U3) "gilt für" als Dialog: Esc verwirft, ohne Haken kein Übernehmen
                bool okScope;
                using (PufferScopeForm sf = new PufferScopeForm("WFI-Tank", new List<string>(new[] { "Puffer 1: A", "Puffer 2: B" }), 3))
                {
                    sf.Show(); sf.TestBoxes[1].Checked = false;
                    okScope = sf.Mask == 1 && sf.TestOkEnabled;
                    sf.TestBoxes[0].Checked = false; okScope &= !sf.TestOkEnabled;
                    sf.TestEsc(); okScope &= sf.DialogResult == DialogResult.Cancel;
                }

                stage = 5; // (5) Hauptlauf entfernen: Rueckfrage kennt die abhaengigen Schritte; (7) gemeinsame Zeile umsortiert -> andere Karte stimmt
                bool okHead = f.HeadDeps(head) == 4 && f.HeadDeps(pu) == 0;
                List<EquipRow> own0 = f.cards[0].TestRows; EquipRow sh0 = own0[own0.Count - 2];   // erste gemeinsame Zeile (zwei gemeinsame am Ende)
                f.cards[0].TestReorder.Step(sh0, 1); Application.DoEvents();
                List<EquipAssign> a1 = f.cards[1].TestAssigns;
                bool okShare = pu.PufferShared[0].Eq.Choices[0].Id == "H1-T-0121" && a1.Count >= 2 && a1[a1.Count - 2] == pu.PufferShared[0].Eq && a1[a1.Count - 1] == pu.PufferShared[1].Eq;

                stage = 6; // (10) Esc bricht Ziehen ab (Zeilen-Griff, Kalender)
                List<EquipRow> rs = f.cards[0].TestRows; RowGrip gr = (RowGrip)rs[0].TestGrip; EquipAssign first = pu.Puffers[0].Lines[0];
                gr.TestPress(); f.cards[0].TestReorder.DragTo(rs[0], f.cards[0].TestSlot(1).Bottom);
                bool okEsc = gr.TestEsc() && !gr.TestPressed && !f.cards[0].TestReorder.LineVisible && pu.Puffers[0].Lines[0] == first;
                PlanStrip.Item it = f.strip.TestItems.Find(delegate(PlanStrip.Item i) { return i.Step == head; });
                int hd = f.FirstOf(head, f.Plan());
                f.strip.TestDrag(it, it.Day + 2);
                okEsc &= f.strip.TestEsc() && !f.strip.TestDragging && f.FirstOf(head, f.Plan()) == hd;

                stage = 7; // (U4) Karten umsortieren ▲▼ (Rand deaktiviert), Rueckgaengig; (U6) Entfernen meldet Rueckgaengig
                bool okUp = !f.cards[0].TestCardUp.Enabled && f.cards[0].TestCardDown.Enabled && f.cards[1].TestCardUp.Enabled && !f.cards[1].TestCardDown.Enabled && f.cards[1].TestCardUp.Visible;
                string n0 = pu.Puffers[0].Name;
                f.MovePuffer(1, 0); Application.DoEvents();
                okUp &= pu.Puffers[1].Name == n0 && f.cards[0].TestSummary.StartsWith("Puffer 1: Lösung B") && f.stripMsg.Text.Contains("Stelle 1");
                f.UndoStep(-1); Application.DoEvents(); pu = f.cur;
                okUp &= pu.Puffers[0].Name == n0;
                f.RemovePuffer(1, false); Application.DoEvents();
                bool okRem = f.cur.Puffers.Count == 1 && f.stripMsg.Text.Contains("entfernt – Rückgängig");
                f.UndoStep(-1); Application.DoEvents(); okRem &= f.cur.Puffers.Count == 2;

                stage = 8; // (14) ein gemeinsamer Hover-Timer
                bool okTimer = RowReorder.LiveCount >= live0 + 3 && RowReorder.TimerRunning;
                f.doneTimer.Start();
                res = "N7 Editor: Karte vor dem Start " + T(okCard) + " Tippen gebündelt, Strg+Z-Hinweis " + T(okType) + " Anordnung nach Ausnahme, Mehr Optionen " + T(okLay)
                    + " gilt-für-Dialog " + T(okScope) + " Hauptlauf-Rückfrage " + T(okHead) + " gemeinsame Zeile umsortiert " + T(okShare) + " Esc beim Ziehen " + T(okEsc)
                    + " Karten ▲▼ " + T(okUp) + " Entfernen mit Rückgängig-Hinweis " + T(okRem);
                res += " Hover-Timer " + T(okTimer);
            }
            catch (Exception ex) { res = "N7 Editor: Ausnahme (Abschnitt " + stage + ") " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Replace("\r", "").Replace("\n", " / "); }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
            bool okDisp = !f.doneTimer.Enabled && RowReorder.LiveCount == live0;   // (6) Timer mit dem Fenster freigegeben, (14) Instanzen abgemeldet
            bool tipGone = false;   // (15) ToolTip der Zeile wird mit ihr freigegeben
            EquipRow lone = new EquipRow(UxA("VT", "VT-1"), store.GetCatalog(), "", delegate { }, delegate(EquipAssign z) { });
            lone.TestTip.Disposed += delegate { tipGone = true; }; lone.Dispose();
            return res + " Freigabe Timer/Tooltip " + T(okDisp && tipGone) + "\r\n" + TestN7Belongs(equip, start);
        }

        // (U10) Zubehoer passt nicht zum Hauptgeraet: Schritt anspringen, betroffene Zeilen rot
        static string TestN7Belongs(IEquipmentStatusProvider equip, DateTime start)
        {
            MemoryStore ms = new MemoryStore();
            Package p = new Package(); p.Name = "N7-Zugehörigkeit"; p.ProductCode = "4321";
            PackageStep other = new PackageStep(); other.Title = "Vorher"; other.Row = "Suite 1"; other.Day = 1; p.Steps.Add(other);
            PackageStep st = new PackageStep(); st.Title = "Filtern"; st.Row = "Suite 1"; st.Day = 2;
            st.Equipment.Add(UxA("TFF", "H1-X-0102")); st.Equipment.Add(UxA("FG", "H1-F-0101-01")); st.Equipment.Add(UxA("VT", "VT-1")); p.Steps.Add(st);
            PackageForm f = new PackageForm(ms, equip, start, p, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                f.SelectStep(0); Application.DoEvents();
                f.GoToBelongs(); Application.DoEvents();
                bool ok = f.cur != null && f.cur.Title == "Filtern" && f.eqRows.Count == 3 && f.eqRows[0].Flagged && f.eqRows[1].Flagged && !f.eqRows[2].Flagged
                    && f.hintList.Exists(delegate(Hint h) { return h.Blocks && h.Text.Contains("H1-F-0101-01"); });
                f.cur.Equipment[0].Choices[0] = new EquipChoice("TFF", "H1-X-0101"); f.Changed(); Application.DoEvents();
                ok &= !f.eqRows[0].Flagged && !f.eqRows[1].Flagged;
                return "N7 Zugehörigkeit: Schritt angesprungen, Zeilen rot " + T(ok) + "\r\n";
            }
            catch (Exception ex) { return "N7 Zugehörigkeit: Ausnahme " + ex.GetType().Name + ": " + ex.Message + "\r\n"; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
    }
}
#endif
