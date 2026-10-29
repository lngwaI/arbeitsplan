using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Reihenfolge und Termine aendern, ohne die Ansicht neu aufzubauen. Ablaufliste: ▲/▼, Alt+Hoch/Runter und Ziehen
    // (Einfuegelinie, auch bis ans Ende); Auswahl bleibt auf dem verschobenen Schritt. Kalender (PlanStrip): Block auf einen anderen
    // Tag ziehen = dieser und alle folgenden Laeufe (Move), mit Umschalt = nur dieser Lauf (Shift); verschobene Laeufe sind fest (Schloss)
    // und bleiben bei Regelaenderungen an ihrem Tag (Repin); Rechtsklick-Menue fuer dasselbe tageweise und zum Zuruecksetzen.
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
            if (!s.MoveAll(delta, p.Day1)) return false;
            p.Normalize(false);
            return true;
        }

        // Nach dem Verschieben: nur die Wann?-Felder, Liste (Texte in place), Kalender und Hinweise nachziehen; Equipment bleibt stehen
        void AfterMove(PackageStep s)
        {
            dirty = true;
            StepsView().Normalize(false);   // nach jedem Verschieben: frueheste Termine (Puffer -1) nie vor Tag 1
            RecordPins(s);   // bewusste Aenderung: die neuen Tage sind der Sollstand der festen Laeufe
            if (s != cur) Bind(s);
            else { BindWhen(s); UpdateView(true); }
            CommitUndo();
        }

        // ---------- feste (von Hand verschobene) Laeufe ----------

        PackageStep pinStep;                                   // Schritt, dessen feste Laeufe vermerkt sind
        readonly Dictionary<int, int> pinDays = new Dictionary<int, int>();   // Lauf -> Tag des Pakets

        void RecordPins(PackageStep s)
        {
            pinStep = s; pinDays.Clear();
            if (s == null || IsJe(s)) return;
            List<int> d = s.Days(pk.Day1);
            for (int i = 0; i < d.Count; i++) if (s.Pinned(i)) pinDays[i] = d[i];
        }

        // Regel des gewaehlten Schritts geaendert (Anzahl, Abstand, Wochentage ...): feste Laeufe bleiben an ihrem Tag, der Versatz passt sich an
        void Repin()
        {
            if (pinStep != cur || cur == null || pinDays.Count == 0) return;
            if (IsJe(cur)) { pinDays.Clear(); return; }
            List<int> keys = new List<int>(pinDays.Keys); keys.Sort();
            foreach (int run in keys)
            {
                List<int> d = cur.Days(pk.Day1);
                if (run >= d.Count || d[run] == pinDays[run]) continue;
                Dictionary<int, int> map = cur.Shift.ContainsKey(run) || !cur.Move.ContainsKey(run) ? cur.Shift : cur.Move;
                int v; map.TryGetValue(run, out v); v += pinDays[run] - d[run];
                if (v == 0) map.Remove(run); else map[run] = v;
            }
        }

        // ---------- Kalender ----------

        // Block im Kalender auf toDay gezogen: dieser und alle folgenden Laeufe (der erste Lauf = ganzer Schritt) oder (onlyRun) nur dieser Lauf.
        // Vorher wird auf einer Kopie geprueft (Package.Check, Regel E4): entstuenden zwei Hauptlaeufe am selben Tag im selben Block, passiert nichts.
        void MoveInCalendar(PackageStep s, int run, int toDay, bool onlyRun)
        {
            CommitUndo();
            int si = steps.IndexOf(s), at = -1;
            foreach (RunDate d in Plan()) if (d.Step == si && d.Run == run) at = d.Day;
            if (at < 0 || toDay == at) return;
            int delta = toDay - at;
            Package trial = StepsView().Clone();
            if (!trial.DragRun(trial.Steps[si], run, delta, onlyRun))
            {
                Say(onlyRun && IsJe(s) ? "Dieser Schritt folgt dem Hauptlauf – er lässt sich nur für alle Läufe verschieben." : "Nicht verschoben: Dort ist kein Termin möglich (vor dem ersten Tag oder Versatz zu groß).");
                return;
            }
            string clash = NewClash(StepsView(), trial);
            if (clash != null) { Say("Nicht verschoben: " + clash); return; }
            if (!StepsView().DragRun(s, run, delta, onlyRun)) return;
            Say(""); nextRun = run; AfterMove(s);
        }

        // Erste Sperre "Zwei Hauptläufe am selben Tag", die nur nach dem Verschieben besteht; null = keine neue
        static string NewClash(Package before, Package after)
        {
            List<string> old = new List<string>();
            foreach (PackageProblem p in before.Check()) if (p.Code == "gleichtag") old.Add(p.Text);
            foreach (PackageProblem p in after.Check()) if (p.Code == "gleichtag" && !old.Contains(p.Text)) return p.Text;
            return null;
        }

        // Verschiebung(en) entfernen: run >= 0 = nur dieser Lauf (Move und Shift), -1 = alle des Schritts
        void ResetRun(PackageStep s, int run)
        {
            CommitUndo();
            if (run >= 0 ? !s.ResetRun(run) : !s.ResetMoves()) return;
            Say(""); nextRun = run; AfterMove(s);
        }

        void StripMenu(PlanStrip.Item it, Point at)
        {
            PackageStep s = it.Step; int run = it.Run, day = it.Day;
            if (s != cur) { nextRun = run; Bind(s); }
            stripMenu.Items.Clear();
            bool many = StepsView().RunCountOf(s) > 1, je = IsJe(s);
            string who = many && run > 0 && !je ? "Diesen und alle folgenden Läufe" : (je ? "Alle Läufe" : "Ganzen Schritt");
            stripMenu.Items.Add(who + " 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, false); });
            stripMenu.Items.Add(who + " 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, false); });
            if (many && !je)
            {
                stripMenu.Items.Add(new ToolStripSeparator());
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag früher", null, delegate { MoveInCalendar(s, run, day - 1, true); }).Enabled = day > 1;
                stripMenu.Items.Add("Nur diesen Lauf 1 Tag später", null, delegate { MoveInCalendar(s, run, day + 1, true); });
            }
            if (!je && s.Shift.Count + s.Move.Count > 0)
            {
                stripMenu.Items.Add(new ToolStripSeparator());
                stripMenu.Items.Add("Diesen Lauf auf Regel zurücksetzen", null, delegate { ResetRun(s, run); }).Enabled = s.Pinned(run);
                stripMenu.Items.Add("Alle Verschiebungen dieses Schritts zurücksetzen", null, delegate { ResetRun(s, -1); });
            }
            stripMenu.Show(strip, at);
        }
    }
}
