using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static partial class Program
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

        // Daten oeffnen und Start pruefen (Umgebung.Open); Ersteinrichtung nach Rueckfrage nur fuer Admins= aus der ini.
        // null = Programm beenden (Meldung wurde gezeigt). ask = false: Ersteinrichtung ohne Rueckfrage (nur Testbilder).
        static Arbeitsplan.Core.FileStore OpenData(Arbeitsplan.Core.AppConfig cfg, string login, Splash sp, bool ask)
        {
            if (Arbeitsplan.Core.Umgebung.NeedsSetup(cfg.DataFolder) && Arbeitsplan.Core.Umgebung.IsSetupAdmin(cfg, login))
            {
                if (sp != null) sp.Hide();
                if (ask && MessageBox.Show("Neuen Arbeitsplan einrichten?\n\nDatenordner: " + cfg.DataFolder
                    + "\n\nEs werden die verschlüsselte Datendatei und die Schlüsseldatei angelegt; Sie werden Admin."
                    + "\nNur einmal nötig. Bei bereits vorhandenen Daten: Nein und den Datenordner prüfen.",
                    "Arbeitsplan - Ersteinrichtung", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return null;
                string err = Arbeitsplan.Core.Umgebung.Setup(cfg);
                if (err != null) { MessageBox.Show(err, "Arbeitsplan - Ersteinrichtung", MessageBoxButtons.OK, MessageBoxIcon.Error); return null; }
            }
            Arbeitsplan.Core.FileStore fs;
            string msg = Arbeitsplan.Core.Umgebung.Open(cfg, login, out fs);
            if (msg == null) return fs;
            if (sp != null) sp.Hide();
            MessageBox.Show(msg, "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        // Splash mind. 1 s sichtbar, schliesst sobald das Hauptfenster erscheint
        static void RunMain(MainForm m, Splash s, DateTime start)
        {
            while ((DateTime.Now - start).TotalMilliseconds < 1000) { Application.DoEvents(); System.Threading.Thread.Sleep(15); }
            m.Shown += delegate
            {
                s.Close();
                string msg = m.StartMessage;   // OFFLINE (letzter Stand dieses PCs) …
                if (msg != null) m.BeginInvoke((MethodInvoker)delegate { MessageBox.Show(m, msg, "Arbeitsplan - Daten", MessageBoxButtons.OK, MessageBoxIcon.Warning); });
            };
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
        // Aufruf: Arbeitsplan.exe [--user <login>] [--nosave] [--data <ordner>] [--4x3 | --size BxH]
        //   Release: Daten laut Arbeitsplan.ini neben der exe (FileStore); Test-Build ohne --data: Testdaten im Speicher
        //   Test:  --shot <png> [--size BxH] [--edit spalte,zeile]  (rendert die Ansicht als Bild und beendet)
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#if !TESTHOOKS
            // Release: neutrale Fehlermeldung statt Stacktrace-Dialog; muss vor dem ersten Fenster/Timer stehen (sonst InvalidOperationException)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate { Fatal(); };
            AppDomain.CurrentDomain.UnhandledException += delegate { Fatal(); };
#else
            // Test-Build: unbehandelte Ausnahme mit vollem Stacktrace nach %TEMP%\arbeitsplan_ausnahme.txt und anzeigen
            Application.ThreadException += delegate(object o, System.Threading.ThreadExceptionEventArgs e)
            {
                string p = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arbeitsplan_ausnahme.txt");
                try { System.IO.File.AppendAllText(p, DateTime.Now.ToString("s") + "\r\n" + e.Exception + "\r\n\r\n"); } catch (Exception) { }
                MessageBox.Show(e.Exception.ToString(), "Arbeitsplan (Test-Build): Ausnahme, gespeichert in " + p, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
#endif

            Theme.Select(null);
            InstallIcon();
            DateTime t0 = DateTime.Now;
#if TESTHOOKS
            string login = Arg(args, "--user") ?? Environment.UserName;
            bool nosave = Array.IndexOf(args, "--nosave") >= 0;
            string shot = Arg(args, "--shot");
            // Test: --4x3 startet im Fenster 1024x768 (4:3), --size BxH beliebig (auch ohne --shot)
            string sz = Arg(args, "--size") ?? (Array.IndexOf(args, "--4x3") >= 0 ? "1024x768" : null);
            Size cs = new Size(1280, 720);
            if (sz != null) cs = new Size(int.Parse(sz.Split('x')[0]), int.Parse(sz.Split('x')[1]));
            if (Arg(args, "--weeks") == "2") MainForm.TestWeeks = 2;
            // Test: --data <ordner> nutzt die verschluesselte Datendatei (wie Release) statt der Testdaten; Testadmin mmuster
            Arbeitsplan.Core.AppConfig cfg = null;
            Arbeitsplan.Core.FileStore fs = null;
#if DEMO
            // Vorfuehr-Version: eigener Demo-Datenordner, Rollenwahl statt Windows-Login (Program.Demo.cs)
            if (!DemoStart(args, shot != null, nosave, ref login, out cfg, out fs)) return;
#endif
            if (!Arbeitsplan.Core.AppId.IsDemo && Arg(args, "--data") != null)
            {
                cfg = Arbeitsplan.Core.AppConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
                cfg.DataFolder = System.IO.Path.GetFullPath(Arg(args, "--data")); cfg.Note = null;
                cfg.BackupFolder = System.IO.Path.Combine(cfg.DataFolder, "sicherung");
                cfg.PackageFolder = System.IO.Path.Combine(cfg.DataFolder, "Pakete");
                cfg.CachePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arbeitsplan_testcache.apdb");
                cfg.ReadOnly |= nosave; cfg.Admins.Add("mmuster");
                System.IO.Directory.CreateDirectory(cfg.DataFolder);   // Test: leerer Ordner = Ersteinrichtung (mmuster)
                fs = OpenData(cfg, login, null, shot == null);
                if (fs == null) return;
            }
            if (Arg(args, "--splash") != null)   // Test: Startbildschirm als Bild
            {
                Splash sp = StartSplash();
                for (int i = 0; i < 15; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
                using (Bitmap bmp = new Bitmap(sp.Width, sp.Height)) { using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(sp.Location, Point.Empty, sp.Size); bmp.Save(Arg(args, "--splash"), System.Drawing.Imaging.ImageFormat.Png); }
                return;
            }
            // Test: Lasttest wie zwei PCs auf demselben Datenordner (Workflow startet zwei gleichzeitig), danach Pruefung
            if (Arg(args, "--stress") != null || Arg(args, "--stresscheck") != null)
            {
                string res;
                try { res = Arg(args, "--stress") != null ? MainForm.Stress(Arg(args, "--stress"), Arg(args, "--name"), int.Parse(Arg(args, "--count"))) : MainForm.StressCheck(Arg(args, "--stresscheck"), int.Parse(Arg(args, "--count"))); }
                catch (Exception ex) { res = "Mehrplatz-Prozesse: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
                System.IO.File.WriteAllText(Arg(args, "--out"), res);
                return;
            }
            string mk = Arg(args, "--makepkg");   // Test: Testpaket als Datei erzeugen (Name = Dateiname), z. B. testpakete\Name.apkg
            if (mk != null) { string mkn = System.IO.Path.GetFileNameWithoutExtension(mk); (mkn.StartsWith("UX5b") ? PackageForm.Ux5bSample(mkn) : mkn.StartsWith("UX5f") ? PackageForm.Ux5fSample(mkn) :mkn.StartsWith("PB") ? Arbeitsplan.Core.Package.SampleBuffers(mkn) : Arbeitsplan.Core.Package.SampleCampaign(mkn)).Save(mk); return; }   // PB...: mit 3 mobilen Puffern
            if (Arg(args, "--makebeispiel") != null) { MakeBeispiel(Arg(args, "--makebeispiel"), Arg(args, "--only") == "packages"); return; }   // beispiel\: Pakete + Ausgangsstand als Sicherung (scripts\beispiel-bauen.cmd)
            if (shot == null)
            {
#if DEMO
                MainForm dm = new MainForm(login, nosave, cfg, fs);
                if (sz != null) dm.ClientSize = cs; else dm.FitToCursorScreen();
                DemoRun(dm, cfg);
                return;
#else
                Splash sp = StartSplash();
                MainForm m = new MainForm(login, nosave, cfg, fs);
                if (sz != null) m.ClientSize = cs; else m.FitToCursorScreen();
                m.Shown += delegate { m.QueueStartup(m.OfferPufferDefaults); };
                RunMain(m, sp, t0);
                return;
#endif
            }

            MainForm f = new MainForm(login, nosave, cfg, fs);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            f.ClientSize = cs;
            f.TopMost = true;
            f.Show();
            string edit = Arg(args, "--edit");
            if (edit != null) f.TestEdit(int.Parse(edit.Split(',')[0]), int.Parse(edit.Split(',')[1]));
            if (Arg(args, "--applypkg") != null) f.TestApply(Arg(args, "--applypkg"), Arg(args, "--runs") != null ? int.Parse(Arg(args, "--runs")) : 0);   // --runs n: Laufanzahl wie im Kampagnenstart aendern
            if (Arg(args, "--go") != null) f.TestGo(int.Parse(Arg(args, "--go")));   // Fenster um n Tage verschieben (z. B. -2 = ab Samstag)
            if (Array.IndexOf(args, "--made") >= 0) f.TestMade();   // Einstellung "Puffer hergestellt" an, ein Puffer hergestellt (Wochenansicht-Shot)
            if (Arg(args, "--status") != null) f.TestStatusArg(Arg(args, "--status"));   // "H1-X-0102=Quarantaene,H1-T-0102=" (leer = entfernen)
            if (Arg(args, "--search") != null) f.TestSearch(Arg(args, "--search"));
            if (Arg(args, "--bench") != null) System.IO.File.WriteAllText(Arg(args, "--bench"), f.Bench(20).ToString("0.0") + " ms je Wechsel");
            if (Arg(args, "--selftest") != null)
            {
                string res;
                try { res = f.SelfTest(); } catch (Exception ex) { res = "FEHLER: Selbsttest abgebrochen: " + ex; }
                System.IO.File.WriteAllText(Arg(args, "--selftest"), res);
            }
            if (Arg(args, "--hintclick") != null) System.IO.File.WriteAllText(Arg(args, "--hintclick"), f.TestHintClick());
            if (Arg(args, "--weeks") == "late") f.TestWeeksLate();   // wie Menue "2 Wochen" nach dem Start
            if (Arg(args, "--scroll") != null) f.TestScroll(int.Parse(Arg(args, "--scroll")));
            Form target = Array.IndexOf(args, "--users") >= 0 ? f.TestUsers() : (Arg(args, "--form") != null ? f.TestForm(Arg(args, "--form")) : f);
            for (int i = 0; i < 12; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
            System.IO.File.WriteAllText(shot + ".txt", target.GetType().Name + " visible=" + target.Visible + " bounds=" + target.Bounds);
            using (Bitmap bmp = new Bitmap(target.Width, target.Height))   // Bildschirmabzug (DrawToBitmap zeichnet RichTextBox nicht)
            {
                if (target != f && Array.IndexOf(args, "--screen") < 0) target.DrawToBitmap(bmp, new Rectangle(0, 0, target.Width, target.Height));   // Dialoge: vollstaendig, unabhaengig von der Bildschirmskalierung
                else using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(target.Location, Point.Empty, target.Size);
                bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
            }
#else
            // Release: keine Testschalter, Nutzer = Windows-Anmeldename (Fehlerbehandlung siehe Anfang von Main)
            Splash sp = StartSplash();
            Arbeitsplan.Core.AppConfig rc = Arbeitsplan.Core.AppConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
            if (!FindDataFolder(rc, sp)) { sp.Close(); return; }   // Laufwerk nicht verbunden / neuer PC: Datenordner waehlen
            Arbeitsplan.Core.FileStore rs = OpenData(rc, Environment.UserName, sp, true);   // nur im Firmennetz, nur angelegte Nutzer
            if (rs == null) { sp.Close(); return; }
            MainForm rm = new MainForm(Environment.UserName, false, rc, rs); rm.FitToCursorScreen();
            rm.Shown += delegate { rm.QueueStartup(delegate { Shortcut.Offer(rm); }); rm.QueueStartup(rm.OfferPufferDefaults); };   // nacheinander: Meldungen, Desktop-Verknuepfung, alter Bestand: Puffer-Standards (nur Admin)
            RunMain(rm, sp, t0);
#endif
        }
    }
}
