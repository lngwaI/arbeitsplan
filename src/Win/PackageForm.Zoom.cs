using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Kalender (PlanStrip), Teil 2: Zoom (wie viele Tage im Blickfeld sind), "nur Auswahl", Tooltip je Kaestchen, Schloss.
    partial class PlanStrip
    {
        public static readonly int[] ZoomDays = { 1, 4, 7, 14 };                           // Tage im Blickfeld je Stufe
        public static readonly string[] ZoomNames = { "Tag", "½ Woche", "Woche", "2 Wochen" };
        const int MinDayW = 40;
        int zoom = 1, dayW = 100;
        bool only;
        readonly ToolTip tt = new ToolTip();
        Item hover;
        public Func<Item, string> TipFor;     // Text des Tooltips je Kaestchen
        public event Action ZoomChanged;

        public int Zoom
        {
            get { return zoom; }
            set
            {
                value = Math.Max(0, Math.Min(ZoomDays.Length - 1, value));
                if (value == zoom) return;
                zoom = value; Rebuild(); Invalidate();
                ScrollTo(selected, selRun);
                if (ZoomChanged != null) ZoomChanged();
            }
        }

        // Breite einer Tagesspalte mit Eintraegen (leere bleiben schmal): Platz / Tage der Stufe
        public int DayWidth { get { return dayW; } }
        int CalcDayW() { int avail = Math.Max(MinDayW * 2, ClientSize.Width - rowW - SystemInformation.VerticalScrollBarWidth - 2); return Math.Max(MinDayW, avail / ZoomDays[zoom]); }

        // Nur der gewaehlte Schritt (mit ◀ ▶ im Editor zum vorigen/naechsten Schritt)
        public bool OnlySelected
        {
            get { return only; }
            set { if (only == value) return; only = value; Rebuild(); Invalidate(); sx = 0; ScrollTo(selected, selRun); }
        }

#if TESTHOOKS
        internal List<Item> Items { get { return items; } }
#endif
        public Point ScrollPos { get { return new Point(sx, sy); } set { SetX(value.X); SetY(value.Y); Invalidate(); } }

        static void DrawLock(Graphics g, int x, int y, Color c)
        {
            System.Drawing.Drawing2D.SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(c, 1.6f)) g.DrawArc(p, x + 1.8f, y - 0.5f, 5.4f, 8f, 180, 180);
            using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, x, y + 4, 9, 7);
            g.SmoothingMode = old;
        }

        void ShowTip(Point at)
        {
            string t = hover != null && TipFor != null ? TipFor(hover) : "";
            if (t.Length == 0) tt.Hide(this); else tt.Show(t, this, at.X + 14, at.Y + 20, 12000);
        }

        void TipMove(Point p)
        {
            Item it = ItemAt(p);
            if (it == hover) return;
            hover = it; ShowTip(p);
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != null) { hover = null; tt.Hide(this); } }

        protected override void Dispose(bool disposing) { if (disposing) tt.Dispose(); base.Dispose(disposing); }
    }

    // Leiste ueber dem Kalender: ↶ ↷ (Rueckgaengig/Wiederholen, PackageForm.Undo.cs), Zoom −/+, "nur Auswahl" mit ◀ ▶, Rueckmeldung
    partial class PackageForm
    {
        readonly Panel stripBar = new Panel();
        readonly Button bZoomOut = new Button(), bZoomIn = new Button(), bStepPrev = new Button(), bStepNext = new Button();
        readonly Label lZoom = new Label(), stripMsg = new Label();
        readonly CheckBox chOnly = new CheckBox();
        readonly System.Windows.Forms.Timer msgTimer = new System.Windows.Forms.Timer();
        const string DragHint = "Ziehen: nur dieser Block · Umschalt: dieser und alle folgenden";

        Control BuildStripBar()
        {
            stripBar.Tag = "keep"; stripBar.Dock = DockStyle.Top; stripBar.Height = Font.Height + 12; stripBar.BackColor = Color.White;
            int h = stripBar.Height - 4, x = 2;
            foreach (Button b in new[] { bUndo, bRedo, bZoomOut, bZoomIn, bStepPrev, bStepNext }) { b.TabStop = false; b.Tag = "keep"; b.Font = Font; }
            bUndo.Text = "↶"; bRedo.Text = "↷"; bZoomOut.Text = "−"; bZoomIn.Text = "+"; bStepPrev.Text = "◀"; bStepNext.Text = "▶";
            bUndo.SetBounds(x, 2, 26, h); bRedo.SetBounds(x += 28, 2, 26, h);
            bZoomOut.SetBounds(x += 34, 2, 26, h);
            lZoom.AutoSize = false; lZoom.TextAlign = ContentAlignment.MiddleCenter; lZoom.Tag = "keep"; lZoom.SetBounds(x += 28, 2, TextRenderer.MeasureText("2 Wochen", Font).Width + 14, h);
            bZoomIn.SetBounds(x += lZoom.Width + 2, 2, 26, h);
            chOnly.Text = "nur Auswahl"; chOnly.AutoSize = true; chOnly.Tag = "keep"; chOnly.Location = new Point(x += 32, 2 + (h - chOnly.PreferredSize.Height) / 2);
            bStepPrev.SetBounds(x += chOnly.PreferredSize.Width + 4, 2, 26, h); bStepNext.SetBounds(x += 28, 2, 26, h);
            stripMsg.AutoEllipsis = true; stripMsg.TextAlign = ContentAlignment.MiddleLeft; stripMsg.Tag = "keep"; stripMsg.ForeColor = Color.Firebrick;
            InitBig(); InitRhythm();
            stripMsg.SetBounds(x += 34, 2, 100, h); stripMsg.Anchor = TLR;
            stripBar.Controls.AddRange(new Control[] { bUndo, bRedo, bZoomOut, lZoom, bZoomIn, chOnly, bStepPrev, bStepNext, bBig, bRhythm, stripMsg });
            stripBar.Resize += delegate { LayoutBarEnd(h); };
            LayoutBarEnd(h);
            msgTimer.Interval = 9000; msgTimer.Tick += delegate { msgTimer.Stop(); Say(""); };
            bZoomOut.Click += delegate { strip.Zoom = strip.Zoom + 1; }; bZoomIn.Click += delegate { strip.Zoom = strip.Zoom - 1; };
            strip.ZoomChanged += UpdateStripBar;
            chOnly.CheckedChanged += delegate { strip.OnlySelected = chOnly.Checked; UpdateStripBar(); };
            bStepPrev.Click += delegate { StepNav(-1); }; bStepNext.Click += delegate { StepNav(1); };
            strip.TipFor = StripTip;
            tip.SetToolTip(bZoomOut, "Herauszoomen: mehr Tage im Blickfeld (Tag, ½ Woche, Woche, 2 Wochen)"); tip.SetToolTip(bZoomIn, "Hineinzoomen: weniger Tage im Blickfeld");
            tip.SetToolTip(chOnly, "Kalender zeigt nur den gewählten Schritt"); tip.SetToolTip(bStepPrev, "Voriger Schritt (Reihenfolge wie im Plan)"); tip.SetToolTip(bStepNext, "Nächster Schritt (Reihenfolge wie im Plan)");
            tip.SetToolTip(stripMsg, DragHint);
            InitUndoButtons();
            UpdateStripBar();
            return stripBar;
        }

        // "Groß" und "Puffer-Rhythmus…" hinter ◀ ▶, danach die Rueckmeldung; in schmaler Leiste heisst der zweite Knopf kurz "Rhythmus"
        void LayoutBarEnd(int h)
        {
            int x = bStepNext.Right + 4, bw = TextRenderer.MeasureText("Klein", Font).Width + 14;
            int full = TextRenderer.MeasureText("Puffer-Rhythmus…", Font).Width + 14, shrt = TextRenderer.MeasureText("Rhythmus", Font).Width + 14;
            bool wide = stripBar.ClientSize.Width - (x + bw + 4) >= full + 110;
            bRhythm.Text = wide ? "Puffer-Rhythmus…" : "Rhythmus";
            bBig.SetBounds(x, 2, bw, h); bRhythm.SetBounds(bBig.Right + 4, 2, wide ? full : shrt, h);
            stripMsg.SetBounds(bRhythm.Right + 6, 2, Math.Max(0, stripBar.ClientSize.Width - bRhythm.Right - 10), h);
        }

        // Zustand der Knoepfe: Zoom-Grenzen, ◀ ▶ nur mit "nur Auswahl" und wenn es einen vorigen/naechsten Schritt gibt
        void UpdateStripBar()
        {
            lZoom.Text = PlanStrip.ZoomNames[strip.Zoom];
            bZoomOut.Enabled = strip.Zoom < PlanStrip.ZoomDays.Length - 1; bZoomIn.Enabled = strip.Zoom > 0;
            List<PackageStep> o = Ordered(); int i = o.IndexOf(cur);
            bStepPrev.Enabled = chOnly.Checked && i > 0; bStepNext.Enabled = chOnly.Checked && i >= 0 && i < o.Count - 1;
        }

        void StepNav(int dir)
        {
            List<PackageStep> o = Ordered(); int i = o.IndexOf(cur) + dir;
            if (cur != null && i >= 0 && i < o.Count) Bind(o[i]);
        }

        // Kurze Rueckmeldung in der Leiste (z. B. abgelehntes Verschieben); verschwindet nach einigen Sekunden
        void Say(string msg)
        {
            stripMsg.Text = msg; tip.SetToolTip(stripMsg, msg.Length > 0 ? msg : DragHint);
            msgTimer.Stop(); if (msg.Length > 0) msgTimer.Start();
        }

        // Tooltip eines Kaestchens: voller Titel, Datum/Bezug, Lauf, ggf. "fest verschoben", dazu die Bedienung
        string StripTip(PlanStrip.Item it)
        {
            RunDate d = new RunDate(); d.Step = steps.IndexOf(it.Step); d.Run = it.Run; d.Day = it.Day;
            int n = StepsView().RunCountOf(it.Step);
            bool run = IsJe(it.Step) || it.Step.Kind != StepKind.Normal;
            string t = TitleOf(it.Step) + (it.Sub.Length > 0 && it.Step.Numbered ? " – " + it.Sub : "") + "\n" + RelOf(it.Step, d) + "\n" + (run ? "Lauf " : "Termin ") + (it.Run + 1) + " von " + n;
            Package pv = StepsView();
            if (pv.CardsSplit(it.Step, pv.Day1))   // UX-5c: Karten mit eigenem Versatz
                foreach (int dl in it.Step.CardDeltas()) t += "\n" + pv.CardNames(it.Step, dl) + ": " + CardOffText(it.Step.RunOffset + dl);
            if (it.Shifted) t += "\nSchloss: fest verschoben – Rechtsklick: auf Regel zurücksetzen";
            return t + "\n\n" + (it.Je ? "Ziehen: alle Läufe (folgt dem Hauptlauf)" : DragHint);
        }
    }
}
