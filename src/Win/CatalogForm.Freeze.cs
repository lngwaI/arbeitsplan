using System;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Katalog-Reiter "Freeze": Standardwerte neuer Freeze-Vorgaenge und Vorlagen (Catalog.FreezeDefaults). Bestehende Vorgaenge behalten ihre Werte.
    partial class CatalogForm
    {
        NumericUpDown nFz2, nFz3;

        void BuildFreeze(TabPage p)
        {
            FreezeConfig c = cat.FreezeDefaults;
            p.Controls.Add(L("Standardwerte für neue Freeze-Vorgänge und Vorlagen; vorhandene Vorgänge behalten ihre Werte.", 8, 8));
            FzHours(p, "Dauer je Zyklus (Stunden):", 36, 0.25m, c.DauerMin, delegate(int v) { c.DauerMin = v; });
            FzHours(p, "Zeit zwischen zwei Zyklen, je Anlage (Stunden):", 66, 0.25m, c.AbstandMin, delegate(int v) { c.AbstandMin = v; });
            p.Controls.Add(L("Anzahl Zyklen:", 8, 98));
            NumericUpDown cnt = new NumericUpDown(); cnt.Minimum = 1; cnt.Maximum = FreezeCalc.MaxCycles; cnt.Value = Math.Max(1, Math.Min(FreezeCalc.MaxCycles, c.Cycles)); cnt.SetBounds(310, 96, 80, 24);
            cnt.ValueChanged += delegate { c.Cycles = (int)cnt.Value; };
            p.Controls.Add(cnt);
            nFz2 = FzHours(p, "Start Zyklus 2 ohne Eingabe: Zyklus 1 + (Stunden):", 126, 0, c.Offset2Min, delegate(int v) { c.Offset2Min = v; });
            nFz3 = FzHours(p, "Start Zyklus 3 ohne Eingabe: Zyklus 1 + (Stunden):", 156, 0, c.Offset3Min, delegate(int v) { c.Offset3Min = v; });
        }

        static NumericUpDown FzHours(TabPage p, string text, int y, decimal min, int value, Action<int> set)
        {
            p.Controls.Add(L(text, 8, y + 2));
            NumericUpDown n = new NumericUpDown(); n.DecimalPlaces = 2; n.Increment = 0.25m; n.Minimum = min; n.Maximum = 168; n.SetBounds(310, y, 80, 24);
            n.Value = Math.Max(min, Math.Min(168m, Math.Round(value / 60m, 2)));
            n.ValueChanged += delegate { set((int)Math.Round(n.Value * 60m)); };
            p.Controls.Add(n);
            return n;
        }

#if TESTHOOKS
        public FreezeConfig TestFzOffsets(decimal h2, decimal h3) { nFz2.Value = h2; nFz3.Value = h3; return cat.FreezeDefaults; }
        public CatalogForm TestFreezeTab()
        {
            foreach (Control x in Controls) { TabControl t = x as TabControl; if (t != null) t.SelectedIndex = t.TabCount - 1; }
            return this;
        }
#endif
    }
}
