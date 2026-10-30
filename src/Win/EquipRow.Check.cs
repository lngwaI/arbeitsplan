using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pruefungen einer Equipment-Zeile (validiert, Platzhalter) und die Zugehoerigkeitspruefung vor Speichern/Uebernehmen (N7: aus PackageForm.Equip.cs verschoben).
    partial class EquipRow
    {
        // U8: Strg+Z im Feld gilt dem getippten Text; nach dem Verlassen des Felds gilt Strg+Z fuer den ganzen Editor
        const string UndoTip = "\nStrg+Z im Feld macht das Tippen rückgängig; nach Verlassen des Felds gilt Strg+Z für den ganzen Paket-Editor.";

        // U10: Zeile rot hinterlegen (Zubehoer passt nicht zum Hauptgeraet); Load/Rebind setzt zurueck
        bool flagged;
        public bool Flagged { get { return flagged; } }

        public void SetFlag(bool on)
        {
            if (flagged == on) return;
            flagged = on; BackColor = on ? Color.FromArgb(253, 222, 222) : Color.Empty;   // Empty = Hintergrund des Containers
        }

        // N7 (15): ToolTip gehoert der Zeile und wird mit ihr freigegeben (RowPool verwirft ueberzaehlige Zeilen per Dispose)
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
#if TESTHOOKS
        internal ToolTip TestTip { get { return tip; } }
        internal ComboBox TestFbBox { get { return cbFb; } }
#endif

        // Platzhalter aus den erlaubten Geraeten (Produktprofil oder global), z. B. H1-X-010X
        static string Placeholder(Catalog cat, string product, string sh) { EquipPosition t = new EquipPosition(); t.Ids = cat.Allowed(product, sh); return t.Placeholder(); }

        // Ist das eingetragene Geraet fuer das Produkt erlaubt (oder leer/abwechselnd)? Fuer die Pruefhinweise.
        public static bool Known(EquipAssign a, Catalog cat, string product)
        {
            if (a.Alternate || a.Choices.Count == 0) return true;
            string t = a.Choices[0].Id;
            return cat.Allowed(product, a.Short).Contains(t) || (t.Length > 0 && t == Placeholder(cat, product, a.Short));
        }

        // Frei eingetippte (nicht erlaubte/validierte) Geraete einer Zeile: Geraet, Wechselgeraete, Ausweich-Geraet (Austausch-Gruppe)
        public static List<string> Unvalidated(EquipAssign a, Catalog cat, string product)
        {
            List<string> r = new List<string>();
            if (!Known(a, cat, product)) r.Add(a.Choices[0].Id);
            List<EquipChoice> grp = cat.Interchangeable(a.Short);
            List<string> swaps = cat.SwapsFor(product, a.Short, ""), allowed = cat.Allowed(product, a.Short);
            string ph = Placeholder(cat, product, a.Short);
            List<string> more = new List<string>();
            if (a.Alternate) foreach (EquipChoice c in a.Choices) more.Add(c.Id);
            if (a.Fallback.Length > 0) more.Add(a.Fallback);
            foreach (string id in more)
                if (id.Length > 0 && !r.Contains(id) && id != ph && !allowed.Contains(id) && !swaps.Contains(id) && cat.Master.Device(id) == null && !grp.Exists(delegate(EquipChoice c) { return c.Id == id; })) r.Add(id);
            return r;
        }
    }

    partial class PackageForm
    {
        // Harter Fehler vor Speichern/Uebernehmen: Zubehoer eines anderen Hauptgeraets im selben Lauf. true = in Ordnung.
        static bool BelongsOk(IWin32Window owner, Package p, Catalog cat, string action)
        {
            List<string> e = cat.CheckPackage(p);
            if (e.Count == 0) return true;
            MessageBox.Show(owner, "Das Paket kann nicht " + action + " werden – Zubehör ist mit dem falschen Hauptgerät eingeplant:\n\n" + string.Join("\n", e.ToArray())
                + "\n\nBitte das Zubehör oder das Hauptgerät anpassen.", "Equipment passt nicht zusammen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // U10: Beteiligte einer Zugehoerigkeits-Meldung ("Gerät X gehört zu Y, eingeplant ist aber Z.") -> X und Z
        static List<string> BelongIds(string e)
        {
            List<string> r = new List<string>();
            int a = e.IndexOf("Gerät ", StringComparison.Ordinal), b = e.IndexOf(" gehört zu ", StringComparison.Ordinal), c = e.IndexOf("eingeplant ist aber ", StringComparison.Ordinal);
            if (a >= 0 && b > a) r.Add(e.Substring(a + 6, b - a - 6));
            if (c >= 0) r.Add(e.Substring(c + 20).TrimEnd('.', ' '));
            return r;
        }

        readonly HashSet<EquipRow> belongRows = new HashSet<EquipRow>();   // Zeilen des aktuellen Schritts mit Zugehoerigkeits-Fehler (rot)

        // Karte (Puffer) bzw. Zeile (normaler Schritt) der beteiligten Geraete rot markieren
        void MarkBelongs(PackageStep s, string e)
        {
            foreach (string id in BelongIds(e))
            {
                if (s.IsBuffer) { MarkDevice(s, 0, id); continue; }
                if (s != cur) continue;
                foreach (EquipRow r in eqRows)
                    if (r.Assign.Choices.Exists(delegate(EquipChoice c) { return string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase); })) belongRows.Add(r);
            }
        }

        void FlagRows() { foreach (EquipRow r in eqRows) r.SetFlag(belongRows.Contains(r)); }

        // U10: nach der Meldung den ersten betroffenen Schritt waehlen, die rote Karte aufklappen bzw. die rote Zeile ins Bild holen
        internal void GoToBelongs()
        {
            foreach (PackageStep s in Ordered())
            {
                Package one = new Package(); one.ProductCode = Product; one.RunPrefix = prefix; one.Steps.Add(s);
                if (s.IsEmpty || cat.CheckPackage(one).Count == 0) continue;
                if (s != cur) Bind(s); else UpdateView(false);
                HashSet<int> set;
                if (s.IsBuffer && pufBad.TryGetValue(s, out set)) { foreach (int k in set) if (k < s.Puffers.Count) openSpecs.Add(s.Puffers[k]); LayoutDetails(); }
                foreach (EquipRow r in eqRows) if (r.Flagged) { scroll.ScrollControlIntoView(r); break; }
                foreach (PufferCard c in cards) if (c.Bad) { scroll.ScrollControlIntoView(c); break; }
                return;
            }
        }
    }
}
