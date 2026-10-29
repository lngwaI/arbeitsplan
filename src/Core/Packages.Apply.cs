using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    public partial class Package
    {
        // Block fuer SaveManyWithAudit; mit Protokolleintrag, wenn der Nutzer bekannt ist (Paket-Uebernahme erscheint im Verlauf)
        static SaveItem Item(List<RowInfo> rows, Target t, Entry result, User by)
        {
            SaveItem it = new SaveItem(); it.Day = t.Day; it.RowId = t.RowId; it.Data = result;
            if (by != null) foreach (RowInfo r in rows) if (r.Id == t.RowId) { it.Audit = Audit.New(t.Day, r, by); break; }
            return it;
        }

        // Ergebnis aus dem aktuellen Stand; null = ueberspringen (Modus FillEmpty und Block belegt)
        static SaveItem Prepare(IDataStore store, List<RowInfo> rows, Target t, ApplyMode mode, User by)
        {
            Entry cur = store.Get(t.Day, t.RowId);
            Entry result = Merge(cur, t.Data, mode);
            if (result == null) return null;
            result.Version = cur != null ? cur.Version : 0;
            return Item(rows, t, result, by);
        }

        // Text und Formate von add hinter dst haengen (Leerzeile dazwischen), Formate verschoben
        static void AppendText(Entry dst, Entry add)
        {
            if (add.Content.Length == 0) return;
            int shift = dst.Content.Length > 0 ? dst.Content.Length + 2 : 0;
            dst.Content = dst.Content.Length > 0 ? dst.Content + "\n\n" + add.Content : add.Content;
            foreach (FormatSpan f in add.Spans) { FormatSpan c = f.Clone(); c.Start += shift; dst.Spans.Add(c); }
        }

        // Notizzeilen aus old ans Ende von dst (nicht, wenn dst dieselbe Zeile schon hat)
        static void KeepNotes(Entry dst, Entry old)
        {
            List<string> have = Notes.NoteTexts(dst), keep = new List<string>();
            foreach (string t in Notes.NoteTexts(old)) if (!have.Contains(t)) keep.Add(t);
            if (keep.Count == 0) return;
            int first = Notes.Lines(dst.Content).Count;
            dst.Content = dst.Content.Length > 0 ? dst.Content + "\n" + string.Join("\n", keep.ToArray()) : string.Join("\n", keep.ToArray());
            Notes.SetNote(dst, first, first + keep.Count - 1, true);
        }

        // Neuer Blockinhalt aus vorhandenem (cur) und Paket-Block (n); null = ueberspringen. Kuerzel und LKZ-Haken des vorhandenen Blocks bleiben erhalten.
        // Ein Block mit nur LKZ-Haken zaehlt als leer (IsEmpty), verliert den Haken aber nicht.
        internal static Entry Merge(Entry cur, Entry n, ApplyMode mode)
        {
            Entry result;
            if (cur == null) return n.Clone();
            bool free = cur.IsEmpty && cur.Buffers.Count == 0;   // ein Block mit Puffern gilt als belegt; nur Notizen zaehlen als leer
            if (free || mode == ApplyMode.Overwrite)
            {
                result = n.Clone();
                for (int i = 0; i < 3; i++) if (result.People[i].Length == 0) result.People[i] = cur.People[i];
                if (n.Buffers.Count == 0) result.Buffers = BufferSet.CloneAll(cur.Buffers);   // ohne Puffer im Paket bleiben die vorhandenen
                KeepNotes(result, cur);   // vorhandene Notizen bleiben (ans Ende)
            }
            else if (mode == ApplyMode.FillEmpty) return null;
            else
            {   // anhaengen: Text nach dem vorhandenen, Formatierung der neuen Textteile verschieben
                result = cur.Clone();
                AppendText(result, n);
                for (int i = 0; i < 3; i++) if (n.People[i].Length > 0) result.People[i] = result.People[i].Length > 0 ? result.People[i] + ", " + n.People[i] : n.People[i];
                BufferSet.AddRange(result.Buffers, n.Buffers);   // Puffer hinten anhaengen (neue Ids)
            }
            result.Lkz = cur.Lkz || n.Lkz;
            return result;
        }

        // Ergebnis der Uebernahme (Meldung fuer den Nutzer). schedule: Tage je Lauf (aus Schedule(), ggf. verschoben); null = Paket-Rhythmus.
        public string Apply(IDataStore store, DateTime start, ApplyMode mode, List<RunDate> schedule, User by = null)
        {
            if (schedule != null) return ApplyCore(store, start, mode, schedule, by);
            Package c = Clone();   // Back-Up-Tage nur auf der Kopie angleichen, das Eingabepaket bleibt unveraendert
            SyncBackups(c.Steps);   // Back-Up immer einen Tag nach dem Packen
            return c.ApplyCore(store, start, mode, null, by);
        }

        string ApplyCore(IDataStore store, DateTime start, ApplyMode mode, List<RunDate> schedule, User by)
        {
            List<string> belongs = store.GetCatalog().CheckPackage(this);   // harter Fehler: Zubehoer eines anderen Hauptgeraets
            if (belongs.Count > 0) throw new InvalidOperationException("Equipment passt nicht zusammen:\n" + string.Join("\n", belongs.ToArray()));
            string bp = BackupProblem(Steps);   // Back-Up ohne Packen
            if (bp != null) throw new InvalidOperationException(bp);
            List<PackageProblem> rules = Check(store, start, mode, schedule);   // Regeln (Packages.Rules.cs): Sperre gilt fuer alle, auch PackageAdmin
            if (HasBlock(rules)) throw new InvalidOperationException("Gesperrt, Regel verletzt:\n" + DescribeBlocks(rules));
            List<RowInfo> rows = store.GetRows();
            int written = 0, skipped = 0, unknownRow, conflicts = 0;
            // Alle Bloecke in EINEM Schreibvorgang (Versionspruefung je Block)
            List<SaveItem> batch = new List<SaveItem>(); List<Target> src = new List<Target>();
            foreach (Target t in BuildTargets(rows, start, schedule, out unknownRow))
            {
                SaveItem it = Prepare(store, rows, t, mode, by);
                if (it == null) { skipped++; continue; }
                batch.Add(it); src.Add(t);
            }
            if (batch.Count > 0) written += store.SaveManyWithAudit(batch);
            // Konflikte: Ergebnis aus dem neuen Stand mit derselben Modus-Logik neu berechnen, zweiter Durchgang nur fuer diese
            List<SaveItem> again = new List<SaveItem>();
            for (int i = 0; i < batch.Count; i++)
            {
                if (batch[i].Saved) continue;
                SaveItem it = Prepare(store, rows, src[i], mode, by);
                if (it == null) skipped++; else again.Add(it);
            }
            if (again.Count > 0) { int w = store.SaveManyWithAudit(again); written += w; conflicts += again.Count - w; }
            string msg = written + " Block/Blöcke geschrieben";
            if (skipped > 0) msg += ", " + skipped + " übersprungen (bereits belegt)";
            if (unknownRow > 0) msg += ", " + unknownRow + " mit unbekanntem Blocknamen ignoriert";
            if (conflicts > 0) msg += ", " + conflicts + " wegen Konflikt nicht gespeichert";
            return msg + ".";
        }

        static int RowId(List<RowInfo> rows, string name)
        {
            foreach (RowInfo r in rows) if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) return r.Id;
            return -1;
        }
    }
}
