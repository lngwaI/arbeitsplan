#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P5: Export mit Produkten (Paketordner + Katalog), Geraete aus der Liste, "nicht erkannt", mehrtaegige Laeufe, Kalender.
    partial class MainForm
    {
        // Erfundene Testumgebung: Kampagne PRD-1 (Paket, Praefix 1111) mit Lauf ueber Nacht, unbekanntes Produkt 2204, Produkt ohne Paket
        static IDataStore P5Store(DateTime d0, string dir)
        {
            MemoryStore s = new MemoryStore();
            Catalog cat = s.GetCatalog();
            ProductProfile p1 = new ProductProfile(); p1.Code = "PRD-1"; p1.Name = "Produkt-1"; cat.Products.Add(p1);
            ProductProfile p2 = new ProductProfile(); p2.Code = "PRD-2"; p2.Name = "Produkt-2"; cat.Products.Add(p2);
            EquipDevice dv = new EquipDevice(); dv.Id = "Ansatz-Tank2"; dv.Class = "Test"; cat.Master.Devices.Add(dv);
            EquipPosition pos = new EquipPosition(); pos.Name = "Testposition"; pos.Short = "TP"; pos.Ids.Add("TFF1"); cat.Positions.Add(pos);
            s.SaveCatalog(cat);
            Package pk = new Package(); pk.Name = "Kampagne PRD-1"; pk.ProductCode = "PRD-1"; pk.RunPrefix = "1111"; pk.Save(Path.Combine(dir, "prd1.apkg"));
            File.WriteAllText(Path.Combine(dir, "defekt.apkg"), "kein Paket");
            int r = s.GetRows()[0].Id, r2 = s.GetRows()[1].Id;
            Put(s, d0, r, "Lauf 11110001\nTP: TFF1\nTP: QX77 (Ausweich: H1-T-0111)\nTP: frei nach Absprache 2");
            Put(s, d0.AddDays(3), r, "Lauf 11110002\nTP: ansatz-tank2\nPumpe H1-P-0222");
            Put(s, d0.AddDays(4), r, "Lauf 11110002\nTP: H1-T-0333\nvorTFF1-2 nicht treffen");   // ueber Nacht: zweiter Tag
            Put(s, d0.AddDays(9), r, "Lauf 11110003");
            Entry note = s.Get(d0.AddDays(9), r);
            note.Content = "Lauf 11110003\nTP: QX88\nTP: S12"; FormatSpan ns = new FormatSpan(); ns.Start = 14; ns.Length = 8; ns.Note = ns.Italic = true; note.Spans.Add(ns);   // Notizzeile
            FormatSpan it = new FormatSpan(); it.Start = 27; it.Length = 3; it.Italic = true; note.Spans.Add(it);                                                                   // kursive Notiz hinter dem Kurznamen
            s.Save(d0.AddDays(9), r, note);
            Put(s, d0.AddDays(5), r2, "Lauf 22040030");
            Put(s, d0.AddDays(6), r2, "Lauf 22040031");
            return s;
        }

        static string P5Dir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ap_p5_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Testbilder: --form equipp5 (Dialog), --form equipcal (Kalender)
        Form TestP5Form(string which)
        {
            DateTime d0 = start.Date;
            string dir = P5Dir();
            IDataStore s = P5Store(d0, dir);
            EquipExportForm ef = new EquipExportForm(s, d0, null, dir);
            try { Directory.Delete(dir, true); } catch (IOException) { }
            ef.TestCampMode();
            return which == "equipcal" ? (Form)ef.TestCalendar() : ef;
        }

        void SelfTestP5(StringBuilder sb)
        {
            DateTime d0 = new DateTime(2031, 3, 3);
            string dir = P5Dir();
            try
            {
                IDataStore s = P5Store(d0, dir);
                EquipExport.ProductMap map = new EquipExport.ProductMap(dir, s.GetCatalog());
                string why;
                List<EquipExport.CampaignSpan> cs = map.Campaigns(s, d0.AddDays(-5), d0.AddDays(30), "", out why);
                bool okText = cs.Count == 2 && cs[0].Label == "Produkt-1 (PRD-1) · Lauf 1–3 · 03.03.2031 – 12.03.2031" && cs[1].Label == "Produkt unbekannt (2204) · Lauf 30–31 · 08.03.2031 – 09.03.2031";
                sb.AppendLine("P5 Kampagnentext mit und ohne Produkt: " + Ok(okText) + (okText || cs.Count == 0 ? "" : " [" + cs[0].Label + "]"));

                // Mehrtaegiger Lauf: letzter Tag zaehlt, Equipment des zweiten Tags dabei; Lauf 2 nur einmal
                List<EquipExport.CampaignSpan> night = EquipExport.Campaigns(s, d0, d0.AddDays(4), "");
                bool okNight = night.Count == 1 && night[0].FirstNo == 1 && night[0].LastNo == 2 && night[0].Last == d0.AddDays(4) && night[0].Runs.Count == 2 && night[0].RunDays.Count == 3
                    && EquipExport.Campaigns(s, d0, d0.AddDays(4), "1111")[0].Last == d0.AddDays(9);
                List<string> second = cs.Count > 0 ? EquipExport.CollectCampaign(s, cs[0], 0, 0) : new List<string>();
                sb.AppendLine("P5 Lauf ueber Nacht: ein Lauf, letzter Tag " + Ok(okNight) + " zweiter Tag im Export " + Ok(second.Contains("H1-T-0333")));

                // Geraete aus der Liste, Wortgrenzen, Schreibweise der Liste; nicht erkannt ohne Notizen
                List<string> un = new List<string>();
                List<KeyValuePair<string, int>> ids = EquipExport.Count(s, d0, d0.AddDays(9), un);
                List<string> idl = EquipExport.Ids(ids);
                bool okList = idl.Contains("TFF1") && idl.Contains("Ansatz-Tank2") && idl.Contains("H1-T-0111") && !idl.Contains("ansatz-tank2");
                bool okBound = ids.Find(delegate(KeyValuePair<string, int> k) { return k.Key == "TFF1"; }).Value == 1;   // "vorTFF1-2" zaehlt nicht
                bool okUn = un.Count == 1 && un[0] == "QX77" && !idl.Contains("QX77");
                sb.AppendLine("P5 Geraete aus der Liste: " + Ok(okList) + " Wortgrenzen " + Ok(okBound) + " nicht erkannt ohne Notizen " + Ok(okUn) + (okUn ? "" : " [" + string.Join(",", un.ToArray()) + "]"));

                using (EquipExportForm f = new EquipExportForm(s, d0, null, dir))
                {
                    f.TestCampMode();
                    bool okItems = f.TestProductItems().Contains("Produkt-1 (PRD-1)") && f.TestProductItems()[0] == EquipExport.AllProducts;
                    bool okAll = f.TestCamps().Count == 2 && f.TestCount.Contains("Equipment-Nummern");
                    f.TestProduct("PRD-1"); bool okCode = f.TestCamps().Count == 1 && f.TestCamps()[0].StartsWith("Produkt-1 (PRD-1)") && f.TestPreview.Contains("Ansatz-Tank2") && f.TestUnknown == "1 Eintrag nicht erkannt: QX77";
                    f.TestProduct("Produkt-1 (PRD-1)"); bool okEntry = f.TestCamps().Count == 1;
                    f.TestProduct("prd-1"); bool okCase = f.TestCamps().Count == 1;
                    f.TestProduct("1111"); bool okPrefix = f.TestCamps().Count == 1 && f.TestCamps()[0].StartsWith("Produkt-1");
                    f.TestProduct("2204"); bool okUnk = f.TestCamps().Count == 1 && f.TestCamps()[0].StartsWith("Produkt unbekannt (2204)");
                    f.TestProduct("PRD-2"); bool okNoPkg = f.TestCamps().Count == 0 && f.TestCount.Contains("kein Paket") && !f.TestCount.Contains("0 Equipment");
                    f.TestProduct("GIBTSNICHT"); bool okNone = f.TestCamps().Count == 0 && f.TestCount.Contains("Kein Produkt") && !f.TestCount.Contains("0 Equipment");
                    f.TestProduct("9999"); bool okNoRun = f.TestCamps().Count == 0 && f.TestCount.Contains("Keine Läufe");
                    sb.AppendLine("P5 Produktfeld: Eintraege " + Ok(okItems) + " alle " + Ok(okAll) + " Code tippen " + Ok(okCode) + " Eintrag " + Ok(okEntry) + " klein " + Ok(okCase) + " Praefix " + Ok(okPrefix) + " unbekannt " + Ok(okUnk)
                        + " Grund: ohne Paket " + Ok(okNoPkg) + " kein Produkt " + Ok(okNone) + " keine Laeufe " + Ok(okNoRun));

                    // Kalender: Filter steht auf 9999, Klick muss ihn loesen
                    using (CampaignCalendar cal = f.TestCalendar())
                    {
                        cal.Show(); cal.ClientSize = new Size(1000, 660); Application.DoEvents();
                        cal.TestGo(0);
                        bool okBuf = cal.TestBuffered;
                        bool okTip = cal.TestTip(d0).StartsWith("Produkt-1 (PRD-1)") && cal.TestTip(d0.AddDays(20)) == "";
                        bool okSoft = cal.TestSoftAt(d0.AddDays(1)) && !cal.TestSoftAt(d0) && !cal.TestSoftAt(d0.AddDays(4));
                        cal.TestClick(d0); bool okClick = f.TestSelected.StartsWith("Produkt-1 (PRD-1)") && f.TestCount.Contains("Equipment-Nummern");
                        cal.TestClick(d0.AddDays(20)); bool okEmpty = f.TestSelected.StartsWith("Produkt-1 (PRD-1)");   // leerer Tag aendert nichts
                        cal.TestDrag(d0.AddDays(12), d0.AddDays(10)); bool okDrag = f.TestRangeText == "2031-03-13..2031-03-15" && f.TestSelected == "";
                        cal.TestClick(d0.AddDays(6)); bool okSecond = f.TestSelected.StartsWith("Produkt-1") || f.TestSelected.StartsWith("Produkt unbekannt");
                        Size c0 = cal.TestCell; int m0 = cal.TestMonths;
                        cal.WindowState = FormWindowState.Normal; cal.ClientSize = new Size(1500, 1000); Application.DoEvents();
                        Size c1 = cal.TestCell; int m1 = cal.TestMonths;
                        cal.ClientSize = new Size(460, 300); Application.DoEvents();
                        Size c2 = cal.TestCell;
                        bool okSize = c1.Width > c0.Width && m1 >= m0 && (c1.Height > c0.Height || m1 > m0) && c2.Width == 16;
                        DateTime f0 = cal.TestFirst; cal.TestGo(1); bool okNext = cal.TestFirst == f0.AddMonths(1); cal.TestGo(-2); bool okPrev = cal.TestFirst == f0.AddMonths(-1);
                        cal.TestGo(-99); bool okClamp = cal.TestFirst.Year == 2030;
                        sb.AppendLine("P5 Kalender: Doppelpuffer " + Ok(okBuf) + " Tooltip " + Ok(okTip) + " Puffer heller " + Ok(okSoft) + " Klick waehlt " + Ok(okClick) + " leerer Tag " + Ok(okEmpty) + " Ziehen setzt Zeitraum " + Ok(okDrag) + " Ueberlappung " + Ok(okSecond)
                            + " Groesse " + Ok(okSize) + (okSize ? "" : " [" + c0 + m0 + c1 + m1 + c2 + "]") + " blaettern " + Ok(okNext && okPrev && okClamp));
                        cal.Close();
                    }
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }
    }
}
#endif
