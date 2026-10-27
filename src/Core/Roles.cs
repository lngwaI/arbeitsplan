using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Rollen: Nutzerrechte = Rolle + Plus - Minus (nie rollenlos). Pruefung und Zusammenfuehren der Nutzerverwaltung.
    public partial class UserDb
    {
        public const string AdminId = "admin";
        public const string LastAdmin = "Mindestens ein Admin (alle Rechte) muss bleiben.";

        public RightsPackage Role(string id)
        {
            foreach (RightsPackage p in Packages) if (p.Id == id) return p;
            return null;
        }

        public static string Display(RightsPackage r) { return r.Name + (r.Active ? "" : " (beendet)"); }

        public int RoleUsers(string id) { int n = 0; foreach (User u in Users) if (u.RoleId == id) n++; return n; }

        // Wirksame Rechte = Rolle + Plus - Minus; unbekannte Rolle: bisherige Rechte bleiben (Rueckfall aus der Datei)
        public void Resolve(User u)
        {
            RightsPackage r = Role(u.RoleId);
            if (r != null)
            {
                u.Rights.Clear();
                foreach (string p in r.Rights) u.Rights.Add(p);
                foreach (string p in u.Plus) u.Rights.Add(p);
                foreach (string p in u.Minus) u.Rights.Remove(p);
            }
            u.RoleText = RoleLabel(u);
        }

        public void ResolveAll()
        {
            foreach (RightsPackage r in Packages) if (r.Id == AdminId) r.Rights = (string[])Perm.All.Clone();
            foreach (User u in Users) Resolve(u);
        }

        // Rolle setzen und Plus/Minus so, dass genau die gewuenschten Rechte wirksam sind
        public void SetRights(User u, string roleId, ICollection<string> rights)
        {
            RightsPackage r = Role(roleId);
            u.RoleId = roleId; u.Plus.Clear(); u.Minus.Clear();
            if (r != null)
                foreach (string d in Deviations(new HashSet<string>(rights), r))
                    (d[0] == '+' ? u.Plus : u.Minus).Add(d.Substring(1));
            else { u.Rights.Clear(); foreach (string p in rights) u.Rights.Add(p); }
            Resolve(u);
        }

        // Rolle (Id oder Name) ohne Abweichungen
        public void Assign(User u, string role)
        {
            RightsPackage r = Package(role);
            if (r != null) SetRights(u, r.Id, r.Rights);
        }

        // "+Recht" (zuerst), dann "-Recht" gegenueber der Rolle, je in der Reihenfolge von Perm.All
        public static List<string> Deviations(HashSet<string> rights, RightsPackage r)
        {
            List<string> d = new List<string>();
            HashSet<string> has = new HashSet<string>(r.Rights);
            foreach (string p in Perm.All) if (rights.Contains(p) && !has.Contains(p)) d.Add("+" + p);
            foreach (string p in Perm.All) if (!rights.Contains(p) && has.Contains(p)) d.Add("-" + p);
            return d;
        }

        // Fuer Nutzer ohne Rolle (alte Datei): exakt gleiche Rechte, sonst wenigste Abweichungen, bei Gleichstand weniger Rechte
        public RightsPackage Closest(HashSet<string> rights)
        {
            RightsPackage best = null; int bd = int.MaxValue;
            foreach (RightsPackage p in Packages)
            {
                int d = Deviations(rights, p).Count;
                if (d < bd || (d == bd && p.Rights.Length < best.Rights.Length)) { best = p; bd = d; }
            }
            return best;
        }

        // Statusleiste/Info/Protokoll: "Produktion", "Produktion (+Export, -Format)", ab 3 Abweichungen "Produktion (angepasst)"
        public string RoleLabel(User u)
        {
            RightsPackage r = Role(u.RoleId);
            if (r == null) { r = PackageOf(u); return r != null ? Display(r) : "benutzerdefiniert"; }
            List<string> d = Deviations(u.Rights, r);
            if (d.Count == 0) return Display(r);
            if (d.Count > 2) return Display(r) + " (angepasst)";
            for (int i = 0; i < d.Count; i++) d[i] = d[i][0] + Perm.Short(d[i].Substring(1));
            return Display(r) + " (" + string.Join(", ", d.ToArray()) + ")";
        }

        public string RoleTooltip(User u)
        {
            RightsPackage r = Role(u.RoleId);
            StringBuilder sb = new StringBuilder("Rolle: " + (r != null ? Display(r) : RoleLabel(u)));
            if (r != null)
            {
                List<string> plus = new List<string>(), minus = new List<string>();
                foreach (string d in Deviations(u.Rights, r)) (d[0] == '+' ? plus : minus).Add(Perm.Label(d.Substring(1)));
                if (plus.Count > 0) sb.Append("\nZusätzlich: " + string.Join(", ", plus.ToArray()));
                if (minus.Count > 0) sb.Append("\nEntzogen: " + string.Join(", ", minus.ToArray()));
            }
            sb.Append("\n\nWirksame Rechte:");
            foreach (string p in Perm.All) if (u.Has(p)) sb.Append("\n  • " + Perm.Label(p));
            return sb.ToString();
        }

        public string FreeRoleId()
        {
            int n = 0, k;
            foreach (RightsPackage p in Packages)
                if (p.Id != null && p.Id.StartsWith("r") && int.TryParse(p.Id.Substring(1), out k)) n = Math.Max(n, k);
            return "r" + (n + 1);
        }

        public RightsPackage NewRole(string name, string[] rights)
        {
            RightsPackage r = new RightsPackage(FreeRoleId(), name, rights);
            Packages.Add(r);
            return r;
        }

        public UserDb Copy()
        {
            UserDb c = new UserDb();
            foreach (User u in Users) c.Users.Add(u.Clone());
            foreach (RightsPackage p in Packages) c.Packages.Add(p.Clone());
            c.Domains.AddRange(Domains);
            return c;
        }

        void Restore(UserDb b)
        {
            Users.Clear(); Users.AddRange(b.Users);
            Packages.Clear(); Packages.AddRange(b.Packages);
            Domains.Clear(); Domains.AddRange(b.Domains);
        }

        static bool SameSet(IEnumerable<string> a, IEnumerable<string> b) { return new HashSet<string>(a).SetEquals(b); }

        // Rechte, die sich zwischen a und b unterscheiden und die der Aendernde nicht selbst hat (null = keine)
        static string Foreign(IEnumerable<string> a, IEnumerable<string> b, HashSet<string> mine)
        {
            HashSet<string> x = new HashSet<string>(a); x.SymmetricExceptWith(b);
            List<string> bad = new List<string>();
            foreach (string p in x) if (!mine.Contains(p)) bad.Add(Perm.Label(p));
            return bad.Count > 0 ? string.Join(", ", bad.ToArray()) : null;
        }

        // Prueft diesen Stand gegen den Stand davor. by = wer aendert (null = nur Grundregeln). null = in Ordnung, sonst Meldung.
        public string Validate(UserDb before, User by)
        {
            if (before.AdminCount(null) > 0 && AdminCount(null) == 0) return LastAdmin;
            if (by == null) return null;
            User meBefore = before.FindByLogin(by.Login);
            HashSet<string> mine = meBefore != null ? meBefore.Rights : by.Rights;
            const string Own = ": Sie können nur Rechte vergeben oder entziehen, die Sie selbst haben (";
            foreach (RightsPackage r in Packages)
            {
                RightsPackage o = before.Role(r.Id);
                if (o != null && o.Name == r.Name && o.Active == r.Active && SameSet(o.Rights, r.Rights)) continue;
                if (!mine.Contains(Perm.PackageAdmin)) return "Rollen ändern erfordert das Recht \"" + Perm.Label(Perm.PackageAdmin) + "\".";
                string bad = Foreign(o != null ? o.Rights : new string[0], r.Rights, mine);
                if (bad != null) return "Rolle \"" + r.Name + "\"" + Own + bad + ").";
            }
            foreach (User u in Users)
            {
                User o = before.Users.Find(delegate(User x) { return x.Id == u.Id; });
                bool sameRole = o != null && o.RoleId == u.RoleId && SameSet(o.Plus, u.Plus) && SameSet(o.Minus, u.Minus);
                if (sameRole && o.Rights.SetEquals(u.Rights)) continue;
                if (!sameRole && o != null && string.Equals(u.Login, by.Login, StringComparison.OrdinalIgnoreCase))
                    return "Die eigene Rolle und die eigenen Rechte können nicht geändert werden.";
                string bad = Foreign(o != null ? o.Rights : new HashSet<string>(), u.Rights, mine);
                if (bad != null) return u.Display + Own + bad + ").";
                RightsPackage r = Role(u.RoleId);
                if ((o == null || o.RoleId != u.RoleId) && r != null && !r.Active)
                    return "Die Rolle \"" + r.Name + "\" ist beendet und kann nicht neu vergeben werden.";
            }
            return null;
        }

        // Aenderungen der Nutzerverwaltung nach Id in den aktuellen Stand einarbeiten (fremde parallele Aenderungen bleiben):
        // Rollen und Nutzer ersetzen/anhaengen, geloeschte Nutzer entfernen; neue Rollen/Nutzer, deren Id inzwischen vergeben ist,
        // bekommen eine freie. Danach Resolve + Validate; Fehler -> InvalidOperationException, Stand unveraendert.
        public void Merge(UserChanges c)
        {
            UserDb before = Copy(), keep = new UserDb();
            keep.Users.AddRange(Users); keep.Packages.AddRange(Packages); keep.Domains.AddRange(Domains);
            Dictionary<string, string> moved = new Dictionary<string, string>();
            foreach (RightsPackage r in c.Roles)
            {
                int i = Packages.FindIndex(delegate(RightsPackage x) { return x.Id == r.Id; });
                if (i >= 0 && Packages[i] == r) continue;
                if (i >= 0 && c.AddedRoles.Contains(r)) { string id = FreeRoleId(); moved[r.Id] = id; r.Id = id; i = -1; }
                if (i >= 0) Packages[i] = r; else Packages.Add(r);
            }
            if (c.Domains != null) { Domains.Clear(); Domains.AddRange(c.Domains); }
            Users.RemoveAll(delegate(User x) { return c.Deleted.Contains(x.Id); });
            foreach (User u in c.Changed)
            {
                string nid;
                if (moved.TryGetValue(u.RoleId, out nid)) u.RoleId = nid;
                int i = Users.FindIndex(delegate(User x) { return x.Id == u.Id; });
                if (i >= 0 && Users[i] == u) continue;
                if (i >= 0 && c.Added.Contains(u))
                {
                    foreach (User x in Users) u.Id = Math.Max(u.Id, x.Id + 1);
                    i = -1;
                }
                if (i >= 0) Users[i] = u; else Users.Add(u);
            }
            ResolveAll();
            string err = Validate(before, c.By);
            if (err == null) return;
            Restore(keep); ResolveAll();
            throw new InvalidOperationException(err);
        }
    }
}
