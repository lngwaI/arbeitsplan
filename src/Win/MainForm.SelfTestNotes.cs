#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Notizzeilen (Core/Notes.cs): leer-Regel, mehrere Notizen, XML-Rundweg (Sicherung, Paket, Plan-Datei), Konflikt,
    // Paket-Modi, Laufnummern, Protokolltext; Ansicht/Editor (Rechtsklick, ExtractSpans, Enter, Standardformat).
    partial class MainForm
    {
        static Entry NoteEntry(string content, params int[] noteLines)
        {
            Entry e = new Entry(); e.Content = content;
            foreach (int l in noteLines) Notes.SetNote(e, l, l, true);
            return e;
        }

        static int SuiteId(MemoryStore s) { return s.GetRows().Find(delegate(RowInfo r) { return r.Name == "Suite 1"; }).Id; }

        static string NJoin(List<string> l) { return string.Join("|", l.ToArray()); }

        static void SelfTestNotes(StringBuilder sb)
        {
            // leer-Regel: Block nur mit Notizen = leer, Notiz allein zaehlt nicht als Inhalt
            Entry only = NoteEntry("Hinweis eins\nHinweis zwei", 0, 1);
            Entry mixed = NoteEntry("Lauf 12340001\nHinweis", 1);
            bool okEmpty = only.HasNotes && !only.HasText && only.IsEmpty && only.IsBlank && Notes.NoteLines(only).Count == 2 && Notes.ContentText(only) == ""
                && mixed.HasText && !mixed.IsEmpty && !mixed.IsBlank && Notes.ContentText(mixed) == "Lauf 12340001" && Notes.NoteTexts(mixed)[0] == "Hinweis"
                && only.Spans.TrueForAll(delegate(FormatSpan f) { return f.Note && f.Italic; }) && !new Entry().HasNotes;
            Entry pe = only.Clone(); pe.People[0] = "ABCD";
            okEmpty &= !pe.IsEmpty && !pe.IsBlank;   // Kuerzel machen den Block belegt

            // Normalize: Teilmarkierung wird ganze Zeile, kursiv; leere Zeilen und Leerzeichen sind keine Notiz; idempotent
            Entry part = new Entry(); part.Content = "Alpha beta gamma\n \nZeile drei";
            FormatSpan ps = new FormatSpan(); ps.Start = 6; ps.Length = 4; ps.Note = true; part.Spans.Add(ps);
            FormatSpan ws = new FormatSpan(); ws.Start = 16; ws.Length = 5; ws.Note = true; ws.Bold = true; part.Spans.Add(ws);   // reicht ueber Leerzeile in Zeile drei
            Notes.Normalize(part);
            string k1 = part.SpanKey(); Notes.Normalize(part);
            List<int[]> pl = Notes.NoteLines(part);
            bool okNorm = pl.Count == 2 && pl[0][0] == 0 && pl[0][2] == 16 && pl[1][0] == 2 && part.SpanKey() == k1 && part.Spans.Exists(delegate(FormatSpan f) { return f.Start == 0 && f.Length == 16 && f.Note && f.Italic; });
            Entry none = new Entry(); none.Content = "a\nb"; Notes.SetNote(none, 0, 0, true); Notes.SetNote(none, 0, 0, false);
            okNorm &= none.Spans.Count == 0 && !none.HasNotes;

            // mehrere Notizen aus dem Paket (Enter im Notizfeld = naechste Notiz), ohne Praefix "Notiz:", Kennung bleibt letzte Zeile
            PackageStep st = new PackageStep(); st.Title = "Packen"; st.Text = "Zeile a\nZeile b"; st.Note = "Eins\r\nZwei\n\n  Drei  "; st.Ident = "PA 40010001"; st.Row = "Suite 1";
            Entry pk = st.EntryFor(0, "");
            bool okPkgNotes = pk.Content == "Packen\nZeile a\nZeile b\nEins\nZwei\nDrei\nPA 40010001" && NJoin(Notes.NoteTexts(pk)) == "Eins|Zwei|Drei"
                && Notes.ContentText(pk) == "Packen\nZeile a\nZeile b\nPA 40010001" && pk.Spans.Exists(delegate(FormatSpan f) { return f.Bold && f.Rgb == 0xFF0000 && !f.Note; }) && pk.Content.IndexOf("Notiz:", StringComparison.Ordinal) < 0;
            PackageStep st0 = new PackageStep(); st0.Title = "Nur Titel"; st0.Row = "Suite 1";
            okPkgNotes &= !st0.EntryFor(0, "").HasNotes;

            // XML-Rundweg: Sicherung (Attribut n), Paket (notiz), Plan-Datei
            MemoryStore s = new MemoryStore();
            int sid = SuiteId(s); DateTime w = D(2031, 5, 5);
            Entry src = NoteEntry("Lauf 22040030\nHinweis ü ö ß\nKennung", 1); src.Spans.Add(PackageStep.IdentSpan(src.Content.Length - 7, 7));
            s.Save(w, sid, src); s.Save(w.AddDays(1), sid, NoteEntry("Nur Notiz", 0));
            byte[] xml = Backup.ToXml(s.Snapshot());
            StoreData back = Backup.FromXml(xml);
            Entry rt = EntryIn(back, w, sid), rt2 = EntryIn(back, w.AddDays(1), sid);
            bool okBackup = rt != null && rt.SpanKey() == s.Get(w, sid).SpanKey() && Notes.NoteLines(rt).Count == 1 && rt.HasText && rt2 != null && rt2.IsEmpty && rt2.HasNotes
                && Encoding.UTF8.GetString(xml).Contains(" n=\"1\"");
            string path = Path.Combine(Path.GetTempPath(), "selftest_notiz.apkg");
            Package fw = Package.FromWeek(s, w, "Notizen");
            fw.Save(path);
            Package ld = Package.Load(path);
            PackageBlock b0 = ld.Blocks.Find(delegate(PackageBlock bl) { return bl.Day == 1; }), b1 = ld.Blocks.Find(delegate(PackageBlock bl) { return bl.Day == 2; });
            bool okPkgFile = ld.Blocks.Count == 2 && b0 != null && b0.Data.SpanKey() == s.Get(w, sid).SpanKey() && b1 != null && b1.Data.HasNotes   // Block nur mit Notiz wird mit exportiert
                && File.ReadAllText(path).Contains("notiz=\"1\"");
            File.Delete(path);
            string plan = Path.Combine(Path.GetTempPath(), "selftest_notiz.applan");
            PlanFile.Write(PlanFile.Take(s, w, 7), plan);
            PlanSnapshot ps2 = PlanFile.Read(plan); File.Delete(plan);
            Entry pr = EntryIn(ps2.Data, w, sid);
            bool okPlanFile = pr != null && pr.SpanKey() == s.Get(w, sid).SpanKey() && Notes.NoteLines(pr).Count == 1;

            // Konflikt: Markierung einer Zeile gegen Textaenderung einer anderen Zeile = still zusammengefuehrt; dieselbe Zeile = Konflikt
            Entry bas = new Entry(); bas.Content = "Zeile A\nZeile B\nZeile C";
            Entry mine = bas.Clone(); Notes.SetNote(mine, 1, 1, true);
            Entry theirs = bas.Clone(); theirs.Content = "Zeile A neu\nZeile B\nZeile C"; theirs.Version = 5;
            List<string> cl = new List<string>();
            Entry mg = Conflict.Merge(bas, mine, theirs, cl);
            bool okMergeSide = cl.Count == 0 && mg.Content == "Zeile A neu\nZeile B\nZeile C" && NJoin(Notes.NoteTexts(mg)) == "Zeile B" && mg.Version == 5;
            theirs = bas.Clone(); theirs.Content = "Zeile A\nZeile B neu\nZeile C";
            cl = new List<string>(); mg = Conflict.Merge(bas, mine, theirs, cl);
            bool okMergeClash = cl.Contains("Inhalt") && mg.Content.Contains("Zeile B neu") && mg.Content.Contains("Zeile B\n");
            List<int[]> mlines = Notes.NoteLines(mg);
            foreach (int[] n in mlines) okMergeClash &= mg.Content.Substring(n[1], n[2]) == "Zeile B";   // nur die eigene (markierte) Fassung ist Notiz
            Entry mine2 = bas.Clone(); Notes.SetNote(mine2, 1, 1, true); Entry theirs2 = bas.Clone(); Notes.SetNote(theirs2, 1, 1, true);   // beide gleich
            cl = new List<string>(); mg = Conflict.Merge(bas, mine2, theirs2, cl);
            bool okMergeSame = cl.Count == 0 && Notes.NoteLines(mg).Count == 1;
            Entry unmark = Conflict.Merge(mine, bas.Clone(), mine.Clone(), cl = new List<string>());   // ich hebe auf, andere aendern nichts: Aufhebung gilt
            bool okMergeOff = cl.Count == 0 && Notes.NoteLines(unmark).Count == 0 && unmark.Spans.Count == 0;

            // Paket-Modi: nur leere fuellen (Block nur mit Notizen gilt als leer), Ueberschreiben behaelt Notizen (ans Ende, ohne Doppelte), Anhaengen
            Entry np = new Entry(); np.Content = "Lauf 12340001";
            Entry curNotes = NoteEntry("Meine Notiz", 0);
            Entry fill = Package.Merge(curNotes, np, ApplyMode.FillEmpty);
            bool okFill = fill != null && fill.Content == "Lauf 12340001\nMeine Notiz" && NJoin(Notes.NoteTexts(fill)) == "Meine Notiz" && Notes.ContentText(fill) == "Lauf 12340001";
            Entry curBusy = NoteEntry("Alter Text\nNotiz X", 1);
            bool okBusy = Package.Merge(curBusy, np, ApplyMode.FillEmpty) == null;
            Entry over = Package.Merge(curBusy, np, ApplyMode.Overwrite);
            bool okOver = over.Content == "Lauf 12340001\nNotiz X" && NJoin(Notes.NoteTexts(over)) == "Notiz X";
            Entry np2 = NoteEntry("Lauf 12340001\nNotiz X", 1);   // Paket bringt dieselbe Notiz mit: nicht doppelt
            bool okOver2 = Package.Merge(curBusy, np2, ApplyMode.Overwrite).Content == "Lauf 12340001\nNotiz X";
            Entry app = Package.Merge(curBusy, np, ApplyMode.Append);
            bool okApp = app.Content == "Alter Text\nNotiz X\n\nLauf 12340001" && NJoin(Notes.NoteTexts(app)) == "Notiz X";
            MemoryStore ms = new MemoryStore();
            int msid = SuiteId(ms); DateTime d0 = D(2031, 6, 2);
            ms.Save(d0, msid, NoteEntry("Bleibt", 0));
            Package ap = new Package(); PackageStep ast = new PackageStep(); ast.Title = "Rüsten"; ast.Note = "Paketnotiz"; ast.Row = "Suite 1"; ast.Day = 1; ap.Steps.Add(ast);
            ap.Apply(ms, d0, ApplyMode.FillEmpty, null);
            Entry ae = ms.Get(d0, msid);
            bool okApply = ae.Content == "Rüsten\nPaketnotiz\nBleibt" && NJoin(Notes.NoteTexts(ae)) == "Paketnotiz|Bleibt" && Notes.ContentText(ae) == "Rüsten";

            // Laufnummern: Notizzeilen werden ignoriert (Anzeige, PastRuns/LastRun)
            Entry rn = NoteEntry("Lauf 12340001\nLauf 12340099 noch offen\nPacken 12340050", 1, 2);
            bool okRuns = RunNumbers.Find(rn).Count == 1 && RunNumbers.Find(rn)[0].Run == "0001" && RunNumbers.Find(rn.Content).Count == 3;
            MemoryStore rs = new MemoryStore();
            rs.Save(D(2031, 7, 7), SuiteId(rs), NoteEntry("Lauf 12340001\nLauf 12340099", 1));
            List<KeyValuePair<int, DateTime>> past = Package.PastRuns(rs, "1234");
            okRuns &= past.Count == 1 && past[0].Key == 1;

            // Protokolltext und Verlauf
            string pv = Audit.Preview(only), pv0 = Audit.Preview(new Entry()), pv1 = Audit.Preview(mixed);
            AuditEntry ma = new AuditEntry(); ma.Before = mixed.Clone(); ma.After = mixed.Clone(); Notes.SetNote(ma.After, 1, 1, false);
            AuditEntry mb = new AuditEntry(); mb.Before = ma.After; mb.After = mixed;
            AuditEntry mc = new AuditEntry(); mc.Before = mixed; mc.After = mixed.Clone(); mc.After.Content = "anders";
            bool okAudit = pv.StartsWith("(nur Notizen) ") && pv0 == "(leer)" && pv1.StartsWith("Lauf 12340001")
                && Audit.NoteChange(ma) == "Notiz aufgehoben" && Audit.NoteChange(mb) == "Notiz markiert" && Audit.NoteChange(mc) == ""
                && Audit.Summary(mb).StartsWith("Notiz markiert: ") && Audit.Text(mixed, new[] { "F", "S", "N" }).Contains("[Notiz] Hinweis");

            // Loeschen: Notiztext zaehlt als Text
            Entry no = NoteEntry("Text\nNotiz hier", 1), nn = new Entry(); nn.Content = "Text";
            bool okRemove = RemovesText(no, nn) && !RemovesText(no, no.Clone());

            // Suche findet Notizen (Inhalt bleibt Inhalt der Datei)
            bool okFind = s.Find("hinweis").Count == 1;

            sb.AppendLine("Notizen: leer-Regel=" + Ok(okEmpty) + " Normalize=" + Ok(okNorm) + " Paket mehrere Notizen=" + Ok(okPkgNotes) + " Sicherung XML=" + Ok(okBackup)
                + " Paketdatei=" + Ok(okPkgFile) + " Plan-Datei=" + Ok(okPlanFile) + " Zusammenführen andere Zeile=" + Ok(okMergeSide) + " gleiche Zeile=" + Ok(okMergeClash)
                + " beide markiert=" + Ok(okMergeSame) + " aufgehoben=" + Ok(okMergeOff));
            sb.AppendLine("Notizen Paket: nur leere füllen=" + Ok(okFill && okBusy) + " überschreiben=" + Ok(okOver && okOver2) + " anhängen=" + Ok(okApp) + " Übernahme=" + Ok(okApply)
                + " Laufnummern=" + Ok(okRuns) + " Protokoll=" + Ok(okAudit) + " Löschen=" + Ok(okRemove) + " Suche=" + Ok(okFind));
        }

        // Ansicht und Editor (Montag ohne Testdaten anderer Selbsttests)
        void SelfTestNotesUi(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Notizen Oberfläche: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            bool removed = false;
            try
            {
                DateTime wk = D(2024, 9, 23), day = wk.AddDays(1), day3 = wk.AddDays(3);
                int sr = 0, cr = RowOf("Raum CC"), sid = rowInfo[sr].Id, cid = rowInfo[cr].Id;
                store.Save(day, sid, NoteEntry("Lauf 22040030\nProbe bis 10:00 bringen", 1));
                store.Save(day3, cid, NoteEntry("Schlüssel beim Pförtner", 0));
                start = wk; Rebuild(true); Expand(sr);
                CellData c = CellOf(sr, 1), cc = CellOf(cr, 3);
                // Ansicht: Notiz zaehlt nicht als Inhalt (Hohlpunkt statt Punkt, zugeklappt), Laufnummer aus der Textzeile
                bool okView = c != null && c.Cur.HasNotes && c.Cur.HasText && cc != null && cc.Cur.IsBlank && cc.Cur.HasNotes && view.DefaultFold()[cr]
                    && c.CB != null && c.CB.Para.Count >= 2 && c.CB.Para[c.CB.Para.Count - 1] == 1 && c.CB.ParaAt(1) == 0 && c.CB.ParaAt(c.CB.LineH[0] + 1) == 1;

                // Editor: Notizzeile wird als Hintergrund geladen und unveraendert zurueckgelesen
                BeginEdit(c);
                int p2 = edContent.Text.IndexOf("Probe");
                edContent.Select(p2, 1); bool back2 = edContent.SelectionBackColor.ToArgb() == NoteArgb;
                edContent.Select(0, 1); bool back1 = edContent.SelectionBackColor.ToArgb() == NoteArgb;
                Entry w0 = WorkEntry(c);
                bool okLoad = back2 && !back1 && w0.SpanKey() == c.Cur.SpanKey() && !EntryDirty(c, w0) && Notes.NoteLines(w0).Count == 1;

                // markieren / aufheben im Editor
                edContent.Select(0, 3); EditorNote(true);
                Entry w1 = WorkEntry(c);
                bool okMark = Notes.NoteLines(w1).Count == 2 && !w1.HasText && w1.IsEmpty && EntryDirty(c, w1);
                edContent.Select(0, edContent.TextLength); EditorNote(false);
                Entry w2 = WorkEntry(c);
                bool okUnmark = Notes.NoteLines(w2).Count == 0 && w2.Spans.Count == 0 && w2.HasText;
                edContent.Select(p2, 2); EditorNote(true);

                // Enter am Ende der Notizzeile: neue Zeile ohne Notizformat; Standardformat hebt Notizen nicht auf
                edContent.Select(edContent.TextLength, 0);
                bool enter = NoteEnter();
                edContent.SelectedText = "Neuer Text";
                Entry w3 = WorkEntry(c);
                bool okEnter = enter && w3.Content == "Lauf 22040030\nProbe bis 10:00 bringen\nNeuer Text" && Notes.NoteLines(w3).Count == 1 && NJoin(Notes.NoteTexts(w3)) == "Probe bis 10:00 bringen";
                edContent.SelectAll(); ClearFormat();
                Entry w4 = WorkEntry(c);
                bool okClear = Notes.NoteLines(w4).Count == 1 && w4.Spans.Exists(delegate(FormatSpan f) { return f.Note && f.Italic; });
                edContent.Select(0, 0);
                bool okEnter2 = !NoteEnter();   // am Zeilenanfang / ausserhalb einer Notiz: normale Eingabe
                EndEdit(false);

                // Rechtsklick in der Ansicht: sofort speichern wie der LKZ-Haken, Recht "Text aendern", Verlauf
                CellData cv = CellOf(sr, 1);
                view.Selected = cv; view.MenuPara = 0; ViewNote(true);
                Entry s1 = store.Get(day, sid);
                bool okOn = Notes.NoteLines(s1).Count == 2 && s1.IsEmpty && CellOf(sr, 1).Cur.HasNotes && CellOf(sr, 1).Cur.IsBlank;
                view.Selected = CellOf(sr, 1); view.MenuPara = 0; ViewNote(false);
                Entry s2 = store.Get(day, sid);
                bool okOff = Notes.NoteLines(s2).Count == 1 && s2.HasText && NJoin(Notes.NoteTexts(s2)) == "Probe bis 10:00 bringen";
                List<AuditEntry> au = store.GetAudit(day, sid);
                bool okLog = au.Count >= 2 && Audit.NoteChange(au[au.Count - 1]) == "Notiz aufgehoben" && Audit.NoteChange(au[au.Count - 2]) == "Notiz markiert";
                string ver = Audit.Summary(au[au.Count - 2]);
                okLog &= ver.StartsWith("Notiz markiert: (nur Notizen)");
                removed = me.Rights.Remove(Perm.EditText);
                string before = store.Get(day, sid).SpanKey();
                view.Selected = CellOf(sr, 1); view.MenuPara = 0; ViewNote(true);
                bool okRight = store.Get(day, sid).SpanKey() == before;
                if (removed) me.Rights.Add(Perm.EditText);
                removed = false;
                // anderer PC hat inzwischen eine Zeile davor eingefuegt: die Aufhebung folgt dem Text, nicht der Zeilennummer
                Entry other = store.Get(day, sid).Clone(); other.Content = "Vorspann\n" + other.Content; other.Spans.Clear();
                Notes.SetNote(other, 2, 2, true);
                store.Save(day, sid, other);
                view.Selected = cv; view.MenuPara = 1; ViewNote(false);   // cv zeigt noch den alten Stand: "Probe bis ..." war Zeile 1
                Entry s3 = store.Get(day, sid);
                bool okMoved = s3.Content == "Vorspann\nLauf 22040030\nProbe bis 10:00 bringen" && Notes.NoteLines(s3).Count == 0 && s3.HasText;
                sb.AppendLine("Notizen Oberfläche: Ansicht=" + Ok(okView) + " Editor lädt=" + Ok(okLoad) + " markieren=" + Ok(okMark) + " aufheben=" + Ok(okUnmark) + " Enter=" + Ok(okEnter && okEnter2)
                    + " Standardformat=" + Ok(okClear) + " Rechtsklick an=" + Ok(okOn) + " aus=" + Ok(okOff) + " Verlauf=" + Ok(okLog) + " ohne Recht=" + Ok(okRight) + " Zeilen verschoben=" + Ok(okMoved));
            }
            catch (Exception ex) { sb.AppendLine("Notizen Oberfläche: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                if (removed) me.Rights.Add(Perm.EditText);
                if (editCell != null) EndEdit(false);
                start = keep; Rebuild(true);
            }
        }
    }
}
#endif
