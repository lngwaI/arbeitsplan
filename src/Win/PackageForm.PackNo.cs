using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor (UX-5a): "Packen-Nr." im Kopf. Die 8-stellige Nummer des Packens wird von Hand eingetragen (kein Vorschlag). Jedes Packen hat eine
    // eigene Nummer: Feld i gehoert zu Package.HandNoSteps()[i] (Einzelpaket: erster Schritt); das zweite Feld erscheint nur bei einem zweiten Packen.
    // Im Schritt bleibt das Feld als Ueberschreibung ("wie Kopf" / "weicht vom Kopf ab"); eine Aenderung im Kopf zieht den Schritt nur nach, wenn er noch
    // den alten Kopfwert hat oder leer ist. Es gibt kein eigenes Paketfeld: der Kopfwert wird aus den Schritten gelesen (ManualNo, Format unveraendert).
    // shortcut: der Kopf zeigt zwei Packen, ein drittes bekommt seine Nummer im Schritt oder beim Kampagnenstart; Felder ergaenzen, falls das vorkommt.
    partial class PackageForm
    {
        readonly TextBox tPackNo = new TextBox(), tPackNo2 = new TextBox();
        Label lPackNo, lPackNo2, lManualHint;
        bool packShow, packShow2;
        readonly string[] packPrev = { "", "" };                 // zuletzt uebernommener gueltiger Kopfwert je Feld ("" = keiner)
        readonly PackageStep[] packRef = new PackageStep[2];     // Schritte hinter den Feldern; aendern sie sich, werden die Felder neu gelesen

        List<PackageStep> PackSteps() { return StepsView().HandNoSteps(); }
        TextBox PackBox(int i) { return i == 0 ? tPackNo : tPackNo2; }
        static PackageStep PackAt(List<PackageStep> l, int i) { return i < l.Count ? l[i] : null; }

        // Felder nur zeigen, wenn es Packen-Schritte gibt (Einzelpaket: erster Schritt) und das Paket kein ausdrueckliches Sonderereignis ist
        void SyncPackNo()
        {
            List<PackageStep> l = PackSteps();
            bool show = l.Count > 0 && !(pk.Type == PackageType.Ereignis && pk.TypeIsSet), show2 = show && l.Count > 1;
            string t = pk.Type == PackageType.Einzel ? "Laufnummer:" : "Nr. für Packen:";
            if (lPackNo.Text != t) lPackNo.Text = t;
            if (l.Count > 0 && (PackAt(l, 0) != packRef[0] || PackAt(l, 1) != packRef[1])) LoadPackNo();   // Packen entfernt, hinzugefuegt oder umsortiert
            if (show != packShow || show2 != packShow2) { packShow = show; packShow2 = show2; LayoutHead(headH); }
        }

        // Kopfwerte aus den Schritten lesen (Laden, Rueckgaengig, andere Packen)
        void LoadPackNo()
        {
            List<PackageStep> l = PackSteps();
            bool was = loading; loading = true;
            for (int i = 0; i < 2; i++)
            {
                packRef[i] = PackAt(l, i);
                packPrev[i] = packRef[i] != null ? packRef[i].ManualNo : "";
                PackBox(i).Text = packPrev[i]; PackBox(i).ForeColor = SystemColors.WindowText;
            }
            loading = was;
        }

        // Erstes sichtbares Feld mit ungueltiger Eingabe; null = alle in Ordnung
        TextBox PackNoBad()
        {
            for (int i = 0; i < 2; i++)
            {
                string t = PackBox(i).Text.Trim();
                if ((i == 0 ? packShow : packShow2) && t.Length > 0 && !PackageStep.IsManualNo(t)) return PackBox(i);
            }
            return null;
        }

        string PackNoError()
        {
            TextBox b = PackNoBad();
            return b == null ? null : (b == tPackNo2 ? "2. Packen" : "Nr. für Packen") + ": bitte 8 Ziffern eingeben (z. B. 12340001).";
        }

        void PackNoTyped(int i)
        {
            if (loading) return;
            string t = PackBox(i).Text.Trim();
            bool ok = t.Length == 0 || PackageStep.IsManualNo(t);
            PackBox(i).ForeColor = ok ? SystemColors.WindowText : Color.Firebrick;
            if (ok) ApplyPackNo(i, t);
            Changed();
        }

        // Kopfwert v in das zugehoerige Packen eintragen (wenn leer oder mit dem alten Kopfwert; eine eigene Abweichung bleibt)
        void ApplyPackNo(int i, string v)
        {
            PackageStep s = PackAt(PackSteps(), i);
            if (s != null && (s.ManualNo.Length == 0 || s.ManualNo == packPrev[i])) s.ManualNo = v;
            packPrev[i] = v;
            if (cur != null) { bool was = loading; loading = true; tManualNo.Text = cur.ManualNo; tManualNo.ForeColor = SystemColors.WindowText; ShowManualHint(cur); loading = was; }
        }

        // Kopfwert zum Schritt ("" = kein Feld im Kopf)
        string PrevOf(PackageStep s)
        {
            int i = PackSteps().IndexOf(s);
            return i >= 0 && i < 2 ? packPrev[i] : "";
        }

        // Neuer Packen-Schritt: Kopfwert seines Felds uebernehmen (nur das erste kann schon einen haben, z. B. nach Entfernen)
        void AdoptPackNo(PackageStep s)
        {
            if (s.ManualNo.Length == 0) s.ManualNo = PrevOf(s);
        }

        // Vor dem Einfuegen: weiteres Packen bekommt die naechste freie PackNo und den Tag des ersten Packens (sonst Sperre packen-tag).
        // true = es gab schon ein Packen (einmaliger Hinweis, eigener Back-Up)
        bool NewPacken(PackageStep s)
        {
            PackageStep first = steps.Find(delegate(PackageStep x) { return x.IsPack; });
            if (!s.IsPack || first == null) return false;
            Package.AssignPackNo(steps, s);
            s.SameTimes(first);
            return true;
        }

        // Packen fuer einen neuen Back-Up: das gewaehlte, sonst das erste ohne Back-Up; null = keines frei
        PackageStep FreePacken()
        {
            if (cur != null && cur.IsPack && !Package.HasBackup(steps, cur)) return cur;
            return steps.Find(delegate(PackageStep x) { return x.IsPack && !Package.HasBackup(steps, x); });
        }

        // Eingabe im Schritt: leer = wie Kopf, gueltig = Ueberschreibung, sonst (halb getippt) keine Nummer
        string StepPackNo(string text)
        {
            string t = text.Trim();
            return t.Length == 0 ? PrevOf(cur) : PackageStep.IsManualNo(t) ? t : "";
        }

        string ManualHint(PackageStep s)
        {
            string prev = PrevOf(s);
            if (prev.Length == 0) return "";
            if (s.ManualNo.Length == 0 || s.ManualNo == prev) return "leer oder gleich = wie Nr. für Packen im Kopf (" + prev + ")";
            return "weicht vom Kopf ab (Nr. für Packen: " + prev + ")";
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
