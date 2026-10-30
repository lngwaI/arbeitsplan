#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests F1 (Freeze-Modul Core): Rechnung, Anlagen, Schaetzung, Hinweise, XML (auch alte Dateien), Merge, Rechte, SplitIndex, Suche, Fortsetzungen, Paket-Uebernahme.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static readonly DateTime FzD0 = new DateTime(2031, 3, 3, 12, 0, 0);   // Montag 12:00

        static FreezeItem FzBag(DateTime c1) { FreezeItem f = new FreezeItem(); f.Start1to3[0] = c1; return f; }

        static Entry FzEntry(FreezeItem f) { Entry e = new Entry(); FreezeSet.Add(e.Freezes, f); return e; }

        void SelfTestFreeze(StringBuilder sb)
        {
            try { FzCalc(sb); } catch (Exception ex) { sb.AppendLine("Freeze Rechnung: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzXml(sb); } catch (Exception ex) { sb.AppendLine("Freeze XML: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzMerge(sb); } catch (Exception ex) { sb.AppendLine("Freeze Merge: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { FzIndex(sb); } catch (Exception ex) { sb.AppendLine("Freeze Index: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        void FzCalc(StringBuilder sb)
        {
            FreezeItem f = FzBag(FzD0);
            List<FreezeCycle> cy = FreezeCalc.Cycles(f);
            bool okBeisp = cy.Count == 14 && cy[3].Actual == FzD0.AddHours(7) && cy[6].Actual == FzD0.AddHours(14) && cy[6].Day == FzD0.Date.AddDays(1) && cy[6].Actual.Hour == 2
                && cy[3].End == FzD0.AddHours(13) && cy[0].DelayMin == 0;
            string an = ""; foreach (FreezeCycle c in cy) an += (an.Length > 0 ? "," : "") + c.Anlage;
            bool okAnlage = an == "1,2,3,1,2,3,1,2,3,1,2,3,1,2" && FreezeCalc.Anlage(14) == 2;
            bool okSchaetz = cy[1].Estimated && cy[2].Estimated && !cy[0].Estimated && !cy[3].Estimated && cy[1].Actual == FzD0.AddHours(1) && cy[2].Actual == FzD0.AddHours(2);
            FreezeItem g = FzBag(FzD0); g.Start1to3[1] = FzD0.AddMinutes(90);
            bool okEigen = !FreezeCalc.Cycles(g)[1].Estimated && FreezeCalc.Cycles(g)[1].Actual == FzD0.AddMinutes(90) && FreezeCalc.Cycles(g)[2].Estimated;
            FreezeItem h = FzBag(FzD0); h.Ist[4] = new DateTime(2031, 3, 8, 2, 0, 0);   // C4 real Samstag 02:00
            List<FreezeCycle> hc = FreezeCalc.Cycles(h);
            bool okIst = hc[6].Actual == new DateTime(2031, 3, 8, 9, 0, 0) && hc[9].Actual == new DateTime(2031, 3, 8, 16, 0, 0) && hc[4].Actual == cy[4].Actual && hc[5].Actual == cy[5].Actual
                && hc[3].HasIst && !hc[6].HasIst && hc[3].DelayMin == hc[6].DelayMin && hc[3].DelayMin > 0 && hc[4].DelayMin == 0 && hc[6].Planned == cy[6].Planned;
            FreezeItem t = new FreezeItem(); t.Art = FreezeArt.Tank; t.Start1to3[0] = FzD0;
            List<FreezeCycle> tc = FreezeCalc.Cycles(t);
            bool okTank = tc.Count == 1 && tc[0].End == FzD0.AddHours(6) && t.DauerMin == 360 && FreezeCalc.Cycles(new FreezeItem()).Count == 0;
            FreezeItem few = FzBag(FzD0); few.Cycles = 40; FreezeItem one = FzBag(FzD0); one.Cycles = 0;
            bool okAnz = FreezeCalc.Cycles(few).Count == 14 && FreezeCalc.Cycles(one).Count == 1;
            FreezeItem hi = FzBag(FzD0); hi.AbstandMin = 300;
            FreezeItem hj = FzBag(FzD0); hj.Ist[4] = FzD0.AddHours(3);
            FreezeItem hk = FzBag(FzD0); hk.Ist[4] = FzD0.AddHours(8);
            bool okHint = FreezeCalc.Hints(f).Count == 0 && FreezeCalc.Hints(hi).Count == 1 && FreezeCalc.Hints(hj).Count == 1 && FreezeCalc.Hints(hk).Count == 0;
            bool okText = FreezeCalc.DelayText(420) == "+7 h" && FreezeCalc.DelayText(-30) == "-30 min" && FreezeCalc.DelayText(90) == "+1 h 30 min" && FreezeCalc.DelayText(0) == "" && FreezeCalc.Dur(360) == "6 h";
            FreezeItem dl = FzBag(FzD0); dl.Ist[4] = FzD0.AddHours(14);
            List<string> lines = FreezeSet.DisplayLines(FzEntry(dl).Freezes, true);
            bool okLines = lines.Count == 2 && lines[1] == "    Z4 Ist 04.03. 02:00 (+7 h)" && lines[0].StartsWith("Freeze 1: Bag");
            sb.AppendLine("Freeze Rechnung: C1 12:00 Abstand 7 h -> C4 19:00, C7 02:00 Folgetag=" + Ok(okBeisp) + " Anlage C1..C14=" + Ok(okAnlage) + " geschätzt C2/C3=" + Ok(okSchaetz) + " eigene Starts=" + Ok(okEigen)
                + " Ist C4 -> C7/C10 folgen, C5/C6 gleich=" + Ok(okIst) + " Tank Start+Dauer=" + Ok(okTank) + " Anzahl 1-14=" + Ok(okAnz) + " Hinweise=" + Ok(okHint) + " Texte=" + Ok(okText) + " Protokollzeile=" + Ok(okLines));
        }

        // Entry mit zwei Vorgaengen (Bag mit Ist, Tank) in "Freeze / Thaw" eines MemoryStore speichern
        static MemoryStore FzStore(out int row, out FreezeItem bag)
        {
            MemoryStore st = new MemoryStore(); row = -1;
            foreach (RowInfo r in st.GetRows()) if (r.Name == "Freeze / Thaw") row = r.Id;
            bag = FzBag(FzD0); bag.Cycles = 9; bag.MainRef = "30010010"; bag.SubNo = "30019010"; bag.Tanks = "H2-T-0251, H2-T-0252"; bag.Note = "Hinweis Ümlaut\n zweite Zeile ";
            bag.Start1to3[1] = FzD0.AddHours(1); bag.Ist[4] = FzD0.AddHours(14);
            Entry e = FzEntry(bag);
            FreezeItem tank = new FreezeItem(); tank.Art = FreezeArt.Tank; tank.Anlage = 2; tank.Kreislauf = 2; tank.Start1to3[0] = FzD0.AddHours(3); tank.DauerMin = 300; tank.Tanks = "H2-T-0253";
            FreezeSet.Add(e.Freezes, tank);
            st.Save(FzD0.Date, row, e);
            return st;
        }

        void FzXml(StringBuilder sb)
        {
            int row; FreezeItem bag; MemoryStore st = FzStore(out row, out bag);
            Catalog cat = st.GetCatalog(); cat.FreezeDefaults.DauerMin = 300; cat.FreezeDefaults.Cycles = 10; st.SaveCatalog(cat);
            byte[] xml = Backup.ToXml(st.Snapshot());
            StoreData back = Backup.FromXml(xml);
            Entry orig = st.Get(FzD0.Date, row), re = null;
            foreach (StoredEntry s in back.Entries) if (s.RowId == row) re = s.Data;
            bool okRund = re != null && re.Freezes.Count == 2 && FreezeSet.Key(re.Freezes) == FreezeSet.Key(orig.Freezes) && re.Freezes[0].Note == bag.Note && re.Freezes[1].Art == FreezeArt.Tank
                && re.Freezes[1].Anlage == 2 && re.Freezes[0].Ist[4] == FzD0.AddHours(14) && re.Freezes[0].Start1to3[2] == DateTime.MinValue;
            bool okRows = false; foreach (RowInfo r in back.Rows) if (r.Id == row) okRows = r.Freeze;
            bool okCat = back.Catalog.FreezeDefaults.DauerMin == 300 && back.Catalog.FreezeDefaults.Cycles == 10 && back.Catalog.FreezeDefaults.AbstandMin == 420 && back.Catalog.FreezeDefaults.GwkAnlagen == 2;
            // alte Datei: ohne <freeze>, ohne Katalog-Element, ohne Zeilenattribut
            string text = Encoding.UTF8.GetString(xml);
            text = Regex.Replace(text, @"<freeze\b[^>]*?(/>|>.*?</freeze>)", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<Freeze\b[^>]*/>", ""); text = Regex.Replace(text, @" freeze=""[01]""", "");
            StoreData old = Backup.FromXml(Encoding.UTF8.GetBytes(text));
            Entry oe = null; foreach (StoredEntry s in old.Entries) if (s.RowId == row) oe = s.Data;
            bool okAlt = oe != null && oe.Freezes.Count == 0 && old.Catalog.FreezeDefaults.DauerMin == 360 && old.Catalog.FreezeDefaults.Cycles == 14;
            bool okMig = false; foreach (RowInfo r in old.Rows) if (r.Id == row) okMig = r.Freeze;   // Name enthaelt "Freeze"
            bool otherRows = true; foreach (RowInfo r in old.Rows) if (r.Name != "Freeze / Thaw" && r.Freeze) otherRows = false;
            Entry cl = orig.Clone(); cl.Freezes[0].Ist[5] = FzD0; cl.Freezes[0].Start1to3[0] = FzD0.AddDays(1);
            bool okClone = !orig.Freezes[0].Ist.ContainsKey(5) && orig.Freezes[0].Start1to3[0] == FzD0 && !orig.IsBlank && !orig.IsEmpty && new Entry().IsBlank;
            MemoryStore ms = MemoryStore.Sample(new DateTime(2031, 3, 3)); int fr = -1;
            foreach (RowInfo r in ms.GetRows()) if (r.Freeze) fr = r.Id;
            Entry sb1 = ms.Get(new DateTime(2031, 3, 4), fr), sb3 = ms.Get(new DateTime(2031, 3, 6), fr);
            bool okSample = fr > 0 && sb1 != null && sb1.Freezes.Count == 1 && sb1.Freezes[0].Art == FreezeArt.Bag && sb3 != null && sb3.Freezes[0].Art == FreezeArt.Tank && FreezeCalc.Cycles(sb1.Freezes[0]).Count == 8;
            sb.AppendLine("Freeze XML: Rundweg=" + Ok(okRund) + " Zeile freeze=" + Ok(okRows) + " Katalog-Standardwerte=" + Ok(okCat) + " alte Datei lesbar=" + Ok(okAlt) + " Migration Name Freeze=" + Ok(okMig && otherRows)
                + " Clone tief, Block belegt=" + Ok(okClone) + " Testdaten=" + Ok(okSample));
        }

        void FzMerge(StringBuilder sb)
        {
            int row; FreezeItem bag; FzStore(out row, out bag);
            Entry b = FzEntry(bag), mine = b.Clone(), theirs = b.Clone();
            mine.Freezes[0].Ist[5] = FzD0.AddHours(21); theirs.Freezes[0].Ist[6] = FzD0.AddHours(22); theirs.Freezes[0].Note = "neue Notiz";
            List<string> cl = new List<string>();
            Entry m = Conflict.Merge(b, mine, theirs, cl);
            FreezeItem mi = m.Freezes[0];
            bool okVerschieden = cl.Count == 0 && m.Freezes.Count == 1 && mi.Ist[5] == FzD0.AddHours(21) && mi.Ist[6] == FzD0.AddHours(22) && mi.Ist[4] == bag.Ist[4] && mi.Note == "neue Notiz";
            Entry mine2 = b.Clone(), theirs2 = b.Clone(); mine2.Freezes[0].Ist[4] = FzD0.AddHours(30); theirs2.Freezes[0].Ist[4] = FzD0.AddHours(40);
            List<string> cl2 = new List<string>(); Entry m2 = Conflict.Merge(b, mine2, theirs2, cl2);
            List<string> cl3 = new List<string>(); Entry m3 = Conflict.Merge(b, mine2, theirs2, cl3, true);
            bool okGleich = cl2.Contains("Freeze") && m2.Freezes.Count == 1 && m2.Freezes[0].Ist[4] == FzD0.AddHours(40) && cl3.Contains("Freeze") && m3.Freezes[0].Ist[4] == FzD0.AddHours(30);
            Entry mine4 = b.Clone(), theirs4 = b.Clone(); mine4.Freezes[0].Note = "meine Notiz"; theirs4.Freezes[0].Note = "andere Notiz";
            List<string> cl4 = new List<string>(); Entry m4 = Conflict.Merge(b, mine4, theirs4, cl4);
            bool okParam = cl4.Contains("Freeze") && m4.Freezes.Count == 1 && m4.Freezes[0].Note == "andere Notiz";
            Entry mine5 = b.Clone(); FreezeItem nf = FzBag(FzD0.AddDays(1)); FreezeSet.Add(mine5.Freezes, nf);
            List<string> cl5 = new List<string>(); Entry m5 = Conflict.Merge(b, mine5, b.Clone(), cl5);
            Entry both = b.Clone(); both.Freezes.Add(FzBag(FzD0.AddDays(2))); both.Freezes[both.Freezes.Count - 1].Id = nf.Id;   // gleichzeitig mit gleicher Id angelegt
            List<string> cl6 = new List<string>(); Entry m6 = Conflict.Merge(b, mine5, both, cl6);
            bool okNeu = cl5.Count == 0 && m5.Freezes.Count == 2 && m6.Freezes.Count == 3 && m6.Freezes[1].Id != m6.Freezes[2].Id;
            Entry gone = b.Clone(); gone.Freezes.Clear();
            Entry m7 = Conflict.Merge(b, gone, b.Clone(), new List<string>());
            Entry chg = b.Clone(); chg.Freezes[0].Cycles = 5;
            Entry m8 = Conflict.Merge(b, gone, chg, new List<string>());
            bool okLoesch = m7.Freezes.Count == 0 && m8.Freezes.Count == 1 && m8.Freezes[0].Cycles == 5;
            bool a1, r1, c1, a2, r2, c2, a3, r3, c3, a4, r4, c4;
            Entry plus = b.Clone(); FreezeSet.Add(plus.Freezes, FzBag(FzD0)); FreezeSet.Diff(b.Freezes, plus.Freezes, out a1, out r1, out c1);
            Entry ist = b.Clone(); ist.Freezes[0].Ist[8] = FzD0.AddHours(50); FreezeSet.Diff(b.Freezes, ist.Freezes, out a2, out r2, out c2);
            Entry minus = b.Clone(); minus.Freezes.Clear(); FreezeSet.Diff(b.Freezes, minus.Freezes, out a3, out r3, out c3);
            FreezeSet.Diff(b.Freezes, b.Clone().Freezes, out a4, out r4, out c4);
            bool okRechte = a1 && !r1 && !c1 && !a2 && !r2 && c2 && !a3 && r3 && !c3 && !a4 && !r4 && !c4;
            Entry cur = new Entry(); FreezeSet.Add(cur.Freezes, FzBag(FzD0));
            Entry pk = new Entry(); pk.Content = "Paketzeile";
            bool okApply = Package.Merge(cur, pk, ApplyMode.FillEmpty) == null && Package.Merge(cur, pk, ApplyMode.Overwrite).Freezes.Count == 1
                && Package.Merge(cur, pk, ApplyMode.Append).Freezes.Count == 1 && Package.Merge(new Entry(), pk, ApplyMode.FillEmpty).Content == "Paketzeile";
            sb.AppendLine("Freeze Merge: verschiedene Cycles still=" + Ok(okVerschieden) + " gleicher Cycle Konflikt (gespeichert/meine)=" + Ok(okGleich) + " Parameter beidseitig=" + Ok(okParam)
                + " neue Vorgänge=" + Ok(okNeu) + " Löschen=" + Ok(okLoesch) + " Rechte (neu/ändern/entfernen)=" + Ok(okRechte) + " Paket belegt=" + Ok(okApply));
        }

        void FzIndex(StringBuilder sb)
        {
            MemoryStore st = new MemoryStore(); int row = st.GetRows()[0].Id, row2 = st.GetRows()[1].Id;
            FreezeItem f = FzBag(FzD0); f.MainRef = "40020010"; f.SubNo = "40029010"; f.Tanks = "H2-T-0251, H2-T-0252";
            st.Save(FzD0.Date, st.GetRows()[5].Id, FzEntry(f));
            Entry txt = new Entry(); txt.Content = "Splitten\nUnterlauf 40029011 (aus 40020010)\nUnterlauf 40029010 (aus 40020010)\nUnterlauf (aus 40020010)";
            st.Save(FzD0.Date.AddDays(1), row2, txt);
            Entry main = new Entry(); main.Content = "Lauf 40020010\nMischen";
            st.Save(FzD0.Date, row, main);
            SplitIndex ix = SplitIndex.Build(st, FzD0.Date.AddDays(-14), FzD0.Date.AddDays(14));
            SplitInfo si = ix.Get("40020010");
            bool okItem = si != null && si.Subs.Count == 2 && si.Subs[0] == "40029010" && si.Subs[1] == "40029011" && si.Tanks.Count == 2;
            List<string> ln = ix.LinesFor(main);
            bool okLine = ln.Count == 1 && ln[0] == "gesplittet in 40029010, 40029011 · Tanks H2-T-0251, H2-T-0252" && ix.LinesFor(new Entry()).Count == 0;
            SplitIndex only = new SplitIndex(); Entry t2 = new Entry(); t2.Content = "Unterlauf 40029012 (aus 40020020)"; only.Add(t2);
            Entry m2 = new Entry(); m2.Content = "Lauf 40020020";
            bool okText = only.LinesFor(m2).Count == 1 && only.LinesFor(m2)[0] == "gesplittet in 40029012" && only.Get("40020020").Tanks.Count == 0;
            FreezeItem g = FzBag(FzD0); g.MainRef = "40020030"; g.Tanks = "H2-T-0260";
            SplitIndex tk = new SplitIndex(); tk.Add(FzEntry(g));
            bool okTanks = tk.Get("40020030") != null && tk.Get("40020030").Text == "gesplittet · Tanks H2-T-0260" && new SplitIndex().Get("40020030") == null;
            Entry noMain = new Entry(); FreezeItem x = FzBag(FzD0); x.SubNo = "40029099"; FreezeSet.Add(noMain.Freezes, x); SplitIndex n0 = new SplitIndex(); n0.Add(noMain);
            bool okOhneHaupt = n0.Count == 0;
            // Suche, Fortsetzung, Equipment-Liste
            List<SearchHit> h1 = st.Find("40029010"), h2 = st.Find("H2-T-0252"), h3 = st.Find("gibt-es-nicht");
            bool okFind = h1.Count == 2 && h2.Count == 1 && h2[0].RowId == st.GetRows()[5].Id && h3.Count == 0;
            List<FreezeCycle> on0 = FreezeSet.CyclesOn(st.Get(FzD0.Date, st.GetRows()[5].Id), FzD0.Date), on1 = FreezeSet.CyclesOn(st.Get(FzD0.Date, st.GetRows()[5].Id), FzD0.Date.AddDays(1));
            List<FreezeCycle> cont = FreezeSet.Continuations(st, FzD0.Date.AddDays(1), st.GetRows()[5].Id);
            bool okTag = on0.Count == 6 && on1.Count == 8 && on0[0].N == 1 && on1[0].N == 7 && cont.Count == 8 && cont[0].OriginDay == FzD0.Date && FreezeSet.Continuations(st, FzD0.Date, st.GetRows()[5].Id).Count == 0
                && FreezeSet.CyclesOn(null, FzD0).Count == 0;
            List<string> ids = EquipExport.Collect(st, FzD0.Date, FzD0.Date);
            bool okEquip = ids.Contains("H2-T-0251") && ids.Contains("H2-T-0252") && ids.Count == 2;
            sb.AppendLine("Freeze Index: SplitIndex aus Vorgang und Textzeile=" + Ok(okItem) + " Zeile am Hauptlauf=" + Ok(okLine) + " nur Text=" + Ok(okText) + " nur Tanks=" + Ok(okTanks) + " ohne Hauptlauf=" + Ok(okOhneHaupt)
                + " Suche=" + Ok(okFind) + " Cycles je Tag/Fortsetzung=" + Ok(okTag) + " Tanks in Equipment-Liste=" + Ok(okEquip));
        }
    }
}
#endif
