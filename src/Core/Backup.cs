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
        public DutyConfig Duties = DutyConfig.Default();
        public List<WeekMark> WeekMarks = new List<WeekMark>();
        public List<DeviceStatus> Statuses = new List<DeviceStatus>();   // manuelle Equipment-Status
        public bool StatusesKnown;          // gelesene Datei enthielt <Status> (Format >= 4); sonst bleibt beim Wiederherstellen der Stand
        public UserDb Users;                // nur in der Datendatei (FileStore), nie in Sicherungen
    }

    // Datensicherung als eine Datei (.apbak): Nutzlast GZip( "APBAK2\n" + SHA-256-Hex des XML + "\n" + XML ), als APBAK2 mit dem
    // Datenschluessel verschluesselt+signiert (Protected). APBAK1 (gleiche Nutzlast, nur GZip, ohne Schluessel) bleibt lesbar
    // und wird weiter geschrieben, wenn der Store keinen Schluessel hat (MemoryStore/Testdaten: keine Schluesseldatei vorhanden).
    // Geschuetzte Einstellungsdateien (Protected) sind nicht Teil der Sicherung.
    // Datei stammt von einer neueren Programmversion: nur Lesen mit Hinweis "neu starten" (FileStore -> OFFLINE)
    public class NewerFormatException : Exception
    {
        public NewerFormatException() : base("Die Daten wurden mit einer neueren Programmversion gespeichert. Bitte das Programm schließen und neu starten.") { }
    }

    public static class Backup
    {
        // Datenformat; bei inkompatibler Aenderung erhoehen (aeltere exe lesen die Datei dann nicht mehr und schreiben nie zurueck)
        public const int FormatVersion = 4;   // 2: Equipment-Stammdaten im Katalog; 3: Pflichtpunkte, Varianten, Wochenhaken, Produktprofile, "gehoertzu"; 4: manueller Equipment-Status, Statusuebergaenge, Regeln auf Art-/Gruppenebene ("@Art", "#Gruppe")
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

        public static void SaveFolder(string iniPath, string folder) { SaveSetting(iniPath, "ordner", folder); }

        // "stunden=<n>": Abstand der automatischen Sicherung (hat wie "ordner=" Vorrang vor Arbeitsplan.ini)
        public static int LoadHours(string iniPath, int defaultHours)
        {
            int n;
            try
            {
                if (File.Exists(iniPath))
                    foreach (string l in File.ReadAllLines(iniPath, Utf8))
                        if (l.StartsWith("stunden=") && int.TryParse(l.Substring(8).Trim(), NumberStyles.Integer, Inv, out n)) return ClampHours(n);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return ClampHours(defaultHours);
        }

        public const int MaxHours = 720;
        public static int ClampHours(int h) { return Math.Max(1, Math.Min(MaxHours, h)); }

        // Eine Zeile "key=value" setzen, andere Zeilen bleiben erhalten
        public static void SaveSetting(string iniPath, string key, string value)
        {
            List<string> lines = new List<string>();
            if (File.Exists(iniPath)) foreach (string l in File.ReadAllLines(iniPath, Utf8)) if (!l.StartsWith(key + "=") && l.Trim().Length > 0) lines.Add(l);
            lines.Add(key + "=" + value);
            File.WriteAllText(iniPath, string.Join("\r\n", lines.ToArray()) + "\r\n", Utf8);
        }

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

        public static bool AutoDue(string folder, int hours) { return Due(Last(folder), DateTime.Now, hours); }

        // Automatische Sicherung faellig: noch keine oder die letzte ist mindestens "hours" Stunden alt. Liegt die letzte in der
        // Zukunft (Uhr des Servers vor), ist nichts faellig: sonst wuerde bei jeder Pruefung gesichert und die Historie verdraengt.
        public static bool Due(DateTime? last, DateTime now, int hours)
        {
            return last == null || now - last.Value >= TimeSpan.FromHours(ClampHours(hours));
        }

        // Schreibt die Sicherung (erst .tmp, dann umbenennen) und behaelt die letzten Keep Dateien. Liefert den Pfad.
        // Schluessel des Datenspeichers (Datei arbeitsplan.key); null = Store ohne Schluessel (MemoryStore/Testdaten:
        // dort gibt es keine Schluesseldatei, die Sicherung bleibt wie bisher APBAK1). FileStore ohne lesbaren Schluessel wirft.
        public static string KeyOf(IDataStore store)
        {
            FileStore fs = store as FileStore;
            return fs == null ? null : fs.BackupKey();
        }

        public static string WriteTo(IDataStore store, string folder, string tag) { return WriteTo(store, folder, tag, KeyOf(store)); }

        public static string WriteTo(IDataStore store, string folder, string tag, string key) { return WriteTo(store.Snapshot(), folder, tag, key); }

        // Schreibt einen bereits geholten Stand (auch aus einem Hintergrund-Thread: braucht den Store nicht mehr)
        public static string WriteTo(StoreData snapshot, string folder, string tag, string key)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, Prefix + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", Inv) + (tag != null ? "_" + tag : "") + Ext);
            Write(snapshot, path, key);
            string[] f = Files(folder);
            for (int i = 0; i < f.Length - Keep; i++)
                try { File.Delete(f[i]); } catch (Exception) { }   // ohne Loeschrecht (Ordnerrechte der IT) bleiben aeltere liegen
            return path;
        }

        // key != null: APBAK2 = Protected( GZip( Kopf "APBAK2" + SHA-256 + XML ) ); key == null: APBAK1 (nur GZip).
        public static void Write(StoreData d, string path, string key)
        {
            byte[] xml = ToXml(d);
            string tmp = path + ".tmp";
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    byte[] head = Utf8.GetBytes((key != null ? "APBAK2" : "APBAK1") + "\n" + Hex(Hash(xml)) + "\n");
                    gz.Write(head, 0, head.Length);
                    gz.Write(xml, 0, xml.Length);
                }
                byte[] outp = key != null ? Protected.ProtectBytes(ms.ToArray(), key, FileStore.Rounds) : ms.ToArray();
                File.WriteAllBytes(tmp, outp);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // ---------- Lesen (mit Selbstpruefung) ----------

        static InvalidDataException Bad() { return new InvalidDataException("Die Sicherung ist beschädigt oder wurde verändert und wird nicht geladen."); }

        // key: Datenschluessel (fuer APBAK2); APBAK1-Dateien brauchen ihn nicht.
        public static StoreData Read(string path, string key)
        {
            byte[] all; int len;
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                // GZip beginnt mit 1F 8B (APBAK1); alles andere ist die verschluesselte Form (APBAK2)
                bool gz = raw.Length > 2 && raw[0] == 0x1F && raw[1] == 0x8B;
                if (!gz)
                {
                    if (key == null) throw Bad();
                    try { raw = Protected.UnprotectBytes(raw, key, FileStore.Rounds); }
                    catch (CryptographicException) { throw Bad(); }
                }
                using (MemoryStream ms = Unzip(new MemoryStream(raw))) { all = ms.GetBuffer(); len = (int)ms.Length; }
            }
            catch (InvalidDataException) { throw Bad(); }
            int a = Array.IndexOf(all, (byte)'\n', 0, len), b = a < 0 ? -1 : Array.IndexOf(all, (byte)'\n', a + 1, len - a - 1);
            if (b < 0) throw Bad();
            string magic = Utf8.GetString(all, 0, a);
            if (magic != "APBAK1" && magic != "APBAK2") throw Bad();
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

        internal static byte[] Hash(byte[] b) { return Hash(b, 0, b.Length); }
        internal static byte[] Hash(byte[] b, int off, int len)
        {
            try { using (SHA256 h = new SHA256Managed()) return h.ComputeHash(b, off, len); }
            catch (InvalidOperationException) { using (SHA256 h = new SHA256CryptoServiceProvider()) return h.ComputeHash(b, off, len); }   // FIPS-Richtlinie
        }

        internal static string Hex(byte[] b) { StringBuilder sb = new StringBuilder(); foreach (byte x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }

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
                    w.WriteStartElement("Arbeitsplan"); w.WriteAttributeString("version", FormatVersion.ToString(Inv)); w.WriteAttributeString("erstellt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
                    w.WriteStartElement("Zeilen");
                    foreach (RowInfo r in d.Rows)
                    {
                        w.WriteStartElement("Zeile"); w.WriteAttributeString("id", I(r.Id)); w.WriteAttributeString("name", r.Name);
                        for (int i = 0; i < 3; i++) w.WriteAttributeString("l" + i, r.Labels[i]);
                        w.WriteAttributeString("min", I(r.MinLines)); w.WriteAttributeString("lkz", B(r.HasLkz));
                        if (r.Mode != ShiftMode.FSN) w.WriteAttributeString("modus", r.Mode.ToString());
                        if (r.Variants.Length > 0) w.WriteAttributeString("varianten", r.Variants);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Bloecke");
                    foreach (StoredEntry s in d.Entries) { w.WriteStartElement("Block"); w.WriteAttributeString("tag", s.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(s.RowId)); WriteEntry(w, s.Data); w.WriteEndElement(); }
                    w.WriteEndElement();
                    WriteCatalog(w, d.Catalog);
                    WriteDuties(w, d.Duties);
                    w.WriteStartElement("Wochen");
                    foreach (WeekMark m in d.WeekMarks)
                    {
                        w.WriteStartElement("Woche"); w.WriteAttributeString("montag", m.Monday.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("k", m.Key);
                        w.WriteAttributeString("erledigt", B(m.Done)); w.WriteAttributeString("ver", I(m.Version));
                        if (m.At != DateTime.MinValue) w.WriteAttributeString("am", m.At.ToString("yyyy-MM-ddTHH:mm:ss", Inv));   // optional, aeltere Programme ignorieren es
                        if (m.By.Length > 0) w.WriteAttributeString("von", m.By);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Status");
                    foreach (DeviceStatus ds in d.Statuses)
                    {
                        w.WriteStartElement("Geraet"); w.WriteAttributeString("id", ds.Id); w.WriteAttributeString("status", ds.Status.ToString());
                        w.WriteAttributeString("am", ds.At.ToString("yyyy-MM-ddTHH:mm:ss", Inv)); w.WriteAttributeString("von", ds.By); w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Protokoll");
                    foreach (AuditEntry a in d.Audit)
                    {
                        w.WriteStartElement("Eintrag"); w.WriteAttributeString("zeit", I(a.Time.Ticks)); w.WriteAttributeString("login", a.Login); w.WriteAttributeString("kuerzel", a.Kuerzel);
                        if (a.Role.Length > 0) w.WriteAttributeString("rolle", a.Role);
                        w.WriteAttributeString("tag", a.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(a.RowId)); w.WriteAttributeString("name", a.RowName); w.WriteAttributeString("ver", I(a.Version));
                        if (a.Mark.Length > 0) { w.WriteAttributeString("pflicht", a.Mark); w.WriteAttributeString("wert", a.Value); }
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
            if (e.Variant.Length > 0) w.WriteAttributeString("variante", e.Variant);
            foreach (string m in e.Marks) { w.WriteStartElement("Mark"); w.WriteAttributeString("k", m); w.WriteEndElement(); }
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
            WriteMaster(w, c.Master);
            ProductXml.Write(w, c.Products);
            w.WriteEndElement();
        }

        static void WriteDuties(XmlWriter w, DutyConfig c)
        {
            w.WriteStartElement("Pflichten"); w.WriteAttributeString("land", c.Land);
            foreach (CheckDef d in c.Defs)
            {
                w.WriteStartElement("Def"); w.WriteAttributeString("k", d.Key); w.WriteAttributeString("name", d.Name); w.WriteAttributeString("kurz", d.Short);
                w.WriteAttributeString("zeile", I(d.RowId)); w.WriteAttributeString("regel", d.Rule.ToString()); w.WriteAttributeString("bed", d.Cond);
                w.WriteAttributeString("woche", B(d.Weekly)); w.WriteEndElement();
            }
            foreach (RoundDef r in c.Rounds)
            {
                w.WriteStartElement("Rundgang"); w.WriteAttributeString("k", r.Key); w.WriteAttributeString("name", r.Name); w.WriteAttributeString("kurz", r.Short);
                w.WriteAttributeString("aktiv", B(r.Active)); w.WriteEndElement();
            }
            foreach (DateTime x in c.ExtraHolidays) { w.WriteStartElement("Feiertag"); w.WriteAttributeString("tag", x.ToString("yyyy-MM-dd", Inv)); w.WriteEndElement(); }
            foreach (DateTime x in c.RemovedHolidays) { w.WriteStartElement("KeinFeiertag"); w.WriteAttributeString("tag", x.ToString("yyyy-MM-dd", Inv)); w.WriteEndElement(); }
            w.WriteEndElement();
        }

        static DutyConfig ReadDuties(XmlElement p)
        {
            DutyConfig c = new DutyConfig();
            if (p.GetAttribute("land").Length > 0) c.Land = p.GetAttribute("land");
            foreach (XmlElement e in Kids(p, "Def"))
            {
                CheckDef d = new CheckDef(); d.Key = e.GetAttribute("k"); d.Name = e.GetAttribute("name"); d.Short = e.GetAttribute("kurz");
                d.RowId = Int(e, "zeile"); d.Rule = En(e, "regel", CheckRule.IfOccupied); d.Cond = e.GetAttribute("bed"); d.Weekly = Bool(e, "woche");
                c.Defs.Add(d);
            }
            foreach (XmlElement e in Kids(p, "Rundgang"))
            {
                RoundDef r = new RoundDef(); r.Key = e.GetAttribute("k"); r.Name = e.GetAttribute("name"); r.Short = e.GetAttribute("kurz"); r.Active = Bool(e, "aktiv");
                c.Rounds.Add(r);
            }
            foreach (XmlElement e in Kids(p, "Feiertag")) c.ExtraHolidays.Add(Day(e));
            foreach (XmlElement e in Kids(p, "KeinFeiertag")) c.RemovedHolidays.Add(Day(e));
            return c;
        }

        static void WriteMaster(XmlWriter w, EquipMaster m)
        {
            w.WriteStartElement("Stammdaten");
            foreach (EquipClass c in m.Classes)
            {
                w.WriteStartElement("Klasse"); w.WriteAttributeString("name", c.Name);
                foreach (string f in c.Fields) { w.WriteStartElement("F"); w.WriteAttributeString("v", f); w.WriteEndElement(); }
                WriteRules(w, c.Rules);
                w.WriteEndElement();
            }
            foreach (EquipSeries s in m.Series)
            {
                w.WriteStartElement("Reihe"); w.WriteAttributeString("name", s.Name); w.WriteAttributeString("klasse", s.Class);
                w.WriteAttributeString("von", s.From); w.WriteAttributeString("bis", s.To); w.WriteAttributeString("notiz", s.Note);
                foreach (string id in s.Ids) { w.WriteStartElement("Id"); w.WriteAttributeString("v", id); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipDevice d in m.Devices)
            {
                w.WriteStartElement("Geraet"); w.WriteAttributeString("id", d.Id); w.WriteAttributeString("klasse", d.Class);
                w.WriteAttributeString("notiz", d.Note); w.WriteAttributeString("aktiv", B(d.Active));
                if (d.Parent.Length > 0) w.WriteAttributeString("gehoertzu", d.Parent);
                foreach (EquipProp p in d.Props) { w.WriteStartElement("E"); w.WriteAttributeString("name", p.Name); w.WriteAttributeString("v", p.Value); w.WriteEndElement(); }
                WriteRules(w, d.Rules);
                w.WriteEndElement();
            }
            foreach (EquipLink l in m.Links)
            {
                w.WriteStartElement("Verwendbar"); w.WriteAttributeString("von", l.From); w.WriteAttributeString("notiz", l.Note);
                foreach (string t in l.To) { w.WriteStartElement("Z"); w.WriteAttributeString("v", t); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipSubst s in m.Substs)
            {
                w.WriteStartElement("Ersatz"); w.WriteAttributeString("id", s.Id); w.WriteAttributeString("ersetzt", s.Replaces);
                w.WriteAttributeString("notiz", s.Note); w.WriteAttributeString("beide", B(s.Both)); w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        static void WriteRules(XmlWriter w, List<StatusRule> rules)
        {
            foreach (StatusRule r in rules) { w.WriteStartElement("Uebergang"); w.WriteAttributeString("von", r.From.ToString()); w.WriteAttributeString("nach", r.To.ToString()); w.WriteAttributeString("erlaubt", B(r.Legal)); w.WriteEndElement(); }
        }

        static void ReadRules(XmlElement e, List<StatusRule> rules)
        {
            foreach (XmlElement x in Kids(e, "Uebergang")) rules.Add(new StatusRule(En(x, "von", EquipStatus.NichtGereinigt), En(x, "nach", EquipStatus.NichtGereinigt), Bool(x, "erlaubt")));
        }

        static void ReadMaster(XmlElement sd, EquipMaster m)
        {
            foreach (XmlElement e in Kids(sd, "Klasse"))
            {
                EquipClass c = new EquipClass(); c.Name = e.GetAttribute("name");
                foreach (XmlElement x in Kids(e, "F")) c.Fields.Add(x.GetAttribute("v"));
                ReadRules(e, c.Rules);
                m.Classes.Add(c);
            }
            foreach (XmlElement e in Kids(sd, "Reihe"))
            {
                EquipSeries s = new EquipSeries(); s.Name = e.GetAttribute("name"); s.Class = e.GetAttribute("klasse");
                s.From = e.GetAttribute("von"); s.To = e.GetAttribute("bis"); s.Note = e.GetAttribute("notiz");
                foreach (XmlElement x in Kids(e, "Id")) s.Ids.Add(x.GetAttribute("v"));
                m.Series.Add(s);
            }
            foreach (XmlElement e in Kids(sd, "Geraet"))
            {
                EquipDevice d = new EquipDevice(); d.Id = e.GetAttribute("id"); d.Class = e.GetAttribute("klasse");
                d.Note = e.GetAttribute("notiz"); d.Active = Bool(e, "aktiv"); d.Parent = e.GetAttribute("gehoertzu");
                foreach (XmlElement x in Kids(e, "E")) d.Props.Add(new EquipProp(x.GetAttribute("name"), x.GetAttribute("v")));
                ReadRules(e, d.Rules);
                m.Devices.Add(d);
            }
            foreach (XmlElement e in Kids(sd, "Verwendbar"))
            {
                EquipLink l = new EquipLink(); l.From = e.GetAttribute("von"); l.Note = e.GetAttribute("notiz");
                foreach (XmlElement x in Kids(e, "Z")) l.To.Add(x.GetAttribute("v"));
                m.Links.Add(l);
            }
            foreach (XmlElement e in Kids(sd, "Ersatz"))
            {
                EquipSubst s = new EquipSubst(); s.Id = e.GetAttribute("id"); s.Replaces = e.GetAttribute("ersetzt");
                s.Note = e.GetAttribute("notiz"); s.Both = Bool(e, "beide"); m.Substs.Add(s);
            }
        }

        static IEnumerable<XmlElement> Kids(XmlNode n, string name)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null && e.Name == name) yield return e; }
        }
        static XmlElement Kid(XmlNode n, string name) { foreach (XmlElement e in Kids(n, name)) return e; return null; }
        static int Int(XmlElement e, string a) { return int.Parse(e.GetAttribute(a), Inv); }
        static bool Bool(XmlElement e, string a) { return e.GetAttribute(a) == "1"; }
        // fehlendes Attribut = Standard; unbekannter Wert = FormatException (wie andere Lesefehler)
        static T En<T>(XmlElement e, string a, T def) where T : struct
        {
            string s = e.GetAttribute(a);
            if (s.Length == 0) return def;
            try { return (T)Enum.Parse(typeof(T), s); }
            catch (ArgumentException) { throw new FormatException(); }
        }
        static DateTime Day(XmlElement e) { return DateTime.ParseExact(e.GetAttribute("tag"), "yyyy-MM-dd", Inv); }

        internal static StoreData FromXml(byte[] xml) { return FromXml(new MemoryStream(xml)); }
        internal static StoreData FromXml(Stream xml)
        {
            XmlDocument doc = new XmlDocument();
            XmlReaderSettings rs = new XmlReaderSettings(); rs.DtdProcessing = DtdProcessing.Prohibit; rs.XmlResolver = null; rs.IgnoreWhitespace = true;
            using (XmlReader r = XmlReader.Create(xml, rs)) doc.Load(r);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Arbeitsplan" || Kid(root, "Zeilen") == null || Kid(root, "Katalog") == null) throw new FormatException();
            // Neueres Datenformat (neuere exe auf dem Laufwerk): nie lesen und zurueckschreiben, sonst gingen neue Felder verloren
            int fv;
            if (int.TryParse(root.GetAttribute("version"), NumberStyles.Integer, Inv, out fv) && fv > FormatVersion) throw new NewerFormatException();
            StoreData d = new StoreData();
            foreach (XmlElement z in Kids(Kid(root, "Zeilen"), "Zeile"))
            {
                RowInfo r = new RowInfo(); r.Id = Int(z, "id"); r.Name = z.GetAttribute("name");
                for (int i = 0; i < 3; i++) r.Labels[i] = z.GetAttribute("l" + i);
                r.MinLines = Int(z, "min"); r.HasLkz = Bool(z, "lkz");
                r.Mode = En(z, "modus", ShiftMode.FSN); r.Variants = z.GetAttribute("varianten");
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
            XmlElement sd = Kid(k, "Stammdaten");
            if (sd != null) ReadMaster(sd, d.Catalog.Master);
            ProductXml.Read(k, d.Catalog.Products);   // fehlt in aelteren Dateien = keine Produktprofile
            XmlElement pf = Kid(root, "Pflichten");
            if (pf != null) d.Duties = ReadDuties(pf);   // sonst Standard (v2-Dateien)
            XmlElement wo = Kid(root, "Wochen");
            if (wo != null)
                foreach (XmlElement e in Kids(wo, "Woche"))
                {
                    WeekMark m = new WeekMark(); m.Monday = DateTime.ParseExact(e.GetAttribute("montag"), "yyyy-MM-dd", Inv); m.Key = e.GetAttribute("k");
                    m.Done = Bool(e, "erledigt"); m.Version = Int(e, "ver"); m.By = e.GetAttribute("von");
                    DateTime at;
                    if (DateTime.TryParseExact(e.GetAttribute("am"), "yyyy-MM-ddTHH:mm:ss", Inv, DateTimeStyles.None, out at)) m.At = at;
                    d.WeekMarks.Add(m);
                }
            XmlElement stt = Kid(root, "Status");
            if (stt != null)
            {
                d.StatusesKnown = true;
                foreach (XmlElement e in Kids(stt, "Geraet"))
                {
                    DeviceStatus st = new DeviceStatus(); st.Id = e.GetAttribute("id"); st.Status = En(e, "status", EquipStatus.NichtGereinigt); st.By = e.GetAttribute("von");
                    DateTime at; if (DateTime.TryParseExact(e.GetAttribute("am"), "yyyy-MM-ddTHH:mm:ss", Inv, DateTimeStyles.None, out at)) st.At = at;
                    d.Statuses.Add(st);
                }
            }
            XmlElement pr = Kid(root, "Protokoll");
            if (pr != null)
                foreach (XmlElement e in Kids(pr, "Eintrag"))
                {
                    AuditEntry a = new AuditEntry(); a.Time = new DateTime(long.Parse(e.GetAttribute("zeit"), Inv), DateTimeKind.Local);
                    a.Login = e.GetAttribute("login"); a.Kuerzel = e.GetAttribute("kuerzel"); a.Role = e.GetAttribute("rolle"); a.Day = Day(e); a.RowId = Int(e, "zeile"); a.RowName = e.GetAttribute("name"); a.Version = Int(e, "ver");
                    a.Mark = e.GetAttribute("pflicht"); a.Value = e.GetAttribute("wert");
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
            x.Variant = e.GetAttribute("variante");
            foreach (XmlElement m in Kids(e, "Mark")) x.Marks.Add(m.GetAttribute("k"));
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
