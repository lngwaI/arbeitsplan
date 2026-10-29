using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Arbeitsplan.Core
{
    // Equipment-Status aus einer CSV-Datei (MES -> Datei -> Arbeitsplan, keine Direktverbindung). Format und Regeln: docs/Equipment-Status.md.
    // Standard "nicht gereinigt" (ohne Ablauf): Geraet ohne Eintrag, Eintrag abgelaufen, Datei fehlt/unlesbar/zu alt.
    // Neu laden hoechstens alle 60 s im Hintergrund (nur bei geaenderter Zeit/Groesse); Get blockiert nie.
    public class FileStatusProvider : IEquipmentStatusProvider
    {
        class Entry { public EquipStatus Status; public DateTime At; public DateTime? Until; }

        class Snap
        {
            public Dictionary<string, Entry> Map = new Dictionary<string, Entry>();
            public bool Exists, Readable;
            public DateTime FileTime;
            public long Size;
        }

        public const int ReloadSeconds = 60;
        static readonly string[] Formats = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm" };

        readonly string path;
        readonly TimeSpan maxAge;
        volatile Snap snap = new Snap();
        DateTime lastCheck = DateTime.MinValue;
        int running;

        public Func<DateTime> Clock = delegate { return DateTime.Now; };   // Tests

        public FileStatusProvider(string path, int maxAgeHours)
        {
            this.path = path; maxAge = TimeSpan.FromHours(maxAgeHours > 0 ? maxAgeHours : 24);
            Reload();
        }

        static string Norm(string s)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in (s ?? "").ToLowerInvariant())
            {
                if (c == 'ä') b.Append("ae"); else if (c == 'ö') b.Append("oe"); else if (c == 'ü') b.Append("ue"); else if (c == 'ß') b.Append("ss");
                else if (char.IsLetterOrDigit(c)) b.Append(c);
            }
            return b.ToString();
        }

        static string Key(string n) { return (n ?? "").Trim().ToUpperInvariant(); }

        // Status aus Namen oder Anzeigetext ("Gereinigt", "nicht gereinigt", "In Prozess", "Quarantaene"); sonst false
        public static bool TryStatus(string text, out EquipStatus st)
        {
            string n = Norm(text);
            foreach (EquipStatus s in Enum.GetValues(typeof(EquipStatus)))
                if (Norm(s.ToString()) == n || Norm(ManualStatusProvider.Label(s)) == n) { st = s; return true; }
            st = EquipStatus.NichtGereinigt; return false;
        }

        static bool TryTime(string text, out DateTime t)
        {
            return DateTime.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
        }

        // Eine CSV-Zeile; false = leer/Kopfzeile/ungueltig (uebersprungen)
        static bool ParseLine(string line, DateTime fileTime, out string id, out Entry e)
        {
            id = null; e = null;
            if (string.IsNullOrEmpty(line) || line.Trim().Length == 0) return false;
            string[] f = line.Split(';');
            if (f.Length < 2) return false;
            for (int i = 0; i < f.Length; i++) f[i] = f[i].Trim().Trim('"').Trim();
            id = Key(f[0]);
            if (id.Length == 0) return false;
            Entry x = new Entry();
            if (!TryStatus(f[1], out x.Status)) return false;
            x.At = fileTime;
            if (f.Length > 2 && f[2].Length > 0 && !TryTime(f[2], out x.At)) return false;
            if (f.Length > 3 && f[3].Length > 0) { DateTime u; if (!TryTime(f[3], out u)) return false; x.Until = u; }
            e = x; return true;
        }

        // Datei neu lesen, wenn sich Zeit/Groesse geaendert haben (synchron; Fehler werden geschluckt = Standardstatus fuer alle)
        public void Reload()
        {
            Snap old = snap, n = new Snap();
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Exists)
                {
                    n.Exists = true; n.FileTime = fi.LastWriteTime; n.Size = fi.Length;
                    if (old.Readable && old.Exists && old.FileTime == n.FileTime && old.Size == n.Size) return;   // unveraendert
                    string text;
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false), true))
                        text = sr.ReadToEnd();
                    foreach (string line in text.Split('\n'))
                    {
                        string id; Entry e;
                        if (!ParseLine(line.TrimEnd('\r').TrimStart('﻿'), n.FileTime, out id, out e)) continue;
                        Entry cur;
                        if (!n.Map.TryGetValue(id, out cur) || e.At >= cur.At) n.Map[id] = e;   // neuester Eintrag gewinnt
                    }
                    n.Readable = true;
                }
            }
            catch (Exception) { n.Readable = false; n.Map.Clear(); }
            snap = n;
        }

        // hoechstens alle 60 s im Hintergrund pruefen; der aktuelle Stand wird sofort geliefert
        void Tick()
        {
            DateTime now = Clock();
            if ((now - lastCheck).TotalSeconds < ReloadSeconds) return;
            lastCheck = now;
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate { try { Reload(); } finally { Interlocked.Exchange(ref running, 0); } });
        }

        public EquipmentInfo Get(string number)
        {
            Tick();
            return Lookup(number, Clock());
        }

        public EquipmentInfo Lookup(string number, DateTime now)
        {
            Snap s = snap; Entry e;
            bool fresh = s.Exists && s.Readable && now - s.FileTime <= maxAge;
            if (fresh && s.Map.TryGetValue(Key(number), out e) && (e.Until == null || e.Until.Value >= now))
                return new EquipmentInfo(number, e.Status, e.At);
            return new EquipmentInfo(number, EquipStatus.NichtGereinigt, now);
        }

        public List<string> ListNumbers()
        {
            List<string> l = new List<string>(snap.Map.Keys); l.Sort(); return l;
        }
    }
}
