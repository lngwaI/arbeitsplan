using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Ein- und Ausgabe des Puffer-Dialogs (auch vom Selbsttest ohne Dialog genutzt).
    class BufferChoice
    {
        public enum Act { Ok, Remove, Up, Down }
        public Act Action = Act.Ok;
        public string Name = "", Typ = "", Text = "";
        public string[] Types = new string[0];                  // Werte aus RowInfo.Variants
        public List<string> Names = new List<string>();         // Vorschlaege fuer den Namen
        public int Number;                                      // Position (1..n); 0 = neuer Puffer
        public bool CanUp, CanDown;
    }

    // Puffer anlegen/bearbeiten: Name (mit Vorschlaegen), Typ, Text; vorhandene Puffer zusaetzlich Entfernen und Hoch/Runter.
    class BufferForm : Form
    {
        readonly BufferChoice ch;
        readonly ComboBox cbName = new ComboBox();
        readonly TextBox tText = new TextBox();
        readonly List<RadioButton> radios = new List<RadioButton>();

        public BufferForm(BufferChoice choice)
        {
            ch = choice;
            Text = ch.Number == 0 ? "Puffer hinzufügen" : "Puffer " + ch.Number + " bearbeiten";
            Ui.Dialog(this, 440, 322, false);
            Theme th = Theme.Current;

            Label l1 = new Label(); l1.Text = "Name:"; l1.SetBounds(12, 15, 60, 20);
            cbName.SetBounds(80, 12, 348, 24); cbName.DropDownStyle = ComboBoxStyle.DropDown; cbName.MaxLength = 80;
            foreach (string n in ch.Names) cbName.Items.Add(n);
            cbName.Text = ch.Name;
            if (cbName.Items.Count > 0) { cbName.AutoCompleteMode = AutoCompleteMode.SuggestAppend; cbName.AutoCompleteSource = AutoCompleteSource.ListItems; }

            Label l2 = new Label(); l2.Text = "Typ:"; l2.SetBounds(12, 51, 60, 20);
            int x = 80;
            foreach (string v in ch.Types)
            {
                RadioButton r = new RadioButton(); r.Text = PlanView.VarLabel(v); r.Tag = v; r.AutoSize = true; r.Location = new Point(x, 49);
                r.Checked = v == ch.Typ;
                radios.Add(r); Controls.Add(r);
                x += TextRenderer.MeasureText(r.Text, Font).Width + 40;
            }
            if (radios.Count > 0 && !radios.Exists(delegate(RadioButton r) { return r.Checked; })) radios[0].Checked = true;

            Label l3 = new Label(); l3.Text = "Text (optional):"; l3.SetBounds(12, 84, 200, 20);
            tText.SetBounds(12, 106, 416, 110); tText.Multiline = true; tText.AcceptsReturn = true; tText.ScrollBars = ScrollBars.Vertical; tText.WordWrap = true;
            tText.Text = ch.Text.Replace("\n", "\r\n");
            Ui.SimpleMenu(tText);

            Label hint = new Label(); hint.ForeColor = SystemColors.GrayText; hint.SetBounds(12, 224, 416, 36);
            hint.Text = "Der Typ steuert die Pflichtpunkte: mobil = Tagesprobe mit der Herstellung erledigt, stationär = erledigt die Wochenprobe der KW. "
                + "Er erscheint im Plan nur als Seitenstrich.";

            Button ok = new Button(); ok.Text = "OK"; ok.SetBounds(262, 280, 80, 28);
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(348, 280, 80, 28); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            ok.Click += delegate { Collect(BufferChoice.Act.Ok); DialogResult = DialogResult.OK; };
            th.StyleButton(ok); th.StyleButton(cancel);
            Controls.AddRange(new Control[] { l1, cbName, l2, l3, tText, hint, ok, cancel });

            if (ch.Number > 0)
            {
                Button rem = new Button(); rem.Text = "Entfernen"; rem.SetBounds(12, 280, 84, 28);
                rem.Click += delegate
                {
                    if (MessageBox.Show(this, "Puffer " + ch.Number + " wirklich entfernen?", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    Collect(BufferChoice.Act.Remove); DialogResult = DialogResult.OK;
                };
                Button up = new Button(); up.Text = "Hoch"; up.SetBounds(102, 280, 62, 28); up.Enabled = ch.CanUp;
                up.Click += delegate { Collect(BufferChoice.Act.Up); DialogResult = DialogResult.OK; };
                Button down = new Button(); down.Text = "Runter"; down.SetBounds(170, 280, 62, 28); down.Enabled = ch.CanDown;
                down.Click += delegate { Collect(BufferChoice.Act.Down); DialogResult = DialogResult.OK; };
                th.StyleButton(rem); th.StyleButton(up); th.StyleButton(down);
                Controls.AddRange(new Control[] { rem, up, down });
            }
        }

        void Collect(BufferChoice.Act a)
        {
            ch.Action = a;
            ch.Name = cbName.Text.Trim();
            ch.Text = tText.Text.Replace("\r\n", "\n").TrimEnd();
            foreach (RadioButton r in radios) if (r.Checked) ch.Typ = (string)r.Tag;
        }
    }
}
