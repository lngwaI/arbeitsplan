using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    public static class Perm
    {
        public const string Read = "Lesen", EditText = "TextAendern", DeleteText = "TextLoeschen", AddText = "TextHinzufuegen",
            Format = "Format", SortRows = "ZeilenSortieren", EditNames = "BlockNamenAendern", EditHeight = "BlockhoeheAendern", UserCreate = "NutzerAnlegen", UserEdit = "NutzerBearbeiten", UserDelete = "NutzerLoeschen";

        public static readonly string[] All = { Read, EditText, DeleteText, AddText, Format, EditNames, EditHeight, SortRows, UserCreate, UserEdit, UserDelete };

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
        int nextId = 1;

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
            u.Id = nextId++; u.First = first; u.Last = last; u.Login = login;
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

        public static UserDb Sample()
        {
            UserDb db = new UserDb();
            db.Packages.Add(new RightsPackage("Leser", Perm.Read));
            db.Packages.Add(new RightsPackage("Produktion", Perm.Read, Perm.EditText, Perm.DeleteText, Perm.AddText, Perm.Format));
            db.Packages.Add(new RightsPackage("Admin", Perm.All));
            db.Add("Max", "Mustermann", "mmuster", "Admin").Kuerzel = "MUMA";
            db.Add("Erika", "Beispiel", "ebeispiel", "Leser").Kuerzel = "BEER";
            db.Add("Paula", "Werk", "pwerk", "Produktion").Kuerzel = "WEPA";
            return db;
        }
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
