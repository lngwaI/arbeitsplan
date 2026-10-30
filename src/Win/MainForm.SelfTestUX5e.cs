#if TESTHOOKS
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Selbsttests UX-5e (Leistung/Flackern): Paket-Editor (PackageForm.TestUx5e.cs) und Hauptfenster-Wochenansicht (Doppelpuffer, Resize ohne
    // Neuaufbau der Zellen). Schnelllauf nur dafuer: Umgebungsvariable AP_ONLY=ux5e. Beschriftungen nie "Fehler" nennen: der Workflow zaehlt Treffer.
    partial class MainForm
    {
        static bool Buffered(Control c)
        {
            System.Reflection.PropertyInfo p = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (bool)p.GetValue(c, null);
        }

        void SelfTestUX5e(StringBuilder sb)
        {
            sb.AppendLine(PackageForm.TestUx5e(store, equip, start));   // Editor (PackageForm.TestUx5e.cs)
            // Hauptfenster: Wochenansicht ist doppelt gepuffert, Groessenaenderung baut die Zellen nicht neu auf
            int cells0 = view.Cells.Count; object c00 = cells0 > 0 ? view.Cells[0] : null;
            Size cs = ClientSize;
            ClientSize = new Size(cs.Width - 40, cs.Height - 20); Application.DoEvents();
            ClientSize = cs; Application.DoEvents();
            bool okSame = cells0 > 0 && view.Cells.Count == cells0 && view.Cells[0] == c00;
            sb.AppendLine("UX5e Hauptfenster: Wochenansicht doppelt gepuffert " + Ok(Buffered(view) && Buffered(canvas)) + " Resize ohne Neuaufbau der Zellen " + Ok(okSame));
        }
    }
}
#endif
