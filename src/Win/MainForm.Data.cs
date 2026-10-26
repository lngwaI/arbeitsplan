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
        // Standard: Unterordner "sicherung" neben den Daten (Prototyp: neben der exe)
        static string BackupFolder { get { return Backup.LoadFolder(BackupIni, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sicherung")); } }

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
            string title = (ri.Name.Length > 0 ? ri.Name : "Block " + (c.RowIndex + 1)) + ", " + c.Day.ToString("ddd dd.MM.yyyy", De);
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
            c.Lkz = old.Lkz;
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

#if TESTHOOKS
        Form TestDataForm(string which)
        {
            if (which == "backup")
            {
                string dir = Path.Combine(Path.GetTempPath(), "apbak_form");
                if (Backup.Files(dir).Length == 0) { Backup.WriteTo(store, dir, null); Backup.WriteTo(store, dir, "vor-Wiederherstellung"); }
                return new BackupForm(store, Path.Combine(dir, "sicherung.ini"), dir);
            }
            RowInfo ri = rowInfo[0];
            string[] logins = { "pwerk", "ebeispiel", "mmuster" };
            string[] texts = { "Lauf 100001\nTFF: H1-X-0101", "Lauf 100001\nTFF: H1-X-0102 (Ausweich: H1-X-0103)", "Lauf 100001 – Öl-Wechsel prüfen\nTFF: H1-X-0102" };
            for (int i = 0; i < 3; i++)
            {
                Entry e = store.Get(start, ri.Id) ?? new Entry();
                e.Content = texts[i]; e.People[1] = i == 2 ? "MUMA" : "WEPA"; e.Lkz = i > 0;
                Audit.Save(store, start, ri, users.FindByLogin(logins[i]), e);
            }
            return new HistoryForm(ri.Name + ", " + start.ToString("ddd dd.MM.yyyy", De), store.GetAudit(start, ri.Id), ri.Labels, true);
        }

        // Protokoll schreiben/lesen, Version wiederherstellen, Sicherung -> Wiederherstellen, manipulierte Sicherung.
        void SelfTestData(System.Text.StringBuilder sb)
        {
            Rebuild(true);
            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 0 && x.Col == 6) c = x;
            RowInfo ri = rowInfo[0]; DateTime day = c.Day;
            int before = store.GetAudit(day, ri.Id).Count;
            BeginEdit(c); edContent.Text = "Audit A"; EndEdit(true);
            BeginEdit(c); edContent.Text = "Audit B"; edPpl[2].Text = "ABCD"; EndEdit(true);
            List<AuditEntry> au = store.GetAudit(day, ri.Id);
            AuditEntry last = au[au.Count - 1];
            bool okA = au.Count == before + 2 && last.Before.Content == "Audit A" && last.After.Content == "Audit B" && last.After.People[2] == "ABCD"
                && last.Login == me.Login && last.Kuerzel == me.Kuerzel && last.Version == store.Get(day, ri.Id).Version && last.RowName == ri.Name;
            store.GetAudit(day, ri.Id)[0].After.Content = "X";   // Kopie: Aenderung darf das Protokoll nicht beruehren
            bool okRo = store.GetAudit(day, ri.Id)[0].After.Content != "X";
            RestoreVersion(c, au[au.Count - 2].After);
            string p2 = au[au.Count - 2].After.People[2];
            bool inEdit = editCell == c && edContent.Text == "Audit A" && edPpl[2].Text == p2;
            EndEdit(true);
            Entry now = store.Get(day, ri.Id);
            bool okR = inEdit && now.Content == "Audit A" && now.People[2] == p2 && p2 != "ABCD" &&store.GetAudit(day, ri.Id).Count == before + 3;
            sb.AppendLine("Protokoll: schreiben/lesen=" + Ok(okA) + " unveränderlich=" + Ok(okRo) + " Version wiederherstellen=" + Ok(okR) + " (Einträge " + store.GetAudit(day, ri.Id).Count + ")");

            // Sicherung: alte Woche (1 Jahr zurueck) bleibt erhalten, Roundtrip, Protokoll wird zusammengefuehrt
            string dir = Path.Combine(Path.GetTempPath(), "apbak_selftest");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            DateTime old = day.AddDays(-364);
            Entry oe = new Entry(); oe.Content = "Altlauf vor einem Jahr"; Audit.Save(store, old, ri, me, oe);
            Entry fmtE = store.Get(start.AddDays(4), ri.Id);
            StoreData s0 = store.Snapshot();
            string path = Backup.WriteTo(store, dir, null);
            Entry ch = store.Get(day, ri.Id); ch.Content = "Nach Sicherung"; Audit.Save(store, day, ri, me, ch);
            List<RowInfo> rows = store.GetRows(); rows.Add(new RowInfo()); store.SaveStructure(rows);
            int auditBefore = store.Snapshot().Audit.Count;
            store.Restore(Backup.Read(path));
            Rebuild(true);
            StoreData s1 = store.Snapshot();
            Entry fmtR = store.Get(start.AddDays(4), ri.Id);
            bool okB = store.Get(day, ri.Id).Content == "Audit A" && s1.Entries.Count == s0.Entries.Count && s1.Rows.Count == s0.Rows.Count
                && s1.Catalog.Positions.Count == s0.Catalog.Positions.Count && s1.Catalog.Bausteine.Count == s0.Catalog.Bausteine.Count
                && fmtE != null && fmtR.SpanKey() == fmtE.SpanKey() && fmtR.Content == fmtE.Content && fmtR.Version == fmtE.Version;
            bool okLog = s1.Audit.Count == auditBefore;
            List<SearchHit> hits = store.Find("Altlauf");
            bool okOld = hits.Count == 1 && hits[0].Day == old && store.GetAudit(old, ri.Id).Count == 1;
            sb.AppendLine("Sicherung: Roundtrip=" + Ok(okB) +" Protokoll erhalten=" + Ok(okLog) + " Jahr zurück=" + Ok(okOld) + " (" + new FileInfo(path).Length + " Byte)");

            // Manipulation: (a) Byte in der Datei, (b) Text im XML bei unveraendertem Hash
            byte[] raw = File.ReadAllBytes(path);
            string bad1 = Path.Combine(dir, "kaputt1.bin"), bad2 = Path.Combine(dir, "kaputt2.bin");
            raw[raw.Length / 2] ^= 0x55; File.WriteAllBytes(bad1, raw);
            string text;
            using (FileStream fs = File.OpenRead(path))
            using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            using (StreamReader rd = new StreamReader(gz, new System.Text.UTF8Encoding(false))) text = rd.ReadToEnd();
            byte[] mod = new System.Text.UTF8Encoding(false).GetBytes(text.Replace("Audit A", "Audit X"));
            using (FileStream fs = File.Create(bad2))
            using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Compress)) gz.Write(mod, 0, mod.Length);
            bool rej1 = false, rej2 = false;
            try { Backup.Read(bad1); } catch (InvalidDataException) { rej1 = true; }
            try { Backup.Read(bad2); } catch (InvalidDataException) { rej2 = true; }
            for (int i = 0; i < Backup.Keep + 2; i++) Backup.WriteTo(store, dir, "t" + i.ToString("00"));
            sb.AppendLine("Sicherung manipuliert abgelehnt: Byte=" + Ok(rej1) + " Inhalt=" + Ok(rej2) + " | behalten=" + Backup.Files(dir).Length + " (soll " + Backup.Keep + ")");
        }
#endif
    }
}
