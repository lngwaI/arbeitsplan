using System;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Rechtsklick auf eine erkannte Equipment-Nummer im Plan: "Status setzen" (Recht SetStatus). Der Text bleibt unveraendert,
    // nur die Nummer wird in der Statusfarbe gezeichnet (Status nie als Text). Uebergang bis zur MES-Schnittstelle.
    partial class MainForm
    {
        ToolStripMenuItem miStatus;
        string statusNumber = "";

        void InitStatusMenu(ContextMenuStrip vm)
        {
            miStatus = new ToolStripMenuItem("Status setzen");
            vm.Items.Add(miStatus);
            vm.Opening += delegate
            {
                statusNumber = view.EquipAt(view.PointToClient(Control.MousePosition));
                miStatus.Visible = statusNumber.Length > 0 && me.Has(Perm.SetStatus) && !systemPc;
                if (miStatus.Visible) FillStatusMenu();
            };
        }

        void FillStatusMenu()
        {
            miStatus.Text = "Status setzen (" + statusNumber + ")";
            miStatus.DropDownItems.Clear();
            EquipmentInfo cur = equip.IsManual(statusNumber) ? equip.Get(statusNumber) : null;
            foreach (EquipStatus st in Enum.GetValues(typeof(EquipStatus)))
            {
                EquipStatus s = st;
                ToolStripMenuItem it = new ToolStripMenuItem(ManualStatusProvider.Label(s), Swatch(view.Colors.Get(s)));
                it.Checked = cur != null && cur.Status == s;
                it.Click += delegate { SetEquipStatus(statusNumber, s); };
                miStatus.DropDownItems.Add(it);
            }
            miStatus.DropDownItems.Add(new ToolStripSeparator());
            string num = statusNumber;
            ToolStripMenuItem clr = new ToolStripMenuItem("Manuellen Status entfernen");
            clr.Enabled = cur != null;
            clr.Click += delegate { SetEquipStatus(num, null); };
            miStatus.DropDownItems.Add(clr);
        }

        static Bitmap Swatch(int rgb)
        {
            Bitmap b = new Bitmap(14, 14);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                using (SolidBrush br = new SolidBrush(Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255))) g.FillRectangle(br, 1, 1, 12, 12);
                g.DrawRectangle(Pens.Gray, 0, 0, 13, 13);
            }
            return b;
        }

        // st == null: manuellen Status entfernen
        bool SetEquipStatus(string number, EquipStatus? st)
        {
            if (!me.Has(Perm.SetStatus) || systemPc || number.Length == 0) return false;
            try
            {
                if (!equip.Set(number, st, me)) { MessageBox.Show(this, "Der Status konnte nicht gespeichert werden.", "Equipment-Status", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Der Status konnte nicht gespeichert werden:\n" + ex.Message, "Equipment-Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            view.MarkAllDirty(); view.Relayout();
            return true;
        }

#if TESTHOOKS
        public bool TestSetStatus(string number, EquipStatus? st) { return SetEquipStatus(number, st); }
        public string TestEquipAt(Point p) { return view.EquipAt(p); }
        // "Nummer=Status,Nummer=" (leer = manuellen Status entfernen)
        public void TestStatusArg(string arg)
        {
            foreach (string part in arg.Split(','))
            {
                int eq = part.IndexOf('='); if (eq < 1) continue;
                string v = part.Substring(eq + 1).Trim();
                SetEquipStatus(part.Substring(0, eq).Trim(), v.Length == 0 ? (EquipStatus?)null : (EquipStatus)Enum.Parse(typeof(EquipStatus), v, true));
            }
        }
        public string TestEquipAtFirst(out string expected) { return view.TestEquipAtFirst(out expected); }
        public bool TestStatusMenuVisible(string number) { statusNumber = number; return number.Length > 0 && me.Has(Perm.SetStatus) && !systemPc; }
#endif
    }
}
