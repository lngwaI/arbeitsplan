using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Rueckgaengig / Wiederholen (Strg+Z / Strg+Y, ↶ ↷ in der Kalenderleiste). Schnappschuss = Paket-XML (Package.ToXml)
    // samt Kopffeldern, Auswahl und Scrollstand; hoechstens 50, aelteste fallen weg. Ein Schnappschuss entsteht nach jeder abgeschlossenen
    // Aenderung: Maus loslassen (Verschieben), Feld verlassen, Auswahlfeld gewaehlt, Schritt hinzufuegen/entfernen/umsortieren.
    partial class PackageForm
    {
        public const int MaxUndo = 50;

        class Snap
        {
            public string Xml = "", Name = "", Product = "", First = "", Prefix = "";
            public int Cur = -1, Run = -1;
            public Point Scroll;
            public bool Same(Snap o) { return Xml == o.Xml && Name == o.Name && Product == o.Product && First == o.First && Prefix == o.Prefix; }
        }

        readonly List<Snap> snaps = new List<Snap>();
        int snapAt = -1;
        bool undoBusy;
        readonly Button bUndo = new Button(), bRedo = new Button();
        readonly List<Control> leaveHooked = new List<Control>();

        void InitUndoButtons()
        {
            bUndo.Click += delegate { UndoStep(-1); }; bRedo.Click += delegate { UndoStep(1); };
            tip.SetToolTip(bUndo, "Rückgängig (Strg+Z)"); tip.SetToolTip(bRedo, "Wiederholen (Strg+Y)");
            UpdateUndoButtons();
        }

        // In Textfeldern gilt Strg+Z zuerst fuer den Text; sonst fuer den ganzen Editor
        bool TextUndo() { TextBoxBase tb = ActiveControl as TextBoxBase; return tb != null && tb.CanUndo; }

        // Beim Verlassen eines Eingabefelds ist die Aenderung abgeschlossen
        void HookLeave(Control c)
        {
            if (c == this) leaveHooked.RemoveAll(delegate(Control x) { return x.IsDisposed; });   // Equipment-Zeilen werden bei jedem Bind neu gebaut
            foreach (Control k in c.Controls)
            {
                if ((k is TextBoxBase || k is ComboBox || k is NumericUpDown || k is DateTimePicker) && !leaveHooked.Contains(k)) { leaveHooked.Add(k); k.Leave += delegate { CommitUndo(); }; }
                HookLeave(k);
            }
        }

        void ResetUndo() { HookLeave(this); snaps.Clear(); snapAt = -1; CommitUndo(); }

        Snap TakeSnap()
        {
            try
            {
                Snap s = new Snap();
                s.Xml = StepsView().ToXml();
                s.Name = tName.Text; s.Product = tProduct.Text; s.First = tFirst.Text; s.Prefix = prefix;
                s.Cur = steps.IndexOf(cur); s.Run = curRun; s.Scroll = strip.ScrollPos;
                return s;
            }
            catch (Exception) { return null; }
        }

        // Aktuellen Stand als Schritt merken, falls er sich vom letzten unterscheidet (Wiederholen-Zweig faellt weg)
        void CommitUndo()
        {
            if (loading || undoBusy || IsDisposed) return;
            Snap s = TakeSnap();
            if (s == null) return;
            if (snapAt >= 0 && snaps[snapAt].Same(s)) { snaps[snapAt] = s; return; }   // gleicher Stand: nur Auswahl/Scroll auffrischen
            if (snapAt < snaps.Count - 1) snaps.RemoveRange(snapAt + 1, snaps.Count - snapAt - 1);
            snaps.Add(s);
            if (snaps.Count > MaxUndo) snaps.RemoveAt(0);
            snapAt = snaps.Count - 1;
            UpdateUndoButtons();
        }

        void UpdateUndoButtons() { bUndo.Enabled = snapAt > 0; bRedo.Enabled = snapAt >= 0 && snapAt < snaps.Count - 1; }

        // dir -1 = rueckgaengig, +1 = wiederholen
        void UndoStep(int dir)
        {
            CommitUndo();   // noch nicht festgehaltene Eingaben zuerst sichern
            int to = snapAt + dir;
            if (to < 0 || to >= snaps.Count) return;
            Snap target = snaps[to];
            Snap now = snaps[snapAt];
            now.Cur = steps.IndexOf(cur); now.Run = curRun; now.Scroll = strip.ScrollPos;   // Ausgangspunkt fuer Wiederholen/Rueckgaengig
            undoBusy = true;
            try
            {
                if (!Restore(target)) return;
                snapAt = to;
            }
            finally { undoBusy = false; UpdateUndoButtons(); }
        }

        bool Restore(Snap s)
        {
            Package p;
            try { p = Package.FromXml(s.Xml); }
            catch (Exception) { Say("Der Stand lässt sich nicht wiederherstellen."); return false; }
            loading = true;
            steps.Clear(); steps.AddRange(p.Steps);
            pk = new Package(); pk.Steps = steps; pk.Day1 = p.Day1; pk.Lauf1 = p.Lauf1; if (p.TypeIsSet) pk.Type = p.Type;
            tName.Text = s.Name; tProduct.Text = s.Product; prefix = s.Prefix; tFirst.Text = s.First;
            LoadHead();
            loading = false;
            dirty = true;
            pinStep = null; pinDays.Clear();
            PackageStep c = s.Cur >= 0 && s.Cur < steps.Count ? steps[s.Cur] : null;
            if (c == null) { List<PackageStep> o = Ordered(); c = o.Count > 0 ? o[0] : null; }
            nextRun = s.Run;
            Bind(c);
            strip.ScrollPos = s.Scroll;
            Say("");
            return true;
        }

#if TESTHOOKS
        internal int UndoCount { get { return snaps.Count; } }
        internal int UndoAt { get { return snapAt; } }
        internal void TestUndo(int dir) { UndoStep(dir); }
        internal void TestCommit() { CommitUndo(); }
#endif
    }
}
