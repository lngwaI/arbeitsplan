using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Kopfzeile oben (gross, fett): "Produkt [Name] · Code [AB1] · Erste Laufnummer [13570030]".
    // Der Produktcode ist frei (Package.IsProductCode); die 8-stellige erste Laufnummer ist davon unabhaengig: ihre ersten 4 Stellen
    // merkt sich das Paket als Praefix (Package.RunPrefix), die letzten 4 zaehlen die Folgelaeufe hoch (Ueberlauf > 9999 wird gemeldet).
    partial class PackageForm
    {
        readonly TextBox tName = new TextBox(), tProduct = new TextBox(), tFirst = new TextBox();
        readonly Label lRuns = new Label();
        string prefix = "";     // Laufnummern-Praefix XXYY aus "Erste Laufnummer" ("" = Laeufe ohne 8-stellige Nummer)

        string Product { get { return tProduct.Text.Trim(); } }

        Panel BuildHead()
        {
            Panel head = new Panel(); head.Dock = DockStyle.Top; head.Tag = "keep"; head.BackColor = Color.White;
            Font big = new Font(Font.FontFamily, Font.SizeInPoints + 2f, FontStyle.Bold);
            int h = big.Height + 10, y = 8, x = 10;
            head.Height = h + 16;
            foreach (object[] c in new[] {
                new object[] { "Produkt:", tName, 190 }, new object[] { "Code:", tProduct, 100 }, new object[] { "Erste Laufnummer:", tFirst, 120 } })
            {
                Label l = new Label(); l.Text = (string)c[0]; l.Font = big; l.AutoSize = false; l.UseMnemonic = false; l.TextAlign = ContentAlignment.MiddleLeft;
                l.SetBounds(x, y, TextRenderer.MeasureText(l.Text, big).Width + 4, h);
                TextBox t = (TextBox)c[1]; t.Font = big; t.SetBounds(l.Right + 2, y + (h - t.PreferredHeight) / 2, (int)c[2], t.PreferredHeight);
                head.Controls.Add(l); head.Controls.Add(t);
                x = t.Right + 14;
                Ui.SimpleMenu(t);
            }
            tProduct.MaxLength = 12; tFirst.MaxLength = 8;
            lRuns.SetBounds(x, y + (h - Font.Height - 4) / 2, 200, Font.Height + 4);   // einzeilig, sonst "…"; Breite bis zum rechten Rand
            head.Resize += delegate { lRuns.Width = Math.Max(40, head.ClientSize.Width - lRuns.Left - 10); };
            lRuns.TextAlign = ContentAlignment.MiddleLeft; lRuns.AutoEllipsis = true; lRuns.UseMnemonic = false;
            head.Controls.Add(lRuns);
            tip.SetToolTip(tProduct, "Produktcode, frei wählbar: 2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (z. B. AB1, AB12345678-). Leer = ohne Produkt.");
            tip.SetToolTip(tFirst, "Komplette 8-stellige Laufnummer des ersten Laufs (z. B. 13570030). Folgeläufe zählen die letzten 4 Stellen hoch. Beim Kampagnenstart anpassbar.");
            tName.TextChanged += delegate { Changed(); };
            tProduct.TextChanged += delegate   // anderes Produkt: Geraetelisten der Zeilen neu fuellen (Produktprofil)
            {
                if (!loading && cur != null && (Product.Length == 0 || Package.IsProductCode(Product))) { dirty = true; Bind(cur); } else Changed();
            };
            tFirst.TextChanged += delegate { FirstRunTyped(); };
            return head;
        }

        // Alle Schritte als Paket-Sicht (gleiche Schritt-Objekte, z. B. fuer Umnummerieren und Ueberlauf-Pruefung)
        Package StepsView() { Package p = new Package(); p.Steps = steps; p.RunPrefix = prefix; return p; }

        void FirstRunTyped()
        {
            if (loading) return;
            string p; int no;
            if (Package.ParseRun(tFirst.Text, out p, out no))
            {
                prefix = p;
                StepsView().Renumber(no);   // Lauf-Schritte zaehlen ab hier (Abstaende zwischen den Schritten bleiben)
                if (cur != null) { loading = true; nStartNo.Value = Math.Max(nStartNo.Minimum, Math.Min(nStartNo.Maximum, cur.StartNo)); loading = false; }
            }
            else if (tFirst.Text.Trim().Length == 0) prefix = "";
            Changed();
        }

        // Startnummer (ZZZZ) fuer einen neuen Lauf-Schritt: wie die vorhandenen Laeufe, sonst aus "Erste Laufnummer"
        int NewRunNo()
        {
            int f = StepsView().FirstRunNo;
            string p; int no;
            return f >= 0 ? f : (Package.ParseRun(tFirst.Text, out p, out no) ? no : 1);
        }

        // Pruefung der Kopfzeile; null = in Ordnung, sonst Meldung (fuer Hinweisliste, Speichern und Uebernehmen)
        string ProductError() { return Product.Length > 0 && !Package.IsProductCode(Product) ? "Produktcode ungültig: 2–12 Zeichen aus Buchstaben, Ziffern und - _ . / + (ohne Leerzeichen), z. B. AB1." : null; }

        string FirstRunError()
        {
            string p; int no;
            return tFirst.Text.Trim().Length > 0 && !Package.ParseRun(tFirst.Text, out p, out no) ? "Erste Laufnummer: bitte 8 Ziffern eingeben (z. B. 13570030)." : null;
        }

        void UpdateRunsLabel()
        {
            string err = ProductError() ?? FirstRunError() ?? StepsView().Overflow();
            bool runs = StepsView().FirstRunNo >= 0;
            if (err != null) lRuns.Text = err;
            else if (!runs) lRuns.Text = prefix.Length > 0 ? "noch kein Lauf im Ablauf" : "";
            else
            {
                Package v = StepsView();
                lRuns.Text = prefix.Length > 0 ? "→ " + v.FirstRun + ", " + Package.FormatRun(prefix, v.FirstRunNo + 1) + " …" : "ohne 8-stellige Nummer: Läufe 1, 2, 3 …";
            }
            lRuns.ForeColor = err != null ? Color.Firebrick : Section.HelpColor;
            tip.SetToolTip(lRuns, lRuns.Text);
        }

        // Klick auf einen Pruefhinweis: erst nach der Mausverarbeitung der Liste ausfuehren (Bind baut die Hinweisliste neu auf,
        // waehrend die ListBox ihren Klick noch abarbeitet); Felder unter "Mehr Optionen" klappen dabei auf.
        void HintClicked(Hint h)
        {
            if (h == null || IsDisposed || !IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate { GoToHint(h); });
        }

        void GoToHint(Hint h)
        {
            if (IsDisposed || h == null) return;
            if (h.Step != null && steps.Contains(h.Step) && h.Step != cur) Bind(h.Step);
            Control f = h.Field;
            if (f == null || f.IsDisposed) return;
            if (!more && (f == nStartNo || f == tIdent || f == tNote)) SetMore(true);
            if (f.CanFocus) f.Focus();
        }
    }
}
