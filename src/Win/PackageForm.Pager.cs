using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Vorschau "So erscheint der Schritt im Plan": immer nur ein Eintrag (ein Lauf/Termin des gewaehlten Schritts), Blaettern mit ◀ ▶ oder
    // Pfeiltasten (Fokus im Vorschaufeld). Auswahl im Kalender und Blaettern bleiben synchron (curRun = Wiederholung des gewaehlten Schritts).
    partial class PackageForm
    {
        readonly Button bPrev = new Button(), bNext = new Button();
        readonly Label lPage = new Label();
        List<RunDate> curDates = new List<RunDate>();   // Termine des gewaehlten Schritts, nach Tag sortiert
        int curRun = -1;                                // Wiederholung (RunDate.Run) des angezeigten Eintrags
        int nextRun = -1;                               // beim naechsten Bind/UpdatePreview zu waehlender Lauf (Klick/Verschieben im Kalender)

        Panel BuildPager(Control content, int w)
        {
            Panel p = new Panel(); p.Tag = "keep"; p.Dock = DockStyle.Right; p.Width = w;
            Panel head = new Panel(); head.Tag = "keep"; head.Dock = DockStyle.Top; head.Height = Font.Height + 8;
            lPreview = new Label(); lPreview.Dock = DockStyle.Fill; lPreview.TextAlign = ContentAlignment.MiddleLeft; lPreview.AutoEllipsis = true;
            lPage.Dock = DockStyle.Right; lPage.Width = 5 * Font.Height + 8; lPage.TextAlign = ContentAlignment.MiddleCenter; lPage.AutoEllipsis = true;
            foreach (Button b in new[] { bPrev, bNext }) { b.Dock = DockStyle.Right; b.Width = 28; b.TabStop = false; }
            bPrev.Text = "◀"; bNext.Text = "▶";
            bPrev.Click += delegate { GoPage(PageIndex() - 1); }; bNext.Click += delegate { GoPage(PageIndex() + 1); };
            tip.SetToolTip(bPrev, "Voriger Eintrag (Pfeil links/hoch im Vorschaufeld)"); tip.SetToolTip(bNext, "Nächster Eintrag (Pfeil rechts/runter im Vorschaufeld)");
            blockPreview.Page += delegate(int d) { GoPage(PageIndex() + d); };
            head.Controls.Add(lPreview); head.Controls.Add(bPrev); head.Controls.Add(lPage); head.Controls.Add(bNext);   // zuletzt hinzugefuegt = zuerst angedockt (ganz rechts)
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content); p.Controls.Add(head);
            return p;
        }

        // Position des angezeigten Eintrags in curDates, -1 = keiner
        int PageIndex() { return curDates.FindIndex(delegate(RunDate d) { return d.Run == curRun; }); }

        void GoPage(int ix)
        {
            if (curDates.Count == 0) return;
            ix = Math.Max(0, Math.Min(curDates.Count - 1, ix));
            curRun = curDates[ix].Run;
            strip.Select(cur, curRun);
            ShowCard();
        }

        // Klick im Kalender: Schritt und Lauf waehlen (liegt der Lauf in einem anderen Schritt, wechselt der Editor dorthin)
        void StripClicked(PlanStrip.Item it)
        {
            CommitUndo();
            if (it.Step != cur) { nextRun = it.Run; Bind(it.Step); return; }
            int ix = curDates.FindIndex(delegate(RunDate d) { return d.Run == it.Run; });
            if (ix >= 0) GoPage(ix);
        }

        // Karte des gewaehlten Eintrags (Raum-Kopf, Tag/Datum, Puffer-Zeilen, Plantext mit Format) und Beschriftung "Lauf 2 von 5"
        void ShowCard()
        {
            int ix = PageIndex(), n = curDates.Count;
            List<BlockPreview.Card> cards = new List<BlockPreview.Card>();
            if (cur != null && ix >= 0)
            {
                RunDate d = curDates[ix];
                BlockPreview.Card c = new BlockPreview.Card();
                c.Room = cur.Row.Length > 0 ? cur.Row : "Raum fehlt"; c.Missing = cur.Row.Length == 0;
                c.Day = RelOf(cur, d) + (cur.Pinned(d.Run) ? " (verschoben)" : "");
                Package pv = StepsView(); List<RunDate> plan = Plan();
                c.Data = cur.EntryFor(d.Run, prefix); pv.WithCode(c.Data.Buffers);   // Puffernamen mit Produktcode wie im Plan
                if (cur.Freeze != null && RowIsFreeze(cur)) { c.Data = pv.FreezeRest(cur, d.Run); string fh = PlanView.FreezeHead(pv.FreezeFor(cur, d.Run, pv.LaufOf(plan, d) + 1, DateTime.Today, pv.Day1)); c.Data.Content = c.Data.Content.Length > 0 ? c.Data.Content + "\n" + fh : fh; }
                if (pv.CardsSplit(cur, pv.Day1))   // UX-5c: Karten mit eigenem Versatz, je Tag ein Block
                {
                    int lauf = pv.LaufOf(plan, d) + 1;
                    foreach (int dl in cur.CardDeltas())
                    {
                        BlockPreview.Card cc = new BlockPreview.Card(); cc.Room = c.Room; cc.Missing = c.Missing || d.Day + dl < 1;   // N7: Karte vor dem Start rot
                        cc.Day = RelOf(cur, d, plan, dl); cc.Data = new Entry();
                        cc.Data.Buffers = pv.WithLauf(pv.WithCode(cur.BuffersAt(d.Run, prefix, lauf, dl)), lauf, pv.Day1);
                        cards.Add(cc);
                    }
                }
                else
                {
                    if (c.Data.Buffers.Count > 0) pv.WithLauf(c.Data.Buffers, pv.LaufOf(plan, d) + 1, pv.Day1);   // Kopf "Puffer n: Name · für 12340002" wie im Plan
                    cards.Add(c);
                }
            }
            bool run = cur != null && (IsJe(cur) || cur.Kind != StepKind.Normal);
            lPage.Text = ix >= 0 ? (run ? "Lauf " + (curDates[ix].Run + 1) + " von " + StepsView().RunCountOf(cur) : "Termin " + (ix + 1) + " von " + n) : "";   // Lauf = Nummer im Hauptlauf (Ausfaelle/Verschiebungen aendern die Position, nicht die Nummer)
            bPrev.Enabled = ix > 0; bNext.Enabled = ix >= 0 && ix < n - 1;
            blockPreview.Set(cards, PreviewW);
            previewScroll.AutoScrollPosition = Point.Empty;
        }
    }
}
