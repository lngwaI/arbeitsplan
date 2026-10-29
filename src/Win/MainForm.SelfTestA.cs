#if TESTHOOKS
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests: Inhalt nur aus Leerraum gilt als leer (Entry.HasText/IsBlank), Ausweich-Dropdown ohne Hauptgeraet.
    partial class MainForm
    {
        void SelfTestA(StringBuilder sb)
        {
            Entry e = new Entry(); e.Content = " \t\n  \n";
            Entry f = new Entry(); f.Content = " x ";
            Entry g = new Entry(); g.Content = "\n "; g.Lkz = true;
            bool okBlank = !e.HasText && e.IsBlank && f.HasText && !f.IsBlank && !g.IsBlank && e.Content == " \t\n  \n";
            sb.AppendLine("Leerraum-Inhalt: gilt als leer=" + Ok(okBlank));

            Catalog cat = Catalog.Default();
            EquipAssign a = new EquipAssign(); a.Short = "TFF"; a.Choices.Add(new EquipChoice("TFF", "H1-X-0101"));
            EquipRow row = new EquipRow(a, cat, "", delegate { }, delegate { });
            List<string> items = row.FallbackItems();
            bool okFb = !items.Contains("H1-X-0101") && !items.Exists(delegate(string s) { return s.EndsWith(" H1-X-0101"); });
            sb.AppendLine("Ausweich-Liste ohne Hauptgeraet=" + Ok(okFb) + " (" + items.Count + " Einträge)");
        }
    }
}
#endif
