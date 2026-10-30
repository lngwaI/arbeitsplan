using System;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Paket-Editor, ruhige Anzeige (UX-5e): Tippen in Textfeldern zieht Vorschau, Kalender, Hinweise und Anordnung erst nach kurzer Ruhe
    // nach (ein UpdateView je Tippserie statt je Zeichen); das Modell ist sofort aktuell. Eigene Panels mit WS_EX_COMPOSITED puffern das
    // Zeichnen samt Kindern (kein Zerreissen beim Vergroessern/Verschieben).
    partial class PackageForm
    {
        const int ViewDelay = 150;   // ms Ruhe nach dem letzten Zeichen
        readonly Timer viewTimer = new Timer();
        bool viewDue, rebindDue;

        void InitSmooth()
        {
            viewTimer.Interval = ViewDelay;
            viewTimer.Tick += delegate { FlushView(); };
            Disposed += delegate { viewTimer.Stop(); viewTimer.Dispose(); };
        }

        // Ansicht nach kurzer Ruhe nachziehen (rebind: Zeilen neu binden, z. B. anderes Produkt); jede weitere Eingabe schiebt den Zeitpunkt hinaus
        void ScheduleView(bool rebind) { viewDue = true; rebindDue |= rebind; viewTimer.Stop(); viewTimer.Start(); }

        void CancelView() { viewTimer.Stop(); viewDue = rebindDue = false; }

        // Ausstehende Aktualisierung sofort ausfuehren (Timer, Feld verlassen, Schnappschuss); true = es war etwas ausstehend
        bool FlushView()
        {
            if (!viewDue) return false;
            bool rebind = rebindDue;
            CancelView();
            if (!IsDisposed && cur != null) { if (rebind) Bind(cur); else UpdateView(false); }
            return true;
        }
    }

    // Panel, das samt Kindfenstern in einem Zug gezeichnet wird (WS_EX_COMPOSITED); fuer den Bildlaufbereich der Schritt-Details
    class SmoothPanel : Panel
    {
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }
    }
}
