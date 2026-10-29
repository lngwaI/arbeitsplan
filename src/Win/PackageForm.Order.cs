using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Reihenfolge und Termine aendern, ohne die Ansicht neu aufzubauen. Ablaufliste: ▲/▼, Alt+Hoch/Runter und Ziehen
    // (Einfuegelinie, auch bis ans Ende); Auswahl bleibt auf dem verschobenen Schritt. Kalender (PlanStrip): Block auf einen anderen
    // Tag ziehen = ganzer Schritt, mit Umschalt = nur dieser Lauf (Versatz je Lauf); Rechtsklick-Menue fuer dasselbe tageweise.
    partial class PackageForm
    {
        readonly Panel dropLine = new Panel();
        readonly ContextMenuStrip stripMenu = new ContextMenuStrip();

        void InitListDrag()
        {
            lv.AllowDrop = true;
            dropLine.Height = 2; dropLine.BackColor = Theme.Current.BorderSel; dropLine.Visible = false; dropLine.Enabled = false; dropLine.Tag = "keep";
            lv.Controls.Add(dropLine);
            lv.ItemDrag += delegate(object o, ItemDragEventArgs e)
            {
                PackageStep s = ((ListViewItem)e.Item).Tag as PackageStep;
                if (s == null) return;
                if (s != cur) Bind(s);
                lv.DoDragDrop(s, DragDropEffects.Move);
                dropLine.Visible = false;
            };
            lv.DragOver += delegate(object o, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(typeof(PackageStep))) { e.Effect = DragDropEffects.None; return; }
                e.Effect = DragDropEffects.Move;
                Point p = lv.PointToClient(new Point(e.X, e.Y));
                ScrollWhileDragging(p.Y);
                ShowDropLine(DropSlot(p.Y));
            };
            lv.DragLeave += delegate { dropLine.Visible = false; };
            lv.DragDrop += delegate(object o, DragEventArgs e)
            {
                int slot = DropSlot(lv.PointToClient(new Point(e.X, e.Y)).Y);
                dropLine.Visible = false;
                DropAt(TargetOf(slot, Ordered().IndexOf(cur)));
            };
        }

        // Einfuegestelle (0..n = vor Eintrag i bzw. ans Ende) fuer die Mausposition y
        int DropSlot(int y)
        {
            for (int i = 0; i < lv.Items.Count; i++)
            {
                Rectangle r = lv.Items[i].Bounds;
                if (y < r.Top + r.Height / 2) return i;
            }
            return lv.Items.Count;
        }

        // Einfuegestelle -> Zielposition des gezogenen Eintrags (from = seine Position)
        public static int TargetOf(int slot, int from) { return slot > from ? slot - 1 : slot; }

        void ShowDropLine(int slot)
        {
            int n = lv.Items.Count;
            if (n == 0) { dropLine.Visible = false; return; }
            int y = slot < n ? lv.Items[slot].Bounds.Top : lv.Items[n - 1].Bounds.Bottom;
            dropLine.SetBounds(0, Math.Max(0, y - 1), lv.ClientSize.Width, 2);
            dropLine.Visible = true; dropLine.BringToFront();
        }

        // Am oberen/unteren Rand der Liste beim Ziehen weiterblaettern
        void ScrollWhileDragging(int y)
        {
            if (lv.Items.Count == 0 || lv.TopItem == null) return;
            int top = lv.TopItem.Index;
            if (y < 14 && top > 0) lv.EnsureVisible(top - 1);
            else if (y > lv.ClientSize.Height - 14)
            {
                ListViewItem last = lv.GetItemAt(4, lv.ClientSize.Height - 4);
                if (last != null && last.Index + 1 < lv.Items.Count) lv.EnsureVisible(last.Index + 1);
            }
        }

        // ▲/▼: am selben Tag nur die Reihenfolge, sonst uebernimmt der Schritt den Tag des Nachbarn
        void MoveStep(int dir) { if (cur != null) DropAt(Ordered().IndexOf(cur) + dir); }

        // Gewaehlten Schritt an Position "to" der Ablaufliste bringen
        void DropAt(int to)
        {
            if (cur == null || !MoveTo(cur, to)) return;
            AfterMove(cur);
        }

        // Schritt s an Position "to" der Ablaufliste bringen; ueber eine Tagesgrenze uebernimmt er den Termin des Nachbarn. false = nichts geaendert.
        bool MoveTo(PackageStep s, int to)
        {
            List<PackageStep> o = Ordered();
            int from = o.IndexOf(s);
            if (from < 0 || o.Count < 2) return false;
            to = Math.Max(0, Math.Min(o.Count - 1, to));
            if (to == from) return false;
            PackageStep n = o[to];
            List<RunDate> plan = Plan();
            int delta = FirstOf(n, plan) - FirstOf(s, plan);
            if (delta != 0 && !MoveBy(s, delta)) return false;
            steps.Remove(s);
            int k = steps.IndexOf(n);
            steps.Insert(from < to ? k + 1 : k, s);
            return true;
        }

        // Schritt um delta Tage verschieben: "zu jedem Lauf" ueber den Versatz zum Lauf (RunOffset), sonst ueber sein Raster; vor dem
        // ersten Termin schiebt sich der Ursprung mit (alle anderen wandern nach hinten). false = nicht moeglich (Versatz > 30 Tage).
        bool MoveBy(PackageStep s, int delta)
        {
            Package p = StepsView();
            if (delta == 0) return false;
            if (IsJe(s)) return p.MoveStep(s, delta);
            List<int> d = s.Days(p.Day1); int first = d[0];
            foreach (int x in d) first = Math.Min(first, x);
            if (first + delta < 1) p.ShiftOrigin(1 - (first + delta));
            return s.MoveAll(delta, p.Day1);
        }

        // Nach dem Verschieben: nur die Wann?-Felder, Liste (Texte in place), Kalender und Hinweise nachziehen; Equipment bleibt stehen
        void AfterMove(PackageStep s)
        {
            dirty = true;
            if (s != cur) { Bind(s); return; }
            BindWhen(s);
            UpdateView(true);
        }

        // ---------- Kalender ----------

        // Block im Kalender auf toDay gezogen: ganzer Schritt oder (onlyRun) nur dieser Lauf
        void MoveInCalendar(PackageStep s, int run, int toDay, bool onlyRun)
        {
            int si = steps.IndexOf(s), at = -1;
            foreach (RunDate d in Plan()) if (d.Step == si && d.Run == run) at = d.Day;
            if (at < 0) return;
            int delta = toDay - at;
            if (onlyRun ? (!IsJe(s) && s.MoveRun(run, delta, pk.Day1)) : MoveBy(s, delta)) { nextRun = run; AfterMove(s); }
        }

        void StripMenu(PlanStrip.Item it, Point at)
        {
            PackageStep s = it.Step; int run = it.Run, day = it.Day;
            if (s != cur) { nextRun = run; Bind(s); }
            stripMenu.Items.Clear();
            bool many = StepsView().RunCountOf(s) > 1, je = IsJe(s);
            stripMenu.Items.Add("Ganzen Schritt 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, false); });
            stripMenu.Items.Add("Ganzen Schritt 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, false); });
            if (many)
            {
                stripMenu.Items.Add(new ToolStripSeparator());
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, true); }).Enabled = day > 1 && !je;
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, true); }).Enabled = !je;
                if (!je && s.Shifted(run)) stripMenu.Items.Add("Ausnahme dieses Laufs zurücksetzen", null, delegate { s.Shift.Remove(run); AfterMove(s); });
            }
            stripMenu.Show(strip, at);
        }
    }
}
