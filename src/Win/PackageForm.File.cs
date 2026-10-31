using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    partial class PackageForm : Form
    {
        // ---------- Paket laden/speichern/uebernehmen ----------

        void LoadPackage(Package p)
        {
            loading = true;
            steps.Clear(); cur = null;
            if (p != null) { p = p.Clone(); p.Migrate(); }   // Pakete, die nicht aus einer Datei kommen (Testpaket, Assistent): Schritte folgen dem Hauptlauf (Kopie, das Original bleibt)
            pk = new Package(); pk.Steps = steps;   // Kopf: Lauf 1 am, Typ, Wochentag von Tag 1
            if (p != null) { pk.Day1 = p.Day1; pk.Lauf1 = p.Lauf1; if (p.TypeIsSet) pk.Type = p.Type; }
            blocks = p != null ? p.Blocks : new List<PackageBlock>();
            tName.Text = p != null ? p.Name : ""; tProduct.Text = p != null ? p.ProductCode : "";
            prefix = p != null ? p.Prefix : "";   // alte Pakete: Produktcode (4 Ziffern) = Praefix
            string fr = p != null ? p.FirstRun : "";
            tFirst.Text = fr.Length > 0 ? fr : (prefix.Length > 0 ? Package.FormatRun(prefix, 1) : "");
            bool fresh = p == null || (p.Steps.Count == 0 && p.Blocks.Count == 0 && p.Name.Length == 0 && p.ProductCode.Length > 0);   // neues Paket mit vorbelegtem Produkt (Assistent)
            if (fresh) steps.AddRange(Package.DefaultCampaign(cat, NewRunNo()).Steps);   // Packen, Back-Up, Lauf, Puffer mobil und stationaer
            else steps.AddRange(p.Steps);
            lBlocks.Text = blocks.Count > 0 ? "Enthält zusätzlich " + blocks.Count + " fertige Blöcke aus einem Wochen-Export (werden mit übernommen)." : "";
            lBlocks.Visible = blocks.Count > 0;
            LoadHead();   // Typ, Lauf 1 am (PackageForm.Head.cs)
            loading = false;
            dirty = false;
            List<PackageStep> o = Ordered();
            Bind(o.Count > 0 ? o[0] : null);
            ResetUndo();
        }

        // null = Kopfzeile ungueltig (Produktcode, erste Laufnummer, Ueberlauf; Meldung wurde gezeigt). Schritte als Kopie: der Kampagnenstart nummeriert um.
        Package Collect()
        {
            Package.SyncBackups(steps);
            string err = ProductError() ?? FirstRunError() ?? PackNoError() ?? Package.BackupProblem(steps) ?? StepsView().Overflow();
            if (err != null)
            {
                MessageBox.Show(this, err, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                (ProductError() != null ? tProduct : FirstRunError() == null && PackNoError() != null ? PackNoBad() : tFirst).Focus(); return null;
            }
            Package p = new Package(); p.Name = tName.Text.Trim(); p.ProductCode = Product; p.RunPrefix = prefix;
            foreach (PackageStep s in steps) if (!s.IsEmpty) p.Steps.Add(s.Clone());
            p.Blocks = blocks;
            p.Lauf1 = pk.Lauf1; p.Day1 = pk.Day1;
            if (pk.TypeIsSet) p.Type = pk.Type;
            return p;
        }

        void Setup()
        {
            using (CatalogForm f = new CatalogForm(cat, rowNames))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); }
                catch (Exception ex) { MessageBox.Show(this, "Konnte nicht gespeichert werden.\n" + ex.Message, "Einrichten", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                cat = store.GetCatalog();
                BuildPosMenu();
                Bind(cur);
            }
        }

        void OpenFile()
        {
            if (!AskSave()) return;
            string fn = PackagePicker.Pick(this, Folder, canAdmin);
            if (fn == null) return;
            try { LoadPackage(Package.Load(fn)); file = fn; UpdateHeader(); }
            catch (Exception ex) { MessageBox.Show(this, "Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        bool SaveFile(bool saveAs)
        {
            Package p = Collect();
            if (p == null) return false;
            if (!BelongsOk(this, p, cat, "gespeichert")) { GoToBelongs(); return false; }   // U10: betroffene Karte/Zeile anspringen
            string f = file;
            if (saveAs || f == null)
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = p.Name.Length > 0 ? p.Name : "Paket";
                    PackagePicker.SetSaveFolder(d, Folder);
                    if (d.ShowDialog(this) != DialogResult.OK) return false;
                    f = d.FileName;
                }
            try { p.Save(f); }
            catch (Exception ex) { MessageBox.Show(this, "Paket konnte nicht gespeichert werden.\n" + ex.Message, "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            file = f; dirty = false; MarkSaved();
            UpdateHeader();
            return true;
        }

        void DoApply()
        {
            Package p = Collect();
            if (p == null) return;
            if (p.Steps.Count == 0 && p.Blocks.Count == 0) { MessageBox.Show(this, "Das Paket ist leer.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (PackageStep s in Ordered())
                if (!s.IsEmpty && (s.Row.Length == 0 || !rowNames.Contains(s.Row)))   // sonst fiele der Schritt beim Uebernehmen still weg
                {
                    Bind(s);
                    MessageBox.Show(this, "Schritt " + No(s) + " \"" + TitleOf(s) + "\" " + (s.Row.Length == 0 ? "hat keinen Raum." : "steht im Raum \"" + s.Row + "\", den es im Plan nicht gibt.")
                        + " Bitte unter \"Wo im Plan?\" einen Raum (Block) wählen.", "Paket-Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            if (!BelongsOk(this, p, store.GetCatalog(), "übernommen")) { GoToBelongs(); return; }   // U10
            DateTime st;
            if (StartCampaign(this, p, store, weekStart, By, out st)) { Applied = true; AppliedStart = st; }
        }

        // Kampagnenstart (Startdatum, erste Laufnummer, Termine je Lauf, Modus) und Uebernahme in den Plan; true = Plan geaendert.
        // Auch direkt aus dem Menue "Pakete > Kampagne starten…" (ohne Editor), wenn das Paket vollstaendig ist (Ready).
        public static bool StartCampaign(IWin32Window owner, Package p, IDataStore store, DateTime weekStart, User by, out DateTime start, string file = null)
        {
            List<RunDate> schedule; ApplyMode mode; Package res;
            start = weekStart;
            if (!BelongsOk(owner, p, store.GetCatalog(), "übernommen")) return false;
            using (CampaignForm cf = new CampaignForm(p, store, weekStart.Date, ApplyMode.FillEmpty, file != null))
            {
                if (cf.ShowDialog(owner) != DialogResult.OK) return false;
                start = cf.Start; schedule = cf.Schedule; mode = cf.Mode; res = cf.Result;   // Kopie mit Laufanzahl, Nummern, Ausfaellen
                if (cf.SaveCode)
                {
                    try { p.ProductCode = res.ProductCode; p.Save(file); }
                    catch (Exception ex) { MessageBox.Show(owner, "Der Produktcode konnte nicht im Paket gespeichert werden (gilt trotzdem für diese Kampagne).\n" + ex.Message, "Kampagne starten", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            }
            try
            {
                string msg = res.Apply(store, start, mode, schedule, by);
                MessageBox.Show(owner, msg, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                start = FirstRunDay(res, start, schedule);   // der Plan springt zur Woche von Lauf 1 (nicht zu Tag 1/Packen)
                return true;
            }
            catch (Exception ex) { MessageBox.Show(owner, "Konnte nicht gespeichert werden.\n" + ex.Message, "In Plan übernehmen", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        }

        // Datum des ersten stattfindenden Hauptlaufs (Schedule-Tag 1 = start); ohne Hauptlauf bleibt start
        public static DateTime FirstRunDay(Package p, DateTime start, List<RunDate> schedule)
        {
            int hi = p.Steps.IndexOf(p.Head), best = int.MaxValue;
            if (hi < 0 || schedule == null) return start;
            foreach (RunDate d in schedule) if (d.Step == hi && d.Day < best) best = d.Day;
            return best == int.MaxValue ? start : start.Date.AddDays(best - 1);
        }

        // Direkt startbar: hat Schritte, jeder (nicht leere) Schritt hat einen Raum, den es im Plan gibt
        public static bool Ready(Package p, IDataStore store)
        {
            if (p.Steps.Count == 0) return false;
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) names.Add(r.Name);
            foreach (PackageStep s in p.Steps) if (!s.IsEmpty && !names.Contains(s.Row)) return false;
            return true;
        }
    }
}
