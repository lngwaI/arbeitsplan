using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arbeitsplan.Core
{
    // Benachrichtigung an einen Nutzer (z. B. "Ihre Bearbeitung wurde uebernommen"). Liegt als eigene Datei im Datenordner,
    // nicht in der apdb: kein Einfluss auf Datenformat und Sicherung.
    public class Notice
    {
        public const string KindTakeOver = "uebernahme";
        public const int Keep = 50;          // so viele (die neuesten) zeigt der Verlauf
        public string Id = "";               // Dateiname ohne Endung, vom Dienst vergeben
        public DateTime Time;
        public string Kind = "", Text = "";
        public DateTime Day; public int RowId;   // Bezug: Block (Tag + Zeilen-Id), sonst leer
        public bool Read;

        public Notice Clone() { return (Notice)MemberwiseClone(); }

        public const string NoReason = "kein Grund angegeben";

        // Kommentar einzeilig; leer = "kein Grund angegeben"
        public static string CleanReason(string reason)
        {
            string r = (reason ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return r.Length == 0 ? NoReason : r;
        }

        // "<Wer> (<Kuerzel>) hat Ihre Bearbeitung von <Ueberschrift> am <Datum> uebernommen. Grund: ... Ihre ungespeicherten Aenderungen sind verloren."
        public static Notice TakeOver(string byName, string byKuerzel, string title, string dayText, DateTime day, int rowId, string reason)
        {
            string r = CleanReason(reason);
            Notice n = new Notice(); n.Time = DateTime.Now; n.Kind = KindTakeOver; n.Day = day.Date; n.RowId = rowId;
            n.Text = byName + (byKuerzel.Length > 0 ? " (" + byKuerzel + ")" : "") + " hat Ihre Bearbeitung von " + title + " am " + dayText
                + " übernommen. Grund: " + r + (r.EndsWith(".") ? " " : ". ") + "Ihre ungespeicherten Änderungen sind verloren.";
            return n;
        }
    }

    // Nachrichten je Nutzer (Login). Fehler beim Lesen/Schreiben werfen nie; Add meldet sie als false + Text.
    public interface INoticeService
    {
        bool Add(string login, Notice n, out string error);
        List<Notice> List(string login);          // neueste zuerst, hoechstens Notice.Keep
        bool MarkRead(string login, string id);
        int MarkAllRead(string login);            // Anzahl neu als gelesen markiert
    }

    public class MemoryNoticeService : INoticeService
    {
        readonly Dictionary<string, List<Notice>> all = new Dictionary<string, List<Notice>>();
        int counter;

        static string Key(string login) { return (login ?? "").ToLowerInvariant(); }

        public bool Add(string login, Notice n, out string error)
        {
            lock (all)
            {
                List<Notice> l;
                if (!all.TryGetValue(Key(login), out l)) all[Key(login)] = l = new List<Notice>();
                Notice c = n.Clone(); c.Id = "m" + (++counter).ToString("D6", CultureInfo.InvariantCulture); c.Read = false;
                if (c.Time == DateTime.MinValue) c.Time = DateTime.Now;
                l.Add(c); n.Id = c.Id;
            }
            error = null; return true;
        }

        public List<Notice> List(string login)
        {
            List<Notice> r = new List<Notice>();
            lock (all)
            {
                List<Notice> l;
                if (all.TryGetValue(Key(login), out l))
                    for (int i = l.Count - 1; i >= 0 && r.Count < Notice.Keep; i--) r.Add(l[i].Clone());
            }
            return r;
        }

        public bool MarkRead(string login, string id)
        {
            lock (all)
            {
                List<Notice> l;
                if (all.TryGetValue(Key(login), out l)) foreach (Notice n in l) if (n.Id == id) { n.Read = true; return true; }
            }
            return false;
        }

        public int MarkAllRead(string login)
        {
            int c = 0;
            lock (all)
            {
                List<Notice> l;
                if (all.TryGetValue(Key(login), out l)) foreach (Notice n in l) if (!n.Read) { n.Read = true; c++; }
            }
            return c;
        }
    }

    // <Datenordner>\nachrichten\<login>\<JJJJMMTThhmmssfff>-<Zufall>.txt (UTF-8, Zeilen schluessel=wert, \n/\\ maskiert);
    // gelesen = Endung ".gelesen". Geschrieben wird erst in .tmp, dann umbenannt (nie halbe Dateien sichtbar).
    public class FileNoticeService : INoticeService
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        readonly string dir;

        public FileNoticeService(string dataFolder) { dir = Path.Combine(dataFolder, "nachrichten"); }

        string UserDir(string login)
        {
            StringBuilder s = new StringBuilder();
            foreach (char ch in (login ?? "").ToLowerInvariant()) s.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
            return Path.Combine(dir, s.Length > 0 ? s.ToString() : "_");
        }

        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n"); }

        static string Unesc(string s)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; b.Append(s[i] == 'n' ? '\n' : s[i]); }
                else b.Append(s[i]);
            }
            return b.ToString();
        }

        public bool Add(string login, Notice n, out string error)
        {
            error = null;
            try
            {
                if (n.Time == DateTime.MinValue) n.Time = DateTime.Now;
                string d = UserDir(login); Directory.CreateDirectory(d);
                string id = n.Time.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                string tmp = Path.Combine(d, id + ".tmp");
                File.WriteAllText(tmp, "zeit=" + n.Time.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "\nart=" + Esc(n.Kind)
                    + "\ntag=" + n.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\nzeile=" + n.RowId.ToString(CultureInfo.InvariantCulture)
                    + "\ntext=" + Esc(n.Text) + "\n", Utf8);
                File.Move(tmp, Path.Combine(d, id + ".txt"));
                n.Id = id;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public List<Notice> List(string login)
        {
            List<Notice> r = new List<Notice>();
            try
            {
                string d = UserDir(login);
                if (!Directory.Exists(d)) return r;
                List<string> files = new List<string>();
                foreach (string f in Directory.GetFiles(d))
                {
                    string x = Path.GetExtension(f);
                    if (x == ".txt" || x == ".gelesen") files.Add(f);
                }
                files.Sort(delegate(string a, string b) { return string.CompareOrdinal(Path.GetFileNameWithoutExtension(b), Path.GetFileNameWithoutExtension(a)); });
                foreach (string f in files)
                {
                    if (r.Count >= Notice.Keep) break;
                    Notice n = Parse(f);
                    if (n != null) r.Add(n);
                }
            }
            catch (Exception) { }
            return r;
        }

        static Notice Parse(string f)
        {
            try
            {
                Notice n = new Notice(); n.Id = Path.GetFileNameWithoutExtension(f); n.Read = Path.GetExtension(f) == ".gelesen";
                bool time = false;
                foreach (string line in File.ReadAllText(f, Utf8).Split('\n'))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "zeit") time = DateTime.TryParseExact(v, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out n.Time);
                    else if (k == "art") n.Kind = Unesc(v);
                    else if (k == "tag") DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out n.Day);
                    else if (k == "zeile") int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n.RowId);
                    else if (k == "text") n.Text = Unesc(v);
                }
                return time && n.Text.Length > 0 ? n : null;
            }
            catch (Exception) { return null; }
        }

        public bool MarkRead(string login, string id)
        {
            try
            {
                string d = UserDir(login), f = Path.Combine(d, id + ".txt");
                if (id.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return false;
                if (File.Exists(f)) File.Move(f, Path.Combine(d, id + ".gelesen"));
                return File.Exists(Path.Combine(d, id + ".gelesen"));
            }
            catch (Exception) { return false; }
        }

        public int MarkAllRead(string login)
        {
            int c = 0;
            foreach (Notice n in List(login)) if (!n.Read && MarkRead(login, n.Id)) c++;
            return c;
        }
    }
}
