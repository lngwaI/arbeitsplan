#if DEMO
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Startdialog der Vorfuehr-Version: welche Rolle soll vorgefuehrt werden? Setzt den Demo-Nutzer (kein Passwort, kein Windows-Login).
    class DemoRoleForm : Form
    {
        public static readonly Color Accent = Color.FromArgb(214, 106, 0);
        readonly List<RadioButton> options = new List<RadioButton>();
        readonly List<string> logins = new List<string>();
        public string Login = "";

        public DemoRoleForm(UserDb db, string preselect)
        {
            Text = "Arbeitsplan – VORFÜHRUNG (Testdaten)";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen; Font = Theme.Current.UiFont();
            Panel head = new Panel(); head.SetBounds(0, 0, 520, 52); head.BackColor = Accent;
            Label hl = new Label(); hl.SetBounds(14, 8, 492, 38); hl.ForeColor = Color.White; hl.BackColor = Accent;
            hl.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold); hl.Text = "VORFÜHRUNG – Testdaten\nRolle vorführen"; hl.AutoSize = false;
            head.Controls.Add(hl);
            Controls.Add(head);
            int y = 64;
            foreach (RightsPackage p in db.Packages)
            {
                if (!p.Active) continue;
                User u = db.Users.Find(delegate(User x) { return x.RoleId == p.Id; });
                if (u == null) continue;
                RadioButton rb = new RadioButton(); rb.SetBounds(16, y, 488, 22); rb.Text = p.Name + "  –  " + u.Display + " (" + u.Kuerzel + ")";
                rb.Font = new Font(Font, FontStyle.Bold); rb.Checked = options.Count == 0 || p.Id == preselect;
                Label d = new Label(); d.SetBounds(38, y + 22, 466, 34); d.Text = Describe(p); d.ForeColor = SystemColors.GrayText;
                Controls.Add(rb); Controls.Add(d);
                options.Add(rb); logins.Add(u.Login);
                y += 62;
            }
            Label note = new Label(); note.SetBounds(16, y, 488, 34);
            note.Text = "Die Daten sind erfunden und haben nichts mit den echten Daten zu tun. Wechsel später: Hilfe → Rolle wechseln.";
            note.ForeColor = SystemColors.GrayText; Controls.Add(note);
            Button ok = new Button(), no = new Button();
            ok.Text = "Starten"; ok.SetBounds(300, y + 42, 100, 28); ok.DialogResult = DialogResult.OK;
            no.Text = "Beenden"; no.SetBounds(406, y + 42, 100, 28); no.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { ok, no });
            AcceptButton = ok; CancelButton = no;
            ClientSize = new Size(520, y + 84);
            Theme.Current.Style(this);
            head.BackColor = Accent; hl.BackColor = Accent; hl.ForeColor = Color.White;
            ok.Click += delegate { for (int i = 0; i < options.Count; i++) if (options[i].Checked) Login = logins[i]; };
        }

        static string Describe(RightsPackage p)
        {
            switch (p.Id)
            {
                case "leser": return "Nur ansehen und exportieren (Bild, PDF, Paket).";
                case "produktion": return "Wochenplan bearbeiten, formatieren, Pakete übernehmen, Rundgänge abhaken, Status setzen.";
                case UserDb.AdminId: return "Alles: Einstellungen, Nutzerverwaltung, Equipment-Stammdaten, Pakete einrichten, Sicherung.";
            }
            return "Eigene Rolle der Demo-Daten (Rechte laut Nutzerverwaltung).";
        }
    }
}
#endif
