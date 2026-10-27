using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Verknuepfung "Arbeitsplan" auf dem Desktop. Liegt der Desktop in OneDrive, ist sie auf jedem PC des Nutzers da.
    // Ziel ist immer der UNC-Pfad (\\Server\Freigabe\...), damit sie auch ohne verbundenen Laufwerksbuchstaben funktioniert.
    static class Shortcut
    {
        const string LinkName = "Arbeitsplan.lnk";
        static string Desktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
        static string NoAskFile { get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arbeitsplan"), "keine-verknuepfung"); } }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

        // Pfad auf einem verbundenen Netzlaufwerk (z. B. L:\...) -> \\Server\Freigabe\...; sonst unveraendert
        public static string Unc(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (root == null || root.Length < 2 || root[1] != ':') return path;
                StringBuilder sb = new StringBuilder(1024); int len = sb.Capacity;
                if (WNetGetConnection(root.Substring(0, 2), sb, ref len) != 0) return path;
                return sb.ToString().TrimEnd('\\') + "\\" + path.Substring(root.Length).TrimStart('\\');
            }
            catch (Exception) { return path; }
        }

        public static bool OnNetwork(string path)
        {
            if (path.StartsWith(@"\\")) return true;
            try { return new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network; }
            catch (Exception) { return false; }
        }

        // Wo Arbeitsplan-Verknuepfungen liegen koennen: Desktop, OneDrive-Desktop, Startmenue (Programme + eine Ebene Unterordner)
        public static List<string> Folders()
        {
            List<string> l = new List<string>();
            l.Add(Desktop);
            foreach (string v in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
            {
                string od = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(od)) l.Add(Path.Combine(od, "Desktop"));
            }
            string pr = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            l.Add(pr);
            try { if (Directory.Exists(pr)) l.AddRange(Directory.GetDirectories(pr)); } catch (Exception) { }
            return l;
        }

        // Vorhandene Arbeitsplan-Verknuepfungen (Verknuepfung -> Ziel): Ziel heisst Arbeitsplan.exe oder der Name beginnt mit "Arbeitsplan"
        public static List<KeyValuePair<string, string>> Find(IEnumerable<string> folders)
        {
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in folders)
            {
                string[] files;
                try { if (!seen.Add(Path.GetFullPath(f).TrimEnd('\\')) || !Directory.Exists(f)) continue; files = Directory.GetFiles(f, "*.lnk"); }
                catch (Exception) { continue; }
                foreach (string lnk in files)
                    try
                    {
                        string t = Target(lnk);
                        if (string.Equals(Path.GetFileName(t), "Arbeitsplan.exe", StringComparison.OrdinalIgnoreCase)
                            || Path.GetFileName(lnk).StartsWith("Arbeitsplan", StringComparison.OrdinalIgnoreCase))
                            l.Add(new KeyValuePair<string, string>(lnk, t));
                    }
                    catch (Exception) { }
            }
            return l;
        }

        // Zeigt das Ziel auf diese exe (auch L:\... gegen \\Server\...)?
        public static bool Same(string target, string exe)
        {
            try { return target.Length > 0 && string.Equals(Unc(Path.GetFullPath(target)), Unc(Path.GetFullPath(exe)), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        // Menue Hilfe: vorhandene Verknuepfungen zeigen; keine zeigt auf dieses Programm -> Desktop-Verknuepfung anbieten (nie automatisch aendern)
        public static void Check(IWin32Window owner)
        {
            string exe = Application.ExecutablePath;
            StringBuilder sb = new StringBuilder();
            bool ok = false;
            foreach (KeyValuePair<string, string> k in Find(Folders()))
            {
                bool s = Same(k.Value, exe); ok |= s;
                sb.Append("\n• " + k.Key + "\n    → " + (k.Value.Length > 0 ? k.Value : "(kein Ziel)") + (s ? "  (dieses Programm)" : "  (anderer Ort / alte Version)"));
            }
            string head = sb.Length > 0 ? "Gefundene Verknüpfungen:" + sb + "\n\n" : "Keine Arbeitsplan-Verknüpfung gefunden.\n\n";
            if (ok) { MessageBox.Show(owner, head + "Mindestens eine Verknüpfung startet dieses Programm.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show(owner, head + "Desktop-Verknüpfung \"Arbeitsplan\" auf dieses Programm anlegen?\n" + Unc(exe),
                "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Create(owner);
        }

        // Beim Start vom Laufwerk einmal anbieten; nicht, wenn schon eine Arbeitsplan-Verknuepfung da ist oder "nicht mehr fragen" gewaehlt wurde
        public static void Offer(IWin32Window owner)
        {
            try
            {
                if (!OnNetwork(Application.ExecutablePath) || File.Exists(NoAskFile) || Find(Folders()).Count > 0) return;
            }
            catch (Exception) { return; }
            DialogResult r = MessageBox.Show(owner, "Verknüpfung \"Arbeitsplan\" auf dem Desktop anlegen?\n\nDann starten Sie den Arbeitsplan künftig per Doppelklick, "
                + "ohne sich durch die Ordner zu klicken. Liegt Ihr Desktop in OneDrive, ist die Verknüpfung auch an jedem anderen PC da, an dem Sie sich anmelden.\n\n"
                + "Ja = anlegen      Nein = nicht mehr fragen      Abbrechen = später", "Arbeitsplan", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) Create(owner);
            else if (r == DialogResult.No)
                try { Directory.CreateDirectory(Path.GetDirectoryName(NoAskFile)); File.WriteAllText(NoAskFile, ""); } catch (Exception) { }
        }

        // Menue Hilfe: Verknuepfung (neu) anlegen
        public static void Create(IWin32Window owner)
        {
            string target = Unc(Application.ExecutablePath), lnk = Path.Combine(Desktop, LinkName);
            try
            {
                Write(lnk, target);
                MessageBox.Show(owner, "Verknüpfung angelegt:\n" + lnk + "\n\nZiel: " + target
                    + (OnNetwork(target) ? "" : "\n\nHinweis: Das Programm liegt nicht auf dem Laufwerk, sondern nur auf diesem PC."),
                    "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(owner, "Die Verknüpfung konnte nicht angelegt werden.\n" + ex.Message, "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        public static void Write(string lnk, string target)
        {
            IShellLinkW l = (IShellLinkW)new ShellLink();
            try
            {
                l.SetPath(target);
                l.SetWorkingDirectory(Path.GetDirectoryName(target));
                l.SetDescription("Arbeitsplan");
                l.SetIconLocation(target, 0);
                ((IPersistFile)l).Save(lnk, true);
            }
            finally { Marshal.ReleaseComObject(l); }
        }

        // Ziel einer Verknuepfung (Selbsttest)
        public static string Target(string lnk)
        {
            IShellLinkW l = (IShellLinkW)new ShellLink();
            try
            {
                ((IPersistFile)l).Load(lnk, 0);
                StringBuilder sb = new StringBuilder(1024);
                l.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                return sb.ToString();
            }
            finally { Marshal.ReleaseComObject(l); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
