#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Start im Firmennetz (Umgebung, Ersteinrichtung, Nutzer, Verknuepfung) und Mehrplatz (Zusammenfuehren,
    // Editor-Konflikt). Lasttest mit zwei Prozessen: --stress / --stresscheck (Workflow startet zwei Programme gleichzeitig).
    partial class MainForm
    {
        void SelfTestSite(StringBuilder sb)
        {
            string ud = Umgebung.UserDomain, md = Umgebung.MachineDomain, pc = Umgebung.Machine;
            bool okLike = Umgebung.Like("FIRMA", "firma") && Umgebung.Like("SYS-12", "SYS-??") && !Umgebung.Like("SYS-123", "SYS-??")
                && Umgebung.Like("View-Only3", "view-only*") && !Umgebung.Like("", "*") && !Umgebung.Like("x", "");
            List<string> fl = new List<string>(new[] { "FIRMA" }), fd = new List<string>(new[] { "firma.local" });
            bool okDom = Umgebung.Allowed(new List<string>(new[] { "*" }), "MEINPC", "", "MEINPC") && Umgebung.AutoBind("PC1", "", "PC1").Count == 0
                && Umgebung.AutoBind("AzureAD", "", "PC1").Count == 0 && string.Join(",", Umgebung.AutoBind("FIRMA", "firma.local", "PC1").ToArray()) == "FIRMA,firma.local"
                && Umgebung.Allowed(new List<string>(), "X", "", "X") && Umgebung.Allowed(fl, "firma", "", "PC1") && !Umgebung.Allowed(fl, "MEINPC", "", "MEINPC")
                && Umgebung.Allowed(fd, "PC1", "firma.local", "PC1") && Umgebung.Home("FIRMA", "firma.local", "PC1") == "FIRMA"
                && Umgebung.Home("PC1", "firma.local", "PC1") == "firma.local" && Umgebung.Home("MEINPC", "", "MEINPC") == "MEINPC";

            string root = Path.Combine(Path.GetTempPath(), "apsite_selftest");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string cache = Path.Combine(root, "cache.apdb");   // nie den echten Offline-Stand dieses PCs anfassen
            // exe allein kopiert (z. B. per E-Mail nach Hause): kein Datenordner -> Meldung, nichts wird angelegt
            string home = Path.Combine(root, "zuhause"); Directory.CreateDirectory(home);
            AppConfig hc = AppConfig.Load(home); hc.CacheDays = 0; hc.CachePath = cache;
            FileStore fs;
            string msg = Umgebung.Open(hc, "jemand", out fs);
            bool okCopy = msg != null && fs == null && msg.Contains("nicht erreichbar") && !Directory.Exists(hc.DataFolder);

            // Laufwerk-Aufbau Programm\ + daten\ (leer): nicht eingerichtet; nur Admins= aus der ini darf einrichten
            string prog = Path.Combine(root, "Arbeitsplan\\Programm"), data = Path.Combine(root, "Arbeitsplan\\daten");
            Directory.CreateDirectory(prog); Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(prog, "Arbeitsplan.ini"), "Admins=chef\r\nNurLesenFuer=View-Only*, SYS-??\r\nCacheTage=0\r\n", Encoding.UTF8);
            AppConfig cfg = Cfg(prog, cache);
            bool okLayout = string.Equals(cfg.DataFolder.TrimEnd('\\'), data, StringComparison.OrdinalIgnoreCase) && cfg.ReadOnlyFor("view-only2", "x") && cfg.ReadOnlyFor("y", "SYS-01") && !cfg.ReadOnlyFor("y", "SYS-012");
            msg = Umgebung.Open(cfg, "fremd", out fs);
            bool okNotSet = Umgebung.NeedsSetup(data) && msg != null && msg.Contains("noch nicht eingerichtet") && !File.Exists(Path.Combine(data, FileStore.FileName))
                && Umgebung.IsSetupAdmin(cfg, "CHEF") && !Umgebung.IsSetupAdmin(cfg, "fremd");
            bool okSetup = Umgebung.Setup(cfg) == null && File.Exists(Path.Combine(data, FileStore.FileName)) && !Umgebung.NeedsSetup(data)
                && (File.GetAttributes(Path.Combine(data, FileStore.KeyFileName)) & (FileAttributes.Hidden | FileAttributes.System)) == (FileAttributes.Hidden | FileAttributes.System)
                && (File.GetAttributes(data) & FileAttributes.Hidden) != 0 && Directory.Exists(cfg.PackageFolder);
            // erster Start des Einrichters: darf (Admins=, noch kein Admin in der Liste); Unbekannte nicht
            bool okAdmin = Umgebung.Open(Cfg(prog, cache), "chef", out fs) == null && fs != null && !fs.ReadOnly;
            UserChanges ch = new UserChanges(); User chef = fs.Users.Add("C", "Chef", "chef", "Admin"); ch.Changed.Add(chef); ch.Added.Add(chef);
            ch.Domains = new List<string>(new[] { "FIRMA-TEST-XYZ" }); fs.SaveUsers(ch);
            msg = Umgebung.Open(Cfg(prog, cache), "chef", out fs);
            bool okForeign = msg != null && msg.Contains("nicht freigegeben") && msg.Contains("FIRMA-TEST-XYZ");
            FileStore w = new FileStore(data, false, null, 0);
            UserChanges ch2 = new UserChanges(); ch2.Domains = new List<string>(new[] { Umgebung.Home(ud, md, pc) }); w.SaveUsers(ch2);
            okForeign &= Umgebung.Open(Cfg(prog, cache), "chef", out fs) == null && new FileStore(data, true, null, 0).Users.Domains.Count == 1;
            msg = Umgebung.Open(Cfg(prog, cache), "fremd", out fs);
            bool okUnknown = msg != null && msg.Contains("nicht als Nutzer angelegt") && msg.Contains("fremd") && fs == null;
            AppConfig ro = Cfg(prog, cache);
            bool okRO = Umgebung.Open(ro, "View-Only7", out fs) == null && fs.ReadOnly && ro.ReadOnly;
            AppConfig pi = Cfg(prog, cache); pi.Pilot = true;
            bool okPilot = Umgebung.Open(pi, "fremd", out fs) == null && !fs.ReadOnly;
            // Datendatei weg, Schluessel noch da: nie still eine leere anlegen (auch nicht durch "Einrichter")
            string apdb = Path.Combine(data, FileStore.FileName), moved = apdb + ".weg";
            File.Move(apdb, moved);
            FileStore gone = new FileStore(data, false, null, 0);
            bool okGone = gone.Offline && !File.Exists(apdb) && gone.Problem.Contains("fehlt") && Umgebung.Open(Cfg(prog, cache), "chef", out fs) != null && !File.Exists(apdb);
            File.Move(moved, apdb);
            okGone &= Umgebung.Open(Cfg(prog, cache), "chef", out fs) == null;
            bool okNewer = false;
            try { Backup.FromXml(Encoding.UTF8.GetBytes("<Arbeitsplan version=\"" + (Backup.FormatVersion + 1) + "\"><Zeilen/><Katalog/></Arbeitsplan>")); }
            catch (NewerFormatException) { okNewer = true; }
            // Verknuepfung: Ziel lesbar; lokale Pfade bleiben, \\Server gilt als Laufwerk
            string lnk = Path.Combine(root, "Test.lnk");
            Shortcut.Write(lnk, Application.ExecutablePath);
            bool okLnk = File.Exists(lnk) && string.Equals(Shortcut.Target(lnk), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                && Shortcut.Unc(@"C:\x\y.exe") == @"C:\x\y.exe" && Shortcut.OnNetwork(@"\\server\freigabe\a.exe");
            sb.AppendLine("Umgebung: Muster=" + Ok(okLike) + " Domäne=" + Ok(okDom) + " kopierte exe=" + Ok(okCopy) + " Laufwerk-Aufbau=" + Ok(okLayout)
                + " nicht eingerichtet=" + Ok(okNotSet) + " Einrichtung=" + Ok(okSetup) + " Einrichter=" + Ok(okAdmin) + " fremde Domäne=" + Ok(okForeign)
                + " unbekannter Nutzer=" + Ok(okUnknown) + " NurLesenFuer=" + Ok(okRO) + " Pilot=" + Ok(okPilot) + " Datei weg->kein Neuanlegen=" + Ok(okGone) + " neueres Format=" + Ok(okNewer) + " Verknüpfung=" + Ok(okLnk)
                + " (hier: " + Umgebung.Home(ud, md, pc) + ")");
        }

        static AppConfig Cfg(string prog, string cache) { AppConfig c = AppConfig.Load(prog); c.CachePath = cache; return c; }

        static Entry E(string content) { Entry e = new Entry(); e.Content = content; e.Version = 3; return e; }

        void SelfTestMulti(StringBuilder sb)
        {
            List<string> cl = new List<string>();
            Entry b = E("Zeile 1\nZeile 2\nZeile 3"), t = E("Zeile 1\nZeile 2\nZeile 3 anders"); t.People[1] = "XY"; t.Version = 4;
            Entry r = Conflict.Merge(b, E("Zeile 1 neu\nZeile 2\nZeile 3"), t, cl);
            bool okLines = cl.Count == 0 && r.Content == "Zeile 1 neu\nZeile 2\nZeile 3 anders" && r.People[1] == "XY" && r.Version == 4;
            r = Conflict.Merge(E("A\nB"), E("A2\nB"), E("A\nB2"), cl);   // Nachbarzeilen
            okLines &= cl.Count == 0 && r.Content == "A2\nB2";
            cl.Clear(); r = Conflict.Merge(b, E("Mein\nZeile 2\nZeile 3"), E("Dein\nZeile 2\nZeile 3"), cl);
            bool okSame = cl.Count == 1 && cl[0] == "Inhalt" && r.Content == "Dein\nMein\nZeile 2\nZeile 3";
            cl.Clear(); r = Conflict.Merge(E("A\nB\nC"), E("A1\nB\nC"), E("A2\nB\nC2"), cl, true);   // "Meine Fassung": fremde Aenderung an C bleibt
            okSame &= cl.Count == 1 && r.Content == "A1\nB\nC2";
            Entry bp = E("x"), mp = E("x"), tp = E("x"); bp.People[0] = "AA"; mp.People[0] = "AA, BB"; tp.People[0] = "AA, CC";
            cl.Clear(); r = Conflict.Merge(bp, mp, tp, cl);
            bool okPpl = cl.Count == 1 && cl[0] == "F" && r.People[0] == "AA, CC, BB";
            Entry bf = E("ab\ncd"); FormatSpan fb = new FormatSpan(); fb.Start = 3; fb.Length = 2; fb.Bold = true; bf.Spans.Add(fb);
            Entry mf = bf.Clone(); mf.Content = "abXYZ\ncd"; mf.Spans[0].Start = 6; Entry tf = bf.Clone(); tf.Lkz = true;
            cl.Clear(); r = Conflict.Merge(bf, mf, tf, cl);
            bool okFmt = cl.Count == 0 && r.Content == "abXYZ\ncd" && r.Spans.Count == 1 && r.Spans[0].Start == 6 && r.Spans[0].Length == 2 && r.Spans[0].Bold && r.Lkz;
            cl.Clear(); r = Conflict.Merge(new Entry(), E("A"), E("B"), cl);
            bool okNew = cl.Count == 1 && r.Content == "B\nA";

            // Im Editor: ein "anderer PC" speichert denselben Block waehrend der Bearbeitung
            Rebuild(true);
            CellData c = Cell(1, 3); RowInfo ri = rowInfo[1]; DateTime day = c.Day;
            Entry s0 = store.Get(day, ri.Id) ?? new Entry(); s0.Content = "Zeile 1\nZeile 2"; Audit.Save(store, day, ri, me, s0);
            Rebuild(true); c = Cell(1, 3);
            BeginEdit(c); edContent.Text = "Zeile 1 von mir\nZeile 2";
            Entry o = store.Get(day, ri.Id); o.Content = "Zeile 1\nZeile 2 vom anderen PC"; o.People[2] = "QQQQ"; store.Save(day, ri.Id, o);
            bool saved = EndEdit(true);
            Entry now = store.Get(day, ri.Id);
            bool okEd = saved && editCell == null && now.Content == "Zeile 1 von mir\nZeile 2 vom anderen PC" && now.People[2] == "QQQQ";
            BeginEdit(c); edContent.Text = "Mein Text\nZeile 2 vom anderen PC";
            o = store.Get(day, ri.Id); o.Content = "Dein Text\nZeile 2 vom anderen PC"; store.Save(day, ri.Id, o);
            quietChoice = ConflictForm.Choice.Merge;
            bool stays = !EndEdit(true) && editCell == c && edContent.Text.Contains("Dein Text") && edContent.Text.Contains("Mein Text") && HasUnsavedChanges;
            bool saved2 = EndEdit(true);
            now = store.Get(day, ri.Id);
            bool okProp = stays && saved2 && now.Content == "Dein Text\nMein Text\nZeile 2 vom anderen PC";
            BeginEdit(c); edContent.Text = "Nur meins";
            o = store.Get(day, ri.Id); o.Content = "Nur deins"; store.Save(day, ri.Id, o);
            quietChoice = ConflictForm.Choice.Cancel;
            bool cancel = !EndEdit(true) && editCell == c && store.Get(day, ri.Id).Content == "Nur deins";
            quietChoice = ConflictForm.Choice.Mine;
            bool mine = EndEdit(true) && store.Get(day, ri.Id).Content == "Nur meins";
            quietChoice = ConflictForm.Choice.Merge;

            // Paket-Uebernahme auf PC A, waehrend PC B einen Zielblock aendert (A hat noch den alten Stand): nichts geht verloren
            string pdir = Path.Combine(Path.GetTempPath(), "apmulti_selftest");
            if (Directory.Exists(pdir)) Directory.Delete(pdir, true);
            Directory.CreateDirectory(pdir);
            FileStore pa = new FileStore(pdir, false, null, 0), pb = new FileStore(pdir, false, null, 0);
            Package pk = Package.SampleCampaign("Mehrplatz");
            DateTime ps = new DateTime(2026, 3, 2);
            RunDate r0 = pk.Schedule()[0];
            RowInfo row0 = pb.GetRows().Find(delegate(RowInfo x) { return x.Name == r0.Row; });
            bool okPkg = row0 != null;
            if (okPkg)
            {
                Entry eb = new Entry(); eb.Content = "Von PC B"; pb.Save(ps.AddDays(r0.Day - 1), row0.Id, eb);
                string res = pk.Apply(pa, ps, ApplyMode.Append, null, me);
                Entry got = new FileStore(pdir, true, null, 0).Get(ps.AddDays(r0.Day - 1), row0.Id);
                okPkg = got != null && got.Content.StartsWith("Von PC B") && got.Content.Length > "Von PC B".Length && !res.Contains("Konflikt");
            }
            sb.AppendLine("Mehrplatz: Zeilen=" + Ok(okLines) + " gleicheZeile=" + Ok(okSame) + " Kürzel=" + Ok(okPpl) + " Format/LKZ=" + Ok(okFmt) + " neu=" + Ok(okNew)
                + " Editor zusammengeführt=" + Ok(okEd) + " Vorschlag=" + Ok(okProp) + " Abbrechen/Meine=" + Ok(cancel && mine) + " Paket-Übernahme mit Konflikt=" + Ok(okPkg));
        }

        // Kampagnenstart: Datum nur vollstaendig (TT.MM.JJJJ); direkt startbar nur mit bekannten Raeumen
        void SelfTestCampaignStart(StringBuilder sb)
        {
            Package p = Package.SampleCampaign("Start");
            foreach (PackageStep s in p.Steps) s.Row = rowInfo[1].Name;   // Raeume, die es im (umbenannten) Testplan gibt
            bool ready = PackageForm.Ready(p, store);
            Package q = Package.SampleCampaign("Raum"); foreach (PackageStep s in q.Steps) s.Row = rowInfo[1].Name; q.Steps[0].Row = "Gibt es nicht";
            bool notReady = !PackageForm.Ready(q, store) && !PackageForm.Ready(new Package(), store);
            CampaignForm.Quiet = true;
            bool part, full;
            using (CampaignForm cf = new CampaignForm(p, store, start, ApplyMode.FillEmpty))
            {
                string before = cf.TestCell(1);
                int d0 = cf.Schedule[1].Day;
                part = cf.TestTyped(1, "13.03.") == before && cf.Schedule[1].Day == d0;
                DateTime want = cf.Start.AddDays(d0 - 1 + 2);
                full = cf.TestTyped(1, want.ToString("dd.MM.yyyy")) == Ui.DayText(want) && cf.Schedule[1].Day == d0 + 2 && cf.TestStartField();
            }
            CampaignForm.Quiet = false;
            sb.AppendLine("Kampagnenstart: Datum ohne Jahr abgelehnt=" + Ok(part) + " TT.MM.JJJJ=" + Ok(full) + " direkt startbar=" + Ok(ready) + " unbekannter Raum->Editor=" + Ok(notReady));
        }

        // Verschiedene Equipment-Nummern: erkannt (auch mit Zusatz, in Klammern, nach Doppelpunkt, mehrere), nicht erkannt
        // (falscher Bereich/Typ, zu kurz/lang, angeklebt); Status aus der Quelle; frei eingetippte Wechsel-/Ausweichgeraete gemeldet
        void SelfTestEquipment(StringBuilder sb)
        {
            string text = "TFF:H1-X-0101 (H1-X-0103), Pumpe H1-P-0101-05; H2-F-0202-01/H1-T-0102\nH3-T-0102 H1-Z-0102 H1-T-012 H1-T-01023 XH1-T-0102 H1-T-0102-1 h1-t-0102";
            List<string> got = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(text)) got.Add(m.Value);
            string want = "H1-X-0101|H1-X-0103|H1-P-0101-05|H2-F-0202-01|H1-T-0102|H1-T-0102";
            bool okPat = string.Join("|", got.ToArray()) == want;
            bool okStat = equip.Get("H1-X-0101") != null && equip.Get("H1-X-0101").Status == EquipStatus.Sterilisiert && equip.Get("H9-X-0000") == null
                && new StatusColors().Get(EquipStatus.Sterilisiert) == 0xFF0000;
            Catalog cat = store.GetCatalog();
            EquipAssign ok1 = new EquipAssign(); ok1.Short = "TFF"; ok1.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); ok1.Fallback = "H1-X-0103";
            EquipAssign alt = new EquipAssign(); alt.Short = "TFF"; alt.Alternate = true; alt.Choices.Add(new EquipChoice("TFF", "H1-X-0101")); alt.Choices.Add(new EquipChoice("TFF", "H1-X-9999"));
            EquipAssign fb = new EquipAssign(); fb.Short = "Pumpe"; fb.Choices.Add(new EquipChoice("Pumpe", "H1-P-0101-05")); fb.Fallback = "H1-P-0999-05";
            EquipAssign free = new EquipAssign(); free.Short = "PT"; free.Choices.Add(new EquipChoice("PT", "H1-T-0999"));
            bool okVal = EquipRow.Unvalidated(ok1, cat).Count == 0 && string.Join(",", EquipRow.Unvalidated(alt, cat).ToArray()) == "H1-X-9999"
                && string.Join(",", EquipRow.Unvalidated(fb, cat).ToArray()) == "H1-P-0999-05" && string.Join(",", EquipRow.Unvalidated(free, cat).ToArray()) == "H1-T-0999";
            sb.AppendLine("Equipment: Nummern erkannt=" + Ok(okPat) + " (" + got.Count + " von 6) Status/Farbe=" + Ok(okStat) + " Wechsel/Ausweich geprüft=" + Ok(okVal));
        }

        CellData Cell(int row, int col)
        {
            foreach (CellData x in view.Cells) if (x.RowIndex == row && x.Col == col) return x;
            return null;
        }

        // Lasttest wie ein PC: n-mal eigene Bloecke, einen gemeinsamen Zaehler (lesen-aendern-schreiben mit Versionspruefung)
        // und einen gemeinsamen Block (Zeile anhaengen, bei Konflikt zusammenfuehren wie der Editor) speichern.
        public static string Stress(string dir, string name, int n)
        {
            Directory.CreateDirectory(dir);
            FileStore fs = new FileStore(dir, false, null, 0);
            if (fs.Offline) return name + ": OFFLINE " + fs.Problem;
            File.WriteAllText(Path.Combine(dir, "bereit_" + name), "");   // beide Prozesse gleichzeitig loslaufen lassen (echte Konkurrenz)
            for (int k = 0; k < 600 && Directory.GetFiles(dir, "bereit_*").Length < 2; k++) System.Threading.Thread.Sleep(50);
            RowInfo own = fs.GetRows()[name == "A" ? 0 : 1], shared = fs.GetRows()[2];
            DateTime d0 = new DateTime(2026, 1, 5), dc = new DateTime(2026, 1, 1), dm = new DateTime(2026, 1, 2);
            User u = new User(); u.Login = name;
            int conflicts = 0, busy = 0;
            Random rnd = new Random(name.GetHashCode());   // "Bearbeitungszeit" zwischen Lesen und Speichern: so entstehen echte Konflikte
            for (int i = 0; i < n; i++)
            {
                Entry e = new Entry(); e.Content = name + " " + i;
                Retry(delegate { return Audit.Save(fs, d0.AddDays(i), own, u, e); }, ref busy);
                Retry(delegate
                {
                    Entry c = fs.Get(dc, shared.Id) ?? new Entry();
                    int v; int.TryParse(c.Content, out v); c.Content = (v + 1).ToString();
                    System.Threading.Thread.Sleep(rnd.Next(0, 30));
                    if (fs.Save(dc, shared.Id, c)) return true;
                    conflicts++; return false;
                }, ref busy);
                Entry bas = fs.Get(dm, shared.Id) ?? new Entry();
                Entry mine = bas.Clone(); mine.Content = (bas.Content.Length > 0 ? bas.Content + "\n" : "") + name + i;
                System.Threading.Thread.Sleep(rnd.Next(0, 30));
                Retry(delegate
                {
                    if (Audit.Save(fs, dm, shared, u, mine)) return true;
                    Entry latest = fs.Get(dm, shared.Id) ?? new Entry();
                    mine = Conflict.Merge(bas, mine, latest, new List<string>()); bas = latest;
                    conflicts++; return false;
                }, ref busy);
            }
            return name + ": " + n + " Durchgänge, Konflikte=" + conflicts + ", Sperre belegt=" + busy;
        }

        delegate bool Attempt();
        static void Retry(Attempt a, ref int busy)
        {
            for (int k = 0; k < 400; k++)
                try { if (a()) return; }
                catch (IOException) { busy++; System.Threading.Thread.Sleep(50); }   // Sperre gerade belegt: wie ein Nutzer erneut versuchen
            throw new IOException("Lasttest: kein Speichern nach 400 Versuchen");
        }

        public static string StressCheck(string dir, int n)
        {
            FileStore fs = new FileStore(dir, true, null, 0);
            List<RowInfo> rows = fs.GetRows();
            DateTime d0 = new DateTime(2026, 1, 5);
            bool own = true;
            for (int i = 0; i < n; i++)
            {
                Entry a = fs.Get(d0.AddDays(i), rows[0].Id), b = fs.Get(d0.AddDays(i), rows[1].Id);
                own &= a != null && a.Content == "A " + i && b != null && b.Content == "B " + i;
            }
            Entry cnt = fs.Get(new DateTime(2026, 1, 1), rows[2].Id), m = fs.Get(new DateTime(2026, 1, 2), rows[2].Id);
            List<string> lines = m == null ? new List<string>() : new List<string>(m.Content.Split('\n'));
            bool merged = lines.Count == 2 * n;
            for (int i = 0; i < n; i++) merged &= lines.Contains("A" + i) && lines.Contains("B" + i);
            bool counter = cnt != null && cnt.Content == (2 * n).ToString();
            return "Mehrplatz-Prozesse: eigene Blöcke=" + Ok(own) + " Zähler=" + Ok(counter) + " (" + (cnt == null ? "-" : cnt.Content) + ")"
                + " Zusammenführen=" + Ok(merged) + " (" + lines.Count + " Zeilen) Offline=" + Ok(!fs.Offline);
        }
    }
}
#endif
