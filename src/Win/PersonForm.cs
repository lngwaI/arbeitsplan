using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Meine Schichten: 4 Wochen ab aktueller Woche fuer ein Kuerzel (nur lesend), mit Warnungen.
    class PersonForm : Form
    {
        const int Days = 28;
        readonly IDataStore store;
        readonly DateTime from;
        readonly TextBox tbKz = new TextBox();
        readonly ListView list = new ListView(), warn = new ListView();
        readonly Label info = new Label();

        public PersonForm(IDataStore store, string kuerzel, DateTime from)
        {
            this.store = store; this.from = from;
            Text = "Meine Schichten";
            Ui.Dialog(this, 520, 480);

            Label l = new Label(); l.Text = "Kürzel:"; l.SetBounds(10, 14, 50, 20);
            tbKz.SetBounds(64, 10, 100, 24); tbKz.Text = kuerzel ?? "";
            tbKz.TextChanged += delegate { Fill(); };
            Ui.SimpleMenu(tbKz);
            info.SetBounds(180, 14, 330, 20);

            list.SetBounds(10, 42, 500, 270);
            list.View = View.Details; list.FullRowSelect = true; list.GridLines = true;
            list.Columns.Add("Datum", 150); list.Columns.Add("Schicht", 70); list.Columns.Add("Block", 255);
            warn.SetBounds(10, 346, 500, 124);
            warn.View = View.Details; warn.HeaderStyle = ColumnHeaderStyle.None; warn.ForeColor = Color.FromArgb(190, 20, 20);
            warn.Columns.Add("", 480);
            Label wl = new Label(); wl.Text = "Warnungen"; wl.SetBounds(10, 322, 300, 20);

            Controls.AddRange(new Control[] { l, tbKz, info, list, wl, warn });
            Fill();
        }

        void Fill()
        {
            list.Items.Clear(); warn.Items.Clear();
            string k = tbKz.Text.Trim();
            if (k.Length == 0) { info.Text = "Bitte Kürzel eingeben."; return; }
            List<PersonShift> sh = PersonPlan.Collect(store, store.GetRows(), k, from, Days);
            foreach (PersonShift p in sh)
                list.Items.Add(new ListViewItem(new string[] { Ui.DayText(p.Day), PersonPlan.ShiftNames[p.Shift], p.Block }));
            List<string> w = PersonPlan.Warnings(sh);
            foreach (string s in w) warn.Items.Add(s);
            if (w.Count == 0) { ListViewItem ok = warn.Items.Add("Keine Warnungen."); ok.ForeColor = SystemColors.WindowText; }
            info.Text = sh.Count + " Schichten, " + w.Count + " Warnung(en), " + from.ToString("dd.MM.", Ui.De) + " - " + from.AddDays(Days - 1).ToString("dd.MM.yyyy", Ui.De);
        }
    }
}
