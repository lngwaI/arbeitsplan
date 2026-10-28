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

        // Datenordner suchen (Release): erst der von diesem Nutzer gemerkte (je Programmordner), dann der aus der ini.
        // Nicht erreichbar -> Dialog. false = abgebrochen und kein Offline-Stand: Programm beenden.
        static bool FindDataFolder(Arbeitsplan.Core.AppConfig cfg, Splash sp)
        {
            string exeDir = Shortcut.Unc(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
            string mem = Arbeitsplan.Core.Umgebung.Remembered(Arbeitsplan.Core.Umgebung.RememberFile, exeDir);
            string got = mem != null ? Arbeitsplan.Core.Umgebung.FindData(mem, 5000) : null;
            if (got != null) { cfg.UseDataFolder(got); return true; }
            if (Arbeitsplan.Core.Umgebung.Reachable(cfg.DataFolder, 5000)) return true;   // auch leer: Ersteinrichtung/Meldung wie bisher
            if (sp != null) sp.Hide();
            bool cache = cfg.CacheDays > 0 && System.IO.File.Exists(cfg.CachePath);
            got = AskDataFolder(mem ?? cfg.DataFolder, cache);
            if (got == null) return cache;   // Abbrechen: mit Offline-Stand nur lesen, sonst Ende
            got = Shortcut.Unc(got);   // Laufwerksbuchstabe -> \\Server\Freigabe\... (an jedem PC gleich)
            Arbeitsplan.Core.Umgebung.Remember(Arbeitsplan.Core.Umgebung.RememberFile, exeDir, got);
            cfg.UseDataFolder(got);
            return true;
        }

        // Dialog "Datenordner nicht gefunden": Laufwerk oder \\Server\Freigabe\..., nur Ordner mit arbeitsplan.apdb (oder deren Ordner darueber)
        static string AskDataFolder(string current, bool cache)
        {
            using (Form f = new Form())
            {
                f.Text = "Arbeitsplan - Datenordner nicht gefunden";
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false;
                f.StartPosition = FormStartPosition.CenterScreen; f.Font = Theme.Current.UiFont();
                f.ClientSize = new Size(560, 200);
                Label l = new Label(); l.SetBounds(12, 12, 536, 96);
                l.Text = "Die Arbeitsplan-Daten wurden nicht gefunden:\n" + current + "\n\nBitte prüfen, ob das Laufwerk verbunden ist, oder den Datenordner angeben: "
                    + "Laufwerk (z. B. L:\\Arbeitsplan\\daten) oder Netzwerkpfad (\\\\server\\freigabe\\Arbeitsplan\\daten). Darin liegt die Datei "
                    + Arbeitsplan.Core.FileStore.FileName + "; es wird nichts angelegt.";
                TextBox t = new TextBox(); t.SetBounds(12, 116, 436, 23); t.Text = current;
                Button br = new Button(), ok = new Button(), no = new Button();
                br.Text = "Durchsuchen…"; br.SetBounds(454, 115, 94, 26);
                ok.Text = "Öffnen"; ok.SetBounds(cache ? 196 : 352, 160, 96, 28);
                no.Text = cache ? "Ohne Verbindung (nur Lesen)" : "Beenden"; no.SetBounds(cache ? 298 : 454, 160, cache ? 250 : 94, 28);
                no.DialogResult = DialogResult.Cancel;
                f.AcceptButton = ok; f.CancelButton = no;
                string found = null;
                br.Click += delegate
                {
                    using (FolderBrowserDialog d = new FolderBrowserDialog())
                    {
                        d.Description = "Datenordner des Arbeitsplans wählen (oder den Ordner darüber, in dem \"daten\" liegt)";
                        d.ShowNewFolderButton = false;
                        if (d.ShowDialog(f) == DialogResult.OK) t.Text = d.SelectedPath;
                    }
                };
                ok.Click += delegate
                {
                    f.Cursor = Cursors.WaitCursor;
                    found = t.Text.Trim().Length > 0 ? Arbeitsplan.Core.Umgebung.FindData(t.Text, 5000) : null;
                    f.Cursor = Cursors.Default;
                    if (found != null) { f.DialogResult = DialogResult.OK; return; }
                    MessageBox.Show(f, "Dort liegt keine Datei " + Arbeitsplan.Core.FileStore.FileName + " oder der Ordner ist nicht erreichbar:\n" + t.Text,
                        "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
                f.Controls.AddRange(new Control[] { l, t, br, ok, no });
                Theme.Current.Style(f);
                return f.ShowDialog() == DialogResult.OK ? found : null;
            }
        }

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
            if (Arg(args, "--data") != null)
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
            if (mk != null) { Arbeitsplan.Core.Package.SampleCampaign(System.IO.Path.GetFileNameWithoutExtension(mk)).Save(mk); return; }
            if (shot == null)
            {
                Splash sp = StartSplash();
                MainForm m = new MainForm(login, nosave, cfg, fs);
                if (sz != null) m.ClientSize = cs; else m.FitToCursorScreen();
                m.Shown += delegate { m.BeginInvoke((MethodInvoker)delegate { m.OfferPufferDefaults(); }); };
                RunMain(m, sp, t0);
                return;
            }

            MainForm f = new MainForm(login, nosave, cfg, fs);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            f.ClientSize = cs;
            f.TopMost = true;
            f.Show();
            string edit = Arg(args, "--edit");
            if (edit != null) f.TestEdit(int.Parse(edit.Split(',')[0]), int.Parse(edit.Split(',')[1]));
            if (Arg(args, "--applypkg") != null) f.TestApply(Arg(args, "--applypkg"));
            if (Arg(args, "--go") != null) f.TestGo(int.Parse(Arg(args, "--go")));   // Fenster um n Tage verschieben (z. B. -2 = ab Samstag)
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
            rm.Shown += delegate { rm.BeginInvoke((MethodInvoker)delegate { Shortcut.Offer(rm); rm.OfferPufferDefaults(); }); };   // Desktop-Verknuepfung anbieten; alter Bestand: Puffer-Standards (nur Admin, Rueckfrage)
            RunMain(rm, sp, t0);
#endif
        }
    }
}
