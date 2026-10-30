using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor (UX-5a): "Packen-Nr." im Kopf. Die 8-stellige Nummer des Packens wird von Hand eingetragen (kein Vorschlag) und in alle
    // Packen-Schritte (Package.HandNoSteps; Einzelpaket: erster Schritt) uebernommen. Im Schritt bleibt das Feld als Ueberschreibung
    // ("wie Kopf" / "weicht vom Kopf ab"); eine Aenderung im Kopf zieht nur Schritte nach, die noch den alten Kopfwert haben oder leer sind.
    // Es gibt kein eigenes Paketfeld: der Kopfwert wird beim Laden aus den Schritten gelesen (ManualNo, Format unveraendert).
    partial class PackageForm
    {
        readonly TextBox tPackNo = new TextBox();
        Label lPackNo, lManualHint;
        bool packShow;
        string packPrev = "";   // zuletzt uebernommener gueltiger Kopfwert ("" = keiner)

        List<PackageStep> PackSteps() { return StepsView().HandNoSteps(); }

        // Feld nur zeigen, wenn es Packen-Schritte gibt (Einzelpaket: erster Schritt) und das Paket kein ausdrueckliches Sonderereignis ist
        void SyncPackNo()
        {
            bool show = PackSteps().Count > 0 && !(pk.Type == PackageType.Ereignis && pk.TypeIsSet);
            string t = pk.Type == PackageType.Einzel ? "Laufnummer:" : "Packen-Nr.:";
            if (lPackNo.Text != t) lPackNo.Text = t;
            if (show != packShow) { packShow = show; LayoutHead(headH); }
        }

        // Kopfwert aus den Schritten lesen (Laden, Rueckgaengig): erste vorhandene Nummer
        void LoadPackNo()
        {
            string v = "";
            foreach (PackageStep s in PackSteps()) if (s.ManualNo.Length > 0) { v = s.ManualNo; break; }
            bool was = loading; loading = true; tPackNo.Text = v; tPackNo.ForeColor = SystemColors.WindowText; loading = was;
            packPrev = v;
        }

        string PackNoError()
        {
            string t = tPackNo.Text.Trim();
            return packShow && t.Length > 0 && !PackageStep.IsManualNo(t) ? "Packen-Nr.: bitte 8 Ziffern eingeben (z. B. 12340001)." : null;
        }

        void PackNoTyped()
        {
            if (loading) return;
            string t = tPackNo.Text.Trim();
            bool ok = t.Length == 0 || PackageStep.IsManualNo(t);
            tPackNo.ForeColor = ok ? SystemColors.WindowText : Color.Firebrick;
            if (ok) ApplyPackNo(t);
            Changed();
        }

        // Kopfwert v in die Packen-Schritte eintragen (leere und solche mit dem alten Kopfwert; eigene Abweichungen bleiben)
        void ApplyPackNo(string v)
        {
            foreach (PackageStep s in PackSteps()) if (s.ManualNo.Length == 0 || s.ManualNo == packPrev) s.ManualNo = v;
            packPrev = v;
            if (cur != null) { bool was = loading; loading = true; tManualNo.Text = cur.ManualNo; tManualNo.ForeColor = SystemColors.WindowText; ShowManualHint(cur); loading = was; }
        }

        // Neuer Packen-Schritt: Kopfwert uebernehmen
        void AdoptPackNo(PackageStep s)
        {
            if (packPrev.Length > 0 && s.ManualNo.Length == 0 && PackSteps().Contains(s)) s.ManualNo = packPrev;
        }

        // Eingabe im Schritt: leer = wie Kopf, gueltig = Ueberschreibung, sonst (halb getippt) keine Nummer
        string StepPackNo(string text)
        {
            string t = text.Trim();
            return t.Length == 0 ? packPrev : PackageStep.IsManualNo(t) ? t : "";
        }

        string ManualHint(PackageStep s)
        {
            if (packPrev.Length == 0) return "";
            if (s.ManualNo.Length == 0 || s.ManualNo == packPrev) return "leer oder gleich = wie Packen-Nr. im Kopf (" + packPrev + ")";
            return "weicht vom Kopf ab (Packen-Nr.: " + packPrev + ")";
        }

        void BuildManualHint(Control parent, int x, int w)
        {
            lManualHint = Lbl(parent, "", x, w); lManualHint.AutoEllipsis = true; lManualHint.Height = Font.Height + 2; lManualHint.ForeColor = Section.HelpColor;
            lManualHint.Anchor = TLR;
        }

        void ShowManualHint(PackageStep s)
        {
            string h = ManualHint(s);
            lManualHint.Text = h; lManualHint.ForeColor = h.StartsWith("weicht") ? Color.DarkOrange : Section.HelpColor;
            tip.SetToolTip(lManualHint, h);
        }
    }
}
