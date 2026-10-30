#if TESTHOOKS
using System;
using System.Text;

namespace Arbeitsplan.Win
{
    // UX-6: Selbsttests und Testbilder des Paket-Editor-Teils (Tests in PackageForm.TestUx6.cs). Beschriftungen nie "Fehler" nennen.
    partial class MainForm
    {
        partial void SelfTestUX6Editor(StringBuilder sb)
        {
            sb.AppendLine(PackageForm.TestUx6Editor(store, equip, start));
        }

        partial void Ux6FormEditor(string which, ref System.Windows.Forms.Form f)
        {
            if (which == "ux6pick" || which == "ux6pick0") f = PackageForm.Ux6PickForm(store, which == "ux6pick");
            else if (which == "ux6add") f = PackageForm.Ux6AddForm(store, equip, start);
        }
    }
}
#endif
