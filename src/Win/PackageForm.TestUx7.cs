#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest UX-7 (Usability-Befunde) am sichtbaren Paket-Editor bei 1024x768: Schritt-Details haben genug Hoehe, Felder sind nicht
    // zu schmal und nichts wird abgeschnitten, schmale Fenster ordnen "Wann?" unter "Was?", Kalender bekommt Breite, Standardschaltflaeche,
    // Beschriftungen, Info-Hinweis "Packen ohne Nummer". Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        const int Ux7MinDetailsH = 200;   // sichtbare Hoehe der Schritt-Details bei 1024x768 (gemessen ca. 250)
        const int Ux7MinStripW = 380;     // Breite des Kalenders bei 1024x768 (gemessen ca. 435)

        static bool Cut(Label l) { return l.Visible && l.Height <= l.Font.Height + 8 && TextRenderer.MeasureText(l.Text, l.Font).Width > l.Width + 1; }

        public static string TestUx7(IDataStore realStore, IEquipmentStatusProvider equip, DateTime start)
        {
            IDataStore store = new MemoryStore();   // frischer Katalog: keine Hinweise aus anderen Tests
            Ux1bPrepare(store);
            string oldPrefs = PrefsFile, tmp = Path.Combine(Path.GetTempPath(), "ux7_prefs_" + Guid.NewGuid().ToString("N") + ".ini");
            PrefsFile = tmp;
            int oldStack = StackW;
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("UX7"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); f.Size = new Size(1024, 768); Application.DoEvents();
                f.SelectStep(2); Application.DoEvents();
                // (1) Hoehe und Feldbreiten der Schritt-Details, nichts abgeschnitten
                int dh = f.scroll.ClientSize.Height;
                bool okH = dh >= Ux7MinDetailsH;
                bool okW = true;
                foreach (Control c in new Control[] { f.tTitle, f.cbRow, f.cbKind, f.cbRepeat }) okW &= c.Visible && c.Width >= 110;
                bool okCut = true;
                foreach (Label l in new[] { f.lTitle, f.lRow, f.lKind, f.lRepeat, f.lInterval }) okCut &= !Cut(l);
                okCut &= f.cbKind.Width >= TextRenderer.MeasureText("Hauptlauf", f.cbKind.Font).Width + 24;
                // (2) schmal: "Wann?" unter "Was?", breit: daneben; Standard-Schwelle gesetzt
                StackW = 100000; f.LayoutDetails(); Application.DoEvents();
                bool okStack = f.gWhen.Top >= f.gWhat.Bottom && f.gWhen.Width == f.gWhat.Width && f.gWhat.Width >= f.body.ClientSize.Width - 2;
                StackW = 0; f.LayoutDetails(); Application.DoEvents();
                bool okSide = f.gWhen.Left >= f.gWhat.Right && f.gWhen.Top == f.gWhat.Top;
                StackW = oldStack; f.LayoutDetails();
                // (3) Vorschau bei kleinem Fenster verkleinert, per Teiler wieder aufziehbar; Kalender und Spalten
                int pane0 = f.paneWrap.Height; bool okPane = pane0 <= f.PaneMax && f.PaneMax < 280;
                f.TestPaneMoved(f.PaneMax + 80); Application.DoEvents();
                okPane &= f.paneWrap.Height > pane0;
                f.TestPaneMoved(pane0); Application.DoEvents();
                bool okStrip = f.strip.Width >= Ux7MinStripW && f.hintsCol.Width == 210 && f.pagerCol.Width == 290;
                f.Size = new Size(1300, 800); Application.DoEvents();
                okStrip &= f.hintsCol.Width == 290 && f.pagerCol.Width == 372;
                f.Size = new Size(1024, 768); Application.DoEvents();
                bool okHead = true;
                for (int d = 1; d <= 4; d++) { string h = f.StripHead(d, true); okHead &= h.Length > 0 && h.Length <= 14 && !h.Contains("Tage"); }
                // (4) Standardschaltflaeche, Tab-Reihenfolge, Beschriftungen, Banner
                Button acc = f.AcceptButton as Button, can = f.CancelButton as Button;
                bool okBtn = acc != null && acc.Text.StartsWith("In Plan") && can != null && can.Text == "Schließen" && acc.TabIndex < can.TabIndex && acc.Parent == can.Parent;
                bool okLbl = f.lPackNo.Text == "Nr. für Packen:";
                bool code = false; foreach (Control c in f.headPanel.Controls) if (c is Label && c.Text == "Produktcode:") code = true;
                okLbl &= code && f.marker.Text.StartsWith("Schritt 3 von " + f.steps.Count + " – ") && !f.marker.Text.Contains("(") && f.tip.GetToolTip(f.marker).Contains("Du bearbeitest");
                // (5) Hinweis "Packen ohne Nummer": Info (nicht rot), mit Loesung; Pruefung bleibt gruen (Kampagne mit Puffer, sonst Hinweis "pufkeine")
                PackageStep ux7p = new PackageStep(); ux7p.Title = "Puffer mobil"; ux7p.Source = "Puffer mobil"; ux7p.Row = "Puffer"; ux7p.Variant = "mobil"; ux7p.Bind = StepBind.JeLauf; ux7p.RunOffset = -1;
                f.AddStep(ux7p); f.dirty = false; Application.DoEvents();
                Hint info = f.hintList.Find(delegate(Hint h) { return h.Code == "packen"; });
                bool okInfo = info != null && info.Info && !info.Blocks && info.Text.StartsWith("Info – ") && info.Text.Contains("„Nr. für Packen“") && f.lbHints.ForeColor == Color.DarkGreen
                    && f.hintList.Exists(delegate(Hint h) { return h.Text.StartsWith("Keine Auffälligkeiten"); });
                return "UX7 Paket-Editor 1024x768: Details-Höhe " + dh + " (ab " + Ux7MinDetailsH + ") " + T(okH) + ", Felder nicht zu schmal " + T(okW) + ", nichts abgeschnitten " + T(okCut)
                    + ", schmal untereinander " + T(okStack) + ", breit nebeneinander " + T(okSide) + ", Vorschau klein/aufziehbar " + T(okPane) + ", Kalender " + f.strip.Width + " px (ab " + Ux7MinStripW + ") " + T(okStrip)
                    + ", Kalenderkopf kurz " + T(okHead) + ", Standardschaltfläche " + T(okBtn) + ", Beschriftungen/Banner " + T(okLbl) + ", Info-Hinweis " + T(okInfo) + (okInfo ? "" : " [" + (info == null ? "kein Hinweis" : info.Text) + " | " + f.hintList.Count + " Hinweise, Farbe " + f.lbHints.ForeColor.Name + "]");
            }
            catch (Exception ex) { return "UX7 Paket-Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally
            {
                StackW = oldStack; f.dirty = false; f.Close(); f.Dispose();
                PrefsFile = oldPrefs;
                try { File.Delete(tmp); } catch (Exception) { }
            }
        }
    }
}
#endif
