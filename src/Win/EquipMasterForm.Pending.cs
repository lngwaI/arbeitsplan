using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Stammdaten (N5): Liste "Vorgemerkte Geräte" fuer den Admin; der Knopf ersetzt oben den Hinweis zur Baumstruktur, solange etwas vorgemerkt ist.
    partial class EquipMasterForm
    {
        readonly Button btnPending = new Button();
        Control hintTree;

        void BuildPending()
        {
            btnPending.SetBounds(412, 6, 270, 26); btnPending.Click += delegate { OpenPending(); }; Controls.Add(btnPending);
            RefreshPending();
        }

        void RefreshPending()
        {
            int n = m.Pending.Count;
            btnPending.Visible = n > 0; if (hintTree != null) hintTree.Visible = n == 0;
            btnPending.Text = n + (n == 1 ? " Gerät vorgemerkt – prüfen…" : " Geräte vorgemerkt – prüfen…");
            btnPending.ForeColor = Color.DarkOrange;
        }

        void OpenPending()
        {
            using (EquipPendingForm f = new EquipPendingForm(cat))
            {
                f.ShowDialog(this);
                if (f.Changes > 0) { search.Text = ""; RebuildTree(cur); RefreshUnassigned(); }
            }
            RefreshPending();
        }
    }
}
