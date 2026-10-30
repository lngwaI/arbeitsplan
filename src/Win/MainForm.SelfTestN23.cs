#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests N2 (Puffer "hergestellt": Datenfeld, Merge, Protokoll, Tages-/Wochenprobe, Einstellung, Wochenansicht) und
    // N3 (Equipment-Liste mit Anzahl, Kampagne mit Puffern ueber BufferItem.LaufRef). Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        // Testschalter --made: Einstellung "Puffer als hergestellt abhaken" an, Puffer E (Donnerstag, mobil) hergestellt (fuer --shot)
        public void TestMade()
        {
            DutyConfig dc = store.GetDuties(); dc.BufMade = true; store.SaveDuties(dc);
            int pr = RowOf("Puffer");
            Entry e = store.Get(start.AddDays(3), rowInfo[pr].Id);
            if (e != null && e.Buffers.Count > 2) { e.Buffers[2].Made = true; store.Save(start.AddDays(3), rowInfo[pr].Id, e); }
            Rebuild(false);
        }

        static BufferItem NB(string id, string typ, string name, bool made)
        {
            BufferItem b = new BufferItem(); b.Id = id; b.Typ = typ; b.Name = name; b.Made = made; return b;
        }

        // speichern ueber den aktuellen Stand (Version uebernehmen)
        static void Sv(IDataStore st, DateTime d, int row, Entry e)
        {
            Entry cur = st.Get(d, row); if (cur != null) e.Version = cur.Version;
            st.Save(d, row, e);
        }

        static Entry NE(params BufferItem[] bs) { Entry e = new Entry(); e.Buffers.AddRange(bs); return e; }

        void SelfTestN23(StringBuilder sb)
        {
            try { SelfTestN2Core(sb); } catch (Exception ex) { sb.AppendLine("N2 Modell: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { SelfTestN2Ui(sb); } catch (Exception ex) { sb.AppendLine("N2 Wochenansicht: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            try { SelfTestN3(sb); } catch (Exception ex) { sb.AppendLine("N3 Equipment-Liste: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
        }

        void SelfTestN2Core(StringBuilder sb)
        {
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime mon = D(2026, 3, 2), tue = mon.AddDays(1), wed = mon.AddDays(2), thu = mon.AddDays(3);

            // (a) XML-Rundweg, alter Bestand, Formatversion
            Sv(ms, tue, puf, NE(NB("p1", "mobil", "A", true), NB("p2", "mobil", "B", false), NB("p3", "stationaer", "C", true)));
            DutyConfig on = ms.GetDuties(); on.BufMade = true; ms.SaveDuties(on);
            byte[] xml = Backup.ToXml(ms.Snapshot());
            string xs = Encoding.UTF8.GetString(xml);
            StoreData x = Backup.FromXml(xml);
            Entry xe = null; foreach (StoredEntry se in x.Entries) if (se.Day == tue && se.RowId == puf) xe = se.Data;
            bool okXml = xe != null && xe.Buffers.Count == 3 && xe.Buffers[0].Made && !xe.Buffers[1].Made && xe.Buffers[2].Made && x.Duties.BufMade && xs.Contains("fertig=\"1\"") && xs.Contains("puffer-fertig=\"1\"");
            StoreData xo = Backup.FromXml(Encoding.UTF8.GetBytes(xs.Replace(" fertig=\"1\"", "").Replace(" puffer-fertig=\"1\"", "")));
            Entry oe = null; foreach (StoredEntry se in xo.Entries) if (se.Day == tue && se.RowId == puf) oe = se.Data;
            bool okOld = oe != null && !oe.Buffers[0].Made && !oe.Buffers[2].Made && !xo.Duties.BufMade && new DutyConfig().BufMade == false && Backup.FormatVersion == 5;
            bool okKey = NB("p1", "mobil", "A", true).Key() != NB("p1", "mobil", "A", false).Key() && !NB("p1", "mobil", "A", true).SameValue(NB("p1", "mobil", "A", false)) && NB("p1", "mobil", "A", true).SameCore(NB("p1", "mobil", "A", false));
            sb.AppendLine("N2 hergestellt: XML-Rundweg=" + Ok(okXml) + " alter Bestand ohne Attribut=" + Ok(okOld) + " Schlüssel/Vergleich=" + Ok(okKey));

            // (b) Merge: einseitig geaendert gewinnt, Made und Name getrennt, beide gleich = true
            Entry b0 = NE(NB("p1", "mobil", "A", false)), mine = NE(NB("p1", "mobil", "A", true)), theirs = NE(NB("p1", "mobil", "Neu", false));
            List<string> cl = new List<string>();
            Entry m1 = Conflict.Merge(b0, mine, theirs, cl);
            bool okM1 = m1.Buffers.Count == 1 && m1.Buffers[0].Made && m1.Buffers[0].Name == "Neu" && !cl.Contains("Puffer");
            Entry m2 = Conflict.Merge(b0, theirs, mine, new List<string>());   // umgekehrt
            bool okM2 = m2.Buffers.Count == 1 && m2.Buffers[0].Made && m2.Buffers[0].Name == "Neu";
            Entry m3 = Conflict.Merge(b0, NE(NB("p1", "mobil", "A", true)), NE(NB("p1", "mobil", "A", true)), new List<string>());
            Entry m4 = Conflict.Merge(NE(NB("p1", "mobil", "A", true)), NE(NB("p1", "mobil", "A", false)), NE(NB("p1", "mobil", "A", true)), new List<string>());   // ich nehme zurueck, andere aendert nichts
            bool okM3 = m3.Buffers.Count == 1 && m3.Buffers[0].Made && m4.Buffers.Count == 1 && !m4.Buffers[0].Made;
            sb.AppendLine("N2 hergestellt: Merge getrennt vom Inhalt=" + Ok(okM1 && okM2) + " beide gleich / zurückgenommen=" + Ok(okM3));

            // (c) Protokoll und Recht (Made-Aenderung = "Text aendern" wie der LKZ-Haken)
            Entry before = NE(NB("p1", "mobil", "A", false), NB("p2", "mobil", "B", false)), after = NE(NB("p1", "mobil", "A", false), NB("p2", "mobil", "B", true));
            AuditEntry ae = new AuditEntry(); ae.Before = before; ae.After = after;
            AuditEntry ar = new AuditEntry(); ar.Before = after; ar.After = before;
            bool ad, rm, ch; BufferSet.Diff(before.Buffers, after.Buffers, out ad, out rm, out ch);
            bool okAud = Audit.Summary(ae).StartsWith("Puffer 2 hergestellt") && Audit.Summary(ar).Contains("hergestellt zurückgenommen") && BufferSet.DisplayLines(after.Buffers, false)[1].EndsWith("hergestellt")
                && BufferSet.MadeChange(before, before) == "";
            bool okRight = ch && !ad && !rm;
            sb.AppendLine("N2 hergestellt: Protokolltext=" + Ok(okAud) + " Recht (nur Änderung, kein Hinzufügen/Löschen)=" + Ok(okRight));

            // (d) Tagesprobe / Wochenprobe
            DateTime today = D(2026, 3, 20);
            CheckDef probe = ms.GetDuties().Defs.Find(delegate(CheckDef d) { return d.Key == "PROBE"; }), wfiw = ms.GetDuties().Defs.Find(delegate(CheckDef d) { return d.Key == "WFIW"; });
            DutyConfig off = ms.GetDuties(); off.BufMade = false;
            Sv(ms, tue, puf, NE(NB("p1", "mobil", "A", false), NB("p2", "mobil", "B", false)));            // Di: zwei mobile, keiner hergestellt
            Sv(ms, wed, puf, NE(NB("p1", "mobil", "A", false), NB("p2", "mobil", "B", true)));             // Mi: der zweite (nicht der obere) hergestellt
            Sv(ms, thu, puf, NE(NB("p1", "stationaer", "S", false)));                                       // Do: stationaer, nicht hergestellt
            bool okOffDay = CheckEval.Day(ms, probe, tue, today, off) == DayState.Auto && CheckEval.Day(ms, probe, wed, today, off) == DayState.Auto;   // ohne Feature: wie bisher
            bool okOffWeek = CheckEval.Day(ms, wfiw, tue, today, off) == DayState.Auto && CheckEval.Week(ms, wfiw, tue, today, off) == DayState.Done;
            bool okOnDay = CheckEval.Day(ms, probe, tue, today, on) == DayState.Missing && CheckEval.Day(ms, probe, wed, today, on) == DayState.Auto
                && CheckEval.Day(ms, probe, tue, mon, on) == DayState.Due;   // heute = Montag: Dienstag liegt in der Zukunft = neutral "faellig"
            bool okOnWeek = CheckEval.Day(ms, wfiw, tue, today, on) == DayState.WeekOpen && CheckEval.Week(ms, wfiw, tue, today, on) == DayState.Missing;
            Entry te = ms.Get(tue, puf).Clone(); te.Marks.Add("PROBE"); Sv(ms, tue, puf, te);                // manuell abhaken bleibt moeglich
            bool okManual = CheckEval.Day(ms, probe, tue, today, on) == DayState.Done;
            Sv(ms, thu, puf, NE(NB("p1", "stationaer", "S", true)));
            bool okSt = CheckEval.Day(ms, wfiw, tue, today, on) == DayState.Auto && CheckEval.Week(ms, wfiw, tue, today, on) == DayState.Done
                && CheckEval.Day(ms, probe, thu, today, on) == DayState.Missing;                              // stationaer hergestellt: Wochenprobe ja, Tagesprobe der Do nein
            // mobil hergestellt erledigt die Wochenprobe nicht; stationaer hergestellt nicht die Tagesprobe
            DateTime mon2 = mon.AddDays(14);
            Sv(ms, mon2.AddDays(1), puf, NE(NB("p1", "mobil", "A", true)));
            bool okSep = CheckEval.Day(ms, probe, mon2.AddDays(1), today, on) == DayState.Auto && CheckEval.Day(ms, wfiw, mon2.AddDays(1), today, on) == DayState.WeekOpen
                && CheckEval.Week(ms, wfiw, mon2, today, on) == DayState.WeekOpen;   // Sonntag 22.03. noch nicht erreicht
            // gemischter Tag (mobil hergestellt + stationaer offen): EIN hergestellter mobiler genuegt
            Sv(ms, mon2.AddDays(2), puf, NE(NB("p1", "stationaer", "S", false), NB("p2", "mobil", "M", true)));
            bool okMix = CheckEval.Day(ms, probe, mon2.AddDays(2), today, on) == DayState.Auto;
            // Namen: nur mit Feature, eigene Namen bleiben
            CheckDef own = probe.Clone(); own.Name = "Eigene Probe";
            bool okName = MadeRules.Display(probe, true).Name == MadeRules.DayName && MadeRules.Display(wfiw, true).Name == MadeRules.WeekName && MadeRules.Display(probe, false).Name == probe.Name
                && MadeRules.Display(own, true).Name == "Eigene Probe" && MadeRules.Display(DutyConfig.Default().Rounds[4], true).Name == MadeRules.RoundName
                && MadeRules.Display(DutyConfig.Default().Rounds[4], false).Name == "WFI-Probenahme";
            sb.AppendLine("N2 Tagesprobe: Feature aus = wie bisher=" + Ok(okOffDay && okOffWeek) + " an: ein Puffer genügt, sonst fehlt/fällig=" + Ok(okOnDay) + " manuell=" + Ok(okManual) + " gemischter Tag=" + Ok(okMix));
            sb.AppendLine("N2 Wochenprobe: an, nicht hergestellt = offen/fehlt=" + Ok(okOnWeek) + " stationär hergestellt=" + Ok(okSt) + " getrennt von Tagesprobe=" + Ok(okSep) + " Namen=" + Ok(okName));

            // (e) Einstellung speichern / laden
            ms.SaveDuties(on);
            bool okSet = ms.GetDuties().BufMade && ms.GetDuties().Clone().BufMade;
            ms.SaveDuties(off);
            okSet &= !ms.GetDuties().BufMade;
            sb.AppendLine("N2 Einstellung speichern/laden: " + Ok(okSet));
        }

        void SelfTestN2Ui(StringBuilder sb)
        {
            if (systemPc) { sb.AppendLine("N2 Wochenansicht: übersprungen (Systemrechner)"); return; }
            DateTime keep = start;
            bool keepOn = store.GetDuties().BufMade;
            try
            {
                int pr = RowOf("Puffer");
                if (pr < 0) { sb.AppendLine("N2 Wochenansicht: Zeile Puffer fehlt"); return; }
                int rid = rowInfo[pr].Id;
                DateTime wk = D(2035, 2, 5);   // Montag, Zukunft: offene Tagesprobe = "faellig"
                Entry e = new Entry(); e.Content = "Herstellung";
                BufferSet.Add(e.Buffers, "Lösung A", "mobil", "Tank H1-T-0101"); BufferSet.Add(e.Buffers, "Lösung B", "mobil", ""); BufferSet.Add(e.Buffers, "Lösung C", "stationaer", "");
                Sv(store, wk.AddDays(1), rid, e);
                DutyConfig dc = store.GetDuties(); dc.BufMade = false; store.SaveDuties(dc);
                start = wk; Rebuild(true); Expand(pr);
                CellData c = CellOf(pr, 1);
                int ip = view.Defs[pr].FindIndex(delegate(CheckDef d) { return d.Key == "PROBE"; }), iw = view.Defs[pr].FindIndex(delegate(CheckDef d) { return d.Key == "WFIW"; });
                bool okOff = c != null && !view.BufMade && c.BufParts.Count == 3 && c.BufParts[0].Box.IsEmpty && c.States[ip] == DayState.Due &&   // gemischter Tag (mobil + stationaer) ist ohne Feature nie automatisch
                     view.Defs[pr][ip].Name == "Tagesprobe (täglich)";
                dc.BufMade = true; store.SaveDuties(dc);
                Rebuild(true); Expand(pr); c = CellOf(pr, 1);
                bool okOn = c != null && view.BufMade && c.BufParts.Count == 3 && !c.BufParts[0].Box.IsEmpty && !c.BufParts[1].Made && c.States[ip] == DayState.Due && c.States[iw] == DayState.WeekOpen
                    && view.Defs[pr][ip].Name == MadeRules.DayName && view.Defs[pr][iw].Name == MadeRules.WeekName;
                // Klick auf den Haken von Puffer B (der zweite): hergestellt -> Tagesprobe gezogen (Auto), Wochenprobe unberuehrt
                if (okOn) view.TestClick(view.MadePoint(c, 1));
                c = CellOf(pr, 1);
                Entry s1 = store.Get(wk.AddDays(1), rid);
                bool okClick = s1 != null && s1.Buffers.Count == 3 && !s1.Buffers[0].Made && s1.Buffers[1].Made && !s1.Buffers[2].Made && s1.Content == "Herstellung"
                    && c != null && c.BufParts[1].Made && c.States[ip] == DayState.Auto && c.States[iw] == DayState.WeekOpen;
                List<AuditEntry> au = store.GetAudit(wk.AddDays(1), rid);
                bool okAud = au.Count > 0 && Audit.Summary(au[au.Count - 1]).StartsWith("Puffer 2 hergestellt");
                // zurueck: nicht hergestellt -> wieder offen; der stationaere Puffer erledigt die Wochenprobe
                if (okClick) view.TestClick(view.MadePoint(c, 1));
                c = CellOf(pr, 1);
                bool okBack = c != null && !c.BufParts[1].Made && c.States[ip] == DayState.Due;
                if (okBack) view.TestClick(view.MadePoint(c, 2));
                c = CellOf(pr, 1);
                bool okWeek = c != null && c.BufParts[2].Made && c.States[iw] == DayState.Auto && c.States[ip] == DayState.Due;
                bool okTip = c != null && c.BufParts[2].MadeTip.Contains("Wochenprobe") && c.BufParts[2].MadeTip.Contains("WFI-Rundgang") && c.BufParts[0].MadeTip.Contains("Tagesprobe");
                sb.AppendLine("N2 Wochenansicht: Feature aus = ohne Haken, wie bisher=" + Ok(okOff) + " an: Haken, Probe offen=" + Ok(okOn) + " Klick speichert, Tagesprobe gezogen=" + Ok(okClick)
                    + " Protokoll=" + Ok(okAud) + " zurücknehmen=" + Ok(okBack) + " stationär = Wochenprobe=" + Ok(okWeek) + " Tooltips=" + Ok(okTip));
            }
            finally
            {
                DutyConfig dc = store.GetDuties(); dc.BufMade = keepOn; store.SaveDuties(dc);
                start = keep; Rebuild(true);
            }
        }

        void SelfTestN3(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows();
            DateTime d0 = new DateTime(2031, 3, 3);
            Put(s, d0, rows[0].Id, "Tank H1-T-0101, Pumpe H1-P-0202 und H1-T-0101");   // 0101 zweimal im selben Block = 1
            Put(s, d0, rows[1].Id, "Skid H1-X-0303 und H1-T-0101");
            Put(s, d0.AddDays(1), rows[0].Id, "H1-P-0202 wieder");
            List<KeyValuePair<string, int>> cnt = EquipExport.Count(s, d0, d0.AddDays(1));
            string txt = EquipExport.FormatCounts(cnt);
            bool okCnt = cnt.Count == 3 && cnt[0].Key == "H1-T-0101" && cnt[0].Value == 2 && cnt[1].Key == "H1-P-0202" && cnt[1].Value == 2 && cnt[2].Key == "H1-X-0303" && cnt[2].Value == 1
                && txt == "H1-T-0101\t2\r\nH1-P-0202\t2\r\nH1-X-0303\t1" && EquipExport.Format(EquipExport.Collect(s, d0, d0.AddDays(1))) == "H1-T-0101\r\n\r\nH1-P-0202\r\n\r\nH1-X-0303";

            // Kampagne: Laeufe 13570030 (Tag 10), 13570031 (Tag 12); Puffer fuer Lauf 1 schon 6 Tage vor Lauf 1 (vor dem Vorlauf), Puffer ohne Lauf-Angabe 1 Tag davor
            int pr = PufferSetup.Row(s.GetRows()).Id;
            Put(s, d0.AddDays(10), rows[0].Id, "Lauf 13570030 Tank H1-T-0777");
            Put(s, d0.AddDays(12), rows[0].Id, "Lauf 13570031 Tank H1-T-0778");
            Entry early = new Entry(); BufferSet.Add(early.Buffers, "Puffer A", "mobil", "Tank H1-T-0901").LaufRef = 1; BufferSet.Add(early.Buffers, "Puffer B", "stationaer", "Tank H1-T-0902").LaufRef = 2;
            Sv(s, d0.AddDays(4), pr, early);                                                                  // Lauf 1 (Tag 10): 6 Tage vorher; Lauf 2 (Tag 12): 8 Tage vorher
            Entry near = new Entry(); BufferSet.Add(near.Buffers, "Puffer C", "mobil", "Tank H1-T-0903"); Sv(s, d0.AddDays(9), pr, near);   // ohne Lauf-Angabe, 1 Tag vorher
            Entry other = new Entry(); BufferSet.Add(other.Buffers, "Puffer D", "mobil", "Lauf 99990030 Tank H1-T-0904").LaufRef = 1; Sv(s, d0.AddDays(5), pr, other);   // anderes Produkt
            Entry late = new Entry(); BufferSet.Add(late.Buffers, "Puffer E", "mobil", "Tank H1-T-0905").LaufRef = 1; Sv(s, d0.AddDays(11), pr, late);                    // nach Lauf 1: nicht fuer Lauf 1 (Lauf 1 war Tag 10)
            List<EquipExport.CampaignSpan> cs = EquipExport.Campaigns(s, d0.AddDays(-30), d0.AddDays(60), "1357");
            EquipExport.CampaignSpan c = null; foreach (EquipExport.CampaignSpan x in cs) if (x.FirstNo == 30) c = x;
            DateTime f1, t1, f2, t2;
            c = c ?? new EquipExport.CampaignSpan();
            List<string> ids1 = EquipExport.Ids(EquipExport.CountCampaign(s, c, 1, 0, out f1, out t1));
            List<string> ids0 = EquipExport.Ids(EquipExport.CountCampaign(s, c, 0, 0, out f2, out t2));
            bool okRef = ids1.Contains("H1-T-0901") && ids1.Contains("H1-T-0902") && ids1.Contains("H1-T-0903") && ids1.Contains("H1-T-0777") && ids1.Contains("H1-T-0778")
                && !ids1.Contains("H1-T-0904") && ids1.Contains("H1-T-0905") && f1 == d0.AddDays(4) && t1 == d0.AddDays(12);   // 0905 liegt im Zeitraum (Tag 11) und wird als Block des Zeitraums erfasst
            bool okNoRef = !ids0.Contains("H1-T-0903") && ids0.Contains("H1-T-0901") && ids0.Contains("H1-T-0902") && f2 == d0.AddDays(4);   // ohne Vorlauf: nur der Puffer MIT Lauf-Angabe kommt dazu
            List<string> viaOld = EquipExport.CollectCampaign(s, c, 1, 0);
            bool okOld = viaOld.Count == ids1.Count;
            sb.AppendLine("N3 Equipment-Liste: mit Anzahl=" + Ok(okCnt) + " Kampagne mit Puffer vor Lauf 1 über Lauf-Angabe=" + Ok(okRef) + " Vorlauf nur als Fallback=" + Ok(okNoRef) + " CollectCampaign gleich=" + Ok(okOld));
        }
    }
}
#endif
