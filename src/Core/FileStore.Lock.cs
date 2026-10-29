using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;   // ProtectedData: System.Security.dll
using System.Text;
using System.Threading;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Soft-Lock als kleine Datei je Block im Datenordner ("sperren\JJJJ-MM-TT_Zeile.lock", Inhalt = Anzeigename; der Login steht in der Zusatzdatei "<Sperre>.login" (Zeile 1 Name, Zeile 2 Login), damit aeltere exe die .lock unveraendert lesen). Verfaellt nach 30 min.
    public class FileLockService : ILockService
    {
        static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        readonly string dir;

        public FileLockService(string dataFolder) { dir = Path.Combine(dataFolder, "sperren"); }

        string FileOf(DateTime d, int r) { return Path.Combine(dir, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + r + ".lock"); }

        public string Holder(DateTime day, int row, string me)
        {
            try
            {
                string f = FileOf(day, row);
                if (!File.Exists(f) || DateTime.UtcNow - File.GetLastWriteTimeUtc(f) >= Stale) return null;
                string u = FirstLine(File.ReadAllText(f, Utf8));
                return u.Length > 0 && u != me ? u : null;
            }
            catch (Exception) { return null; }   // Sperren sind nur ein Hinweis
        }

        static string FirstLine(string t) { string[] l = t.Split('\n'); return l[0].Trim(); }

        public void Acquire(DateTime day, int row, string me) { AcquireAs(day, row, me, ""); }

        public void AcquireAs(DateTime day, int row, string me, string login)
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(FileOf(day, row), me, Utf8);
                string lf = FileOf(day, row) + ".login";
                if (string.IsNullOrEmpty(login)) { if (File.Exists(lf)) File.Delete(lf); }
                else File.WriteAllText(lf, me + "\n" + login, Utf8);
            }
            catch (Exception) { }
        }

        // Login nur, wenn die Zusatzdatei zum Namen in der Sperre passt (sonst hat eine aeltere exe die Sperre neu gesetzt)
        public string HolderLogin(DateTime day, int row)
        {
            try
            {
                string f = FileOf(day, row), lf = f + ".login";
                if (!File.Exists(f) || !File.Exists(lf) || DateTime.UtcNow - File.GetLastWriteTimeUtc(f) >= Stale) return "";
                string[] l = File.ReadAllText(lf, Utf8).Split('\n');
                return l.Length > 1 && l[0].Trim() == FirstLine(File.ReadAllText(f, Utf8)) ? l[1].Trim() : "";
            }
            catch (Exception) { return ""; }
        }

        public void Release(DateTime day, int row, string me)
        {
            try { string f = FileOf(day, row); if (File.Exists(f) && FirstLine(File.ReadAllText(f, Utf8)) == me) { File.Delete(f); if (File.Exists(f + ".login")) File.Delete(f + ".login"); } } catch (Exception) { }
        }
    }
}
