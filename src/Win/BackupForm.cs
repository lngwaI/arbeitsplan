using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Einstellungen -> Sicherung (Recht PackageAdmin): Ordner waehlen, jetzt sichern, Sicherung wiederherstellen.
    // Der Aufrufer beendet vorher den Bearbeitungsmodus und baut die Ansicht danach neu auf.
    class BackupForm : Form
    {
        readonly IDataStore store;
        readonly string iniPath;
        string folder;
        readonly TextBox tbFolder = new TextBox();
        readonly Label lastLabel = new Label();
        readonly ListBox files = new ListBox();
        readonly NumericUpDown nHours = new NumericUpDown();
        readonly Button bFolder = new Button(), bNow = new Button(), bRestore = new Button(), bFile = new Button(), bClose = new Button();

        // defaultHours: SicherungAlleStunden= aus Arbeitsplan.ini; die Auswahl hier (sicherung.ini) hat Vorrang
        public BackupForm(IDataStore store, string iniPath, string folder, int defaultHours = 24)
        {
            this.store = store; this.iniPath = iniPath; this.folder = folder;
            Text = "Datensicherung";
            Ui.Dialog(this, 620, 420);

            Label lf = new Label(); lf.Text = "Ordner:"; lf.SetBounds(10, 14, 60, 18);
            tbFolder.SetBounds(72, 11, 430, 22); tbFolder.ReadOnly = true; Ui.SimpleMenu(tbFolder);
            bFolder.Text = "Ändern…"; bFolder.SetBounds(510, 9, 100, 26); bFolder.Click += delegate { PickFolder(); };

            Label info = new Label();
            info.Text = "Gesichert werden alle Pläne (ohne Zeitgrenze), Blockstruktur, Equipment/Bausteine und das Änderungsprotokoll.\n"
                + "Automatisch im laufenden Programm und beim Beenden, wenn die letzte Sicherung älter als eingestellt ist. Die letzten " + Backup.Keep + " Sicherungen bleiben erhalten.";
            info.SetBounds(10, 42, 600, 50);
            Label lh = new Label(); lh.Text = "Automatisch sichern alle"; lh.AutoSize = true; lh.Location = new Point(10, 99);
            nHours.Minimum = 1; nHours.Maximum = Backup.MaxHours; nHours.Value = Backup.LoadHours(iniPath, defaultHours);
            nHours.SetBounds(lh.Left + lh.PreferredWidth + 6, 96, 60, 22); nHours.TextAlign = HorizontalAlignment.Right;
            Label lu = new Label(); lu.Text = "Stunden"; lu.AutoSize = true; lu.Location = new Point(nHours.Right + 6, 99);
            nHours.ValueChanged += delegate { SaveHours(); };
            lastLabel.SetBounds(10, 128, 600, 18);
            files.SetBounds(10, 150, 600, 214); files.IntegralHeight = false;
            files.DoubleClick += delegate { RestoreSelected(false); };

            bNow.Text = "Jetzt sichern"; bNow.SetBounds(10, 378, 130, 30); bNow.Click += delegate { BackupNow(); };
            bRestore.Text = "Wiederherstellen…"; bRestore.SetBounds(148, 378, 150, 30); bRestore.Click += delegate { RestoreSelected(false); };
            bFile.Text = "Andere Datei…"; bFile.SetBounds(306, 378, 130, 30); bFile.Click += delegate { RestoreSelected(true); };
            bClose.Text = "Schließen"; bClose.SetBounds(515, 378, 95, 30); bClose.DialogResult = DialogResult.Cancel;
            CancelButton = bClose;

            Controls.AddRange(new Control[] { lf, tbFolder, bFolder, info, lh, nHours, lu, lastLabel, files, bNow, bRestore, bFile, bClose });
            Fill();
            ActiveControl = files;
        }

        void Fill()
        {
            tbFolder.Text = folder;
            files.Items.Clear();
            string[] f = Backup.Files(folder);
            for (int i = f.Length - 1; i >= 0; i--) files.Items.Add(Path.GetFileName(f[i]));
            DateTime? last = Backup.Last(folder);
            lastLabel.Text = "Letzte Sicherung: " + (last == null ? "noch keine" : last.Value.ToString("dd.MM.yyyy HH:mm")
                + "  ·  nächste automatisch ab " + last.Value.AddHours((int)nHours.Value).ToString("dd.MM.yyyy HH:mm")) + "   (Doppelklick = wiederherstellen)";
            lastLabel.AutoEllipsis = true;
            bRestore.Enabled = files.Items.Count > 0;
            if (files.Items.Count > 0) files.SelectedIndex = 0;
        }

        static void Error(string what, Exception ex) { MessageBox.Show(what + "\n" + ex.Message, "Datensicherung", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        void PickFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.SelectedPath = folder;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Backup.SaveFolder(iniPath, d.SelectedPath); folder = d.SelectedPath; }
                catch (Exception ex) { Error("Ordner konnte nicht gespeichert werden.", ex); }
                Fill();
            }
        }

        // Gilt wie der Ordner fuer alle PCs mit derselben exe (sicherung.ini neben der exe)
        void SaveHours()
        {
            try { Backup.SaveSetting(iniPath, "stunden", ((int)nHours.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            catch (Exception ex) { Error("Einstellung konnte nicht gespeichert werden.", ex); }
            Fill();
        }

        void BackupNow()
        {
            try { string p = Backup.WriteTo(store, folder, null); MessageBox.Show("Gesichert:\n" + p, "Datensicherung", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { Error("Sicherung fehlgeschlagen.", ex); }
            Fill();
        }

        void RestoreSelected(bool other)
        {
            string path = null;
            if (!other) { if (files.SelectedItem == null) return; path = Path.Combine(folder, (string)files.SelectedItem); }
            else
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Filter = "Arbeitsplan-Sicherung (*" + Backup.Ext + ")|*" + Backup.Ext; d.InitialDirectory = folder;
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    path = d.FileName;
                }
            StoreData data;
            try { data = Backup.Read(path, Backup.KeyOf(store)); }
            catch (Exception ex) { Error("Sicherung kann nicht geladen werden.", ex); return; }
            if (MessageBox.Show("Sicherung \"" + Path.GetFileName(path) + "\" wiederherstellen?\n\nAlle Pläne, die Blockstruktur und der Katalog werden durch den Stand der Sicherung ersetzt."
                + "\nDer aktuelle Stand wird vorher automatisch gesichert; das Änderungsprotokoll bleibt vollständig erhalten.",
                "Datensicherung", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { Backup.WriteTo(store, folder, "vor-Wiederherstellung"); }
            catch (Exception ex) { Error("Der aktuelle Stand konnte nicht gesichert werden; es wurde nichts wiederhergestellt.", ex); return; }
            try { store.Restore(data); }
            catch (Exception ex) { Error("Wiederherstellen fehlgeschlagen.", ex); return; }
            MessageBox.Show("Sicherung wiederhergestellt.", "Datensicherung", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Fill();
        }
    }
}
