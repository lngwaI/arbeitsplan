using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Auswahl eines Pakets aus dem Paketordner (Liste *.apkg: Name, Produkt, Datum). Doppelklick = waehlen.
    // "Andere Datei..." (klassischer Dateidialog) nur mit allowOther (Recht PackageAdmin).
    // Kein Ordner (Testdaten ohne Datenordner) oder Ordner nicht anlegbar/lesbar: klassischer Dateidialog als Rueckfall.
    class PackagePicker : Form
    {
        public string Result;
        readonly string folder;
        readonly ListView list = new ListView();

        public static string FileFilter { get { return "Arbeitsplan-Paket (*.apkg)|*.apkg|Alle Dateien|*.*"; } }

        // Ordner bei Bedarf anlegen; false = nicht moeglich
        public static bool EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            try { Directory.CreateDirectory(folder); return Directory.Exists(folder); }
            catch (Exception) { return false; }
        }

        public static string Pick(IWin32Window owner, string folder, bool allowOther) { return Pick(owner, folder, allowOther, "Paket auswählen", "Öffnen"); }

        // title/okText: z. B. "Kampagne starten – Produkt wählen" / "Weiter"
        public static string Pick(IWin32Window owner, string folder, bool allowOther, string title, string okText) { return Pick(owner, folder, allowOther, title, okText, null); }

        // product != null: nur Pakete dieses Produktcodes (Filter)
        public static string Pick(IWin32Window owner, string folder, bool allowOther, string title, string okText, string product)
        {
            if (!EnsureFolder(folder)) return Browse(owner, folder);
            using (PackagePicker p = new PackagePicker(folder, allowOther, product))
            {
                p.Text = title; p.okButton.Text = okText;
                if (p.ShowDialog(owner) != DialogResult.OK) return null;
                return p.Result;
            }
        }

        static string Browse(IWin32Window owner, string folder)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = FileFilter;
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) d.InitialDirectory = folder;
                return d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
            }
        }

        // Standardordner fuer "Speichern unter": nur setzen, wenn er existiert bzw. angelegt werden kann
        public static void SetSaveFolder(SaveFileDialog d, string folder) { if (EnsureFolder(folder)) d.InitialDirectory = folder; }

        readonly Button okButton = new Button();

        readonly string productFilter;   // null = alle Pakete

        PackagePicker(string folder, bool allowOther, string productFilter)
        {
            this.folder = folder; this.productFilter = productFilter;
            Text = "Paket auswählen";
            Ui.Dialog(this, 560, 380);
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.Columns.Add("Name", 270); list.Columns.Add("Produkt", 70); list.Columns.Add("Geändert", 130);
            list.SetBounds(10, 10, 540, 290);
            list.DoubleClick += delegate { Choose(); };
            Button ok = okButton; ok.Text = "Öffnen"; ok.SetBounds(10, 310, 100, 30); ok.Click += delegate { Choose(); };
            Button other = new Button(); other.Text = "Andere Datei…"; other.SetBounds(118, 310, 120, 30); other.Visible = allowOther;
            other.Click += delegate
            {
                string f = Browse(this, folder);
                if (f != null) { Result = f; DialogResult = DialogResult.OK; }
            };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(450, 310, 100, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { list, ok, other, cancel });
            Fill();
        }

        void Fill()
        {
            foreach (KeyValuePair<string, string> kv in Files(folder))
            {
                string f = kv.Key;
                if (productFilter != null && kv.Value != productFilter) continue;
                ListViewItem it = new ListViewItem(Path.GetFileNameWithoutExtension(f));
                it.SubItems.Add(kv.Value);
                it.SubItems.Add(File.GetLastWriteTime(f).ToString("dd.MM.yyyy HH:mm"));
                it.Tag = f;
                list.Items.Add(it);
            }
            if (list.Items.Count > 0) list.Items[0].Selected = true;
            else list.Items.Add(new ListViewItem("(noch keine Pakete – zuerst „Neues Paket anlegen…“ und speichern)"));   // Tag null: nicht waehlbar
        }

        // Produktcode (frei, Package.IsProductCode) des Pakets; leer, wenn nicht lesbar oder ohne Produkt
        public static string Product(string file)
        {
            try { return Package.Load(file).ProductCode; }
            catch (Exception) { return ""; }
        }

        // Alle .apkg im Ordner mit Produktcode (nicht lesbare mit ""), nach Dateiname sortiert
        public static List<KeyValuePair<string, string>> Files(string folder)
        {
            List<KeyValuePair<string, string>> res = new List<KeyValuePair<string, string>>();
            string[] files;
            try { files = Directory.GetFiles(folder ?? "", "*.apkg"); }
            catch (Exception) { return res; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files) res.Add(new KeyValuePair<string, string>(f, Product(f)));
            return res;
        }

        void Choose()
        {
            if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag == null) return;
            Result = (string)list.SelectedItems[0].Tag; DialogResult = DialogResult.OK;
        }
    }
}
