using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Plan teilen: Woche(n) als Plan-Datei (.applan) speichern und eine Plan-Datei schreibgeschuetzt ansehen
    // (eigenes Hauptfenster mit Testdaten-Speicher aus der Datei, ohne Datenordner, Nutzerdaten oder Speichern).
    partial class MainForm
    {
        const string PlanFilter = "Arbeitsplan-Plan (*" + PlanFile.Ext + ")|*" + PlanFile.Ext;

        // Speicher fuer die Ansicht: Daten der Datei, jedes Speichern scheitert (nur ansehen)
        static MemoryStore ViewStore(PlanSnapshot p)
        {
            MemoryStore ms = new MemoryStore();
            ms.Restore(p.Data);
            ms.FailSaves = true;
            return ms;
        }

        void SavePlanFile()
        {
            if (!EndEdit(true)) return;
            int weeks = AskWeeks();
            if (weeks <= 0) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = PlanFilter; d.DefaultExt = PlanFile.Ext;
                d.FileName = "Plan_" + start.ToString("yyyy-MM-dd") + (weeks > 1 ? "_" + weeks + "Wochen" : "");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { PlanFile.Write(PlanFile.Take(store, start, 7 * weeks), d.FileName); Status("Plan-Datei gespeichert", false); }
                catch (Exception ex) { ExportFailed("Speichern der Plan-Datei", ex); }
            }
        }

        // Anzahl Wochen ab der angezeigten Woche; 0 = abgebrochen
        int AskWeeks()
        {
            using (Form f = new Form())
            {
                f.Text = "Als Plan-Datei speichern";
                Ui.Dialog(f, 380, 120, false);
                Label l = new Label(); l.AutoSize = true; l.Location = new Point(10, 16);
                l.Text = "Wochen ab " + weekLabel.Text.Split(' ')[0] + " (" + Ui.DayText(start) + "):";
                NumericUpDown n = new NumericUpDown(); n.Minimum = 1; n.Maximum = PlanFile.MaxDays / 7; n.Value = 1;
                n.SetBounds(l.Left + l.PreferredWidth + 8, 12, 60, 22); n.TextAlign = HorizontalAlignment.Right;
                Label h = new Label(); h.AutoSize = true; h.Location = new Point(10, 46); h.ForeColor = Color.DimGray;
                h.Text = "Nur Pläne (Blöcke, Format, Kürzel), keine Nutzer, Rechte oder Protokolle.";
                Button ok = new Button(); ok.Text = "Weiter…"; ok.SetBounds(186, 80, 90, 28); ok.DialogResult = DialogResult.OK;
                Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(282, 80, 90, 28); cancel.DialogResult = DialogResult.Cancel;
                f.Controls.AddRange(new Control[] { l, n, h, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                Theme.Current.Style(f);
                return f.ShowDialog(this) == DialogResult.OK ? (int)n.Value : 0;
            }
        }

        void OpenPlanFile()
        {
            if (!EndEdit(true)) return;
            string path;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = PlanFilter;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                path = d.FileName;
            }
            PlanSnapshot p;
            try { p = PlanFile.Read(path); }
            catch (Exception ex) { MessageBox.Show(this, "Die Plan-Datei kann nicht geöffnet werden.\n" + ex.Message, "Plan-Datei", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            using (MainForm v = new MainForm(me.Login, true, null, null, p))
            {
                v.StartPosition = FormStartPosition.Manual; v.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (WindowState == FormWindowState.Maximized) v.WindowState = FormWindowState.Maximized;
                v.ShowDialog(this);   // modal: das Hauptfenster bleibt unveraendert, die Abfrage laeuft danach weiter
            }
        }
    }
}
