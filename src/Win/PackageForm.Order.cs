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
            if (cur == null || !StepOrder.MoveTo(steps, cur, to)) return;
            AfterMove(cur);
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
            List<int> d = s.Days();
            if (run < 0 || run >= d.Count) return;
            int delta = toDay - d[run];
            if (onlyRun ? s.MoveRun(run, delta) : s.MoveAll(delta)) AfterMove(s);
        }

        void StripMenu(PlanStrip.Item it, Point at)
        {
            PackageStep s = it.Step; int run = it.Run, day = it.Day;
            if (s != cur) Bind(s);
            stripMenu.Items.Clear();
            bool many = s.RunCount > 1;
            stripMenu.Items.Add("Ganzen Schritt 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, false); }).Enabled = s.FirstDay > 1;
            stripMenu.Items.Add("Ganzen Schritt 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, false); });
            if (many)
            {
                stripMenu.Items.Add(new ToolStripSeparator());
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, true); }).Enabled = day > 1;
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, true); });
                if (s.Shifted(run)) stripMenu.Items.Add("Ausnahme dieses Laufs zurücksetzen", null, delegate { s.Shift.Remove(run); AfterMove(s); });
            }
            stripMenu.Show(strip, at);
        }
    }
}
