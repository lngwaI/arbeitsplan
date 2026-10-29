#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E3: neue Standard-Bausteine (Puffer mobil / stationaer, Zwischenreinigung), Schritt-Dialog ohne alten Puffer,
    // Puffer-Schritt mit festem Typ, alter Schritt "beides" = zwei Puffer, Assistent-Pakettypen.
    partial class MainForm
    {
        void SelfTestE3(StringBuilder sb)
        {
            int seen = Package.Seen.Count;
            string dir = Path.Combine(Path.GetTempPath(), "selftest_e3_" + Guid.NewGuid().ToString("N")), tmp = Path.GetTempFileName();
            try
            {
                Catalog c = Catalog.Default();
                Baustein bm = c.Baustein_("Puffer mobil"), bs = c.Baustein_("Puffer stationär"), bz = c.Baustein_("Zwischenreinigung"), bo = c.Baustein_(Catalog.OldBufferName);
                bool okCat = bm != null && bs != null && bz != null && bo != null && bm.Variant == "mobil" && bs.Variant == "stationaer" && bo.Variant.Length == 0
                    && bm.Positions.Count > 0 && bs.Positions.Count > 0 && bz.Special && !bz.DefaultInCampaign && !bz.Unique;
                foreach (Baustein b in c.Bausteine) if (b.DefaultInCampaign && b.Name != Package.PackenName) okCat = false;   // nichts Neues laeuft automatisch mit
                MemoryStore ms = new MemoryStore(); StoreData d = ms.Snapshot(); d.Catalog = c;
                Backup.Write(d, tmp, Backup.KeyOf(ms));
                Catalog rc = Backup.Read(tmp, Backup.KeyOf(ms)).Catalog;
                bool okRound = rc.Baustein_("Puffer mobil").Variant == "mobil" && rc.Baustein_("Puffer stationär").Variant == "stationaer" && rc.Baustein_("Zwischenreinigung").Special && rc.Clone().Baustein_("Puffer mobil").Variant == "mobil";
                Catalog oc = new Catalog(); Baustein own = new Baustein(); own.Name = "Puffer"; oc.Bausteine.Add(own);
                oc.AddStandardBausteine();
                bool okOld = oc.Baustein_("Puffer mobil") != null && oc.Position("MobT") != null && oc.Baustein_("Puffer").Variant.Length == 0;   // alter Katalog wird bei Bedarf ergaenzt
                sb.AppendLine("E3 Bausteine: Standard " + K(okCat) + " Speichern/Lesen " + K(okRound) + " alter Katalog ergaenzt " + K(okOld));

                List<string> rows = new List<string>(); rows.Add("Puffer"); rows.Add("Suite 1");
                bool okPick;
                using (StepPicker p = new StepPicker(c, new List<PackageStep>(), rows))
                {
                    List<string> n = p.TestNames;
                    okPick = n.Contains("Puffer mobil") && n.Contains("Puffer stationär") && n.Contains("Zwischenreinigung") && !n.Contains(Catalog.OldBufferName)
                        && n.IndexOf("Zwischenreinigung") > n.IndexOf("Puffer stationär");
                }
                sb.AppendLine("E3 Schritt-Dialog: alter Puffer ausgeblendet " + K(okPick));

                PackageStep sm = new PackageStep(); sm.Title = "Ansatz A"; sm.Row = "Puffer"; sm.Variant = bm.Variant;
                PackageStep ss = new PackageStep(); ss.Title = "Ansatz B"; ss.Row = "Puffer"; ss.Variant = bs.Variant;
                PackageStep both = new PackageStep(); both.Title = "Alt"; both.Row = "Puffer"; both.Variant = "mobil|stationaer";
                List<BufferItem> bm1 = sm.BuffersFor(0, ""), bs1 = ss.BuffersFor(0, ""), bb = both.BuffersFor(0, "");
                bool okBuf = sm.IsBuffer && bm1.Count == 1 && bm1[0].Typ == "mobil" && bm1[0].Name == "Ansatz A" && ss.IsBuffer && bs1.Count == 1 && bs1[0].Typ == "stationaer"
                    && sm.EntryFor(0, "").Buffers.Count == 1;
                bool okBoth = bb.Count == 2 && bb[0].Typ == "mobil" && bb[1].Typ == "stationaer" && bb[0].Name == "Alt" && bb[1].Name == "Alt";
                sb.AppendLine("E3 Puffer-Schritt: ein Schritt = ein Puffer mit Typ " + K(okBuf) + " alter Schritt beides = zwei Puffer " + K(okBoth));

                Package zr = new Package(); zr.Name = "Zwischen"; zr.ProductCode = "AB1";
                PackageStep zs = new PackageStep(); zs.Title = "Zwischenreinigung"; zs.Source = "Zwischenreinigung"; zs.Row = bz.Row; zr.Steps.Add(zs);
                bool okZw = zr.Head == null && zr.Type == PackageType.Ereignis && zr.Schedule().Count <= 1;
                sb.AppendLine("E3 Zwischenreinigung: von Hand, keine Automatik " + K(okZw));

                Directory.CreateDirectory(dir);
                Package ca = Package.SampleCampaign("Alpha"); ca.ProductCode = "AB1"; ca.Save(Path.Combine(dir, "alpha.apkg"));
                Package ei = new Package(); ei.Name = "Einzel"; ei.ProductCode = "AB1"; ei.Type = PackageType.Einzel;
                PackageStep es = new PackageStep(); es.Title = "Packen"; es.Source = "Packen"; es.Row = "Suite 2"; es.ManualNo = "12340002"; ei.Steps.Add(es); ei.Save(Path.Combine(dir, "einzel.apkg"));
                Package ev = new Package(); ev.Name = "Flush"; ev.ProductCode = "AB1"; ev.Type = PackageType.Ereignis;
                PackageStep fs = new PackageStep(); fs.Title = "TFF Flush"; fs.Source = "TFF Flush"; fs.Row = "Suite 1"; ev.Steps.Add(fs); ev.Save(Path.Combine(dir, "flush.apkg"));
                using (CampaignWizard w = new CampaignWizard(dir, c, null))
                {
                    w.TestPage(2, "AB1", PackageType.Kampagne); List<string> k = w.TestItems(); PackageType tk = w.Type;
                    w.TestPage(2, "AB1", PackageType.Einzel); List<string> e = w.TestItems(); PackageType te = w.Type;
                    w.TestPage(2, "AB1", PackageType.Ereignis); List<string> x = w.TestItems(); PackageType tx = w.Type; bool sp = w.Special;
                    bool okWiz = tk == PackageType.Kampagne && k.Count == 1 && k[0] == "alpha" && te == PackageType.Einzel && e.Count == 1 && e[0] == "einzel"
                        && tx == PackageType.Ereignis && sp && x.Count == 1 && x[0] == "flush";
                    sb.AppendLine("E3 Assistent: Typen Kampagne | Einzelpaket | Sonderereignis " + K(okWiz));
                }
                Package le = Package.Load(Path.Combine(dir, "einzel.apkg"));
                sb.AppendLine("E3 Einzelpaket: Typ bleibt in der Datei (Kampagnenstart nutzt den einfachen Modus) " + K(le.Type == PackageType.Einzel && le.Steps[0].ManualNo == "12340002"));
            }
            catch (Exception ex) { sb.AppendLine("E3 Selbsttest: FEHLER " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
                try { File.Delete(tmp); } catch (IOException) { }
                if (Package.Seen.Count > seen) Package.Seen.RemoveRange(seen, Package.Seen.Count - seen);   // neue Formate nicht in den Golden-Test
            }
        }
    }
}
#endif
