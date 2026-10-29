#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Paket-Editor v3: freier Produktcode, 8-stellige erste Laufnummer (Hochzaehlen, Ueberlauf), alte Pakete,
    // Puffer-Variante beim Uebernehmen, Standard-Bausteine, Kampagnenstart mit Laufnummer; Editor-Tests in PackageForm.Test.cs.
    partial class MainForm
    {
        void SelfTestPaket2(System.Text.StringBuilder sb)
        {
            // Produktcode: 2-12 Zeichen, Buchstaben/Ziffern/- _ . / +, keine Leerzeichen
            string[] good = { "AB1", "AB12345678-", "1357", "ab_1.2/3+x", "Xy" }, bad = { "", "A", "AB 1", "AB1234567890-", "AB#1", "--", "AB1\t" };
            bool okGood = Array.TrueForAll(good, Package.IsProductCode), okBad = !Array.Exists(bad, Package.IsProductCode);
            sb.AppendLine("Paket2 Produktcode: gueltig " + Ok(okGood) + " ungueltig " + Ok(okBad));

            // Erste Laufnummer 8-stellig, Folgelaeufe zaehlen hoch, Ueberlauf > 9999; Produktcode unabhaengig
            Package p = new Package(); p.Name = "Lauftest"; p.ProductCode = "AB1";
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Count = 3; lf.Interval = 2; lf.Day = 2;
            PackageStep sp = new PackageStep(); sp.Title = "Splitten"; sp.Row = "Mix / Split"; sp.Kind = StepKind.SubRun; sp.Count = 3; sp.Day = 3; sp.SubNos = "4186, 4187, 4188";
            p.Steps.Add(lf); p.Steps.Add(sp);
            bool set = p.SetFirstRun("13570030");
            bool okNo = set && p.RunPrefix == "1357" && p.FirstRun == "13570030" && lf.Label(p.Prefix, 2) == "Lauf 13570032"
                && sp.Compose(0, p.Prefix) == "Splitten\nUnterlauf 13574186 (aus 13570030)" && p.Schedule()[1].Label == "Lauf 13570031" && p.Overflow() == null;
            string pre; int no;
            bool okParse = !Package.ParseRun("1357003", out pre, out no) && !Package.ParseRun("1357003X", out pre, out no) && !Package.ParseRun("123456789", out pre, out no)
                && Package.ParseRun(" 99990001 ", out pre, out no) && pre == "9999" && no == 1;
            bool okOver = p.SetFirstRun("13579998") && p.Overflow() != null && p.Overflow().Contains("9999");
            p.SetFirstRun("13570030");
            string path = Path.Combine(Path.GetTempPath(), "selftest_paket2.apkg");
            p.Save(path);
            Package q = Package.Load(path);
            bool okRt = q.ProductCode == "AB1" && q.RunPrefix == "1357" && q.FirstRun == "13570030" && q.Steps[0].Compose(1, q.Prefix) == "Lauf 13570031";
            sb.AppendLine("Paket2 Laufnummer: 8-stellig/Hochzaehlen " + Ok(okNo) + " Eingabe " + Ok(okParse) + " Ueberlauf gemeldet " + Ok(okOver) + " Datei " + Ok(okRt));

            // Alte Pakete (Version 3: produkt = 4 Ziffern, Startnummer ZZZZ) bleiben lesbar: Laufnummer = Produkt + Startnummer
            File.WriteAllText(path, "<Paket name=\"Alt\" version=\"3\" produkt=\"1357\"><Schritt titel=\"Lauf\" block=\"Suite 2\" tag=\"1\" anzahl=\"2\" abstand=\"1\" startnr=\"30\" art=\"lauf\"/></Paket>");
            Package old = Package.Load(path);
            bool okOld = old.ProductCode == "1357" && old.Prefix == "1357" && old.FirstRun == "13570030" && old.Steps[0].Compose(1, old.Prefix) == "Lauf 13570031";
            old.Save(path); Package old2 = Package.Load(path);
            okOld &= old2.RunPrefix == "1357" && old2.FirstRun == "13570030";
            sb.AppendLine("Paket2 alte Pakete: lesbar " + Ok(okOld));

            // Puffer-Variante: wird beim Uebernehmen gesetzt; leere Variante im Paket ueberschreibt nicht; anhaengen = Vereinigung
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2031, 3, 3);
            Package pv = new Package(); pv.Name = "Puffer";
            PackageStep ps = new PackageStep(); ps.Title = "Puffer"; ps.Row = "Puffer"; ps.Variant = "mobil|stationaer"; pv.Steps.Add(ps);
            pv.Apply(ms, d0, ApplyMode.Overwrite, null);
            bool okSet = ms.Get(d0, puf) != null && ms.Get(d0, puf).Types == "mobil|stationaer";
            Entry keep = new Entry(); keep.Content = "Puffer alt"; BufferSet.SetTypes(keep, "mobil"); ms.Save(d0.AddDays(1), puf, keep);
            Package pn = new Package(); PackageStep pns = ps.Clone(); pns.Variant = ""; pn.Steps.Add(pns);
            pn.Apply(ms, d0.AddDays(1), ApplyMode.Overwrite, null);
            bool okKeep = ms.Get(d0.AddDays(1), puf).Types == "mobil" && ms.Get(d0.AddDays(1), puf).Content == "Puffer";
            Package pa = new Package(); PackageStep pas = ps.Clone(); pas.Variant = "stationaer"; pa.Steps.Add(pas);
            pa.Apply(ms, d0.AddDays(1), ApplyMode.Append, null);
            bool okUnion = ms.Get(d0.AddDays(1), puf).Types == "mobil|stationaer";
            pv.Save(path);
            bool okFile = Package.Load(path).Steps[0].Variant == "mobil|stationaer";
            sb.AppendLine("Paket2 Puffer-Variante: gesetzt " + Ok(okSet) + " leer behaelt " + Ok(okKeep) + " anhaengen vereinigt " + Ok(okUnion) + " Datei " + Ok(okFile));

            // Standard-Bausteine: neu im Standard; alter Katalog wird ergaenzt, Vorhandenes bleibt
            Catalog cd = Catalog.Default();
            bool okDef = cd.Baustein_("Puffer") != null && cd.Baustein_("Puffer").Row == "Puffer" && cd.Baustein_("Freeze / Thaw") != null && cd.Baustein_("CIP / SIP") != null && cd.Baustein_("Einwaage") != null;
            Catalog oc = new Catalog(); Baustein own = new Baustein(); own.Name = "Puffer"; own.Row = "Eigen"; oc.Bausteine.Add(own);
            int added = oc.AddStandardBausteine(), again = oc.AddStandardBausteine();
            bool okMig = added == 8 && again == 0 && oc.Baustein_("Puffer").Row == "Eigen" && oc.Bausteine.Count == 9 && oc.Baustein_(Package.BackupName) != null;
            sb.AppendLine("Paket2 Bausteine: Standard " + Ok(okDef) + " Ergaenzung alter Bestand " + Ok(okMig) + " (" + added + " ergaenzt)");

            // Kampagnenstart: komplette Laufnummer eintippen, Ueberlauf sperrt
            CampaignForm.Quiet = true;
            using (CampaignForm cf = new CampaignForm(q, ms, d0.AddDays(14), ApplyMode.Overwrite))
            {
                cf.FirstRunText = "24680040";
                bool okC = cf.Warning == null && cf.Schedule.Exists(delegate(RunDate r) { return r.Label == "Lauf 24680041"; });
                cf.FirstRunText = "24689998";
                bool okCo = cf.Warning != null;
                cf.FirstRunText = "2468004";
                bool okCi = cf.Warning != null;
                sb.AppendLine("Paket2 Kampagnenstart: Laufnummer " + Ok(okC) + " Ueberlauf gesperrt " + Ok(okCo) + " ungueltig gesperrt " + Ok(okCi));
            }
            CampaignForm.Quiet = false;
            sb.AppendLine(PackageForm.TestPaket2(store, equip, start));
            sb.AppendLine(TestHintClick());
        }

        // Jeden Pruefhinweis im neuen Paket anklicken: modale Faelle (PackageForm.TestHintClickNew) und der echte Menueweg
        // (OpenPackageEditor wie "Neues Paket anlegen…", Editor per Timer finden). Auch einzeln: --hintclick <txt>.
        public string TestHintClick()
        {
            string r = PackageForm.TestHintClickNew(this, store, equip, start);
            List<string> err = new List<string>(); int[] n = new int[3]; bool[] jumped = { true };
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 100;
            t.Tick += delegate
            {
                foreach (System.Windows.Forms.Form f in System.Windows.Forms.Application.OpenForms)
                    if (f is PackageForm && f.Visible) { t.Stop(); ((PackageForm)f).AutoClickHints(err, n, jumped); return; }
            };
            System.Threading.ThreadExceptionEventHandler h = delegate(object o, System.Threading.ThreadExceptionEventArgs e) { err.Add(e.Exception.ToString()); };
            System.Windows.Forms.Application.ThreadException += h;
            try { t.Start(); OpenPackageEditor(null); }
            catch (Exception ex) { err.Add(ex.ToString()); }
            finally { t.Dispose(); System.Windows.Forms.Application.ThreadException -= h; }
            return r + "\r\nHinweis-Klick Menue Neues Paket: " + n[0] + " Klicks (" + n[1] + " per Maus), Sprung zum Namen " + (jumped[0] ? "OK" : "FEHLER")
                + ", Ausnahmen " + err.Count + " " + (err.Count == 0 && n[0] >= 1 ? "OK" : "FEHLER") + (err.Count > 0 ? " [" + err[0].Replace("\r", " ").Replace("\n", " | ") + "]" : "");
        }
    }
}
#endif
