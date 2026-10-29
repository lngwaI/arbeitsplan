using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Reiter "Rollen": umbenennen, neu/kopieren, beenden/reaktivieren (nie loeschen), Rechte aendern. Nur mit Recht PackageAdmin.
    partial class UserAdminForm
    {
        readonly ListBox roleList = new ListBox();
        readonly TextBox tRoleName = new TextBox(), tRoleId = new TextBox();
        readonly Button bRoleNew = new Button(), bRoleCopy = new Button(), bRoleEnd = new Button(), bRoleSave = new Button();
        readonly Label lRoleHint = new Label();
        readonly Dictionary<string, CheckBox> roleBoxes = new Dictionary<string, CheckBox>();
        RightsPackage curRole;

        bool CanRoles { get { return me.Has(Perm.PackageAdmin); } }

        void BuildRoles()
        {
            roleList.SetBounds(10, 10, 255, 522);
            roleList.SelectedIndexChanged += delegate { RoleItem it = roleList.SelectedItem as RoleItem; SelectRole(it != null ? it.R : null); };
            bRoleNew.SetBounds(10, 542, 65, 30); bRoleNew.Text = "Neu"; bRoleNew.Click += delegate { NewRole(false); };
            bRoleCopy.SetBounds(80, 542, 80, 30); bRoleCopy.Text = "Kopieren"; bRoleCopy.Click += delegate { NewRole(true); };
            bRoleEnd.SetBounds(165, 542, 100, 30); bRoleEnd.Click += delegate { ToggleRole(); };
            Label1(pRoles, "Name", 280, 10); tRoleName.SetBounds(400, 10, 280, 22);
            Label1(pRoles, "Kennung", 280, 40); tRoleId.SetBounds(400, 40, 120, 22); tRoleId.ReadOnly = true;
            Label1(pRoles, "Rechte der Rolle:", 280, 74);
            int y = 96;
            foreach (string r in Perm.All)
            {
                CheckBox cb = new CheckBox(); cb.Text = Perm.Label(r); cb.Tag = r; cb.AutoSize = true; cb.Location = new Point(290, y);
                y += 22; roleBoxes[r] = cb; pRoles.Controls.Add(cb);
            }
            lRoleHint.SetBounds(280, y + 8, 400, 70);
            bRoleSave.SetBounds(280, 542, 120, 30); bRoleSave.Text = "Übernehmen"; bRoleSave.Click += delegate { ApplyRole(); };
            pRoles.Controls.AddRange(new Control[] { roleList, bRoleNew, bRoleCopy, bRoleEnd, tRoleName, tRoleId, lRoleHint, bRoleSave });
            ReloadRoles(null);
        }

        void ReloadRoles(RightsPackage select)
        {
            roleList.Items.Clear();
            foreach (RightsPackage r in db.Packages)
            {
                roleList.Items.Add(new RoleItem(r, db.RoleUsers(r.Id)));
                if (r == select) roleList.SelectedIndex = roleList.Items.Count - 1;
            }
            if (roleList.SelectedIndex < 0 && roleList.Items.Count > 0) roleList.SelectedIndex = 0;
        }

        void SelectRole(RightsPackage r)
        {
            curRole = r;
            tRoleName.Text = r != null ? r.Name : ""; tRoleId.Text = r != null ? r.Id : "";
            bool admin = r != null && r.Id == UserDb.AdminId, can = CanRoles && r != null;
            foreach (KeyValuePair<string, CheckBox> kv in roleBoxes)
            {
                kv.Value.Checked = r != null && Array.IndexOf(r.Rights, kv.Key) >= 0;
                kv.Value.Enabled = can && !admin && me.Has(kv.Key);
            }
            tRoleName.Enabled = bRoleSave.Enabled = bRoleCopy.Enabled = can;
            bRoleNew.Enabled = CanRoles;
            bRoleEnd.Text = r != null && !r.Active ? "Reaktivieren" : "Beenden";
            bRoleEnd.Enabled = can && !admin;
            lRoleHint.Text = r == null ? "" : !CanRoles ? "Rollen ändern erfordert das Recht \"" + Perm.Label(Perm.PackageAdmin) + "\"."
                : "Änderungen betreffen " + db.RoleUsers(r.Id) + " Nutzer."
                + (admin ? "\nDie Rolle Admin hat immer alle Rechte (nur umbenennbar)." : "")
                + (!r.Active ? "\nBeendet: nicht mehr neu vergebbar; bisherige Nutzer behalten sie." : "");
        }

        // Aenderung pruefen (gegen den Stand beim Oeffnen); bei Fehler zuruecknehmen
        bool CommitRole(RightsPackage r, RightsPackage save, bool isNew)
        {
            db.ResolveAll();
            string err = db.Validate(orig, me);
            if (err != null)
            {
                if (isNew) db.Packages.Remove(r);
                else { r.Name = save.Name; r.Rights = save.Rights; r.Active = save.Active; }
                db.ResolveAll();
                Warn(err);
                SelectRole(curRole);
                return false;
            }
            Changes.Roles.Add(r);
            if (isNew) Changes.AddedRoles.Add(r);
            ReloadRoles(r);
            Select(cur);   // Rollen-Auswahl und Rechte im Reiter "Nutzer" aktualisieren
            return true;
        }

        void NewRole(bool copy)
        {
            if (!CanRoles || (copy && curRole == null)) return;
            string[] rights = copy ? Array.FindAll(curRole.Rights, me.Has) : new[] { Perm.Read };
            RightsPackage r = db.NewRole(copy ? "Kopie von " + curRole.Name : "Neue Rolle", rights);
            CommitRole(r, null, true);
        }

        void ToggleRole()
        {
            if (curRole == null || curRole.Id == UserDb.AdminId) return;
            RightsPackage save = curRole.Clone();
            curRole.Active = !curRole.Active;
            CommitRole(curRole, save, false);
        }

        void ApplyRole()
        {
            RightsPackage r = curRole;
            if (r == null) return;
            string name = tRoleName.Text.Trim();
            RightsPackage other = db.Package(name);
            if (name.Length == 0 || (other != null && other != r && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
            { Warn("Der Rollenname muss ausgefüllt und eindeutig sein."); return; }
            List<string> rights = new List<string>();
            foreach (KeyValuePair<string, CheckBox> kv in roleBoxes) if (kv.Value.Checked) rights.Add(kv.Key);
            RightsPackage save = r.Clone();
            r.Name = name;
            if (r.Id != UserDb.AdminId) r.Rights = rights.ToArray();
            if (r.Name == save.Name && new HashSet<string>(r.Rights).SetEquals(save.Rights)) return;   // nichts geaendert
            CommitRole(r, save, false);
        }

#if TESTHOOKS
        // Selbsttest: eigene Rechte gesperrt, nur eigene Rechte vergebbar, letzter Admin bleibt, nur Aenderungen gemeldet, leerer neuer Nutzer verworfen,
        // Rollen nur mit PackageAdmin, Kopieren/Beenden, beendete Rolle nicht waehlbar
        public static string SelfTest()
        {
            Quiet = true;
            UserDb db = UserDb.Default();
            User adm = db.Add("A", "Admin", "aadmin", "Admin"), pr = db.Add("P", "Prod", "pprod", "Produktion"), le = db.Add("L", "Leser", "lleser", "Leser");
            db.SetRights(pr, "produktion", new List<string>(pr.Rights) { Perm.UserEdit, Perm.UserCreate, Perm.UserDelete });
            bool own, limit, last, lastDel, admOk, onlyChg, discard, dom, roles;
            using (UserAdminForm f = new UserAdminForm(db, pr))
            {
                f.Select(pr);
                own = !f.rightBoxes[Perm.Read].Enabled && !f.cbRole.Enabled && f.tKuerzel.Enabled;
                dom = !f.tDomains.Enabled;   // Domaenen nur fuer Admins
                roles = !f.bRoleNew.Enabled && !f.bRoleSave.Enabled;   // Rollen nur mit PackageAdmin
                f.Select(le);
                limit = f.cbRole.Items.Count == 2;   // Leser, Produktion; Admin nicht (Rechte, die pr nicht hat)
                f.Select(adm);
                limit &= f.rightBoxes[Perm.EditText].Enabled && !f.rightBoxes[Perm.PackageAdmin].Enabled;
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
                f.cbRole.SelectedIndex = 0; f.Apply();   // Leser
                admOk = !UserDb.IsAdmin(adm2.Rights) && adm2.RoleId == "leser" && f.Changes.Changed.Contains(adm2) && f.Changes.Added.Count == 0;
                f.tDomains.Text = "FREMD-XYZ"; f.TakeDomains();   // wuerde diesen PC aussperren: abgelehnt
                dom &= f.tDomains.Enabled && f.Changes.Domains == null;
                f.tDomains.Text = Umgebung.Home(Umgebung.UserDomain, Umgebung.MachineDomain, Umgebung.Machine) + ", Zweite"; f.TakeDomains();
                dom &= f.Changes.Domains != null && f.Changes.Domains.Count == 2;
                // Rollen: Produktion kopieren, beenden; fuer einen anderen Nutzer nicht mehr waehlbar
                f.SelectRole(db.Role("produktion")); f.NewRole(true);
                RightsPackage cp = f.curRole;
                roles &= cp.Id == "r1" && cp.Name == "Kopie von Produktion" && f.Changes.AddedRoles.Contains(cp) && f.bRoleSave.Enabled;
                f.ToggleRole();
                f.Select(le);
                bool listed = false; foreach (RoleItem it in f.cbRole.Items) listed |= it.R == cp;
                roles &= !cp.Active && !listed && f.Changes.Roles.Contains(cp);
                f.SelectRole(db.Role(UserDb.AdminId));
                roles &= !f.roleBoxes[Perm.Read].Enabled && !f.bRoleEnd.Enabled;
                int n = db.Users.Count;
                f.AddUser(); f.Close();
                discard = db.Users.Count == n && f.Changes.Added.Count == 0;
            }
            Quiet = false;
            return "Nutzerverwaltung: eigene Rechte gesperrt=" + Ok(own) + " nur eigene Rechte vergebbar=" + Ok(limit) + " letzter Admin bleibt=" + Ok(last && lastDel)
                + " Admin herabstufen (2 Admins)=" + Ok(admOk) + " ohne Änderung nichts=" + Ok(onlyChg) + " leerer Neuer verworfen=" + Ok(discard)
                + " Domänen nur Admin/kein Aussperren=" + Ok(dom) + " Rollen-Reiter=" + Ok(roles);
        }
        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }
#endif
    }
}
