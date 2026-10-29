using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Nutzerverwaltung: Reiter Nutzer (anlegen, bearbeiten, loeschen; Rolle + Einzelrechte), Rollen (UserAdminForm.Roles.cs), Umgebung (Domaenen).
    // Jede Aenderung wird gegen den Stand beim Oeffnen geprueft (UserDb.Validate: letzter Admin, nur eigene Rechte, eigene Zeile, beendete Rolle).
    partial class UserAdminForm : Form
    {
        readonly UserDb db, orig;
        readonly User me;
        readonly TabControl tabs = new TabControl();
        readonly TabPage pUsers = new TabPage("Nutzer"), pRoles = new TabPage("Rollen"), pEnv = new TabPage("Umgebung");
        readonly ListBox list = new ListBox();
        readonly TextBox tFirst = new TextBox(), tLast = new TextBox(), tLogin = new TextBox(), tKuerzel = new TextBox(), tDomains = new TextBox();
        readonly ComboBox cbRole = new ComboBox();
        readonly Label lPreview = new Label();
        readonly Button bNew = new Button(), bDel = new Button(), bSave = new Button(), bReset = new Button();
        readonly Dictionary<string, CheckBox> rightBoxes = new Dictionary<string, CheckBox>();
        readonly HashSet<User> fresh = new HashSet<User>();   // eben angelegt: darf mit "anlegen"-Recht ausgefuellt werden
        User cur;
        bool loading;
        public readonly UserChanges Changes = new UserChanges();   // nur das wird gespeichert (MainForm.SaveUsers)

        static readonly Color Green = Color.FromArgb(0, 128, 0), Red = Color.FromArgb(192, 0, 0);

        public UserAdminForm(UserDb db, User me)
        {
            this.db = db; this.me = me; orig = db.Copy(); Changes.By = me;
            FormClosed += delegate { foreach (User u in fresh) db.Users.Remove(u); fresh.Clear(); };   // nie uebernommen (ohne Login) = verwerfen
            FormClosing += delegate { TakeDomains(); };
            Text = "Nutzerverwaltung";
            Ui.Dialog(this, 720, 658);
            tabs.SetBounds(8, 8, 704, 642);
            tabs.TabPages.AddRange(new[] { pUsers, pRoles, pEnv });
            Controls.Add(tabs);

            list.SetBounds(10, 10, 200, 552);
            list.SelectedIndexChanged += delegate { Select(list.SelectedItem as User); };
            bNew.SetBounds(10, 572, 60, 30); bNew.Text = "+"; bNew.Enabled = me.Has(Perm.UserCreate);
            bNew.Click += delegate { AddUser(); };
            bDel.SetBounds(80, 572, 90, 30); bDel.Text = "Löschen"; bDel.Enabled = me.Has(Perm.UserDelete);
            bDel.Click += delegate { DeleteUser(); };

            Label1(pUsers, "Vorname", 225, 10); tFirst.SetBounds(440, 10, 240, 22);
            Label1(pUsers, "Nachname", 225, 40); tLast.SetBounds(440, 40, 240, 22);
            Label1(pUsers, "Windows-Login", 225, 70); tLogin.SetBounds(440, 70, 240, 22);
            Label1(pUsers, "Kürzel (wird im Plan grün markiert)", 225, 100); tKuerzel.SetBounds(440, 100, 240, 22);
            Label1(pUsers, "Rolle", 225, 132); cbRole.SetBounds(440, 132, 240, 22); cbRole.DropDownStyle = ComboBoxStyle.DropDownList;
            cbRole.SelectedIndexChanged += delegate { if (!loading) TakeRole(); };
            Label1(pUsers, "Einzelrechte (Haken = wirksam; grün + = zusätzlich, rot − = entzogen):", 225, 164);
            int y = 186;
            foreach (string r in Perm.All)
            {
                CheckBox cb = new CheckBox();
                cb.Tag = r; cb.AutoSize = true; cb.Location = new Point(235, y);
                cb.CheckedChanged += delegate { if (!loading) RefreshMarks(); };
                y += 22; rightBoxes[r] = cb; pUsers.Controls.Add(cb);
            }
            lPreview.SetBounds(225, y + 6, 460, 20);
            bReset.SetBounds(225, 572, 200, 30); bReset.Text = "Abweichungen zurücksetzen";
            bReset.Click += delegate { TakeRole(); };
            bSave.SetBounds(435, 572, 120, 30); bSave.Text = "Übernehmen";
            bSave.Click += delegate { Apply(); };
            pUsers.Controls.AddRange(new Control[] { list, bNew, bDel, tFirst, tLast, tLogin, tKuerzel, cbRole, lPreview, bReset, bSave });

            // Umgebung (nur Admin): wo das Programm starten darf (Umgebung.Allowed); leer = ueberall
            Label1(pEnv, "Freigegebene Windows-Domänen/Rechner (Komma, * erlaubt; nur Admin):", 10, 10);
            tDomains.SetBounds(10, 34, 670, 22); tDomains.Text = string.Join(", ", db.Domains.ToArray());
            tDomains.Enabled = UserDb.IsAdmin(me.Rights); Ui.SimpleMenu(tDomains);
            string tip = "Das Programm startet nur auf PCs dieser Windows-Domänen (oder Rechner). * = überall (keine Prüfung).\n"
                + "Leer = der nächste Admin mit Domänenkonto trägt seine Domäne ein. Ihre Umgebung: " + Umgebung.Home(Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine);
            new ToolTip().SetToolTip(tDomains, tip);
            Label hint = new Label(); hint.Text = tip; hint.SetBounds(10, 66, 670, 60); pEnv.Controls.AddRange(new Control[] { tDomains, hint });

            BuildRoles();
            Reload(null);
        }

        static void Label1(Control parent, string text, int x, int y)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y + 3);
            parent.Controls.Add(l);
        }

        void Reload(User select)
        {
            list.Items.Clear();
            foreach (User u in db.Users) list.Items.Add(u);
            list.DisplayMember = "Display";
            if (select != null) list.SelectedItem = select;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            else Select(null);
        }

        // Rollen-Auswahl: aktive Rollen im Rahmen der eigenen Rechte, dazu die bisherige Rolle des Nutzers (auch beendet)
        void FillRoles(User u)
        {
            cbRole.Items.Clear();
            foreach (RightsPackage r in db.Packages)
                if ((r.Active && Array.TrueForAll(r.Rights, me.Has)) || (u != null && r.Id == u.RoleId))
                {
                    cbRole.Items.Add(new RoleItem(r, -1));
                    if (u != null && r.Id == u.RoleId) cbRole.SelectedIndex = cbRole.Items.Count - 1;
                }
        }

        RightsPackage SelectedRole() { RoleItem it = cbRole.SelectedItem as RoleItem; return it != null ? it.R : null; }

        void Select(User u)
        {
            cur = u;
            loading = true;
            tFirst.Text = u != null ? u.First : ""; tLast.Text = u != null ? u.Last : ""; tLogin.Text = u != null ? u.Login : ""; tKuerzel.Text = u != null ? u.Kuerzel : "";
            FillRoles(u);
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) kv.Value.Checked = u != null && u.Has(kv.Key);
            loading = false;
            RefreshMarks();
            bool canEdit = u != null && (me.Has(Perm.UserEdit) || (fresh.Contains(u) && me.Has(Perm.UserCreate)));
            tFirst.Enabled = tLast.Enabled = tLogin.Enabled = tKuerzel.Enabled = bSave.Enabled = canEdit;
            bool canRights = canEdit && u != me;   // eigene Rechte schreibgeschuetzt; vergeben/entziehen nur, was man selbst hat
            cbRole.Enabled = bReset.Enabled = canRights;
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) kv.Value.Enabled = canRights && me.Has(kv.Key);
            bDel.Enabled = u != null && u != me && me.Has(Perm.UserDelete);
        }

        HashSet<string> CheckedRights()
        {
            HashSet<string> s = new HashSet<string>();
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) if (kv.Value.Checked) s.Add(kv.Key);
            return s;
        }

        // Rolle uebernehmen (Auswahl oder "Abweichungen zuruecksetzen"): eigene Rechte wie die Rolle, fremde Rechte bleiben
        void TakeRole()
        {
            RightsPackage r = SelectedRole();
            if (r == null || cur == null) return;
            loading = true;
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) if (me.Has(kv.Key)) kv.Value.Checked = Array.IndexOf(r.Rights, kv.Key) >= 0;
            loading = false;
            RefreshMarks();
        }

        // Kennzeichnung je Recht (aus Rolle / + / −) und Vorschau der Rollenanzeige
        void RefreshMarks()
        {
            RightsPackage r = SelectedRole();
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes)
            {
                bool inRole = r != null && Array.IndexOf(r.Rights, kv.Key) >= 0, on = kv.Value.Checked;
                string l = Perm.Label(kv.Key);
                kv.Value.Text = on && !inRole ? "+ " + l : !on && inRole ? "− " + l : on ? l + "   (aus Rolle)" : l;
                kv.Value.ForeColor = on && !inRole ? Green : !on && inRole ? Red : ForeColor;
            }
            if (cur == null || r == null) { lPreview.Text = ""; return; }
            User t = new User(); t.RoleId = r.Id; t.Rights = CheckedRights();
            lPreview.Text = "Anzeige: " + db.RoleLabel(t);
        }

        // Domaenenliste beim Schliessen uebernehmen; nie so, dass der eigene PC ausgesperrt waere
        void TakeDomains()
        {
            if (!tDomains.Enabled) return;
            List<string> l = Umgebung.SplitList(tDomains.Text);
            if (string.Join(",", l.ToArray()) == string.Join(",", db.Domains.ToArray())) return;
            if (!Umgebung.Allowed(l, Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine))
            {
                Warn("Die Domänenliste wurde nicht übernommen: Dieser PC (" + Umgebung.Home(Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine)
                    + ") wäre danach selbst ausgesperrt.");
                return;
            }
            Changes.Domains = l;
        }

        void AddUser()
        {
            User u = db.Add("Neu", "Nutzer", "", "leser");
            fresh.Add(u);
            Reload(u);
        }

        void DeleteUser()
        {
            if (cur == null || cur == me) return;
            if (UserDb.IsAdmin(cur.Rights) && db.AdminCount(cur) == 0) { Warn(UserDb.LastAdmin); return; }
            if (!Quiet && MessageBox.Show("Nutzer " + cur.Display + " löschen?", "Nutzerverwaltung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            db.Users.Remove(cur);
            if (Changes.Added.Contains(cur) || fresh.Contains(cur)) Changes.Added.Remove(cur);   // nie gespeichert: nichts zu loeschen
            else Changes.Deleted.Add(cur.Id);
            Changes.Changed.Remove(cur); fresh.Remove(cur);
            Reload(null);
        }

        static bool Quiet;   // Selbsttest: keine Meldungsfenster
        static void Warn(string text) { if (!Quiet) MessageBox.Show(text, "Nutzerverwaltung", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        void Apply()
        {
            if (cur == null) return;
            string login = tLogin.Text.Trim();
            User other = db.FindByLogin(login);
            if (login.Length == 0 || (other != null && other != cur)) { Warn("Der Windows-Login muss ausgefüllt und eindeutig sein."); return; }
            User save = cur.Clone();
            cur.First = tFirst.Text.Trim(); cur.Last = tLast.Text.Trim(); cur.Login = login; cur.Kuerzel = tKuerzel.Text.Trim();
            RightsPackage r = SelectedRole();
            if (cur != me && r != null) db.SetRights(cur, r.Id, CheckedRights());
            bool isNew = fresh.Contains(cur);
            if (!isNew && cur.First == save.First && cur.Last == save.Last && cur.Login == save.Login && cur.Kuerzel == save.Kuerzel
                && cur.RoleId == save.RoleId && cur.Rights.SetEquals(save.Rights) && cur.Plus.Count == save.Plus.Count && cur.Minus.Count == save.Minus.Count) return;   // nichts geaendert
            string err = db.Validate(orig, me);
            if (err != null) { cur.CopyFrom(save); Warn(err); Select(cur); return; }
            fresh.Remove(cur);
            Changes.Changed.Add(cur);
            if (isNew) Changes.Added.Add(cur);
            Reload(cur);
            ReloadRoles(curRole);   // Nutzerzahl je Rolle
        }

        // Eintrag fuer Rollen-Auswahl und -Liste (n >= 0: mit Nutzerzahl)
        class RoleItem
        {
            public readonly RightsPackage R; readonly int n;
            public RoleItem(RightsPackage r, int n) { R = r; this.n = n; }
            public override string ToString() { return UserDb.Display(R) + (n >= 0 ? "   (" + n + " Nutzer)" : ""); }
        }
    }
}
