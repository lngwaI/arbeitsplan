using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Schmale Leiste im Paket-Editor (N5): "H1-T-0123 ist nicht in der Equipment-Liste – [Hinzufügen] [Alle hinzufügen (n)] [Mit Angaben hinzufügen…] [Ersetzen durch ▾] [Rückgängig] [Ignorieren]" bzw. nach dem
    // Hinzufuegen "… hinzugefügt – [Rückgängig] [OK]". Eine Instanz je Fenster, unterhalb der Kopfzeile "Du bearbeitest gerade".
    class EquipBar : Panel
    {
        readonly Label msg = new Label();
        readonly Button bMain = new Button(), bAll = new Button(), bDetails = new Button(), bReplace = new Button(), bUndo = new Button(), bClose = new Button();
        readonly ToolTip tt = new ToolTip();
        public Action Main, All, Details, Undo, Close;
        public Action<Control> Replace;
        public bool Confirm;   // true = Rueckmeldung nach dem Hinzufuegen (Hauptknopf = Rueckgaengig)
        bool holdAll, holdUndo;   // Platz bleibt reserviert, auch wenn der Knopf gerade fehlt: die anderen Knoepfe wandern nie

        public EquipBar()
        {
            Tag = "keep"; Dock = DockStyle.Top; Visible = false; BackColor = Color.FromArgb(255, 247, 220); Padding = new Padding(8, 3, 6, 3);
            msg.AutoEllipsis = true; msg.TextAlign = ContentAlignment.MiddleLeft; msg.UseMnemonic = false;
            bDetails.Text = "Mit Angaben hinzufügen…"; bClose.Text = "Ignorieren"; bReplace.Text = "Ersetzen durch ▾"; bUndo.Text = "Rückgängig";
            Controls.AddRange(new Control[] { msg, bMain, bAll, bDetails, bReplace, bUndo, bClose });
            foreach (Button b in new[] { bMain, bAll, bDetails, bReplace, bUndo, bClose }) { Theme.Current.StyleButton(b); b.TabStop = false; }
            bMain.Click += delegate { if (Main != null) Main(); };
            bAll.Click += delegate { if (All != null) All(); };
            bDetails.Click += delegate { if (Details != null) Details(); };
            bReplace.Click += delegate { if (Replace != null) Replace(bReplace); };
            bUndo.Click += delegate { if (Undo != null) Undo(); };
            bClose.Click += delegate { if (Close != null) Close(); };
        }

        public string Message { get { return msg.Text; } }
        public string MainText { get { return bMain.Text; } }
        public bool HasDetails { get { return bDetails.Visible; } }
        public bool HasUndo { get { return bUndo.Visible; } }
        public string AllText { get { return bAll.Visible ? bAll.Text : ""; } }

        // Frage: Text, Hauptknopf ("Hinzufügen" / "Admin informieren"), admin = "Alle hinzufügen" + "Mit Angaben…", allCount >= 2 zeigt "Alle hinzufügen (n)",
        // undo = letztes Geraet ist im Text vermerkt und per "Rückgängig" zurueckzunehmen
        public void ShowPrompt(string text, string mainText, bool admin, int allCount, bool undo)
        {
            Confirm = false; msg.Text = text; bMain.Text = mainText; bMain.Visible = true; bDetails.Visible = admin; bReplace.Visible = true;
            holdAll = admin; bAll.Visible = admin && allCount >= 2; bAll.Text = "Alle hinzufügen (" + allCount + ")";
            holdUndo = true; bUndo.Visible = undo; bClose.Text = "Ignorieren";
            Show2();
        }

        // Rueckmeldung mit "Rückgängig" (undo = false: nur Text und OK)
        public void ShowDone(string text, bool undo)
        {
            Confirm = true; msg.Text = text; bMain.Text = "Rückgängig"; bMain.Visible = undo; bAll.Visible = bDetails.Visible = bReplace.Visible = bUndo.Visible = false;
            holdAll = holdUndo = false; bClose.Text = "OK";
            Show2();
        }

        void Show2() { tt.SetToolTip(msg, msg.Text); Visible = true; DoLayout(); }

        public void Hide2() { Visible = false; Confirm = false; }

        protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayout(); }

        int W(Button b, params string[] texts)
        {
            int m = 0;
            foreach (string s in texts) m = Math.Max(m, TextRenderer.MeasureText(s, Font).Width);
            return m + 16;
        }

        // Oben der Text ueber die ganze Breite, darunter die Knoepfe von rechts mit festen Breiten (Hauptknopf ganz rechts); reicht die Breite nicht, folgt eine weitere Zeile
        void DoLayout()
        {
            int h = Font.Height + 8, mh = Font.Height + 4, top = Padding.Top + mh + 2;
            int x = ClientSize.Width - Padding.Right, y = top;
            Button[] bs = { bMain, bAll, bDetails, bReplace, bUndo, bClose };
            int[] ws = { W(bMain, "Admin informieren", "Rückgängig"), W(bAll, "Alle hinzufügen (99)"), W(bDetails, "Mit Angaben hinzufügen…"), W(bReplace, "Ersetzen durch ▾"), W(bUndo, "Rückgängig"), W(bClose, "Ignorieren", "OK") };
            for (int i = 0; i < bs.Length; i++)
            {
                bool held = (bs[i] == bUndo && holdUndo) || (bs[i] == bAll && holdAll);
                if (!bs[i].Visible && !held) continue;
                if (x - ws[i] < Padding.Left && x < ClientSize.Width - Padding.Right) { x = ClientSize.Width - Padding.Right; y += h + 4; }
                x -= ws[i]; bs[i].SetBounds(x, y, ws[i], h); x -= 6;
            }
            Height = y + h + Padding.Bottom;
            msg.SetBounds(Padding.Left, Padding.Top, Math.Max(20, ClientSize.Width - Padding.Horizontal), mh);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(232, 205, 130))) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

#if TESTHOOKS
        internal void TestClick(int which) { Button[] b = { bMain, bDetails, bClose, bAll, bUndo, bReplace }; b[which].PerformClick(); }
#endif
    }
}
