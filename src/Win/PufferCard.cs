using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Karte eines Puffers im Paket-Editor (UX-1b): zugeklappt eine Zeile ("Puffer 2: Name · mobil · Ansatztank X"), aufgeklappt Name, Typ
    // und die Equipment-Zeilen dieses Puffers (Gerät aus der nach Produkt gefilterten Liste oder "nicht benötigt"). Gemeinsame Zeilen
    // (z. B. WFI-Tank) stehen in der Karte des ersten betroffenen Puffers mit "gilt für"; die übrigen zeigen sie nur an.
    partial class PufferCard : Panel
    {
        public const string ShareShort = "WFI";   // Position, die als gemeinsame Zeile für mehrere Puffer gelten kann

        class Row
        {
            public EquipAssign A; public EquipRow R; public Label Info, ScopeLbl; public Button ScopeBtn;
        }

        PackageStep step; Catalog cat; string product;
        readonly Action changed, rebuild, structure; readonly Action<int> toggled, removeMe;   // structure: Zeilen der Karte geaendert (Anzahl/Reihenfolge), ohne alles neu aufzubauen
        readonly RowPool pool;   // Zeilen dieser Karte: freie bleiben versteckt in der Karte
        public int Index;
        public bool Open, Bad;
        public bool ShowTiming;   // UX-5c: Zeile "Zeitpunkt" (nur Schritt "zu jedem Lauf")
        readonly Button bToggle = new Button(), bRemove = new Button(), bPos = new Button();
        readonly Label lSum = new Label(), lTitle = new Label(), lNone = new Label(), lTime = new Label(), lTimeInfo = new Label();
        readonly ComboBox cbTime = new ComboBox(), cbTimeDir = new ComboBox();
        readonly NumericUpDown nTime = new NumericUpDown();
        readonly TextBox tName = new TextBox();
        readonly ComboBox cbTyp = new ComboBox();
        readonly Label[] heads = new Label[5];
        readonly List<Row> rows = new List<Row>();
        readonly ToolTip tip = new ToolTip();
        readonly RowReorder reorder;   // Zeilen umsortieren (UX-5b): Griff, ▲▼
        bool loading;
        Font bold, boldFor;

        PufferSpec Spec { get { return step.Puffers[Index]; } }

        // Die Karte wird einmal gebaut und mit Bind auf einen Puffer eines Schritts gesetzt (Fensterhandles anlegen ist teuer, UX-5e)
        public PufferCard(Action changed, Action rebuild, Action structure, Action<int> toggled, Action<int> removeMe)
        {
            this.changed = changed; this.rebuild = rebuild; this.structure = structure; this.toggled = toggled; this.removeMe = removeMe; pool = new RowPool(this);
            reorder = new RowReorder(this, delegate { return rows.Count; }, SlotBounds, delegate(int f, int t) { MoveRow(f, t); });
            reorder.Lo = delegate(int f) { return f < Spec.Lines.Count ? 0 : Spec.Lines.Count; };   // eigene Zeilen bleiben oben, gemeinsame darunter
            reorder.Hi = delegate(int f) { return f < Spec.Lines.Count ? Spec.Lines.Count - 1 : rows.Count - 1; };
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(247, 248, 250);
            bToggle.Text = "▸"; bToggle.Click += delegate { toggled(Index); };
            lSum.AutoEllipsis = true; lSum.TextAlign = ContentAlignment.MiddleLeft; lSum.UseMnemonic = false; lSum.Cursor = Cursors.Hand; lSum.Click += delegate { toggled(Index); };
            lTitle.TextAlign = ContentAlignment.MiddleLeft; lTitle.UseMnemonic = false;
            cbTyp.DropDownStyle = ComboBoxStyle.DropDownList; cbTyp.Items.AddRange(new object[] { "mobil", "stationär" });
            bRemove.Text = "Entfernen"; bRemove.Click += delegate { removeMe(Index); };
            bPos.Text = "+ Position ▾"; bPos.Click += delegate { ShowPosMenu(); };
            lNone.Text = "Keine Position – mit „+ Position“ ergänzen."; lNone.ForeColor = Section.HelpColor; lNone.TextAlign = ContentAlignment.MiddleLeft; lNone.AutoEllipsis = true;
            Controls.AddRange(new Control[] { bToggle, lSum, lTitle, tName, cbTyp, bRemove, bPos, lNone });
            lTime.Text = "Zeitpunkt:"; lTime.TextAlign = ContentAlignment.MiddleLeft; lTime.UseMnemonic = false;
            lTimeInfo.TextAlign = ContentAlignment.MiddleLeft; lTimeInfo.UseMnemonic = false; lTimeInfo.AutoEllipsis = true; lTimeInfo.ForeColor = Section.HelpColor;
            cbTime.DropDownStyle = ComboBoxStyle.DropDownList; cbTime.Items.AddRange(new object[] { "wie Schritt", "eigener Versatz" });
            cbTimeDir.DropDownStyle = ComboBoxStyle.DropDownList; cbTimeDir.Items.AddRange(new object[] { "Tag(e) vor dem Lauf", "am Tag des Laufs", "Tag(e) nach dem Lauf" });
            nTime.Minimum = 1; nTime.Maximum = PackageStep.MaxCardOffset; nTime.Value = 1;
            Controls.AddRange(new Control[] { lTime, lTimeInfo, cbTime, cbTimeDir, nTime });
            tip.SetToolTip(cbTime, "Wann dieser Puffer bereitgestellt wird: wie der Schritt (gemeinsamer Abstand) oder mit eigenem Versatz zum Lauf, z. B. Puffer 1 zwei Tage, Puffer 2 einen Tag vor dem Lauf.");
            tip.SetToolTip(nTime, "Tage zum Lauf (1–" + PackageStep.MaxCardOffset + ")");
            string[] hd = { "Position", "Gerät", "abwechselnd", "Ausweich-Gerät", "Notiz" };
            for (int i = 0; i < 5; i++) { heads[i] = new Label(); heads[i].Text = hd[i]; heads[i].AutoEllipsis = true; heads[i].UseMnemonic = false; Controls.Add(heads[i]); }
            foreach (Button b in new[] { bToggle, bRemove, bPos }) Theme.Current.StyleButton(b);
            Ui.SimpleMenu(tName);
            tip.SetToolTip(bToggle, "Karte auf- oder zuklappen");
            tip.SetToolTip(tName, "Name des Puffers im Plan. Leer = Titel des Schritts (ab Puffer 2 mit Nummer).");
            tip.SetToolTip(bPos, "Weitere Position (z. B. zweiter Tank) zu diesem Puffer");
            tName.TextChanged += delegate { if (!loading) { Spec.Name = tName.Text.Replace('|', '/').Trim(); changed(); } };
            cbTyp.SelectedIndexChanged += delegate { if (!loading) { Spec.Typ = cbTyp.SelectedIndex == 1 ? "stationaer" : "mobil"; changed(); } };
            cbTime.SelectedIndexChanged += delegate { TimeChanged(); };
            cbTimeDir.SelectedIndexChanged += delegate { TimeChanged(); };
            nTime.ValueChanged += delegate { TimeChanged(); };
        }

        // Karte auf Puffer index von step setzen: Felder und Zeilen aus dem Modell (Zeilen kommen aus dem Pool), Steuerelemente bleiben
        public void Bind(PackageStep step, int index, Catalog cat, string product)
        {
            ReleaseRows();
            this.step = step; Index = index; this.cat = cat; this.product = product;
            Bad = false; Open = false; ShowTiming = false;
            loading = true;
            lTitle.Text = "Puffer " + (index + 1) + ":";
            bRemove.Enabled = step.Puffers.Count > 1;
            tip.SetToolTip(bRemove, step.Puffers.Count > 1 ? "Diesen Puffer entfernen (gemeinsame Zeilen werden angepasst)" : "Ein Puffer-Schritt braucht mindestens einen Puffer – sonst den Schritt entfernen");
            tName.Text = Spec.Name; cbTyp.SelectedIndex = Spec.Typ == "stationaer" ? 1 : 0;
            int off = Spec.OffsetDays.HasValue ? Spec.OffsetDays.Value : 0;
            cbTime.SelectedIndex = Spec.OffsetDays.HasValue ? 1 : 0; cbTimeDir.SelectedIndex = off < 0 ? 0 : off == 0 ? 1 : 2; nTime.Value = Math.Max(1, Math.Min(PackageStep.MaxCardOffset, Math.Abs(off)));
            foreach (EquipAssign a in Spec.Lines) AddRow(a, null);
            foreach (PufferShare g in step.PufferShared) if (g.Covers(Index)) AddRow(g.Eq, g);
            loading = false;
            Invalidate();
        }

        // Alle Zeilen zurueck in den Pool (Karte wird freigegeben oder neu gebunden)
        public void ReleaseRows() { foreach (Row r in rows) DropRow(r); rows.Clear(); reorder.Clear(); }

        // "2 Tage vor dem Lauf", "am Tag des Laufs", "1 Tag nach dem Lauf"
        public static string OffText(int off) { return off == 0 ? "am Tag des Laufs" : Math.Abs(off) + (Math.Abs(off) == 1 ? " Tag" : " Tage") + (off < 0 ? " vor" : " nach") + " dem Lauf"; }

        // Zeitpunkt der Karte: null = wie der Schritt, sonst Versatz zum Lauf (negativ = davor)
        void TimeChanged()
        {
            if (loading) return;
            if (cbTime.SelectedIndex == 0) Spec.OffsetDays = null;
            else if (!Spec.OffsetDays.HasValue)   // Start mit dem Versatz des Schritts
            {
                int o = Math.Max(-PackageStep.MaxCardOffset, Math.Min(PackageStep.MaxCardOffset, step.RunOffset)); Spec.OffsetDays = o;
                loading = true; cbTimeDir.SelectedIndex = o < 0 ? 0 : o == 0 ? 1 : 2; nTime.Value = Math.Max(1, Math.Min(PackageStep.MaxCardOffset, Math.Abs(o))); loading = false;
            }
            else { int n = (int)nTime.Value, dir = cbTimeDir.SelectedIndex; Spec.OffsetDays = dir == 0 ? -n : dir == 1 ? 0 : n; }
            PlaceTiming(); changed();
        }

        // Felder der Zeile "Zeitpunkt" je nach Auswahl ein-/ausblenden (Ort y, Breite iw)
        int timeY, timeW;

        void PlaceTiming()
        {
            bool on = Open && ShowTiming, own = on && cbTime.SelectedIndex == 1, num = own && cbTimeDir.SelectedIndex != 1;
            foreach (Control c in new Control[] { lTime, cbTime }) c.Visible = on;
            cbTimeDir.Visible = own; nTime.Visible = num; lTimeInfo.Visible = on && !own;
            if (!on) return;
            int ih = Math.Max(24, Font.Height + 8), lw = TextRenderer.MeasureText(lTime.Text, Font).Width + 8;
            lTime.SetBounds(8, timeY, lw, ih);
            cbTime.SetBounds(8 + lw, timeY + (ih - cbTime.Height) / 2, 130, cbTime.Height);
            int x = cbTime.Right + 8;
            if (num) { nTime.SetBounds(x, timeY + (ih - nTime.Height) / 2, 52, nTime.Height); x = nTime.Right + 6; }
            cbTimeDir.SetBounds(x, timeY + (ih - cbTimeDir.Height) / 2, Math.Max(80, Math.Min(150, timeW - x + 8)), cbTimeDir.Height);
            lTimeInfo.SetBounds(x, timeY, Math.Max(10, timeW - x + 8), ih);
            lTimeInfo.Text = "(= " + OffText(step.RunOffset) + ")"; tip.SetToolTip(lTimeInfo, lTimeInfo.Text);
        }

        static string TypText(string t) { return t == "stationaer" ? "stationär" : t; }

        string PufferName(int k) { return "Puffer " + (k + 1); }

        // Zugeklappte Zeile: "Puffer 2: Name · mobil · Ansatztank AT-1 · Vorlagetank VT-1"
        public string Summary()
        {
            List<BufferItem> bi = step.BuffersFor(0, "");
            string nm = Index < bi.Count && bi[Index].Name.Length > 0 ? bi[Index].Name : "–";
            List<string> p = new List<string>();
            p.Add(PufferName(Index) + ": " + nm); p.Add(TypText(Spec.Typ));
            if (ShowTiming && Spec.OffsetDays.HasValue) p.Add(OffText(Spec.OffsetDays.Value));
            foreach (EquipChoice c in step.PufferChoices(Index, 0)) { EquipPosition ep = cat.Position(c.Short); p.Add((ep != null ? ep.Name : c.Short) + " " + c.Id); }
            return string.Join(" · ", p.ToArray());
        }

        // "nur dieser Puffer" / "gilt auch für Puffer 2" / "alle Puffer" (aus Sicht dieser Karte)
        string ScopeText(EquipAssign a)
        {
            PufferShare g = step.ShareOf(a);
            if (g == null) return "nur dieser Puffer";
            int m = step.MaskOf(g);
            if (m == step.AllMask && step.Puffers.Count > 2) return "alle Puffer";
            List<string> o = new List<string>();
            for (int k = 0; k < step.Puffers.Count; k++) if (k != Index && ((m >> k) & 1) != 0) o.Add((k + 1).ToString());
            return o.Count == 0 ? "nur dieser Puffer" : "gilt auch für Puffer " + string.Join(", ", o.ToArray());
        }

        void ShowScope(EquipAssign a, Button anchor)
        {
            PufferShare g = step.ShareOf(a);
            int mask = g != null ? step.MaskOf(g) : 1 << Index;
            ContextMenuStrip m = new ContextMenuStrip();
            for (int k = 0; k < step.Puffers.Count; k++)
            {
                ToolStripMenuItem it = new ToolStripMenuItem(PufferName(k) + (step.Puffers[k].Name.Length > 0 ? ": " + step.Puffers[k].Name : ""));
                it.CheckOnClick = true; it.Checked = ((mask >> k) & 1) != 0; m.Items.Add(it);
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Menü schließen = übernehmen").Enabled = false;
            m.Closing += delegate(object o, ToolStripDropDownClosingEventArgs e) { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };
            m.Closed += delegate
            {
                int nm = 0;
                for (int k = 0; k < step.Puffers.Count; k++) if (((ToolStripMenuItem)m.Items[k]).Checked) nm |= 1 << k;
                if (nm != 0 && nm != mask) BeginInvoke(new Action(delegate { SetScope(a, nm); }));
            };
            m.Show(anchor, new Point(0, anchor.Height));
        }

        public void SetScope(EquipAssign a, int mask) { if (step.SetScope(a, mask)) rebuild(); }

        void ShowPosMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            List<string> shorts = new List<string>();
            Baustein b = cat.Baustein_(Catalog.StandardPufferName);
            if (b != null) shorts.AddRange(b.Positions);
            foreach (EquipAssign a in Spec.Lines) if (!shorts.Contains(a.Short)) shorts.Add(a.Short);
            foreach (string sh in shorts)
            {
                string s = sh; EquipPosition ep = cat.Position(sh);
                m.Items.Add((ep != null ? ep.Name + "  (" + sh + ")" : sh), null, delegate { AddLine(s); });
            }
            m.Show(bPos, new Point(0, bPos.Height));
        }

        // Texte und Zustand aus dem Modell auffrischen und alles anordnen; liefert die Hoehe der Karte
        public int Arrange(int w, bool wide)
        {
            int ih = Math.Max(24, Font.Height + 8), iw = w - 16, y = 6;
            Width = w;
            if (boldFor == null || boldFor.Name != Font.Name || boldFor.Size != Font.Size)   // Schrift kommt erst vom Elternteil
            {
                if (bold != null) bold.Dispose();
                bold = new Font(Font, FontStyle.Bold); boldFor = Font;
                lTitle.Font = bold; foreach (Label l in heads) l.Font = bold;
            }
            bToggle.Text = Open ? "▾" : "▸";
            bToggle.SetBounds(6, y, 26, ih);
            int rw = 84;
            bRemove.SetBounds(w - 6 - rw, y, rw, ih);
            lSum.Visible = !Open; lTitle.Visible = tName.Visible = cbTyp.Visible = Open;
            lSum.Text = Summary(); tip.SetToolTip(lSum, lSum.Text);
            if (!Open) lSum.SetBounds(36, y, Math.Max(10, bRemove.Left - 6 - 36), ih);
            else
            {
                lTitle.SetBounds(36, y, TextRenderer.MeasureText(lTitle.Text, lTitle.Font).Width + 6, ih);
                cbTyp.SetBounds(bRemove.Left - 6 - 96, y + (ih - cbTyp.Height) / 2, 96, cbTyp.Height);
                tName.SetBounds(lTitle.Right + 2, y + (ih - tName.Height) / 2, Math.Max(40, cbTyp.Left - 6 - lTitle.Right - 2), tName.Height);
            }
            y += ih + 4;
            timeY = y; timeW = iw; PlaceTiming();
            if (Open && ShowTiming) y += ih + 4;
            bool any = rows.Count > 0;   // Sichtbarkeit je Steuerelement einmal setzen (erst aus- und wieder einblenden flackert)
            bPos.Visible = Open; lNone.Visible = Open && !any;
            for (int i = 0; i < 5; i++) heads[i].Visible = Open && any && (i < 2 || i == 4 || wide);
            foreach (Row r in rows) { if (r.R != null) r.R.Visible = Open; if (r.Info != null) r.Info.Visible = Open; if (r.ScopeBtn != null) r.ScopeLbl.Visible = r.ScopeBtn.Visible = Open; }
            if (!Open) return Height = y + 2;

            int[] x = EquipRow.Cols(iw, wide);
            if (rows.Count > 0)
            {
                for (int i = 0; i < 5; i++) heads[i].SetBounds(8 + x[i], y, Math.Max(0, x[i + 1] - x[i] - 6), Font.Height + 2);
                y += Font.Height + 4;
            }
            else { lNone.SetBounds(8, y, iw, ih); y += ih + 2; }
            for (int ri = 0; ri < rows.Count; ri++)
            {
                Row r = rows[ri];
                if (r.R != null) { r.R.SetOrder(ri, reorder.CanStep(ri, -1), reorder.CanStep(ri, 1)); r.R.Arrange(iw, wide); r.R.Location = new Point(8, y); r.R.TabIndex = 10 + ri; y += r.R.Height; }
                else
                {
                    PufferShare g = step.ShareOf(r.A); EquipChoice ch = r.A.ChoiceFor(0);
                    r.Info.Text = ShareName(r.A) + ": " + (ch != null ? ch.Id : "–") + "   – gemeinsam mit " + PufferName(g != null ? step.OwnerOf(g) : 0) + " (dort ändern)";
                    tip.SetToolTip(r.Info, r.Info.Text);
                    r.Info.SetBounds(8, y, iw, ih); y += ih + 2;
                }
                if (r.ScopeBtn != null)
                {
                    r.ScopeBtn.Text = ScopeText(r.A) + " ▾";
                    r.ScopeLbl.SetBounds(8, y, Math.Max(40, x[1] - 6), ih); r.ScopeBtn.SetBounds(8 + x[1], y, Math.Min(Math.Max(120, iw - x[1]), 230), ih);
                    y += ih + 4;
                }
            }
            bPos.SetBounds(8, y, 130, ih); y += ih + 6;
            return Height = y;
        }

        string ShareName(EquipAssign a) { EquipPosition ep = cat.Position(a.Short); return ep != null ? ep.Name : a.Short; }

        protected override void Dispose(bool disposing) { if (disposing) { reorder.Dispose(); tip.Dispose(); if (bold != null) bold.Dispose(); } base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Color c = Bad ? Color.Firebrick : Color.FromArgb(196, 202, 212);
            using (Pen p = new Pen(c, Bad ? 2 : 1)) g.DrawRectangle(p, Bad ? 1 : 0, Bad ? 1 : 0, Width - (Bad ? 3 : 1), Height - (Bad ? 3 : 1));
        }

#if TESTHOOKS
        internal ComboBox TestTime { get { return cbTime; } }
        internal ComboBox TestDir { get { return cbTimeDir; } }
        internal NumericUpDown TestNum { get { return nTime; } }
        internal TextBox TestName { get { return tName; } }
        internal ComboBox TestTyp { get { return cbTyp; } }
        internal List<EquipRow> TestRows { get { List<EquipRow> l = new List<EquipRow>(); foreach (Row r in rows) if (r.R != null) l.Add(r.R); return l; } }
        internal int TestInfoRows { get { int n = 0; foreach (Row r in rows) if (r.Info != null) n++; return n; } }
        internal RowReorder TestReorder { get { return reorder; } }
        internal Rectangle TestSlot(int i) { return SlotBounds(i); }
        internal string TestSummary { get { return lSum.Text; } }
        internal Button TestRemove { get { return bRemove; } }
        internal Button TestToggle { get { return bToggle; } }
        internal string TestScopeOf(EquipAssign a) { return ScopeText(a); }
#endif
    }
}
