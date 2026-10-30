using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Schmale Leiste im Paket-Editor (N5): "H1-T-0123 ist nicht in der Equipment-Liste – [Hinzufügen] [Details…] [Ignorieren]" bzw. nach dem
    // Hinzufuegen "… hinzugefügt – [Rückgängig] [OK]". Eine Instanz je Fenster, unterhalb der Kopfzeile "Du bearbeitest gerade".
    class EquipBar : Panel
    {
        readonly Label msg = new Label();
        readonly Button bMain = new Button(), bDetails = new Button(), bClose = new Button();
        public Action Main, Details, Close;
        public bool Confirm;   // true = Rueckmeldung nach dem Hinzufuegen (Hauptknopf = Rueckgaengig)

        public EquipBar()
        {
            Tag = "keep"; Dock = DockStyle.Top; Visible = false; BackColor = Color.FromArgb(255, 247, 220); Padding = new Padding(8, 3, 6, 3);
            msg.AutoEllipsis = true; msg.TextAlign = ContentAlignment.MiddleLeft; msg.UseMnemonic = false;
            bDetails.Text = "Details…"; bClose.Text = "Ignorieren";
            Controls.AddRange(new Control[] { msg, bMain, bDetails, bClose });
            foreach (Button b in new[] { bMain, bDetails, bClose }) { Theme.Current.StyleButton(b); b.TabStop = false; }
            bMain.Click += delegate { if (Main != null) Main(); };
            bDetails.Click += delegate { if (Details != null) Details(); };
            bClose.Click += delegate { if (Close != null) Close(); };
        }

        public string Message { get { return msg.Text; } }
        public string MainText { get { return bMain.Text; } }
        public bool HasDetails { get { return bDetails.Visible; } }

        // Frage: Text, Hauptknopf ("Hinzufügen" / "Admin informieren"), mit/ohne "Details…"
        public void ShowPrompt(string text, string mainText, bool details)
        {
            Confirm = false; msg.Text = text; bMain.Text = mainText; bMain.Visible = true; bDetails.Visible = details; bClose.Text = "Ignorieren";
            Show2();
        }

        // Rueckmeldung mit "Rückgängig" (undo = false: nur Text und OK)
        public void ShowDone(string text, bool undo)
        {
            Confirm = true; msg.Text = text; bMain.Text = "Rückgängig"; bMain.Visible = undo; bDetails.Visible = false; bClose.Text = "OK";
            Show2();
        }

        void Show2() { Visible = true; DoLayout(); }

        public void Hide2() { Visible = false; Confirm = false; }

        protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayout(); }

        void DoLayout()
        {
            int h = Font.Height + 8;
            Height = h + Padding.Vertical;
            int x = ClientSize.Width - Padding.Right;
            foreach (Button b in new[] { bClose, bDetails, bMain })
            {
                if (!b.Visible) continue;
                int w = TextRenderer.MeasureText(b.Text, Font).Width + 24;
                x -= w; b.SetBounds(x, Padding.Top, w, h); x -= 6;
            }
            msg.SetBounds(Padding.Left, Padding.Top, Math.Max(20, x - Padding.Left), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(232, 205, 130))) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

#if TESTHOOKS
        internal void TestClick(int which) { if (which == 0) bMain.PerformClick(); else if (which == 1) bDetails.PerformClick(); else bClose.PerformClick(); }
#endif
    }
}
