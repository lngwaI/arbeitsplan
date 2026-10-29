using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Soft-Lock: nur Anzeige, wer gerade bearbeitet; blockiert nie.
    public interface ILockService
    {
        string Holder(DateTime day, int row, string me);   // Name eines anderen Bearbeiters oder null
        void Acquire(DateTime day, int row, string me);
        void Release(DateTime day, int row, string me);
        void AcquireAs(DateTime day, int row, string me, string login);   // wie Acquire, merkt zusaetzlich den Login (eindeutige Zuordnung)
        string HolderLogin(DateTime day, int row);                        // Login des aktuellen Halters oder "" (unbekannt, z. B. alte Sperre)
    }

    public class MemoryLockService : ILockService
    {
        class Lock { public string User, Login = ""; public DateTime Time; }
        static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);   // abgestuerzte Clients blockieren nicht ewig
        readonly Dictionary<string, Lock> locks = new Dictionary<string, Lock>();

        static string Key(DateTime d, int r) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + r; }

        public string Holder(DateTime day, int row, string me)
        {
            Lock l;
            if (locks.TryGetValue(Key(day, row), out l) && l.User != me && DateTime.Now - l.Time < Stale) return l.User;
            return null;
        }

        public void Acquire(DateTime day, int row, string me) { AcquireAs(day, row, me, ""); }

        public void AcquireAs(DateTime day, int row, string me, string login)
        {
            Lock l = new Lock(); l.User = me; l.Login = login ?? ""; l.Time = DateTime.Now;
            locks[Key(day, row)] = l;
        }

        public string HolderLogin(DateTime day, int row)
        {
            Lock l;
            return locks.TryGetValue(Key(day, row), out l) && DateTime.Now - l.Time < Stale ? l.Login : "";
        }

        public void Release(DateTime day, int row, string me)
        {
            Lock l;
            if (locks.TryGetValue(Key(day, row), out l) && l.User == me) locks.Remove(Key(day, row));
        }
    }
}
