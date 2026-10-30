using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Pruefhinweise (UX-2): Regelprobleme mit Beteiligten, Sperren vor Hinweisen, gleiche Regeln gruppiert,
    // Klick springt zum ersten Beteiligten (zweiter wird im Kalender rot umrandet); nicht validiertes Equipment.
    partial class PackageForm
    {
        const int GroupFrom = 3;                                          // ab so vielen gleichen Problemen: Gruppenkopf mit Zaehler
        readonly HashSet<string> openGroups = new HashSet<string>();      // aufgeklappte Gruppen (Code|Sperre)
        readonly HashSet<string> notedIds = new HashSet<string>();        // fuer den Admin vorgemerkte Nummern (ohne Recht)

        Hint RuleHint(PackageProblem p, List<PackageStep> o)
        {
            PackageStep ps = p.Step >= 0 && p.Step < steps.Count ? steps[p.Step] : null;
            PackageStep ps2 = p.Step2 >= 0 && p.Step2 < steps.Count ? steps[p.Step2] : null;
            int ix = ps != null ? o.IndexOf(ps) : -1;
            Control fld = p.Code == "ident" ? tIdent : (p.Code == "number" ? (ps != null && ps.Kind != StepKind.Normal ? (Control)tFirst : nStartNo) : null);
            Hint h = new Hint((p.Blocks ? "SPERRE – " : p.IsInfo ? "Info – " : "") + (ix >= 0 ? "Schritt " + (ix + 1) + ": " : "") + p.Full, ps, fld);
            h.Blocks = p.Blocks; h.Info = p.IsInfo; h.Code = p.Code; h.Run = p.Run; h.Step2 = ps2; h.Run2 = p.Run2;
            if (p.Code == "pufdoppelt") NoteDup(p, ps, ps2);   // betroffene Karten rot (PackageForm.PufferCards.cs)
            return h;
        }

        // Alle Regelprobleme: Sperren zuerst; ab GroupFrom gleichen Problemen ein Gruppenkopf (Kids = Einzelhinweise)
        List<Hint> RuleHints(List<PackageStep> o)
        {
            List<PackageProblem> ps = StepsView().Check();
            List<PackageProblem> sorted = ps.FindAll(delegate(PackageProblem p) { return p.Blocks; });
            sorted.AddRange(ps.FindAll(delegate(PackageProblem p) { return !p.Blocks; }));
            List<Hint> res = new List<Hint>();
            Dictionary<string, Hint> heads = new Dictionary<string, Hint>();
            foreach (PackageProblem p in sorted)
            {
                Hint h = RuleHint(p, o);
                string key = p.Code + "|" + p.Blocks, title = PackageProblem.RuleTitle(p.Code);
                if (title.Length == 0) { res.Add(h); continue; }
                Hint head;
                if (!heads.TryGetValue(key, out head)) { head = new Hint("", null, null); head.Code = p.Code; head.Blocks = p.Blocks; head.Kids = new List<Hint>(); head.Text = key; heads[key] = head; res.Add(head); }
                head.Kids.Add(h);
            }
            for (int i = 0; i < res.Count; i++)
            {
                Hint head = res[i];
                if (head.Kids == null) continue;
                if (head.Kids.Count < GroupFrom) { res.RemoveAt(i); res.InsertRange(i, head.Kids); i += head.Kids.Count - 1; continue; }
                string key = head.Text; head.Open = openGroups.Contains(key);
                head.Step = head.Kids[0].Step; head.Run = head.Kids[0].Run;
                foreach (Hint k in head.Kids) k.Indent = true;
                SetGroupText(head, key);
            }
            return res;
        }

        void SetGroupText(Hint head, string key)
        {
            head.Text = (head.Blocks ? "SPERRE – " : "") + (head.Open ? "▾ " : "▸ ") + head.Kids.Count + " Probleme: " + PackageProblem.RuleTitle(head.Code) + (head.Open ? "" : " (Klick = aufklappen)");
            head.Field = null; head.UnvalId = null;
            head.Code = key.Substring(0, key.IndexOf('|'));
        }

        // Sperren (rote Hinweise) vor den uebrigen, sonst Reihenfolge behalten
        static List<Hint> BlocksFirst(List<Hint> h)
        {
            List<Hint> r = h.FindAll(delegate(Hint x) { return x.Blocks; });
            r.AddRange(h.FindAll(delegate(Hint x) { return !x.Blocks && !x.Info; }));
            r.AddRange(h.FindAll(delegate(Hint x) { return x.Info; }));   // Infos zuletzt
            return r;
        }

        // Liste befuellen: Gruppenkoepfe immer, Einzelhinweise nur bei aufgeklappter Gruppe
        void FillHintList(List<Hint> h)
        {
            hintList = new List<Hint>(); shownHints = new List<Hint>();
            foreach (Hint x in h)
            {
                hintList.Add(x); shownHints.Add(x);
                if (x.Kids != null) foreach (Hint k in x.Kids) { hintList.Add(k); if (x.Open) shownHints.Add(k); }
            }
            lbHints.BeginUpdate();
            lbHints.Items.Clear();
            foreach (Hint x in shownHints) lbHints.Items.Add(x);
            lbHints.EndUpdate();
        }

        // Gruppenkopf auf-/zuklappen (Hinweisliste neu fuellen, ohne alles neu zu pruefen)
        void ToggleGroup(Hint head)
        {
            string key = head.Code + "|" + head.Blocks;
            head.Open = !head.Open;
            if (head.Open) openGroups.Add(key); else openGroups.Remove(key);
            SetGroupText(head, key);
            List<Hint> top = hintList.FindAll(delegate(Hint x) { return !x.Indent; });
            FillHintList(top);
        }

        void SelectRun(PackageStep s, int run)
        {
            if (s != cur) { nextRun = run; Bind(s); return; }
            int ix = curDates.FindIndex(delegate(RunDate d) { return d.Run == run; });
            if (ix >= 0) GoPage(ix);
        }

        // ---------- nicht validiertes Equipment ----------

        Hint EquipHint(string n, EquipAssign a, string id, PackageStep s)
        {
            bool unknown = cat.Master.Device(id) == null;
            if (!unknown)
                return new Hint(n + ": Gerät " + id + " (" + a.Short + ") ist " + (cat.Product(Product) != null ? "für dieses Produkt nicht vorgesehen." : "nicht validiert."), s, null);
            string head = n + ": Gerät " + id + " (" + a.Short + "): Nicht in der Equipment-Liste (nicht validiert). ";
            NoteUnknown(id, a.Short, s);   // fuer die Leiste (PackageForm.EquipBar.cs)
            string tail = canAdmin ? "Klick: „Zur Liste hinzufügen…“"
                : (IsNoted(id) ? "Vorgemerkt – der Admin muss die Nummer noch aufnehmen." : "Bitte Admin bitten, die Nummer aufzunehmen (Klick = vormerken).");
            Hint h = new Hint(head + tail, s, null); h.UnvalId = id; h.UnvalShort = a.Short;
            return h;
        }

        // Klick auf einen nicht validierten Hinweis: Admin traegt das Geraet ein, sonst Vormerkung
        void AddToList(string id, string shortName)
        {
#if TESTHOOKS
            if (Environment.CommandLine.Contains("--selftest") || Environment.CommandLine.Contains("--hintclick")) return;   // Klick-Tests duerfen keinen modalen Dialog oeffnen
#endif
            if (!canAdmin)
            {   // dauerhaft vormerken (Katalog); Rueckmeldung als Leiste, wenn der Schritt gerade offen ist
                Unk u = unk.Find(delegate(Unk x) { return x.Id == id && x.Step == cur; }) ?? new Unk();
                if (u.Id == null) { u.Id = id; u.Short = shortName; u.Step = cur; }
                if (!IsNoted(id)) Inform(u); else UpdateHints();
                return;
            }
            using (EquipAddForm f = new EquipAddForm(id, cat, shortName))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                AddDevice(f.Device, shortName);
            }
        }

        // Geraet in Stammdaten und Positionsliste aufnehmen, Katalog speichern, Zeilen neu aufbauen (werden validiert)
        internal bool AddDevice(EquipDevice d, string shortName)
        {
            if (!canAdmin || d == null || d.Id.Length == 0) return false;
            if (cat.Master.Device(d.Id) == null) cat.Master.Devices.Add(d);
            EquipPosition pos = cat.Position(shortName);
            if (pos != null && !pos.Ids.Contains(d.Id)) pos.Ids.Add(d.Id);
            cat.Master.RemovePending(d.Id);   // eine Vormerkung ist damit erledigt
            try { store.SaveCatalog(cat); }
            catch (Exception ex) { MessageBox.Show(this, "Die Equipment-Liste konnte nicht gespeichert werden:\n" + ex.Message, "Speichern", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            notedIds.Remove(d.Id);
            if (cur != null) Bind(cur); else UpdateHints();
            return true;
        }
    }
}
