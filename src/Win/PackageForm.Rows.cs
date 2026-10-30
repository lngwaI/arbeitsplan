using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Zeilen des normalen Schritts (UX-5e): Position hinzufuegen/entfernen/verschieben nur an der betroffenen Zeile (Pool, keine
    // neuen Steuerelemente), Aenderung eines Felds (Tippen gebuendelt: PackageForm.Smooth.cs).
    partial class PackageForm
    {
        EquipRow AddEquipRow(EquipAssign a)
        {
            EquipRow er = rowPool.Get(a, cat, Product, delegate { Changed(); }, RemoveAssign, false);
            er.Visible = true;   // aus dem Pool kommt sie versteckt
            eqRows.Add(er); equipOrder.Add(er);
            return er;
        }

        // Neue Position: nur eine Zeile kommt dazu, die anderen bleiben (Instanzen, Fokus, Bildlauf)
        void AddPosition(string shortName)
        {
            if (cur == null) return;
            EquipAssign a = new EquipAssign(); a.Short = shortName;
            cur.Equipment.Add(a);
            dirty = true;
            HoldDraw();
            try { HookLeave(AddEquipRow(a)); UpdateView(false); }
            finally { ReleaseDraw(); }
        }

        // Position entfernen: nur diese Zeile geht in den Pool
        void RemoveAssign(EquipAssign a)
        {
            if (cur == null) return;
            cur.Equipment.Remove(a); dirty = true;
            HoldDraw();
            try
            {
                int i = eqRows.FindIndex(delegate(EquipRow r) { return r.Assign == a; });
                if (i >= 0) { rowPool.Release(eqRows[i]); eqRows.RemoveAt(i); }
                SyncEquipOrder(); UpdateView(false);
            }
            finally { ReleaseDraw(); }
        }

        // Reihenfolge im RowReorder wie die Zeilen im Container
        void SyncEquipOrder() { equipOrder.Clear(); foreach (EquipRow r in eqRows) equipOrder.Add(r); }

        // Zeile f nach t verschoben (Modell ist schon geaendert): dieselben Steuerelemente in neuer Reihenfolge, kein Neuaufbau
        void MoveEquipRow(int f, int t)
        {
            if (f < 0 || f >= eqRows.Count) return;
            EquipRow x = eqRows[f]; eqRows.RemoveAt(f); eqRows.Insert(Math.Max(0, Math.Min(eqRows.Count, t)), x);
            SyncEquipOrder(); UpdateView(false); CommitUndo();
        }

        // Aenderung eines Felds: Ansicht nachziehen. Beim Tippen in einem Textfeld erst nach kurzer Ruhe (ein UpdateView je Serie), Schnappschuss beim Verlassen des Felds.
        void Changed()
        {
            if (loading) return;
            dirty = true;
            if (Typing()) { ScheduleView(false); return; }
            CancelView(); UpdateView(false); CommitUndo();
        }

        // N7: Tippen in einem Textfeld oder einem frei beschreibbaren Geraete-Feld (EquipRow): Ansicht nach Ruhe, Rueckgaengig-Schnappschuss beim Verlassen
        bool Typing()
        {
            Control a = ActiveControl;   // ohne Abstieg in Container (NumericUpDown enthaelt ein Textfeld: Pfeile sollen sofort wirken)
            ComboBox cb = a as ComboBox;
            return a is TextBoxBase || (cb != null && cb.DropDownStyle != ComboBoxStyle.DropDownList && cb.Parent is EquipRow && !cb.DroppedDown);
        }

    }
}
