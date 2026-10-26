using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Nutzerverwaltung: anlegen (+), bearbeiten, loeschen; Rechte einzeln oder per Rechtepaket.
    class UserAdminForm : Form
    {
        readonly UserDb db;
        readonly User me;
        readonly ListBox list = new ListBox();
        readonly TextBox tFirst = new TextBox(), tLast = new TextBox(), tLogin = new TextBox(), tKuerzel = new TextBox(), tDomains = new TextBox();
        readonly Button bNew = new Button(), bDel = new Button(), bSave = new Button();
        readonly List<CheckBox> pkBoxes = new List<CheckBox>();
        readonly Dictionary<string, CheckBox> rightBoxes = new Dictionary<string, CheckBox>();
        readonly HashSet<User> fresh = new HashSet<User>();   // eben angelegt: darf mit "anlegen"-Recht ausgefuellt werden
        User cur;
        bool loading;
        public readonly UserChanges Changes = new UserChanges();   // nur das wird gespeichert (MainForm.SaveUsers)

        public UserAdminForm(UserDb db, User me)
        {
            this.db = db; this.me = me;
            FormClosed += delegate { foreach (User u in fresh) db.Users.Remove(u); fresh.Clear(); };   // nie uebernommen (ohne Login) = verwerfen
            FormClosing += delegate { TakeDomains(); };
            Text = "Nutzerverwaltung";
            Ui.Dialog(this, 640, 520);

            list.SetBounds(10, 10, 200, 430);
            list.SelectedIndexChanged += delegate { Select(list.SelectedItem as User); };
            bNew.SetBounds(10, 460, 60, 30); bNew.Text = "+"; bNew.Enabled = me.Has(Perm.UserCreate);
            bNew.Click += delegate { AddUser(); };
            bDel.SetBounds(80, 460, 90, 30); bDel.Text = "Löschen"; bDel.Enabled = me.Has(Perm.UserDelete);
            bDel.Click += delegate { DeleteUser(); };

            Label1("Vorname", 230, 14); tFirst.SetBounds(450, 10, 170, 22);
            Label1("Nachname", 230, 44); tLast.SetBounds(450, 40, 170, 22);
            Label1("Windows-Login", 230, 74); tLogin.SetBounds(450, 70, 170, 22);
            Label1("Kürzel (wird im Plan grün markiert)", 230, 104); tKuerzel.SetBounds(450, 100, 170, 22);
            Label1("Rechtepaket (ein Klick vergibt alle Rechte):", 230, 134);
            int x = 230;
            foreach (RightsPackage p in db.Packages)
            {
                CheckBox cb = new CheckBox();
                cb.Text = p.Name; cb.Tag = p; cb.AutoCheck = false; cb.AutoSize = true; cb.Location = new Point(x, 154);
                cb.Click += OnPackageClick;
                x += 100; pkBoxes.Add(cb); Controls.Add(cb);
            }
            Label1("Einzelrechte:", 230, 184);
            int y = 206;
            foreach (string r in Perm.All)
            {
                CheckBox cb = new CheckBox();
                cb.Text = Perm.Label(r); cb.Tag = r; cb.AutoSize = true; cb.Location = new Point(240, y);
                cb.CheckedChanged += delegate { if (!loading) RefreshPackages(); };
                y += 24; rightBoxes[r] = cb; Controls.Add(cb);
            }
            int by = y + 10;                         // Buttons unter der Rechteliste; Dialoghoehe richtet sich nach der Anzahl der Rechte
            // Umgebung (nur Admin): wo das Programm starten darf (Umgebung.Allowed); leer = ueberall
            Label1("Freigegebene Windows-Domänen/Rechner (Komma, * erlaubt; nur Admin):", 10, by + 44);
            tDomains.SetBounds(10, by + 66, 610, 22); tDomains.Text = string.Join(", ", db.Domains.ToArray());
            tDomains.Enabled = UserDb.IsAdmin(me.Rights); Ui.SimpleMenu(tDomains);
            new ToolTip().SetToolTip(tDomains, "Das Programm startet nur auf PCs dieser Windows-Domänen (oder Rechner). Ihre Umgebung: "
                + Umgebung.Home(Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine));
            ClientSize = new Size(640, by + 100);
            list.Height = by - 20;
            bNew.Top = bDel.Top = by;
            bSave.SetBounds(230, by, 120, 30); bSave.Text = "Übernehmen";
            bSave.Click += delegate { Apply(); };

            Controls.AddRange(new Control[] { list, bNew, bDel, tFirst, tLast, tLogin, tKuerzel, bSave });
            Reload(null);
        }

        void Label1(string text, int x, int y)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y + 3);
            Controls.Add(l);
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

        void Select(User u)
        {
            cur = u;
            loading = true;
            tFirst.Text = u != null ? u.First : ""; tLast.Text = u != null ? u.Last : ""; tLogin.Text = u != null ? u.Login : ""; tKuerzel.Text = u != null ? u.Kuerzel : "";
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) kv.Value.Checked = u != null && u.Has(kv.Key);
            loading = false;
            RefreshPackages();
            bool canEdit = u != null && (me.Has(Perm.UserEdit) || (fresh.Contains(u) && me.Has(Perm.UserCreate)));
            tFirst.Enabled = tLast.Enabled = tLogin.Enabled = tKuerzel.Enabled = bSave.Enabled = canEdit;
            bool canRights = canEdit && u != me;   // eigene Rechte schreibgeschuetzt; vergeben/entziehen nur, was man selbst hat
            foreach (CheckBox cb in pkBoxes) cb.Enabled = canRights && Array.TrueForAll(((RightsPackage)cb.Tag).Rights, me.Has);
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) kv.Value.Enabled = canRights && me.Has(kv.Key);
            bDel.Enabled = u != null && u != me && me.Has(Perm.UserDelete);
        }

        HashSet<string> CheckedRights()
        {
            HashSet<string> s = new HashSet<string>();
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) if (kv.Value.Checked) s.Add(kv.Key);
            return s;
        }

        // Paket gilt als "ausgewaehlt", wenn die gesetzten Rechte exakt seinen Rechten entsprechen.
        void RefreshPackages()
        {
            HashSet<string> now = CheckedRights();
            foreach (CheckBox cb in pkBoxes) cb.Checked = cur != null && UserDb.Matches(now, (RightsPackage)cb.Tag);
        }

        void OnPackageClick(object sender, EventArgs e)
        {
            CheckBox pb = (CheckBox)sender;
            if (!pb.Enabled) return;
            HashSet<string> set = new HashSet<string>();
            UserDb.ApplyPackage(set, (RightsPackage)pb.Tag);
            loading = true;
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) if (me.Has(kv.Key)) kv.Value.Checked = set.Contains(kv.Key);   // fremde Rechte bleiben
            loading = false;
            RefreshPackages();   // danach einzeln feinjustierbar
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
            User u = db.Add("Neu", "Nutzer", "", "Leser");
            fresh.Add(u);
            Reload(u);
        }

        void DeleteUser()
        {
            if (cur == null || cur == me) return;
            if (UserDb.IsAdmin(cur.Rights) && db.AdminCount(cur) == 0) { Warn(LastAdmin); return; }
            if (!Quiet && MessageBox.Show("Nutzer " + cur.Display + " löschen?", "Nutzerverwaltung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            db.Users.Remove(cur);
            if (Changes.Added.Contains(cur) || fresh.Contains(cur)) Changes.Added.Remove(cur);   // nie gespeichert: nichts zu loeschen
            else Changes.Deleted.Add(cur.Id);
            Changes.Changed.Remove(cur); fresh.Remove(cur);
            Reload(null);
        }

        const string LastAdmin = "Mindestens ein Admin (alle Rechte) muss bleiben.";
        static bool Quiet;   // Selbsttest: keine Meldungsfenster
        static void Warn(string text) { if (!Quiet) MessageBox.Show(text, "Nutzerverwaltung", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        void Apply()
        {
            if (cur == null) return;
            string login = tLogin.Text.Trim();
            User other = db.FindByLogin(login);
            if (login.Length == 0 || (other != null && other != cur)) { Warn("Der Windows-Login muss ausgefüllt und eindeutig sein."); return; }
            HashSet<string> rights = cur == me ? cur.Rights : CheckedRights();
            if (UserDb.IsAdmin(cur.Rights) && !UserDb.IsAdmin(rights) && db.AdminCount(cur) == 0) { Warn(LastAdmin); return; }
            string first = tFirst.Text.Trim(), last = tLast.Text.Trim(), kz = tKuerzel.Text.Trim();
            bool isNew = fresh.Remove(cur);
            if (!isNew && cur.First == first && cur.Last == last && cur.Login == login && cur.Kuerzel == kz && cur.Rights.SetEquals(rights)) return;   // nichts geaendert
            cur.First = first; cur.Last = last; cur.Login = login; cur.Kuerzel = kz; cur.Rights = rights;
            Changes.Changed.Add(cur);
            if (isNew) Changes.Added.Add(cur);
            Reload(cur);
        }

#if TESTHOOKS
        // Selbsttest: eigene Rechte gesperrt, nur eigene Rechte vergebbar, letzter Admin bleibt, nur Aenderungen gemeldet, leerer neuer Nutzer verworfen
        public static string SelfTest()
        {
            Quiet = true;
            UserDb db = UserDb.Default();
            User adm = db.Add("A", "Admin", "aadmin", "Admin"), pr = db.Add("P", "Prod", "pprod", "Produktion");
            pr.Rights.Add(Perm.UserEdit); pr.Rights.Add(Perm.UserCreate); pr.Rights.Add(Perm.UserDelete);
            bool own, limit, last, lastDel, admOk, onlyChg, discard;
            using (UserAdminForm f = new UserAdminForm(db, pr))
            {
                f.Select(pr);
                own = !f.rightBoxes[Perm.Read].Enabled && !f.pkBoxes[0].Enabled && f.tKuerzel.Enabled;
                f.Select(adm);
                limit = f.rightBoxes[Perm.EditText].Enabled && !f.rightBoxes[Perm.PackageAdmin].Enabled && f.pkBoxes[0].Enabled && !f.pkBoxes[2].Enabled;
                f.rightBoxes[Perm.EditText].Checked = false; f.Apply();
                last = UserDb.IsAdmin(adm.Rights) && !f.Changes.Any;
                f.DeleteUser();
                lastDel = db.Users.Contains(adm) && !f.Changes.Any;
            }
            User adm2 = db.Add("B", "Admin", "badmin", "Admin");
            using (UserAdminForm f = new UserAdminForm(db, adm))
            {
                f.Opacity = 0; f.ShowInTaskbar = false; f.Show();
                f.Select(adm2); f.Apply();
                onlyChg = !f.Changes.Any;
                f.OnPackageClick(f.pkBoxes[0], EventArgs.Empty); f.Apply();
                admOk = !UserDb.IsAdmin(adm2.Rights) && f.Changes.Changed.Contains(adm2) && f.Changes.Added.Count == 0;
                int n = db.Users.Count;
                f.AddUser(); f.Close();
                discard = db.Users.Count == n && f.Changes.Added.Count == 0;
            }
            Quiet = false;
            return "Nutzerverwaltung: eigene Rechte gesperrt=" + Ok(own) + " nur eigene Rechte vergebbar=" + Ok(limit) + " letzter Admin bleibt=" + Ok(last && lastDel)
                + " Admin herabstufen (2 Admins)=" + Ok(admOk) + " ohne Änderung nichts=" + Ok(onlyChg) + " leerer Neuer verworfen=" + Ok(discard);
        }
        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }
#endif
    }
}
