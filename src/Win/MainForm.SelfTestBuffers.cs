#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Puffer-Liste (P1, nur Core): XML-Rundweg, Migration alter Daten, Typen, Merge, Rechte-Diff, Suche, Protokolltext, Paket anhaengen/ueberschreiben.
    partial class MainForm
    {
        static Entry Bufs(string content, params string[] nameTypText)   // je Puffer drei Werte: Name, Typ, Text
        {
            Entry e = new Entry(); e.Content = content;
            for (int i = 0; i + 2 < nameTypText.Length; i += 3) BufferSet.Add(e.Buffers, nameTypText[i], nameTypText[i + 1], nameTypText[i + 2]);
            return e;
        }

        static string Names(Entry e) { List<string> l = new List<string>(); foreach (BufferItem b in e.Buffers) l.Add(b.Name + ":" + b.Typ + ":" + b.Text.Replace("\n", "/")); return string.Join(" | ", l.ToArray()); }

        static bool UniqueIds(Entry e) { List<string> ids = new List<string>(); foreach (BufferItem b in e.Buffers) { if (b.Id.Length == 0 || ids.Contains(b.Id)) return false; ids.Add(b.Id); } return true; }

        static void SelfTestBuffers(StringBuilder sb)
        {
            // XML-Rundweg: Namen mit Umlauten/Sonderzeichen, mehrzeiliger Text, kein Attribut "variante" mehr
            MemoryStore s = new MemoryStore();
            int pid = PufferSetup.Row(s.GetRows()).Id;
            DateTime w = D(2026, 6, 1);
            Entry src = Bufs("Herstellung", "Puffer Ä <1> & \"x\"", "mobil", "Ansatz: Tank 500 L\nWFI: H1-X-0101\nNotiz: ü ö ß", "", "stationaer", "", "Puffer C", "mobil", "eins");
            s.Save(w, pid, src);
            byte[] xml = Backup.ToXml(s.Snapshot());
            StoreData back = Backup.FromXml(xml);
            Entry rt = EntryIn(back, w, pid);
            bool okXml = rt != null && BufferSet.Key(rt.Buffers) == BufferSet.Key(s.Get(w, pid).Buffers) && rt.Buffers.Count == 3 && rt.Buffers[0].Text.Contains("\n")
                && rt.Types == "mobil:2|stationaer" && Encoding.UTF8.GetString(xml).IndexOf("variante=", StringComparison.Ordinal) < 0 && UniqueIds(rt);

            // Migration: alte Datei mit Attribut variante (ohne <puffer>) -> je Anzahl ein namenloser Puffer, Inhalt unveraendert
            string old = "<Arbeitsplan version=\"3\"><Zeilen><Zeile id=\"1\" name=\"Puffer\" l0=\"F\" l1=\"S\" l2=\"N\" min=\"3\" lkz=\"0\"/></Zeilen>"
                + "<Bloecke><Block tag=\"2026-10-19\" zeile=\"1\" ver=\"2\" lkz=\"0\" inhalt=\"Puffer A und B\" variante=\"mobil:2|stationaer\" p0=\"\" p1=\"\" p2=\"\"/>"
                + "<Block tag=\"2026-10-20\" zeile=\"1\" ver=\"1\" lkz=\"0\" inhalt=\"neu\" variante=\"mobil\" p0=\"\" p1=\"\" p2=\"\"><puffer id=\"p1\" typ=\"stationaer\" name=\"X\">t</puffer></Block></Bloecke><Katalog/>"
                + "<Protokoll/></Arbeitsplan>";
            StoreData od = Backup.FromXml(Encoding.UTF8.GetBytes(old));
            Entry m0 = od.Entries[0].Data, m1 = od.Entries[1].Data;
            bool okMig = m0.Content == "Puffer A und B" && m0.Buffers.Count == 3 && m0.Types == "mobil:2|stationaer" && m0.Buffers[0].Name == "" && UniqueIds(m0)
                && m1.Buffers.Count == 1 && m1.Buffers[0].Name == "X" && m1.Types == "stationaer";   // <puffer> hat Vorrang vor dem alten Attribut
            bool okTypes = Bufs("x", "", "mobil", "", "", "mobil", "", "", "stationaer", "").Types == "mobil:2|stationaer" && new Entry().Types == "" && new Entry().IsBlank
                && !Bufs("", "", "mobil", "").IsBlank && Bufs("", "", "mobil", "").Clone().Buffers.Count == 1;

            // Tagesprobe / Wochen-WFI wie vorher (Typen aus der Puffer-Liste)
            DutyConfig cfg = s.GetDuties();
            CheckDef probe = cfg.Defs.Find(delegate(CheckDef x) { return x.RowId == pid && x.Key == "PROBE"; });
            DateTime tu = w.AddDays(1);   // Dienstag
            s.Save(tu, pid, Bufs("Herstellung", "A", "mobil", ""));
            bool autoMobil = probe != null && CheckEval.Day(s, probe, tu, tu, cfg) == DayState.Auto;
            DateTime we = w.AddDays(2), fr = w.AddDays(4);   // Mi kombiniert, Fr stationaer
            s.Save(we, pid, Bufs("Herstellung", "A", "mobil", "", "B", "stationaer", ""));
            bool kombi = CheckEval.Day(s, probe, we, we, cfg) == DayState.Missing;
            s.Save(fr, pid, Bufs("Herstellung", "A", "stationaer", ""));
            bool stat = CheckEval.Day(s, probe, fr, fr, cfg) == DayState.Missing;   // stationär: Tagesprobe bleibt Pflicht
            bool okRule = autoMobil && kombi && stat;

            // Anzahl-Hilfen fuer die Chips: Namen/Texte bleiben, ueberzaehlige hinten weg, fehlende namenlos am Ende
            Entry ch = Bufs("", "A", "mobil", "ta", "B", "mobil", "tb", "C", "stationaer", "tc");
            BufferSet.SetCount(ch.Buffers, "mobil", 1);
            bool okChip1 = Names(ch) == "A:mobil:ta | C:stationaer:tc";
            BufferSet.SetCount(ch.Buffers, "mobil", 3);
            bool okChip2 = ch.Buffers.Count == 4 && ch.Buffers[2].Typ == "mobil" && ch.Buffers[2].Name == "" && UniqueIds(ch) && ch.Types == "mobil:3|stationaer";
            BufferSet.SetTypes(ch.Buffers, "stationaer");
            bool okChip3 = Names(ch) == "C:stationaer:tc";

            // Suche (Name und Text) und Protokolltext
            MemoryStore fs = new MemoryStore();
            fs.Save(w, pid, Bufs("", "Lösung Zugabe", "mobil", "Sonderanteil 7"));
            bool okFind = fs.Find("zugabe").Count == 1 && fs.Find("sonderanteil").Count == 1 && fs.Find("gibtsnicht").Count == 0;
            string at = Audit.Text(Bufs("Inhalt", "Puffer A", "mobil", "Tank 1\nTank 2", "", "stationaer", ""), new[] { "F", "S", "N" });
            bool okAudit = at.Contains("Puffer 1: Puffer A (mobil)") && at.Contains("    Tank 2") && at.Contains("Puffer 2: – (stationaer)");

            // Merge dreiwege je Id
            Entry b = Bufs("", "A", "mobil", "t1", "B", "stationaer", "t2");
            List<string> cl = new List<string>();
            Entry mine = b.Clone(); mine.Buffers[0].Name = "A neu";                       // ich aendere p1
            Entry theirs = b.Clone(); theirs.Buffers[1].Text = "t2 neu";                  // die anderen aendern p2
            Entry r = Conflict.Merge(b, mine, theirs, cl);
            bool okSide = cl.Count == 0 && Names(r) == "A neu:mobil:t1 | B:stationaer:t2 neu";
            mine = b.Clone(); mine.Buffers.RemoveAt(1);                                   // ich loesche p2
            r = Conflict.Merge(b, mine, b.Clone(), cl);
            Entry r2 = Conflict.Merge(b, b.Clone(), mine, cl);                            // die anderen loeschen p2
            bool okDel = cl.Count == 0 && Names(r) == "A:mobil:t1" && Names(r2) == "A:mobil:t1";
            mine = b.Clone(); BufferSet.Add(mine.Buffers, "Mein neuer", "mobil", "m");    // beide legen p3 gleichzeitig an
            theirs = b.Clone(); BufferSet.Add(theirs.Buffers, "Neuer der anderen", "stationaer", "t");
            r = Conflict.Merge(b, mine, theirs, cl);
            bool okNew = cl.Count == 0 && r.Buffers.Count == 4 && UniqueIds(r) && Names(r).Contains("Neuer der anderen") && Names(r).Contains("Mein neuer");
            mine = b.Clone(); mine.Buffers[0].Name = "von mir"; theirs = b.Clone(); theirs.Buffers[0].Name = "von dort";   // gleiche Stelle
            List<string> cl3 = new List<string>(), cl4 = new List<string>();
            r = Conflict.Merge(b, mine, theirs, cl3);
            Entry rm = Conflict.Merge(b, mine, theirs, cl4, true);
            bool okClash = cl3.Contains("Puffer") && r.Buffers.Count == 3 && r.Buffers[0].Name == "von dort" && Names(r).Contains("von mir") && UniqueIds(r)
                && cl4.Contains("Puffer") && rm.Buffers.Count == 2 && rm.Buffers[0].Name == "von mir";
            mine = b.Clone(); mine.Buffers[0].Name = "x"; theirs = b.Clone(); theirs.Buffers.RemoveAt(0);   // einer loescht, einer aendert: bleibt
            r = Conflict.Merge(b, mine, theirs, new List<string>());
            bool okDelEdit = r.Buffers.Count == 2 && r.Buffers[0].Name == "x";
            mine = b.Clone(); mine.Buffers[0].Name = "x"; mine.Buffers[0].Text = "y"; theirs = b.Clone(); theirs.Buffers[0].Name = "x"; theirs.Buffers[0].Text = "y";   // gleiche Aenderung
            bool okSame = Conflict.Merge(b, mine, theirs, cl = new List<string>()).Buffers.Count == 2 && cl.Count == 0;

            // Rechte-Diff: hinzufuegen / entfernen / aendern
            bool a1, r1, c1, a2, r2b, c2, a3, r3, c3;
            Entry plus = b.Clone(); BufferSet.Add(plus.Buffers, "", "mobil", ""); BufferSet.Diff(b.Buffers, plus.Buffers, out a1, out r1, out c1);
            Entry minus = b.Clone(); minus.Buffers.RemoveAt(0); BufferSet.Diff(b.Buffers, minus.Buffers, out a2, out r2b, out c2);
            Entry edit = b.Clone(); edit.Buffers[1].Typ = "mobil"; BufferSet.Diff(b.Buffers, edit.Buffers, out a3, out r3, out c3);
            bool okDiff = a1 && !r1 && !c1 && !a2 && r2b && !c2 && !a3 && !r3 && c3;

            sb.AppendLine("Puffer-Liste: XML-Rundweg=" + Ok(okXml) + " Migration=" + Ok(okMig) + " Typen=" + Ok(okTypes) + " Tagesprobe=" + Ok(okRule)
                + " Anzahl-Hilfen=" + Ok(okChip1 && okChip2 && okChip3) + " Suche=" + Ok(okFind) + " Protokolltext=" + Ok(okAudit)
                + " Merge einseitig=" + Ok(okSide) + " gelöscht=" + Ok(okDel) + " gleichzeitig neu=" + Ok(okNew) + " gleiche Stelle=" + Ok(okClash)
                + " gelöscht+geändert=" + Ok(okDelEdit) + " gleiche Änderung=" + Ok(okSame) + " Rechte-Diff=" + Ok(okDiff));

            // Paket: Puffer-Schritt -> Puffer (Name = Titel, Text = Ansatz/Notiz), mehrere am Tag angehaengt, Modi, Datei
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2031, 3, 3);
            Package p = new Package(); p.Name = "PufferTest";
            PackageStep a = new PackageStep(); a.Title = "Puffer A"; a.Row = "Puffer"; a.Variant = "mobil"; a.Note = "Charge 1"; a.Day = 1;
            PackageStep c = new PackageStep(); c.Title = "Puffer B"; c.Row = "Puffer"; c.Variant = "mobil|stationaer"; c.Day = 1;
            p.Steps.Add(a); p.Steps.Add(c);
            p.Apply(ms, d0, ApplyMode.Overwrite, null);
            Entry g = ms.Get(d0, puf);
            bool okPk = g != null && g.Content == "" && g.Buffers.Count == 3 && g.Buffers[0].Name == "Puffer A" && g.Buffers[0].Text == "Charge 1" && g.Buffers[1].Name == "Puffer B" && g.Buffers[2].Name == "Puffer B"
                && g.Types == "mobil:2|stationaer" && UniqueIds(g);   // "beides" = zwei Puffer gleichen Namens; nicht summiert, sondern angehaengt
            Entry pre = Bufs("alt", "Alt", "mobil", "x");
            ms.Save(d0.AddDays(1), puf, pre);
            Package q = new Package(); PackageStep qs = new PackageStep(); qs.Title = "Neu"; qs.Row = "Puffer"; qs.Variant = "stationaer"; qs.Day = 2; q.Steps.Add(qs);
            q.Apply(ms, d0, ApplyMode.Append, null);
            bool okApp = Names(ms.Get(d0.AddDays(1), puf)) == "Alt:mobil:x | Neu:stationaer:" && ms.Get(d0.AddDays(1), puf).Content == "alt" && UniqueIds(ms.Get(d0.AddDays(1), puf));
            q.Apply(ms, d0, ApplyMode.FillEmpty, null);
            bool okFill = Names(ms.Get(d0.AddDays(1), puf)) == "Alt:mobil:x | Neu:stationaer:";   // Block mit Puffern gilt als belegt
            ms.Save(d0.AddDays(2), puf, Bufs("", "Nur Puffer", "mobil", ""));
            qs.Day = 3; q.Apply(ms, d0, ApplyMode.FillEmpty, null);
            bool okBusy = Names(ms.Get(d0.AddDays(2), puf)) == "Nur Puffer:mobil:";
            qs.Day = 2; q.Apply(ms, d0, ApplyMode.Overwrite, null);
            bool okOver = Names(ms.Get(d0.AddDays(1), puf)) == "Neu:stationaer:";
            Package none = new Package(); PackageStep ns = new PackageStep(); ns.Title = "Reinigung"; ns.Row = "Puffer"; ns.Day = 2; none.Steps.Add(ns);
            none.Apply(ms, d0, ApplyMode.Overwrite, null);
            bool okKeepBuf = Names(ms.Get(d0.AddDays(1), puf)) == "Neu:stationaer:" && ms.Get(d0.AddDays(1), puf).Content == "Reinigung";   // Paket ohne Puffer: vorhandene bleiben
            string path = Path.Combine(Path.GetTempPath(), "selftest_puffer.apkg");
            Package wk = Package.FromWeek(ms, d0, "Woche"); wk.Save(path);
            Package ld = Package.Load(path);
            bool okFile = ld.Blocks.Count > 0 && ld.Blocks.Exists(delegate(PackageBlock bl) { return bl.Data.Buffers.Count == 3 && bl.Data.Buffers[0].Name == "Puffer A" && bl.Data.Types == "mobil:2|stationaer"; })
                && File.ReadAllText(path).IndexOf("variante=\"mobil", StringComparison.Ordinal) < 0 && File.ReadAllText(path).Contains("<puffer");
            File.Delete(path);
            sb.AppendLine("Puffer-Liste Paket: Schritt wird Puffer=" + Ok(okPk) + " anhängen=" + Ok(okApp) + " nur leere füllen=" + Ok(okFill && okBusy) + " überschreiben=" + Ok(okOver)
                + " ohne Puffer im Paket bleibt=" + Ok(okKeepBuf) + " Blöcke in Datei=" + Ok(okFile));
        }

        // Puffer-Dialog in der Wochenansicht (Dialog per Testhaken): anlegen mit Vorgabe/Vorschlaegen, bearbeiten, Hoch/Runter, Entfernen,
        // Abbrechen, fehlendes Recht, Bearbeitungsmodus (wird mit dem Block gespeichert), Nummern und Suchtreffer in der Ansicht.
        void SelfTestBufferUi(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Puffer-Dialog: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            try
            {
                int pr = RowOf("Puffer");
                if (pr < 0) { sb.AppendLine("Puffer-Dialog: Zeile Puffer fehlt"); return; }
                int pid = rowInfo[pr].Id;
                DateTime wk = D(2024, 9, 16), day = wk.AddDays(1);   // Montag ohne Testdaten anderer Selbsttests
                start = wk; Rebuild(true); Expand(pr);
                string bn = "", bt = "mobil", bx = ""; BufferChoice.Act ba = BufferChoice.Act.Ok; bool ret = true;
                List<string> seenTyp = new List<string>(); List<bool> ups = new List<bool>(); List<bool> downs = new List<bool>(); List<string> sugg = null;
                bufferTest = delegate(BufferChoice ch)
                {
                    seenTyp.Add(ch.Typ); ups.Add(ch.CanUp); downs.Add(ch.CanDown); sugg = ch.Names;
                    if (!ret) return false;
                    ch.Name = bn; ch.Typ = bt; ch.Text = bx; ch.Action = ba; return true;
                };
                // anlegen: Vorgabe-Typ = zuletzt gewaehlt, Vorschlaege aus bisherigen Namen
                lastBufferTyp = "";
                bn = "Lösung A"; bt = "mobil"; bx = "Tank 1\nWFI"; view.TestClick(view.SlotPoint(CellOf(pr, 1), -1));
                bn = "Lösung B"; bt = "stationaer"; bx = ""; view.TestClick(view.SlotPoint(CellOf(pr, 1), -1));
                bn = "Lösung C"; bt = "mobil"; view.TestClick(view.SlotPoint(CellOf(pr, 1), -1));
                Entry e = store.Get(day, pid);
                bool okAdd = e != null && Names(e) == "Lösung A:mobil:Tank 1/WFI | Lösung B:stationaer: | Lösung C:mobil:" && e.Types == "mobil:2|stationaer" && UniqueIds(e)
                    && seenTyp[0] == "mobil" && seenTyp[1] == "mobil" && seenTyp[2] == "stationaer" && sugg != null && sugg.Contains("Lösung A") && sugg.Contains("Lösung B");
                // Anzeige: Nummer = Position, Hoehe waechst, Suchtreffer im Namen zaehlen
                CellData pc = CellOf(pr, 1);
                bool okShow = pc.BufParts.Count == 3 && pc.RBuf.Height > 3 * Font.Height && pc.RContent.Top == pc.RBuf.Bottom && pc.RBuf.Top == pc.RChecks.Bottom && CellOf(pr, 2).RBuf.Height == 0
                    && pc.BufParts[1].Y > pc.BufParts[0].Y && pc.BufParts[0].Head.Lines[0][0].Text == "Lösung";
                // bearbeiten
                bn = "Lösung B2"; bt = "mobil"; bx = "neu"; view.TestClick(view.BufPoint(CellOf(pr, 1), 1));
                e = store.Get(day, pid);
                bool okEdit = Names(e) == "Lösung A:mobil:Tank 1/WFI | Lösung B2:mobil:neu | Lösung C:mobil:" && e.Types == "mobil:3";
                // Hoch (letzter: Runter nicht moeglich) und Runter
                bn = "Lösung C"; bt = "mobil"; bx = ""; ba = BufferChoice.Act.Up; view.TestClick(view.BufPoint(CellOf(pr, 1), 2));
                bool upOk = ups[ups.Count - 1] && !downs[downs.Count - 1];
                string afterUp = Names(store.Get(day, pid));
                bn = "Lösung A"; bx = "Tank 1\nWFI"; ba = BufferChoice.Act.Down; view.TestClick(view.BufPoint(CellOf(pr, 1), 0));
                string afterDown = Names(store.Get(day, pid));
                bool okMove = upOk && afterUp == "Lösung A:mobil:Tank 1/WFI | Lösung C:mobil: | Lösung B2:mobil:neu" && afterDown == "Lösung C:mobil: | Lösung A:mobil:Tank 1/WFI | Lösung B2:mobil:neu";
                // Abbrechen aendert nichts; fehlendes Recht (Entfernen) auch nicht
                ret = false; ba = BufferChoice.Act.Remove; view.TestClick(view.BufPoint(CellOf(pr, 1), 0)); ret = true;
                bool okCancel = Names(store.Get(day, pid)) == afterDown;
                bool had = me.Rights.Remove(Perm.DeleteText);
                try { view.TestClick(view.BufPoint(CellOf(pr, 1), 0)); }
                finally { if (had) me.Rights.Add(Perm.DeleteText); }
                bool okRight = Names(store.Get(day, pid)) == afterDown;
                // Entfernen
                view.TestClick(view.BufPoint(CellOf(pr, 1), 0));
                e = store.Get(day, pid);
                bool okRem = Names(e) == "Lösung A:mobil:Tank 1/WFI | Lösung B2:mobil:neu" && CellOf(pr, 1).Buffers.Count == 2 && e.Types == "mobil:2";
                // Bearbeitungsmodus: nur im Block, gespeichert erst mit dem Block; Hinweis erhoeht den Bereich
                CellData ec = CellOf(pr, 1); int h0 = ec.RBuf.Height;
                BeginEdit(ec);
                bn = "Nur im Editor"; bt = "stationaer"; bx = ""; ba = BufferChoice.Act.Ok; view.TestClick(view.SlotPoint(CellOf(pr, 1), -1));
                bool notYet = Names(store.Get(day, pid)) == Names(e) && HasUnsavedChanges && CellOf(pr, 1).Buffers.Count == 3 && CellOf(pr, 1).RBuf.Height > h0;
                bool posOk = edContent.Top >= CellOf(pr, 1).RBuf.Bottom;   // Editor liegt nur ueber dem freien Inhalt
                EndEdit(true);
                bool okEditMode = notYet && posOk && store.Get(day, pid).Buffers.Count == 3 && store.Get(day, pid).Types == "mobil:2|stationaer";
                view.Query = "Editor"; MarkAllDirtyForTest();
                bool okSearch = CellOf(pr, 1).Hits == 1;
                view.Query = "";
                sb.AppendLine("Puffer-Dialog: anlegen/Vorgabe/Vorschläge=" + Ok(okAdd) + " Anzeige/Bereich=" + Ok(okShow) + " bearbeiten=" + Ok(okEdit) + " hoch/runter=" + Ok(okMove)
                    + " abbrechen=" + Ok(okCancel) + " ohne Recht=" + Ok(okRight) + " entfernen=" + Ok(okRem) + " Bearbeitungsmodus=" + Ok(okEditMode) + " Suchtreffer=" + Ok(okSearch));
            }
            catch (Exception ex) { sb.AppendLine("Puffer-Dialog: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally { bufferTest = null; if (editCell != null) EndEdit(false); start = keep; Rebuild(true); }
        }

        void MarkAllDirtyForTest() { view.MarkAllDirty(); view.Relayout(); }
    }
}
#endif
