using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Schmale Leiste im Paket-Editor (N5), eine einzige Zeile: "H1-T-0123 ist nicht in der Equipment-Liste – [Hinzufügen] [▾] [Ignorieren]"; das Menue hinter ▾:
    // Alle hinzufügen (n), Mit Angaben hinzufügen…, Ersetzen durch, Rückgängig. Nach dem Hinzufuegen "… hinzugefügt – [Rückgängig] [OK]".
    // Eine Instanz je Fenster, unterhalb der Kopfzeile "Du bearbeitest gerade".
    class EquipBar : Panel
    {
        readonly Label msg = new Label();
        readonly Button bMain = new Button(), bMenu = new Button(), bClose = new Button();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem bAll = new ToolStripMenuItem(), bDetails = new ToolStripMenuItem("Mit Angaben hinzufügen…"), bReplace = new ToolStripMenuItem("Ersetzen durch ▾"), bUndo = new ToolStripMenuItem("Rückgängig");
        readonly ToolTip tt = new ToolTip();
        public Action Main, All, Details, Undo, Close;
        public Action<Control> Replace;
        public bool Confirm;   // true = Rueckmeldung nach dem Hinzufuegen (Hauptknopf = Rueckgaengig)

        public EquipBar()
        {
            Tag = "keep"; Dock = DockStyle.Top; Visible = false; BackColor = Color.FromArgb(255, 247, 220); Padding = new Padding(8, 3, 6, 3);
            msg.AutoEllipsis = true; msg.TextAlign = ContentAlignment.MiddleLeft; msg.UseMnemonic = false;
            bClose.Text = "Ignorieren"; bMenu.Text = "▾";
            Controls.AddRange(new Control[] { msg, bMain, bMenu, bClose });
            foreach (Button b in new[] { bMain, bMenu, bClose }) { Theme.Current.StyleButton(b); b.TabStop = false; }
            menu.Items.AddRange(new ToolStripItem[] { bAll, bDetails, bReplace, bUndo });
            bMain.Click += delegate { if (Main != null) Main(); };
            bMenu.Click += delegate { menu.Show(bMenu, new Point(bMenu.Width, bMenu.Height), ToolStripDropDownDirection.BelowLeft); };
            bAll.Click += delegate { if (All != null) All(); };
            bDetails.Click += delegate { if (Details != null) Details(); };
            bReplace.Click += delegate { if (Replace != null) Replace(bMenu); };
            bUndo.Click += delegate { if (Undo != null) Undo(); };
            bClose.Click += delegate { if (Close != null) Close(); };
            tt.SetToolTip(bMenu, "Weitere Möglichkeiten: alle hinzufügen, mit Angaben, ersetzen durch …");
        }

        public string Message { get { return msg.Text; } }
        public string MainText { get { return bMain.Text; } }
        public bool HasDetails { get { return bDetails.Available; } }
        public bool HasUndo { get { return bUndo.Available; } }
        public string AllText { get { return bAll.Available ? bAll.Text : ""; } }

        // Frage: Text, Hauptknopf ("Hinzufügen" / "Admin informieren"), admin = "Alle hinzufügen" + "Mit Angaben…", allCount >= 2 zeigt "Alle hinzufügen (n)",
        // undo = letztes Geraet ist im Text vermerkt und per "Rückgängig" zurueckzunehmen
        public void ShowPrompt(string text, string mainText, bool admin, int allCount, bool undo)
        {
            Confirm = false; msg.Text = text; bMain.Text = mainText; bMain.Visible = true; bMenu.Visible = true; bDetails.Available = admin; bReplace.Available = true;
            bAll.Available = admin && allCount >= 2; bAll.Text = "Alle hinzufügen (" + allCount + ")";
            bUndo.Available = undo; bClose.Text = "Ignorieren";
            Show2();
        }

        // Rueckmeldung mit "Rückgängig" (undo = false: nur Text und OK)
        public void ShowDone(string text, bool undo)
        {
            Confirm = true; msg.Text = text; bMain.Text = "Rückgängig"; bMain.Visible = undo; bMenu.Visible = false;
            bAll.Available = bDetails.Available = bReplace.Available = bUndo.Available = false; bClose.Text = "OK";
            Show2();
        }

        void Show2() { tt.SetToolTip(msg, msg.Text); Visible = true; DoLayout(); }

        public void Hide2() { Visible = false; Confirm = false; }

        protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayout(); }

        int W(params string[] texts)
        {
            int m = 0;
            foreach (string s in texts) m = Math.Max(m, TextRenderer.MeasureText(s, Font).Width);
            return m + 16;
        }

        // Eine Zeile: Text links (schneidet bei Platzmangel ab, voller Text im Tooltip), Knoepfe rechts: Hinzufuegen, ▾, Ignorieren
        void DoLayout()
        {
            int h = Font.Height + 8;
            int x = ClientSize.Width - Padding.Right, y = Padding.Top;
            int wc = W("Ignorieren", "OK"), wm = W("Admin informieren", "Rückgängig", "Hinzufügen"), wd = W("▾") - 4;
            bClose.SetBounds(x -= wc, y, wc, h); x -= 6;
            if (bMenu.Visible) { bMenu.SetBounds(x -= wd, y, wd, h); x -= 2; }
            if (bMain.Visible) { bMain.SetBounds(x -= wm, y, wm, h); x -= 6; }
            Height = y + h + Padding.Bottom;
            msg.SetBounds(Padding.Left, y, Math.Max(20, x - Padding.Left), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(232, 205, 130))) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

#if TESTHOOKS
        internal void TestClick(int which)
        {
            if (which == 0) bMain.PerformClick(); else if (which == 2) bClose.PerformClick();
            else if (which == 1) bDetails.PerformClick(); else if (which == 3) bAll.PerformClick(); else if (which == 4) bUndo.PerformClick(); else bReplace.PerformClick();
        }
#endif
    }
}
