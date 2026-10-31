using System;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Umschalter "Woche > Ansicht": Standard oder Plan-Stil (dicht). Pro Nutzer in %APPDATA%\<AppId.Dir>\ansicht ("plan"); Fehlen/Unsinn = Standard.
    partial class MainForm
    {
        public static bool LookFile = true;   // Testbilder/Selbsttest: Datei weder lesen noch schreiben
        ToolStripMenuItem miLookStd, miLookPlan;

        static string LookPath { get { return System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Arbeitsplan.Core.AppId.Dir), "ansicht"); } }

        // Gespeicherten Look waehlen (vor dem Aufbau der Ansicht)
        static bool lookLoaded;
        static void LoadLook()
        {
            if (lookLoaded) return;   // weitere Fenster (Plan-Datei) behalten den aktuellen Look
            lookLoaded = true;
            string n = "";
            if (LookFile) try { if (System.IO.File.Exists(LookPath)) n = System.IO.File.ReadAllText(LookPath).Trim(); } catch (Exception) { }
            Theme.Select(n == "plan" ? "plan" : null);
        }

        void AddLookMenu(ToolStripMenuItem parent)
        {
            ToolStripMenuItem sub = new ToolStripMenuItem("Ansicht");
            miLookStd = new ToolStripMenuItem("Standard", null, delegate { SetLook(null); });
            miLookPlan = new ToolStripMenuItem("Plan-Stil", null, delegate { SetLook("plan"); });
            sub.DropDownItems.Add(miLookStd); sub.DropDownItems.Add(miLookPlan);
            parent.DropDownItems.Add(sub);
            parent.DropDownItems.Add(new ToolStripSeparator());
            ShowLookChecks();
        }

        void ShowLookChecks() { bool p = Theme.Current.Dense; miLookStd.Checked = !p; miLookPlan.Checked = p; }

        // Wie beim Wochenwechsel: Bearbeitung beenden (Speichern fehlgeschlagen = nicht umschalten), dann neu aufbauen.
        void SetLook(string name)
        {
            if (Theme.Current.Dense == (name == "plan")) return;
            if (!EndEdit(true)) return;
            Theme.Select(name);
            ShowLookChecks();
            if (LookFile)
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LookPath)); System.IO.File.WriteAllText(LookPath, name == "plan" ? "plan" : "standard"); } catch (Exception) { }
            Rebuild(true);
        }
    }
}
