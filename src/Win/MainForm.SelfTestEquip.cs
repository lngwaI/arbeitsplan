#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Xml;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Teil C (Equipment-Uebersicht: Regeln auf Art-/Gruppenebene, Matrix, Serie, Tabelle, Link-Felder, Datenformat)
    // und Teil D (manueller Equipment-Status, Rechte, Audit, Uebergangslogik).
    partial class MainForm
    {
        static EquipDevice Pump(string id) { EquipDevice d = new EquipDevice(); d.Id = id; d.Class = "Pumpe"; return d; }

        void SelfTestEquip(StringBuilder sb)
        {
            // ---------- Regeln auf Art-/Gruppenebene, Warnung ----------
            EquipMaster m = EquipMaster.Default();
            m.Devices.Add(Pump("H1-P-0001"));
            EquipLink rl = new EquipLink(); rl.From = "#Tanks 0103 bis 0104"; rl.To.Add("@Pumpe"); m.Links.Add(rl);
            bool okTok = m.CompatibleWith("H1-P-0001").Contains("H1-T-0103") && !m.CompatibleWith("H1-P-0001").Contains("H1-T-0102")
                && m.CompatibleWith("H1-T-0104").Contains("H1-P-0001") && m.CompatibleWith("H1-T-0102").Count == 0 && m.Expand("@Tank", "H1-T-0102").Count == 2;
            bool okChk = m.CheckCompat(new[] { "H1-X-0102", "H1-F-0101-01" }).Count == 1
                && m.CheckCompat(new[] { "H1-X-0101", "H1-F-0101-01", "H1-F-0101-02" }).Count == 0
                && m.CheckCompat(new[] { "H1-T-0102", "H1-F-0101-01" }).Count == 0
                && m.CheckCompat(new[] { "H1-P-0001", "H1-T-0102" }).Count == 1 && m.CheckCompat(new[] { "H1-P-0001", "H1-T-0103" }).Count == 0;
            MemoryStore cs = new MemoryStore(); Catalog cat = cs.GetCatalog(); cat.Master = m.Clone();
            List<string> w = new List<string>(); List<string> er = cat.CheckText("TFF: H1-X-0102\nFilter: H1-F-0101-01", w);
            bool okTxt = er.Count == 1 && w.Count == 1 && w[0].Contains("passt laut Stammdaten nicht");
            Package pk = new Package(); pk.ProductCode = "1234"; PackageStep st = new PackageStep(); st.Title = "Lauf"; st.Row = "Suite 1";
            EquipAssign a1 = new EquipAssign(); a1.Short = "TFF"; a1.Choices.Add(new EquipChoice("TFF", "H1-X-0102"));
            EquipAssign a2 = new EquipAssign(); a2.Short = "FG"; a2.Choices.Add(new EquipChoice("FG", "H1-F-0101-01"));
            st.Equipment.Add(a1); st.Equipment.Add(a2); pk.Steps.Add(st);
            bool okPkgW = cat.CheckPackageWarn(pk).Count == 1 && cat.CheckPackage(pk).Count == 1;
            sb.AppendLine("Equipment Regeln: Art/Gruppe (@/#)=" + Ok(okTok) + " Vertraeglichkeit=" + Ok(okChk) + " Blocktext-Warnung=" + Ok(okTxt) + " Paket-Warnung=" + Ok(okPkgW));

            // ---------- Matrix-Paare, Serie, Vorschlag, Duplizieren ----------
            m.SetPair("H1-F-0101-04", "H1-X-0102", true);
            bool okPair = m.HasPair("H1-X-0102", "H1-F-0101-04");
            m.SetPairNote("H1-F-0101-04", "H1-X-0102", "nur mit Adapter");
            okPair &= m.PairNote("H1-X-0102", "H1-F-0101-04") == "nur mit Adapter";
            m.SetPairNote("H1-X-0101", "H1-F-0101-02", "Variante");
            okPair &= m.PairNote("H1-X-0101", "H1-F-0101-02") == "Variante" && m.PairNote("H1-X-0101", "H1-F-0101-01") == "Gehäuse dieses Skids" && m.HasPair("H1-X-0101", "H1-F-0101-04");
            m.SetPair("H1-X-0101", "H1-F-0101-02", false);
            okPair &= !m.HasPair("H1-X-0101", "H1-F-0101-02") && m.HasPair("H1-X-0101", "H1-F-0101-01") && m.HasPair("H1-F-0101-04", "H1-X-0101");
            string err;
            List<string> ids = EquipMaster.SerialIds("H1-F-0201", "0205", out err);
            List<string> ids2 = EquipMaster.SerialIds("H1-F-0098", "H1-F-0101", out err);
            bool okSer = ids != null && ids.Count == 5 && ids[0] == "H1-F-0201" && ids[4] == "H1-F-0205" && ids2 != null && string.Join(",", ids2.ToArray()) == "H1-F-0098,H1-F-0099,H1-F-0100,H1-F-0101"
                && EquipMaster.SerialIds("H1-F-0105", "H1-F-0101", out err) == null && EquipMaster.SerialIds("H1-F-0101", "H1-X-0105", out err) == null
                && EquipMaster.SerialIds("H1-F-0001", "0300", out err) == null && EquipMaster.SerialIds("Tank", "5", out err) == null;
            List<EquipProp> props = new List<EquipProp>(); props.Add(new EquipProp("Datenblatt (Link)", "https://example.org/a.pdf"));
            int n0 = m.Devices.Count;
            List<string> added = m.AddSerial("Luftfilter", ids, "", props, "Luftfilter 0201 bis 0205");
            okSer &= added.Count == 5 && m.Devices.Count == n0 + 5 && m.Series.Exists(delegate(EquipSeries s) { return s.Name == "Luftfilter 0201 bis 0205" && m.Members(s).Count == 5; })
                && m.AddSerial("Luftfilter", ids, "", props, null).Count == 0;
            EquipClass lfc = new EquipClass(); lfc.Name = "Luftfilter"; lfc.Fields.Add("Datenblatt (Link)"); m.Classes.Add(lfc);
            bool okNext = m.NextId("H1-T-0102") == "H1-T-0105" && m.SuggestId("Tank") == "H1-T-0105" && m.NextId("Tank") == "";
            EquipDevice dup = m.Duplicate(m.Device("H1-F-0101-02"), "H1-F-0101-05");
            okNext &= dup.Parent == "H1-X-0101" && dup.Props.Count == 1 && dup.Props[0].Value == "5 Zoll" && m.Devices.IndexOf(dup) == m.Devices.IndexOf(m.Device("H1-F-0101-02")) + 1;
            okNext &= EquipClass.IsLink("Datenblatt (Link)") && !EquipClass.IsLink("Größe");
            sb.AppendLine("Equipment Matrix-Paare/Notiz=" + Ok(okPair) + " Serienanlage=" + Ok(okSer) + " Vorschlag/Duplizieren/Link-Feld=" + Ok(okNext));

            // ---------- Datenformat v4: Rundreise, v3 lesbar, Rechte ----------
            m.Class("Tank").Rules.Add(new StatusRule(EquipStatus.Gereinigt, EquipStatus.Sterilisiert, false));
            m.Device("H1-T-0103").Rules.Add(new StatusRule(EquipStatus.Gereinigt, EquipStatus.Sterilisiert, true));
            MemoryStore s2 = new MemoryStore(); Catalog c2 = s2.GetCatalog(); c2.Master = m.Clone(); c2.Products.Add(SampleProduct()); s2.SaveCatalog(c2);
            byte[] xml = Backup.ToXml(s2.Snapshot());
            EquipMaster rm = Backup.FromXml(xml).Catalog.Master;
            bool okRt = rm.Links.Exists(delegate(EquipLink l) { return l.From == "#Tanks 0103 bis 0104" && l.To.Contains("@Pumpe"); }) && rm.CompatibleWith("H1-P-0001").Contains("H1-T-0103")
                && rm.Devices.Count == m.Devices.Count && rm.Series.Count == m.Series.Count && rm.PairNote("H1-F-0101-04", "H1-X-0102") == "nur mit Adapter"
                && !rm.IsLegal("H1-T-0102", EquipStatus.Gereinigt, EquipStatus.Sterilisiert) && rm.IsLegal("H1-T-0103", EquipStatus.Gereinigt, EquipStatus.Sterilisiert)
                && rm.Device("H1-L-0201") == null && rm.Device("H1-F-0201").Props[0].Value == "https://example.org/a.pdf";
            string xs = Encoding.UTF8.GetString(xml).Replace("version=\"" + Backup.FormatVersion + "\"", "version=\"3\"");
            bool okV3 = Backup.FormatVersion == 5 && xs.Contains("version=\"3\"") && Backup.FromXml(Encoding.UTF8.GetBytes(xs)).Catalog.Master.Devices.Count == m.Devices.Count;
            string x4 = Encoding.UTF8.GetString(xml).Replace("version=\"" + Backup.FormatVersion + "\"", "version=\"4\"");
            okV3 &= x4.Contains("version=\"4\"") && Backup.FromXml(Encoding.UTF8.GetBytes(x4)).Catalog.Master.Devices.Count == m.Devices.Count;   // v4 bleibt lesbar
            XmlDocument old = new XmlDocument();
            old.LoadXml("<Nutzer rechte=\"2\"><Paket name=\"Leser\"><R v=\"" + Perm.Read + "\"/></Paket><Paket name=\"Produktion\"><R v=\"" + Perm.Read + "\"/><R v=\"" + Perm.EditText + "\"/></Paket></Nutzer>");
            UserDb od = UserDb.ReadXml(old.DocumentElement), nd = UserDb.Default();
            bool okPerm = Array.IndexOf(od.Package("Produktion").Rights, Perm.SetStatus) >= 0 && Array.IndexOf(od.Package("Leser").Rights, Perm.SetStatus) < 0
                && Array.IndexOf(nd.Package("Produktion").Rights, Perm.SetStatus) >= 0 && Array.IndexOf(nd.Package("Leser").Rights, Perm.SetStatus) < 0
                && Array.IndexOf(nd.Package("Admin").Rights, Perm.SetStatus) >= 0 && Perm.Label(Perm.SetStatus) == "Equipment-Status setzen";
            old.LoadXml("<Nutzer rechte=\"3\"><Paket name=\"Produktion\"><R v=\"" + Perm.Read + "\"/><R v=\"" + Perm.EditText + "\"/></Paket></Nutzer>");
            okPerm &= Array.IndexOf(UserDb.ReadXml(old.DocumentElement).Package("Produktion").Rights, Perm.SetStatus) < 0;   // aktuelle Datei: Admin hat es bewusst entzogen
            sb.AppendLine("Equipment Datenformat v" + Backup.FormatVersion + ": Rundreise (Regeln, Links, Uebergaenge)=" + Ok(okRt) + " v3 lesbar=" + Ok(okV3) + " Recht Status (alt/neu)=" + Ok(okPerm));

            // ---------- Oberflaechen: Tabelle, Matrix, Stammdaten-Formular ----------
            bool okTab, okMat, okForm;
            using (EquipTableForm t = new EquipTableForm(m, null))
            {
                Offscreen(t);
                okTab = t.TestRows == m.Devices.Count && t.TestHasLinkColumn && t.TestCell(0, "Geräteart").Length > 0;
                t.TestFilter("Filtergehäuse", "5 Zoll");
                okTab &= t.TestRows == 3 && t.TestCell(0, "Größe") == "5 Zoll";
                t.TestFilter("Luftfilter", "");
                okTab &= t.TestRows == 5 && t.TestCell(0, "Datenblatt (Link)") == "https://example.org/a.pdf";
                t.Close();
            }
            using (EquipMatrixForm mf = new EquipMatrixForm(c2, true, "Filtergehäuse", "Skid"))
            {
                Offscreen(mf);
                okMat = mf.TestCell("H1-F-0101-01", "H1-X-0101") == "✓" && mf.TestCell("H1-F-0101-01", "H1-X-0102") == "";
                mf.TestClick("H1-F-0101-01", "H1-X-0102");
                okMat &= mf.TestCell("H1-F-0101-01", "H1-X-0102") == "✓" && c2.Master.HasPair("H1-F-0101-01", "H1-X-0102");
                mf.TestNote("Adapter nötig");
                okMat &= c2.Master.PairNote("H1-F-0101-01", "H1-X-0102") == "Adapter nötig";
                mf.TestClick("H1-F-0101-01", "H1-X-0102");
                okMat &= mf.TestCell("H1-F-0101-01", "H1-X-0102") == "" && !c2.Master.HasPair("H1-F-0101-01", "H1-X-0102");
                mf.Close();
            }
            using (EquipMatrixForm pf = new EquipMatrixForm(c2, false, "Skid", "Filtergehäuse"))
            {
                Offscreen(pf);
                okMat &= pf.TestProduct("H1-X-0101", "1234") == "✓" && c2.ProductsAllowing("H1-X-0102").Contains("1234") && !c2.ProductsAllowing("H1-F-0101-01").Contains("1234");
                pf.Close();
            }
            MemoryStore s3 = new MemoryStore();
            using (EquipMasterForm f = new EquipMasterForm(s3.GetCatalog()))
            {
                Offscreen(f);
                f.TestSerial("Tank", "H1-T-0105", "0107", "Tanks 0105 bis 0107", "Volumen=500 L");
                f.TestSelect("H1-T-0106"); f.TestDuplicate(); string dupId = f.TestCurrentId();
                f.TestSelect("Tank"); f.TestRule("@Pumpe");
                f.TestNewDevice(); string newId = f.TestCurrentId();
                f.TestSelect("Tanks 0105 bis 0107"); f.TestRule("H1-X-0101");
                bool commit = f.TestCommit();
                EquipMaster fm = f.Result != null ? f.Result.Master : null;
                okForm = commit && dupId == "H1-T-0108" && newId == "H1-T-0109" && fm != null && fm.Device("H1-T-0108").Props.Count == 1 && fm.Device("H1-T-0108").Props[0].Value == "500 L"
                    && fm.Links.Exists(delegate(EquipLink l) { return l.From == "@Tank" && l.To.Contains("@Pumpe"); })
                    && fm.CompatibleWith("H1-X-0101").Contains("H1-T-0106") && fm.Series.Exists(delegate(EquipSeries s) { return s.Name == "Tanks 0105 bis 0107"; });
                f.Close();
            }
            sb.AppendLine("Equipment Formulare: Tabelle (Filter/Links)=" + Ok(okTab) + " Matrix (Klick/Notiz/Produkt)=" + Ok(okMat) + " Stammdaten (Serie/Duplizieren/Regeln)=" + Ok(okForm));

            // ---------- Teil D: manueller Status ----------
            MemoryStore ds = new MemoryStore();
            ManualStatusProvider mp = new ManualStatusProvider(ds, new MockEquipmentProvider());
            EquipmentInfo i0 = mp.Get("H1-T-0102");
            bool okProv = i0 != null && i0.Status == EquipStatus.Gereinigt && !mp.IsManual("H1-T-0102");
            okProv &= mp.Set("H1-T-0102", EquipStatus.Freigabe, me) && mp.Get("H1-T-0102").Status == EquipStatus.Freigabe && mp.IsManual("h1-t-0102") && mp.Get("h1-t-0102").Status == EquipStatus.Freigabe;
            okProv &= mp.Get("H1-T-0999") == null && mp.Set("H1-T-0999", EquipStatus.Sterilisiert, me) && mp.Get("H1-T-0999").Status == EquipStatus.Sterilisiert && mp.ListNumbers().Contains("H1-T-0999");
            List<AuditEntry> au = ds.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow);
            bool okAud = au.Count == 2 && au[0].Mark == "H1-T-0102" && au[0].Value == "Freigabe" && au[0].Login == me.Login && au[1].Mark == "H1-T-0999";
            okProv &= mp.Set("H1-T-0102", null, me) && mp.Get("H1-T-0102").Status == EquipStatus.Gereinigt && !mp.IsManual("H1-T-0102");
            au = ds.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow);
            okAud &= au.Count == 3 && au[2].Value == "";
            StoreData sd = Backup.FromXml(Backup.ToXml(ds.Snapshot()));
            MemoryStore ds2 = new MemoryStore(); ds2.Restore(sd);
            bool okSave = sd.Statuses.Count == 1 && sd.Statuses[0].Id == "H1-T-0999" && sd.Statuses[0].Status == EquipStatus.Sterilisiert && sd.StatusesKnown
                && ds2.GetStatuses().Count == 1 && new ManualStatusProvider(ds2, null).Get("H1-T-0999").Status == EquipStatus.Sterilisiert;
            StoreData legacy = new StoreData(); legacy.Catalog = ds2.GetCatalog(); legacy.Rows = ds2.GetRows();   // Sicherung ohne Status (StatusesKnown=false): Stand bleibt
            ds2.Restore(legacy);
            okSave &= ds2.GetStatuses().Count == 1;
            sb.AppendLine("Status manuell: Provider setzen/entfernen/Gross-klein=" + Ok(okProv) + " Audit=" + Ok(okAud) + " Sicherung/Wiederherstellen=" + Ok(okSave));

            // Uebergangslogik: Geraet vor Art, Standard erlaubt; Schalter aus = nichts vermerkt, an = nur Vermerk im Protokoll (kein UI)
            Catalog dc = ds.GetCatalog(); EquipMaster dm = dc.Master;
            dm.Class("Tank").Rules.Add(new StatusRule(EquipStatus.Gereinigt, EquipStatus.Sterilisiert, false));
            dm.Device("H1-T-0103").Rules.Add(new StatusRule(EquipStatus.Gereinigt, EquipStatus.Sterilisiert, true));
            ds.SaveCatalog(dc);
            bool okLegal = !dm.IsLegal("H1-T-0102", EquipStatus.Gereinigt, EquipStatus.Sterilisiert) && dm.IsLegal("H1-T-0103", EquipStatus.Gereinigt, EquipStatus.Sterilisiert)
                && dm.IsLegal("H1-T-0102", EquipStatus.Gereinigt, EquipStatus.Freigabe) && dm.IsLegal("H1-T-0102", EquipStatus.Gereinigt, EquipStatus.Gereinigt) && dm.IsLegal("H9-X-0000", EquipStatus.Gereinigt, EquipStatus.Sterilisiert);
            bool flag = ManualStatusProvider.CheckTransitions, okSw;
            try
            {
                ManualStatusProvider.CheckTransitions = false;
                mp.Set("H1-T-0102", EquipStatus.Sterilisiert, me);                       // aktuell Gereinigt (Mock) -> unzulaessig, aber Schalter aus
                string v1 = ds.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow).FindLast(delegate(AuditEntry x) { return x.Mark == "H1-T-0102"; }).Value;
                mp.Set("H1-T-0102", EquipStatus.Gereinigt, me);
                ManualStatusProvider.CheckTransitions = true;
                mp.Set("H1-T-0102", EquipStatus.Sterilisiert, me);                       // Gereinigt -> Sterilisiert: Vermerk "!", wird trotzdem gesetzt
                string v2 = ds.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow).FindLast(delegate(AuditEntry x) { return x.Mark == "H1-T-0102"; }).Value;
                mp.Set("H1-T-0102", EquipStatus.Gereinigt, me); mp.Set("H1-T-0103", EquipStatus.Gereinigt, me); mp.Set("H1-T-0103", EquipStatus.Sterilisiert, me);
                string v3 = ds.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow).FindLast(delegate(AuditEntry x) { return x.Mark == "H1-T-0103"; }).Value;
                okSw = v1 == "Sterilisiert" && v2 == "Sterilisiert!" && v3 == "Sterilisiert" && mp.Get("H1-T-0102").Status == EquipStatus.Gereinigt && mp.Get("H1-T-0103").Status == EquipStatus.Sterilisiert;
            }
            finally { ManualStatusProvider.CheckTransitions = flag; }
            okSw &= !flag;   // Standard: aus
            sb.AppendLine("Status Uebergangslogik: Regeln Geraet/Art/Standard=" + Ok(okLegal) + " Schalter aus/an (nur Vermerk, kein UI)=" + Ok(okSw));

            // Oberflaeche: Wort unter dem Mauszeiger, Recht, Setzen im Plan, Farbe der Nummer
            Font tf = Font;
            TextBlock tb = TextBlock.Build(new List<Piece>(new[] { new Piece("TFF: H1-X-0101, Notiz", tf, Color.Black, false, Color.Empty) }), tf, 400);
            Item hit = tb.Lines[0].Find(delegate(Item x) { return x.Text.StartsWith("H1-X"); });
            string word = hit != null ? tb.WordAt(hit.X + 2, 2) : null;
            bool okWord = word != null && EquipmentMatcher.Pattern.Match(word).Value == "H1-X-0101" && tb.WordAt(hit.X + hit.W + 1, 2) == "" && tb.WordAt(5000, 2) == "";
            if (systemPc) sb.AppendLine("Status im Plan: übersprungen (Systemrechner) Wort-Treffer=" + Ok(okWord));
            else
            {
                bool had = me.Has(Perm.SetStatus), okUi = had;
                string exp, got;
                if (had)
                {
                    okUi = SetEquipStatus("H1-X-0102", EquipStatus.Quarantaene) && equip.Get("H1-X-0102").Status == EquipStatus.Quarantaene && store.GetStatuses().Exists(delegate(DeviceStatus x) { return x.Id == "H1-X-0102"; })
                        && TestStatusMenuVisible("H1-X-0102");
                    okUi &= store.GetAudit(DateTime.Today, ManualStatusProvider.AuditRow).Exists(delegate(AuditEntry x) { return x.Mark == "H1-X-0102" && x.Value == "Quarantaene"; });
                    got = TestEquipAtFirst(out exp);
                    okWord &= exp != null && got == exp;
                    me.Rights.Remove(Perm.SetStatus);
                    try { okUi &= !SetEquipStatus("H1-X-0102", EquipStatus.Freigabe) && equip.Get("H1-X-0102").Status == EquipStatus.Quarantaene && !TestStatusMenuVisible("H1-X-0102"); }
                    finally { me.Rights.Add(Perm.SetStatus); }
                    okUi &= SetEquipStatus("H1-X-0102", null) && !equip.IsManual("H1-X-0102");
                }
                sb.AppendLine("Status im Plan: Rechtsklick-Wort=" + Ok(okWord) + " Setzen/Recht/Audit/Entfernen=" + Ok(okUi) + (had ? "" : " (Nutzer ohne Recht, übersprungen)"));
            }
        }
    }
}
#endif
