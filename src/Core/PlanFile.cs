using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Schnappschuss eines Zeitraums zum Ansehen (Plan teilen, Datei .applan)
    public class PlanSnapshot
    {
        public DateTime From;     // erster Tag
        public int Days;          // Anzahl Tage ab From
        public StoreData Data;    // Zeilen, Bloecke des Zeitraums, Pflichtpunkte, Wochenhaken (kein Katalog, kein Protokoll, keine Nutzer)
        public string Name = "";  // Dateiname (Anzeige)
    }

    // Plan-Datei (.applan): GZip( "APPLAN1\n" + SHA-256-Hex(Nutzlast) + "\n" + Nutzlast ), Nutzlast = "von=JJJJ-MM-TT;tage=n\n" + XML
    // im Format der Sicherung. Unverschluesselt (nur Plandaten des gewaehlten Zeitraums, keine Nutzer/Rechte/Protokolle);
    // der Hash erkennt Beschaedigung und Veraenderung. Ansehen braucht keinen Datenordner.
    public static class PlanFile
    {
        public const string Ext = ".applan";
        public const int MaxDays = 371;   // bis 53 Wochen
        const string Magic = "APPLAN1";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static PlanSnapshot Take(IDataStore store, DateTime from, int days)
        {
            PlanSnapshot p = new PlanSnapshot(); p.From = from.Date; p.Days = Math.Max(1, Math.Min(MaxDays, days));
            StoreData d = new StoreData();
            d.Rows = store.GetRows(); d.Duties = store.GetDuties();
            for (int i = 0; i < p.Days; i++)
            {
                DateTime day = p.From.AddDays(i);
                foreach (RowInfo r in d.Rows)
                {
                    Entry e = store.Get(day, r.Id);
                    if (e == null) continue;
                    StoredEntry s = new StoredEntry(); s.Day = day; s.RowId = r.Id; s.Data = e; d.Entries.Add(s);
                }
                if (i == 0 || day.DayOfWeek == DayOfWeek.Monday) d.WeekMarks.AddRange(store.GetWeekMarks(day.AddDays(-(((int)day.DayOfWeek + 6) % 7))));
            }
            p.Data = d;
            return p;
        }

        public static void Write(PlanSnapshot p, string path)
        {
            byte[] xml = Backup.ToXml(p.Data);
            byte[] meta = Utf8.GetBytes("von=" + p.From.ToString("yyyy-MM-dd", Inv) + ";tage=" + p.Days.ToString(Inv) + "\n");
            byte[] payload = new byte[meta.Length + xml.Length];
            Buffer.BlockCopy(meta, 0, payload, 0, meta.Length); Buffer.BlockCopy(xml, 0, payload, meta.Length, xml.Length);
            string tmp = path + ".tmp";
            using (FileStream fs = File.Create(tmp))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
            {
                byte[] head = Utf8.GetBytes(Magic + "\n" + Backup.Hex(Backup.Hash(payload)) + "\n");
                gz.Write(head, 0, head.Length);
                gz.Write(payload, 0, payload.Length);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        static InvalidDataException Bad() { return new InvalidDataException("Die Plan-Datei ist beschädigt oder wurde verändert und wird nicht geöffnet."); }

        public static PlanSnapshot Read(string path)
        {
            byte[] all; int len;
            try { using (FileStream fs = File.OpenRead(path)) using (MemoryStream ms = Backup.Unzip(fs)) { all = ms.GetBuffer(); len = (int)ms.Length; } }
            catch (InvalidDataException) { throw Bad(); }
            int a = Array.IndexOf(all, (byte)'\n', 0, len), b = a < 0 ? -1 : Array.IndexOf(all, (byte)'\n', a + 1, len - a - 1);
            if (b < 0 || Utf8.GetString(all, 0, a) != Magic) throw Bad();
            if (Utf8.GetString(all, a + 1, b - a - 1) != Backup.Hex(Backup.Hash(all, b + 1, len - b - 1))) throw Bad();
            int c = Array.IndexOf(all, (byte)'\n', b + 1, len - b - 1);
            if (c < 0) throw Bad();
            PlanSnapshot p = new PlanSnapshot();
            p.Name = Path.GetFileName(path);
            try
            {
                Dictionary<string, string> m = new Dictionary<string, string>();
                foreach (string kv in Utf8.GetString(all, b + 1, c - b - 1).Split(';')) { int eq = kv.IndexOf('='); if (eq > 0) m[kv.Substring(0, eq)] = kv.Substring(eq + 1); }
                p.From = DateTime.ParseExact(m["von"], "yyyy-MM-dd", Inv);
                p.Days = Math.Max(1, Math.Min(MaxDays, int.Parse(m["tage"], Inv)));
                p.Data = Backup.FromXml(new MemoryStream(all, c + 1, len - c - 1, false));
            }
            catch (KeyNotFoundException) { throw Bad(); }
            catch (XmlException) { throw Bad(); }
            catch (FormatException) { throw Bad(); }
            catch (OverflowException) { throw Bad(); }
            p.Data.Users = null; p.Data.Audit.Clear();   // Ansicht: nie Nutzer oder Protokoll uebernehmen
            return p;
        }
    }
}
