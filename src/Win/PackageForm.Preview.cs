using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Teil 3 (Vorschau & Pruefung): Kalenderleiste der Kampagne, der gewaehlte Schritt als Block wie im Plan
    // (Raumname als Kopf, Tag/Datum, Plantext mit Format, Puffer) und die Pruefhinweise (Klick = hinspringen).
    partial class PackageForm
    {
        readonly PlanStrip strip = new PlanStrip();
        readonly Panel stripScroll = new Panel(), previewScroll = new Panel();   // stripScroll = nur der Rahmen um den Kalender
        readonly BlockPreview blockPreview = new BlockPreview();
        readonly ListBox lbHints = new ListBox();
        Label lPreview;
        List<Hint> hintList = new List<Hint>();   // alle Hinweise (Gruppenkoepfe samt Einzelhinweisen)
        List<Hint> shownHints = new List<Hint>(); // Inhalt von lbHints (fuer MeasureItem): zugeklappte Gruppen ohne Einzelhinweise

        class Hint
        {
            public string Text; public PackageStep Step; public Control Field;
            public int Run = -1, Run2 = -1; public PackageStep Step2;   // Beteiligte: Klick springt zum ersten, der zweite wird im Kalender rot umrandet
            public bool Blocks, Indent;                                 // Blocks = Sperre (steht vor den Hinweisen)
            public string Code = "", UnvalId, UnvalShort;               // UnvalId: nicht validiertes Geraet (Klick = zur Liste hinzufuegen)
            public List<Hint> Kids; public bool Open;                   // Gruppenkopf gleicher Regeln (Klick klappt auf)
            public Hint(string t, PackageStep s, Control f) { Text = t; Step = s; Field = f; }
            public override string ToString() { return (Indent ? "    " : "") + Text; }
        }

        Control BuildPreview()
        {
            paneWrap.Tag = "keep"; paneWrap.Dock = DockStyle.Bottom; paneWrap.Padding = new Padding(8, 0, 8, 0);
            InitPane();   // Hoehe (gemerkt), Teiler zwischen Abschnitt 2 und 3 (PackageForm.Pane.cs)
            Section sec = new Section("3   Vorschau & Prüfung", "Kalender wie im Plan; der gewählte Schritt ist blau. Klick wählt, Ziehen verschiebt diesen und alle folgenden Läufe (Umschalt = nur diesen Lauf).", true, 1000);
            sec.Dock = DockStyle.Fill;
            previewScroll.AutoScroll = true; previewScroll.BackColor = Color.White; previewScroll.BorderStyle = BorderStyle.FixedSingle; previewScroll.Tag = "keep";
            blockPreview.Location = Point.Empty; blockPreview.Font = Font; previewScroll.Controls.Add(blockPreview);
            previewScroll.Resize += delegate { blockPreview.Relayout(PreviewW); };
            Panel right = BuildPager(previewScroll, 372);   // Ein Eintrag mit ◀ ▶ (PackageForm.Pager.cs)
            Panel hints = Column(sec, "Prüfhinweise (Klick = hinspringen):", lbHints, 290);
            lbHints.IntegralHeight = false; lbHints.DrawMode = DrawMode.OwnerDrawVariable;   // Hinweise umbrechen statt abschneiden
            lbHints.MeasureItem += delegate(object o, MeasureItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= shownHints.Count) return;   // MeasureItem kommt beim Add, bevor Items[e.Index] existiert
                e.ItemHeight = Math.Min(255, TextRenderer.MeasureText(shownHints[e.Index].ToString(), Font, new Size(Math.Max(20, lbHints.Width - SystemInformation.VerticalScrollBarWidth - 12), 0), TextFormatFlags.WordBreak).Height + 4);
            };
            lbHints.DrawItem += delegate(object o, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= lbHints.Items.Count) return;
                e.DrawBackground();
                bool sel = (e.State & DrawItemState.Selected) != 0;
                Rectangle r = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 2);
                Hint hh = lbHints.Items[e.Index] as Hint;
                TextRenderer.DrawText(e.Graphics, Convert.ToString(lbHints.Items[e.Index]), Font, r, sel ? SystemColors.HighlightText : (hh != null && hh.Blocks ? Color.Firebrick : lbHints.ForeColor), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            };
            // Resize: nur bei echter Breitenaenderung neu umbrechen (RewrapHints, PackageForm.Sizing.cs)
            lbHints.MouseUp += delegate(object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                int ix = lbHints.IndexFromPoint(e.Location);   // SelectedItem wirft bei OwnerDraw-Liste (Klick unter dem Eintrag) eine NullReferenceException
                if (ix >= 0 && ix < lbHints.Items.Count) HintClicked(lbHints.Items[ix] as Hint);
            };
            stripScroll.Dock = DockStyle.Fill; stripScroll.BackColor = Color.White; stripScroll.BorderStyle = BorderStyle.FixedSingle; stripScroll.Tag = "keep";
            strip.Dock = DockStyle.Fill; strip.SetRows(rowNames); strip.ItemClicked += StripClicked;
            strip.ItemMoved += delegate(PlanStrip.Item it, int day, bool onlyRun) { MoveInCalendar(it.Step, it.Run, day, onlyRun); };
            strip.ItemMenu += StripMenu;
            stripScroll.Controls.Add(strip); stripScroll.Controls.Add(BuildStripBar());   // Leiste (oben) zuletzt: wird zuerst angedockt
            sec.Controls.Add(stripScroll); sec.Controls.Add(Spacer()); sec.Controls.Add(hints); sec.Controls.Add(Spacer()); sec.Controls.Add(right);
            paneWrap.Controls.Add(sec);
            return paneWrap;
        }

        int PreviewW { get { return Math.Max(80, previewScroll.ClientSize.Width - (blockPreview.Height > previewScroll.ClientSize.Height ? 0 : 1)); } }

        static Panel Spacer() { Panel p = new Panel(); p.Tag = "keep"; p.Dock = DockStyle.Right; p.Width = 8; return p; }

        Panel Column(Control parent, string title, Control content, int w)
        {
            Panel p = new Panel(); p.Tag = "keep"; p.Dock = DockStyle.Right; p.Width = w;
            Label l = new Label(); l.Text = title; l.Dock = DockStyle.Top; l.Height = Font.Height + 4; l.AutoEllipsis = true;
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content); p.Controls.Add(l);
            parent.Controls.Add(p);
            return p;
        }

        // ---------- Vorschau und Pruefung ----------

        void UpdatePreview(bool scrollTo)
        {
            List<PlanStrip.Item> items = new List<PlanStrip.Item>();
            List<RunDate> plan = Plan();
            foreach (PackageStep s in Ordered())
            {
                int si = steps.IndexOf(s); bool je = IsJe(s), many = StepsView().RunCountOf(s) > 1;
                foreach (RunDate d in plan)
                {
                    if (d.Step != si) continue;
                    PlanStrip.Item it = new PlanStrip.Item(); it.Step = s; it.Day = d.Day; it.Run = d.Run; it.Shifted = !je && s.Pinned(d.Run); it.Je = je;
                    it.Title = TitleOf(s); it.Row = s.Row;
                    it.Sub = s.Numbered ? s.RunNo(prefix, d.Run) : (many ? (je || s.Kind != StepKind.Normal ? "Lauf " : "Termin ") + (d.Run + 1) : "");
                    items.Add(it);
                }
            }
            curDates = cur != null ? plan.FindAll(delegate(RunDate d) { return d.Step == steps.IndexOf(cur); }) : new List<RunDate>();
            curDates.Sort(delegate(RunDate a, RunDate b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.Run.CompareTo(b.Run); });
            if (nextRun >= 0) { curRun = nextRun; nextRun = -1; }
            if (PageIndex() < 0) curRun = curDates.Count > 0 ? curDates[0].Run : -1;
            strip.HeadText = StripHead;
            strip.Set(items, cur, curRun);
            if (scrollTo) strip.ScrollTo(cur, curRun);
            UpdateStripBar();
            lPreview.Text = "So erscheint der Schritt im Plan:";
            tip.SetToolTip(lPreview, pk.Lauf1.HasValue ? "Vorschau mit den Daten ab Lauf 1 am " + DText(pk.Lauf1.Value) + "." : "Vorschau relativ zu Lauf 1; das Datum steht fest, sobald „Lauf 1 am“ im Kopf gesetzt ist oder beim Kampagnenstart.");
            ShowCard();
        }

        void UpdateHints()
        {
            List<Hint> h = new List<Hint>();
            if (tName.Text.Trim().Length == 0) h.Add(new Hint("Das Muster hat noch keinen Namen.", null, tName));
            bool runs = steps.Exists(delegate(PackageStep s) { return s.Kind != StepKind.Normal; });
            if (ProductError() != null) h.Add(new Hint(ProductError(), null, tProduct));
            if (FirstRunError() != null) h.Add(new Hint(FirstRunError(), null, tFirst));
            else if (prefix.Length == 0 && runs) h.Add(new Hint("Erste Laufnummer fehlt – Läufe bekommen keine 8-stellige Nummer.", null, tFirst));
            if (steps.Count == 0 && blocks.Count == 0) h.Add(new Hint("Der Ablauf ist leer – mit „+ Schritt hinzufügen“ beginnen.", null, bAdd));
            List<PackageStep> o = Ordered();
            for (int i = 0; i < o.Count; i++)
            {
                PackageStep s = o[i]; string n = "Schritt " + (i + 1);
                if (s.IsEmpty) { h.Add(new Hint(n + " ist leer und wird nicht gespeichert.", s, tTitle)); continue; }
                if (s.Row.Length == 0) h.Add(new Hint(n + " (" + TitleOf(s) + ") hat keinen Raum – unter „Wo im Plan?“ wählen.", s, cbRow));
                else if (!rowNames.Contains(s.Row)) h.Add(new Hint(n + ": Raum „" + s.Row + "“ gibt es im Plan nicht.", s, cbRow));
                int nRuns = StepsView().RunCountOf(s); bool own = !IsJe(s);
                if (s.Kind == StepKind.SubRun && s.SubNos.Trim().Length == 0) h.Add(new Hint(n + ": Unterlauf-Nummern fehlen.", s, tSubNos));
                else if (s.Kind == StepKind.SubRun && s.SubNos.Split(',').Length != nRuns)
                    h.Add(new Hint(n + ": Unterlauf-Nummern für " + s.SubNos.Split(',').Length + " Lauf/Läufe, aber " + nRuns + " Wiederholung(en) (Komma = nächster Lauf).", s, tSubNos));
                if (own && s.Repeat == RepeatMode.Weekdays && (s.WeekDays & 127) == 0) h.Add(new Hint(n + ": Wiederholung an Wochentagen, aber kein Wochentag gewählt.", s, cbRepeat));
                if (own && s.Repeat == RepeatMode.DayList && PackageStep.ParseDays(s.DayList).Count == 0) h.Add(new Hint(n + ": Termine fehlen (Tage des Ablaufs, z. B. 1, 4, 9).", s, tDays));
                if (s == cur && tManualNo.Text.Trim().Length > 0 && !PackageStep.IsManualNo(tManualNo.Text.Trim())) h.Add(new Hint(n + ": Laufnummer von Hand muss 8 Ziffern haben (z. B. 12340001).", s, tManualNo));
                foreach (string t in s.MissingTanks(cat))
                    h.Add(new Hint(n + ": Tank (" + t + ") nicht gewählt – Gerät wählen oder Ansatz „Flasche/Gebinde“/„Ohne Gebinde“.", s, cbAnsatz));
                foreach (EquipAssign a in s.Equipment)
                    foreach (string id in EquipRow.Unvalidated(a, cat, Product)) h.Add(EquipHint(n, a, id, s));
                Package one = new Package(); one.ProductCode = Product; one.RunPrefix = prefix; one.Steps.Add(s);   // Zugehoerigkeit: sperrt Speichern/Uebernehmen
                foreach (string e in cat.CheckPackage(one)) { Hint fh = new Hint(n + " FEHLER: " + e, s, null); fh.Blocks = true; h.Add(fh); }
                foreach (string e in cat.CheckPackageWarn(one)) h.Add(new Hint(n + ": " + e, s, null));   // Stammdaten "Passt zu": nur Hinweis
            }
            h.AddRange(RuleHints(o));   // Regeln aus Core/Packages.Rules.cs (Sperre = Übernehmen nicht möglich), PackageForm.Hints.cs
            bool ok = h.Count == 0;
            if (ok) h.Add(new Hint("Keine Auffälligkeiten – das Muster kann übernommen werden.", null, null));
            FillHintList(BlocksFirst(h));
            strip.Mark(null, -1);
            lbHints.ForeColor = ok ? Color.DarkGreen : Color.FromArgb(150, 60, 0);
        }
    }

    // Vorschau eines Schritts wie im Plan: je Lauf ein Block mit Raum-Kopf (Tag/Datum rechts), Herstellung und Plantext mit Format
    // (z. B. rote, fette Kennung). Statusfarben der Equipment-Nummern erst im Plan (wie im Bearbeitungsmodus).
    class BlockPreview : Control
    {
        public class Card { public string Room = "", Day = "", Extra = ""; public Entry Data; public bool Missing; }
        List<Card> cards = new List<Card>();
        readonly List<TextBlock> blocks = new List<TextBlock>();
        const int Pad = 6, Gap = 8;

        public event Action<int> Page;   // Pfeiltasten: -1 = voriger, +1 = naechster Eintrag

        public BlockPreview() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true); TabStop = true; }

