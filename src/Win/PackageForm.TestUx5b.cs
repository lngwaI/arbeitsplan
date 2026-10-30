#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5b am sichtbaren Paket-Editor: Reihenfolge der Positionen (Knoepfe, Ziehen mit Einfuegelinie, Gruppengrenze, Rueckgaengig,
    // normale Schritte) und Vorschau der Puffer (Lauf-Angabe, Abstand, Farbwechsel am Bild). Core/Plan: MainForm.SelfTestUX5b.cs.
    // Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class PackageForm
    {
        // Kampagne + Puffer-Schritt mit drei Puffern (A: Ansatztank, Vorlagetank, Tween; B: Ansatztank, Vorlagetank; C stationaer: Ansatztank), WFI gemeinsam fuer A und B,
        // folgt jedem Lauf einen Tag spaeter (Plan: Donnerstag nach dem Hauptlauf am Mittwoch). Testbild: --form package15, --makepkg UX5b...
        public static Package Ux5bSample(string name)
        {
            Package p = Ux1bSample(name, false);
            PackageStep pu = p.Steps[p.Steps.Count - 1];
            pu.Puffers.Add(UxP("Lösung C", "stationaer", UxA("Ansatztank", "AT-3")));
            pu.Bind = StepBind.JeLauf; pu.RunOffset = 1;
            pu.SyncPuffers();
            return p;
        }

        // Testbild: Zeile k der Karte c mit sichtbaren Knoepfen (Hover/Fokus nachgestellt)
        internal void TestHotRow(int c, int k)
        {
            if (c >= cards.Count) return;
            List<EquipRow> rows = cards[c].TestRows;
            if (k < rows.Count) { rows[k].PinHot = true; rows[k].SetHot(true); }
        }

        static string Shorts(List<EquipAssign> l) { List<string> r = new List<string>(); foreach (EquipAssign a in l) r.Add(a.Short); return string.Join(",", r.ToArray()); }

        static string ShortsOf(PackageStep s, int k)
        {
            List<string> r = new List<string>();
            foreach (string l in s.BuffersFor(0, "")[k].Text.Split('\n')) r.Add(l.Split(':')[0]);
            return string.Join(",", r.ToArray());
        }

        EquipRow EqRow(int i) { return eqRows[i]; }

        public static string TestUx5b(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Ux1bPrepare(store);
            Package sample = Ux5bSample("UX5b");
            PackageForm f = new PackageForm(store, equip, start, sample, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            int stage = 0; string dbg = "";
            try
            {
                f.Show(); Application.DoEvents();
                f.TestSelectBuffer(); Application.DoEvents();
                PufferCard c0 = f.Card(0);
                List<EquipRow> rows = c0.TestRows;

                stage = 1; // (1) Knoepfe nur bei Hover/Fokus; am Rand der Gruppe (eigene Zeilen / gemeinsame Zeile) deaktiviert
                bool okArrows = rows.Count == 4 && !rows[1].TestUp.Visible && !rows[1].TestDown.Visible && rows[1].TestGrip.Visible;
                rows[1].PinHot = true; rows[1].SetHot(true);
                okArrows &= rows[1].TestUp.Visible && rows[1].TestDown.Visible && rows[1].TestUp.Enabled && rows[1].TestDown.Enabled
                    && !rows[0].TestUp.Enabled && rows[0].TestDown.Enabled && rows[2].TestUp.Enabled && !rows[2].TestDown.Enabled && !rows[3].TestUp.Enabled && !rows[3].TestDown.Enabled;
                rows[1].PinHot = false; rows[1].SetHot(false);
                okArrows &= !rows[1].TestUp.Visible && !rows[1].TestDown.Visible;

                stage = 2; // (2) Knopf ▼ auf Zeile 2: Reihenfolge im Modell und im Plantext, ein Rueckgaengig-Schritt
                PackageStep ps = f.cur;
                int snaps = f.UndoCount;
                rows[1].PinHot = true; rows[1].SetHot(true);
                rows[1].TestDown.PerformClick(); Application.DoEvents();
                ps = f.cur; dbg = "nach Klick: " + (ps != null ? ps.Title + " puffer=" + ps.Puffers.Count : "null") + " karten=" + f.cards.Count + " undo=" + f.UndoCount + "/" + snaps;
                bool okBtn = Shorts(ps.Puffers[0].Lines) == "Ansatztank,Tween,VT" && ShortsOf(ps, 0) == "Ansatztank,Tween,VT,WFI" && ShortsOf(ps, 1) == "Ansatztank,VT,WFI" && f.dirty
                    && f.UndoCount == snaps + 1 && f.Card(0).TestRows[1].TestIdBox.Text == "TW-1" && f.Card(0).TestRows[2].TestIdBox.Text == "VT-1";
                f.TestUndo(-1); f.TestSelectBuffer(); Application.DoEvents();   // Rueckgaengig stellt auch die Auswahl des Schnappschusses her
                ps = f.cur; dbg += "; nach Undo: " + (ps != null ? ps.Title + " puffer=" + ps.Puffers.Count : "null");
                bool okUndo = Shorts(ps.Puffers[0].Lines) == "Ansatztank,VT,Tween" && ShortsOf(ps, 0) == "Ansatztank,VT,Tween,WFI" && f.cards.Count == 3;
                f.TestUndo(1); f.TestSelectBuffer(); Application.DoEvents();
                okUndo &= Shorts(f.cur.Puffers[0].Lines) == "Ansatztank,Tween,VT";
                f.TestUndo(-1); f.TestSelectBuffer(); Application.DoEvents();

                stage = 3; // (3) Ziehen: nach unten (Einfuegelinie am Ziel), nicht ueber die Gruppengrenze (WFI bleibt unten), Abbrechen, nach oben
                PufferCard c = f.Card(0); RowReorder ro = c.TestReorder; EquipRow r0 = c.TestRows[0];
                ro.DragStart(r0); ro.DragTo(r0, c.TestSlot(2).Bottom + 1);
                bool okDrag = ro.LineVisible && ro.TestTarget == 2 && ro.LineBounds.Y == c.TestSlot(2).Bottom - 1 && ro.LineBounds.Height == 2;
                ro.DragEnd(r0, true); Application.DoEvents();
                okDrag &= Shorts(f.cur.Puffers[0].Lines) == "VT,Tween,Ansatztank" && ShortsOf(f.cur, 0) == "VT,Tween,Ansatztank,WFI" && !f.Card(0).TestReorder.LineVisible;
                c = f.Card(0); ro = c.TestReorder; r0 = c.TestRows[0];
                ro.DragStart(r0); ro.DragTo(r0, c.TestSlot(3).Bottom + 20);   // unter die gemeinsame Zeile: bleibt am Ende der eigenen Zeilen
                okDrag &= ro.TestTarget == 2 && ro.LineBounds.Y == c.TestSlot(2).Bottom - 1;
                ro.DragEnd(r0, false); Application.DoEvents();   // Abbrechen: nichts aendert sich
                okDrag &= Shorts(f.cur.Puffers[0].Lines) == "VT,Tween,Ansatztank" && !f.Card(0).TestReorder.LineVisible;
                c = f.Card(0); ro = c.TestReorder; EquipRow r2 = c.TestRows[2];
                ro.DragStart(r2); ro.DragTo(r2, c.TestSlot(0).Top - 3);
                bool okUp = ro.TestTarget == 0 && ro.LineBounds.Y == c.TestSlot(0).Top - 1;
                ro.DragEnd(r2, true); Application.DoEvents();
                okUp &= Shorts(f.cur.Puffers[0].Lines) == "Ansatztank,VT,Tween" && ShortsOf(f.cur, 0) == "Ansatztank,VT,Tween,WFI" && f.UndoCount >= snaps + 2;
                okDrag &= f.Card(0).TestInfoRows == 0 && f.Card(1).TestInfoRows == 1 && f.cur.PufferShared.Count == 1 && f.cur.PufferShared[0].Scope == 3;   // Geltungsbereich bleibt

                stage = 4; // (4) Normaler Schritt (Hauptlauf mit Equipment): Griff-Ziehen und Knopf, Reihenfolge im Modell und im Plantext, Rueckgaengig
                f.SelectStep(2); Application.DoEvents();
                PackageStep lf = f.cur;
                string before = Shorts(lf.Equipment), planBefore = lf.Compose(0, f.prefix);
                int n = f.eqRows.Count;
                bool okEq = n >= 5 && f.EqRow(0).TestGrip.Visible && !f.EqRow(0).TestUp.Enabled && f.EqRow(1).TestUp.Enabled && !f.EqRow(n - 1).TestDown.Enabled && f.EqRow(n - 2).TestDown.Enabled;
                EquipRow e0 = f.EqRow(0);
                f.equipOrder.DragStart(e0); f.equipOrder.DragTo(e0, f.EqRow(2).Bounds.Bottom + 1);
                okEq &= f.equipOrder.LineVisible && f.equipOrder.TestTarget == 2 && f.equipOrder.LineBounds.Width == e0.Width;
                f.equipOrder.DragEnd(e0, true); Application.DoEvents();
                lf = f.cur;
                string[] bs = before.Split(',');
                okEq &= Shorts(lf.Equipment).StartsWith(bs[1] + "," + bs[2] + "," + bs[0] + ",") && Shorts(lf.Equipment).Split(',').Length == bs.Length && lf.Compose(0, f.prefix) != planBefore && f.dirty;
                EquipRow e1 = f.EqRow(1); e1.PinHot = true; e1.SetHot(true);
                e1.TestUp.PerformClick(); Application.DoEvents();   // Zeile 2 nach oben: Reihenfolge wie vorher mit getauschten ersten beiden
                lf = f.cur;
                okEq &= Shorts(lf.Equipment).StartsWith(bs[2] + "," + bs[1] + "," + bs[0] + ",");
                f.TestUndo(-1); Application.DoEvents(); f.TestUndo(-1); f.SelectStep(2); Application.DoEvents();
                okEq &= Shorts(f.cur.Equipment) == before && f.cur.Compose(0, f.prefix) == planBefore;

                stage = 5; // (5) Vorschau: Kopf nennt den Lauf, Abstand und Farbwechsel am Bild, zweiter Lauf = Lauf 2
                f.TestSelectBuffer(); Application.DoEvents();
                BlockPreview bp = f.blockPreview;
                BlockPreview.Card pc = bp.Count > 0 ? bp.TestCard(0) : null;
                bool okLauf = pc != null && pc.Data.Buffers.Count == 3 && pc.Data.Buffers[0].LaufRef == 1 && pc.Data.Buffers[2].LaufRef == 1
                    && pc.Data.Buffers[1].HeadText(2) == "Puffer 2: " + Package.PlanBufferName(f.Product, "Lösung B") + " · Lauf 1" && f.curDates.Count >= 2;
                f.GoPage(1); Application.DoEvents();
                pc = bp.TestCard(0);
                okLauf &= pc.Data.Buffers[0].LaufRef == 2 && pc.Data.Buffers[0].HeadText(1) == "Puffer 1: " + Package.PlanBufferName(f.Product, "Lösung A") + " · Lauf 2";
                List<Rectangle> bands = bp.TestBands(0);
                bool okBands = bands.Count == 3 && bp.TestBufCount(0) == 3 && bands[1].Top - bands[0].Bottom >= 8 && bands[2].Top - bands[1].Bottom >= 8 && bands[0].Top >= 4;
                bool okTint = BufStyle.Tint(0) != BufStyle.Tint(1) && BufStyle.Tint(1) != BufStyle.Tint(2) && BufStyle.Tint(0) != Color.White && BufStyle.Gap >= 8 && BufStyle.Gap <= 12;
                if (bands.Count == 3 && bp.Width > 0 && bp.Height > 0)
                {
                    using (Bitmap bmp = new Bitmap(bp.Width, bp.Height))
                    {
                        bp.DrawToBitmap(bmp, new Rectangle(0, 0, bp.Width, bp.Height));
                        for (int k = 0; k < 3; k++)
                        {
                            Color px = bmp.GetPixel(bands[k].Right - 8, bands[k].Top + 1);
                            okTint &= Math.Abs(px.R - BufStyle.Tint(k).R) <= 2 && Math.Abs(px.G - BufStyle.Tint(k).G) <= 2 && Math.Abs(px.B - BufStyle.Tint(k).B) <= 2;
                        }
                        Color gap = bmp.GetPixel(bands[0].Right - 8, bands[0].Bottom + 1);   // Luecke zwischen den Puffern: weisser Grund
                        okTint &= gap.R >= 250 && gap.G >= 250 && gap.B >= 250;
                        Color stripe = bmp.GetPixel(bands[2].Left + 1, bands[2].Top + 2), stripe0 = bmp.GetPixel(bands[0].Left + 1, bands[0].Top + 2);
                        okTint &= stripe != stripe0;   // Typ bleibt als Seitenstrich (C stationaer, A mobil)
                    }
                }
                return "UX5b Editor: Knöpfe nur bei Hover/Fokus " + T(okArrows) + " ▼ Knopf " + T(okBtn) + " Rückgängig " + T(okUndo) + " Ziehen/Einfügelinie/Gruppe " + T(okDrag) + " nach oben " + T(okUp)
                    + " normaler Schritt " + T(okEq) + " Vorschau Lauf " + T(okLauf) + " Abstand " + T(okBands) + " Farbwechsel " + T(okTint);
            }
            catch (Exception ex) { return "UX5b Editor: FEHLER Ausnahme (Abschnitt " + stage + ", " + dbg + ") " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Replace("\r", "").Replace("\n", " / "); }
            finally { f.dirty = false; f.Close(); f.Dispose(); Package.Seen.Remove(sample); }
        }
    }
}
#endif
