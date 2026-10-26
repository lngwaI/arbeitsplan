using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Aenderungsprotokoll je Block (append-only): wer hat wann was gespeichert, Stand vorher/nachher.
    // Wird atomar mit dem Speichern geschrieben (IDataStore.SaveWithAudit), spaeter ebenso auf dem Server.
    public class AuditEntry
    {
        public DateTime Time;               // Speicherzeitpunkt (lokal)
        public string Login = "", Kuerzel = "";
        public DateTime Day;
        public int RowId;
        public string RowName = "";
        public Entry Before;                // null = Block war leer/neu
        public Entry After;
        public int Version;                 // Version nach dem Speichern

        public AuditEntry Clone()
        {
            AuditEntry a = (AuditEntry)MemberwiseClone();
            a.Before = Before == null ? null : Before.Clone();
            a.After = After == null ? null : After.Clone();
            return a;
        }

        // Eindeutig genug, um Protokolle zusammenzufuehren (Wiederherstellen einer Sicherung).
        public string Key() { return Time.Ticks + "|" + Day.ToString("yyyy-MM-dd") + "|" + RowId + "|" + Version + "|" + Login; }
    }

    public static class Audit
    {
        // Speichert wie IDataStore.Save und protokolliert im selben Schreibvorgang. false = Konflikt (nichts gespeichert/protokolliert).
        public static bool Save(IDataStore store, DateTime day, RowInfo row, User user, Entry e)
        {
            AuditEntry a = new AuditEntry();
            a.Time = DateTime.Now; a.Login = user.Login; a.Kuerzel = user.Kuerzel;
            a.Day = day.Date; a.RowId = row.Id; a.RowName = row.Name;
            return store.SaveWithAudit(day, row.Id, e, a);   // Vorher/Nachher/Version setzt der Store (atomar)
        }

        // Lesbarer Text eines Stands (Inhalt, Kuerzel je Schicht, LKZ).
        public static string Text(Entry e, string[] labels)
        {
            if (e == null) return "(leer)";
            string s = e.Content;
            for (int i = 0; i < 3; i++) s += "\n" + labels[i] + ": " + e.People[i];
            return s + "\nLKZ gemessen: " + (e.Lkz ? "ja" : "nein");
        }

        public static string Preview(Entry e)
        {
            if (e == null || e.IsEmpty) return "(leer)";
            string s = e.Content.Replace("\n", " | ");
            if (s.Length == 0) s = string.Join(" / ", e.People);
            return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
        }
    }
}