#if TESTHOOKS
        public int Count { get { return cards.Count; } }
        public void TestKey(Keys k) { OnKeyDown(new KeyEventArgs(k)); }
#endif

        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            bool back = e.KeyCode == Keys.Left || e.KeyCode == Keys.Up, fwd = e.KeyCode == Keys.Right || e.KeyCode == Keys.Down;
            if ((back || fwd) && Page != null) { Page(back ? -1 : 1); e.Handled = true; }
        }

        int HeadH { get { return Font.Height + 8; } }

        int ExtraH(Card c) { return c.Extra.Length == 0 ? 0 : c.Extra.Split('\n').Length * Font.Height + 2; }

        public void Set(List<Card> list, int width) { cards = list; Relayout(width); }

        public void Relayout(int width)
        {
            blocks.Clear();
            int h = Gap / 2, cw = Math.Max(40, width - 2 * Gap - 2 * Pad);
            Font plain = FontCache.Get(Font.Name, Font.SizeInPoints, FontStyle.Regular, Font);
            foreach (Card c in cards)
            {
                TextBlock b = c.Data != null ? TextBlock.Build(Pieces(c.Data, plain), plain, cw) : null;
                blocks.Add(b);
                h += HeadH + ExtraH(c) + (b != null ? b.Height : 0) + 2 * Pad + Gap;
            }
            Size = new Size(Math.Max(40, width), Math.Max(h, 20));
            Invalidate();
        }

        static List<Piece> Pieces(Entry e, Font plain)
        {
            string text = e.Content;
            Color[] fore = new Color[text.Length]; Font[] fnt = new Font[text.Length];
            for (int i = 0; i < text.Length; i++) { fore[i] = Color.Black; fnt[i] = plain; }
            foreach (FormatSpan sp in e.Spans)
            {
                Font f = PlanView.SpanFont(sp, plain);
                for (int i = sp.Start; i < sp.Start + sp.Length && i < text.Length; i++) { if (sp.Rgb != 0) fore[i] = Ui.Rgb(sp.Rgb); fnt[i] = f; }
            }
            foreach (int[] nl in Notes.NoteLines(e))   // Notizzeilen kursiv, wie im Plan
                for (int i = nl[1]; i < nl[1] + nl[2] && i < text.Length; i++) fnt[i] = FontCache.Get(fnt[i].Name, fnt[i].SizeInPoints, fnt[i].Style | FontStyle.Italic, fnt[i]);
            List<Piece> res = new List<Piece>();
            for (int s = 0, i = 1; i <= text.Length; i++)
                if (i == text.Length || fore[i] != fore[s] || fnt[i] != fnt[s]) { res.Add(new Piece(text.Substring(s, i - s), fnt[s], fore[s], false, Color.Empty)); s = i; }
            return res;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current; Graphics g = e.Graphics;
            g.Clear(Color.White);
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            int y = Gap / 2, w = Width - 2 * Gap;
            using (Font bold = new Font(Font, FontStyle.Bold))
                for (int i = 0; i < cards.Count; i++)
                {
                    Card c = cards[i];
                    TextBlock b = blocks[i];
                    int h = HeadH + ExtraH(c) + (b != null ? b.Height : 0) + 2 * Pad;
                    Rectangle head = new Rectangle(Gap, y, w, HeadH);
                    t.FillHead(g, head, t.RowHead);
                    int dw = TextRenderer.MeasureText(c.Day, Font).Width + 8;
                    TextRenderer.DrawText(g, c.Room, bold, new Rectangle(head.X + 6, y, w - dw - 10, HeadH), c.Missing ? Color.Firebrick : t.HeadText, ff);
                    TextRenderer.DrawText(g, c.Day, Font, new Rectangle(head.Right - dw, y, dw - 4, HeadH), t.HeadText, ff | TextFormatFlags.Right);
                    int ty = y + HeadH + Pad;
                    if (c.Extra.Length > 0) { foreach (string line in c.Extra.Split('\n')) { TextRenderer.DrawText(g, line, Font, new Rectangle(Gap + Pad, ty, w - 2 * Pad, Font.Height), t.DoneText, ff); ty += Font.Height; } ty += 2; }
                    if (b != null) b.Draw(g, Gap + Pad, ty);
                    g.DrawRectangle(t.Pen(t.CellBorder, 1), Gap, y, w - 1, h - 1);
                    y += h + Gap;
                }
            if (cards.Count == 0) TextRenderer.DrawText(g, "Kein Schritt gewählt.", Font, new Point(6, 6), Section.HelpColor);
        }
    }
}
