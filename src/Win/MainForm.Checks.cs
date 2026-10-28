using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pflichtpunkte je Block (LKZ, Tagesprobe, Wochen-WFI …), Variante, Rundgaenge je KW, Einstellungen dazu.
    partial class MainForm
    {
        DutyConfig duties = new DutyConfig();
        readonly Dictionary<DateTime, bool> roundFold = new Dictionary<DateTime, bool>();   // Rundgangsleiste je KW, nur lokal
        readonly ContextMenuStrip variantMenu = new ContextMenuStrip();

        // Pflichtpunkte je Zeile (LKZ aus der Blockstruktur automatisch), Zustand je Block, Kopf-Hinweis "LKZ KW44 offen"
        // fuer begonnene KWs ohne Haken, Rundgaenge je KW. Nachbarwochen (Fenster Sa-Fr) liest CheckEval selbst aus dem Store.
        void ComputeChecks()
        {
            ComputeChecks(view, start);
#if TESTHOOKS
            if (week2 != null && week2.View.Start == start.AddDays(7)) { ComputeChecks(week2.View, week2.View.Start); week2.View.Invalidate(); }   // 2-Wochen-Ansicht: gleiche Zustaende
#endif
        }

        // Pflichtpunkte je Zeile (wie MainForm, LKZ aus der Blockstruktur), auch fuer die zweite Woche
        List<CheckDef>[] DefsByRow()
        {
            List<CheckDef>[] a = new List<CheckDef>[rowInfo.Count];
            for (int r = 0; r < rowInfo.Count; r++)
            {
                int id = rowInfo[r].Id;
                List<CheckDef> l = duties.Defs.FindAll(delegate(CheckDef d) { return d.RowId == id; });
                if (rowInfo[r].HasLkz && !l.Exists(delegate(CheckDef d) { return d.Key == CheckDef.LkzKey; })) l.Insert(0, CheckDef.Lkz(id));
                a[r] = l;
            }
            return a;
        }

        void ComputeChecks(PlanView v, DateTime from)
        {
            DateTime today = DateTime.Today;
            v.Defs = DefsByRow();
            Dictionary<string, bool> open = new Dictionary<string, bool>();   // Zeile|Pflichtpunkt|Montag -> KW offen
            foreach (CellData c in v.Cells)
            {
                if (c.RowIndex >= v.Defs.Length) continue;
                List<CheckDef> l = v.Defs[c.RowIndex];
                c.States = new DayState[l.Count];
                DateTime mon = IsoWeek.Monday(c.Day);
                List<string> o = new List<string>();
                for (int i = 0; i < l.Count; i++)
                {
                    c.States[i] = CheckEval.Day(store, l[i], c.Day, today, duties);
                    string k = c.RowIndex + "|" + i + "|" + mon.Ticks;
                    bool wo;
                    if (!open.TryGetValue(k, out wo))
                    {
                        wo = false;
                        for (int d = 0; d < 7 && !wo && mon <= today; d++) wo = CheckEval.Day(store, l[i], mon.AddDays(d), today, duties) == DayState.WeekOpen;
                        open[k] = wo;
                    }
                    if (wo) o.Add(l[i].Short);
                }
                c.Note = o.Count == 0 ? "" : string.Join(", ", o.ToArray()) + " KW" + IsoWeek.Number(c.Day) + " offen";
            }
            ComputeRounds(v, from);
        }

        void ComputeRounds() { ComputeRounds(view, start); }

        // Eine Gruppe je KW im Fenster; jede KW zaehlt nur ihre eigenen Haken.
        void ComputeRounds(PlanView v, DateTime from)
        {
            v.Rounds.Clear();
            List<RoundDef> act = duties.Rounds.FindAll(delegate(RoundDef d) { return d.Active; });
            if (act.Count == 0) return;
            for (int c = 0; c < 7; c++)
            {
                DateTime mon = IsoWeek.Monday(from.AddDays(c));
                RoundGroup g = v.Rounds.Count > 0 ? v.Rounds[v.Rounds.Count - 1] : null;
                if (g != null && g.Monday == mon) { g.Last = c; continue; }
                g = new RoundGroup(); g.Monday = mon; g.First = g.Last = c; g.Defs = act;
                foreach (WeekMark m in store.GetWeekMarks(mon)) if (m.Done) g.Done.Add(m.Key);
                bool f;
                g.Collapsed = roundFold.TryGetValue(mon, out f) ? f : g.Complete;   // Standard: offen, solange etwas fehlt
                v.Rounds.Add(g);
            }
        }

        // ---------- Haken und Variante am Block ----------

        void ClickMark(CellData c, string key) { if (key == CheckDef.LkzKey) ToggleLkz(c); else ToggleMark(c, key); }

        void ToggleLkz(CellData c)
        {
            bool want = !c.Lkz;                     // Ziel = Gegenteil dessen, was der Nutzer sieht
            Change(c, delegate(CellData x) { x.Lkz = want; }, delegate(Entry e) { return e.Lkz == want; }, delegate(Entry e) { e.Lkz = want; });
        }

        void ToggleMark(CellData c, string key)
        {
            bool want = !c.Marks.Contains(key);
            Change(c, delegate(CellData x) { SetMark(x.Marks, key, want); }, delegate(Entry e) { return e.Marks.Contains(key) == want; }, delegate(Entry e) { SetMark(e.Marks, key, want); });
        }

        static void SetMark(List<string> l, string key, bool on) { l.Remove(key); if (on) l.Add(key); }

        void SetVariant(CellData c, string v)
        {
            Change(c, delegate(CellData x) { x.Variant = v; }, delegate(Entry e) { return e.Variant == v; }, delegate(Entry e) { e.Variant = v; });
        }

        // Mehrfachauswahl: Haken je Variante (an einem Tag z. B. mobil UND stationaer), "keine Angabe" leert
        void ChooseVariant(CellData c, Point screen)
        {
            variantMenu.Items.Clear();
            List<string> all = new List<string>(rowInfo[c.RowIndex].VariantList()); all.Add("");
            foreach (string v in all)
            {
                string x = v;
                ToolStripMenuItem it = new ToolStripMenuItem(v.Length > 0 ? v : "keine Angabe");
                it.Checked = v.Length > 0 ? VariantSet.Contains(c.Variant, v) : c.Variant.Length == 0;
                it.Click += delegate { SetVariant(c, x.Length > 0 ? VariantSet.Toggle(c.Variant, x) : ""); };
                variantMenu.Items.Add(it);
            }
            variantMenu.Show(screen);
        }

        // Haken/Variante: im Bearbeitungsmodus nur im Editor (wird mit dem Block gespeichert), sonst sofort speichern.
        // Hat ein anderer PC den Block geaendert, bleibt dessen Inhalt; nur diese Aenderung wird (einmal still erneut) gesetzt.
        void Change(CellData c, Action<CellData> local, Func<Entry, bool> already, Action<Entry> apply)
        {
            if (!c.Editing) FreshCur(c);
            if (!CanEdit(c)) return;
            if (c.Editing) { local(c); UpdateChanged(); ComputeChecks(); view.Relayout(); return; }
            RowInfo ri = rowInfo[c.RowIndex];
            if (already(c.Cur)) { c.Show(c.Cur); ComputeChecks(); view.Relayout(); return; }   // anderer PC hat es schon so gesetzt
            Entry n = c.Cur.Clone(); apply(n);
            try
            {
                bool ok = Audit.Save(store, c.Day, ri, me, n);
                if (!ok)
                {
                    Entry latest = store.Get(c.Day, ri.Id);
                    n = latest != null ? latest.Clone() : new Entry(); apply(n);
                    ok = (latest != null && already(latest)) || Audit.Save(store, c.Day, ri, me, n);
                }
                if (!ok) { if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden, weil der Block gerade geändert wird. Bitte erneut klicken.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            catch (Exception ex)
            {
                if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden.\n" + ex.Message, "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SetCur(c, n);
            ComputeChecks();
            view.Relayout();
        }

        // ---------- Rundgaenge ----------

        void ToggleRoundFold(RoundGroup g)
        {
            g.Collapsed = !g.Collapsed; roundFold[g.Monday] = g.Collapsed;
            view.Relayout();
        }

        // Klick speichert sofort (mit Protokoll); Konflikt wie beim LKZ-Haken: neu lesen, einmal still erneut.
        void ToggleRound(RoundGroup g, string key)
        {
            if (!me.Has(Perm.Rounds))
            {
                if (!quiet) MessageBox.Show("Rundgänge abhaken dürfen nur Nutzer mit dem Recht \"Rundgänge abhaken\".", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool want = !g.Done.Contains(key);
            RoundDef def = g.Defs.Find(delegate(RoundDef d) { return d.Key == key; });
            try
            {
                if (fstore != null) fstore.Refresh();
                bool ok = false;
                for (int round = 0; round < 2 && !ok; round++)
                {
                    WeekMark cur = store.GetWeekMarks(g.Monday).Find(delegate(WeekMark m) { return m.Key == key; });
                    if (cur != null && cur.Done == want) { ok = true; break; }   // anderer PC hat schon so gehakt
                    WeekMark w = cur ?? new WeekMark();
                    w.Monday = g.Monday; w.Key = key; w.Done = want;
                    AuditEntry a = new AuditEntry(); a.Time = DateTime.Now; a.Login = me.Login; a.Kuerzel = me.Kuerzel; a.RowName = def != null ? def.Name : key;
                    ok = store.SaveWeekMark(w, a);
                }
                if (!ok) { if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden. Bitte erneut klicken.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            catch (Exception ex)
            {
                if (!quiet) MessageBox.Show("Der Haken konnte nicht gespeichert werden.\n" + ex.Message, "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ComputeRounds();
            view.Relayout();
        }

        void ShowRoundHistory(RoundGroup g)
        {
            string title = "Rundgänge KW" + IsoWeek.Number(g.Monday) + " (" + g.Monday.ToString("dd.MM.yyyy") + " - " + g.Monday.AddDays(6).ToString("dd.MM.yyyy") + ")";
            using (HistoryForm f = new HistoryForm(title, store.GetAudit(g.Monday, -1), new[] { "F", "S", "N" }, false)) f.ShowDialog(this);
        }

        void EditDuties()
        {
            if (!EndEdit(true)) return;
            using (DutiesForm f = new DutiesForm(store.GetDuties(), store.GetRows()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    store.SaveDuties(f.Result);
                    List<RowInfo> rows = store.GetRows();
                    RowInfo pr = f.PufferVariants ? PufferSetup.Row(rows) : null;   // Knopf "Standard Puffer einrichten": Varianten nur, wenn noch keine
                    if (pr != null && pr.Variants.Length == 0) { pr.Variants = PufferSetup.Variants; store.SaveStructure(rows); }
                }
                catch (Exception ex) { ShowSaveError(ex); return; }
            }
            Rebuild(false);
        }

        static string NoPufferFile { get { return System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arbeitsplan"), "keine-puffer-standards"); } }

        // Alter Bestand ohne Puffer-Standards (Pflichtpunkte/Varianten): nach dem Start nur Admins (PackageAdmin) fragen, nie ungefragt,
        // vorhandene Einstellungen bleiben. "Nein" wird auf diesem PC gemerkt (Knopf in "Pflichtpunkte & Feiertage" bleibt).
        public void OfferPufferDefaults()
        {
            if (shownPlan != null || systemPc || !me.Has(Perm.PackageAdmin) || (fstore != null && (fstore.Offline || fstore.ReadOnly))) return;
            if (System.IO.File.Exists(NoPufferFile) || !PufferSetup.Needed(store.GetRows(), store.GetDuties())) return;
            DialogResult r = MessageBox.Show(this, "Standard-Pflichtpunkte für Puffer einrichten?\n\nTagesprobe werktags (mobil = automatisch), WFI-Probe je KW (entfällt bei stationär) "
                + "und Herstellung mobil/stationär an der Zeile \"Puffer\". Vorhandene Einstellungen bleiben unverändert.\n\n"
                + "Später jederzeit: Einstellungen → Pflichtpunkte & Feiertage → \"Standard Puffer einrichten\".", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
            {
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(NoPufferFile)); System.IO.File.WriteAllText(NoPufferFile, DateTime.Now.ToString("s")); } catch (Exception) { }
                return;
            }
            if (!EndEdit(true)) return;
            try { PufferSetup.Apply(store, me); } catch (Exception ex) { ShowSaveError(ex); return; }
            Rebuild(false);
        }
    }
}
