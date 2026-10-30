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
        string BackupFolder
        {
            get
            {
                string def = cfg != null ? cfg.BackupFolder : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sicherung");
                string f = Backup.LoadFolder(BackupIni, def);
#if DEMO
                if (!DemoEnv.IsDemoPath(f)) return def;   // Demo: Sicherungen nur im Demo-Ordner
#endif
                return f;
            }
        }

        void InitData()
        {
            ContextMenuStrip vm = new ContextMenuStrip();
            vm.Items.Add("Verlauf…", null, delegate { if (view.MenuRound != null) ShowRoundHistory(view.MenuRound); else if (view.Selected != null) ShowHistory(view.Selected); });
            vm.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e) { e.Cancel = view.Selected == null && view.MenuRound == null; };
            InitStatusMenu(vm);   // MainForm.Status.cs
            view.ContextMenuStrip = vm;
            ContextMenuStrip cm = edContent.ContextMenuStrip;
            cm.Items.Add(new ToolStripSeparator());
            ToolStripItem h = cm.Items.Add("Verlauf…", null, delegate { if (editCell != null) ShowHistory(editCell); });
            cm.Opening += delegate { h.Enabled = true; };   // nach der Format-Sperre: Lesen genuegt
            InitFreeze(vm);   // Freeze hinzufuegen (MainForm.Freeze.cs)
            InitNotes(vm, cm);   // Notizzeilen markieren/aufheben (MainForm.Notes.cs)
            FormClosed += delegate { AutoBackup(); };
            StartBackupTimer();
        }

        void ShowHistory(CellData c)
        {
            RowInfo ri = rowInfo[c.RowIndex];
            string title = BlockTitle(c) + ", " + Ui.DayText(c.Day);
            Entry pick;
            using (HistoryForm f = new HistoryForm(title, store.GetAudit(c.Day, ri.Id), ri.Labels, me.Has(Perm.EditText), users))
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
            using (BackupForm f = new BackupForm(store, BackupIni, BackupFolder, DefaultBackupHours)) f.ShowDialog(this);
            Rebuild(true);
        }

        // Abstand der automatischen Sicherung: sicherung.ini (Dialog) vor Arbeitsplan.ini (SicherungAlleStunden=), sonst 24
        int DefaultBackupHours { get { return cfg != null ? cfg.BackupHours : 24; } }
        int BackupHours { get { return Backup.LoadHours(BackupIni, DefaultBackupHours); } }

        // Beim Beenden, wenn die letzte Sicherung aelter als eingestellt ist. Fehler blockieren das Beenden nie.
        void AutoBackup()
        {
            if (systemPc || backupBusy) return;
            try { string f = BackupFolder; if (Backup.AutoDue(f, BackupHours)) Backup.WriteTo(store, f, null); }
            catch (Exception) { }
        }

        // Im laufenden Programm alle 10 min pruefen: faellig -> Stand holen (UI-Thread, FileStore ist nicht thread-sicher), Datei im
        // Hintergrund schreiben. Nicht im Bearbeitungsmodus, bei offenem Dialog, OFFLINE oder ohne Speichern (wie beim Beenden).
        bool backupBusy;
        void StartBackupTimer()
        {
            if (systemPc) return;
            Timer t = new Timer(); t.Interval = 10 * 60 * 1000;
            t.Tick += delegate { TimedBackup(); };
            t.Start();
            FormClosed += delegate { t.Stop(); t.Dispose(); };
        }

        void TimedBackup()
        {
            if (systemPc || backupBusy || editCell != null || Application.OpenForms.Count > 1 || (fstore != null && (fstore.ReadOnly || fstore.Offline))) return;
            string folder; StoreData snap; string key;
            try
            {
                folder = BackupFolder;
                if (!Backup.AutoDue(folder, BackupHours)) return;
                key = Backup.KeyOf(store); snap = store.Snapshot();
            }
            catch (Exception) { return; }   // naechster Versuch beim naechsten Takt
            backupBusy = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                try { Backup.WriteTo(snap, folder, null, key); ok = true; } catch (Exception) { }
                try { BeginInvoke((MethodInvoker)delegate { backupBusy = false; if (ok) Status("Sicherung " + DateTime.Now.ToString("HH:mm"), false); }); }
                catch (Exception) { }   // Fenster schon geschlossen (Ausnahme im Hintergrund-Thread wuerde das Programm beenden)
            });
        }
    }
}
