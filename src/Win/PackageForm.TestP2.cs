#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest P2 (Paket-Editor-Oberflaeche): Packen "Wann?", Art ausgeblendet, Tabelle Abstand je Lauf, Equipment-Leiste (Alle hinzufuegen,
    // Rueckgaengig, Ersetzen), "+" je Equipment-Zeile (auch Schlauch ohne Geraeteliste und Puffer-Karte). Nur erfundene Daten.
    partial class PackageForm
    {
        const string P2A = "H1-T-0991", P2B = "H1-T-0992";

        public static string TestP2Editor(IEquipmentStatusProvider equip, DateTime start)
        {
            IDataStore store = P1Store();
            string oldPrefs = PrefsFile, tmp = Path.Combine(Path.GetTempPath(), "p2_prefs_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            PackageForm f = new PackageForm(store, equip, start, null, true, true);   // Admin
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            PackageForm n = null;
            string stage = "";
            try
            {
                f.Show(); Application.DoEvents();
                Predicate<PackageStep> isLauf = delegate(PackageStep x) { return x.Kind == StepKind.Run; }, isBk = delegate(PackageStep x) { return x.IsBackup; },
                    isPk = delegate(PackageStep x) { return x.Source == Package.PackenName && !x.IsBackup; }, isMo = delegate(PackageStep x) { return x.Variant == "mobil"; };

                // (1) Packen: nur "Tag(e) vor Lauf 1", mindestens 2 mit Back-Up; Back-Up nur Anzeige; Art ausgeblendet
                stage = "packen"; PackageStep pk = f.P1Step(isPk), lauf = f.P1Step(isLauf), bk = f.P1Step(isBk), mo = f.P1Step(isMo);
                f.Bind(pk); Application.DoEvents();
                bool okPk = f.nDay.Visible && f.cbDir.Visible && f.cbDir.Items.Count == 1 && f.cbDir.Items[0].ToString().Contains("vor Lauf 1") && !f.cbBind.Visible && !f.cbRepeat.Visible
                    && !f.cbEnd.Visible && f.nDay.Minimum == 2 && f.nDay.Value == 2;
                f.nDay.Value = 3; Application.DoEvents();
                okPk &= f.StepsView().Lauf1Day - pk.FirstDay == 3 && f.StepsView().Lauf1Day - bk.FirstDay == 2 && !Package.HasBlock(f.StepsView().Check());
                bool okArt = !f.cbKind.Visible && !f.lKind.Visible && !f.lPufInfo.Visible;
                f.Bind(bk); Application.DoEvents();
                bool okBk = !f.nDay.Visible && !f.cbDir.Visible && !f.cbBind.Visible && !f.cbRepeat.Visible && !f.cbKind.Visible;
                f.Bind(lauf); Application.DoEvents();
                okArt &= f.cbKind.Visible;
                lauf.Count = 3; lauf.Interval = 6;
                f.Bind(mo); Application.DoEvents();
                okArt &= !f.cbKind.Visible && f.lPufInfo.Visible && f.lPufInfo.Text.StartsWith("Puffer · bedient Lauf 1–3");

                // (2) Tabelle "Lauf | Tage vorher"
                stage = "tabelle";
                bool okTab = f.gridPuf.Visible && f.gridPuf.Rows.Count == 3 && f.gridPuf[1, 0].Value.ToString() == "1" && f.gridPuf[1, 2].Value.ToString() == "1";
                f.SetPufDist(1, "3"); Application.DoEvents();
                okTab &= mo.Shift.Count == 1 && mo.RunOffset + mo.Shift[1] == -3 && f.gridPuf[1, 1].Value.ToString() == "3" && f.gridPuf[1, 0].Value.ToString() == "1";
                f.SetPufDist(1, "x"); Application.DoEvents();
                okTab &= mo.RunOffset + mo.Shift[1] == -3 && f.gridPuf[1, 1].Value.ToString() == "3";
                f.Bind(lauf); Application.DoEvents();
                okTab &= !f.gridPuf.Visible;

                // (3) Ansatz: stationaer keine Wahl, Hinweisziel nicht das ausgeblendete Feld; gemischte Karten: Tank | Flasche
                stage = "ansatz";
                PackageStep mixed = new PackageStep(); mixed.Puffers.Add(new PufferSpec()); PufferSpec ps = new PufferSpec(); ps.Typ = "stationaer"; mixed.Puffers.Add(ps);
                bool okAns = mixed.AnsatzChoices().Count == 2 && mixed.AnsatzChoices()[0].Value.StartsWith("Tank") && f.AnsatzField(mixed) == f.cbAnsatz;
                PackageStep st = new PackageStep(); st.Variant = "stationaer";
                okAns &= st.AnsatzChoices().Count == 1 && f.AnsatzField(st) != f.cbAnsatz;

                // (4) Equipment-Leiste: Hinzufuegen springt weiter, Rueckgaengig, Alle hinzufuegen mit einem Rueckgaengig
                stage = "leiste";
                EquipPosition pos = null; foreach (EquipPosition x in store.GetCatalog().Positions) if (x.Ids.Count > 0 && x.Short != "Ansatztank") { pos = x; break; }
                string sh = pos != null ? pos.Short : "PT";
                f.Bind(pk); pk.Equipment.Clear();
                foreach (string id in new[] { P2A, P2B }) { EquipAssign a = new EquipAssign(); a.Short = sh; a.Choices.Add(new EquipChoice(sh, id)); pk.Equipment.Add(a); }
                f.Bind(pk); Application.DoEvents();
                bool okBar = f.TestBar.Visible && f.TestBar.Message.StartsWith(P2A + " ist nicht") && f.TestBar.AllText == "Alle hinzufügen (2)" && f.TestBar.HasDetails && !f.TestBar.HasUndo;
                f.TestBar.TestClick(0); Application.DoEvents();   // Hinzufuegen: gleich das naechste Geraet, kein OK
                bool okNext = f.cat.Master.Device(P2A) != null && f.TestBar.Visible && !f.TestBar.Confirm && f.TestBarTarget == P2B && f.TestBar.HasUndo
                    && f.TestBar.Message.Contains("Zuletzt: " + P2A + " hinzugefügt") && f.TestBar.AllText == "";
                f.TestBar.TestClick(4); Application.DoEvents();   // Rueckgaengig
                bool okUndo1 = f.cat.Master.Device(P2A) == null && f.TestBarTarget == P2A && f.TestBar.AllText == "Alle hinzufügen (2)";
                f.TestBar.TestClick(3); Application.DoEvents();   // Alle
                bool okAll = f.cat.Master.Device(P2A) != null && f.cat.Master.Device(P2B) != null && f.TestBar.Confirm && f.TestBar.Message.StartsWith("2 Geräte hinzugefügt") && EquipRow.Known(pk.Equipment[0], f.cat, f.Product);
                f.TestBar.TestClick(0); Application.DoEvents();   // Rueckgaengig fuer alle
                okAll &= f.cat.Master.Device(P2A) == null && f.cat.Master.Device(P2B) == null && store.GetCatalog().Master.Device(P2B) == null && f.TestBarTarget == P2A;

                // (5) "+" je Zeile: Position mit Liste bleibt leer (Fokus Geraet); ohne Liste (Schlauch) wird das Geraet uebernommen (Fokus Notiz); Rueckgaengig
                stage = "plus";
                EquipPosition hose = new EquipPosition(); hose.Name = "Schlauch"; hose.Short = "SchlauchP2"; f.cat.Positions.Add(hose);
                pk.Equipment.Clear();
                EquipAssign s0 = new EquipAssign(); s0.Short = "SchlauchP2"; s0.Choices.Add(new EquipChoice("SchlauchP2", "SCH-1")); s0.Note = "grün";
                EquipAssign s1 = new EquipAssign(); s1.Short = sh; s1.Choices.Add(new EquipChoice(sh, pos != null ? pos.Ids[0] : "X-1"));
                pk.Equipment.Add(s0); pk.Equipment.Add(s1);
                f.Bind(pk); Application.DoEvents(); f.CommitUndo();
                f.AddBelow(s0); Application.DoEvents();
                bool okPlus = pk.Equipment.Count == 3 && pk.Equipment[0] == s0 && pk.Equipment[2] == s1 && pk.Equipment[1].Short == "SchlauchP2" && pk.Equipment[1].Choices.Count == 1
                    && pk.Equipment[1].Choices[0].Id == "SCH-1" && pk.Equipment[1].Note == "" && f.eqRows.Count == 3 && f.eqRows[1].Assign == pk.Equipment[1] && f.eqRows[1].AddBelow != null;
                f.AddBelow(s1); Application.DoEvents();
                okPlus &= pk.Equipment.Count == 4 && pk.Equipment[3].Short == sh && pk.Equipment[3].Choices.Count == 0 && f.eqRows.Count == 4 && f.eqRows[3].Assign == pk.Equipment[3];
                f.UndoStep(-1); Application.DoEvents();
                okPlus &= f.cur.Equipment.Count == 3;
                f.UndoStep(-1); Application.DoEvents();
                okPlus &= f.cur.Equipment.Count == 2;

                // (6) Puffer-Karte (ohne Recht): "+" in der Karte und Ersetzen durch das aehnlichste Geraet
                stage = "karte";
                Package pp = Ux1bSample("p2", false);
                n = new PackageForm(store, equip, start, pp, true, false);
                n.StartPosition = FormStartPosition.Manual; n.Location = new Point(0, 0);
                n.Show(); Application.DoEvents();
                PackageStep pu = n.steps[n.steps.Count - 1];
                n.Bind(pu); n.TestOpenCard(0); Application.DoEvents();
                PufferSpec card = pu.Puffers[0]; int cn = card.Lines.Count;
                EquipAssign at = card.Lines[0];
                at.Choices[0].Id = "AT-22"; n.Bind(pu); n.TestOpenCard(0); Application.DoEvents();
                bool okRep0 = n.TestBar.Visible && n.TestBarTarget == "AT-22" && n.TestBar.MainText == "Admin informieren" && !n.TestBar.HasDetails;
                Unk u = new Unk(); u.Id = "AT-22"; u.Short = "Ansatztank"; u.Step = pu;
                List<string> allowed, more; n.ReplaceChoices(u, out allowed, out more);
                bool okRep = allowed.Count == 3 && allowed[0] == "AT-2" && allowed[2] == "AT-3" || allowed.Count == 3 && allowed[0] == "AT-2";
                okRep &= more.Count > 0 && !more.Contains("AT-2");
                n.dirty = false;
                bool changed = n.ReplaceId("AT-22", "AT-2");
                okRep &= changed && at.Choices[0].Id == "AT-2" && n.dirty && n.TestBarTarget != "AT-22";
                // "+" in der Karte: Zeile mit Liste leer darunter, Geraet-Fokus
                n.cards[0].AddLineBelow(card.Lines[0]); Application.DoEvents();
                bool okCard = card.Lines.Count == cn + 1 && card.Lines[1].Short == card.Lines[0].Short && card.Lines[1].Choices.Count == 0;
                n.UndoStep(-1); Application.DoEvents();
                okCard &= n.steps[n.steps.Count - 1].Puffers[0].Lines.Count == cn;

                return "P2 Editor: Packen Wann? " + T(okPk) + " Back-Up nur Anzeige " + T(okBk) + " Art ausgeblendet, Puffer-Anzeige " + T(okArt) + " Tabelle Abstand je Lauf " + T(okTab)
                    + " Ansatz/Hinweisziel " + T(okAns) + " Leiste " + T(okBar) + " Hinzufügen springt weiter " + T(okNext) + " Rückgängig " + T(okUndo1) + " Alle hinzufügen + Rückgängig " + T(okAll)
                    + " Plus-Zeile " + T(okPlus) + " Ersetzen ohne Recht " + T(okRep0 && okRep) + " Plus in Karte " + T(okCard);
            }
            catch (Exception ex) { return "P2 Editor: ABBRUCH (" + stage + ") " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace; }
            finally
            {
                f.dirty = false; f.Close(); f.Dispose();
                if (n != null) { n.dirty = false; n.Close(); n.Dispose(); }
                PrefsFile = oldPrefs;
                try { File.Delete(tmp); } catch (Exception) { }
            }
        }
    }
}
#endif
