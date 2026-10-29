using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static partial class Program
    {
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
    }
}
