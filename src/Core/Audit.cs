using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arbeitsplan.Core
{
    // Aenderungsprotokoll je Block (append-only): wer hat wann was gespeichert, Stand vorher/nachher.
    // Wird atomar mit dem Speichern geschrieben (IDataStore.SaveWithAudit), spaeter ebenso auf dem Server.
    public class AuditEntry
    {
        public DateTime Time;               // Speicherzeitpunkt (lokal)
        public string Login = "", Kuerzel = "";
        public string Role = "";            // Rolle beim Speichern (User.RoleText), alte Eintraege leer
        public DateTime Day;
        public int RowId;
        public string RowName = "";
        public Entry Before;                // null = Block war leer/neu
        public Entry After;
        public int Version;                 // Version nach dem Speichern
        // Wochenhaken (Rundgang): RowId = -1, Day = Montag, Mark = Schluessel, Value = "1"/"0"; sonst leer
        public string Mark = "", Value = "";

        public AuditEntry Clone()
        {
            AuditEntry a = (AuditEntry)MemberwiseClone();
            a.Before = Before == null ? null : Before.Clone();
            a.After = After == null ? null : After.Clone();
            return a;
        }

        // Eindeutig genug, um Protokolle zusammenzufuehren (Wiederherstellen einer Sicherung).
        public string Key() { return Time.Ticks + "|" + Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + RowId + "|" + Version + "|" + Login + (Mark.Length > 0 ? "|" + Mark : ""); }
    }

    public static class Audit
    {
        // Speichert wie IDataStore.Save und protokolliert im selben Schreibvorgang. false = Konflikt (nichts gespeichert/protokolliert).
        public static bool Save(IDataStore store, DateTime day, RowInfo row, User user, Entry e)
        {
            return store.SaveWithAudit(day, row.Id, e, New(day, row, user));   // Vorher/Nachher/Version setzt der Store (atomar)
        }

        // Protokolleintrag ohne Vorher/Nachher (setzt der Store beim Speichern).
        public static AuditEntry New(DateTime day, RowInfo row, User user)
        {
            AuditEntry a = new AuditEntry();
            a.Time = DateTime.Now; a.Login = user.Login; a.Kuerzel = user.Kuerzel; a.Role = user.RoleText;
            a.Day = day.Date; a.RowId = row.Id; a.RowName = row.Name;
            return a;
        }

        // Lesbarer Text eines Stands (Inhalt, Kuerzel je Schicht, LKZ).
        public static string Text(Entry e, string[] labels)
        {
            if (e == null) return "(leer)";
            string s = e.Content;
            if (e.HasNotes)   // Notizzeilen kenntlich machen
            {
                List<int[]> nl = Notes.NoteLines(e);
                string[] ls = e.Content.Split('\n');
                foreach (int[] n in nl) ls[n[0]] = "[Notiz] " + ls[n[0]];
                s = string.Join("\n", ls);
            }
            for (int i = 0; i < 3; i++) s += "\n" + labels[i] + ": " + e.People[i];
            s += "\nLKZ gemessen: " + (e.Lkz ? "ja" : "nein");
            foreach (string l in BufferSet.DisplayLines(e.Buffers, true)) s += "\n" + l;
            if (e.Marks.Count > 0) s += "\nAbgehakt: " + string.Join(", ", e.Marks.ToArray());
            return s;
        }

        // Wurde nur die Notiz-Markierung geaendert (Text, Kuerzel, LKZ gleich)? "Notiz markiert" / "Notiz aufgehoben", sonst "".
        public static string NoteChange(AuditEntry a)
        {
            Entry b = a.Before ?? new Entry(), n = a.After ?? new Entry();
            if (b.Content != n.Content || b.People[0] != n.People[0] || b.People[1] != n.People[1] || b.People[2] != n.People[2] || b.Lkz != n.Lkz || BufferSet.Key(b.Buffers) != BufferSet.Key(n.Buffers)) return "";
            int cb = Notes.NoteLines(b).Count, cn = Notes.NoteLines(n).Count;
            return cn > cb ? "Notiz markiert" : cn < cb ? "Notiz aufgehoben" : "";
        }

        // Zeile im Verlauf: Notiz-Aenderung oder Vorschau des neuen Stands
        public static string Summary(AuditEntry a)
        {
            string c = NoteChange(a);
            if (c.Length == 0) c = BufferSet.MadeChange(a.Before ?? new Entry(), a.After ?? new Entry());
            return c.Length > 0 ? c + ": " + Preview(a.After) : Preview(a.After);
        }

        public static string Preview(Entry e)
        {
            if (e == null) return "(leer)";
            string pre = "", bufs = string.Join(" / ", BufferSet.DisplayLines(e.Buffers, false).ToArray());
            if (e.IsEmpty)
            {
                if (!e.HasNotes && e.Buffers.Count == 0) return "(leer)";
                if (e.HasNotes) pre = "(nur Notizen) ";   // Notizen allein zaehlen nicht als Inhalt
            }
            string s = pre + e.Content.Replace("\n", " | ");
            if (e.IsEmpty && bufs.Length > 0) s = (e.HasNotes ? s + " | " : "") + bufs;
            else if (s.Length == 0) s = string.Join(" / ", e.People);
            return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
        }
    }
}
