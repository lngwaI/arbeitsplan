using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Soft-Lock erweitert: Bearbeitung uebernehmen (Recht TakeOver), Benachrichtigung des Verdraengten, Menue Hilfe -> Benachrichtigungen.
    partial class MainForm
    {
        public const int NoticeAuditRow = -3;   // Protokollzeile fuer Uebernahmen (-1 Wochenhaken, -2 Equipment-Status)

        INoticeService notices;
        ToolStripMenuItem miNotices;
        DateTime editStart;                                           // Beginn der laufenden Bearbeitung (nur Nachrichten danach verdraengen)
        readonly HashSet<string> shownNotices = new HashSet<string>();   // schon als Fenster gezeigt (falls Markieren als gelesen scheitert)
        int lastUnread;

        void InitNotices()
        {
            notices = cfg == null || shownPlan != null ? (INoticeService)new MemoryNoticeService() : new FileNoticeService(cfg.DataFolder);
            if (shownPlan != null) return;
            bool busy = false, closed = false;
            Timer t = new Timer(); t.Interval = 5000;
            t.Tick += delegate
            {
                if (busy) return;
                busy = true;
                string login = me.Login;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    List<Notice> l = null;
                    try { l = notices.List(login); } catch (Exception) { }
                    try { BeginInvoke((MethodInvoker)delegate { busy = false; if (!closed && !IsDisposed && l != null) OnNotices(l); }); }
                    catch (Exception) { }   // Fenster schon geschlossen
                });
            };
            t.Start();
            FormClosed += delegate { closed = true; t.Stop(); t.Dispose(); };
#if TESTHOOKS
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--shot") >= 0) return;   // Testbild/Selbsttest: keine Rueckfrage beim Start
#endif
            Shown += delegate { BeginInvoke((MethodInvoker)delegate { StartupNotices(); }); };
        }

        // Nach dem Programmstart: ungelesene Meldungen (z. B. Programm war geschlossen oder abgestuerzt)
        void StartupNotices()
        {
            List<Notice> l;
            try { l = notices.List(me.Login); } catch (Exception) { return; }
            int n = CountUnread(l);
            ShowNoticeCount(n);
            if (n > 0 && MessageBox.Show(this, "Sie haben " + n + " ungelesene Benachrichtigung" + (n == 1 ? "" : "en") + ".\n\nJetzt ansehen?",
                "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) ShowNotices();
        }

        static int CountUnread(List<Notice> l) { int n = 0; foreach (Notice x in l) if (!x.Read) n++; return n; }

        void ShowNoticeCount(int n)
        {
            if (miNotices != null) miNotices.Text = n > 0 ? "Benachrichtigungen (" + n + " neu)…" : "Benachrichtigungen…";
            lastUnread = n;
        }

        void ShowNotices()
        {
            using (NoticesForm f = new NoticesForm(delegate { return notices.List(me.Login); }, delegate { return notices.MarkAllRead(me.Login); })) f.ShowDialog(this);
            try { ShowNoticeCount(CountUnread(notices.List(me.Login))); } catch (Exception) { }
        }

        // Ergebnis der Abfrage im UI-Thread: Menuezaehler, Hinweis auf neue Meldungen, Verdraengung aus dem Bearbeitungsmodus
        void OnNotices(List<Notice> l)
        {
            int n = CountUnread(l);
            if (n > lastUnread) Status("Neue Benachrichtigung (Hilfe)", true);
            ShowNoticeCount(n);
            CellData c = editCell;
            if (c == null || (!quiet && Application.OpenForms.Count > 1)) return;   // kein Dialog offen: sonst naechster Takt
            foreach (Notice x in l)
                if (!x.Read && x.Kind == Notice.KindTakeOver && x.Day == c.Day.Date && x.RowId == rowInfo[c.RowIndex].Id
                    && x.Time >= editStart.AddMinutes(-1) && !shownNotices.Contains(x.Id))
                { Evicted(x); return; }
        }

        // Bearbeitungsmodus endet ohne Speichern und ohne Rueckfrage; Meldung; Block aus dem aktuellen Stand neu laden
        void Evicted(Notice x)
        {
            shownNotices.Add(x.Id);
            notices.MarkRead(me.Login, x.Id);
            EndEdit(false);
            if (fstore != null) fstore.Refresh();
            Rebuild(false);
            ShowNoticeCount(Math.Max(0, lastUnread - 1));
            if (!quiet) MessageBox.Show(this, x.Text, "Bearbeitung übernommen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Hinweis beim Bearbeiten eines belegten Blocks (Soft-Lock). false = nicht bearbeiten.
        bool AskLocked(CellData c, string holder)
        {
            if (quiet) return true;
            string title = BlockTitle(c);
            LockedForm.Choice ch;
            using (LockedForm f = new LockedForm(title, c.Day, holder, me.Has(Perm.TakeOver))) { f.ShowDialog(this); ch = f.Result; }
            if (ch == LockedForm.Choice.Cancel) return false;
            if (ch == LockedForm.Choice.Continue) return true;
            string reason;
            using (TakeOverForm t = new TakeOverForm(title, c.Day, holder))
            {
                if (t.ShowDialog(this) != DialogResult.OK) return false;
                reason = t.Reason;
            }
            string err;
            if (TakeOver(c, holder, reason, out err)) return true;
            MessageBox.Show(this, "Übernahme nicht möglich: " + err, "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Protokoll + Nachricht an den bisherigen Bearbeiter (die Sperre setzt BeginEdit danach auf uns). Schreibfehler -> false mit Text.
        bool TakeOver(CellData c, string holder, string reason, out string err)
        {
            err = null;
            if (!me.Has(Perm.TakeOver)) { err = "Das Recht \"" + Perm.Label(Perm.TakeOver) + "\" fehlt."; return false; }
            User victim = null; int found = 0;
            foreach (User u in users.Users) if (string.Equals(u.Display, holder, StringComparison.OrdinalIgnoreCase)) { victim = u; found++; }
            if (found != 1) { err = found == 0 ? holder + " ist nicht als Nutzer bekannt." : "Der Name " + holder + " ist mehrfach vergeben; die Benachrichtigung ist nicht eindeutig möglich."; return false; }
            RowInfo ri = rowInfo[c.RowIndex];
            Notice nt = Notice.TakeOver(me.Display, me.Kuerzel, BlockTitle(c), Ui.DayText(c.Day), c.Day, ri.Id, reason);
            AuditEntry a = new AuditEntry();   // erst protokollieren: schlaegt es fehl, wird niemand benachrichtigt
            a.Time = DateTime.Now; a.Login = me.Login; a.Kuerzel = me.Kuerzel; a.Role = me.RoleText;
            a.Day = c.Day.Date; a.RowId = NoticeAuditRow; a.RowName = BlockTitle(c); a.Mark = "Uebernahme";
            a.Value = "Bearbeitung übernommen von " + me.Display + ", bisher " + holder + ", Grund " + Notice.CleanReason(reason);
            try { store.AppendAudit(a); } catch (Exception ex) { err = ex.Message; return false; }
            return notices.Add(victim.Login, nt, out err);
        }
    }
}
