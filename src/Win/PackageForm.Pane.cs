using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Abschnitt 3 (Vorschau & Pruefung) ist in der Hoehe verstellbar: Teiler zwischen Abschnitt 2 und 3 (nur der Balken wird beim Ziehen
    // gezeigt, die Hoehe gilt erst beim Loslassen: kein Flackern). Die Hoehe wird je Nutzer in %APPDATA%\Arbeitsplan\paketeditor.ini gemerkt
    // (Hoehe3=px) und bei kleinem Fenster nur vorlaeufig begrenzt; Mindesthoehe = der alte feste Wert.
    partial class PackageForm
    {
        readonly Panel paneWrap = new Panel();
        readonly Splitter paneSplit = new PaneSplitter();
        int paneWant;                       // gewuenschte Hoehe (gemerkt); die angezeigte kann bei kleinem Fenster kleiner sein
        const int MiddleMin = 360;          // Platz, der Abschnitt 1 und 2 standardmaessig bleibt (kleines Fenster: Vorschau schrumpft bis PaneMin)
        const int MiddleHard = 240;         // Untergrenze, wenn der Nutzer den Teiler selbst zieht
        bool paneDragged;

        // Datei der gemerkten Einstellungen (Tests setzen einen anderen Pfad)
        internal static string PrefsFile = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Arbeitsplan.Core.AppId.Dir), "paketeditor.ini");

        // Mindesthoehe: Abschnittskopf + Leiste + Tageskopf + mindestens 3 Zeilen im Kalender (+ Bildlaufleiste)
        int PaneMin { get { int fh = Font.Height; return (fh + 10) + 6 + 8 + 2 + (fh + 12) + (fh + 6) + 3 * (2 * fh + 13) + SystemInformation.HorizontalScrollBarHeight; } }
        int PaneMax
        {
            get
            {
                int fixedH = (headPanel != null ? headPanel.Height : 0) + Theme.Current.ButtonH + 14 + paneSplit.Height;
                return Math.Max(PaneMin, ClientSize.Height - fixedH - (paneDragged ? MiddleHard : MiddleMin));
            }
        }

        void InitPane()
        {
            int saved = PrefInt("Hoehe3");
            paneWant = saved > 0 ? saved : PaneMin + 12;
            paneSplit.Dock = DockStyle.Bottom; paneSplit.Height = 7; paneSplit.Tag = "keep"; paneSplit.MinSize = PaneMin; paneSplit.MinExtra = MiddleHard;
            paneSplit.SplitterMoved += delegate { PaneMoved(); };
            tip.SetToolTip(paneSplit, "Ziehen = Höhe von „3 Vorschau & Prüfung“ ändern (wird gemerkt)");
            ClampPane();
        }

        void PaneMoved() { paneWant = paneWrap.Height; paneDragged = true; ClampPane(); SetPref("Hoehe3", paneWrap.Height.ToString()); }

        // Angezeigte Hoehe = gewuenschte, begrenzt auf Mindesthoehe und den Platz im Fenster
        void ClampPane()
        {
            int h = Math.Max(PaneMin, Math.Min(paneWant, PaneMax));
            if (paneWrap.Height != h) paneWrap.Height = h;
        }

        // ---------- gemerkte Einstellungen (Schluessel=Wert) ----------

        static string Pref(string key)
        {
            try
            {
                if (File.Exists(PrefsFile))
                    foreach (string l in File.ReadAllLines(PrefsFile))
                    {
                        int i = l.IndexOf('=');
                        if (i > 0 && l.Substring(0, i).Trim() == key) return l.Substring(i + 1).Trim();
                    }
            }
            catch (Exception) { }
            return "";
        }

        static int PrefInt(string key) { int v; return int.TryParse(Pref(key), out v) && v > 0 && v < 5000 ? v : 0; }

        static void SetPref(string key, string value)
        {
            try
            {
                List<string> keep = new List<string>();
                if (File.Exists(PrefsFile))
                    foreach (string l in File.ReadAllLines(PrefsFile)) { int i = l.IndexOf('='); if (i > 0 && l.Substring(0, i).Trim() != key) keep.Add(l); }
                keep.Add(key + "=" + value);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefsFile));
                File.WriteAllLines(PrefsFile, keep.ToArray());
            }
            catch (Exception) { }   // nur eine Komfort-Einstellung: Fehler (z. B. schreibgeschuetztes Profil) ignorieren
        }
    }

    // Teiler mit drei Punkten als Griff
    class PaneSplitter : Splitter
    {
        public PaneSplitter() { BackColor = Theme.Current.Window; Cursor = Cursors.HSplit; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int cx = Width / 2, cy = Height / 2;
            using (SolidBrush b = new SolidBrush(Theme.Current.Arrow))
                for (int i = -1; i <= 1; i++) e.Graphics.FillRectangle(b, cx + i * 8 - 1, cy - 1, 3, 3);
        }
    }
}
