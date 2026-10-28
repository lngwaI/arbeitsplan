#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Block G: mehrere Puffer-Varianten je Tag + Wochenende/Feiertag (G0).
    partial class MainForm
    {
        static Entry EntryIn(StoreData d, DateTime day, int row)
        {
            foreach (StoredEntry se in d.Entries) if (se.Day == day && se.RowId == row) return se.Data;
            return null;
        }

        void SelfTestG(StringBuilder sb)
        {
            SelfTestG0(sb);
            SelfTestG1(sb);
            SelfTestG2(sb);
        }

        // 2-Wochen-Ansicht: Pruefleisten-Zustaende und Rundgangsleiste je KW auch in der zweiten Woche (auch nach Neuaufbau)
        void SelfTestG2(StringBuilder sb)
        {
            bool had = week2 != null;
            SetWeeks(2);
            int n1 = 0, n2 = 0;
            foreach (CellData c in view.Cells) if (c.States != null && c.States.Length > 0) n1++;
            foreach (CellData c in week2.View.Cells) if (c.States != null && c.States.Length > 0) n2++;
            int r2 = week2.View.Rounds.Count;
            bool okRounds = r2 > 0 && week2.View.Rounds[0].Monday == IsoWeek.Monday(start.AddDays(7));
            Rebuild(false);
            int n3 = 0;
            foreach (CellData c in week2.View.Cells) if (c.States != null && c.States.Length == week2.View.Defs[c.RowIndex].Count && c.States.Length > 0) n3++;
            bool okRe = n3 == n2 && week2.View.Rounds.Count == r2;
            if (!had) SetWeeks(1);
            sb.AppendLine("2-Wochen: Prüfleiste Zellen " + n2 + "/" + n1 + "=" + Ok(n2 > 0 && n2 == n1) + " Rundgänge je KW (" + r2 + ")=" + Ok(okRounds) + " nach Neuaufbau=" + Ok(okRe));
        }

        // Migration alter Bestaende: ohne Recht nichts, mit PackageAdmin genau einmal, Vorhandenes bleibt
        static void SelfTestG1(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            List<RowInfo> rows = s.GetRows(); PufferSetup.Row(rows).Variants = ""; s.SaveStructure(rows);
            s.SaveDuties(DutyConfig.Default());                                   // alter Bestand: keine Puffer-Pflichtpunkte
            User leser = new User(); leser.Rights.Add(Perm.Read);
            User admin = new User(); admin.Rights.Add(Perm.Read); admin.Rights.Add(Perm.PackageAdmin);
            bool needed = PufferSetup.Needed(s.GetRows(), s.GetDuties());
            bool noRight = !PufferSetup.Apply(s, leser) && !PufferSetup.Apply(s, null) && s.GetDuties().Defs.Count == 0 && PufferSetup.Row(s.GetRows()).Variants == "";
            bool first = PufferSetup.Apply(s, admin);
            DutyConfig d1 = s.GetDuties();
            int pid = PufferSetup.Row(s.GetRows()).Id;
            bool okSet = first && d1.Defs.Count == 2 && d1.Defs.TrueForAll(delegate(CheckDef d) { return d.RowId == pid; })
                && PufferSetup.Row(s.GetRows()).Variants == "mobil|stationaer" && !PufferSetup.Needed(s.GetRows(), d1);
            bool again = !PufferSetup.Apply(s, admin) && s.GetDuties().Defs.Count == 2;
            // teilweise vorhanden: eigene Tagesprobe und eigene Varianten bleiben, nur die Wochen-WFI kommt dazu
            MemoryStore p = new MemoryStore();
            List<RowInfo> pr = p.GetRows(); PufferSetup.Row(pr).Variants = "mobil"; p.SaveStructure(pr);
            DutyConfig pc = DutyConfig.Default(); CheckDef own = new CheckDef(); own.Key = "PROBE"; own.Name = "Eigene Probe"; own.RowId = PufferSetup.Row(pr).Id; pc.Defs.Add(own); p.SaveDuties(pc);
            bool part = PufferSetup.Apply(p, admin);
            DutyConfig pd = p.GetDuties();
            bool okKeep = part && pd.Defs.Count == 2 && pd.Defs[0].Name == "Eigene Probe" && pd.Defs[0].Cond == "" && pd.Defs[1].Key == "WFIW" && PufferSetup.Row(p.GetRows()).Variants == "mobil";
            bool okNew = !PufferSetup.Needed(new MemoryStore().GetRows(), new MemoryStore().GetDuties());   // neue Bestaende haben die Standards schon
            sb.AppendLine("Puffer-Migration: nötig erkannt=" + Ok(needed) + " ohne Recht nichts=" + Ok(noRight) + " eingerichtet=" + Ok(okSet) + " idempotent=" + Ok(again)
                + " Vorhandenes bleibt=" + Ok(okKeep) + " neuer Bestand ohne Rückfrage=" + Ok(okNew));
        }

        static void SelfTestG0(StringBuilder sb)
        {
            MemoryStore s = new MemoryStore();
            int pid = PufferSetup.Row(s.GetRows()).Id;
            DutyConfig cfg = s.GetDuties();
            CheckDef probe = cfg.Defs.Find(delegate(CheckDef d) { return d.Key == "PROBE"; }), wfi = cfg.Defs.Find(delegate(CheckDef d) { return d.Key == "WFIW"; });
            if (probe == null || wfi == null) { sb.AppendLine("Puffer-Varianten: FEHLER Standard-Pflichtpunkte fehlen"); return; }
            DateTime w = D(2026, 6, 1), today = D(2026, 6, 10);   // KW23, Do 04.06. Fronleichnam; heute = Mi KW24
            s.Save(w, pid, E("Herstellung", false, "mobil"));
            s.Save(w.AddDays(1), pid, E("Herstellung", false, "mobil|stationaer"));
            s.Save(w.AddDays(2), pid, E("Herstellung", false, "stationaer|mobil", "PROBE"));   // andere Reihenfolge = gleiche Menge
            s.Save(w.AddDays(3), pid, E("Herstellung", false, "stationaer"));                  // Feiertag mit Herstellung
            s.Save(w.AddDays(6), pid, E("Herstellung", false, "mobil"));                       // So nur mobil
            s.Save(w.AddDays(13), pid, E("Herstellung", false, ""));                           // So KW24: Inhalt ohne Variante
            s.Save(w.AddDays(12), pid, E("Herstellung", false, "mobil|stationaer"));           // Sa KW24 (Zukunft)
            s.Save(D(2026, 5, 23), pid, E("Herstellung", false, "stationaer", "PROBE"));       // Sa KW21 gehakt
            object[][] cases = {
                new object[] { "Mo mobil = automatisch", w, DayState.Auto },
                new object[] { "Di mobil+stationär ohne Haken", w.AddDays(1), DayState.Missing },
                new object[] { "Mi mobil+stationär mit Haken", w.AddDays(2), DayState.Done },
                new object[] { "Do Feiertag stationär", w.AddDays(3), DayState.Missing },
                new object[] { "Fr ohne Angabe (Werktag)", w.AddDays(4), DayState.Missing },
                new object[] { "Sa ohne Herstellung", w.AddDays(5), DayState.NA },
                new object[] { "So nur mobil", w.AddDays(6), DayState.Auto },
                new object[] { "Sa Zukunft mobil+stationär", w.AddDays(12), DayState.Due },
                new object[] { "So Inhalt ohne Variante", w.AddDays(13), DayState.NA },
                new object[] { "Pfingstmontag ohne Herstellung", D(2026, 5, 25), DayState.NA },
                new object[] { "Sa stationär gehakt", D(2026, 5, 23), DayState.Done } };
            bool okDays = true;
            StringBuilder tab = new StringBuilder();
            foreach (object[] c in cases)
            {
                DayState got = CheckEval.Day(s, probe, (DateTime)c[1], today, cfg);
                bool ok = got == (DayState)c[2]; okDays &= ok;
                tab.AppendLine("  " + ((string)c[0]).PadRight(32) + ((DateTime)c[1]).ToString("dd.MM.") + " -> " + got + " (" + Ok(ok) + ")");
            }
            // Wochen-WFI: KW23 enthaelt stationaer (kombiniert) -> entfaellt; KW25 nur mobil, am So gehakt -> erledigt
            DateTime k25 = D(2026, 6, 15);
            s.Save(k25, pid, E("Herstellung", false, "mobil"));
            bool wfiOpen = CheckEval.Week(s, wfi, k25, today, cfg) == DayState.WeekOpen;
            s.Save(k25.AddDays(6), pid, E("", false, "", "WFIW"));
            bool okWfi = CheckEval.Week(s, wfi, w, today, cfg) == DayState.NA && CheckEval.Day(s, wfi, w.AddDays(5), today, cfg) == DayState.NA
                && wfiOpen && CheckEval.Week(s, wfi, k25, today, cfg) == DayState.Done && CheckEval.Day(s, wfi, k25.AddDays(6), today, cfg) == DayState.Done
                && CheckEval.Day(s, wfi, k25, today, cfg) == DayState.NotNeeded;
            // Speicherformat: mehrere Varianten im selben Attribut, alter Einzelwert bleibt gueltig
            StoreData x = Backup.FromXml(Backup.ToXml(s.Snapshot()));
            Entry e1 = EntryIn(x, w.AddDays(1), pid), e3 = EntryIn(x, w.AddDays(3), pid);
            bool okXml = e1 != null && e1.Variant == "mobil|stationaer" && e3 != null && e3.Variant == "stationaer"
                && VariantSet.Of("stationaer").Count == 1 && VariantSet.Of("stationaer|mobil|mobil").Count == 2;
            // Merge: Vereinigung, Entfernen einer Seite bleibt erhalten
            List<string> cl = new List<string>();
            Entry m1 = Conflict.Merge(E("x", false, ""), E("x", false, "mobil"), E("x", false, "stationaer"), cl);
            Entry m2 = Conflict.Merge(E("x", false, "mobil"), E("x", false, ""), E("x", false, "mobil|stationaer"), cl);
            bool okMerge = m1.Variant == "mobil|stationaer" && m2.Variant == "stationaer" && cl.Count == 0
                && VariantSet.Toggle("mobil", "stationaer") == "mobil|stationaer" && VariantSet.Toggle("mobil|stationaer", "mobil") == "stationaer"
                && VariantSet.Display("stationaer|mobil") == "mobil + stationaer";
            sb.AppendLine("Puffer-Varianten: Tagesprobe kombiniert/Wochenende/Feiertag=" + Ok(okDays) + " Wochen-WFI entfällt bei Kombination, So hakbar=" + Ok(okWfi)
                + " XML-Rundreise=" + Ok(okXml) + " Merge-Vereinigung=" + Ok(okMerge));
            sb.Append(tab.ToString());
        }
    }
}
#endif
