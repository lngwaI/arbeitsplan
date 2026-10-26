using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Arbeitsplan.Core
{
    public static class Perm
    {
        public const string Read = "Lesen", EditText = "TextAendern", DeleteText = "TextLoeschen", AddText = "TextHinzufuegen",
            Format = "Format", SortRows = "ZeilenSortieren", PackageAdmin = "PaketeEinrichten", Export = "Exportieren", ImportPackage = "PaketeImportieren", EditNames = "BlockNamenAendern", EditHeight = "BlockhoeheAendern", UserCreate = "NutzerAnlegen", UserEdit = "NutzerBearbeiten", UserDelete = "NutzerLoeschen";

        public static readonly string[] All = { Read, EditText, DeleteText, AddText, Format, EditNames, EditHeight, SortRows, Export, ImportPackage, PackageAdmin, UserCreate, UserEdit, UserDelete };

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
            }
            return r;
        }
    }

    public class User
    {
        public int Id;
        public string First = "", Last = "", Login = "", Kuerzel = "";
        public HashSet<string> Rights = new HashSet<string>();

        public string Display
        {
            get { string n = (First + " " + Last).Trim(); return n.Length > 0 ? n : Login; }
        }
        public bool Has(string right) { return Rights.Contains(right); }
    }

    public class RightsPackage
    {
        public string Name;
        public string[] Rights;
        public RightsPackage(string name, params string[] rights) { Name = name; Rights = rights; }
    }

    // In-Memory-Nutzerdatenbank (spaeter Server-DB).
    public class UserDb
    {
        public readonly List<User> Users = new List<User>();
        public readonly List<RightsPackage> Packages = new List<RightsPackage>();

        public User FindByLogin(string login)
        {
            foreach (User u in Users)
                if (string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase)) return u;
            return null;
        }

        public RightsPackage Package(string name)
        {
            foreach (RightsPackage p in Packages) if (p.Name == name) return p;
            return null;
        }

        public User Add(string first, string last, string login, string package)
        {
            User u = new User();
            foreach (User x in Users) u.Id = Math.Max(u.Id, x.Id);
            u.Id++; u.First = first; u.Last = last; u.Login = login;
            ApplyPackage(u.Rights, Package(package));
            Users.Add(u);
            return u;
        }

        public static void ApplyPackage(HashSet<string> rights, RightsPackage p)
        {
            rights.Clear();
            if (p != null) foreach (string r in p.Rights) rights.Add(r);
        }

        // "ausgewaehlt" nur bei exakt gleichen Rechten.
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

        // Aenderungen der Nutzerverwaltung nach Id in den aktuellen Stand einarbeiten (fremde parallele Aenderungen bleiben):
        // geaenderte ersetzen/anhaengen, geloeschte entfernen; neue Nutzer, deren Id inzwischen vergeben ist, bekommen eine freie.
        public void Merge(UserChanges c)
        {
            Users.RemoveAll(delegate(User x) { return c.Deleted.Contains(x.Id); });
            foreach (User u in c.Changed)
            {
                int i = Users.FindIndex(delegate(User x) { return x.Id == u.Id; });
                if (i >= 0 && Users[i] == u) continue;
                if (i >= 0 && c.Added.Contains(u))
                {
                    foreach (User x in Users) u.Id = Math.Max(u.Id, x.Id + 1);
                    i = -1;
                }
                if (i >= 0) Users[i] = u; else Users.Add(u);
            }
        }

        // Nur Rechtepakete, keine Nutzer (Erststart der Datendatei)
        public static UserDb Default()
        {
            UserDb db = new UserDb();
            db.Packages.Add(new RightsPackage("Leser", Perm.Read, Perm.Export));
            db.Packages.Add(new RightsPackage("Produktion", Perm.Read, Perm.EditText, Perm.DeleteText, Perm.AddText, Perm.Format, Perm.Export, Perm.ImportPackage));
            db.Packages.Add(new RightsPackage("Admin", Perm.All));
            return db;
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteStartElement("Nutzer");
            foreach (RightsPackage p in Packages)
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("name", p.Name);
                foreach (string r in p.Rights) { w.WriteStartElement("R"); w.WriteAttributeString("v", r); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (User u in Users)
            {
                w.WriteStartElement("Person"); w.WriteAttributeString("id", u.Id.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("vorname", u.First); w.WriteAttributeString("nachname", u.Last); w.WriteAttributeString("login", u.Login); w.WriteAttributeString("kuerzel", u.Kuerzel);
                foreach (string r in u.Rights) { w.WriteStartElement("R"); w.WriteAttributeString("v", r); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        public static UserDb ReadXml(XmlElement e)
        {
            UserDb db = new UserDb();
            foreach (XmlNode n in e.ChildNodes)
            {
                XmlElement x = n as XmlElement;
                if (x == null) continue;
                List<string> rights = new List<string>();
                foreach (XmlNode r in x.ChildNodes) { XmlElement re = r as XmlElement; if (re != null && re.Name == "R") rights.Add(re.GetAttribute("v")); }
                if (x.Name == "Paket") db.Packages.Add(new RightsPackage(x.GetAttribute("name"), rights.ToArray()));
                else if (x.Name == "Person")
                {
                    User u = new User(); u.Id = int.Parse(x.GetAttribute("id"), CultureInfo.InvariantCulture);
                    u.First = x.GetAttribute("vorname"); u.Last = x.GetAttribute("nachname"); u.Login = x.GetAttribute("login"); u.Kuerzel = x.GetAttribute("kuerzel");
                    foreach (string r in rights) u.Rights.Add(r);
                    db.Users.Add(u);
                }
            }
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

    // Was die Nutzerverwaltung geaendert hat (Changed enthaelt auch die neuen Nutzer aus Added).
    public class UserChanges
    {
        public readonly HashSet<User> Changed = new HashSet<User>(), Added = new HashSet<User>();
        public readonly HashSet<int> Deleted = new HashSet<int>();
        public bool Any { get { return Changed.Count > 0 || Deleted.Count > 0; } }
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

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd") + "|" + r; }

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
