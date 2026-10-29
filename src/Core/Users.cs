using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Arbeitsplan.Core
{
    public static class Perm
    {
        public const string Read = "Lesen", EditText = "TextAendern", DeleteText = "TextLoeschen", AddText = "TextHinzufuegen",
            Format = "Format", SortRows = "ZeilenSortieren", PackageAdmin = "PaketeEinrichten", Export = "Exportieren", ImportPackage = "PaketeImportieren", EditNames = "BlockNamenAendern", EditHeight = "BlockhoeheAendern", UserCreate = "NutzerAnlegen", UserEdit = "NutzerBearbeiten", UserDelete = "NutzerLoeschen", Rounds = "RundgangAbhaken", SetStatus = "StatusSetzen", TakeOver = "BearbeitungUebernehmen";

        public static readonly string[] All = { Read, EditText, DeleteText, AddText, Format, EditNames, EditHeight, SortRows, Export, ImportPackage, Rounds, SetStatus, TakeOver, PackageAdmin, UserCreate, UserEdit, UserDelete };

        public static string Label(string r)
        {
            switch (r)
            {
                case Read: return "Lesen";
                case EditText: return "Text ändern";
                case DeleteText: return "Text löschen";
                case AddText: return "Text hinzufügen";
                case Format: return "Farben / Textformat";
                case EditNames: return "Blockname / F-S-N ändern, Zeile hinzufügen";
                case EditHeight: return "Blockhöhe einstellen";
                case SortRows: return "Zeilen sortieren";
                case Export: return "Exportieren (Bild, PDF, Paket)";
                case ImportPackage: return "Pakete importieren / in Plan übernehmen";
                case PackageAdmin: return "Pakete einrichten (Equipment, Bausteine)";
                case UserCreate: return "Nutzer anlegen";
                case UserEdit: return "Nutzer bearbeiten";
                case UserDelete: return "Nutzer löschen";
                case Rounds: return "Rundgänge abhaken";
                case SetStatus: return "Equipment-Status setzen";
                case TakeOver: return "Bearbeitung übernehmen (Bearbeiter ablösen)";
            }
            return r;
        }

        // Kurzname fuer die Rollenanzeige, z. B. "Produktion (+Export, -Format)"
        public static string Short(string r)
        {
            switch (r)
            {
                case Read: return "Lesen";
                case EditText: return "Ändern";
                case DeleteText: return "Löschen";
                case AddText: return "Hinzufügen";
                case EditNames: return "Blocknamen";
                case EditHeight: return "Blockhöhe";
                case SortRows: return "Sortieren";
                case Export: return "Export";
                case ImportPackage: return "Import";
                case PackageAdmin: return "Pakete einrichten";
                case UserCreate: return "Nutzer anlegen";
                case UserEdit: return "Nutzer bearbeiten";
                case UserDelete: return "Nutzer löschen";
                case Rounds: return "Rundgänge";
                case SetStatus: return "Status";
                case TakeOver: return "Übernehmen";
            }
            return r;
        }
    }

    public class User
    {
        public int Id;
        public string First = "", Last = "", Login = "", Kuerzel = "";
        public string RoleId = "";                                   // Rolle (RightsPackage.Id); nie leer bei gespeicherten Nutzern
        public List<string> Plus = new List<string>(), Minus = new List<string>();   // Einzelrechte zusaetzlich/entzogen
        public HashSet<string> Rights = new HashSet<string>();      // wirksam = Rolle + Plus - Minus (UserDb.Resolve)
        public string RoleText = "";                                 // Rollenanzeige (UserDb.Resolve), z. B. fuer das Protokoll

        public string Display
        {
            get { string n = (First + " " + Last).Trim(); return n.Length > 0 ? n : Login; }
        }
        public bool Has(string right) { return Rights.Contains(right); }

        public User Clone()
        {
            User c = (User)MemberwiseClone();
            c.Rights = new HashSet<string>(Rights); c.Plus = new List<string>(Plus); c.Minus = new List<string>(Minus);
            return c;
        }

        public void CopyFrom(User o)
        {
            User c = o.Clone();
            Id = c.Id; First = c.First; Last = c.Last; Login = c.Login; Kuerzel = c.Kuerzel;
            RoleId = c.RoleId; Plus = c.Plus; Minus = c.Minus; Rights = c.Rights; RoleText = c.RoleText;
        }
    }

    // Rolle (frueher "Rechtepaket"): feste Ids "leser", "produktion", "admin", eigene "r<n>".
    // Nie geloescht, nur beendet (Active = false). "admin" hat immer alle Rechte (UserDb.ResolveAll).
    public class RightsPackage
    {
        public string Id, Name;
        public string[] Rights;
        public bool Active = true;
        public bool Builtin { get { return Id == "leser" || Id == "produktion" || Id == UserDb.AdminId; } }
        public RightsPackage(string id, string name, params string[] rights) { Id = id; Name = name; Rights = rights; }
        public RightsPackage Clone() { RightsPackage c = (RightsPackage)MemberwiseClone(); c.Rights = (string[])Rights.Clone(); return c; }
    }

    // In-Memory-Nutzerdatenbank (spaeter Server-DB). Rollenlogik in Roles.cs.
    public partial class UserDb
    {
        public readonly List<User> Users = new List<User>();
        public readonly List<RightsPackage> Packages = new List<RightsPackage>();
        // Erlaubte Windows-Domaenen/Rechner (Umgebung.Allowed), leer = keine Pruefung; der erste Admin traegt seine Domaene ein
        public readonly List<string> Domains = new List<string>();

        public User FindByLogin(string login)
        {
            foreach (User u in Users)
                if (string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase)) return u;
            return null;
        }

        // Rolle nach Id, sonst nach Name (Gross/Klein egal)
        public RightsPackage Package(string idOrName)
        {
            foreach (RightsPackage p in Packages) if (p.Id == idOrName) return p;
            foreach (RightsPackage p in Packages) if (string.Equals(p.Name, idOrName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public User Add(string first, string last, string login, string role)
        {
            User u = new User();
            foreach (User x in Users) u.Id = Math.Max(u.Id, x.Id);
            u.Id++; u.First = first; u.Last = last; u.Login = login;
            Assign(u, role);
            Users.Add(u);
            return u;
        }

        // exakt gleiche Rechte (Rueckfall fuer Nutzer ohne Rolle)
        public static bool Matches(HashSet<string> rights, RightsPackage p)
        {
            if (rights.Count != p.Rights.Length) return false;
            foreach (string r in p.Rights) if (!rights.Contains(r)) return false;
            return true;
        }

        public RightsPackage PackageOf(User u)
        {
            foreach (RightsPackage p in Packages) if (Matches(u.Rights, p)) return p;
            return null;
        }

        // Admin = alle Rechte
        public static bool IsAdmin(HashSet<string> rights) { foreach (string r in Perm.All) if (!rights.Contains(r)) return false; return true; }
        public int AdminCount(User except) { int n = 0; foreach (User u in Users) if (u != except && IsAdmin(u.Rights)) n++; return n; }

        // Nur Rollen, keine Nutzer (Erststart der Datendatei)
        public static UserDb Default()
        {
            UserDb db = new UserDb();
            db.Packages.Add(new RightsPackage("leser", "Leser", Perm.Read, Perm.Export));
            db.Packages.Add(new RightsPackage("produktion", "Produktion", Perm.Read, Perm.EditText, Perm.DeleteText, Perm.AddText, Perm.Format, Perm.Export, Perm.ImportPackage, Perm.Rounds, Perm.SetStatus, Perm.TakeOver));
            db.Packages.Add(new RightsPackage(AdminId, "Admin", (string[])Perm.All.Clone()));
            return db;
        }

        // Rolle id/name/aktiv; Person rolle/plus/minus. Die R-Kinder bleiben (wirksame Rechte) als Rueckfall fuer aeltere Programmstaende.
        public void WriteXml(XmlWriter w)
        {
            w.WriteStartElement("Nutzer");
            w.WriteAttributeString("rechte", "4");   // 2: mit Perm.Rounds; 3: mit Perm.SetStatus; 4: mit Perm.TakeOver (Rechte sind Texte, aeltere Programme ignorieren das neue)
            if (Domains.Count > 0) w.WriteAttributeString("domaenen", string.Join(",", Domains.ToArray()));
            foreach (RightsPackage p in Packages)
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("id", p.Id); w.WriteAttributeString("name", p.Name);
                if (!p.Active) w.WriteAttributeString("aktiv", "0");
                foreach (string r in p.Rights) { w.WriteStartElement("R"); w.WriteAttributeString("v", r); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (User u in Users)
            {
                w.WriteStartElement("Person"); w.WriteAttributeString("id", u.Id.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("vorname", u.First); w.WriteAttributeString("nachname", u.Last); w.WriteAttributeString("login", u.Login); w.WriteAttributeString("kuerzel", u.Kuerzel);
                w.WriteAttributeString("rolle", u.RoleId);
                if (u.Plus.Count > 0) w.WriteAttributeString("plus", string.Join(",", u.Plus.ToArray()));
                if (u.Minus.Count > 0) w.WriteAttributeString("minus", string.Join(",", u.Minus.ToArray()));
                foreach (string r in u.Rights) { w.WriteStartElement("R"); w.WriteAttributeString("v", r); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        public static UserDb ReadXml(XmlElement e)
        {
            UserDb db = new UserDb();
            db.Domains.AddRange(Umgebung.SplitList(e.GetAttribute("domaenen")));
            foreach (XmlNode n in e.ChildNodes)
            {
                XmlElement x = n as XmlElement;
                if (x == null) continue;
                List<string> rights = new List<string>();
                foreach (XmlNode r in x.ChildNodes) { XmlElement re = r as XmlElement; if (re != null && re.Name == "R") rights.Add(re.GetAttribute("v")); }
                int rv; int.TryParse(e.GetAttribute("rechte"), out rv);
                if (rv == 0 && rights.Contains(Perm.EditText)) rights.Add(Perm.Rounds);   // alte Datei: Produktion/Admin bekommen das neue Recht
                if (rv < 3 && rights.Contains(Perm.EditText)) rights.Add(Perm.SetStatus);
                if (rv < 4 && rights.Contains(Perm.EditText)) rights.Add(Perm.TakeOver);
                if (x.Name == "Paket")
                {
                    string name = x.GetAttribute("name"), id = x.GetAttribute("id");
                    if (id.Length == 0) id = name == "Leser" ? "leser" : name == "Produktion" ? "produktion" : name == "Admin" ? AdminId : "";   // alte Datei: feste Rollen ueber den Namen
                    RightsPackage p = new RightsPackage(id, name, rights.ToArray());
                    p.Active = x.GetAttribute("aktiv") != "0";
                    db.Packages.Add(p);
                }
                else if (x.Name == "Person")
                {
                    User u = new User(); u.Id = int.Parse(x.GetAttribute("id"), CultureInfo.InvariantCulture);
                    u.First = x.GetAttribute("vorname"); u.Last = x.GetAttribute("nachname"); u.Login = x.GetAttribute("login"); u.Kuerzel = x.GetAttribute("kuerzel");
                    u.RoleId = x.GetAttribute("rolle"); u.Plus = Umgebung.SplitList(x.GetAttribute("plus")); u.Minus = Umgebung.SplitList(x.GetAttribute("minus"));
                    foreach (string r in rights) u.Rights.Add(r);
                    db.Users.Add(u);
                }
            }
            foreach (RightsPackage p in db.Packages) if (p.Id.Length == 0) p.Id = db.FreeRoleId();
            db.ResolveAll();   // admin = alle Rechte, bevor Nutzer ohne Rolle zugeordnet werden
            foreach (User u in db.Users)
                if (u.RoleId.Length == 0)
                {   // alte Datei: Rolle mit den wenigsten Abweichungen, Abweichungen als Plus/Minus (wirksame Rechte bleiben gleich)
                    RightsPackage r = db.Closest(u.Rights);
                    if (r != null) db.SetRights(u, r.Id, new List<string>(u.Rights));
                }
            db.ResolveAll();
            return db;
        }

        public static UserDb Sample()
        {
            UserDb db = Default();
#if TESTHOOKS
            db.Add("Max", "Mustermann", "mmuster", "Admin").Kuerzel = "MUMA";
            db.Add("Erika", "Beispiel", "ebeispiel", "Leser").Kuerzel = "BEER";
            db.Add("Paula", "Werk", "pwerk", "Produktion").Kuerzel = "WEPA";
#endif
            return db;
        }
    }

    // Was die Nutzerverwaltung geaendert hat (Changed enthaelt auch die neuen Nutzer aus Added, Roles die neuen Rollen aus AddedRoles).
    public class UserChanges
    {
        public readonly HashSet<User> Changed = new HashSet<User>(), Added = new HashSet<User>();
        public readonly HashSet<int> Deleted = new HashSet<int>();
        public readonly HashSet<RightsPackage> Roles = new HashSet<RightsPackage>(), AddedRoles = new HashSet<RightsPackage>();
        public List<string> Domains;   // null = unveraendert
        public User By;                // wer aendert: Merge prueft "im Rahmen der eigenen Rechte"; null = nur Grundregeln
        public bool Any { get { return Changed.Count > 0 || Deleted.Count > 0 || Roles.Count > 0 || Domains != null; } }
    }

    // Soft-Lock: nur Anzeige, wer gerade bearbeitet; blockiert nie.
    public interface ILockService
    {
        string Holder(DateTime day, int row, string me);   // Name eines anderen Bearbeiters oder null
        void Acquire(DateTime day, int row, string me);
        void Release(DateTime day, int row, string me);
    }

    public class MemoryLockService : ILockService
    {
        class Lock { public string User; public DateTime Time; }
        static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);   // abgestuerzte Clients blockieren nicht ewig
        readonly Dictionary<string, Lock> locks = new Dictionary<string, Lock>();

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + r; }

        public string Holder(DateTime day, int row, string me)
        {
            Lock l;
            if (locks.TryGetValue(Key(day, row), out l) && l.User != me && DateTime.Now - l.Time < Stale) return l.User;
            return null;
        }

        public void Acquire(DateTime day, int row, string me)
        {
            Lock l = new Lock(); l.User = me; l.Time = DateTime.Now;
            locks[Key(day, row)] = l;
        }

        public void Release(DateTime day, int row, string me)
        {
            Lock l;
            if (locks.TryGetValue(Key(day, row), out l) && l.User == me) locks.Remove(Key(day, row));
        }
    }
}
