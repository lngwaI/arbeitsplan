#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-5f/N5 am sichtbaren Paket-Editor: Notiz kursiv in der Vorschau (Puffer: eigene Zeile), Leiste "nicht in der Equipment-Liste"
    // (nur bei unbekannter ID; Ignorieren; Hinzufuegen = sofort angelegt, Art geraten, Katalog gespeichert, Rueckgaengig; ohne Recht dauerhafte
    // Vormerkung, fuer den Admin sichtbar). Modell/Plan: MainForm.SelfTestUX5f.cs. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        // Testbild --form package18: Puffer mit Notizen an Equipment-Zeilen (Vorschau: eigene kursive Zeile unter dem Geraet)
        public static Package Ux5fSample(string name)
        {
            Package p = Ux1bSample(name, false);
            PackageStep pu = p.Steps[p.Steps.Count - 1];
            pu.Puffers[0].Lines[0].Note = "500 L, Edelstahl"; pu.Puffers[0].Lines[1].Note = "bitte vorher spülen";
            return p;
        }

        // Testbild --form equipadd2: Lauf-Schritt mit unbekanntem Tank (Leiste) und Equipment-Notizen
        public static Package Ux5fBarSample(string name)
        {
            Package p = Package.SampleCampaign(name);
            EquipAssign a = new EquipAssign(); a.Short = "PT"; a.Choices.Add(new EquipChoice("PT", "H1-T-0123")); a.Note = "neuer Tank, noch nicht freigegeben";
            p.Steps[2].Equipment.Add(a);
            return p;
        }

        static EquipAssign Ux5fA(string sh, string id, string note) { EquipAssign a = new EquipAssign(); a.Short = sh; if (id.Length > 0) a.Choices.Add(new EquipChoice(sh, id)); a.Note = note; return a; }

        static int CountHints(PackageForm f, string id) { return f.hintList.FindAll(delegate(Hint h) { return h.UnvalId == id; }).Count; }

        public static string TestUx5f(IEquipmentStatusProvider equip, DateTime start)
        {
            const string Id1 = "H1-T-0123", Id2 = "H1-T-0124", Id3 = "H1-T-0125";
            MemoryStore ms = new MemoryStore();
            PackageForm.Ux1bPrepare(ms);
            Package pk = new Package(); pk.Name = "ux5f";
            PackageStep st = new PackageStep(); st.Title = "Test"; st.Row = "Suite 1"; st.Day = 1; pk.Steps.Add(st);
            EquipPosition pos = null; foreach (EquipPosition x in ms.GetCatalog().Positions) if (x.Ids.Count > 0 && x.Short != "Ansatztank") { pos = x; break; }
            string sh = pos != null ? pos.Short : "PT", known = pos != null ? pos.Ids[0] : "H1-T-0102";
            EquipAssign a = new EquipAssign(); a.Short = sh; a.Choices.Add(new EquipChoice(sh, known)); st.Equipment.Add(a);
            Package pp1 = Ux1bSample("p", false); pk.Steps.Add(pp1.Steps[pp1.Steps.Count - 1]);   // zweiter Schritt: Puffer mit Karten
            PackageForm f = new PackageForm(ms, equip, start, pk, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000); f.ShowInTaskbar = false;
            string stage = "";
            bool okKnown = false, okBar = false, okIgnore = false, okAdd = false, okClass = false, okSaved = false, okGreen = false, okConfirm = false, okUndo = false, okDetails = false, okPuf = false;
            bool okNoRight = false, okPersist = false, okAdmin = false, okDup = false, okXml = false, okDialog = false, okOnce = false;
            try
            {
                f.Show(); Application.DoEvents();
                st = f.steps[0]; a = st.Equipment[0];   // der Editor arbeitet auf Kopien
                stage = "bekannt"; f.Bind(st); Application.DoEvents();
                okKnown = !f.TestBar.Visible && f.TestBarTarget == "";                                  // bekanntes Geraet: keine Leiste
                stage = "unbekannt"; a.Choices[0].Id = Id1; f.Bind(st); Application.DoEvents();
                okBar = f.TestBar.Visible && f.TestBar.Message.StartsWith(Id1 + " ist nicht in der Equipment-Liste") && f.TestBar.MainText == "Hinzufügen" && f.TestBarTarget == Id1;
                okDetails = f.TestBar.HasDetails;
                okOnce = CountHints(f, Id1) == 1;                                                       // genau ein Hinweis in der Liste
                stage = "ignorieren"; f.TestBar.TestClick(2); Application.DoEvents();
                okIgnore = !f.TestBar.Visible && CountHints(f, Id1) == 1;                               // Leiste weg, Hinweisliste bleibt konsistent

                stage = "hinzufuegen"; a.Choices[0].Id = Id2; f.Bind(st); Application.DoEvents();
                bool shown = f.TestBar.Visible && f.TestBarTarget == Id2;
                f.TestBar.TestClick(0); Application.DoEvents();
                EquipDevice d = f.cat.Master.Device(Id2);
                string want = ""; foreach (EquipDevice x in f.cat.Master.Devices) if (x.Id.StartsWith("H1-T-") && x.Id != Id2 && x.Class.Length > 0) { want = x.Class; break; }
                okAdd = shown && d != null; okClass = d != null && want.Length > 0 && d.Class == want;
                EquipPosition pp = f.cat.Position(sh);
                okSaved = ms.GetCatalog().Master.Device(Id2) != null && ms.GetCatalog().Position(sh).Ids.Contains(Id2) && pp != null && pp.Ids.Contains(Id2);
                okGreen = EquipRow.Known(a, f.cat, f.Product) && CountHints(f, Id2) == 0;
                okConfirm = f.TestBar.Visible && f.TestBar.Confirm && f.TestBar.Message.StartsWith(Id2 + " hinzugefügt") && f.TestBar.MainText == "Rückgängig";
                stage = "rueckgaengig"; f.TestBar.TestClick(0); Application.DoEvents();
                okUndo = f.cat.Master.Device(Id2) == null && ms.GetCatalog().Master.Device(Id2) == null && !ms.GetCatalog().Position(sh).Ids.Contains(Id2)
                    && f.TestBar.Visible && !f.TestBar.Confirm && f.TestBarTarget == Id2;               // wieder unbekannt: Leiste fragt erneut

                stage = "puffer"; PackageStep pu = f.steps[1];
                pu.Puffers[0].Lines[0].Choices[0].Id = "H1-T-0199"; f.Bind(pu); Application.DoEvents();
                okPuf = f.TestBar.Visible && f.TestBarTarget == "H1-T-0199" && f.TestBar.Message.StartsWith("H1-T-0199 ist nicht in der Equipment-Liste");
            }
            catch (Exception ex) { return "UX5f Editor: ABBRUCH (" + stage + ") " + ex.GetType().Name + " " + ex.Message + "\r\n"; }
            finally { f.dirty = false; f.Close(); }

            // ohne Recht: Vormerkung bleibt dauerhaft, der Admin sieht sie
            Package pk2 = new Package(); pk2.Name = "ux5f2";
            PackageStep st2 = new PackageStep(); st2.Title = "Test"; st2.Row = "Suite 1"; st2.Day = 1; pk2.Steps.Add(st2);
            EquipAssign b = new EquipAssign(); b.Short = sh; b.Choices.Add(new EquipChoice(sh, Id3)); st2.Equipment.Add(b);
            PackageForm n = new PackageForm(ms, equip, start, pk2, true, false);
            User u = new User(); u.Login = "pwerk"; n.By = u;
            n.StartPosition = FormStartPosition.Manual; n.Location = new Point(-4000, -4000); n.ShowInTaskbar = false;
            PackageForm g = null;
            try
            {
                n.Show(); Application.DoEvents();
                st2 = n.steps[0];
                stage = "ohne Recht"; n.Bind(st2); Application.DoEvents();
                okNoRight = n.TestBar.Visible && n.TestBar.MainText == "Admin informieren" && !n.TestBar.HasDetails && n.TestBarTarget == Id3 && ms.GetCatalog().Master.PendingFor(Id3) == null;
                n.TestBar.TestClick(0); Application.DoEvents();
                EquipPending p = ms.GetCatalog().Master.PendingFor(Id3);
                okPersist = p != null && p.By == "pwerk" && p.Short == sh && p.At > DateTime.Now.AddMinutes(-5) && ms.GetCatalog().Master.Device(Id3) == null
                    && n.TestBar.Confirm && n.TestBar.Message.StartsWith(Id3 + " vorgemerkt") && n.hintList.Exists(delegate(Hint h) { return h.UnvalId == Id3 && h.Text.Contains("Vorgemerkt"); }) && CountHints(n, Id3) == 1;
                okDup = !ms.GetCatalog().Master.AddPending(Id3, sh, "x", DateTime.Now) && ms.GetCatalog().Master.Pending.Count == 1;
                // Rundweg XML (Katalog im Sicherungsformat) und frischer Editor des Admins
                StoreData back = Backup.FromXml(Backup.ToXml(ms.Snapshot()));
                EquipPending q = back.Catalog.Master.PendingFor(Id3);
                okXml = q != null && q.By == "pwerk" && q.Short == sh && q.At.ToString("s") == p.At.ToString("s");
                g = new PackageForm(ms, equip, start, pk2, true, true);
                g.StartPosition = FormStartPosition.Manual; g.Location = new Point(-4000, -4000); g.ShowInTaskbar = false;
                g.Show(); g.Bind(g.steps[0]); Application.DoEvents();
                okAdmin = g.cat.Master.PendingFor(Id3) != null && g.TestBar.Visible && g.TestBar.MainText == "Hinzufügen" && g.TestBarTarget == Id3;
                // Stammdaten: Vormerkung uebernehmen = Geraet in Position, Vormerkung weg; verwerfen
                Catalog c2 = ms.GetCatalog(); c2.Master.AddPending("H1-T-0130", sh, "pwerk", DateTime.Now);
                using (EquipPendingForm pf = new EquipPendingForm(c2))
                {
                    pf.StartPosition = FormStartPosition.Manual; pf.Location = new Point(-4000, -4000); pf.Show();
                    pf.Accept(0); pf.Discard(0);
                    okDialog = c2.Master.Pending.Count == 0 && c2.Master.Device(Id3) != null && c2.Master.Device("H1-T-0130") == null && c2.Position(sh).Ids.Contains(Id3) && pf.Changes == 2;
                }
            }
            catch (Exception ex) { return "UX5f Editor: ABBRUCH (" + stage + ") " + ex.GetType().Name + " " + ex.Message + "\r\n"; }
            finally { n.dirty = false; n.Close(); if (g != null) { g.dirty = false; g.Close(); } }

            return "UX5f Leiste: bekanntes Gerät keine Leiste " + T(okKnown) + " unbekannt mit Hinzufügen/Details " + T(okBar && okDetails) + " ein Hinweis " + T(okOnce) + " Ignorieren " + T(okIgnore)
                + " Hinzufügen legt an " + T(okAdd) + " Art aus Schema " + T(okClass) + " Katalog gespeichert " + T(okSaved) + " validiert " + T(okGreen) + " Bestätigung " + T(okConfirm) + " Rückgängig " + T(okUndo)
                + " Puffer-Karte " + T(okPuf) + "\r\nUX5f Vormerkung: ohne Recht Leiste " + T(okNoRight) + " dauerhaft gespeichert " + T(okPersist) + " keine Doppelte " + T(okDup) + " XML-Rundweg " + T(okXml)
                + " Admin sieht sie " + T(okAdmin) + " Stammdaten übernehmen/verwerfen " + T(okDialog) + "\r\n";
        }
    }
}
#endif
