using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Aenderungsprotokoll (Verlauf je Block) und Datensicherung; getrennt von MainForm.cs gehalten.
    partial class MainForm
    {
        static string BackupIni { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sicherung.ini"); } }
        // Standard: Sicherungsordner aus Arbeitsplan.ini bzw. <Datenordner>\sicherung (Testdaten: neben der exe); Auswahl im Dialog (sicherung.ini) hat Vorrang
        string BackupFolder { get { return Backup.LoadFolder(BackupIni, cfg != null ? cfg.BackupFolder : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sicherung")); } }

        void InitData()
        {
            ContextMenuStrip vm = new ContextMenuStrip();
            vm.Items.Add("Verlauf…", null, delegate { if (view.Selected != null) ShowHistory(view.Selected); });
            vm.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e) { e.Cancel = view.Selected == null; };
            view.ContextMenuStrip = vm;
            ContextMenuStrip cm = edContent.ContextMenuStrip;
            cm.Items.Add(new ToolStripSeparator());
            ToolStripItem h = cm.Items.Add("Verlauf…", null, delegate { if (editCell != null) ShowHistory(editCell); });
            cm.Opening += delegate { h.Enabled = true; };   // nach der Format-Sperre: Lesen genuegt
            FormClosed += delegate { AutoBackup(); };
        }

        void ShowHistory(CellData c)
        {
            RowInfo ri = rowInfo[c.RowIndex];
            string title = BlockTitle(c) + ", " + Ui.DayText(c.Day);
            Entry pick;
            using (HistoryForm f = new HistoryForm(title, store.GetAudit(c.Day, ri.Id), ri.Labels, me.Has(Perm.EditText)))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                pick = f.Result;
            }
            RestoreVersion(c, pick);
        }

        // Alte Fassung in den Editor laden; gespeichert wird erst mit Strg+S/Blockwechsel (Versionspruefung wie sonst).
        void RestoreVersion(CellData c, Entry old)
        {
            if (editCell != c)
            {
                fold[rowInfo[c.RowIndex].Id] = false; ApplyFold(); view.Relayout();
                BeginEdit(c);
                if (editCell != c) return;
            }
            Entry keep = c.Cur, r = old.Clone();
            c.Cur = r; LoadEditors(c); c.Cur = keep;
            c.Show(r);
            fmtChanged = r.SpanKey() != keep.SpanKey();
            UpdateChanged();
            view.Relayout();
            edContent.Focus();
        }

        void OpenBackup()
        {
            if (!EndEdit(true)) return;
            using (BackupForm f = new BackupForm(store, BackupIni, BackupFolder)) f.ShowDialog(this);
            Rebuild(true);
        }

        // Beim Beenden, wenn die letzte Sicherung aelter als 24 h ist. Fehler blockieren das Beenden nie.
        void AutoBackup()
        {
            if (systemPc) return;
            try { string f = BackupFolder; if (Backup.AutoDue(f)) Backup.WriteTo(store, f, null); }
            catch (Exception) { }
        }
    }
}
