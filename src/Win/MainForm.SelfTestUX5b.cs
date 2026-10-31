#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5b: Reihenfolge der Positionen im Modell (MoveEquip: Gruppengrenze, gemeinsame Zeilen, XML), Lauf-Angabe je Puffer (Plan,
    // Luecken, mehrere Laeufe am Tag, alter Bestand ohne Attribut) und Darstellung in der Wochenansicht. Editor und Vorschau: PackageForm.TestUx5b.cs.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static PackageStep UxStep(out Package pk)
        {
            pk = PackageForm.Ux5bSample("UX5b-Modell"); Package.Seen.Remove(pk);
            return pk.Steps[pk.Steps.Count - 1];
        }

        static string UxShorts(List<EquipAssign> l) { List<string> r = new List<string>(); foreach (EquipAssign a in l) r.Add(a.Short); return string.Join(",", r.ToArray()); }

        // Reihenfolge der Positionen im Plantext des Puffers k
        static string UxPlan(PackageStep s, int k)
        {
            List<string> r = new List<string>();
            foreach (string l in s.BuffersFor(0, "")[k].Text.Split('\n')) r.Add(l.Split(':')[0]);
            return string.Join(",", r.ToArray());
        }

        static EquipAssign UxEq(string sh, string id) { EquipAssign a = new EquipAssign(); a.Short = sh; a.Choices.Add(new EquipChoice(sh, id)); return a; }

        static PackageStep UxPuf(string name, int offset, bool je, int day, string dev)
        {
            PackageStep s = new PackageStep(); s.Title = name; s.Source = name; s.Row = "Puffer"; s.Day = day;
            PufferSpec sp = new PufferSpec(); sp.Name = name; sp.Typ = "mobil"; sp.Lines.Add(UxEq("Ansatztank", dev)); s.Puffers.Add(sp);
            if (je) { s.Bind = StepBind.JeLauf; s.RunOffset = offset; }
            s.SyncPuffers();
            return s;
        }

        static string Laufs(Entry e)
        {
            List<string> r = new List<string>();
            if (e != null) foreach (BufferItem b in e.Buffers) r.Add(b.Name + "=" + b.LaufRef);
            return string.Join("|", r.ToArray());
        }

        void SelfTestUX5b(StringBuilder sb)
        {
            // (1) Reihenfolge im Modell: innerhalb der Gruppe, Gruppengrenze, nichts zu tun
            Package pk; PackageStep s1 = UxStep(out pk);
            bool okOrder = UxPlan(s1, 0) == "Ansatztank,VT,Tween,WFI" && s1.MoveEquip(0, 1, 2) && UxShorts(s1.Puffers[0].Lines) == "Ansatztank,Tween,VT"
                && UxPlan(s1, 0) == "Ansatztank,Tween,VT,WFI" && UxPlan(s1, 1) == "Ansatztank,VT,WFI" && UxPlan(s1, 2) == "Ansatztank";   // andere Puffer unveraendert
            PackageStep s2 = UxStep(out pk);
            okOrder &= s2.MoveEquip(0, 0, 3) && UxPlan(s2, 0) == "VT,Tween,Ansatztank,WFI";   // Ziel hinter die eigenen Zeilen: ans Ende der eigenen Zeilen geklemmt
            okOrder &= !s2.MoveEquip(0, 3, 0) && !s2.MoveEquip(0, 1, 1) && !s2.MoveEquip(5, 0, 1) && !s2.MoveEquip(0, 9, 0) && !s2.MoveEquip(0, -1, 0) && UxPlan(s2, 0) == "VT,Tween,Ansatztank,WFI";
            // gemeinsame Zeilen: Reihenfolge gilt fuer alle betroffenen Puffer, Geltungsbereich bleibt
            PackageStep s3 = UxStep(out pk);
            PufferShare mat = new PufferShare(); mat.Eq = UxEq("Material", "M-1"); mat.Scope = 3; s3.PufferShared.Add(mat);
            okOrder &= UxPlan(s3, 0) == "Ansatztank,VT,Tween,WFI,Material" && UxPlan(s3, 1) == "Ansatztank,VT,WFI,Material" && UxPlan(s3, 2) == "Ansatztank";
            bool okShared = s3.MoveEquip(1, 2, 3) && UxPlan(s3, 0) == "Ansatztank,VT,Tween,Material,WFI" && UxPlan(s3, 1) == "Ansatztank,VT,Material,WFI" && UxPlan(s3, 2) == "Ansatztank"
                && s3.PufferShared[0] == mat && s3.PufferShared[0].Scope == 3 && s3.PufferShared[1].Scope == 3 && s3.PufferShared.Count == 2;
            okShared &= !s3.MoveEquip(1, 2, 0) && !s3.MoveEquip(1, 3, 3);   // gemeinsame Zeile nie zwischen die eigenen
            // XML-Rundweg (Paketformat unveraendert): Reihenfolge bleibt
            string xml = pk.ToXml();
            Package rt = Package.FromXml(xml); Package.Seen.Remove(rt);
            PackageStep r3 = rt.Steps[rt.Steps.Count - 1];
            bool okXml = UxPlan(r3, 0) == UxPlan(s3, 0) && UxPlan(r3, 1) == UxPlan(s3, 1) && UxPlan(r3, 2) == UxPlan(s3, 2) && r3.PufferShared.Count == 2 && r3.PufferShared[0].Eq.Short == "Material" && r3.PufferShared[0].Scope == 3
                && UxShorts(r3.Puffers[0].Lines) == "Ansatztank,VT,Tween";
            // Geltungsbereich aendern / Puffer entfernen nach dem Umsortieren: Reihenfolge bleibt
            s3.RemovePuffer(0);
            okShared &= UxPlan(s3, 0) == "Ansatztank,VT,Material,WFI" && s3.PufferShared[0].Scope == 1;
            // normale Schritte: Equipment-Liste
            PackageStep ns = new PackageStep(); ns.Title = "Normal"; ns.Row = "Suite 1";
            ns.Equipment.Add(UxEq("Pumpe", "P-1")); ns.Equipment.Add(UxEq("TFF", "T-1")); ns.Equipment.Add(UxEq("VS", "V-1"));
            bool okNormal = PackageStep.MoveItem(ns.Equipment, 0, 2) && UxShorts(ns.Equipment) == "TFF,VS,Pumpe" && ns.Compose(0, "").Replace("\n", "|") == "Normal|TFF: T-1|VS: V-1|Pumpe: P-1"
                && !PackageStep.MoveItem(ns.Equipment, 1, 1) && !PackageStep.MoveItem(ns.Equipment, 0, 3) && !PackageStep.MoveItem(ns.Equipment, -1, 0);
            Package nsp = new Package(); nsp.Steps.Add(ns);
            Package nsb = Package.FromXml(nsp.ToXml()); Package.Seen.Remove(nsb);
            okNormal &= UxShorts(nsb.Steps[0].Equipment) == "TFF,VS,Pumpe";
            sb.AppendLine("UX5b Reihenfolge Core: innerhalb Gruppe/Rand " + Ok(okOrder) + " gemeinsame Zeilen " + Ok(okShared) + " XML-Rundweg " + Ok(okXml) + " normaler Schritt " + Ok(okNormal));

            // (2) Lauf-Angabe je Puffer: Hauptlauf an Tag 3, 5, 9; Puffer je Lauf einen Tag davor (P1) bzw. danach (P2), frei vor/zwischen/nach den Laeufen
            Package lp = new Package(); lp.Name = "UX5b-Lauf";
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Day = 3; lf.StartNo = 1; lf.Repeat = RepeatMode.DayList; lf.DayList = "3, 5, 9";
            lp.Steps.Add(lf);
            lp.Steps.Add(UxPuf("P1", -1, true, 1, "AT-1")); lp.Steps.Add(UxPuf("P2", 1, true, 1, "AT-2"));
            lp.Steps.Add(UxPuf("Fa", 0, false, 1, "AT-3")); lp.Steps.Add(UxPuf("Fb", 0, false, 7, "AT-3")); lp.Steps.Add(UxPuf("Fc", 0, false, 12, "AT-3"));
            Package.Seen.Remove(lp);
            MemoryStore ms = new MemoryStore();
            int puf = PufferSetup.Row(ms.GetRows()).Id;
            DateTime d0 = new DateTime(2036, 3, 3);
            string ap = null;
            try { ap = lp.Apply(ms, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = "Ausnahme: " + ex.Message; }
            Func<int, Entry> E = delegate(int day) { return ms.Get(d0.AddDays(day - 1), puf); };
            bool okLauf = Laufs(E(1)) == "Fa=1" && Laufs(E(2)) == "P1=1" && Laufs(E(4)) == "P1=2|P2=1" && Laufs(E(6)) == "P2=2" && Laufs(E(7)) == "Fb=3" && Laufs(E(8)) == "P1=3" && Laufs(E(10)) == "P2=3" && Laufs(E(12)) == "Fc=0";
            bool okMulti = E(4) != null && E(4).Buffers.Count == 2 && E(4).Buffers[0].HeadText() == "P1 · Lauf 2" && E(4).Buffers[1].HeadText() == "P2 · Lauf 1" && E(12) != null && E(12).Buffers[0].HeadText() == "Fc";
            // Luecke: Lauf 2 faellt aus -> Nummern bleiben (Lauf 3 bleibt Lauf 3), kein Puffer fuer Lauf 2
            lf.Skip.Add(1);
            MemoryStore ms2 = new MemoryStore();
            try { lp.Apply(ms2, d0, ApplyMode.Overwrite, null); } catch (Exception ex) { ap = "Ausnahme: " + ex.Message; }
            Func<int, Entry> E2 = delegate(int day) { return ms2.Get(d0.AddDays(day - 1), puf); };
            bool okGap = Laufs(E2(4)) == "P2=1" && Laufs(E2(6)) == "" && Laufs(E2(8)) == "P1=3" && Laufs(E2(10)) == "P2=3" && Laufs(E2(7)) == "Fb=3";
            // XML: Attribut lauf wird geschrieben und gelesen; alter Bestand ohne Attribut zeigt keine Lauf-Angabe; Wert in Key/SameValue
            Package wk = Package.FromWeek(ms, d0, "x"); Package.Seen.Remove(wk);
            string wx = wk.ToXml();
            Package wb = Package.FromXml(wx); Package.Seen.Remove(wb);
            PackageBlock b4 = wb.Blocks.Find(delegate(PackageBlock b) { return b.Day == 4; });
            bool okLx = wx.Contains(" lauf=\"2\"") && b4 != null && b4.Data.Buffers.Count == 2 && b4.Data.Buffers[0].LaufRef == 2 && b4.Data.Buffers[1].LaufRef == 1;
            Package wo = Package.FromXml(wx.Replace(" lauf=\"", " x=\"")); Package.Seen.Remove(wo);
            PackageBlock o4 = wo.Blocks.Find(delegate(PackageBlock b) { return b.Day == 4; });
            okLx &= o4 != null && o4.Data.Buffers.Count == 2 && o4.Data.Buffers[0].LaufRef == 0 && o4.Data.Buffers[0].HeadText() == "P1" && o4.Data.Buffers[0].Name == "P1";
            byte[] bx = Backup.ToXml(ms.Snapshot());   // Sicherung/Datendatei: gleiches Format
            okLx &= Encoding.UTF8.GetString(bx).Contains(" lauf=\"2\"");
            BufferItem k1 = E(4) != null && E(4).Buffers.Count > 0 ? E(4).Buffers[0].Clone() : new BufferItem(), k2 = k1.Clone(); k2.LaufRef = 0;
            okLx &= k1.LaufRef == 2 && !k1.SameValue(k2) && k1.Key() != k2.Key() && k1.SameValue(k1.Clone()) && k2.HeadText() == "P1";
            sb.AppendLine("UX5b Lauf-Angabe: Plan je Lauf " + Ok(okLauf) + " mehrere Läufe am Tag, eigener Kopf " + Ok(okMulti) + " Lücke bleibt " + Ok(okGap) + " XML/alter Bestand " + Ok(okLx) + (ap != null && ap.StartsWith("Ausnahme") ? " " + ap : "") + " [" + Laufs(E(4)) + "]");

            sb.AppendLine(SelfTestUX5bPlan());   // Wochenansicht
            sb.AppendLine(PackageForm.TestUx5b(store, equip, start));   // Editor, Vorschau (PackageForm.TestUx5b.cs)
        }

        // Wochenansicht: Puffer eines Tages (Donnerstag, 3 Puffer) getrennt: Kopf mit Lauf, Abstand, Farbwechsel, gleiche Zeilenhoehe, keine Ueberlappung
        string SelfTestUX5bPlan()
        {
            if (systemPc) return "UX5b Wochenansicht: übersprungen (Systemrechner)";
            DateTime keep = start;
            try
            {
                int pr = RowOf("Puffer");
                if (pr < 0) return "UX5b Wochenansicht: Zeile Puffer fehlt";
                DateTime wk = D(2035, 1, 1);
                Package p = PackageForm.Ux5bSample("UX5b-Plan"); Package.Seen.Remove(p);
                PackageStep pu = p.Steps[p.Steps.Count - 1];
                pu.Puffers[0].Lines[2].Note = "mit einer längeren Notiz, damit der Text im schmalen Feld umbricht und die Höhe wächst";
                string res = p.Apply(store, wk, ApplyMode.Overwrite, null);
                start = wk; Rebuild(true); Expand(pr);
                CellData c = CellOf(pr, 3);   // Donnerstag = Hauptlauf (Mittwoch) + 1
                bool okShow = c != null && c.BufParts.Count == 3 && c.Buffers[0].LaufRef == 1 && c.Buffers[2].LaufRef == 1;
                string h0 = okShow ? UxHead(c.BufParts[0]) : "", h1 = okShow ? UxHead(c.BufParts[1]) : "", h2 = okShow ? UxHead(c.BufParts[2]) : "";
                bool okHead = h0.StartsWith(Package.PlanBufferName(p.ProductCode, "Lösung A")) && h0.EndsWith("für " + p.LaufNoOf(1, wk.DayOfWeek)) && h1.StartsWith(Package.PlanBufferName(p.ProductCode, "Lösung B")) && h1.EndsWith("für " + p.LaufNoOf(1, wk.DayOfWeek)) && h2.StartsWith(Package.PlanBufferName(p.ProductCode, "Lösung C")) && h2.EndsWith("für " + p.LaufNoOf(1, wk.DayOfWeek));
                bool okGap = okShow, okOver = okShow;
                if (okShow)
                    for (int i = 0; i + 1 < c.BufParts.Count; i++)
                    {
                        BufPart a = c.BufParts[i], b = c.BufParts[i + 1];
                        okGap &= b.Y - (a.Y + a.H) >= 8 && b.Y - (a.Y + a.H) <= 12;
                        okOver &= b.Y >= a.Y + a.H && a.H >= a.Head.Height + (a.Body != null ? a.Body.Height : 0);
                    }
                if (okShow) { BufPart l = c.BufParts[2]; okOver &= c.RBuf.Height >= l.Y + l.H && c.RBuf.Bottom <= c.RContent.Top && c.RBuf.Top == c.RChecks.Bottom; }
                bool okTint = BufStyle.Tint(0) != BufStyle.Tint(1) && BufStyle.Tint(1) != BufStyle.Tint(2);
                // Zeilenhoehe: alle 7 Spalten der Zeile gleich hoch, der Block waechst mit
                int h = c != null ? c.R.Height : -1; bool okRow = c != null;
                for (int col = 0; col < 7 && okRow; col++) { CellData x = CellOf(pr, col); okRow &= x != null && x.R.Height == h && x.R.Top == c.R.Top; }
                okRow &= c != null && h > 3 * Font.Height;
                // alter Bestand ohne Lauf-Angabe: Kopf ohne "Lauf"
                Entry old = store.Get(wk.AddDays(3), rowInfo[pr].Id);
                foreach (BufferItem bi in old.Buffers) bi.LaufRef = 0;
                Entry oc = old.Clone(); oc.Version = old.Version;
                store.Save(wk.AddDays(3), rowInfo[pr].Id, oc);
                Rebuild(true); Expand(pr);
                CellData c2 = CellOf(pr, 3);
                bool okOld = c2 != null && c2.BufParts.Count == 3 && UxHead(c2.BufParts[0]).StartsWith(Package.PlanBufferName(p.ProductCode, "Lösung A")) && !UxHead(c2.BufParts[0]).Contains("Lauf");
                return "UX5b Wochenansicht: 3 Puffer " + Ok(okShow) + " Kopf mit Lauf " + Ok(okHead) + " Abstand " + Ok(okGap) + " keine Überlappung " + Ok(okOver) + " Farbwechsel " + Ok(okTint)
                    + " Zeilenhöhe synchron " + Ok(okRow) + " alter Bestand ohne Lauf " + Ok(okOld) + (res != null && res.StartsWith("Ausnahme") ? " " + res : "");
            }
            catch (Exception ex) { return "UX5b Wochenansicht: Ausnahme " + ex.GetType().Name + ": " + ex.Message; }
            finally { start = keep; Rebuild(true); }
        }

        static string UxHead(BufPart p)
        {
            StringBuilder s = new StringBuilder();
            foreach (List<Item> ln in p.Head.Lines) foreach (Item it in ln) s.Append(it.Text);
            return s.ToString();
        }
    }
}
#endif
