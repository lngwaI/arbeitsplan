#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Soft-Lock erweitern: Recht, Nachrichten (Datei/Speicher), Uebernahme, Sperrenwechsel, Verdraengung. Testbilder: --form locked|takeover|notices.
    partial class MainForm
    {
        Form TestNoticeForm(string which)
        {
            DateTime day = start.AddDays(4);
            if (which == "locked") return new LockedForm("Suite 1", day, "Paula Werk", true);
            if (which == "takeover") { TakeOverForm t = new TakeOverForm("Suite 1", day, "Paula Werk"); t.TestReason("Ich muss den Lauf vor der Übergabe korrigieren.\r\nBitte morgen kurz melden."); return t; }
            MemoryNoticeService m = new MemoryNoticeService(); string e;
            Notice a = Notice.TakeOver("Max Mustermann", "MUMA", "Suite 2", Ui.DayText(day.AddDays(-2)), day.AddDays(-2), 2, "");
            a.Time = DateTime.Now.AddHours(-30); m.Add("x", a, out e); m.MarkRead("x", a.Id);
            Notice b = Notice.TakeOver("Erika Beispiel", "BEER", "Suite 1", Ui.DayText(day), day, 1, "Laufnummer war falsch, bitte nicht überschreiben.");
            b.Time = DateTime.Now.AddHours(-3); m.Add("x", b, out e);
            m.Add("x", Notice.TakeOver("Max Mustermann", "MUMA", "Mix / Split", Ui.DayText(day), day, 3, "Dringend: Probe fehlt"), out e);
            return new NoticesForm(delegate { return m.List("x"); }, delegate { return m.MarkAllRead("x"); });
        }

        static UserDb UsersRoundTrip(UserDb db)
        {
            StringBuilder s = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(s)) db.WriteXml(w);
            XmlDocument doc = new XmlDocument(); doc.LoadXml(s.ToString());
            return UserDb.ReadXml(doc.DocumentElement);
        }

        static bool Has(RightsPackage p, string r) { return Array.IndexOf(p.Rights, r) >= 0; }

        void SelfTestTakeOver(StringBuilder sb)
        {
            string e;
            // ---------- Recht (kein neues Datenformat: Rechte sind Texte) ----------
            UserDb d = UserDb.Default();
            bool okPerm = Has(d.Package("Produktion"), Perm.TakeOver) && !Has(d.Package("Leser"), Perm.TakeOver) && Has(d.Package("Admin"), Perm.TakeOver)
                && Array.IndexOf(Perm.All, Perm.TakeOver) >= 0 && Perm.Label(Perm.TakeOver).Length > 0 && Has(UsersRoundTrip(d).Package("Produktion"), Perm.TakeOver);
            RightsPackage pr = d.Package("Produktion"); pr.Rights = Array.FindAll(pr.Rights, delegate(string p) { return p != Perm.TakeOver; });
            okPerm &= !Has(UsersRoundTrip(d).Package("Produktion"), Perm.TakeOver);   // aktuelle Datei: bewusst entzogen bleibt entzogen
            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<Nutzer rechte=\"3\"><Paket id=\"produktion\" name=\"Produktion\"><R v=\"Lesen\"/><R v=\"TextAendern\"/></Paket><Paket id=\"leser\" name=\"Leser\"><R v=\"Lesen\"/></Paket></Nutzer>");
            UserDb od = UserDb.ReadXml(doc.DocumentElement);
            okPerm &= Has(od.Package("Produktion"), Perm.TakeOver) && !Has(od.Package("Leser"), Perm.TakeOver);   // Datei vor dem Recht: Produktion bekommt es

            // ---------- Nachrichten als Dateien (Temp-Ordner) und im Speicher ----------
            string dir = Path.Combine(Path.GetTempPath(), "notices_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            bool okFile = true, okMem = true, okErr = true, okLockF = true;
            try
            {
                DateTime day = new DateTime(2027, 3, 12);
                FileNoticeService fs = new FileNoticeService(dir);
                Notice a = Notice.TakeOver("Max Mustermann", "MUMA", "Suite 1", Ui.DayText(day), day, 3, "Dringend:\näöü \\ Test");
                Notice b = Notice.TakeOver("Erika Beispiel", "", "Suite 2", Ui.DayText(day), day, 4, "");
                b.Time = a.Time.AddSeconds(5);
                Notice ml = new Notice(); ml.Kind = "x"; ml.Text = "Zeile 1\nZeile 2 mit \\n und \\\\"; ml.Time = a.Time.AddSeconds(10);
                okFile &= fs.Add("PWerk", a, out e) && fs.Add("pwerk", b, out e) && fs.Add("PWERK", ml, out e) && fs.Add("ebeispiel", a.Clone(), out e);
                File.WriteAllText(Path.Combine(dir, "nachrichten", "pwerk", "halb.tmp"), "zeit=halb");        // halb geschriebene/kaputte Dateien stoeren nicht
                File.WriteAllText(Path.Combine(dir, "nachrichten", "pwerk", "kaputt.txt"), "murks");
                List<Notice> l = new FileNoticeService(dir).List("pwerk");   // neue Instanz = Rundreise
                okFile &= l.Count == 3 && l[0].Id == ml.Id && l[1].Id == b.Id && l[2].Id == a.Id && CountUnread(l) == 3
                    && l[0].Text == ml.Text && l[1].Text.Contains("kein Grund angegeben") && !l[1].Text.Contains("(")
                    && l[2].Text == a.Text && l[2].Text.Contains("Dringend: äöü \\ Test") && l[2].Kind == Notice.KindTakeOver && l[2].Day == day && l[2].RowId == 3
                    && Directory.GetFiles(Path.Combine(dir, "nachrichten", "pwerk"), "*.tmp").Length == 1 && new FileNoticeService(dir).List("ebeispiel").Count == 1;
                okFile &= fs.MarkRead("pwerk", b.Id) && CountUnread(new FileNoticeService(dir).List("pwerk")) == 2 && !fs.MarkRead("pwerk", "..\\x")
                    && File.Exists(Path.Combine(dir, "nachrichten", "pwerk", b.Id + ".gelesen"));
                okFile &= fs.MarkAllRead("pwerk") == 2 && CountUnread(fs.List("pwerk")) == 0 && fs.List("pwerk").Count == 3 && fs.List("niemand").Count == 0;   // gelesene bleiben im Verlauf
                for (int i = 0; i < Notice.Keep + 5; i++) fs.Add("viele", Notice.TakeOver("A", "", "B", "C", day, i, ""), out e);
                List<Notice> many = fs.List("viele");
                okFile &= many.Count == Notice.Keep && many[0].Time >= many[many.Count - 1].Time;
                // Fehler beim Schreiben: Meldung statt Absturz
                string blocker = Path.Combine(dir, "blocker.txt"); File.WriteAllText(blocker, "x");
                okErr = !new FileNoticeService(blocker).Add("x", a.Clone(), out e) && e != null && e.Length > 0 && new FileNoticeService(blocker).List("x").Count == 0;
                // Speicher-Variante
                MemoryNoticeService ms = new MemoryNoticeService();
                okMem &= ms.Add("A", a.Clone(), out e) && ms.Add("a", b.Clone(), out e) && ms.List("A").Count == 2 && ms.List("a")[0].Text == b.Text && CountUnread(ms.List("a")) == 2
                    && ms.MarkRead("a", ms.List("a")[0].Id) && CountUnread(ms.List("a")) == 1 && ms.MarkAllRead("A") == 1 && ms.List("b").Count == 0;
                for (int i = 0; i < Notice.Keep + 5; i++) ms.Add("v", a.Clone(), out e);
                okMem &= ms.List("v").Count == Notice.Keep;
                // Sperre als Datei: nur die eigene Sperre wird freigegeben
                FileLockService fl = new FileLockService(dir);
                fl.Acquire(day, 1, "Paula Werk"); okLockF &= fl.Holder(day, 1, "Max Mustermann") == "Paula Werk";
                fl.Acquire(day, 1, "Max Mustermann");                                          // Uebernahme: Sperre wechselt
                okLockF &= fl.Holder(day, 1, "Paula Werk") == "Max Mustermann";
                fl.Release(day, 1, "Paula Werk");                                              // Verdraengter beendet seinen Modus
                okLockF &= fl.Holder(day, 1, "Paula Werk") == "Max Mustermann";
                fl.Release(day, 1, "Max Mustermann"); okLockF &= fl.Holder(day, 1, "Paula Werk") == null;
            }
            catch (Exception ex) { okFile = false; sb.AppendLine("Übernahme: Ausnahme in Nachrichtentest " + ex.GetType().Name + ": " + ex.Message); }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
            sb.AppendLine("Übernahme-Grundlagen: Recht=" + Ok(okPerm) + " Nachrichten-Datei=" + Ok(okFile) + " Speicher=" + Ok(okMem) + " Schreibproblem gemeldet=" + Ok(okErr) + " Sperre-Datei=" + Ok(okLockF));

            // ---------- Uebernahme im Fenster (Testdaten: Paula Werk ist Nutzerin) ----------
            if (users.FindByLogin("pwerk") == null || fstore != null || systemPc) { sb.AppendLine("Übernahme im Fenster: übersprungen (nur Testdaten ohne Systemrechner)"); return; }
            Rebuild(false);
            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 2 && x.Col == 5) c = x;
            fold[rowInfo[2].Id] = false; ApplyFold(); view.Relayout();
            int rid = rowInfo[c.RowIndex].Id; DateTime cday = c.Day; string title = BlockTitle(c);
            int a0 = store.GetAudit(cday, NoticeAuditRow).Count, n0 = notices.List("pwerk").Count;
            locks.Acquire(cday, rid, "Paula Werk");
            bool t1 = TakeOver(c, "Paula Werk", "Dringend\nbitte melden", out e), t2 = TakeOver(c, "Paula Werk", "", out e);
            List<Notice> nl = notices.List("pwerk");
            bool okN = t1 && t2 && nl.Count == n0 + 2 && nl[1].Text.Contains("Dringend bitte melden") && nl[1].Text.Contains(me.Display) && nl[1].Text.Contains(title + " am " + Ui.DayText(cday))
                && (me.Kuerzel.Length == 0 || nl[1].Text.Contains("(" + me.Kuerzel + ")")) && nl[0].Text.Contains("kein Grund angegeben") && nl[0].Text.EndsWith("Ihre ungespeicherten Änderungen sind verloren.")
                && CountUnread(nl) >= 2 && nl[0].RowId == rid && nl[0].Day == cday.Date;
            List<AuditEntry> au = store.GetAudit(cday, NoticeAuditRow);
            bool okA = au.Count == a0 + 2 && au[au.Count - 2].Value.Contains("Dringend bitte melden") && au[au.Count - 2].Value.Contains("bisher Paula Werk") && au[au.Count - 2].Value.StartsWith("Bearbeitung übernommen von " + me.Display)
                && au[au.Count - 1].Value.Contains("kein Grund angegeben") && au[au.Count - 1].RowName == title && au[au.Count - 1].Login == me.Login;
            int unread = CountUnread(notices.List("pwerk")); notices.MarkAllRead("pwerk");
            bool okRead = unread >= 2 && CountUnread(notices.List("pwerk")) == 0 && notices.List("pwerk").Count == nl.Count;

            bool had = me.Has(Perm.TakeOver); me.Rights.Remove(Perm.TakeOver);
            string e2; bool okNo = !TakeOver(c, "Paula Werk", "x", out e2) && e2.Length > 0 && notices.List("pwerk").Count == nl.Count && store.GetAudit(cday, NoticeAuditRow).Count == a0 + 2;
            using (LockedForm lf = new LockedForm("X", cday, "Paula Werk", me.Has(Perm.TakeOver))) okNo &= !lf.CanTakeOver;
            if (had) me.Rights.Add(Perm.TakeOver);
            using (LockedForm lf = new LockedForm("X", cday, "Paula Werk", true)) okNo &= lf.CanTakeOver;
            string e3; okNo &= !TakeOver(c, "Niemand Bekannt", "", out e3) && e3.Contains("nicht als Nutzer bekannt");

            locks.Acquire(cday, rid, me.Display);                                              // wie BeginEdit nach der Uebernahme
            bool okLock = locks.Holder(cday, rid, "Paula Werk") == me.Display;
            locks.Release(cday, rid, "Paula Werk");                                            // Verdraengter beendet seinen Modus
            okLock &= locks.Holder(cday, rid, "Paula Werk") == me.Display;
            locks.Release(cday, rid, me.Display); okLock &= locks.Holder(cday, rid, "Paula Werk") == null;

            // Verdraengung: ich bearbeite mit ungespeicherter Aenderung, ein anderer uebernimmt (Nachricht an mich)
            BeginEdit(c);
            bool okEv = editCell == c;
            Entry before = store.Get(cday, rid) ?? new Entry(); string content0 = before.Content; int ver0 = before.Version;
            edContent.Text = edContent.Text + " ungespeichert"; okEv &= HasUnsavedChanges;
            Notice old = Notice.TakeOver("Paula Werk", "WEPA", title, Ui.DayText(cday), cday, rid, ""); old.Time = editStart.AddMinutes(-5); notices.Add(me.Login, old, out e);
            Notice oth = Notice.TakeOver("Paula Werk", "WEPA", "Anderer Block", Ui.DayText(cday), cday, rid + 1000, ""); notices.Add(me.Login, oth, out e);
            OnNotices(notices.List(me.Login));
            okEv &= editCell == c && locks.Holder(cday, rid, "Paula Werk") == me.Display;          // alte Nachricht/anderer Block: bleibt im Bearbeitungsmodus
            Notice real = Notice.TakeOver("Paula Werk", "WEPA", title, Ui.DayText(cday), cday, rid, "Test"); notices.Add(me.Login, real, out e);
            OnNotices(notices.List(me.Login));
            Entry after = store.Get(cday, rid) ?? new Entry();
            List<Notice> mine = notices.List(me.Login);
            okEv &= editCell == null && !HasUnsavedChanges && after.Content == content0 && after.Version == ver0                       // ohne Speichern, ohne Rueckfrage
                && mine.Find(delegate(Notice x) { return x.Id == real.Id; }).Read && !mine.Find(delegate(Notice x) { return x.Id == old.Id; }).Read && CountUnread(mine) == 2;
            notices.MarkAllRead(me.Login);
            sb.AppendLine("Übernahme: Nachricht mit/ohne Grund=" + Ok(okN) + " Protokoll=" + Ok(okA) + " gelesen/ungelesen=" + Ok(okRead) + " Recht fehlt=" + Ok(okNo)
                + " Sperre wechselt=" + Ok(okLock) + " Verdrängung ohne Speichern=" + Ok(okEv));
        }
    }
}
#endif
