using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Arbeitsplan.Core
{
    // Passwortgeschuetzte Einstellungsdatei (z. B. spaeter Katalog, Nutzer-DB, Serveradresse).
    // Format: "APS1" | Salt(16) | IV(16) | AES-256-CBC(UTF-8-Text) | HMAC-SHA256 ueber alles davor (Encrypt-then-MAC).
    // Schluessel aus Passwort: PBKDF2 (Rfc2898, 100.000 Runden). Das Passwort steht NICHT im Programm.
    // Wer das Passwort nicht kennt, kann die Datei weder lesen noch unbemerkt aendern.
    public static class Protected
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("APS1");
        const int Iterations = 100000;

        static void Keys(string password, byte[] salt, out byte[] enc, out byte[] mac)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                enc = kdf.GetBytes(32);
                mac = kdf.GetBytes(32);
            }
        }

        // Gruppenrichtlinie "FIPS-konforme Algorithmen" sperrt die hier genutzten Managed-Klassen.
        static CryptographicException Fips() { return new CryptographicException("Verschlüsselung auf diesem PC nicht verfügbar (FIPS-Richtlinie)."); }

        public static byte[] Protect(string text, string password)
        {
            try { return ProtectCore(text, password); } catch (InvalidOperationException) { throw Fips(); }
        }

        static byte[] ProtectCore(string text, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Passwort fehlt.");
            byte[] salt = new byte[16], iv = new byte[16], enc, mac, cipher;
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            Keys(password, salt, out enc, out mac);
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = enc; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                byte[] plain = new UTF8Encoding(false).GetBytes(text);
                using (ICryptoTransform t = aes.CreateEncryptor()) cipher = t.TransformFinalBlock(plain, 0, plain.Length);
            }
            byte[] body = new byte[Magic.Length + 16 + 16 + cipher.Length];
            Buffer.BlockCopy(Magic, 0, body, 0, 4);
            Buffer.BlockCopy(salt, 0, body, 4, 16);
            Buffer.BlockCopy(iv, 0, body, 20, 16);
            Buffer.BlockCopy(cipher, 0, body, 36, cipher.Length);
            byte[] tag;
            using (HMACSHA256 h = new HMACSHA256(mac)) tag = h.ComputeHash(body);
            byte[] all = new byte[body.Length + 32];
            Buffer.BlockCopy(body, 0, all, 0, body.Length);
            Buffer.BlockCopy(tag, 0, all, body.Length, 32);
            return all;
        }

        // Wirft CryptographicException bei falschem Passwort, Manipulation oder fremdem Format (bewusst ohne Unterscheidung).
        public static string Unprotect(byte[] blob, string password)
        {
            try { return UnprotectCore(blob, password); } catch (InvalidOperationException) { throw Fips(); }
        }

        static string UnprotectCore(byte[] blob, string password)
        {
            if (blob == null || blob.Length < 4 + 16 + 16 + 16 + 32 || string.IsNullOrEmpty(password)) throw new CryptographicException("Ungültige Datei.");
            for (int i = 0; i < 4; i++) if (blob[i] != Magic[i]) throw new CryptographicException("Ungültige Datei.");
            byte[] salt = new byte[16], iv = new byte[16], enc, mac;
            Buffer.BlockCopy(blob, 4, salt, 0, 16);
            Buffer.BlockCopy(blob, 20, iv, 0, 16);
            Keys(password, salt, out enc, out mac);
            int bodyLen = blob.Length - 32;
            byte[] body = new byte[bodyLen], tag = new byte[32], calc;
            Buffer.BlockCopy(blob, 0, body, 0, bodyLen);
            Buffer.BlockCopy(blob, bodyLen, tag, 0, 32);
            using (HMACSHA256 h = new HMACSHA256(mac)) calc = h.ComputeHash(body);
            int diff = 0;
            for (int i = 0; i < 32; i++) diff |= calc[i] ^ tag[i];      // zeitkonstanter Vergleich
            if (diff != 0) throw new CryptographicException("Falsches Passwort oder Datei verändert.");
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = enc; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform t = aes.CreateDecryptor())
                    return new UTF8Encoding(false).GetString(t.TransformFinalBlock(blob, 36, bodyLen - 36));
            }
        }

        public static void Save(string path, string text, string password)
        {
            string tmp = path + ".tmp";   // erst vollstaendig schreiben, dann ersetzen (Absturz beim Schreiben laesst die alte Datei heil)
            File.WriteAllBytes(tmp, Protect(text, password));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        public static string Load(string path, string password) { return Unprotect(File.ReadAllBytes(path), password); }
    }
}
