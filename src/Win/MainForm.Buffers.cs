using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Puffer eines Blocks bearbeiten (Zeile mit Puffer-Typen): "+ Puffer" bzw. Klick auf eine Puffer-Zeile oeffnet BufferForm.
    // Gespeichert wird wie ein Haken (Change): ohne Bearbeitungsmodus sofort mit Protokoll, im Bearbeitungsmodus mit dem Block.
    partial class MainForm
    {
        string lastBufferTyp = "";   // Vorgabe fuer neue Puffer = zuletzt gewaehlter Typ
#if TESTHOOKS
        Func<BufferChoice, bool> bufferTest;   // Selbsttest: statt des Dialogs (true = OK)
#endif

        // index -1 = neuer Puffer, sonst Puffer an dieser Position
        void EditBuffer(CellData c, int index)
        {
            if (!me.Has(Perm.EditText) && !me.Has(Perm.AddText))   // Recht vor dem Dialog pruefen (Feinpruefung folgt in ChangeBuffers)
            {
                if (!quiet) MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!c.Editing) FreshCur(c);
            RowInfo ri = rowInfo[c.RowIndex];
            if (index >= c.Buffers.Count) return;
            BufferItem cur = index >= 0 ? c.Buffers[index] : null;
            BufferChoice ch = new BufferChoice();
            ch.Types = ri.VariantList(); ch.Names = BufferNames(c.RowIndex); ch.Number = index + 1;
            ch.CanUp = index > 0; ch.CanDown = index >= 0 && index < c.Buffers.Count - 1;
            if (cur != null) { ch.Name = cur.Name; ch.Typ = cur.Typ; ch.Text = cur.Text; }
            else ch.Typ = Array.IndexOf(ch.Types, lastBufferTyp) >= 0 ? lastBufferTyp : (ch.Types.Length > 0 ? ch.Types[0] : "");
            if (!AskBuffer(ch)) return;
            lastBufferTyp = ch.Typ;

            string id = cur != null ? cur.Id : null, name = ch.Name, typ = ch.Typ, text = ch.Text;
            BufferChoice.Act act = ch.Action;
            ChangeBuffers(c, delegate(List<BufferItem> l)
            {
                BufferItem b = id == null ? null : l.Find(delegate(BufferItem x) { return x.Id == id; });
                if (act == BufferChoice.Act.Remove) { if (b != null) l.Remove(b); return; }
                if (b == null) b = BufferSet.Add(l, name, typ, text);   // neu (oder inzwischen von einem anderen PC entfernt: bleibt erhalten)
                else { b.Name = name; b.Typ = typ; b.Text = text; }
                if (act == BufferChoice.Act.Up || act == BufferChoice.Act.Down) MoveBuffer(l, b.Id, act == BufferChoice.Act.Up ? -1 : 1);
            });
        }

        bool AskBuffer(BufferChoice ch)
        {
#if TESTHOOKS
            if (bufferTest != null) return bufferTest(ch);
#endif
            using (BufferForm f = new BufferForm(ch)) return f.ShowDialog(this) == DialogResult.OK;
        }

        static void MoveBuffer(List<BufferItem> l, string id, int d)
        {
            int i = l.FindIndex(delegate(BufferItem x) { return x.Id == id; }), j = i + d;
            if (i < 0 || j < 0 || j >= l.Count) return;
            BufferItem t = l[i]; l[i] = l[j]; l[j] = t;
        }

        // Vorschlaege fuer den Namen: bisherige Puffer-Namen der Zeile (angezeigte Tage), dann Katalog-Bausteine dieser Zeile bzw. mit "Puffer" im Namen
        List<string> BufferNames(int row)
        {
            List<string> r = new List<string>();
            foreach (CellData x in view.Cells)
                if (x.RowIndex == row) foreach (BufferItem b in x.Buffers) AddName(r, b.Name);
            foreach (Baustein b in store.GetCatalog().Bausteine)
                if (b.Row == rowInfo[row].Name || b.Name.IndexOf("Puffer", StringComparison.OrdinalIgnoreCase) >= 0) AddName(r, b.Name);
            return r;
        }

        static void AddName(List<string> l, string n)
        {
            if (n.Length > 0 && !l.Exists(delegate(string x) { return string.Equals(x, n, StringComparison.OrdinalIgnoreCase); })) l.Add(n);
        }

        // Rechte fuer die Puffer-Liste: Hinzufuegen AddText (oder EditText), Entfernen DeleteText, Aendern EditText. null = erlaubt, sonst fehlendes Recht.
        string BufferRightMissing(Entry old, List<BufferItem> now)
        {
            bool add, rem, chg; BufferSet.Diff(old.Buffers, now, out add, out rem, out chg);
            if (rem && !me.Has(Perm.DeleteText)) return "Text löschen";
            if (chg && !me.Has(Perm.EditText)) return "Text ändern";
            if (add && !me.Has(Perm.AddText) && !me.Has(Perm.EditText)) return "Text hinzufügen";
            return null;
        }

        // op ändert die Puffer-Liste (am Block und, falls ein anderer PC den Block inzwischen geaendert hat, am neuen Stand erneut angewendet)
        void ChangeBuffers(CellData c, Action<List<BufferItem>> op)
        {
            if (!c.Editing) FreshCur(c);
            List<BufferItem> now = BufferSet.CloneAll(c.Buffers); op(now);
            if (BufferSet.Key(now) == BufferSet.Key(c.Buffers)) return;
            string miss = BufferRightMissing(c.Cur, now);
            if (miss != null)
            {
                if (!quiet) MessageBox.Show("Für diese Änderung fehlt das Recht \"" + miss + "\".", "Keine Berechtigung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Change(c, delegate(CellData x) { op(x.Buffers); x.Dirty = true; },
                delegate(Entry e) { List<BufferItem> t = BufferSet.CloneAll(e.Buffers); op(t); return BufferSet.Key(t) == BufferSet.Key(e.Buffers); },
                delegate(Entry e) { op(e.Buffers); });
        }
    }
}
