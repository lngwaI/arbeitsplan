#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class MainForm
    {
        static void SelfTestRoles(StringBuilder sb) { sb.AppendLine(RoleSelfTest.Run()); }
    }

    // Selbsttests Rollen (nur Core): Migration alter Dateien, Admin = alle Rechte, Beenden, paralleles Zusammenfuehren, Pruefregeln, Anzeige, Protokoll.
    static class RoleSelfTest
    {
        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }
        static bool Same(List<string> l, params string[] v) { return new HashSet<string>(l).SetEquals(v); }

        static string R(params string[] r) { StringBuilder s = new StringBuilder(); foreach (string x in r) s.Append("<R v=\"" + x + "\"/>"); return s.ToString(); }

        public static string Run()
        {
            // ---------- alte Datei (ohne id/rolle, ohne Rundgaenge) ----------
            string[] adminOld = Array.FindAll(Perm.All, delegate(string p) { return p != Perm.Rounds; });
            string xml = "<Nutzer><Paket name=\"Leser\">" + R(Perm.Read, Perm.Export) + "</Paket>"
                + "<Paket name=\"Produktion\">" + R(Perm.Read, Perm.EditText, Perm.DeleteText, Perm.AddText, Perm.Format, Perm.Export, Perm.ImportPackage) + "</Paket>"
                + "<Paket name=\"Admin\">" + R(adminOld) + "</Paket>"
                + "<Paket name=\"Schicht\">" + R(Perm.Read, Perm.Export, Perm.SortRows, Perm.EditHeight) + "</Paket>"
                + "<Person id=\"1\" login=\"a\">" + R(adminOld) + "</Person>"
                + "<Person id=\"2\" login=\"b\">" + R(Perm.Read, Perm.Export, Perm.EditText) + "</Person>"
                + "<Person id=\"3\" login=\"c\">" + R(Perm.Read, Perm.EditText, Perm.DeleteText, Perm.AddText, Perm.Export, Perm.ImportPackage) + "</Person>"
                + "<Person id=\"4\" login=\"d\">" + R(Perm.Read, Perm.Export, Perm.SortRows) + "</Person></Nutzer>";
            XmlDocument doc = new XmlDocument(); doc.LoadXml(xml);
            UserDb o = UserDb.ReadXml(doc.DocumentElement);
            User a = o.FindByLogin("a"), b = o.FindByLogin("b"), c = o.FindByLogin("c"), d = o.FindByLogin("d");
            bool okOld = o.Packages[0].Id == "leser" && o.Packages[1].Id == "produktion" && o.Packages[2].Id == "admin" && o.Packages[3].Id == "r1"
                && a.RoleId == "admin" && a.Plus.Count == 0 && a.Minus.Count == 0 && UserDb.IsAdmin(a.Rights)
                && b.RoleId == "leser" && Same(b.Plus, Perm.EditText, Perm.Rounds, Perm.SetStatus) && b.Minus.Count == 0                 // Rundgaenge wie bisher dazu
                && c.RoleId == "produktion" && c.Plus.Count == 0 && Same(c.Minus, Perm.Format)
                && d.RoleId == "leser" && Same(d.Plus, Perm.SortRows);                                                   // Gleichstand: weniger Rechte
            // Rundreise: neue Attribute bleiben, wirksame Rechte gleich
            o.Role("r1").Active = false;
            UserDb o2 = RoundTrip(o);
            User c2 = o2.FindByLogin("c");
            okOld &= c2.RoleId == "produktion" && Same(c2.Minus, Perm.Format) && c2.Rights.SetEquals(c.Rights) && !o2.Role("r1").Active && o2.Role("r1").Name == "Schicht";

            // ---------- Admin immer alle Rechte ----------
            UserDb s = UserDb.Sample();
            s.Role(UserDb.AdminId).Rights = new[] { Perm.Read }; s.ResolveAll();
            bool okAdm = s.Role(UserDb.AdminId).Rights.Length == Perm.All.Length && UserDb.IsAdmin(s.FindByLogin("mmuster").Rights) && o.Role("admin").Rights.Length == Perm.All.Length;

            // ---------- Rolle beenden: Nutzer unveraendert ----------
            User pw = s.FindByLogin("pwerk");
            HashSet<string> pwr = new HashSet<string>(pw.Rights);
            s.Role("produktion").Active = false; s.ResolveAll();
            bool okEnd = pw.Rights.SetEquals(pwr) && pw.RoleId == "produktion" && s.RoleLabel(pw) == "Produktion (beendet)";
            s.Role("produktion").Active = true; s.ResolveAll();

            // ---------- Anzeige ----------
            s.SetRights(pw, "produktion", Without(s.Role("produktion").Rights, Perm.Export));
            bool okLbl = s.RoleLabel(pw) == "Produktion (-Export)";
            List<string> l2 = Without(s.Role("produktion").Rights, Perm.Format); l2.Add(Perm.SortRows);
            s.SetRights(pw, "produktion", l2);
            okLbl &= s.RoleLabel(pw) == "Produktion (+Sortieren, -Format)" && pw.RoleText == s.RoleLabel(pw) && s.RoleTooltip(pw).Contains("Entzogen: " + Perm.Label(Perm.Format));
            l2.Remove(Perm.Export); s.SetRights(pw, "produktion", l2);
            okLbl &= s.RoleLabel(pw) == "Produktion (angepasst)";
            RightsPackage sch = s.NewRole("Schicht", new[] { Perm.Read });
            s.SetRights(pw, sch.Id, new[] { Perm.Read, Perm.Export });
            okLbl &= s.RoleLabel(pw) == "Schicht (+Export)" && Perm.Short(Perm.Export) == "Export";

            // ---------- zwei parallele Aenderungen (Rolle + Nutzer) bleiben beide ----------
            UserDb bas = UserDb.Sample(), pa = bas.Copy(), pb = bas.Copy();
            UserChanges ca = new UserChanges(), cb = new UserChanges();
            RightsPackage ra = pa.NewRole("Spät", new[] { Perm.Read }); ca.Roles.Add(ra); ca.AddedRoles.Add(ra);
            User ea = pa.FindByLogin("ebeispiel"); pa.SetRights(ea, ra.Id, new[] { Perm.Read }); ca.Changed.Add(ea);
            RightsPackage rb = pb.NewRole("Früh", new[] { Perm.Read, Perm.Export }); cb.Roles.Add(rb); cb.AddedRoles.Add(rb);   // gleiche Id r1
            User wb = pb.FindByLogin("pwerk"); pb.SetRights(wb, rb.Id, new[] { Perm.Read, Perm.Export }); cb.Changed.Add(wb);
            RightsPackage lb = pb.Role("leser").Clone(); lb.Name = "Lesend"; cb.Roles.Add(lb);
            bas.Merge(ca); bas.Merge(cb);
            User e1 = bas.FindByLogin("ebeispiel"), w1 = bas.FindByLogin("pwerk");
            bool okPar = bas.Packages.Count == 5 && bas.Role(e1.RoleId).Name == "Spät" && bas.Role(w1.RoleId).Name == "Früh" && e1.RoleId != w1.RoleId
                && bas.Role("leser").Name == "Lesend" && w1.Rights.SetEquals(new[] { Perm.Read, Perm.Export });

            // ---------- Pruefregeln ----------
            UserDb v = UserDb.Sample();
            UserChanges cl = new UserChanges(); User mm = v.FindByLogin("mmuster").Clone();
            v.SetRights(mm, "leser", new[] { Perm.Read, Perm.Export }); cl.Changed.Add(mm);
            bool thrown = false;
            try { v.Merge(cl); } catch (InvalidOperationException) { thrown = true; }
            bool okLast = thrown && UserDb.IsAdmin(v.FindByLogin("mmuster").Rights) && v.FindByLogin("mmuster").RoleId == "admin";

            UserDb before = v.Copy();
            User prod = v.FindByLogin("pwerk"); v.SetRights(prod, "produktion", Plus(prod.Rights, Perm.UserEdit)); before = v.Copy();   // Produktion + Nutzer bearbeiten
            User eb = v.FindByLogin("ebeispiel");
            v.SetRights(eb, "leser", new[] { Perm.Read, Perm.Export, Perm.PackageAdmin });
            bool okOwn = v.Validate(before, prod) != null;                                   // Recht, das pwerk nicht hat
            v.SetRights(eb, "leser", new[] { Perm.Read, Perm.Export, Perm.EditText });
            okOwn &= v.Validate(before, prod) == null;
            User self = v.FindByLogin("pwerk"); v.SetRights(self, "produktion", Plus(self.Rights, Perm.SortRows));
            okOwn &= v.Validate(before, prod.Clone()) != null;                                // eigene Zeile gesperrt
            v.SetRights(self, "produktion", before.FindByLogin("pwerk").Rights);
            RightsPackage rr = v.Role("leser").Clone(); rr.Name = "Anders"; v.Packages[0] = rr;
            okOwn &= v.Validate(before, prod) != null;                                         // Rollen nur mit PackageAdmin
            v.Packages[0] = before.Role("leser").Clone();
            // Merge mit Aendernden: unerlaubtes Recht -> Ausnahme, nichts uebernommen
            UserChanges cx = new UserChanges(); cx.By = prod; User ex = v.FindByLogin("ebeispiel").Clone();
            v.SetRights(ex, "leser", new[] { Perm.Read, Perm.Export, Perm.UserDelete }); cx.Changed.Add(ex);
            thrown = false; try { v.Merge(cx); } catch (InvalidOperationException) { thrown = true; }
            okOwn &= thrown && !v.FindByLogin("ebeispiel").Has(Perm.UserDelete);

            // beendete Rolle: nicht neu vergebbar, bisherige Nutzer behalten sie
            UserDb t = UserDb.Sample(); User tm = t.FindByLogin("mmuster");
            t.Role("leser").Active = false; UserDb tb = t.Copy();
            User te = t.FindByLogin("ebeispiel"); t.SetRights(te, "leser", new[] { Perm.Read, Perm.Export, Perm.EditText });
            bool okEnded = t.Validate(tb, tm) == null;
            User tp = t.FindByLogin("pwerk"); t.SetRights(tp, "leser", new[] { Perm.Read, Perm.Export });
            okEnded &= t.Validate(tb, tm) != null && te.RoleText.StartsWith("Leser (beendet)");

            // ---------- Protokoll: Rolle Rundreise ----------
            StoreData sd = new StoreData(); RowInfo row = new RowInfo(); row.Id = 1; row.Name = "Suite 1"; sd.Rows.Add(row);   // Sicherung braucht >= 1 Zeile
            AuditEntry ae = new AuditEntry(); ae.Time = DateTime.Now; ae.Login = "pwerk"; ae.Day = DateTime.Today; ae.RowId = 1; ae.Role = "Produktion (+Export)";
            AuditEntry old = new AuditEntry(); old.Time = DateTime.Now; old.Login = "pwerk"; old.Day = DateTime.Today; old.RowId = 2;
            sd.Audit.Add(ae); sd.Audit.Add(old);
            string f = Path.Combine(Path.GetTempPath(), "ap-rollen-" + Guid.NewGuid().ToString("N") + ".apbak");
            bool okAudit;
            try
            {
                Backup.Write(sd, f, null);
                StoreData back = Backup.Read(f, null);
                okAudit = back.Audit.Count == 2 && back.Audit[0].Role == "Produktion (+Export)" && back.Audit[1].Role == "";
            }
            finally { File.Delete(f); }
            User an = UserDb.Sample().FindByLogin("pwerk");
            okAudit &= Audit.New(DateTime.Today, new RowInfo(), an).Role == "Produktion";

            return "Rollen: alte Datei->Rolle/Plus/Minus=" + Ok(okOld) + " Admin alle Rechte=" + Ok(okAdm) + " beenden lässt Nutzer=" + Ok(okEnd)
                + " Anzeige=" + Ok(okLbl) + " parallel Rolle+Nutzer=" + Ok(okPar) + " letzter Admin=" + Ok(okLast)
                + " nur eigene Rechte/eigene Zeile=" + Ok(okOwn) + " beendete Rolle=" + Ok(okEnded) + " Protokoll-Rolle=" + Ok(okAudit);
        }

        static List<string> Without(string[] r, string x) { List<string> l = new List<string>(r); l.Remove(x); return l; }
        static List<string> Plus(HashSet<string> r, string x) { List<string> l = new List<string>(r); l.Add(x); return l; }

        static UserDb RoundTrip(UserDb db)
        {
            StringBuilder sb = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(sb)) db.WriteXml(w);
            XmlDocument doc = new XmlDocument(); doc.LoadXml(sb.ToString());
            return UserDb.ReadXml(doc.DocumentElement);
        }
    }
}
#endif
