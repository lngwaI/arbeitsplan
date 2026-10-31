#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5f (Equipment-Notiz kursiv) im Modell: kursiver FormatSpan nur fuer den Notiz-Teil der Geraetezeile (kein Note-Span, zaehlt als Inhalt),
    // Plantext unveraendert (Compose/Golden ueber alle geladenen Pakete), Puffer: Marker im Text (Plain = alter Text, Anzeige = eigene eingerueckte kursive
    // Zeile), Alt-Daten ohne Marker, Suche und Equipment-Export zaehlen den Notiztext, XML-Rundwege. Editor/Leiste/Vormerkung: PackageForm.TestUx5f.cs.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static EquipAssign UxNoteEq(string sh, string id, string note, string fallback)
        {
            EquipAssign a = new EquipAssign(); a.Short = sh; if (id.Length > 0) a.Choices.Add(new EquipChoice(sh, id)); a.Note = note; a.Fallback = fallback; return a;
        }

        // Texte der kursiven Spans (ohne Note-Spans), "|"-getrennt
        static string UxItalics(Entry e)
        {
            List<string> r = new List<string>();
            foreach (FormatSpan sp in e.Spans) if (sp.Italic && sp.Rgb == 0 && sp.Size == 0f) r.Add(e.Content.Substring(sp.Start, sp.Length));
            return string.Join("|", r.ToArray());
        }

        static Entry EntryWith(BufferItem b) { Entry e = new Entry(); e.Buffers.Add(b.Clone()); return e; }

        void SelfTestUX5f(StringBuilder sb)
        {
            // (1) normaler Schritt: Notiz-Teil kursiv, Text unveraendert, zaehlt als Inhalt
            PackageStep s = new PackageStep(); s.Title = "Lauf"; s.Source = "Lauf"; s.Row = "Suite 1"; s.Day = 1;
            s.Equipment.Add(UxNoteEq("VS", "H1-F-0103-04", "(nur eins vorhanden)", "")); s.Equipment.Add(UxNoteEq("TFF", "H1-X-0101", "Reserve", "H1-X-0103"));
            s.Equipment.Add(UxNoteEq("Schlauch", "", "DN 25", "")); s.Equipment.Add(UxNoteEq("Pumpe", "H1-P-0101-05", "", ""));
            const string Plain = "Lauf\nVS: H1-F-0103-04 (nur eins vorhanden)\nTFF: H1-X-0101 (Ausweich: H1-X-0103) Reserve\nSchlauch: DN 25\nPumpe: H1-P-0101-05";
            Entry e = s.EntryFor(0, "");
            bool okText = s.Compose(0, "") == Plain && e.Content == Plain && Plain.IndexOf(BufferSet.NoteMark) < 0 && s.Equipment[1].Line(0) == "TFF: H1-X-0101 (Ausweich: H1-X-0103) Reserve";
            bool okSpan = UxItalics(e) == "(nur eins vorhanden)|Reserve|DN 25";
            bool okInhalt = !e.HasNotes && !e.IsEmpty && Notes.ContentText(e) == Plain;   // Geraetezeile bleibt Inhalt, keine Notizzeile
            // mit eigener Notizzeile (A3) und Kennung: Positionen der Spans stimmen weiterhin
            PackageStep s2 = s.Clone(); s2.Note = "Hinweis A"; s2.Kind = StepKind.Run; s2.IdentStart = "PA 40010001";
            Entry e2 = s2.EntryFor(0, "");
            bool okMix = UxItalics(e2).StartsWith("(nur eins vorhanden)|Reserve|DN 25") && e2.HasNotes && Notes.NoteTexts(e2)[0] == "Hinweis A" && e2.Content.EndsWith("PA 40010001") && e2.Spans.Exists(delegate(FormatSpan x) { return x.Bold && x.Rgb == 0xFF0000; });
            // ohne Notizen: kein Span (Alt-Daten unveraendert)
            PackageStep s3 = new PackageStep(); s3.Title = "Lauf"; s3.Equipment.Add(UxNoteEq("TFF", "H1-X-0101", "", "")); s3.Equipment.Add(UxNoteEq("Pumpe", "H1-P-0101-05", "", "H1-P-0102-05"));
            Entry e3 = s3.EntryFor(0, "");
            bool okAlt = e3.Spans.Count == 0 && e3.Content == "Lauf\nTFF: H1-X-0101\nPumpe: H1-P-0101-05 (Ausweich: H1-P-0102-05)" && s3.Compose(0, "") == e3.Content;
            // Golden: alle geladenen Pakete, alle Laeufe: Inhalt == Plantext, nur kursive Spans neu
            int badGold = 0, runs = 0;
            foreach (Package pk in new List<Package>(Package.Seen))
                foreach (PackageStep st in pk.Steps)
                {
                    if (st.IsBuffer) continue;
                    for (int r = 0; r < Math.Max(1, Math.Min(st.Count, 4)); r++)
                    {
                        Entry x = st.EntryFor(r, pk.Prefix); runs++;
                        if (x.Content != st.Compose(r, pk.Prefix)) badGold++;
                        foreach (FormatSpan sp in x.Spans) if (sp.Italic && sp.Rgb == 0 && sp.Start + sp.Length > x.Content.Length) badGold++;
                    }
                }
            // Plan: uebernehmen -> Spans im Block, Rundweg Datei
            MemoryStore ms = new MemoryStore(); DateTime d0 = new DateTime(2036, 3, 3);
            Package ap = new Package(); ap.Name = "UX5f"; ap.Steps.Add(s.Clone()); Package.Seen.Remove(ap);
            ap.Apply(ms, d0, ApplyMode.Overwrite, null);
            int srow = 0; foreach (RowInfo ri in ms.GetRows()) if (ri.Name == "Suite 1") srow = ri.Id;
            Entry blk = ms.Get(d0, srow);
            bool okApply = blk != null && blk.Content == Plain && UxItalics(blk) == "(nur eins vorhanden)|Reserve|DN 25";
            Package rt = Package.FromXml(ap.ToXml()); Package.Seen.Remove(rt);
            Entry er = rt.Steps[0].EntryFor(0, rt.Prefix);
            bool okXml = er.Content == Plain && UxItalics(er) == UxItalics(e);
            sb.AppendLine("UX5f Plan: Notiz-Teil kursiv " + Ok(okSpan) + " Text unverändert " + Ok(okText) + " zählt als Inhalt, keine Notizzeile " + Ok(okInhalt) + " mit Notizzeile/Kennung " + Ok(okMix)
                + " ohne Notiz kein Span " + Ok(okAlt) + " Golden (" + runs + " Läufe) " + Ok(badGold == 0) + " Übernehmen " + Ok(okApply) + " Paket-Rundweg " + Ok(okXml));

            // (2) Puffer: Marker im Text, Plain = alter Text, Anzeige = eigene eingerueckte Zeile (kursiv)
            PackageStep pu = new PackageStep(); pu.Title = "Puffer A"; pu.Source = "Puffer mobil"; pu.Row = "Puffer"; pu.Day = 1;
            PufferSpec sp1 = new PufferSpec(); sp1.Name = "Puffer A"; sp1.Typ = "mobil"; sp1.Lines.Add(UxNoteEq("Ansatztank", "AT-1", "500 L", "")); sp1.Lines.Add(UxEq("VT", "VT-1")); pu.Puffers.Add(sp1);
            pu.SyncPuffers();
            BufferItem bi = pu.BuffersFor(0, "")[0];
            const string PlainBuf = "Ansatztank: AT-1 500 L\nVT: VT-1";
            bool[] it; string disp = BufferSet.ForDisplay(bi.Text, out it);
            bool okMark = bi.Text.IndexOf(BufferSet.NoteMark) > 0 && BufferSet.Plain(bi.Text) == PlainBuf && bi.Name == "Puffer A";
            string itl = ""; for (int i = 0; i < disp.Length; i++) if (it[i]) itl += disp[i];
            bool okDisp = disp == "Ansatztank: AT-1\n  500 L\nVT: VT-1" && itl == "500 L" && it.Length == disp.Length;
            // Alt-Daten ohne Marker: Anzeige = Text, nichts kursiv
            bool[] it0; string d0t = BufferSet.ForDisplay("Ansatz 500 L\nWFI: H1-T-0102", out it0);
            bool okOld = d0t == "Ansatz 500 L\nWFI: H1-T-0102" && Array.IndexOf(it0, true) < 0 && BufferSet.Plain("Ansatz 500 L") == "Ansatz 500 L";
            // Plan + Datei: Marker bleibt erhalten, alter Text gleich; Suche (auch ueber die Notiz hinweg) und Equipment-Export
            Package pp = new Package(); pp.Name = "UX5f-Puffer"; pp.Steps.Add(pu.Clone()); Package.Seen.Remove(pp);
            MemoryStore pm = new MemoryStore(); int puf = PufferSetup.Row(pm.GetRows()).Id;
            pp.Apply(pm, d0, ApplyMode.Overwrite, null);
            Entry pb = pm.Get(d0, puf);
            bool okPl = pb != null && pb.Buffers.Count == 1 && pb.Buffers[0].Text == bi.Text && BufferSet.Plain(pb.Buffers[0].Text) == PlainBuf;
            StoreData back = Backup.FromXml(Backup.ToXml(pm.Snapshot()));
            Entry rb = EntryIn(back, d0, puf);
            bool okBk = rb != null && rb.Buffers.Count == 1 && rb.Buffers[0].Text == bi.Text && BufferSet.Key(rb.Buffers) == BufferSet.Key(pb.Buffers);
            bool okFind = BufferSet.Matches(pb.Buffers, "AT-1 500 L") && BufferSet.Matches(pb.Buffers, "500 l") && pm.Find("AT-1 500 L").Count > 0 && pm.Find("VT-1").Count > 0;
            Entry ex = new Entry(); ex.Buffers.Add(new BufferItem { Id = "p1", Typ = "mobil", Name = "P", Text = "Tank: H1-T-0102" + BufferSet.NoteMark + "H1-T-0103 neu" });
            pm.Save(d0.AddDays(1), puf, ex);
            List<string> ids = EquipExport.Collect(pm, d0, d0.AddDays(1));
            bool okExp = ids.Contains("H1-T-0102") && ids.Contains("H1-T-0103") && ids.Count == 2;
            bool okAud = Audit.Text(pb, new[] { "F", "S", "N" }).IndexOf(BufferSet.NoteMark) < 0 && Audit.Text(pb, new[] { "F", "S", "N" }).Contains("Ansatztank: AT-1 500 L");
            // Wochenansicht: die Notiz steht in einer eigenen Zeile unter dem Geraet, kursiv (nur dieser Teil); nichts davon aendert den Block
            bool okView = true;
            if (!systemPc)
            {
                DateTime keep = start;
                try
                {
                    int pr = RowOf("Puffer"); DateTime wk = D(2024, 10, 7), day = wk.AddDays(1);
                    store.Save(day, rowInfo[pr].Id, bi.Text.Length > 0 ? EntryWith(bi) : null);
                    start = wk; Rebuild(true); Expand(pr);
                    CellData pc = CellOf(pr, 1);
                    TextBlock body = pc.BufParts.Count == 1 ? pc.BufParts[0].Body : null;
                    bool ital = false, plain = false; int lines = 0, nl = 0;
                    if (body != null)
                    {
                        lines = body.Lines.Count;
                        foreach (List<Item> ln in body.Lines) { nl++; foreach (Item x in ln) { if (x.Text.Contains("500") || x.Text.Contains("L")) { if (x.Font.Italic) ital = true; } if (x.Text.Contains("AT-1") || x.Text.Contains("VT-1")) { if (!x.Font.Italic) plain = true; } } }
                    }
                    okView = body != null && lines == 3 && ital && plain;
                }
                catch (Exception exv) { okView = false; sb.AppendLine("UX5f Wochenansicht: Ausnahme " + exv.GetType().Name + " " + exv.Message); }
                finally { start = keep; Rebuild(true); }
            }
            sb.AppendLine("UX5f Puffer: Marker im Text, Plain = alter Text " + Ok(okMark) + " Anzeige eigene kursive Zeile " + Ok(okDisp) + " Alt-Daten ohne Marker " + Ok(okOld) + " Plan " + Ok(okPl)
                + " Datei-Rundweg " + Ok(okBk) + " Suche zählt Notiztext " + Ok(okFind) + " Equipment-Export " + Ok(okExp) + " Verlauf ohne Marker " + Ok(okAud) + " Wochenansicht kursive Zeile " + Ok(okView));
        }
    }
}
#endif
