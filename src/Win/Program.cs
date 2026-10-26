using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static class Program
    {
        static string Arg(string[] a, string name)
        {
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

#if !TESTHOOKS
        static void Fatal()
        {
            try { MessageBox.Show("Es ist ein unerwarteter Fehler aufgetreten. Nicht gespeicherte Eingaben können verloren gehen.\nDas Programm wird beendet.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Environment.Exit(1); }
        }
#endif

        // Startbildschirm zeigen, Name im Hintergrund suchen (blockiert nie); Splash.SetName wird per Timer im UI-Thread nachgereicht
        static string foundName; static bool lookupDone;
        static Splash StartSplash()
        {
            System.Threading.Thread th = new System.Threading.Thread(delegate () { try { foundName = Splash.LookupName(); } catch { } lookupDone = true; });
            th.IsBackground = true; th.Start();
            Splash s = new Splash();
            s.Show(); Application.DoEvents();
            Timer w = new Timer(); w.Interval = 50;
            w.Tick += delegate { if (lookupDone) { w.Stop(); s.SetName(foundName); } };
            w.Start();
            return s;
        }

        // Splash mind. 1 s sichtbar, schliesst sobald das Hauptfenster erscheint
        static void RunMain(MainForm m, Splash s, DateTime start)
        {
            while ((DateTime.Now - start).TotalMilliseconds < 1000) { Application.DoEvents(); System.Threading.Thread.Sleep(15); }
            m.Shown += delegate { s.Close(); };
            Application.Run(m);
        }

        // Fenster-Icon fuer alle Formulare (Hauptfenster, Dialoge) aus der exe
        static void InstallIcon()
        {
            Icon green, orange;
            try { green = Icon.ExtractAssociatedIcon(Application.ExecutablePath); orange = Recolor(green); } catch { return; }
            if (green == null) return;
            Timer t = new Timer(); t.Interval = 200;
            t.Tick += delegate
            {
                foreach (Form f in Application.OpenForms)
                {
                    if (f is Splash) continue;
                    MainForm mf = f as MainForm;
                    Icon want = mf != null && mf.HasUnsavedChanges && orange != null ? orange : green;
                    if (f.Icon != want) f.Icon = want;
                }
            };
            t.Start();
        }

        // Orange Variante: gruenen Haken umfaerben (orange = ungespeicherte Aenderungen)
        static Icon Recolor(Icon src)
        {
            using (Bitmap b = src.ToBitmap())
            {
                for (int y = 0; y < b.Height; y++)
                    for (int x = 0; x < b.Width; x++)
                    {
                        Color c = b.GetPixel(x, y);
                        if (c.A > 0 && c.G > c.R + 30 && c.G > c.B + 30) b.SetPixel(x, y, Color.FromArgb(c.A, Math.Min(255, c.G + 40), (int)(c.G * 0.6), c.B / 2));
                    }
                return Icon.FromHandle(b.GetHicon());
            }
        }
        // Aufruf: Arbeitsplan.exe [--theme xp|win7|win10] [--user <login>] [--nosave] [--4x3 | --size BxH]
        //   Test:  --shot <png> [--size BxH] [--edit spalte,zeile]  (rendert die Ansicht als Bild und beendet)
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Theme.Select(Arg(args, "--theme"));
            InstallIcon();
            DateTime t0 = DateTime.Now;   // xp | win7 | win10, sonst passend zum Betriebssystem (auch im Release)
#if TESTHOOKS
            string login = Arg(args, "--user") ?? Environment.UserName;
            bool nosave = Array.IndexOf(args, "--nosave") >= 0;
            string shot = Arg(args, "--shot");
            // Test: --4x3 startet im Fenster 1024x768 (4:3), --size BxH beliebig (auch ohne --shot)
            string sz = Arg(args, "--size") ?? (Array.IndexOf(args, "--4x3") >= 0 ? "1024x768" : null);
            Size cs = new Size(1280, 720);
            if (sz != null) cs = new Size(int.Parse(sz.Split('x')[0]), int.Parse(sz.Split('x')[1]));
            if (Arg(args, "--splash") != null)   // Test: Startbildschirm als Bild
            {
                Splash sp = StartSplash();
                for (int i = 0; i < 15; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
                using (Bitmap bmp = new Bitmap(sp.Width, sp.Height)) { using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(sp.Location, Point.Empty, sp.Size); bmp.Save(Arg(args, "--splash"), System.Drawing.Imaging.ImageFormat.Png); }
                return;
            }
            if (shot == null)
            {
                Splash sp = StartSplash();
                MainForm m = new MainForm(login, nosave);
                if (sz != null) m.ClientSize = cs; else m.FitToCursorScreen();
                RunMain(m, sp, t0);
                return;
            }

            MainForm f = new MainForm(login, nosave);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            f.ClientSize = cs;
            f.TopMost = true;
            f.Show();
            string edit = Arg(args, "--edit");
            if (edit != null) f.TestEdit(int.Parse(edit.Split(',')[0]), int.Parse(edit.Split(',')[1]));
            if (Arg(args, "--search") != null) f.TestSearch(Arg(args, "--search"));
            if (Arg(args, "--bench") != null) System.IO.File.WriteAllText(Arg(args, "--bench"), f.Bench(20).ToString("0.0") + " ms je Wechsel");
            if (Arg(args, "--selftest") != null) System.IO.File.WriteAllText(Arg(args, "--selftest"), f.SelfTest());
            if (Arg(args, "--scroll") != null) f.TestScroll(int.Parse(Arg(args, "--scroll")));
            Form target = Array.IndexOf(args, "--users") >= 0 ? f.TestUsers() : (Arg(args, "--form") != null ? f.TestForm(Arg(args, "--form")) : f);
            for (int i = 0; i < 12; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
            System.IO.File.WriteAllText(shot + ".txt", target.GetType().Name + " visible=" + target.Visible + " bounds=" + target.Bounds);
            using (Bitmap bmp = new Bitmap(target.Width, target.Height))   // Bildschirmabzug (DrawToBitmap zeichnet RichTextBox nicht)
            {
                if (target != f) target.DrawToBitmap(bmp, new Rectangle(0, 0, target.Width, target.Height));   // Dialoge: vollstaendig, unabhaengig von der Bildschirmskalierung
                else using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(target.Location, Point.Empty, target.Size);
                bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
            }
#else
            // Release: keine Testschalter, Nutzer = Windows-Anmeldename, neutrale Fehlermeldung statt Stacktrace-Dialog
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate { Fatal(); };
            AppDomain.CurrentDomain.UnhandledException += delegate { Fatal(); };
            Splash sp = StartSplash();
            MainForm rm = new MainForm(Environment.UserName, false); rm.FitToCursorScreen();
            RunMain(rm, sp, t0);
#endif
        }
    }
}
