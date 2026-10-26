using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Arbeitsplan.Core
{
    // Passwortgeschuetzte Einstellungsdatei (z. B. spaeter Katalog, Nutzer-DB, Serveradresse).
    // Format: "APS1" | Salt(16) | IV(16) | AES-256-CBC(UTF-8-Text) | HMAC-SHA256 ueber alles davor (Encrypt-then-MAC).
    // Schluessel aus Passwort: PBKDF2 (Rfc2898, 100.000 Runden). Das Passwort steht NICHT im Programm
    // (Ausnahme: Datendatei des FileStore, dort verschleiert eingebettet, siehe dort).
    // Wer das Passwort nicht kennt, kann die Datei weder lesen noch unbemerkt aendern.
    public static class Protected
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("APS1");
        const int Iterations = 100000;

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        static void Keys(string password, byte[] salt, int iterations, out byte[] enc, out byte[] mac)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                enc = kdf.GetBytes(32);
                mac = kdf.GetBytes(32);
            }
        }

        // Gruppenrichtlinie "FIPS-konforme Algorithmen" sperrt die hier genutzten Managed-Klassen.
        static CryptographicException Fips() { return new CryptographicException("Verschlüsselung auf diesem PC nicht verfügbar (FIPS-Richtlinie)."); }

        public static byte[] Protect(string text, string password) { return ProtectBytes(Utf8.GetBytes(text), password, Iterations); }

        // iterations: weniger Runden nur, wenn das Passwort selbst schon zufaellig/lang ist (Datendatei, siehe FileStore)
        public static byte[] ProtectBytes(byte[] plain, string password, int iterations)
        {
            try { return ProtectCore(plain, password, iterations); } catch (InvalidOperationException) { throw Fips(); }
        }

        static byte[] ProtectCore(byte[] plain, string password, int iterations)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Passwort fehlt.");
            byte[] salt = new byte[16], iv = new byte[16], enc, mac, cipher;
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            Keys(password, salt, iterations, out enc, out mac);
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = enc; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform t = aes.CreateEncryptor()) cipher = t.TransformFinalBlock(plain, 0, plain.Length);
            }
            int bodyLen = Magic.Length + 16 + 16 + cipher.Length;
            byte[] all = new byte[bodyLen + 32], tag;
            Buffer.BlockCopy(Magic, 0, all, 0, 4);
            Buffer.BlockCopy(salt, 0, all, 4, 16);
            Buffer.BlockCopy(iv, 0, all, 20, 16);
            Buffer.BlockCopy(cipher, 0, all, 36, cipher.Length);
            using (HMACSHA256 h = new HMACSHA256(mac)) tag = h.ComputeHash(all, 0, bodyLen);
            Buffer.BlockCopy(tag, 0, all, bodyLen, 32);
            return all;
        }

        // Wirft CryptographicException bei falschem Passwort, Manipulation oder fremdem Format (bewusst ohne Unterscheidung).
        public static string Unprotect(byte[] blob, string password) { return Utf8.GetString(UnprotectBytes(blob, password, Iterations)); }

        public static byte[] UnprotectBytes(byte[] blob, string password, int iterations)
        {
            try { return UnprotectCore(blob, password, iterations); } catch (InvalidOperationException) { throw Fips(); }
        }

        static byte[] UnprotectCore(byte[] blob, string password, int iterations)
        {
            if (blob == null || blob.Length < 4 + 16 + 16 + 16 + 32 || string.IsNullOrEmpty(password)) throw new CryptographicException("Ungültige Datei.");
            for (int i = 0; i < 4; i++) if (blob[i] != Magic[i]) throw new CryptographicException("Ungültige Datei.");
            byte[] salt = new byte[16], iv = new byte[16], enc, mac;
            Buffer.BlockCopy(blob, 4, salt, 0, 16);
            Buffer.BlockCopy(blob, 20, iv, 0, 16);
            Keys(password, salt, iterations, out enc, out mac);
            int bodyLen = blob.Length - 32;
            byte[] calc;
            using (HMACSHA256 h = new HMACSHA256(mac)) calc = h.ComputeHash(blob, 0, bodyLen);
            int diff = 0;
            for (int i = 0; i < 32; i++) diff |= calc[i] ^ blob[bodyLen + i];      // zeitkonstanter Vergleich
            if (diff != 0) throw new CryptographicException("Falsches Passwort oder Datei verändert.");
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = enc; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform t = aes.CreateDecryptor())
                    return t.TransformFinalBlock(blob, 36, bodyLen - 36);
            }
        }

        public static void Save(string path, string text, string password)
        {
            string tmp = path + ".tmp";   // erst vollstaendig schreiben, dann ersetzen (Absturz beim Schreiben laesst die alte Datei heil)
            File.WriteAllBytes(tmp, Protect(text, password));
            if (!File.Exists(path)) { File.Move(tmp, path); return; }
            try { File.Replace(tmp, path, null); }   // atomar: es gibt immer entweder die alte oder die neue Datei
            catch (IOException) { File.Copy(tmp, path, true); File.Delete(tmp); }   // Laufwerke ohne Replace
            catch (PlatformNotSupportedException) { File.Copy(tmp, path, true); File.Delete(tmp); }
        }
        public static string Load(string path, string password) { return Unprotect(File.ReadAllBytes(path), password); }
    }
}
