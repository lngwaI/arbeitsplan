using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Hinweis beim Bearbeiten eines Blocks, den ein anderer gerade bearbeitet (Soft-Lock): trotzdem bearbeiten, Bearbeitung uebernehmen
    // (nur mit Recht TakeOver) oder abbrechen.
    class LockedForm : Form
    {
        public enum Choice { Continue, TakeOver, Cancel }
        public Choice Result = Choice.Cancel;
        readonly Button bGo = new Button(), bTake = new Button(), bCancel = new Button();

        public LockedForm(string title, DateTime day, string holder, bool mayTakeOver)
        {
            Text = "Arbeitsplan";
            Ui.Dialog(this, 520, 150, false);
            // Layout nach Texthöhe: Meldung, Hinweis, darunter die Knöpfe
            string msg = "Das Fenster " + title + " am " + Ui.DayText(day) + " wird gerade von " + holder + " bearbeitet.";
            string hint = mayTakeOver ? "Trotzdem bearbeiten: beide bearbeiten, Speichern fragt bei Überschneidung nach." : "Das Recht \"Bearbeitung übernehmen\" fehlt.";
            const TextFormatFlags wb = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            int mh = Math.Max(20, TextRenderer.MeasureText(msg, Font, new Size(488, 0), wb).Height + 4);
            int hh = Math.Max(18, TextRenderer.MeasureText(hint, Font, new Size(488, 0), wb).Height + 4);
            Label l = new Label(); l.SetBounds(16, 16, 488, mh); l.Text = msg;
            Label h = new Label(); h.Text = hint; h.ForeColor = SystemColors.GrayText; h.SetBounds(16, 16 + mh + 6, 488, hh);
            int by = 16 + mh + 6 + hh + 14;
            ClientSize = new Size(520, by + 28 + 14);
            bGo.Text = "Trotzdem bearbeiten"; bGo.SetBounds(16, by, 150, 28); bGo.Click += delegate { Result = Choice.Continue; DialogResult = DialogResult.OK; };
            bTake.Text = "Bearbeitung übernehmen…"; bTake.SetBounds(174, by, 170, 28); bTake.Enabled = mayTakeOver;
            bTake.Click += delegate { Result = Choice.TakeOver; DialogResult = DialogResult.OK; };
            bCancel.Text = "Abbrechen"; bCancel.SetBounds(404, by, 100, 28); bCancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { l, h, bGo, bTake, bCancel });
            AcceptButton = bGo; CancelButton = bCancel; ActiveControl = bGo;
            Theme.Current.Style(this);
        }

#if TESTHOOKS
        public bool CanTakeOver { get { return bTake.Enabled; } }
#endif
    }

    // Zweiter Schritt der Uebernahme: Warnung, optionaler Kommentar "Warum?" (hoechstens 300 Zeichen), Bestaetigung.
    class TakeOverForm : Form
    {
        public const int MaxReason = 300;
        readonly TextBox why = new TextBox();
        public string Reason { get { return why.Text.Trim(); } }

        public TakeOverForm(string title, DateTime day, string holder)
        {
            Text = "Bearbeitung übernehmen";
            Ui.Dialog(this, 520, 270, false);
            Label l = new Label(); l.SetBounds(16, 14, 488, 64);
            l.Text = holder + " wird aus dem Bearbeitungsmodus von " + title + " am " + Ui.DayText(day) + " ausgeschlossen. Nicht gespeicherte Änderungen von "
                + holder + " gehen verloren. " + holder + " bekommt eine Benachrichtigung.";
            Label w = new Label(); w.Text = "Warum? (optional, wird " + holder + " angezeigt)"; w.AutoSize = true; w.Location = new Point(16, 86);
            why.Multiline = true; why.AcceptsReturn = true; why.MaxLength = MaxReason; why.ScrollBars = ScrollBars.Vertical; why.SetBounds(16, 106, 488, 98);
            Ui.SimpleMenu(why);
            Label cnt = new Label(); cnt.AutoSize = true; cnt.ForeColor = SystemColors.GrayText; cnt.Location = new Point(16, 208);
            why.TextChanged += delegate { cnt.Text = why.TextLength + " / " + MaxReason; };
            cnt.Text = "0 / " + MaxReason;
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(284, 232, 110, 28); ok.DialogResult = DialogResult.OK;
            Button no = new Button(); no.Text = "Abbrechen"; no.SetBounds(404, 232, 100, 28); no.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { l, w, why, cnt, ok, no });
            AcceptButton = null; CancelButton = no; ActiveControl = why;
            Theme.Current.Style(this);
        }

#if TESTHOOKS
        public void TestReason(string r) { why.Text = r; }
#endif
    }
}
