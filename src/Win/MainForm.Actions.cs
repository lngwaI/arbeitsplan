using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Bildschirmanpassung, Einstellungen (Struktur, Katalog), Meine Schichten, Suche, Export und Pakete.
    partial class MainForm
    {
        // ---------- Bildschirmanpassung (4:3 / 16:9, mehrere Monitore) ----------
        Screen lastScreen;
        bool filled = true;   // Fenster fuellt den Arbeitsbereich seines Bildschirms

        static bool Fills(Rectangle b, Rectangle wa)
        {
            return Math.Abs(b.Width - wa.Width) < 40 && Math.Abs(b.Height - wa.Height) < 40;
        }

        // Start: den Arbeitsbereich des Bildschirms fuellen, auf dem die Maus steht (dort wurde das Programm gestartet)
        public void FitToCursorScreen()
        {
            Screen s = Screen.FromPoint(Cursor.Position);
            StartPosition = FormStartPosition.Manual;
            Bounds = s.WorkingArea;
            WindowState = FormWindowState.Maximized;   // UX-4: Start im Vollbild (Arbeitsbereich), auch Testdaten/Demo; Shot/Selbsttest rufen das nicht auf
            lastScreen = s; filled = true;
        }

        // Fenster auf einen anderen Bildschirm gezogen (oder Aufloesung geaendert): passt Groesse an, damit nie Tage ausserhalb liegen oder Platz fehlt
        void RefitIfScreenChanged()
        {
            if (WindowState != FormWindowState.Normal) { lastScreen = Screen.FromControl(this); return; }
            Screen s = Screen.FromControl(this);
            if (lastScreen == null) lastScreen = s;
            if (s.DeviceName != lastScreen.DeviceName)
            {
                Rectangle wa = s.WorkingArea;
                if (filled || Width > wa.Width || Height > wa.Height) Bounds = wa;
                else Location = new Point(Math.Max(wa.Left, Math.Min(Left, wa.Right - Width)), Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height)));
                lastScreen = s;
            }
            else if (!Fills(Bounds, s.WorkingArea) && !Fills(Bounds, lastScreen.WorkingArea)) filled = false;
            else filled = Fills(Bounds, s.WorkingArea);
            if (Width > s.WorkingArea.Width || Height > s.WorkingArea.Height) Bounds = s.WorkingArea;
        }

        void ResizeView()
        {
            if (inResize) return;
            inResize = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int cw = canvas.ClientSize.Width;
                    view.Width = cw;
                    view.MarkAllDirty();
                    view.Relayout();
                    if (canvas.ClientSize.Width == cw) break;   // Scrollbalken kann die Breite aendern
                }
                for (int c = 0; c < 7; c++) dateLabels[c].SetBounds(view.ColLeft(c), 2, view.ColW, 22);
            }
            finally { inResize = false; }
        }

        // ---------- Einstellungen: Blockstruktur ----------

        void ShowPersons()
        {
            if (!EndEdit(true)) return;
            using (PersonForm f = new PersonForm(store, me.Kuerzel, MondayOf(DateTime.Today))) f.ShowDialog(this);
        }

        void EditStructure()
        {
            if (!EndEdit(true)) return;
            List<RowInfo> copy = store.GetRows();
            using (StructureForm f = new StructureForm(copy, me.Has(Perm.EditNames), me.Has(Perm.EditHeight), me.Has(Perm.SortRows)))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveStructure(f.Result); } catch (Exception ex) { ShowSaveError(ex); return; }
            }
            Rebuild(false);
        }

        void EditCatalog()
        {
            List<string> names = new List<string>();
            foreach (RowInfo r in store.GetRows()) if (r.Name.Length > 0) names.Add(r.Name);
            List<string> vars = new List<string>();
            foreach (RowInfo r in store.GetRows()) foreach (string v in r.VariantList()) if (!vars.Contains(v)) vars.Add(v);
            using (CatalogForm f = new CatalogForm(store.GetCatalog(), names, vars.Count > 0 ? vars.ToArray() : null))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); } catch (Exception ex) { ShowSaveError(ex); }
            }
        }

        void EditMaster()
        {
            using (EquipMasterForm f = new EquipMasterForm(store.GetCatalog()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); } catch (Exception ex) { ShowSaveError(ex); }
            }
        }

        void EditProducts()
        {
            using (ProductForm f = new ProductForm(store.GetCatalog()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try { store.SaveCatalog(f.Result); } catch (Exception ex) { ShowSaveError(ex); }
            }
        }

        // ---------- Suche ----------

        void OnSearchChanged()
        {
            view.Query = search.Text.Trim();
            hitPos = -1;
            view.MarkAllDirty();
            view.Relayout();
        }

        // Enter: naechster Treffer (auch in anderen Wochen); Raumnamen werden nicht durchsucht.
        void NextHit()
        {
            if (view.Query.Length == 0) return;
            List<SearchHit> hits = store.Find(view.Query);
            if (hits.Count == 0) { hitsLabel.Text = "keine Treffer"; return; }
            hitPos = (hitPos + 1) % hits.Count;
            SearchHit h = hits[hitPos];
            if (h.Day < start || h.Day > start.AddDays(6))
            {
                if (!EndEdit(true)) return;
                start = MondayOf(h.Day);
                Rebuild(true);
            }
            foreach (CellData c in view.Cells)
                if (c.Day == h.Day && rowInfo[c.RowIndex].Id == h.RowId)
                {
                    if (view.Collapsed[c.RowIndex]) { fold[h.RowId] = false; ApplyFold(); view.Relayout(); }
                    view.Selected = c; view.Invalidate();
                    canvas.AutoScrollPosition = new Point(0, Math.Max(0, c.R.Y - 4));
                    break;
                }
            hitsLabel.Text = "Treffer " + (hitPos + 1) + " von " + hits.Count;
        }

        // ---------- Export und Pakete ----------

        // Rendert die ganze Woche; zugeklappte Zeilen mit Inhalt werden dafuer aufgeklappt (nichts darf im Export fehlen).
        Bitmap RenderAll()
        {
            if (!EndEdit(true)) return null;
            bool[] keep = view.Collapsed; CellData sel = view.Selected;
            List<bool> rk = new List<bool>();   // Rundgangsleiste im Export immer aufgeklappt
            foreach (RoundGroup g in view.Rounds) { rk.Add(g.Collapsed); g.Collapsed = false; }
            view.Collapsed = view.DefaultFold(); view.Selected = null; view.Relayout();
            try { return Export.RenderWeek(view, weekLabel.Text, start); }
            finally
            {
                for (int i = 0; i < rk.Count; i++) view.Rounds[i].Collapsed = rk[i];
                view.Collapsed = keep; view.Selected = sel; view.Relayout();
            }
        }

        void ExportImage(bool pdf)
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = pdf ? "PDF (*.pdf)|*.pdf" : "PNG-Bild (*.png)|*.png";
                d.FileName = "Arbeitsplan_" + weekLabel.Text.Substring(0, weekLabel.Text.IndexOf(' '));
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Bitmap bmp = RenderAll()) { if (bmp == null) return; if (pdf) Export.SavePdf(bmp, d.FileName); else Export.SavePng(bmp, d.FileName); }
                }
                catch (Exception ex) { ExportFailed("Export", ex); }
            }
        }

        void ExportFailed(string what, Exception ex)
        {
            MessageBox.Show(what + " fehlgeschlagen.\n" + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void ExportClipboard()
        {
            try { using (Bitmap bmp = RenderAll()) if (bmp != null) Clipboard.SetImage(bmp); }   // SetImage kopiert die Daten
            catch (Exception ex) { ExportFailed("Kopieren", ex); }
        }

        void ExportEquipList()
        {
            if (!EndEdit(true)) return;
            DateTime? sel = view.Selected != null ? (DateTime?)view.Selected.Day : null;
            using (EquipExportForm f = new EquipExportForm(store, start, sel)) f.ShowDialog(this);
        }

        void ExportPackage()
        {
            if (!EndEdit(true)) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Arbeitsplan-Paket (*.apkg)|*.apkg"; d.FileName = "Woche_" + start.ToString("yyyy-MM-dd");
                PackagePicker.SetSaveFolder(d, PackageFolder);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Package.FromWeek(store, start, weekLabel.Text).Save(d.FileName); }
                catch (Exception ex) { ExportFailed("Export", ex); }
            }
        }

        void OpenPackageEditor(Package p)
        {
            if (!EndEdit(true)) return;
            using (PackageForm f = new PackageForm(store, equip, start, p, me.Has(Perm.ImportPackage), me.Has(Perm.PackageAdmin)))
            {
                f.By = me; f.Folder = PackageFolder; f.ShowDialog(this);
                if (f.Applied) ShowWeekOf(f.AppliedStart);
            }
        }

        // Pakete > Kampagne starten…: Produkt-Paket waehlen, gleich Startdatum/Laeufe festlegen (ohne Editor).
        // Unvollstaendige Pakete (Raum fehlt/unbekannt) oder reine Wochen-Exporte oeffnen im Editor mit Pruefhinweisen.
        void StartCampaignFromFile()
        {
            if (!EndEdit(true)) return;
            string f = PackagePicker.Pick(this, PackageFolder, me.Has(Perm.PackageAdmin), "Kampagne starten – Produkt (Paket) wählen", "Weiter");
            if (f == null) return;
            Package p;
            try { p = Package.Load(f); }
            catch (Exception ex) { MessageBox.Show("Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!PackageForm.Ready(p, store)) { OpenPackageEditor(p); return; }
            DateTime st;
            if (PackageForm.StartCampaign(this, p, store, start, me, out st)) ShowWeekOf(st);
        }

        // Pakete > Neue Kampagne…: Assistent (Produkt, Kampagne/Sonderlauf, Paket), dann der uebliche Kampagnenstart bzw. der Editor fuer ein neues Paket.
        void NewCampaign()
        {
            if (!EndEdit(true)) return;
            string file; Package neu = null;
            using (CampaignWizard w = new CampaignWizard(PackageFolder, store.GetCatalog(), null))
            {
                if (w.ShowDialog(this) != DialogResult.OK) return;
                file = w.PackageFile;
                if (w.NewPackage)
                {   // Einzelpaket/Sonderereignis: Name gesetzt = ohne Standardschritte (Packen); Kampagne: leer = mit Packen + Back-Up
                    neu = new Package(); neu.ProductCode = w.Product;
                    if (w.Type != PackageType.Kampagne) { neu.Type = w.Type; neu.Name = (w.Special ? "Sonderereignis " : "Einzelpaket ") + w.Product; }
                }
            }
            if (neu != null) { OpenPackageEditor(neu); return; }
            Package p;
            try { p = Package.Load(file); }
            catch (Exception ex) { MessageBox.Show("Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!PackageForm.Ready(p, store)) { OpenPackageEditor(p); return; }
            DateTime st;
            if (PackageForm.StartCampaign(this, p, store, start, me, out st)) ShowWeekOf(st);
        }

        // Nach dem Uebernehmen: Woche des Kampagnenstarts zeigen (Ergebnis sofort sichtbar)
        void ShowWeekOf(DateTime day)
        {
            DateTime m = MondayOf(day);
            if (m != start && day != DateTime.MinValue) { start = m; Rebuild(true); }
            else Rebuild(false);
        }

        void ImportPackageFile()
        {
            string f = PackagePicker.Pick(this, PackageFolder, me.Has(Perm.PackageAdmin));
            if (f == null) return;
            try { OpenPackageEditor(Package.Load(f)); }
            catch (Exception ex) { MessageBox.Show("Paket konnte nicht gelesen werden.\n" + ex.Message, "Paket", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // Paketordner aus der Konfiguration; null bei Testdaten ohne Datenordner (dann klassischer Dateidialog)
        string PackageFolder { get { return cfg != null ? cfg.PackageFolder : null; } }
    }
}
