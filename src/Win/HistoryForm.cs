using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Verlauf eines Blocks: alle gespeicherten Fassungen (nur lesen, append-only). "Wiederherstellen" liefert die
    // gewaehlte Fassung zurueck; MainForm oeffnet damit den Block im Bearbeitungsmodus (Speichern wie gewohnt).
    class HistoryForm : Form
    {
        readonly ListView list = new ListView();
        readonly TextBox before = new TextBox(), after = new TextBox();
        readonly Button bRestore = new Button(), bClose = new Button();
        readonly string[] labels;
        readonly bool canRestore;
        public Entry Result;

        public HistoryForm(string title, List<AuditEntry> audit, string[] labels, bool canRestore)
        {
            this.labels = labels; this.canRestore = canRestore;
            Text = "Verlauf – " + title;
            Ui.Dialog(this, 760, 520);

            Label hint = new Label();
            hint.Text = audit.Count == 0 ? "Für diesen Block wurden noch keine Änderungen protokolliert."
                : "Alle gespeicherten Fassungen (neueste oben). Einträge können nicht geändert oder gelöscht werden.";
            hint.SetBounds(10, 8, 740, 20);

            list.SetBounds(10, 32, 740, 190);
            list.View = View.Details; list.FullRowSelect = true; list.HideSelection = false; list.MultiSelect = false;
            list.Columns.Add("Version", 60); list.Columns.Add("Gespeichert", 130); list.Columns.Add("Nutzer", 170); list.Columns.Add("Inhalt", 355);
            for (int i = audit.Count - 1; i >= 0; i--)
            {
                AuditEntry a = audit[i];
                ListViewItem it = new ListViewItem(new[] { a.Version.ToString(), a.Time.ToString("dd.MM.yyyy HH:mm:ss"),
                    a.Kuerzel.Length > 0 ? a.Login + " (" + a.Kuerzel + ")" : a.Login, Audit.Preview(a.After) });
                it.Tag = a;
                list.Items.Add(it);
            }
            list.SelectedIndexChanged += delegate { ShowSel(); };

            Label lb = new Label(), la = new Label();
            lb.Text = "Vorher"; lb.SetBounds(10, 230, 365, 18);
            la.Text = "Nachher (diese Version)"; la.SetBounds(385, 230, 365, 18);
            foreach (TextBox t in new[] { before, after })
            {
                t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Vertical; t.BackColor = SystemColors.Window;
                Ui.SimpleMenu(t);
            }
            before.SetBounds(10, 250, 365, 220); after.SetBounds(385, 250, 365, 220);

            bRestore.Text = "Diese Version wiederherstellen"; bRestore.SetBounds(10, 480, 230, 30);
            bRestore.Click += delegate { AuditEntry a = Sel(); if (a == null || a.After == null) return; Result = a.After.Clone(); DialogResult = DialogResult.OK; };
            Label note = new Label();
            note.Text = canRestore ? "Öffnet den Block im Bearbeitungsmodus;\nerst Speichern übernimmt die Fassung." : "Wiederherstellen erfordert das Recht \"Text ändern\".";
            note.SetBounds(248, 479, 400, 34);
            bClose.Text = "Schließen"; bClose.SetBounds(655, 480, 95, 30); bClose.DialogResult = DialogResult.Cancel;
            CancelButton = bClose;

            Controls.AddRange(new Control[] { hint, list, lb, la, before, after, bRestore, note, bClose });
            if (list.Items.Count > 0) list.Items[0].Selected = true;
            ShowSel();
        }

        AuditEntry Sel() { return list.SelectedItems.Count > 0 ? (AuditEntry)list.SelectedItems[0].Tag : null; }

        void ShowSel()
        {
            AuditEntry a = Sel();
            before.Text = a == null ? "" : Audit.Text(a.Before, labels).Replace("\n", "\r\n");
            after.Text = a == null ? "" : Audit.Text(a.After, labels).Replace("\n", "\r\n");
            bRestore.Enabled = canRestore && a != null && a.After != null;
        }
    }
}
