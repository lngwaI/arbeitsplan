#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests P4: Rundgangsleiste immer einzeilig (Stempel / ohne Stempel / kompakte Liste), Zyklen-Namen (Label, XML, Merge, Dialog),
    // Kursiv = Notiz. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        void SelfTestP4(StringBuilder sb)
        {
            try { P4Rounds(sb); } catch (Exception ex) { sb.AppendLine("P4 Rundgänge: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P4Names(sb); } catch (Exception ex) { sb.AppendLine("P4 Zyklen-Namen: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { P4Notes(sb); } catch (Exception ex) { sb.AppendLine("P4 Kursiv-Notiz: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        void P4Rounds(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("P4 Rundgänge: übersprungen (Systemrechner)"); return; }
            DateTime keep = start; Size ks = ClientSize;
            bool oneRow = true, groups = true; string modes = "";
            try
            {
                start = IsoWeek.Monday(D(2024, 9, 23)).AddDays(3);   // Donnerstag: Woche reicht ueber die KW-Grenze (4 + 3 Spalten)
                foreach (Size sz in new Size[] { new Size(1024, 768), new Size(1920, 1080) })
                {
                    ClientSize = sz; Rebuild(true);
                    groups &= view.Rounds.Count == 2;
                    int h = Theme.Current.RoundH + 6;
                    foreach (RoundGroup g in view.Rounds)
                    {
                        oneRow &= g.R.Height == h;
                        foreach (Rectangle r in g.RItems) oneRow &= r.Bottom <= g.R.Bottom && r.Top >= g.R.Top;
                        modes += g.Mode;
                    }
                    modes += "/";
                }
                // Aufklappliste: ein Eintrag je Rundgang, Haken wechselt (Closing bei ItemClicked wird abgebrochen)
                RoundGroup rg = view.Rounds[view.Rounds.Count - 1];
                ContextMenuStrip m = view.TestRoundMenu(rg.Monday);
                bool okList = m.Items.Count == rg.Defs.Count && rg.CompactTitle.StartsWith("Rundgänge KW" + IsoWeek.Number(rg.Monday) + " ") && rg.CompactTitle.EndsWith(" ▾");
                if (me.Has(Perm.Rounds) && m.Items.Count > 0)
                {
                    ToolStripMenuItem it0 = (ToolStripMenuItem)m.Items[0]; bool b0 = it0.Checked;
                    it0.PerformClick();
                    okList &= it0.Checked != b0 && view.Rounds[view.Rounds.Count - 1].Done.Contains(rg.Defs[0].Key) != b0;
                    it0.PerformClick();
                    okList &= it0.Checked == b0;
                }
                sb.AppendLine("P4 Rundgangsleiste: einzeilig=" + Ok(oneRow && groups) + " Aufklappliste=" + Ok(okList) + " (Modi 4:3/16:9 " + modes + ")");
            }
            finally { ClientSize = ks; start = keep; Rebuild(true); }
        }

        void P4Names(StringBuilder sb)
        {
            int row; FreezeItem bag; MemoryStore st = FzStore(out row, out bag);
            Entry e = st.Get(FzD0.Date, row);
            e.Freezes[0].CycleNames = "A||C"; st.Save(FzD0.Date, row, e);
            FreezeItem f = e.Freezes[0];
            List<FreezeCycle> cy = FreezeCalc.Cycles(f);
            bool okLabel = cy[0].Label == "A" && cy[1].Label == "Z2" && cy[2].Label == "C" && cy[3].Label == "Z4" && FzBag(FzD0).CycleName(5) == "Z5";
            FreezeItem h = FzBag(FzD0); h.CycleNames = "A|B|C|D"; h.Ist[4] = FzD0.AddHours(1);
            bool okHint = FreezeCalc.Hints(h).Exists(delegate(string x) { return x.StartsWith("D startet vor dem Ende von A"); });
            StoreData back = Backup.FromXml(Backup.ToXml(st.Snapshot()));
            Entry re = null; foreach (StoredEntry s in back.Entries) if (s.RowId == row) re = s.Data;
            bool okXml = re != null && re.Freezes[0].CycleNames == "A||C" && FreezeCalc.Cycles(re.Freezes[0])[2].Label == "C" && re.Freezes[0].Core() == f.Core() && re.Freezes[1].CycleNames == "";
            Entry mine = e.Clone(); mine.Freezes[0].CycleNames = "X"; Entry theirs = e.Clone(); theirs.Freezes[0].Note = "andere Notiz";
            List<string> cl = new List<string>(); Entry mg = Conflict.Merge(e, mine, theirs, cl);
            bool okMerge = cl.Count == 0 && mg.Freezes[0].CycleNames == "X" && mg.Freezes[0].Note == "andere Notiz";
            Entry t2 = e.Clone(); t2.Freezes[0].CycleNames = "Y"; cl = new List<string>(); mg = Conflict.Merge(e, mine, t2, cl);
            okMerge &= cl.Contains("Freeze");
            // Dialog: Spalte Name, Schnellbelegung, doppelte Namen erlaubt
            FreezeChoice ch = new FreezeChoice(); ch.Item = bag.Clone();
            bool okDlg;
            using (FreezeForm d = new FreezeForm(ch))
            {
                d.TestSetName(2, "Dopp"); d.TestSetName(3, "Dopp");
                okDlg = d.TestItem.CycleNames == "|Dopp|Dopp" && d.TestCell(2, 1) == "Dopp" && d.TestCell(2, 0) == "Z2" && d.TestCell(1, 1) == "";
                d.TestQuick(2); okDlg &= d.TestItem.CycleNames.StartsWith("A|B|C") && d.TestCell(3, 1) == "C";
                d.TestQuick(3); okDlg &= d.TestCell(9, 1) == "9";
                d.TestQuick(1); okDlg &= d.TestItem.CycleNames == "" && d.TestCell(3, 1) == "";
            }
            sb.AppendLine("P4 Zyklen-Namen: Label=" + Ok(okLabel) + " Hinweistext=" + Ok(okHint) + " XML=" + Ok(okXml) + " Zusammenführen=" + Ok(okMerge) + " Dialog=" + Ok(okDlg));
        }

        void P4Notes(StringBuilder sb)
        {
            Entry z = new Entry(); z.Content = "Lauf 12340001\nHinweis zum Lauf";
            FormatSpan all = new FormatSpan(); all.Start = 14; all.Length = 16; all.Italic = true; z.Spans.Add(all);
            Entry w = new Entry(); w.Content = z.Content;
            FormatSpan one = new FormatSpan(); one.Start = 14; one.Length = 7; one.Italic = true; w.Spans.Add(one);   // nur ein Wort
            bool okZ = Notes.NoteLines(z).Count == 1 && NJoin(Notes.NoteTexts(z)) == "Hinweis zum Lauf" && Notes.ContentText(z) == "Lauf 12340001" && z.HasText;
            bool okW = Notes.NoteLines(w).Count == 0 && Notes.ContentText(w) == w.Content;
            PackageStep st = new PackageStep(); st.Title = "Packen"; st.Note = "Eins"; st.Row = "Suite 1";
            Entry pk = st.EntryFor(0, "");
            bool okPk = NJoin(Notes.NoteTexts(pk)) == "Eins" && pk.Spans.Exists(delegate(FormatSpan f) { return f.Italic; }) && Notes.ContentText(pk) == "Packen";
            MemoryStore s0 = new MemoryStore(); int sid = SuiteId(s0); s0.Save(D(2031, 5, 5), sid, z);
            Entry rt = EntryIn(Backup.FromXml(Backup.ToXml(s0.Snapshot())), D(2031, 5, 5), sid);
            bool okRt = rt != null && rt.SpanKey() == z.SpanKey() && Notes.NoteLines(rt).Count == 1;
            sb.AppendLine("P4 Kursiv = Notiz: ganze Zeile=" + Ok(okZ) + " einzelnes Wort nicht=" + Ok(okW) + " Paket-Notiz=" + Ok(okPk) + " XML=" + Ok(okRt));
        }
    }
}
#endif
