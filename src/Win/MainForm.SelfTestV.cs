#if TESTHOOKS
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest Task V: Paket-Editor Abschnitt "3 Vorschau & Pruefung" (Kalender als Mini-Plan, Blaettern, verstellbare Hoehe); Ablauf in PackageForm.TestV.cs.
    partial class MainForm
    {
        void SelfTestV(StringBuilder sb) { sb.AppendLine(PackageForm.TestV(new MemoryStore(), equip, start)); }
    }
}
#endif
