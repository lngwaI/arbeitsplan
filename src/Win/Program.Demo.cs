#if DEMO
using System;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Start der Vorfuehr-Version (Build mit DEMO): eigener Datenordner, Rollenwahl statt Windows-Login, keine Umgebungspruefung.
    static partial class Program
    {
        const string DemoTitle = "Arbeitsplan – VORFÜHRUNG";

        // false = Programm beenden (Meldung wurde gezeigt). Testlauf (--shot): eigener Temp-Ordner, Rolle Admin ohne Dialog.
        static bool DemoStart(string[] args, bool test, bool nosave, ref string login, out AppConfig cfg, out FileStore fs)
        {
            cfg = null; fs = null;
            if (Arg(args, "--makepkg") != null || Arg(args, "--splash") != null) return true;   // reine Testhilfen ohne Daten
            if (Arg(args, "--stress") != null || Arg(args, "--stresscheck") != null) return false;   // Lasttest nur in der normalen exe
            cfg = DemoEnv.Config(test ? DemoEnv.TestRoot : DemoEnv.DefaultRoot);
            string data = Arg(args, "--data");
            if (data != null)   // erlaubt nur Demo-Ordner (DemoEnv.CheckFolder), nie die echten Daten
            {
                string bad = DemoEnv.CheckFolder(data);
                if (bad != null) { MessageBox.Show(bad, DemoTitle, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
                cfg.DataFolder = System.IO.Path.GetFullPath(data); cfg.PackageFolder = System.IO.Path.Combine(cfg.DataFolder, "Pakete");
            }
            cfg.ReadOnly |= nosave;
            string problem;
            if (test && data == null) { string r = System.IO.Directory.Exists(cfg.DataFolder) ? DemoEnv.Reset(cfg, true) : null; if (r != null) { MessageBox.Show(r, DemoTitle); return false; } }
            fs = DemoEnv.Open(cfg, out problem);
            if (fs == null) { MessageBox.Show(problem, DemoTitle, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            string role = Arg(args, "--role") ?? (test ? UserDb.AdminId : null);
            User u = fs.Users.FindByLogin(Arg(args, "--user") ?? "");
            if (u == null && role != null) u = fs.Users.Users.Find(delegate(User x) { return x.RoleId == role; });
            if (u == null)
            {
                using (DemoRoleForm f = new DemoRoleForm(fs.Users, role))
                {
                    if (f.ShowDialog() != DialogResult.OK) return false;
                    u = fs.Users.FindByLogin(f.Login);
                }
            }
            if (u == null) return false;
            login = u.Login;
            return true;
        }

        // Hauptfenster laufen lassen; danach ggf. neu starten (Rolle wechseln / Demo zuruecksetzen, siehe MainForm.Demo.cs)
        static void DemoRun(MainForm m, AppConfig cfg)
        {
            Application.Run(m);
            string act = m.DemoRestart;
            if (act == null) return;
            if (act == "reset")
            {
                string err = DemoEnv.Reset(cfg);
                if (err != null) MessageBox.Show(err, DemoTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            try { System.Diagnostics.Process.Start(Application.ExecutablePath, act == "reset" ? "--role " + m.DemoRoleId : ""); } catch (Exception) { }
        }
    }
}
#endif
