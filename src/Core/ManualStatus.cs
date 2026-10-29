using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Von Hand gesetzter Status einer Equipment-Nummer (Uebergang bis zur MES-Schnittstelle). Cleared = manuellen Wert entfernen.
    public class DeviceStatus
    {
        public string Id = "", By = "";
        public EquipStatus Status;
        public DateTime At;
        public bool Cleared;
        public DeviceStatus Clone() { return (DeviceStatus)MemberwiseClone(); }
    }

    // Manueller Status vor der Schnittstelle: Werte aus dem IDataStore haben Vorrang, sonst fragt er die darunterliegende Quelle (inner).
    // Status wird nie als Text im Plan angezeigt, nur als Farbe der Nummer.
    public class ManualStatusProvider : IEquipmentStatusProvider
    {
        // Uebergangspruefung (EquipMaster.IsLegal) im Hintergrund; Standard AUS (ini Uebergangspruefung=1). Keine Meldung, nur Vermerk im Protokoll.
        public static bool CheckTransitions;

        readonly IDataStore store;
        readonly IEquipmentStatusProvider inner;
        Dictionary<string, EquipmentInfo> manual = new Dictionary<string, EquipmentInfo>();

        public ManualStatusProvider(IDataStore store, IEquipmentStatusProvider inner) { this.store = store; this.inner = inner; Reload(); }

        static string Key(string n) { return (n ?? "").Trim().ToUpperInvariant(); }

        // Werte neu aus dem Store lesen (beim Neuaufbau der Ansicht)
        public void Reload()
        {
            Dictionary<string, EquipmentInfo> m = new Dictionary<string, EquipmentInfo>();
            try { foreach (DeviceStatus s in store.GetStatuses()) m[Key(s.Id)] = new EquipmentInfo(s.Id, s.Status, s.At); }
            catch (Exception) { return; }   // Store nicht lesbar: alter Stand bleibt
            manual = m;
        }

        public bool IsManual(string number) { return manual.ContainsKey(Key(number)); }

        // Manuell gilt vor der Quelle darunter, solange der Eintrag hoechstens ManualDays Tage alt ist (ini ManuellTage=); danach gilt die Quelle.
        public int ManualDays = 14;
        public Func<DateTime> Clock = delegate { return DateTime.Now; };

        public EquipmentInfo Get(string number)
        {
            EquipmentInfo i;
            if (manual.TryGetValue(Key(number), out i) && (inner == null || Clock() - i.Timestamp <= TimeSpan.FromDays(ManualDays))) return i;
            return inner != null ? inner.Get(number) : null;
        }

        public List<string> ListNumbers()
        {
            List<string> l = inner != null ? inner.ListNumbers() : new List<string>();
            foreach (EquipmentInfo i in manual.Values) if (!l.Contains(i.Number)) l.Add(i.Number);
            l.Sort();
            return l;
        }

        // Status setzen (st == null: manuellen Wert entfernen). false = nicht gespeichert (Store-Konflikt).
        public bool Set(string number, EquipStatus? st, User by)
        {
            EquipmentInfo cur = Get(number);
            DeviceStatus s = new DeviceStatus(); s.Id = number; s.At = DateTime.Now; s.By = by.Login; s.Cleared = st == null;
            if (st != null) s.Status = st.Value;
            AuditEntry a = new AuditEntry();
            a.Time = DateTime.Now; a.Login = by.Login; a.Kuerzel = by.Kuerzel; a.Role = by.RoleText;
            a.Day = DateTime.Today; a.RowId = AuditRow; a.RowName = "Equipment-Status"; a.Mark = number;
            a.Value = st == null ? "" : st.Value.ToString();
            if (CheckTransitions && st != null && cur != null && !store.GetCatalog().Master.IsLegal(number, cur.Status, st.Value)) a.Value += "!";   // Hintergrund: nur vermerkt
            bool ok = store.SetStatus(s, a);
            if (ok) Reload();
            return ok;
        }

        public const int AuditRow = -2;   // Protokollzeile fuer Statusaenderungen (-1 = Wochenhaken)

        public static string Label(EquipStatus s)
        {
            switch (s)
            {
                case EquipStatus.NichtGereinigt: return "Nicht gereinigt";
                case EquipStatus.NichtGereinigtOhneTimer: return "Nicht gereinigt (ohne Timer)";
                case EquipStatus.Gereinigt: return "Gereinigt";
                case EquipStatus.Regeneriert: return "Regeneriert";
                case EquipStatus.Sterilisiert: return "Sterilisiert";
                case EquipStatus.InProzess: return "In Prozess";
                case EquipStatus.Freigabe: return "Freigabe";
                case EquipStatus.Quarantaene: return "Quarantäne";
            }
            return s.ToString();
        }
    }
}
