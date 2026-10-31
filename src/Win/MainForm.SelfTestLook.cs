#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Look "Plan-Stil": Umschalten aendert nichts am Bestand, Standard-Layout bleibt, Pruefpunkt im Kopf, Alles offen, Titel.
    partial class MainForm
    {
        public void TestLook(string name) { SetLook(name == "plan" ? "plan" : null); }

        void SelfTestLook(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("Ansicht Plan-Stil: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            try { SelfTestLookRun(sb); }
            catch (Exception ex) { sb.AppendLine("Ansicht Plan-Stil: FEHLER Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            finally { if (editCell != null) EndEdit(false); SetLook(null); start = keep; Rebuild(true); }
        }

        string LookSig()
        {
            StringBuilder s = new StringBuilder();
            foreach (CellData c in view.Cells) s.Append(c.Cur.Version).Append(',').Append(c.R.Height).Append(';');
            return s.ToString();
        }

        void SelfTestLookRun(StringBuilder sb)
        {
            SetLook(null); Rebuild(true);
            string sig0 = LookSig();
            int rows0 = store.GetRows().Count;
            SetLook("plan");
            bool okOn = Theme.Current.Dense && miLookPlan.Checked && !miLookStd.Checked;
            SetLook(null);
            bool okBack = !Theme.Current.Dense && miLookStd.Checked && LookSig() == sig0 && store.GetRows().Count == rows0;   // Version + Zeilenhoehen wie vorher

            DateTime wk = D(2024, 8, 5);   // Mo, ohne Testdaten
            int lr = rowInfo.FindIndex(delegate(RowInfo r) { return r.HasLkz; });
            if (lr < 0) { sb.AppendLine("Ansicht Plan-Stil: FEHLER Testzeile fehlt"); return; }
            start = wk; Rebuild(true);
            bool foldStd = Array.TrueForAll(view.DefaultFold(), delegate(bool b) { return b; });
            SetLook("plan");
            bool foldPlan = Array.TrueForAll(view.DefaultFold(), delegate(bool b) { return !b; }) && Array.TrueForAll(view.Collapsed, delegate(bool b) { return !b; });
            bool okExport;
            using (Bitmap eb = Export.RenderWeek(view, "Test", start)) okExport = eb.Width == view.Width && eb.Height > view.Height;   // Export nach dem Umschalten (neue Pinsel, Masse)

            // Pruefpunkt im Kopf: Klick setzt den Haken, Zeile bleibt offen, kein Block wird bearbeitet
            CellData c2 = CellOf(lr, 2);
            bool single = c2.RChecks.Height == 0 && c2.RHead.Height > 0;
            Point p = view.SlotPoint(c2, 0);
            bool inHead = single && c2.RHead.Contains(p);
            bool was = c2.Cur.Lkz;
            view.TestClick(p);
            Entry le = store.Get(c2.Day, rowInfo[lr].Id);
            bool okClick = inHead && le != null && le.Lkz != was && !view.Collapsed[lr] && editCell == null;
            view.TestClick(view.SlotPoint(CellOf(lr, 2), 0));   // zuruecksetzen
            le = store.Get(c2.Day, rowInfo[lr].Id);
            okClick &= le != null && le.Lkz == was;

            // Titel: nur grosse Schrift am Textanfang
            CellData tc = CellOf(lr == 0 ? 1 : 0, 3);
            Entry te = new Entry(); te.Content = "Titel\nText darunter";
            FormatSpan fs = new FormatSpan(); fs.Start = 0; fs.Length = 5; fs.Size = 14; te.Spans.Add(fs);
            tc.Cur = te; tc.Show(te); view.MarkAllDirty(); view.Relayout();
            bool t1 = tc.TitleH > 0 && tc.TitleMore;
            te.Spans.Clear(); tc.Cur = te; tc.Show(te); view.MarkAllDirty(); view.Relayout();
            bool okTitle = t1 && tc.TitleH == 0;

            sb.AppendLine("Ansicht Plan-Stil: umschalten=" + Ok(okOn && okBack) + " alle Zeilen offen=" + Ok(foldStd && foldPlan) + " Prüfpunkt im Kopf=" + Ok(okClick) + " Titel=" + Ok(okTitle) + " Export=" + Ok(okExport));
        }
    }
}
#endif
