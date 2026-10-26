using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    public class StoredEntry { public DateTime Day; public int RowId; public Entry Data; }

    // Vollstaendiger Datenbestand (Plaene ohne Zeitgrenze, Struktur, Katalog, Protokoll).
    public class StoreData
    {
        public List<RowInfo> Rows = new List<RowInfo>();
        public List<StoredEntry> Entries = new List<StoredEntry>();
        public Catalog Catalog = new Catalog();
        public List<AuditEntry> Audit = new List<AuditEntry>();
        public UserDb Users;                 // nur in der Datendatei (FileStore), nie in Sicherungen
    }

    // Datensicherung als eine Datei (.apbak): GZip( "APBAK1\n" + SHA-256-Hex des XML + "\n" + XML ).
    // Der Hash erkennt Beschaedigung/Veraenderung (kein Schutz gegen gezieltes Neuberechnen; dafuer spaeter Protected/Server).
    // Geschuetzte Einstellungsdateien (Protected) sind nicht Teil der Sicherung.
    public static class Backup
    {
        public const string Ext = ".apbak", Prefix = "Arbeitsplan_";
        public const int Keep = 14;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- Ordner-Einstellung (sicherung.ini: "ordner=<Pfad>") ----------

        public static string LoadFolder(string iniPath, string defaultFolder)
        {
            try
            {
                if (File.Exists(iniPath))
                    foreach (string l in File.ReadAllLines(iniPath, Utf8))
                        if (l.StartsWith("ordner=") && l.Length > 7) return l.Substring(7).Trim();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return defaultFolder;
        }

        public static void SaveFolder(string iniPath, string folder) { File.WriteAllText(iniPath, "ordner=" + folder + "\r\n", Utf8); }

        // ---------- Schreiben / Aufraeumen ----------

        public static string[] Files(string folder)
        {
            if (!Directory.Exists(folder)) return new string[0];
            string[] f = Directory.GetFiles(folder, Prefix + "*" + Ext);
            Array.Sort(f, StringComparer.OrdinalIgnoreCase);   // Zeitstempel im Namen: aelteste zuerst
            return f;
        }

        public static DateTime? Last(string folder)
        {
            string[] f = Files(folder);
            return f.Length == 0 ? (DateTime?)null : File.GetLastWriteTime(f[f.Length - 1]);
        }

        public static bool AutoDue(string folder) { DateTime? l = Last(folder); return l == null || DateTime.Now - l.Value > TimeSpan.FromHours(24); }

        // Schreibt die Sicherung (erst .tmp, dann umbenennen) und behaelt die letzten Keep Dateien. Liefert den Pfad.
        public static string WriteTo(IDataStore store, string folder, string tag)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, Prefix + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", Inv) + (tag != null ? "_" + tag : "") + Ext);
            Write(store.Snapshot(), path);
            string[] f = Files(folder);
            for (int i = 0; i < f.Length - Keep; i++) File.Delete(f[i]);
            return path;
        }

        public static void Write(StoreData d, string path)
        {
            byte[] xml = ToXml(d);
            string tmp = path + ".tmp";
            using (FileStream fs = File.Create(tmp))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
            {
                byte[] head = Utf8.GetBytes("APBAK1\n" + Hex(Hash(xml)) + "\n");
                gz.Write(head, 0, head.Length);
                gz.Write(xml, 0, xml.Length);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // ---------- Lesen (mit Selbstpruefung) ----------

        static InvalidDataException Bad() { return new InvalidDataException("Die Sicherung ist beschädigt oder wurde verändert und wird nicht geladen."); }

        public static StoreData Read(string path)
        {
            byte[] all; int len;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                using (MemoryStream ms = Unzip(fs)) { all = ms.GetBuffer(); len = (int)ms.Length; }
            }
            catch (InvalidDataException) { throw Bad(); }
            int a = Array.IndexOf(all, (byte)'\n', 0, len), b = a < 0 ? -1 : Array.IndexOf(all, (byte)'\n', a + 1, len - a - 1);
            if (b < 0 || Utf8.GetString(all, 0, a) != "APBAK1") throw Bad();
            string hash = Utf8.GetString(all, a + 1, b - a - 1);
            if (hash != Hex(Hash(all, b + 1, len - b - 1))) throw Bad();
            try { return FromXml(new MemoryStream(all, b + 1, len - b - 1, false)); }
            catch (XmlException) { throw Bad(); }
            catch (FormatException) { throw Bad(); }
            catch (OverflowException) { throw Bad(); }
        }

        // Entpackt GZip vollstaendig (Position 0); mehr als MaxUnzip Bytes = InvalidDataException (Schutz vor "Zip-Bomben").
        public const long MaxUnzip = 200L * 1024 * 1024;
        internal static MemoryStream Unzip(Stream src)
        {
            MemoryStream ms = new MemoryStream();
            using (GZipStream gz = new GZipStream(src, CompressionMode.Decompress))
            {
                byte[] buf = new byte[65536]; int n;
                while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + n > MaxUnzip) { ms.Dispose(); throw new InvalidDataException("Entpackte Daten sind zu groß (über 200 MB)."); }
                    ms.Write(buf, 0, n);
                }
            }
            ms.Position = 0;
            return ms;
        }

        static byte[] Hash(byte[] b) { return Hash(b, 0, b.Length); }
        static byte[] Hash(byte[] b, int off, int len)
        {
            try { using (SHA256 h = new SHA256Managed()) return h.ComputeHash(b, off, len); }
            catch (InvalidOperationException) { using (SHA256 h = new SHA256CryptoServiceProvider()) return h.ComputeHash(b, off, len); }   // FIPS-Richtlinie
        }

        static string Hex(byte[] b) { StringBuilder sb = new StringBuilder(); foreach (byte x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }

        // ---------- XML ----------

        static string I(long v) { return v.ToString(Inv); }
        static string B(bool v) { return v ? "1" : "0"; }

        internal static byte[] ToXml(StoreData d)
        {
            using (MemoryStream ms = new MemoryStream()) { ToXml(ms, d, true); return ms.ToArray(); }
        }

        // indent=false: kompakt (Datendatei); Lesen kommt mit beiden Formen zurecht. output bleibt offen.
        internal static void ToXml(Stream output, StoreData d, bool indent)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = indent; st.Encoding = Utf8;
            {
                using (XmlWriter w = XmlWriter.Create(output, st))
                {
                    w.WriteStartElement("Arbeitsplan"); w.WriteAttributeString("version", "1"); w.WriteAttributeString("erstellt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
                    w.WriteStartElement("Zeilen");
                    foreach (RowInfo r in d.Rows)
                    {
                        w.WriteStartElement("Zeile"); w.WriteAttributeString("id", I(r.Id)); w.WriteAttributeString("name", r.Name);
                        for (int i = 0; i < 3; i++) w.WriteAttributeString("l" + i, r.Labels[i]);
                        w.WriteAttributeString("min", I(r.MinLines)); w.WriteAttributeString("lkz", B(r.HasLkz));
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Bloecke");
                    foreach (StoredEntry s in d.Entries) { w.WriteStartElement("Block"); w.WriteAttributeString("tag", s.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(s.RowId)); WriteEntry(w, s.Data); w.WriteEndElement(); }
                    w.WriteEndElement();
                    WriteCatalog(w, d.Catalog);
                    w.WriteStartElement("Protokoll");
                    foreach (AuditEntry a in d.Audit)
                    {
                        w.WriteStartElement("Eintrag"); w.WriteAttributeString("zeit", I(a.Time.Ticks)); w.WriteAttributeString("login", a.Login); w.WriteAttributeString("kuerzel", a.Kuerzel);
                        w.WriteAttributeString("tag", a.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(a.RowId)); w.WriteAttributeString("name", a.RowName); w.WriteAttributeString("ver", I(a.Version));
                        if (a.Before != null) { w.WriteStartElement("Vorher"); WriteEntry(w, a.Before); w.WriteEndElement(); }
                        if (a.After != null) { w.WriteStartElement("Nachher"); WriteEntry(w, a.After); w.WriteEndElement(); }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    if (d.Users != null) d.Users.WriteXml(w);
                    w.WriteEndElement();
                }
            }
        }

        static void WriteEntry(XmlWriter w, Entry e)
        {
            w.WriteAttributeString("ver", I(e.Version)); w.WriteAttributeString("lkz", B(e.Lkz));
            w.WriteAttributeString("inhalt", e.Content);
            for (int i = 0; i < 3; i++) w.WriteAttributeString("p" + i, e.People[i]);
            foreach (FormatSpan f in e.Spans)
            {
                w.WriteStartElement("Fmt"); w.WriteAttributeString("start", I(f.Start)); w.WriteAttributeString("len", I(f.Length)); w.WriteAttributeString("rgb", I(f.Rgb));
                w.WriteAttributeString("size", f.Size.ToString("R", Inv)); w.WriteAttributeString("b", B(f.Bold)); w.WriteAttributeString("i", B(f.Italic));
                if (f.Font != null) w.WriteAttributeString("font", f.Font);
                w.WriteEndElement();
            }
        }

        static void WriteCatalog(XmlWriter w, Catalog c)
        {
            w.WriteStartElement("Katalog");
            foreach (EquipPosition p in c.Positions)
            {
                w.WriteStartElement("Position"); w.WriteAttributeString("name", p.Name); w.WriteAttributeString("kurz", p.Short); w.WriteAttributeString("abwechselnd", B(p.AllowRotation));
                foreach (string id in p.Ids) { w.WriteStartElement("Id"); w.WriteAttributeString("v", id); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipGroup g in c.Groups)
            {
                w.WriteStartElement("Gruppe"); w.WriteAttributeString("name", g.Name);
                foreach (string s in g.Shorts) { w.WriteStartElement("K"); w.WriteAttributeString("v", s); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (Baustein b in c.Bausteine)
            {
                w.WriteStartElement("Baustein"); w.WriteAttributeString("name", b.Name); w.WriteAttributeString("block", b.Row); w.WriteAttributeString("text", b.Text);
                w.WriteAttributeString("einmal", B(b.Unique)); w.WriteAttributeString("standard", B(b.DefaultInCampaign)); w.WriteAttributeString("sonder", B(b.Special));
                foreach (string s in b.Positions) { w.WriteStartElement("P"); w.WriteAttributeString("v", s); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        static IEnumerable<XmlElement> Kids(XmlNode n, string name)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null && e.Name == name) yield return e; }
        }
        static XmlElement Kid(XmlNode n, string name) { foreach (XmlElement e in Kids(n, name)) return e; return null; }
        static int Int(XmlElement e, string a) { return int.Parse(e.GetAttribute(a), Inv); }
        static bool Bool(XmlElement e, string a) { return e.GetAttribute(a) == "1"; }
        static DateTime Day(XmlElement e) { return DateTime.ParseExact(e.GetAttribute("tag"), "yyyy-MM-dd", Inv); }

        internal static StoreData FromXml(byte[] xml) { return FromXml(new MemoryStream(xml)); }
        internal static StoreData FromXml(Stream xml)
        {
            XmlDocument doc = new XmlDocument();
            XmlReaderSettings rs = new XmlReaderSettings(); rs.DtdProcessing = DtdProcessing.Prohibit; rs.XmlResolver = null; rs.IgnoreWhitespace = true;
            using (XmlReader r = XmlReader.Create(xml, rs)) doc.Load(r);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Arbeitsplan" || Kid(root, "Zeilen") == null || Kid(root, "Katalog") == null) throw new FormatException();
            StoreData d = new StoreData();
            foreach (XmlElement z in Kids(Kid(root, "Zeilen"), "Zeile"))
            {
                RowInfo r = new RowInfo(); r.Id = Int(z, "id"); r.Name = z.GetAttribute("name");
                for (int i = 0; i < 3; i++) r.Labels[i] = z.GetAttribute("l" + i);
                r.MinLines = Int(z, "min"); r.HasLkz = Bool(z, "lkz");
                d.Rows.Add(r);
            }
            if (d.Rows.Count == 0) throw new FormatException();
            XmlElement bl = Kid(root, "Bloecke");
            if (bl != null)
                foreach (XmlElement e in Kids(bl, "Block")) { StoredEntry s = new StoredEntry(); s.Day = Day(e); s.RowId = Int(e, "zeile"); s.Data = ReadEntry(e); d.Entries.Add(s); }
            XmlElement k = Kid(root, "Katalog");
            foreach (XmlElement e in Kids(k, "Position"))
            {
                EquipPosition p = new EquipPosition(); p.Name = e.GetAttribute("name"); p.Short = e.GetAttribute("kurz"); p.AllowRotation = Bool(e, "abwechselnd");
                foreach (XmlElement x in Kids(e, "Id")) p.Ids.Add(x.GetAttribute("v"));
                d.Catalog.Positions.Add(p);
            }
            foreach (XmlElement e in Kids(k, "Gruppe"))
            {
                EquipGroup g = new EquipGroup(); g.Name = e.GetAttribute("name");
                foreach (XmlElement x in Kids(e, "K")) g.Shorts.Add(x.GetAttribute("v"));
                d.Catalog.Groups.Add(g);
            }
            foreach (XmlElement e in Kids(k, "Baustein"))
            {
                Baustein b = new Baustein(); b.Name = e.GetAttribute("name"); b.Row = e.GetAttribute("block"); b.Text = e.GetAttribute("text");
                b.Unique = Bool(e, "einmal"); b.DefaultInCampaign = Bool(e, "standard"); b.Special = Bool(e, "sonder");
                foreach (XmlElement x in Kids(e, "P")) b.Positions.Add(x.GetAttribute("v"));
                d.Catalog.Bausteine.Add(b);
            }
            XmlElement pr = Kid(root, "Protokoll");
            if (pr != null)
                foreach (XmlElement e in Kids(pr, "Eintrag"))
                {
                    AuditEntry a = new AuditEntry(); a.Time = new DateTime(long.Parse(e.GetAttribute("zeit"), Inv), DateTimeKind.Local);
                    a.Login = e.GetAttribute("login"); a.Kuerzel = e.GetAttribute("kuerzel"); a.Day = Day(e); a.RowId = Int(e, "zeile"); a.RowName = e.GetAttribute("name"); a.Version = Int(e, "ver");
                    XmlElement v = Kid(e, "Vorher"), n = Kid(e, "Nachher");
                    if (v != null) a.Before = ReadEntry(v);
                    if (n != null) a.After = ReadEntry(n);
                    d.Audit.Add(a);
                }
            XmlElement nu = Kid(root, "Nutzer");
            if (nu != null) d.Users = UserDb.ReadXml(nu);
            return d;
        }

        static Entry ReadEntry(XmlElement e)
        {
            Entry x = new Entry(); x.Version = Int(e, "ver"); x.Lkz = Bool(e, "lkz"); x.Content = e.GetAttribute("inhalt");
            for (int i = 0; i < 3; i++) x.People[i] = e.GetAttribute("p" + i);
            foreach (XmlElement f in Kids(e, "Fmt"))
            {
                FormatSpan s = new FormatSpan(); s.Start = Int(f, "start"); s.Length = Int(f, "len"); s.Rgb = Int(f, "rgb");
                s.Size = float.Parse(f.GetAttribute("size"), Inv); s.Bold = Bool(f, "b"); s.Italic = Bool(f, "i");
                s.Font = f.HasAttribute("font") ? f.GetAttribute("font") : null;
                x.Spans.Add(s);
            }
            return x;
        }
    }
}
