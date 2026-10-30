#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests F2 (Freeze-Oberflaeche): Dialog mit Live-Rechnung, Speichern ueber Change (Konflikt, Rechte, Entfernen), Anzeige im Plan
    // (Vorgaenge, Fortsetzung, "gesplittet in", Hoehen/Ueberlappung, Editor), Blockstruktur-Haken, Ladezeit.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static string FzSt(DateTime d) { return d.ToString("ddd dd.MM. HH:mm", Ui.De); }

        void SelfTestFreezeUi(StringBuilder sb)
        {
            try { FzuDialog(sb); } catch (Exception ex) { sb.AppendLine("Freeze Dialog: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzuPlan(sb); } catch (Exception ex) { sb.AppendLine("Freeze Plan: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzuStruct(sb); } catch (Exception ex) { sb.AppendLine("Freeze Struktur: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzuPerf(sb); } catch (Exception ex) { sb.AppendLine("Freeze Laden: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        // Testfenster: --form freeze (Bag mit Verzoegerung), freeze2 (Tank), freeze3 (neu), freeze4 (nur lesen)
        Form TestFreezeForm(string which)
        {
            int fr = rowInfo.FindIndex(delegate(RowInfo r) { return r.Freeze; });
            DateTime day = start.AddDays(which == "freeze2" ? 3 : 1);
            Entry e = store.Get(day, rowInfo[fr].Id) ?? new Entry();
            FreezeChoice ch = new FreezeChoice(); ch.Cfg = store.GetCatalog().FreezeDefaults;
            ch.Where = BlockTitle(CellFor(fr, day)) + " · " + Ui.DayText(day);
            ch.Mains = RunsAround(day); ch.Tanks = FreezeTanks();
            if (which == "freeze3") { ch.IsNew = true; ch.Item.Start1to3[0] = day.AddHours(6); }
            else ch.Item = e.Freezes[0].Clone();
            ch.ReadOnly = which == "freeze4";
            return new FreezeForm(ch);
        }

        // ---------- Dialog ----------

        void FzuDialog(StringBuilder sb)
        {
            FreezeChoice ch = new FreezeChoice(); ch.Item = FzBag(FzD0); ch.IsNew = true; ch.Mains.Add("13570030");
            bool okRows, okLive, okReset, okTime, okSame, okBad, okHint, okArt, okParam, okStart, okCheck, okRo;
            using (FreezeForm f = new FreezeForm(ch))
            {
                string c5 = f.TestCell(5, 3), c6 = f.TestCell(6, 3), c7 = f.TestCell(7, 3);
                okRows = f.TestRows == 14 && f.TestCell(1, 1) == "FA1" && f.TestCell(3, 1) == "FA3" && f.TestCell(4, 1) == "FA1" && f.TestCell(14, 1) == "FA2"
                    && f.TestCell(2, 2).Contains("geschätzt") && !f.TestCell(1, 2).Contains("geschätzt") && f.TestCell(4, 3) == FzSt(FzD0.AddHours(7)) && f.TestCell(4, 4) == FzSt(FzD0.AddHours(13));
                f.TestSetIst(4, "04.03. 02:00");   // C4 real Dienstag 02:00 (geplant Montag 19:00 = +7 h)
                DateTime e4 = new DateTime(2031, 3, 4, 2, 0, 0);
                okLive = f.TestCell(4, 3) == FzSt(e4) && f.TestCell(7, 3) == FzSt(e4.AddHours(7)) && f.TestCell(10, 3) == FzSt(e4.AddHours(14)) && f.TestCell(5, 3) == c5 && f.TestCell(6, 3) == c6
                    && f.TestCell(4, 5) == "+7 h" && f.TestCell(7, 5) == "+7 h" && f.TestCell(5, 5) == "" && f.TestCell(4, 6) == "zurücksetzen" && f.TestCell(7, 6) == "" && f.TestItem.Ist.Count == 1;
                f.TestClickReset(4);
                okReset = f.TestCell(7, 3) == c7 && f.TestCell(4, 6) == "" && f.TestCell(4, 5) == "" && f.TestItem.Ist.Count == 0;
                f.TestSetIst(5, "21:30");   // nur Uhrzeit: Tag wie berechnet; C8 folgt (+7 h)
                okTime = f.TestCell(5, 3) == FzSt(new DateTime(2031, 3, 3, 21, 30, 0)) && f.TestCell(8, 3) == FzSt(new DateTime(2031, 3, 4, 4, 30, 0)) && f.TestCell(5, 5) == "+1 h 30 min" && f.TestCell(6, 3) == c6;
                f.TestSetIst(5, FzSt(FzD0.AddHours(8)));   // gleich dem berechneten Wert = keine Korrektur
                bool same1 = f.TestItem.Ist.Count == 0;
                f.TestSetIst(5, "21:00"); f.TestSetIst(5, "");
                okSame = same1 && f.TestItem.Ist.Count == 0 && f.TestCell(5, 3) == c5;
                f.TestSetIst(6, "abc");
                bool bad1 = f.TestHint.Contains("nicht lesbar") && f.TestItem.Ist.Count == 0;
                f.TestSetIst(6, "");
                okBad = bad1 && !f.TestHint.Contains("nicht lesbar");
                f.TestSetIst(4, "03.03. 15:00");   // vor dem Ende von C1 (18:00): nur ein Hinweis
                bool hint1 = f.TestHint.Contains("C4 startet vor dem Ende von C1");
                f.TestClickReset(4);
                okHint = hint1 && !f.TestHint.Contains("startet vor");
                f.TestSetArt(false);
                bool tank = f.TestTankVisible && !f.TestBagVisible && f.TestRows == 0 && f.TestEnd == FzSt(FzD0.AddHours(6));
                f.TestSetArt(true);
                okArt = tank && f.TestBagVisible && !f.TestTankVisible && f.TestRows == 14;
                f.TestSetGap(5);
                bool gap1 = f.TestHint.Contains("kleiner") && f.TestCell(4, 3) == FzSt(FzD0.AddHours(5));
                f.TestSetGap(7);
                okParam = gap1 && f.TestCell(4, 3) == FzSt(FzD0.AddHours(7)) && !f.TestHint.Contains("kleiner");
                f.TestSetStart(FzD0.AddHours(2));
                okStart = f.TestCell(1, 3) == FzSt(FzD0.AddHours(2)) && f.TestCell(2, 3) == FzSt(FzD0.AddHours(3)) && f.TestCell(4, 3) == FzSt(FzD0.AddHours(9));
                f.TestSetMain("123"); bool m1 = f.TestCheck() != null;
                f.TestSetMain("13570030"); bool m2 = f.TestCheck() == null;
                f.TestSetMain("");
                okCheck = m1 && m2;
            }
            FreezeChoice ro = new FreezeChoice(); ro.Item = FzBag(FzD0); ro.Item.Ist[4] = FzD0.AddHours(14); ro.ReadOnly = true;
            using (FreezeForm f2 = new FreezeForm(ro)) okRo = f2.TestRows == 14 && f2.TestCell(4, 6) == "" && f2.TestCell(7, 3) == FzSt(FzD0.AddHours(21));
            DateTime t, calc = new DateTime(2031, 3, 3, 12, 0, 0);
            bool p1 = FreezeForm.ParseTime("02:30", calc, out t) && t == new DateTime(2031, 3, 3, 2, 30, 0);
            bool p2 = FreezeForm.ParseTime("Di 04.03. 02:30", calc, out t) && t == new DateTime(2031, 3, 4, 2, 30, 0);
            bool p3 = FreezeForm.ParseTime("04.03.2031 02:30", calc, out t) && t == new DateTime(2031, 3, 4, 2, 30, 0);
            bool p4 = FreezeForm.ParseTime("04.03.31 02:30", calc, out t) && t == new DateTime(2031, 3, 4, 2, 30, 0);
            bool p5 = !FreezeForm.ParseTime("25:00", calc, out t) && !FreezeForm.ParseTime("31.02. 10:00", calc, out t) && !FreezeForm.ParseTime("13.13. 10:00", calc, out t) && !FreezeForm.ParseTime("morgen", calc, out t);
            bool p6 = FreezeForm.ParseTime("01.01. 10:00", new DateTime(2031, 12, 31, 12, 0, 0), out t) && t == new DateTime(2032, 1, 1, 10, 0, 0);   // Jahreswechsel
            sb.AppendLine("Freeze Dialog: Tabelle 14 Cycles/Anlagen/geschätzt=" + Ok(okRows) + " Ist C4 -> C7 neu, C5 gleich=" + Ok(okLive) + " zurücksetzen=" + Ok(okReset) + " nur Uhrzeit=" + Ok(okTime)
                + " gleich berechnet/leer=" + Ok(okSame) + " unlesbar=" + Ok(okBad) + " Hinweis=" + Ok(okHint) + " Art wechseln=" + Ok(okArt) + " Abstand=" + Ok(okParam) + " Start=" + Ok(okStart)
                + " Prüfung 8 Ziffern=" + Ok(okCheck) + " nur lesen=" + Ok(okRo) + " Zeit lesen=" + Ok(p1 && p2 && p3 && p4 && p5 && p6));
        }

        // ---------- Plan und Speichern ----------

        void FzuPlan(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Freeze Plan: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            bool remEdit = false, remAdd = false, remDel = false;
            try
            {
                int fr = rowInfo.FindIndex(delegate(RowInfo r) { return r.Freeze; }), h0 = 0;
                if (fr < 0) { sb.AppendLine("Freeze Plan: Freeze-Zeile fehlt"); return; }
                int fid = rowInfo[fr].Id, hid = rowInfo[h0].Id;
                DateTime wk = D(2030, 5, 6), d2 = wk.AddDays(2), d3 = wk.AddDays(3);
                start = wk; Rebuild(true); Expand(fr); Expand(h0);
                // 1) "+ Freeze" -> neuer Vorgang (Vorgaben aus dem Katalog), speichert sofort mit Protokoll
                FreezeChoice seen = null; int calls = 0;
                freezeTest = delegate(FreezeChoice ch)
                {
                    seen = ch; calls++;
                    ch.Item.Start1to3[0] = ch.Item.Start1to3[0].Date.AddHours(22); ch.Item.Cycles = 8; ch.Item.MainRef = "33010010"; ch.Item.SubNo = "33019010"; ch.Item.Tanks = "H2-T-0251";
                    return true;
                };
                view.TestClick(view.FreezeAddPoint(CellOf(fr, 2)));
                Entry e = store.Get(d2, fid);
                List<AuditEntry> au = store.GetAudit(d2, fid);
                bool okNew = seen != null && seen.IsNew && !seen.ReadOnly && seen.Cfg.Cycles == 14 && seen.Item.DauerMin == 360 && seen.Where.Contains("Freeze") && e != null && e.Freezes.Count == 1
                    && e.Freezes[0].Id == "f1" && e.Freezes[0].Cycles == 8 && e.Freezes[0].Start1to3[0] == d2.AddHours(22) && au.Count >= 1 && au[au.Count - 1].After != null && au[au.Count - 1].After.Freezes.Count == 1;
                // 2) Anzeige: Vorgang am Starttag (C1/C2 auf zwei Anlagen), Fortsetzung am Folgetag (C3..C8), Hoehen ohne Ueberlappung
                CellData wc = CellOf(fr, 2), tc = CellOf(fr, 3);
                string headTxt = ""; foreach (List<Item> ln in wc.FrzParts[0].Head.Lines) foreach (Item it in ln) headTxt += it.Text;
                bool okOwn = wc.FrzParts.Count == 1 && !wc.FrzParts[0].Cont && wc.FrzParts[0].Segs.Count == 2 && wc.FrzParts[0].Segs[0].Runs[0].Text == "C1" && wc.FrzParts[0].Segs[1].Y > wc.FrzParts[0].Segs[0].Y
                    && headTxt.Replace(" ", "") == PlanView.FreezeHead(e.Freezes[0]).Replace(" ", "") && headTxt.StartsWith("Bag-Freeze");
                bool okCont = tc.Conts.Count == 6 && tc.FrzParts.Count == 1 && tc.FrzParts[0].Cont && tc.FrzParts[0].Origin == d2 && tc.FrzParts[0].Id == "f1" && tc.FrzParts[0].Segs.Count == 6
                    && tc.FrzParts[0].Segs[0].Runs[0].Font.Italic && tc.FrzParts[0].Head.Lines[0][0].Text == "(aus";
                bool okLayout = FzuLayout(fr);
                // 3) Klick auf die Fortsetzung oeffnet den Vorgang im Block des Ursprungstags (nicht neu)
                string where = null; bool wasNew = true; string wid = "";
                freezeTest = delegate(FreezeChoice ch) { where = ch.Where; wasNew = ch.IsNew; wid = ch.Item.Id; return false; };
                view.TestClick(view.FreezePoint(tc, 0));
                bool okClick = where != null && where.Contains(Ui.DayText(d2)) && !wasNew && wid == "f1" && store.Get(d2, fid).Freezes.Count == 1;
                // 4) tatsaechlicher Start C4 (Do 05:00 -> 08:00): Fortsetzung zeigt "+3 h", C7 folgt
                freezeTest = delegate(FreezeChoice ch) { ch.Item.Ist[4] = d3.AddHours(8); return true; };
                view.TestClick(view.FreezePoint(wc, 0));
                e = store.Get(d2, fid); tc = CellOf(fr, 3);
                bool okIst = e.Freezes[0].Ist.Count == 1 && tc.FrzParts.Count == 1 && FzuNext(tc.FrzParts[0], "C4", "(+3 h)") && FzuNext(tc.FrzParts[0], "C7", "(+3 h)") && FzuHas(tc.FrzParts[0], "C7", "15:00")
                    && !FzuNext(tc.FrzParts[0], "C5", "(+3 h)") && store.GetAudit(d2, fid).Count == au.Count + 1;
                // 5) Konflikt: ein anderer PC aendert den Vorgang waehrend des Dialogs (Notiz); meine Korrektur C5 wird eingearbeitet, die fremde Notiz bleibt
                freezeTest = delegate(FreezeChoice ch)
                {
                    Entry o = store.Get(d2, fid); o.Freezes[0].Note = "von PC 2"; store.Save(d2, fid, o);
                    ch.Item.Ist[5] = d3.AddHours(7); return true;
                };
                view.TestClick(view.FreezePoint(CellOf(fr, 2), 0));
                e = store.Get(d2, fid);
                bool okConf = e.Freezes.Count == 1 && e.Freezes[0].Note == "von PC 2" && e.Freezes[0].Ist.Count == 2 && e.Freezes[0].Ist[5] == d3.AddHours(7) && e.Freezes[0].Ist[4] == d3.AddHours(8);
                // 6) Hauptlauf-Block: abgeleitete Zeile "gesplittet in ... · Tanks ..." unter dem Inhalt, Zeile bleibt gleich hoch
                Entry hm = new Entry(); hm.Content = "Lauf 33010010\nMischen"; store.Save(wk.AddDays(1), hid, hm);
                Rebuild(false);
                CellData hc = CellOf(h0, 1), ho = CellOf(h0, 2);
                string sp = ""; if (hc.SplitTB != null) foreach (List<Item> ln in hc.SplitTB.Lines) foreach (Item it in ln) sp += it.Text;
                bool okSplit = hc.SplitH > 0 && sp.Replace(" ", "") == "gesplittetin33019010·TanksH2-T-0251" && hc.RSplit.Y == hc.RContent.Bottom && hc.RSplit.Height == hc.SplitH && ho.SplitH == 0 && ho.RSplit.Height == 0
                    && hc.R.Height == ho.R.Height && hc.RContent.Height >= rowInfo[h0].MinLines * Font.Height && hc.RSplit.Bottom <= hc.R.Bottom && FzuLayout(h0);
                // 7) Bearbeitungsmodus: Editor nur ueber dem Inhalt, Freeze bleibt beim Speichern erhalten
                CellData ec = CellOf(fr, 2);
                BeginEdit(ec);
                bool okEd = editCell == ec && edContent.Top >= ec.RContent.Top && edContent.Bottom <= ec.RContent.Bottom && ec.RFrz.Bottom <= edContent.Top && ec.RFrz.Height > 0;
                edContent.Text = "Notiz im Block";
                EndEdit(true);
                e = store.Get(d2, fid);
                okEd &= e.Freezes.Count == 1 && e.Freezes[0].Ist.Count == 2 && e.Content == "Notiz im Block" && CellOf(fr, 2).FrzParts.Count == 1;
                // 7b) Ein-/Ausklappen: Zeile nur mit Fortsetzung (Vorgang liegt vor der angezeigten Woche) ist standardmaessig offen
                start = d3; Rebuild(true);   // Woche ab Do: der Vorgang (Mi) liegt davor, nur die Fortsetzung am Do ist sichtbar
                bool okFold = CellOf(fr, 0).Conts.Count == 6 && CellOf(fr, 0).Cur.IsBlank && !view.DefaultFold()[fr];
                start = wk; Rebuild(true); Expand(fr); Expand(h0);
                // 8) Rechte: ohne "Text ändern" kein Ist; ohne "Text hinzufügen"/"ändern" kein neuer Vorgang; ohne "Text löschen" kein Entfernen; nur lesen
                remEdit = me.Rights.Remove(Perm.EditText);
                freezeTest = delegate(FreezeChoice ch) { ch.Item.Ist[6] = d3.AddHours(9); return true; };
                view.TestClick(view.FreezePoint(CellOf(fr, 2), 0));
                bool noIst = !store.Get(d2, fid).Freezes[0].Ist.ContainsKey(6);
                freezeTest = delegate(FreezeChoice ch) { ch.Item.Start1to3[0] = ch.Item.Start1to3[0].Date.AddHours(8); ch.Item.Cycles = 3; return true; };
                view.TestClick(view.FreezeAddPoint(CellOf(fr, 4)));   // leerer Block: mit "Text hinzufügen" erlaubt
                bool addOk = store.Get(wk.AddDays(4), fid) != null && store.Get(wk.AddDays(4), fid).Freezes.Count == 1;
                if (remEdit) me.Rights.Add(Perm.EditText); remEdit = false;
                remDel = me.Rights.Remove(Perm.DeleteText);
                freezeTest = delegate(FreezeChoice ch) { ch.Action = FreezeChoice.Act.Remove; return true; };
                view.TestClick(view.FreezePoint(CellOf(fr, 4), 0));
                bool noDel = store.Get(wk.AddDays(4), fid).Freezes.Count == 1;
                if (remDel) me.Rights.Add(Perm.DeleteText); remDel = false;
                remEdit = me.Rights.Remove(Perm.EditText); remAdd = me.Rights.Remove(Perm.AddText);
                int before = calls; bool ro = false;
                freezeTest = delegate(FreezeChoice ch) { calls++; ro = ch.ReadOnly; ch.Item.Ist[7] = d3.AddHours(10); return true; };
                view.TestClick(view.FreezePoint(CellOf(fr, 2), 0));
                bool noInt = ro && !store.Get(d2, fid).Freezes[0].Ist.ContainsKey(7);
                view.TestClick(view.FreezeAddPoint(CellOf(fr, 5)));
                bool noNew = calls == before + 1 && store.Get(wk.AddDays(5), fid) == null;
                if (remEdit) me.Rights.Add(Perm.EditText); remEdit = false; if (remAdd) me.Rights.Add(Perm.AddText); remAdd = false;
                // 9) Entfernen (mit Recht): der Vorgang verschwindet, der Block ist wieder leer
                freezeTest = delegate(FreezeChoice ch) { ch.Action = FreezeChoice.Act.Remove; return true; };
                view.TestClick(view.FreezePoint(CellOf(fr, 4), 0));
                Entry gone = store.Get(wk.AddDays(4), fid);
                bool okDel = gone == null || gone.Freezes.Count == 0;
                bool okRight = noIst && addOk && noDel && noInt && noNew;
                sb.AppendLine("Freeze Plan: neu über + Freeze, Protokoll=" + Ok(okNew) + " Vorgang C1/C2 je Anlage=" + Ok(okOwn) + " Fortsetzung kursiv (aus Mi)=" + Ok(okCont) + " Höhen ohne Überlappung=" + Ok(okLayout)
                    + " Klick öffnet Ursprung=" + Ok(okClick) + " Ist C4: +3 h in Fortsetzung=" + Ok(okIst) + " Konflikt, fremde Notiz bleibt=" + Ok(okConf) + " gesplittet-Zeile=" + Ok(okSplit)
                    + " Editor/Speichern=" + Ok(okEd) + " Zeile offen mit Fortsetzung=" + Ok(okFold) + " Rechte=" + Ok(okRight) + " Entfernen=" + Ok(okDel));
            }
            finally
            {
                freezeTest = null;
                if (remEdit) me.Rights.Add(Perm.EditText);
                if (remAdd) me.Rights.Add(Perm.AddText);
                if (remDel) me.Rights.Add(Perm.DeleteText);
                if (editCell != null) EndEdit(false);
                start = keep; Rebuild(true);
            }
        }

        // Nach dem Cycle mit Beschriftung label folgt der Eintrag text (Verzoegerung); bzw. der Cycle zeigt die Zeit text
        static bool FzuNext(FrzPart p, string label, string text)
        {
            int i = p.Segs.FindIndex(delegate(FrzSeg s) { return s.Runs[0].Text == label; });
            return i >= 0 && i + 1 < p.Segs.Count && p.Segs[i + 1].Runs[0].Text == text;
        }
        static bool FzuHas(FrzPart p, string label, string text)
        {
            FrzSeg s = p.Segs.Find(delegate(FrzSeg x) { return x.Runs[0].Text == label; });
            return s != null && s.Runs.Exists(delegate(FrzRun r) { return r.Text == text; });
        }

        // Zeile r: alle Bloecke gleich hoch; Bereiche liegen untereinander (Puffer, Freeze, Inhalt, Split); Vorgaenge ueberlappen nicht und liegen im Bereich
        bool FzuLayout(int r)
        {
            int h = -1; bool ok = true;
            foreach (CellData c in view.Cells)
            {
                if (c.RowIndex != r) continue;
                if (h < 0) h = c.R.Height;
                ok &= c.R.Height == h && c.RBuf.Bottom == c.RFrz.Top && c.RFrz.Bottom == c.RContent.Top && c.RContent.Bottom == c.RSplit.Top && c.RSplit.Bottom <= c.R.Bottom && c.RContent.Height > 0;
                for (int i = 0; i < c.FrzParts.Count; i++)
                {
                    FrzPart p = c.FrzParts[i];
                    ok &= p.Y >= 0 && p.Y + p.H <= c.FrzH && (i == 0 || p.Y >= c.FrzParts[i - 1].Y + c.FrzParts[i - 1].H) && p.BodyY + p.SegH <= p.H;
                    foreach (FrzSeg s in p.Segs) ok &= s.Y + Font.Height <= p.SegH && s.X >= 0;
                }
            }
            return ok;
        }

        // ---------- Blockstruktur ----------

        void FzuStruct(StringBuilder sb)
        {
            List<RowInfo> rows = store.GetRows();
            int fz = rows.FindIndex(delegate(RowInfo r) { return r.Freeze; });
            using (StructureForm f = new StructureForm(rows, true, true, true))
            {
                bool was = f.TestFreeze(fz) && !f.TestFreeze(0);
                f.TestSetFreeze(fz, false); f.TestSetFreeze(0, true);
                List<RowInfo> res = f.TestCommit();
                bool ok = res != null && res.Count == rows.Count && !res[fz].Freeze && res[0].Freeze;
                using (StructureForm g = new StructureForm(rows, false, true, true))   // ohne Recht "Namen": Haken gesperrt, Wert bleibt
                {
                    g.TestSetFreeze(0, true);
                    List<RowInfo> r2 = g.TestCommit();
                    ok &= r2 != null && r2[fz].Freeze && !r2[0].Freeze;
                }
                sb.AppendLine("Freeze Struktur: Haken Freeze-Zeile lesen/ändern/speichern=" + Ok(was && ok));
            }
        }

        // ---------- Ladezeit ----------

        // Rueckschau (Fortsetzungen) und SplitIndex ueber 35 Tage mit realistischer Belegung (8 Zeilen, je ~5 Zeilen Text): Zusatzkosten je Wochenwechsel
        void FzuPerf(StringBuilder sb)
        {
            MemoryStore ms = new MemoryStore(); DateTime w = D(2030, 8, 5);
            List<RowInfo> rows = ms.GetRows();
            for (int d = -21; d < 28; d++)
                for (int r = 0; r < 8; r++)
                {
                    Entry e = new Entry(); e.Content = "Lauf 4401" + (1000 + d + 30) + "\nTFF: H1-X-0101\nPumpe: H1-P-0101-05\nPT: H1-T-0102 Notiz\nSplitten"; e.People[0] = "MUMA, BEER";
                    ms.Save(w.AddDays(d), rows[r].Id, e);
                }
            PlanView pv = new PlanView(Font); pv.Rows = rows;
            pv.LoadCells(ms, w);   // anwaermen
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            const int n = 20;
            for (int i = 0; i < n; i++) pv.LoadCells(ms, w.AddDays(7 * (i % 2)));
            double all = sw.ElapsedMilliseconds / (double)n;
            sw.Reset(); sw.Start();
            for (int i = 0; i < n; i++) SplitIndex.Build(ms, w.AddDays(-14), w.AddDays(20));
            double ix = sw.ElapsedMilliseconds / (double)n;
            pv.Dispose();
            sb.AppendLine("Freeze Laden: LoadCells " + all.ToString("0.0") + " ms (davon SplitIndex ~" + ix.ToString("0.0") + " ms, 8 Zeilen x 35 Tage belegt) unter 40 ms=" + Ok(all < 40));
        }
    }
}
#endif
