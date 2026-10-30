using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Auswahl der Puffer eines Produkts (Pufferpaket aus dem Produktprofil oder Standardpuffer); ersetzt die Puffer des Schritts.
    class PufferPickForm : Form
    {
        public List<PufferTemplate> Result = new List<PufferTemplate>();
        readonly List<PufferTemplate> list;
        readonly CheckedListBox lb = new CheckedListBox();

        // Eine Zeile je Vorlage: "Name · Typ · Position: Gerät, …" (Geräte nur, wenn vorbelegt)
        public static string Describe(PufferTemplate t)
        {
            List<string> p = new List<string>();
            foreach (PufferTemplatePos x in t.Positions) p.Add(x.Ids.Count > 0 ? x.Short + " " + string.Join("/", x.Ids.ToArray()) : x.Short);
            return (t.Name.Length > 0 ? t.Name : "(Name aus Titel)") + " · " + (t.Typ == "stationaer" ? "stationär" : t.Typ) + (p.Count > 0 ? " · " + string.Join(", ", p.ToArray()) : "");
        }

        public PufferPickForm(string product, bool own, List<PufferTemplate> list, int existing)
        {
            this.list = list;
            Text = "Puffer für Produkt" + (product.Length > 0 ? " " + product : "");
            Ui.Dialog(this, 520, 330);
            Label l = new Label(); l.SetBounds(10, 8, 490, 64); l.UseMnemonic = false;
            l.Text = (own ? "Für das Produkt ist ein Pufferpaket hinterlegt." : "Für das Produkt ist kein Pufferpaket hinterlegt – es gilt der Standardpuffer (ohne Geräte).")
                + "\nDie angehakten Puffer ersetzen die bisherigen " + existing + " Puffer dieses Schritts; Geräte lassen sich danach in den Karten ändern.";
            lb.SetBounds(10, 78, 490, 180); lb.CheckOnClick = true; lb.IntegralHeight = false; lb.HorizontalScrollbar = true;
            foreach (PufferTemplate t in list) lb.Items.Add(Describe(t), true);
            Button ok = new Button(); ok.Text = "Übernehmen"; ok.SetBounds(300, 270, 100, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(410, 270, 90, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l, lb, ok, cancel });
        }

        void Commit()
        {
            Result = new List<PufferTemplate>();
            for (int i = 0; i < list.Count; i++) if (lb.GetItemChecked(i)) Result.Add(list[i].Clone());
            if (Result.Count == 0) { MessageBox.Show(this, "Bitte mindestens einen Puffer anhaken.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DialogResult = DialogResult.OK;
        }
    }
}
