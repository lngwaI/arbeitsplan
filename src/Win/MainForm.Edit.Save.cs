using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class MainForm
    {
        // Hat ein anderer PC den Block inzwischen gespeichert (Versionskonflikt), wird nie still ueberschrieben: Aenderungen an
        // verschiedenen Stellen fuehrt Conflict.Merge zusammen und speichert; nur bei derselben Stelle fragt ConflictForm.
        // n ist danach das Gespeicherte. false = nicht gespeichert, Block bleibt im Editor (ggf. mit dem Vorschlag).
        bool TrySave(CellData c, ref Entry n)
        {
            refreshing = true; Status("Speichern…", false); statusBar.Update();   // Titel "aktualisiere…", Statusleiste sofort zeichnen
            bool ok = false;
            try { ok = TrySaveCore(c, ref n); return ok; }
            finally { refreshing = false; Status(ok ? "Gespeichert " + DateTime.Now.ToString("HH:mm") : "Nicht gespeichert", !ok); }
        }

        bool TrySaveCore(CellData c, ref Entry n)
        {
            RowInfo ri = rowInfo[c.RowIndex];
            Entry bas = c.Cur;   // Stand beim Beginn der Bearbeitung; nach einer Zusammenfuehrung der damals gespeicherte Stand
            try
            {
                for (int round = 0; round < 5; round++)
                {
                    if (Audit.Save(store, c.Day, ri, me, n)) return true;   // mit Protokolleintrag
                    Entry latest = store.Get(c.Day, ri.Id) ?? new Entry();
                    List<string> clashes = new List<string>();
                    Entry merged = Conflict.Merge(bas, n, latest, clashes);
                    if (clashes.Count > 0)
                    {
                        ConflictForm.Choice ch = quiet ? quietChoice : AskConflict(c, latest, n, merged);
                        if (ch == ConflictForm.Choice.Mine && !me.Has(Perm.DeleteText)) ch = ConflictForm.Choice.Merge;   // Fremdes ersetzen = loeschen
                        if (ch == ConflictForm.Choice.Cancel) return false;
                        if (ch == ConflictForm.Choice.Merge) { LoadMerged(c, latest, merged); return false; }
                        merged = Conflict.Merge(bas, n, latest, new List<string>(), true);   // strittige Stellen meine, Rest bleibt; Fremdes im Verlauf
                    }
                    n = merged; bas = latest;
                }
                if (!quiet) MessageBox.Show("Der Block wird gerade laufend von anderen geändert. Bitte gleich noch einmal speichern (Strg+S).", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex) { ShowSaveError(ex); return false; }
        }

        static ConflictForm.Choice quietChoice = ConflictForm.Choice.Merge;   // Selbsttest: Antwort statt Rueckfrage

        ConflictForm.Choice AskConflict(CellData c, Entry latest, Entry mine, Entry merged)
        {
            string who = "Jemand anderes", when = "";
            AuditEntry last = null;
            foreach (AuditEntry a in store.GetAudit(c.Day, rowInfo[c.RowIndex].Id)) if (last == null || a.Time >= last.Time) last = a;
            if (last != null)
            {
                User u = users.FindByLogin(last.Login);
                who = u != null ? u.Display : last.Login.Length > 0 ? last.Login : who;
                when = "um " + last.Time.ToString("HH:mm") + " Uhr";
            }
            using (ConflictForm f = new ConflictForm(BlockTitle(c) + ", " + Ui.DayText(c.Day), who, when, latest, mine, merged, me.Has(Perm.DeleteText)))
            {
                f.ShowDialog(this);
                return f.Result;
            }
        }

        // Vorschlag in den Editor; Basis ist jetzt der gespeicherte Stand (das naechste Speichern vergleicht damit)
        void LoadMerged(CellData c, Entry latest, Entry merged)
        {
            c.Cur = merged; LoadEditors(c);
            c.Cur = latest; c.Show(merged);
            fmtChanged = merged.SpanKey() != latest.SpanKey();
            UpdateChanged();
            view.Relayout();
        }

        static void ShowSaveError(Exception ex)
        {
            if (quiet) return;
            MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message +
                "\n\nIhre Änderung ist NICHT verloren und bleibt im Editor sichtbar.\nSpeichern später erneut versuchen (Strg+S) oder mit Esc beenden.",
                "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
