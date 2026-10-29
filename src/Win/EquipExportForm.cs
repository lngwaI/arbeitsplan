using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Woche -> "Equipment-Liste exportieren…": erkannte Equipment-Nummern von Tag / Woche / Zeitraum, jede einmal, getrennt durch Leerzeilen.
    class EquipExportForm : Form
    {
        readonly IDataStore store;
        readonly RadioButton rDay = new RadioButton(), rWeek = new RadioButton(), rRange = new RadioButton();
        readonly DateTimePicker dDay = new DateTimePicker(), dFrom = new DateTimePicker(), dTo = new DateTimePicker();
        readonly TextBox preview = new TextBox();
        readonly Label count = new Label();
        readonly DateTime weekStart;
        string text = "";

        public EquipExportForm(IDataStore store, DateTime weekStart, DateTime? selectedDay)
        {
            this.store = store; this.weekStart = weekStart.Date;
            Text = "Equipment-Liste exportieren";
            Ui.Dialog(this, 460, 440, false);
            Theme th = Theme.Current;

            rDay.Text = "Tag"; rDay.AutoSize = true; rDay.Location = new Point(14, 14);
            rWeek.Text = "Woche"; rWeek.AutoSize = true; rWeek.Location = new Point(14, 42);
            rRange.Text = "Zeitraum"; rRange.AutoSize = true; rRange.Location = new Point(14, 70);
            Label bis = new Label(); bis.Text = "bis"; bis.AutoSize = true; bis.Location = new Point(250, 73);
            foreach (DateTimePicker d in new[] { dDay, dFrom, dTo }) { d.Format = DateTimePickerFormat.Short; d.Width = 110; }
            dDay.Location = new Point(110, 11); dFrom.Location = new Point(110, 68); dTo.Location = new Point(280, 68);
            DateTime day = selectedDay.HasValue ? selectedDay.Value.Date : this.weekStart;
            dDay.Value = day; dFrom.Value = this.weekStart; dTo.Value = this.weekStart.AddDays(6);
            Label wk = new Label(); wk.Text = this.weekStart.ToString("dd.MM.yyyy") + " – " + this.weekStart.AddDays(6).ToString("dd.MM.yyyy");
            wk.AutoSize = true; wk.Location = new Point(110, 44); wk.ForeColor = th.HeadText;
            if (selectedDay.HasValue) rDay.Checked = true; else rWeek.Checked = true;
            EventHandler upd = delegate { Update_(); };
            rDay.CheckedChanged += upd; rWeek.CheckedChanged += upd; rRange.CheckedChanged += upd;
            dDay.ValueChanged += upd; dFrom.ValueChanged += upd; dTo.ValueChanged += upd;

            count.AutoSize = true; count.Location = new Point(14, 104); count.Font = new Font(Font, FontStyle.Bold);
            preview.Multiline = true; preview.ReadOnly = true; preview.ScrollBars = ScrollBars.Vertical; preview.BackColor = th.Window;
            preview.SetBounds(14, 128, 430, 238);

            Button clip = new Button(); clip.Text = "In Zwischenablage"; clip.SetBounds(14, 374, 130, 28);
            Button save = new Button(); save.Text = "Als .txt speichern…"; save.SetBounds(150, 374, 130, 28);
            Button close = new Button(); close.Text = "Schließen"; close.SetBounds(344, 374, 100, 28); close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            clip.Click += delegate { Copy(); };
            save.Click += delegate { Save(); };
            th.StyleButton(clip); th.StyleButton(save); th.StyleButton(close);
            Controls.AddRange(new Control[] { rDay, rWeek, rRange, bis, dDay, wk, dFrom, dTo, count, preview, clip, save, close });
            Update_();
        }

        void Range(out DateTime from, out DateTime to)
        {
            if (rDay.Checked) { from = to = dDay.Value.Date; }
            else if (rWeek.Checked) { from = weekStart; to = weekStart.AddDays(6); }
            else { from = dFrom.Value.Date; to = dTo.Value.Date; }
        }

        void Update_()
        {
            dDay.Enabled = rDay.Checked; dFrom.Enabled = dTo.Enabled = rRange.Checked;
            DateTime a, b; Range(out a, out b);
            List<string> ids = b < a ? new List<string>() : EquipExport.Collect(store, a, b);
            text = EquipExport.Format(ids);
            preview.Text = text;
            count.Text = ids.Count + " Equipment-Nummern" + (b < a ? " (Zeitraum: „von“ liegt nach „bis“)" : "");
        }

        void Copy()
        {
            if (text.Length == 0) return;
            try { Clipboard.SetText(text); }
            catch (Exception ex) { MessageBox.Show("Kopieren fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void Save()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                DateTime a, b; Range(out a, out b);
                d.Filter = "Textdatei (*.txt)|*.txt"; d.FileName = "Equipment_" + a.ToString("yyyy-MM-dd") + (b > a ? "_bis_" + b.ToString("yyyy-MM-dd") : "");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, text, new UTF8Encoding(false)); }
                catch (Exception ex) { MessageBox.Show("Speichern fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
