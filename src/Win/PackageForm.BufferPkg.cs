using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Pufferpaket (Unterpaket). Ein Puffer-Schritt wird als kleines Paket (.apkg, nur Puffer-Schritte) gespeichert und
    // in jeder Kampagne als Kopie wieder eingesetzt (nicht verknuepft: spaetere Aenderungen am Pufferpaket aendern die Kampagne nicht).
    partial class PackageForm
    {
        // Eintraege im Menue "Mehr ▾" unter dem Ablauf
        void BuildBufferPkg(ContextMenuStrip m)
        {
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Puffer-Schritt als Pufferpaket speichern…", null, delegate { SaveBufferPackage(); }).ToolTipText = "Wiederverwendbare Puffer als kleines Paket speichern";
            m.Items.Add("Pufferpaket einfügen…", null, delegate { InsertBufferPackage(); }).ToolTipText = "Pufferpaket als Kopie in diese Kampagne einsetzen";
        }

        void SaveBufferPackage()
        {
            if (cur == null || !cur.IsBuffer)
            {
                MessageBox.Show(this, "Zuerst einen Puffer-Schritt im Ablauf wählen.", "Pufferpaket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Package p = new Package(); p.Name = "Pufferpaket " + TitleOf(cur);
            p.Steps.Add(Package.ForBufferPackage(cur));
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = p.Name;
                PackagePicker.SetSaveFolder(d, Folder);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { p.Save(d.FileName); }
                catch (Exception ex) { MessageBox.Show(this, "Pufferpaket konnte nicht gespeichert werden.\n" + ex.Message, "Pufferpaket", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void InsertBufferPackage()
        {
            string fn = PackagePicker.Pick(this, Folder, canAdmin);
            if (fn == null) return;
            try { InsertBufferSteps(Package.Load(fn), true); }
            catch (Exception ex) { MessageBox.Show(this, "Pufferpaket konnte nicht gelesen werden.\n" + ex.Message, "Pufferpaket", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Kopiert die Puffer-Schritte von src in den Ablauf (Rest wird nicht uebernommen); liefert die Anzahl
        int InsertBufferSteps(Package src, bool tell)
        {
            int n = 0, skipped = 0;
            foreach (PackageStep s in src.Steps)
            {
                if (s.IsEmpty) continue;
                if (!s.IsBuffer) { skipped++; continue; }
                PackageStep c = Package.ForBufferPackage(s);
                c.Day = steps.Count == 0 ? 1 : NextDay();
                DefaultBind(c, null);
                AddStep(c); n++;
            }
            if (tell)
                MessageBox.Show(this, n == 0 ? "Das Paket enthält keinen Puffer-Schritt." : n + (n == 1 ? " Puffer-Schritt" : " Puffer-Schritte") + " eingefügt (als Kopie)."
                    + (skipped > 0 ? "\n" + skipped + " andere Schritte wurden nicht übernommen." : ""), "Pufferpaket", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return n;
        }
    }
}
