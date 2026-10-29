#if DEMO
using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Vorfuehr-Version: Banner, Menue Hilfe (Rolle wechseln, Demo zuruecksetzen), Info. Nur im Build mit DEMO (docs/Demo.md).
    partial class MainForm
    {
        public string DemoRestart;   // null = beenden; "role" = danach Rollenwahl; "reset" = Demo zuruecksetzen, dann mit gleicher Rolle neu starten
        public string DemoRoleId { get { return me.RoleId; } }
        Panel demoBanner;
        Label demoLabel;

        Control BuildDemoBanner()
        {
            demoBanner = new Panel(); demoBanner.Dock = DockStyle.Top; demoBanner.Height = Font.Height + 10;
            demoLabel = new Label(); demoLabel.Dock = DockStyle.Fill; demoLabel.TextAlign = ContentAlignment.MiddleCenter;
            demoLabel.Text = "VORFÜHRUNG – Testdaten, keine echten Daten   ·   Rolle: " + users.RoleLabel(me) + "   ·   " + me.Display;
            demoBanner.Controls.Add(demoLabel);
            return demoBanner;
        }

        // Nach Theme.Style (das Farben vorgibt): auffaellige Akzentfarbe
        void StyleDemoBanner()
        {
            demoBanner.BackColor = demoLabel.BackColor = DemoRoleForm.Accent;
            demoLabel.ForeColor = Color.White; demoLabel.Font = new Font(Font, FontStyle.Bold);
        }

        void AddDemoMenu(ToolStripMenuItem help)
        {
            help.DropDownItems.Add("Rolle wechseln…", null, delegate { DemoLeave("role"); });
            help.DropDownItems.Add("Demo zurücksetzen…", null, delegate { DemoLeave("reset"); });
            help.DropDownItems.Add(new ToolStripSeparator());
        }

        // Fenster schliessen und danach (Program.DemoRun) neu starten; nach "Abbrechen" (ungespeicherte Aenderung) bleibt alles wie es ist
        void DemoLeave(string action)
        {
            if (editCell != null) { MessageBox.Show(this, "Bitte zuerst die Bearbeitung beenden (Strg+S oder Esc).", "Vorführung", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (action == "reset" && MessageBox.Show(this, "Die Demo auf den Ausgangszustand zurücksetzen?\n\nAlle Änderungen in der Demo gehen verloren (Plan, Pakete, Nutzer, Katalog, Sicherungen; es sind nur Testdaten)."
                + "\nWeitere Demo-Fenster müssen vorher geschlossen werden. Danach startet die Demo mit der gleichen Rolle neu.",
                "Vorführung – Demo zurücksetzen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            DemoRestart = action;
            Close();
            if (!IsDisposed) DemoRestart = null;   // Schliessen abgebrochen
        }

        bool ShowDemoInfo()
        {
            MessageBox.Show(this, "Arbeitsplan – VORFÜHRUNG\n\nErfundene Testdaten, getrennt von den echten Daten (nichts davon wird je mit dem Firmennetz oder den echten Daten verbunden)."
                + "\n\nDaten: " + (fstore != null ? fstore.Folder : "(im Speicher)")
                + "\nNutzer: " + me.Display + (me.Kuerzel.Length > 0 ? " (" + me.Kuerzel + ")" : "") + ", Rolle: " + users.RoleLabel(me)
                + "\n\nHilfe → Rolle wechseln: andere Rolle vorführen.\nHilfe → Demo zurücksetzen: Ausgangsdaten neu erzeugen.",
                "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
    }
}
#endif
