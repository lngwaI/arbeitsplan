#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class CampaignForm
    {
        public void TestCode(string code, bool save) { tCode.Text = code; chkCode.Checked = save; }
    }

    // Selbsttest P6: Puffer-Kopf ohne "Puffer n:", gemeinsame Zeilen an ihrer Stelle, Kurzform umbenennen (Verweise), Produktcode (Kampagne, Paketdateien),
    // Freeze-Haken. Nur erfundene Daten. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        static PufferTemplatePos P6Pos(string sh, string id, bool shared)
        {
            PufferTemplatePos p = new PufferTemplatePos(); p.Short = sh; p.Ids.Add(id); p.Shared = shared; return p;
        }

        static bool P6Mid(PackageStep x)
        {
            string tx = x.BuffersFor(1, "")[0].Text;
            return tx.IndexOf("AT-1") >= 0 && tx.IndexOf("AT-1") < tx.IndexOf("VT-1") && tx.IndexOf("VT-1") < tx.IndexOf("TW-1");
        }

        public static string TestP6(IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder();
            string dir = Path.Combine(Path.GetTempPath(), "p6_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // (1) Kopf ohne "Puffer n:"
                BufferItem b1 = new BufferItem(); b1.Name = "Lösung A"; b1.LaufNo = "12340002";
                BufferItem b2 = new BufferItem();
                bool okHead = b1.HeadText() == "Lösung A · für 12340002" && b2.HeadText() == "–" && b1.HeadText().IndexOf("Puffer") < 0;
                sb.AppendLine("P6 Puffer-Kopf ohne Nummer: " + T(okHead));

                // (2) gemeinsame Zeile an ihrer Stelle der Vorlage, XML-Rundlauf, alte Daten am Ende
                PufferTemplate t = new PufferTemplate(); t.Name = "Lösung A";
                t.Positions.Add(P6Pos("Ansatztank", "AT-1", false)); t.Positions.Add(P6Pos("VT", "VT-1", true)); t.Positions.Add(P6Pos("Tween", "TW-1", false));
                PackageStep s = new PackageStep(); s.Title = "Puffer"; s.Source = "Puffer"; s.Row = "Puffer"; s.Day = 1;
                Package.ApplyPufferTemplate(s, t);
                bool okMid = P6Mid(s);
                Package pk = new Package(); pk.Name = "P6"; pk.Steps.Add(s);
                string f1 = Path.Combine(dir, "x.apkg"); pk.Save(f1);
                PackageStep s2 = Package.Load(f1).Steps[0];
                bool okRt = s2.PufferShared.Count == 1 && s2.PufferShared[0].After == 1 && P6Mid(s2);
                s2.PufferShared[0].After = -1;
                string tx2 = s2.BuffersFor(1, "")[0].Text;
                bool okEnd = tx2.IndexOf("TW-1") < tx2.IndexOf("VT-1");
                sb.AppendLine("P6 Gemeinsam an Vorlagenstelle: Plan " + T(okMid) + " XML-Rundlauf " + T(okRt) + " alte Daten am Ende " + T(okEnd));

                // (3) Kurzform umbenennen: Verweise im Katalog (auch Tausch)
                Catalog c = new Catalog();
                Baustein bs = new Baustein(); bs.Name = "B"; bs.Positions.AddRange(new[] { "XA", "XB" }); bs.SetDefaultCount("XA", 3); c.Bausteine.Add(bs);
                EquipGroup g = new EquipGroup(); g.Name = "G"; g.Shorts.Add("XA"); c.Groups.Add(g);
                ProductProfile pr = new ProductProfile(); pr.Code = "PRD-1";
                ProfilePos pp = new ProfilePos(); pp.Short = "XA"; pr.Positions.Add(pp);
                PufferTemplate pt = new PufferTemplate(); pt.Positions.Add(P6Pos("XA", "I-1", false)); pr.Puffer.Add(pt); c.Products.Add(pr);
                Dictionary<string, string> map = new Dictionary<string, string>(); map["XA"] = "XC"; map["XB"] = "XA";
                c.RenameShorts(map);
                bool okRen = bs.Positions[0] == "XC" && bs.Positions[1] == "XA" && bs.DefaultCount("XC") == 3 && bs.DefaultCount("XA") == 1 && g.Shorts[0] == "XC"
                    && pp.Short == "XC" && pt.Positions[0].Short == "XC" && s.PufferShared[0].Eq.Short == "VT";
                sb.AppendLine("P6 Kurzform umbenennen: Verweise im Katalog " + T(okRen));

                // (4) Produktcode: Kampagnenstart, Paketdateien, Produkt-Dialog
                IDataStore st = P1Store();
                Package camp = P1Camp(st, 2, 6);
                bool okCamp;
                using (CampaignForm cf = new CampaignForm(camp, st, P1D0, ApplyMode.FillEmpty, true))
                {
                    bool none = !cf.SaveCode;
                    cf.TestCode("PRD-7", false); bool nosave = !cf.SaveCode && cf.Result.ProductCode == "PRD-7";
                    cf.TestCode("PRD-7", true); bool save = cf.SaveCode;
                    cf.TestCode("PRD-1", true); okCamp = none && nosave && save && !cf.SaveCode && camp.ProductCode == "PRD-1";
                }
                Package a = new Package(); a.ProductCode = "PRD-1"; a.Save(Path.Combine(dir, "a.apkg"));
                Package bq = new Package(); bq.ProductCode = "PRD-2"; bq.Save(Path.Combine(dir, "b.apkg"));
                File.WriteAllText(Path.Combine(dir, "c.apkg"), "kein Paket");
                Dictionary<string, string> rn = new Dictionary<string, string>(); rn["PRD-1"] = "PRD-9";
                List<string> errs = new List<string>();
                int n = Package.RenameProduct(dir, rn, errs);
                bool okFiles = n == 1 && errs.Count == 1 && errs[0].StartsWith("c.apkg") && Package.Load(Path.Combine(dir, "a.apkg")).ProductCode == "PRD-9" && Package.Load(Path.Combine(dir, "b.apkg")).ProductCode == "PRD-2";
                Catalog pc = Catalog.Default(); ProductProfile pp1 = new ProductProfile(); pp1.Code = "PRD-1"; pc.Products.Add(pp1);
                bool okForm;
                using (ProductForm pf = new ProductForm(pc))
                {
                    pf.TestNoAsk(); pf.TestSelect("PRD-1", "TFF"); pf.PackageFolder = dir; pf.TestAnswer = DialogResult.Yes;
                    pf.TestEdit("PRD-8", "x"); pf.TestEdit("PRD-6", "x");
                    okForm = pf.PackRename.Count == 1 && pf.PackRename["PRD-1"] == "PRD-6" && pf.TestMsgs.Contains("Auch alle Pakete");
                }
                sb.AppendLine("P6 Produktcode: Kampagnenstart " + T(okCamp) + " Paketdateien (Meldung je Datei) " + T(okFiles) + " Produkt-Dialog " + T(okForm));

                // (5) Freeze-Haken beim neuen Schritt
                PackageForm f = new PackageForm(st, equip, start, null, true, false);
                try
                {
                    Baustein fz = new Baustein(); fz.Name = "Freeze / Thaw"; fz.Row = "Freeze / Thaw";
                    Baustein lb = new Baustein(); lb.Name = "Lauf"; lb.Row = "Suite 1";
                    bool okFz = f.FromBaustein(fz).Freeze != null && f.FromBaustein(lb).Freeze == null;
                    sb.AppendLine("P6 Freeze-Haken standardmäßig an: " + T(okFz));
                }
                finally { f.dirty = false; f.Close(); f.Dispose(); }
            }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
            return sb.ToString().TrimEnd();
        }
    }
}
#endif
