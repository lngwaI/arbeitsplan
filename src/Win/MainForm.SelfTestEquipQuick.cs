#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Tests und Testbilder: Schnellerfassung, Nicht zugeordnet, Passt zu (Mehrfachauswahl), Eigenschaftsfelder je Art.
    partial class MainForm
    {
        const string QuickSample = "H1-T-0110\tNeuer Tank\nH1-T-0111\nH1-X-0120;Skid Reserve\nH2-P-0301\nH1-T-0102\nH1-T-0110\nXYZ 12\nSONDER-7\n";

        // --form quickadd | unassigned | equipfits
        Form TestQuickForm(string which)
        {
            EquipMaster em = store.GetCatalog().Master;
            if (which == "quickadd") { EquipQuickForm q = new EquipQuickForm(em); q.TestPaste(QuickSample); return q; }
            if (which == "unassigned")
            {
                em.AddQuick(em.ParseQuick("SONDER-7\tAlte Nummer\nH1-L-0301\nH1-L-0302\nH1-L-0303\tGehäuse getauscht\nH1-T-0120"));
                em.Device("H1-T-0120").Class = "";
                return new EquipUnassignedForm(em);
            }
            em.Series.Add(Grp("Gehäuse 10 Zoll", "Filtergehäuse", "H1-F-0101-01", "H1-F-0101-04"));
            em.SetPair("H1-X-0102", "H1-F-0101-01", true);
            return new EquipFitsForm(em, "H1-X-0102");
        }

        static EquipSeries Grp(string name, string cls, params string[] ids) { EquipSeries s = new EquipSeries(); s.Name = name; s.Class = cls; s.Ids.AddRange(ids); return s; }

        static void SelfTestEquipQuick(StringBuilder sb)
        {
            // ---------- Parser, Schema-Vorschlag, Duplikate ----------
            EquipMaster m = EquipMaster.Default();
            int n0 = m.Devices.Count;
            List<QuickRow> rows = m.ParseQuick(QuickSample);
            bool okPar = rows.Count == 8 && rows[0].Id == "H1-T-0110" && rows[0].Note == "Neuer Tank" && rows[2].Id == "H1-X-0120" && rows[2].Note == "Skid Reserve";
            bool okSch = rows[0].Class == "Tank" && rows[1].Class == "Tank" && rows[2].Class == "Skid" && rows[3].Class == "Pumpe" && rows[7].Class == "" && EquipMaster.LetterOf("H2-F-0203") == "F";
            bool okDup = rows[4].State == QuickState.Exists && rows[5].State == QuickState.Duplicate && rows[6].State == QuickState.Invalid && rows[7].State == QuickState.New;
            int added = m.AddQuick(rows);
            okPar &= added == 5 && m.Devices.Count == n0 + 5 && m.Device("H1-T-0110").Note == "Neuer Tank" && m.Device("H1-T-0102").Note == "";
            bool okUn = m.Unassigned().Count == 1 && m.Unassigned()[0].Id == "SONDER-7" && m.AddQuick(m.ParseQuick("H1-T-0111")) == 0;
            sb.AppendLine("Schnellerfassung: Parser Tab/Semikolon=" + Ok(okPar) + " Schema-Vorschlag=" + Ok(okSch) + " Duplikate/ungültig=" + Ok(okDup) + " Nicht zugeordnet=" + Ok(okUn));

            // ---------- Formulare: Schnellerfassung, Zuordnen mehrerer, Stammdaten-Zähler ----------
            MemoryStore s = new MemoryStore();
            bool okForm, okMulti, okCnt;
            using (EquipMasterForm f = new EquipMasterForm(s.GetCatalog()))
            {
                Offscreen(f);
                f.TestQuick("A-1\nA-2\nA-3\nH1-T-0150", "");
                okCnt = f.TestUnassignedCount == 3 && f.TestUnassignedText().StartsWith("3 noch nicht zugeordnet");
                okForm = f.TestTree().Contains("H1-T-0150");
                f.Close();
            }
            Catalog cat = s.GetCatalog();
            using (EquipQuickForm q = new EquipQuickForm(cat.Master))
            {
                Offscreen(q);
                q.TestPaste("H1-T-0160\nB-1\nB-2\nH1-T-0102");
                bool a = q.TestRows.Count == 4 && q.TestSummary().Contains("3 neu") && q.TestSummary().Contains("1 schon vorhanden");
                q.TestSetClass(1, "Zubehör");   // neue Art direkt, nur fuer diese Zeile
                okForm &= a && q.TestApply() == 3 && cat.Master.Class("Zubehör") != null && cat.Master.Device("B-1").Class == "Zubehör";
            }
            using (EquipUnassignedForm u = new EquipUnassignedForm(cat.Master))
            {
                Offscreen(u);
                u.TestMark("B-2", "A-9");
                okMulti = u.TestCount == 1;
                cat.Master.AddQuick(cat.Master.ParseQuick("C-1\nC-2\nC-3"));
                using (EquipUnassignedForm u2 = new EquipUnassignedForm(cat.Master))
                {
                    Offscreen(u2);
                    u2.TestMark("C-1", "C-2");
                    okMulti &= u2.TestAssign("Skid") == 2 && cat.Master.Device("C-2").Class == "Skid" && cat.Master.Device("C-3").Class == "" && u2.TestCount == 2;
                    u2.TestMark("C-3", "B-2"); okMulti &= u2.TestAssign("Neue Art X") == 2 && cat.Master.Class("Neue Art X") != null && cat.Master.Unassigned().Count == 0;
                }
            }
            sb.AppendLine("Schnellerfassung Formular=" + Ok(okForm) + " Zuordnen mehrerer=" + Ok(okMulti) + " Zähler Stammdaten=" + Ok(okCnt));

            // ---------- Passt zu: Mehrfachauswahl, Vorschlag nach Gruppe ----------
            EquipMaster g = EquipMaster.Default();
            g.Series.Add(Grp("Gehäuse 10 Zoll", "Filtergehäuse", "H1-F-0101-01", "H1-F-0101-04"));
            g.SetPair("H1-X-0102", "H1-F-0101-01", true);
            List<string> sg = g.SuggestFits("H1-X-0102");
            bool okFit = sg.Count == 1 && sg[0] == "H1-F-0101-04";
            int ad, rm;
            g.SetFits("H1-X-0102", new[] { "H1-F-0101-01", "H1-F-0101-04", "H1-F-0101-02" }, out ad, out rm);
            okFit &= ad == 2 && rm == 0 && g.HasPair("H1-F-0101-02", "H1-X-0102") && g.CompatibleWith("H1-F-0101-04").Contains("H1-X-0102");
            g.SetFits("H1-X-0102", new[] { "H1-F-0101-04" }, out ad, out rm);
            okFit &= ad == 0 && rm == 2 && !g.HasPair("H1-X-0102", "H1-F-0101-01") && g.HasPair("H1-X-0102", "H1-F-0101-04") && g.HasPair("H1-X-0101", "H1-F-0101-01");
            using (EquipFitsForm ff = new EquipFitsForm(g, "H1-X-0102"))
            {
                Offscreen(ff);
                okFit &= ff.TestPreview().Contains("Gewählt: 1");
                ff.TestCheck("H1-T-0102"); ff.TestApply();
                okFit &= g.HasPair("H1-T-0102", "H1-X-0102");
            }
            sb.AppendLine("Passt zu bequemer: Vorschlag nach Gruppe, Mehrfachauswahl, beide Richtungen=" + Ok(okFit));

            // ---------- Eigenschaftsfelder je Art ----------
            MemoryStore fs = new MemoryStore(); bool okFld;
            using (EquipMasterForm f = new EquipMasterForm(fs.GetCatalog()))
            {
                Offscreen(f);
                f.TestSelect("Tank"); f.TestAddField("Farbe"); f.TestRenameField("Volumen", "Inhalt"); f.TestMoveField("Farbe", -1);
                okFld = f.TestFields() == "Inhalt,Farbe,Material" && f.TestCommit()
                    && f.Result.Master.Device("H1-T-0102").Props[0].Name == "Inhalt";
                f.Close();
            }
            sb.AppendLine("Eigenschaftsfelder je Art: hinzufügen/umbenennen/Reihenfolge=" + Ok(okFld));
        }
    }
}
#endif
