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
        readonly TextBox tFirst = new TextBox(), tLast = new TextBox(), tLogin = new TextBox(), tKuerzel = new TextBox();
        readonly Button bNew = new Button(), bDel = new Button(), bSave = new Button();
        readonly List<CheckBox> pkBoxes = new List<CheckBox>();
        readonly Dictionary<string, CheckBox> rightBoxes = new Dictionary<string, CheckBox>();
        readonly HashSet<User> fresh = new HashSet<User>();   // eben angelegt: darf mit "anlegen"-Recht ausgefuellt werden
        User cur;
        bool loading;

        public UserAdminForm(UserDb db, User me)
        {
            this.db = db; this.me = me;
            Text = "Nutzerverwaltung";
            Font = Theme.Current.UiFont();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 520);

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
            ClientSize = new Size(640, by + 46);
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
            foreach (CheckBox cb in pkBoxes) cb.Enabled = canEdit;
            foreach (CheckBox cb in rightBoxes.Values) cb.Enabled = canEdit;
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
            foreach (KeyValuePair<string, CheckBox> kv in rightBoxes) kv.Value.Checked = set.Contains(kv.Key);
            loading = false;
            RefreshPackages();   // danach einzeln feinjustierbar
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
            if (MessageBox.Show("Nutzer " + cur.Display + " löschen?", "Nutzerverwaltung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            db.Users.Remove(cur);
            Reload(null);
        }

        void Apply()
        {
            if (cur == null) return;
            string login = tLogin.Text.Trim();
            User other = db.FindByLogin(login);
            if (login.Length == 0 || (other != null && other != cur))
            {
                MessageBox.Show("Der Windows-Login muss ausgefüllt und eindeutig sein.", "Nutzerverwaltung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            cur.First = tFirst.Text.Trim(); cur.Last = tLast.Text.Trim(); cur.Login = login; cur.Kuerzel = tKuerzel.Text.Trim();
            cur.Rights = CheckedRights();
            fresh.Remove(cur);
            Reload(cur);
        }
    }
}
