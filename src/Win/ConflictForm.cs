using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Rueckfrage, wenn ein anderer PC dieselbe Stelle desselben Blocks geaendert hat (Conflict.Merge meldet Konflikt):
    // gespeicherte Fassung, eigene Fassung und Vorschlag (beides) nebeneinander. Nichts wird ohne Entscheidung ueberschrieben.
    class ConflictForm : Form
    {
        public enum Choice { Cancel, Merge, Mine }
        public Choice Result = Choice.Cancel;

        public ConflictForm(string block, string who, string when, Entry theirs, Entry mine, Entry merged, bool allowMine)
        {
            Text = "Gleichzeitige Änderung – " + block;
            Ui.Dialog(this, 840, 470, false);
            Label info = new Label();
            info.SetBounds(12, 10, 816, 54); info.UseMnemonic = false;
            info.Text = who + " hat diesen Block " + when + " gespeichert, während Sie ihn bearbeitet haben, und dieselbe Stelle geändert.\n"
                + "Der Vorschlag rechts enthält beide Fassungen. Nach \"Vorschlag übernehmen\" bleibt der Block im Bearbeitungsmodus: prüfen, kürzen, dann speichern.";
            Controls.Add(info);
            Box("Gespeichert von " + who, theirs, 12);
            Box("Ihre Fassung", mine, 288);
            Box("Vorschlag (beides)", merged, 564).BackColor = Color.FromArgb(255, 250, 225);

            Button bMerge = Btn("Vorschlag übernehmen", 12, 210, Choice.Merge), bMine = Btn("Meine Fassung speichern", 230, 190, Choice.Mine), bCancel = Btn("Abbrechen", 728, 100, Choice.Cancel);
            bMine.Enabled = allowMine;   // ersetzt fremden Text = Recht "Text löschen"
            new ToolTip().SetToolTip(bMine, "Strittige Stellen in Ihrer Fassung; Änderungen von " + who + " an anderen Stellen bleiben (alles im Verlauf sichtbar)");
            AcceptButton = bMerge; CancelButton = bCancel;
            Theme.Current.Style(this);
        }

        TextBox Box(string title, Entry e, int x)
        {
            Label l = new Label(); l.Text = title; l.SetBounds(x, 70, 264, 20); l.Font = new Font(Font, FontStyle.Bold); l.UseMnemonic = false;
            TextBox t = new TextBox(); t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Vertical; t.BackColor = SystemColors.Window;
            t.SetBounds(x, 92, 264, 320);
            string s = e.Content.Replace("\n", "\r\n");
            string[] sh = { "F", "S", "N" };
            for (int i = 0; i < 3; i++) if (e.People[i].Length > 0) s += "\r\n" + sh[i] + ": " + e.People[i].Replace("\n", ", ");
            if (e.Lkz) s += "\r\nLKZ gemessen: ja";
            t.Text = s; Ui.SimpleMenu(t);
            Controls.Add(l); Controls.Add(t);
            return t;
        }

        Button Btn(string text, int x, int w, Choice c)
        {
            Button b = new Button(); b.Text = text; b.SetBounds(x, 424, w, 32);
            b.Click += delegate { Result = c; DialogResult = c == Choice.Cancel ? DialogResult.Cancel : DialogResult.OK; };
            Controls.Add(b);
            return b;
        }
    }
}
