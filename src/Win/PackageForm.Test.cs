#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests Paket-Editor v3 (am sichtbaren Fenster): Klick auf jeden Pruefhinweis per echter Mausnachricht (neues Paket ohne Namen,
    // leerer Schritt, Testpaket), Kopfzeile (Produktcode frei, erste Laufnummer 8-stellig, Ueberlauf), Schritt-Auswahl mit Raum.
    partial class PackageForm
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        static string T(bool b) { return b ? "OK" : "FEHLER"; }
        public void TestMore() { SetMore(true); }

        // Jeden Hinweis anklicken (WM_LBUTTONDOWN/UP); Ausnahmen aus der Nachrichtenschleife werden mitgezaehlt. Liefert Anzahl Klicks.
        int ClickAllHints(List<string> errors)
        {
            int n = 0;
            ThreadExceptionEventHandler h = delegate(object o, ThreadExceptionEventArgs e) { errors.Add(e.Exception.GetType().Name + ": " + e.Exception.Message); };
            Application.ThreadException += h;
            try
            {
                for (int i = 0; i < lbHints.Items.Count && i < 12; i++)
                {
                    Rectangle r = lbHints.GetItemRectangle(i);
                    IntPtr lp = (IntPtr)(((r.Y + 3) << 16) | ((r.X + 5) & 0xFFFF));
                    try { LButton(true); SendMessage(lbHints.Handle, 0x201, (IntPtr)1, lp); SendMessage(lbHints.Handle, 0x202, IntPtr.Zero, lp); LButton(false); Application.DoEvents(); n++; }
                    catch (Exception ex) { errors.Add(ex.GetType().Name + ": " + ex.Message); }
                }
            }
            finally { Application.ThreadException -= h; }
            return n;
        }

        [DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] k);
        [DllImport("user32.dll")] static extern bool SetKeyboardState(byte[] k);
        // Linke Maustaste fuer diesen Thread als gedrueckt melden (GetKeyState): sonst gibt die ListBox das Capture sofort frei und meldet keinen Click
        static void LButton(bool down) { byte[] k = new byte[256]; GetKeyboardState(k); k[1] = (byte)(down ? 0x80 : 0); SetKeyboardState(k); }

        // Nach dem Anzeigen jeden Pruefhinweis einzeln anklicken (Maus ab/auf wie bei echter Bedienung, die Liste wird nach jedem Klick neu
        // aufgebaut), dann schliessen. log: Ausnahmen; n[0] Klicks, n[1] davon mit Click-Ereignis, n[2] Hinweise; jumped: Namens-Hinweis setzt den Fokus.
        internal void AutoClickHints(List<string> log, int[] n, bool[] jumped)
        {
            int i = 0, phase = 0, seen = 0, clicked = 0;
            lbHints.Click += delegate { clicked++; };
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 150;
            t.Tick += delegate
            {
                try
                {
                    if (phase == 0 && i == 0) n[2] += lbHints.Items.Count;
                    if (IsDisposed || i >= lbHints.Items.Count || i >= 12) { t.Stop(); t.Dispose(); if (!IsDisposed) { dirty = false; Close(); } return; }
                    if (phase == 0)
                    {
                        Rectangle r = lbHints.GetItemRectangle(i);
                        IntPtr lp = (IntPtr)(((r.Y + 3) << 16) | ((r.X + 5) & 0xFFFF));
                        seen = clicked;
                        LButton(true); SendMessage(lbHints.Handle, 0x201, (IntPtr)1, lp); SendMessage(lbHints.Handle, 0x202, IntPtr.Zero, lp); LButton(false);
                        n[0]++; if (clicked > seen) n[1]++;
                        else HintClicked();   // Punkt verdeckt (WindowFromPoint): Klick wie die ListBox ausloesen
                        phase = 1;
                    }
                    else
                    {
                        Hint h = i < hintList.Count ? hintList[i] : null;
                        if (h != null && h.Field == tName && !tName.Focused) jumped[0] = false;
                        phase = 0; i++;
                    }
                }
                catch (Exception ex) { log.Add(ex.ToString()); t.Stop(); dirty = false; Close(); }
            };
            if (Visible) t.Start(); else Shown += delegate { t.Start(); };
        }

        // Wie Menue "Neues Paket anlegen…" (modal, mit Besitzer): Faelle neues Paket (ohne PackageAdmin wie Nutzer "Produktion"),
        // ganz leer, leerer Schritt, Schritt ohne Raum; jeden Pruefhinweis anklicken. Ausnahmen aus der Nachrichtenschleife werden mitgezaehlt.
        public static string TestHintClickNew(Form owner, IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            List<string> err = new List<string>(); int[] n = new int[3]; bool[] jumped = { true };
            ThreadExceptionEventHandler h = delegate(object o, ThreadExceptionEventArgs e) { err.Add(e.Exception.ToString()); };
            Application.ThreadException += h;
            try
            {
                for (int fall = 0; fall < 4; fall++)
                    using (PackageForm f = new PackageForm(store, equip, start, null, true, fall != 0))
                    {
                        f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
                        if (fall == 1) f.LoadPackage(new Package());
                        if (fall >= 2) { PackageStep s = new PackageStep(); s.Day = f.NextDay(); if (fall == 3) s.Title = "Ohne Raum"; f.AddStep(s); }
                        f.AutoClickHints(err, n, jumped);
                        f.ShowDialog(owner);
                    }
            }
            catch (Exception ex) { err.Add(ex.ToString()); }
            finally { Application.ThreadException -= h; }
            return "Hinweis-Klick leeres Paket: " + n[0] + " Klicks (" + n[1] + " per Maus, " + n[2] + " Hinweise in 4 Fällen), Sprung zum Namen " + T(jumped[0])
                + ", Ausnahmen " + err.Count + " " + T(err.Count == 0 && n[0] >= 4) + (err.Count > 0 ? " [" + err[0].Replace("\r", " ").Replace("\n", " | ") + "]" : "");
        }

        public static string TestPaket2(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            List<string> err = new List<string>();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            PackageForm f = new PackageForm(store, equip, start, null, true, true);   // neues Paket ohne Namen
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                int clicks = f.ClickAllHints(err);
                PackageStep e = new PackageStep(); e.Day = f.NextDay(); f.AddStep(e); Application.DoEvents();   // leerer Schritt
                clicks += f.ClickAllHints(err);
                f.LoadPackage(new Package()); Application.DoEvents();   // ganz leer, kein Schritt gewaehlt
                clicks += f.ClickAllHints(err);
                Package sc = Package.SampleCampaign(""); sc.Steps[2].Ident = "PA 1"; sc.Steps[2].Equipment[0].Choices[0].Id = "H9-X-9999";
                f.LoadPackage(sc); f.Bind(f.steps[2]); f.nStartNo.Value = 9998; Application.DoEvents();   // Testpaket ohne Namen, mit Hinweisen zu Kennung/Geraet/Ueberlauf
                int hintsSample = f.lbHints.Items.Count;
                clicks += f.ClickAllHints(err);
                Application.DoEvents();
                sb.AppendLine("Paket2 Hinweis-Klick: " + clicks + " Klicks (Testpaket " + hintsSample + " Hinweise), Ausnahmen " + err.Count + " " + T(err.Count == 0 && clicks > 4)
                    + (err.Count > 0 ? " [" + err[0] + "]" : ""));

                // Kopfzeile: Produktcode frei, erste Laufnummer 8-stellig, Ueberlauf
                f.LoadPackage(Package.SampleCampaign("Kopf")); f.dirty = false; Application.DoEvents();
                f.tProduct.Text = "AB1"; f.tFirst.Text = "13570100"; Application.DoEvents();
                PackageStep lauf = f.steps[2];
                bool head = f.prefix == "1357" && lauf.StartNo == 100 && lauf.Label(f.prefix, 1) == "Lauf – Auftrag/Netzwerk-ID 13570101" && f.ProductError() == null
                    && !f.hintList.Exists(delegate(Hint h) { return h.Field == f.tProduct; });
                f.tFirst.Text = "13579998"; Application.DoEvents();
                bool over = f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("über 9999"); }) && f.lRuns.ForeColor == Color.Firebrick;
                f.tProduct.Text = "AB 1"; f.tFirst.Text = "1357003"; Application.DoEvents();
                bool bad = f.hintList.Exists(delegate(Hint h) { return h.Field == f.tProduct; }) && f.hintList.Exists(delegate(Hint h) { return h.Field == f.tFirst; });
                f.tProduct.Text = "AB1"; f.tFirst.Text = "13570001"; Application.DoEvents();
                Package col = f.ProductError() == null && f.FirstRunError() == null && f.StepsView().Overflow() == null ? f.Collect() : null;   // Collect zeigt sonst eine Meldung
                bool collect = col != null && col.ProductCode == "AB1" && col.RunPrefix == "1357" && col.FirstRun == "13570001";
                // Mehr Optionen: Kennung-Feld nur aufgeklappt; Hinweis-Klick darauf klappt auf
                f.Bind(lauf); bool slim = !f.more && !f.gExtra.Visible && !f.nStartNo.Visible && f.cbRepeat.Visible;
                f.GoToHint(new Hint("x", lauf, f.tIdent)); Application.DoEvents();
                bool opens = f.more && f.gExtra.Visible && f.tIdent.Visible;
                f.SetMore(false);
                sb.AppendLine("Paket2 Kopfzeile: Code+Laufnummer " + T(head) + " Ueberlauf " + T(over) + " ungueltig erkannt " + T(bad)
                    + " Speichern " + T(collect) + " schlank " + T(slim) + " Hinweis klappt auf " + T(opens));

                // Schritt hinzufuegen: Art zuerst, Raum vorbelegt; Sonstiges nur mit Raum
                using (StepPicker p = new StepPicker(f.cat, f.steps, f.rowNames))
                {
                    p.Show(); Application.DoEvents();
                    bool order = p.TestNames.Count > 5 && p.TestNames[0] == "Prozess-Lauf" && p.TestNames[1] == "Puffer" && p.TestNames[p.TestNames.Count - 1] == "Sonstiges";
                    bool rooms = p.TestSelect("Puffer") == "Puffer" && p.TestOk && p.TestSelect("Freeze / Thaw") == "Freeze / Thaw" && p.TestSelect("CIP / SIP") == "CIP / SIP"
                        && p.TestSelect("Einwaage") == "Einwaage" && p.TestSelect("Prozess-Lauf") == (f.rowNames.Contains("Suite 1") ? "Suite 1" : "");   // Selbsttest benennt Suite 1 um: dann nicht vorbelegt
                    bool other = p.TestSelect("Sonstiges") == "" && !p.TestOk;
                    sb.AppendLine("Paket2 Schrittauswahl: Reihenfolge " + T(order) + " Raum vorbelegt " + T(rooms) + " Sonstiges braucht Raum " + T(other));
                    p.Close();
                }
            }
            catch (Exception ex) { sb.AppendLine("Paket2 Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message); }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
            return sb.ToString().TrimEnd();
        }

        // Roundtrip-Test: geladenes Muster unveraendert wieder speichern.
        public string TestSave(string path) { Package p = Collect(); p.Save(path); return "dirty=" + dirty; }

        // Selbsttest der Bedienablaeufe am sichtbaren Editor: Schritte waehlen, hinzufuegen, duplizieren, verschieben,
        // Art/Wiederholung aendern, entfernen, leeres und neues Paket. Ausnahmen (z. B. Owner-Draw-Index) landen im Ergebnis.
        public static string TestFlow(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("Ablauftest"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                int n0 = f.steps.Count;
                foreach (PackageStep s in f.Ordered()) { f.Bind(s); Application.DoEvents(); }
                PackageStep e = new PackageStep(); e.Day = f.NextDay(); f.AddStep(e); Application.DoEvents();
                f.tTitle.Text = "Test äöü"; f.cbKind.SelectedIndex = 2; f.nCount.Value = 3; f.tSubNos.Text = "1/2, 3"; f.tIdent.Text = "PA 1"; Application.DoEvents();
                bool hints = f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("Unterlauf-Nummern für 2") && h.Field == f.tSubNos; })
                    && f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("1 Kennung(en), aber 3"); });
                f.nStartNo.Value = 9998; Application.DoEvents();
                hints &= f.hintList.Exists(delegate(Hint h) { return h.Text.Contains("über 9999"); });
                f.nStartNo.Value = 1; f.tIdent.Text = ""; Application.DoEvents();
                f.Duplicate(); Application.DoEvents();
                f.Bind(f.Ordered()[0]); f.MoveStep(1); f.MoveStep(-1); f.MoveStep(-1); Application.DoEvents();
                PackageStep dragged = f.Ordered()[0]; f.Bind(dragged); f.DropAt(3); Application.DoEvents();
                bool drag = f.Ordered().IndexOf(dragged) == 3;
                f.DropAt(0); drag &= f.Ordered().IndexOf(dragged) == 0;
                int n1 = f.steps.Count;
                f.RemoveStep(false); f.RemoveStep(false); Application.DoEvents();
                int n2 = f.steps.Count;
                f.LoadPackage(new Package()); Application.DoEvents();
                bool empty = f.cur == null && f.lbHints.Items.Count > 0 && !f.body.Visible;
                f.LoadPackage(null); Application.DoEvents();
                bool fresh = f.steps.Count > 0 && f.cur != null && !f.dirty;
                return "Paket-Editor Ablauf: Schritte " + n0 + " -> +2 = " + n1 + " -> -2 = " + n2 + (n1 == n0 + 2 && n2 == n0 ? " ok" : " FEHLER")
                    + ", leeres Paket " + (empty ? "ok" : "FEHLER") + ", neues Paket " + (fresh ? "ok" : "FEHLER") + ", Prüfhinweise " + (hints ? "ok" : "FEHLER") + ", Ziehen " + (drag ? "ok" : "FEHLER");
            }
            catch (Exception ex) { return "Paket-Editor Ablauf: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
    }
}
#endif
