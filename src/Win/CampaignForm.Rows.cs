using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class CampaignForm : Form
    {
        DateTime Proposed(int i) { return Start.AddDays(sched[i].Day - 1); }

        // Wochentag von Tag 1 so setzen, dass Wochentags-Wiederholungen auf echten Wochentagen liegen (haengt von "Lauf 1 am" ab)
        void SyncDay1()
        {
            DateTime l1 = dtLauf1.Value.Date, fix = pkg.SyncDay1(l1);
            day1Hint = null;
            if (fix != l1 && fix >= dtLauf1.MinDate && fix <= dtLauf1.MaxDate)
            {
                day1Hint = Package.Day1Hint + " – auf " + fix.ToString("dd.MM.yyyy") + " gesetzt.";
                bool b = building; building = true; dtLauf1.Value = fix; building = b;
            }
        }
        string day1Hint;

        // Vorschlaege neu berechnen (Datum/Laeufe/Nummern/Rhythmus geaendert); eigene Verschiebungen und Raeume bleiben je Lauf erhalten.
        void Recalc()
        {
            if (building) return;
            bool valid = pkg.FirstRunNo < 0 || pkg.SetFirstRun(tFirst.Text);   // 8 Ziffern: Praefix + Lauf; Folgelaeufe zaehlen hoch
            string pn = tPack.Text.Trim(); bool pv = PackageStep.IsManualNo(pn);
            if (pn != packLast) { foreach (PackageStep s in pkg.HandNoSteps()) s.ManualNo = pv ? pn : ""; packLast = pn; }   // unveraendert vorbelegt: Nummern der Schritte bleiben
            PackWarning = !needPack || pv ? null : (pn.Length == 0 ? "Laufnummer Packen fehlt (8 Ziffern, z. B. 12340001)." : "Laufnummer Packen: bitte 8 Ziffern eingeben.");
            lPack.Text = PackWarning ?? "→ Packen (Nr. " + pn + ")" + (pn == packInit ? " – aus dem Paket, hier änderbar" : ""); lPack.ForeColor = PackWarning == null ? SystemColors.ControlText : Color.Firebrick;
            SyncDay1();
            string over = pkg.Overflow();
            Package view = pkg.Clone();   // ohne Ausfaelle: auch ausgefallene Laeufe bleiben als Zeile sichtbar (zum Zuruecknehmen)
            foreach (PackageStep s in view.Steps) s.Skip.Clear();
            sched = view.Schedule(chkRhythm.Checked ? gaps : null);
            sched.Sort(delegate(RunDate a, RunDate b) { return a.Day != b.Day ? a.Day.CompareTo(b.Day) : (a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Run.CompareTo(b.Run)); });
            string firstLabel = "";
            foreach (RunDate d in sched) if (pkg.Steps[d.Step].Kind != StepKind.Normal) { firstLabel = d.Label; break; }
            lFirst.Text = !valid ? "bitte 8 Ziffern eingeben (z. B. 13570030)" : over ?? day1Hint ?? (firstLabel.Length > 0 ? "→ " + firstLabel : "(keine Läufe im Paket)");
            lFirst.ForeColor = valid && over == null ? SystemColors.ControlText : Color.Firebrick;
            Warning = valid ? over : lFirst.Text;
            filling = true;
            grid.Rows.Clear();
            for (int i = 0; i < sched.Count; i++) grid.Rows.Add(new object[] { sched[i].Label, "", Room(i), Ui.DayText(Proposed(i)), "", null, null, false });
            filling = false;
            skipped = new bool[sched.Count];
            UpdateAll();
        }

        // Alle Zeilen neu beschriften (Ausfaelle, Bezug, belegt, Datum) und Uebernehmen freigeben bzw. sperren
        void UpdateAll()
        {
            Dictionary<long, bool> real = new Dictionary<long, bool>();   // stattfindende Laeufe (mit Ausfaellen)
            foreach (RunDate d in pkg.Schedule(chkRhythm.Checked ? gaps : null)) real[d.Key()] = true;
            skipped = new bool[sched.Count];
            effHead = new List<RunDate>(); firstRunDay = int.MaxValue;
            int hi = HeadIdx();
            for (int i = 0; i < sched.Count; i++)
            {
                skipped[i] = !real.ContainsKey(sched[i].Key());
                if (sched[i].Step != hi || skipped[i]) continue;
                RunDate d = new RunDate(); d.Step = sched[i].Step; d.Run = sched[i].Run; d.Day = sched[i].Day + Off(i);
                effHead.Add(d); firstRunDay = Math.Min(firstRunDay, d.Day);
            }
            bool anyBusy = false;
            for (int i = 0; i < sched.Count; i++) { UpdateRow(i); anyBusy |= IsBusy(i); }
            if (!modeTouched) cbMode.SelectedIndex = anyBusy ? (int)ApplyMode.Append : (int)initialMode;   // Vorkampagne trifft belegte Bloecke: anhaengen
            Problems = pkg.Check(store, Start, Mode, Schedule);
            ShowRules();
            ok.Enabled = Warning == null && PackWarning == null && !Package.HasBlock(Problems);
            ShowWhy();
            SimpleSummary();
        }

        // Neben dem gesperrten Knopf: warum (kurz), Volltext im Tooltip
        void ShowWhy()
        {
            string why = "", full = "";
            if (!ok.Enabled)
            {
                PackageProblem bp = Problems.Find(delegate(PackageProblem p) { return p.Blocks; });
                if (PackWarning != null) { why = "Bitte die Laufnummer für Packen eintragen"; full = PackWarning; }
                else if (Warning != null) { why = "Erste Laufnummer prüfen"; full = Warning; }
                else if (bp != null) { string t = PackageProblem.RuleTitle(bp.Code); why = "Sperre: " + (t.Length > 0 ? t : pkg.Describe(bp)); full = pkg.Describe(bp); }
            }
            lWhy.Text = why; lWhy.Visible = why.Length > 0;
            if (full != whyTip) { whyTip = full; tip.SetToolTip(lWhy, full); }
        }
        string whyTip = "";

        // Regelverstoesse: betroffene Zeilen rot (Tooltip = Grund), erste Meldung unter der Tabelle
        void ShowRules()
        {
            for (int i = 0; i < sched.Count && i < grid.Rows.Count; i++) { grid.Rows[i].Cells[cLabel].Style.BackColor = Color.Empty; grid.Rows[i].Cells[cLabel].ToolTipText = ""; }
            List<PackageProblem> shown = Problems.FindAll(delegate(PackageProblem p) { return p.Code != "packen" && p.Code != "number"; });   // Packen-Nummer/Ueberlauf zeigen schon eigene Felder
            foreach (PackageProblem p in shown)
                for (int i = 0; i < sched.Count && i < grid.Rows.Count; i++)
                    if ((p.Run >= 0 && sched[i].Step == p.Step && sched[i].Run == p.Run) || (p.Run2 >= 0 && sched[i].Step == p.Step2 && sched[i].Run == p.Run2))   // beide Beteiligte rot
                    {
                        grid.Rows[i].Cells[cLabel].Style.BackColor = p.Blocks ? Rot : Belegt;
                        grid.Rows[i].Cells[cLabel].ToolTipText = pkg.Describe(p);
                    }
            PackageProblem first = shown.Find(delegate(PackageProblem p) { return p.Blocks; }) ?? (shown.Count > 0 ? shown[0] : null);
            lRule.Text = first == null ? "" : (first.Blocks ? "Gesperrt: " : "Hinweis: ") + pkg.Describe(first) + (shown.Count > 1 ? "  (+" + (shown.Count - 1) + " weitere)" : "");
            lRule.ForeColor = first != null && first.Blocks ? Color.Firebrick : Color.FromArgb(150, 60, 0);
        }

        // Zeile liegt vor Lauf 1 und der Block ist im Plan schon belegt (Vorkampagne: Packen/Puffer)
        bool IsBusy(int i)
        {
            if (skipped[i] || effHead.Count == 0 || sched[i].Day + Off(i) >= firstRunDay) return false;
            int id;
            if (!ids.TryGetValue(Room(i), out id)) return false;
            Entry e = store.Get(Start.AddDays(sched[i].Day + Off(i) - 1), id);
            return e != null && (!e.IsEmpty || e.Buffers.Count > 0);
        }

        void UpdateRow(int i)
        {
            if (i >= grid.Rows.Count) return;
            bool was = filling; filling = true;
            DataGridViewRow row = grid.Rows[i];
            bool sk = skipped[i], busy = IsBusy(i);
            row.Cells[cLabel].Value = sched[i].Label;
            row.Cells[cRel].Value = pkg.RelText(sched[i].Day + Off(i), effHead) + (busy ? " · belegt" : "");
            row.Cells[cRel].Style.BackColor = busy ? Belegt : Color.Empty;
            row.Cells[cProp].Value = Ui.DayText(Proposed(i));
            row.Cells[cWant].Value = Ui.DayText(Proposed(i).AddDays(Off(i)));
            row.Cells[cWant].Style.BackColor = Off(i) != 0 ? Orange : Color.Empty;
            row.Cells[cRoom].Value = Room(i);
            row.Cells[cRoom].Style.BackColor = Room(i) != sched[i].Row ? Orange : Color.Empty;   // Raumwechsel
            bool head = sched[i].Step == HeadIdx();
            row.Cells[cSkip].Value = sk && head;
            row.Cells[cSkip].ReadOnly = !head;
            foreach (DataGridViewCell c in row.Cells) { c.Style.ForeColor = sk ? Color.Gray : Color.Empty; c.Style.Font = sk && c.ColumnIndex != cSkip ? strike : null; }
            filling = was;
        }

        // Zeile i um delta Tage verschieben; mit "Folgende mitverschieben" auch alle Zeilen darunter.
        public void Shift(int i, int delta)
        {
            if (delta == 0 || i < 0 || i >= sched.Count) return;
            List<int> idx = new List<int>();
            for (int j = i; j < sched.Count && (j == i || chkShift.Checked); j++) idx.Add(j);
            foreach (int j in idx.ToArray()) foreach (int k in BackupRows(j)) if (!idx.Contains(k)) idx.Add(k);   // Back-Up folgt dem Packen
            foreach (int j in idx) offs[sched[j].Key()] = Off(j) + delta;
            UpdateAll();
        }

        // Lauf (Zeile des Hauptlaufs) faellt aus bzw. findet wieder statt: Luecke bei Laufnummer und Kennung, JeLauf-Schritte dieses Laufs entfallen
        public void SetSkip(int i, bool on)
        {
            if (i < 0 || i >= sched.Count || sched[i].Step != HeadIdx()) return;
            pkg.Head.SetSkip(sched[i].Run, on);
            UpdateAll();
        }

        // Zeilen der Back-Ups (gleicher Lauf), wenn Zeile j ein Packen-Schritt mit Back-Up ist
        List<int> BackupRows(int j)
        {
            List<int> r = new List<int>();
            for (int k = 0; k < sched.Count; k++)
                if (sched[k].Run == sched[j].Run && pkg.Steps[sched[k].Step].BacksUp(pkg.Steps[sched[j].Step])) r.Add(k);
            return r;
        }

        // Block der Zeile i aendern: gilt fuer alle Zeilen desselben Laufs im selben Raum (z. B. Lauf, Mischen, Backfill),
        // mit "Raum für ALLE Folgeläufe" zusaetzlich fuer alle spaeteren Zeilen dieses Raums.
        public void SetRoom(int i, string room, bool all)
        {
            if (i < 0 || i >= sched.Count || room.Length == 0) return;
            string orig = sched[i].Row; int g = sched[i].Group;
            for (int j = 0; j < sched.Count; j++)
            {
                if (j != i && (sched[j].Row != orig || !((all && j > i) || (g >= 0 && sched[j].Group == g)))) continue;
                SetRoomOf(j, room);
            }
            foreach (int k in BackupRows(i)) SetRoomOf(k, room);   // Back-Up im selben Raum
            UpdateAll();
        }

        void SetRoomOf(int j, string room) { if (room == sched[j].Row) rooms.Remove(sched[j].Key()); else rooms[sched[j].Key()] = room; }

        // Einzelpaket: ein Raum fuer alle Zeilen
        void SimpleRoom()
        {
            if (cbRoom.Text.Length == 0) return;
            for (int j = 0; j < sched.Count; j++) SetRoomOf(j, cbRoom.Text);
            UpdateAll();
        }

        void SimpleSummary()
        {
            if (!simple) return;
            List<string> l = new List<string>();
            for (int i = 0; i < sched.Count; i++) l.Add(sched[i].Label.Replace("\n", " ") + "  →  " + Ui.DayText(Proposed(i).AddDays(Off(i))) + ", " + Room(i) + (IsBusy(i) ? " (belegt)" : ""));
            lSum.Text = string.Join("\r\n", l.ToArray());
            FitSimple();
        }

        // Einzelpaket: Höhe nach Inhalt (Zusammenfassung, Hinweiszeile, darunter Modus und Knöpfe)
        void FitSimple()
        {
            if (foot == null) return;
            int th = TextRenderer.MeasureText(lSum.Text, lSum.Font, new Size(880, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            lSum.Height = Math.Max(40, th + 8);
            lRule.Top = lSum.Bottom + 4;
            int fy = lRule.Bottom + 8;
            foreach (Control c in foot) c.Top = fy + (c == foot[0] ? 6 : c == foot[1] ? 2 : 0);
            ClientSize = new Size(ClientSize.Width, fy + 28 + 12);
        }
    }
}
