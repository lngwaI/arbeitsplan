using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
// Zeilenumbrueche in Texten immer "\n".
namespace Arbeitsplan.Core
{
    public partial class MemoryStore : IDataStore
    {
        // Erfundene Testdaten (Repo ist oeffentlich; keine echten Nummern/Namen). Kuerzel passen zu den Testnutzern.
        public static MemoryStore Sample(DateTime monday)
        {
            MemoryStore s = new MemoryStore();
            int suite1 = s.IdOf("Suite 1"), mix = s.IdOf("Mix / Split"), cc = s.IdOf("Raum CC");
            string[] a = {
                "Lauf 22040030\nTFF: H1-X-0101\nPumpe: H1-P-0101-05\nPT: H1-T-0102\nSkid: H1-X-0103 (H1-F-0103-04)\n\nSplitten\nTank 1: H1-T-0111 (800L)",
                "Probenahme Charge 4711 – Öl-Wechsel? 5 % (ü ö ä ß) @Tank \"A\"!\nH1-P-0101-05, H1-T-0112",
                "Wartung Abfüllung (extern ab 08:00).\nTägliche WFI Probe ziehen\nLKZ MESSEN",
                "Lauf 22040031 (nach Lauf 33070007)\nSkid: H1-X-0103, Tank: H2-T-0201",
                "Reinigung und Rüsten\nH1-T-0113, H2-F-0202-01",
                "Wochenendbetrieb: Überwachung", "Wochenendbetrieb: Überwachung" };
            string[][] p = {
                new[] { "MUMA, WEPA, BEER", "ABCD, EFGH, IJKL", "" }, new[] { "MUMA, WEPA", "BEER, ABCD", "EFGH" }, new[] { "IJKL, MNOP", "", "" },
                new[] { "QRST, UVWX", "YZAB, CDEF", "GÜAL, SÖNß" }, new[] { "KLMN, OPQR", "STUV, WXYZ", "" }, new[] { "ABCD", "EFGH", "IJKL" }, new[] { "ABCD", "EFGH", "IJKL" } };
            for (int d = 0; d < 7; d++)
            {
                Entry e = new Entry(); e.Content = a[d]; e.People = p[d]; e.Lkz = d == 0 || d == 3;
                if (d == 2)   // Beispiel manuelle Formatierung: rot, fett, groesser (Erinnerung)
                {
                    FormatSpan sp = new FormatSpan(); sp.Start = e.Content.IndexOf("LKZ"); sp.Length = 10; sp.Rgb = 0xC00000; sp.Bold = true; sp.Size = 11;
                    e.Spans.Add(sp);
                }
                if (d == 1)   // Beispiel Notizzeilen (kursiv, zaehlen nicht als Inhalt)
                {
                    e.Content += "\nProbe bis 10:00 zur QC bringen\nBei Abweichung Schichtleiter informieren";
                    int nl = e.Content.Split('\n').Length;
                    Notes.SetNote(e, nl - 2, nl - 1, true);
                }
                s.Save(monday.AddDays(d), suite1, e);
            }
            // Folgewoche (fuer die Testansicht "2 Wochen")
            string[] nxt = { "Rüsten Suite 1, Filter prüfen", "Wartung Pumpe H1-P-0101-05", "Probenahme laut Plan" };
            for (int d = 0; d < nxt.Length; d++)
            {
                Entry e = new Entry(); e.Content = nxt[d]; e.People = new[] { "ABCD, EFGH", "IJKL", "" };
                s.Save(monday.AddDays(7 + d), suite1, e);
            }
            // viele Kuerzel in einer Schicht: Feld waechst, alle Spalten der Zeile bleiben gleich hoch
            Entry m = s.Get(monday, suite1);
            m.People[0] = "MUMA, WEPA, BEER, ABCD, EFGH, IJKL, MNOP, QRST, UVWX, YZAB, CDEF, GHIJ, KLMN, OPQR, STUV, WXYZ";
            s.Save(monday, suite1, m);
            for (int d = 0; d < 5; d++)
            {
                Entry e = new Entry(); e.Content = "Schulung (Raum 2.14), 13:00"; e.People = new[] { "alle", "", "" };
                s.Save(monday.AddDays(d), mix, e);
            }
            Entry c = new Entry(); c.Content = "Raumreinigung"; c.People = new[] { "BEER, WEPA", "", "" };
            s.Save(monday.AddDays(2), cc, c);
            Entry cn = new Entry(); cn.Content = "Schlüssel liegt beim Pförtner"; Notes.SetNote(cn, 0, 0, true);   // Block nur mit Notiz = leer
            s.Save(monday.AddDays(4), cc, cn);
            // Schichtmodus: Raum CC = Tagesaufgabe ("Erledigt von"), Versand = nur Frueh/Spaet
            List<RowInfo> rs = s.GetRows();
            foreach (RowInfo r in rs) { if (r.Name == "Raum CC") r.Mode = ShiftMode.Task; if (r.Name == "Versand") r.Mode = ShiftMode.FS; }
            s.SaveStructure(rs);
            Entry v = new Entry(); v.Content = "Versand Kühlware"; v.People = new[] { "WEPA", "BEER", "" };
            s.Save(monday.AddDays(1), s.IdOf("Versand"), v);
            // Puffer: Herstellung mobil (Tagesprobe automatisch), stationaer mit Tagesprobe, ohne Angabe, beides an einem Tag
            string[][] pf = { new[] { "Herstellung Puffer A", "Puffer A|mobil", "" }, new[] { "Herstellung Puffer B", "Puffer B|stationaer", "PROBE" }, new[] { "Puffer B abfüllen", "", "" },
                              new[] { "Herstellung Puffer C, D und E", "Puffer C|mobil|Puffer D|stationaer|Puffer E|mobil", "PROBE" } };
            for (int d = 0; d < pf.Length; d++)
            {
                Entry e = new Entry(); e.Content = pf[d][0]; e.People = new[] { "WEPA", "", "" };
                string[] bp = pf[d][1].Split('|');
                for (int k = 0; k + 1 < bp.Length; k += 2) BufferSet.Add(e.Buffers, bp[k], bp[k + 1], "");
                if (d == 3) { e.Buffers[0].Text = "Ansatz 500 L\nWFI: H1-T-0102"; e.Buffers[1].Text = "Flasche 5 L"; }
                if (pf[d][2].Length > 0) e.Marks.Add(pf[d][2]);
                s.Save(monday.AddDays(d), s.IdOf("Puffer"), e);
            }
            // Freeze / Thaw: Bag-Serie (C3 geschaetzt, C4 verspaetet) und Tank-Freeze mit erfundenen Nummern
            int fz = s.IdOf("Freeze / Thaw");
            Entry fb = new Entry(); fb.People = new[] { "WEPA", "", "" };
            FreezeItem bag = new FreezeItem(); bag.Cycles = 8; bag.MainRef = "22050010"; bag.SubNo = "22059010"; bag.Tanks = "H2-T-0251";
            bag.Start1to3[0] = monday.AddDays(1).AddHours(12); bag.Start1to3[1] = monday.AddDays(1).AddHours(13); bag.Ist[4] = monday.AddDays(2).AddHours(2);
            FreezeSet.Add(fb.Freezes, bag);
            s.Save(monday.AddDays(1), fz, fb);
            Entry ft = new Entry(); ft.People = new[] { "", "BEER", "" };
            FreezeItem tank = new FreezeItem(); tank.Art = FreezeArt.Tank; tank.Anlage = 2; tank.Kreislauf = 1; tank.MainRef = "22050011"; tank.Tanks = "H2-T-0252";
            tank.Start1to3[0] = monday.AddDays(3).AddHours(8);
            FreezeSet.Add(ft.Freezes, tank);
            s.Save(monday.AddDays(3), fz, ft);
            WeekMark gmp = new WeekMark(); gmp.Monday = monday; gmp.Key = "GMP1"; gmp.Done = true; gmp.At = monday.AddHours(9.2); gmp.By = "MUMA";
            s.SaveWeekMark(gmp, null);
            return s;
        }
    }
}
