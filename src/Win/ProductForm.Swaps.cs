using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Reiter "Austauschgruppen" von "Produkte & Equipment": Geraete eines Produkts, die sich gegenseitig vertreten (Ausweich-Vorschlag zuerst).
    // Mitglieder per Haekchen (Reihenfolge der Ids = Listenreihenfolge), Zaehler, Leerzustand, Rueckmeldung ueber die Statuszeile.
    partial class ProductForm
    {
        readonly ListBox lbSwap = new ListBox();
        readonly CheckedListBox swMembers = new CheckedListBox();
        readonly TextBox swName = new TextBox();
        readonly Label swCount = new Label();
        readonly Panel swMain = new Panel(), swEmpty = new Panel();
        readonly ToolTip swTip = new ToolTip();
        Button bSwDel;
        bool nameTouched;
        string lastTip = "";

        class SwapItem
        {
            public SwapGroup G;
            public override string ToString() { return (G.Name.Length > 0 ? G.Name : "(ohne Name)") + " (" + (G.Ids.Count == 1 ? "1 Gerät" : G.Ids.Count + " Geräte") + ")"; }
        }

        SwapGroup CurSwap { get { SwapItem s = lbSwap.SelectedItem as SwapItem; return s != null ? s.G : null; } }

        void BuildSwaps()
        {
            tabSwap.Controls.Add(swMain); tabSwap.Controls.Add(swEmpty);
            swMain.SetBounds(0, 0, 662, 554); swEmpty.SetBounds(0, 0, 662, 554);
            Label empty = new Label(); empty.Text = "Noch keine Austauschgruppe. Eine Gruppe fasst Geräte zusammen, die sich gegenseitig vertreten (Ausweich-Vorschlag).";
            empty.SetBounds(20, 24, 600, 40); empty.ForeColor = SystemColors.GrayText; empty.UseMnemonic = false;
            swEmpty.Controls.Add(empty); swEmpty.Controls.Add(Btn("Erste Gruppe anlegen", 20, 72, 170, delegate { NewSwap(); }));

            swMain.Controls.Add(L("Austauschgruppen:", 8, 8));
            lbSwap.SetBounds(8, 28, 232, 456); lbSwap.IntegralHeight = false; lbSwap.DrawMode = DrawMode.OwnerDrawFixed; lbSwap.ItemHeight = Font.Height + 3; swMain.Controls.Add(lbSwap);
            lbSwap.DrawItem += DrawSwap;
            lbSwap.SelectedIndexChanged += delegate { if (!loading) LoadSwap(); };
            lbSwap.MouseMove += delegate(object s, MouseEventArgs e)
            {
                int i = lbSwap.IndexFromPoint(e.Location);
                string t = i >= 0 && ((SwapItem)lbSwap.Items[i]).G.Ids.Count < 2 ? "mindestens 2 Geräte anhaken" : "";
                if (t != lastTip) { lastTip = t; swTip.SetToolTip(lbSwap, t); }
            };
            swMain.Controls.Add(Btn("Neue Gruppe", 8, 490, 112, delegate { NewSwap(); }));
            bSwDel = Btn("Entfernen", 128, 490, 112, delegate { RemoveSwap(); }); swMain.Controls.Add(bSwDel);

            Label intro = new Label(); intro.Text = "Geräte einer Gruppe vertreten sich gegenseitig; beim Ausweichen wird zuerst ein Gerät der Gruppe vorgeschlagen.";
            intro.SetBounds(250, 8, 404, 34); intro.ForeColor = SystemColors.GrayText; intro.UseMnemonic = false; swMain.Controls.Add(intro);
            Label nl = new Label(); nl.Text = "Name:"; nl.TextAlign = ContentAlignment.MiddleLeft; nl.UseMnemonic = false; nl.SetBounds(250, 48, 46, 22); swMain.Controls.Add(nl);
            swName.SetBounds(300, 48, 354, 22); Ui.SimpleMenu(swName); swMain.Controls.Add(swName);
            swMain.Controls.Add(L("Geräte der Gruppe (anhaken):", 250, 82));
            swMembers.SetBounds(250, 102, 404, 346); swMembers.CheckOnClick = true; swMembers.IntegralHeight = false; swMain.Controls.Add(swMembers);
            swCount.SetBounds(250, 454, 404, 20); swCount.UseMnemonic = false; swMain.Controls.Add(swCount);
            Label h = new Label(); h.Text = "Zur Auswahl stehen die für dieses Produkt erlaubten Geräte aller Positionen.";
            h.SetBounds(250, 480, 404, 34); h.ForeColor = SystemColors.GrayText; h.UseMnemonic = false; swMain.Controls.Add(h);

            swName.TextChanged += delegate
            {
                SwapGroup g = CurSwap; if (loading || g == null) return;
                g.Name = swName.Text.Trim(); RefreshSwap();
                if (!nameTouched) { nameTouched = true; Touch("Name der Austauschgruppe geändert."); }
            };
            swMembers.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                SwapGroup g = CurSwap; if (loading || g == null) return;
                g.Ids.Clear();
                for (int i = 0; i < swMembers.Items.Count; i++) if (i == e.Index ? e.NewValue == CheckState.Checked : swMembers.GetItemChecked(i)) g.Ids.Add((string)swMembers.Items[i]);
                RefreshSwap();
                Touch("Austauschgruppe „" + g.Name + "“: " + (g.Ids.Count == 1 ? "1 Gerät" : g.Ids.Count + " Geräte") + " angehakt.");
            };
        }

        void DrawSwap(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                SwapItem s = (SwapItem)lbSwap.Items[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                Color c = sel ? SystemColors.HighlightText : s.G.Ids.Count < 2 ? Orange : SystemColors.WindowText;
                TextRenderer.DrawText(e.Graphics, s.ToString(), e.Font, e.Bounds, c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            e.DrawFocusRectangle();
        }

        // Listeneintrag, Zaehler und Reitertitel nach jeder Aenderung
        void RefreshSwap()
        {
            bool o = loading; loading = true;
            int i = lbSwap.SelectedIndex;
            if (i >= 0) { lbSwap.Items[i] = lbSwap.Items[i]; lbSwap.SelectedIndex = i; }
            loading = o;
            SwapGroup g = CurSwap; int n = g != null ? g.Ids.Count : 0;
            swCount.Text = g == null ? "" : n >= 2 ? n + " Geräte angehakt" : n == 1 ? "1 Gerät angehakt – mindestens 2 Geräte anhaken" : "mindestens 2 Geräte anhaken";
            swCount.ForeColor = g != null && n < 2 ? Orange : SystemColors.ControlText;
            tabSwap.Text = "Austauschgruppen (" + (prod != null ? prod.Swaps.Count : 0) + ")";
        }

        void FillSwaps(SwapGroup select)
        {
            loading = true;
            lbSwap.Items.Clear();
            if (prod != null) foreach (SwapGroup g in prod.Swaps) { SwapItem s = new SwapItem(); s.G = g; lbSwap.Items.Add(s); if (g == select) lbSwap.SelectedItem = s; }
            if (lbSwap.SelectedIndex < 0 && lbSwap.Items.Count > 0) lbSwap.SelectedIndex = 0;
            loading = false;
            bool none = lbSwap.Items.Count == 0;
            swEmpty.Visible = none; swMain.Visible = !none;
            if (none) swEmpty.BringToFront(); else swMain.BringToFront();
            LoadSwap();
        }

        void LoadSwap()
        {
            SwapGroup g = CurSwap;
            loading = true; nameTouched = false;
            swName.Text = g != null ? g.Name : ""; swMembers.Items.Clear();
            swName.Enabled = swMembers.Enabled = bSwDel.Enabled = g != null;
            if (g != null)
            {
                List<string> all = new List<string>();
                foreach (EquipPosition p in cat.Positions) foreach (string id in cat.Allowed(prod.Code, p.Short)) if (!all.Contains(id)) all.Add(id);
                foreach (string id in g.Ids) if (!all.Contains(id)) all.Add(id);
                foreach (string id in all) swMembers.Items.Add(id, g.Ids.Contains(id));
            }
            loading = false;
            RefreshSwap();
        }

        void NewSwap()
        {
            if (prod == null) return;
            SwapGroup g = new SwapGroup(); g.Name = "Neue Austauschgruppe"; prod.Swaps.Add(g);
            FillSwaps(g);
            tabs.SelectedTab = tabSwap; swName.Focus(); swName.SelectAll();
            Touch("Neue Austauschgruppe angelegt – Name eintragen und Geräte anhaken.");
        }

        void RemoveSwap()
        {
            SwapGroup g = CurSwap; if (g == null) return;
            prod.Swaps.Remove(g); FillSwaps(null);
            Touch("Austauschgruppe „" + g.Name + "“ entfernt.");
        }

#if TESTHOOKS
        public string TestSwapCount { get { return swCount.Text; } }
        public string TestSwapTab { get { return tabSwap.Text; } }
        public string TestSwapList { get { List<string> r = new List<string>(); foreach (object o in lbSwap.Items) r.Add(o.ToString()); return string.Join("|", r.ToArray()); } }
        public string TestSwapIds { get { SwapGroup g = CurSwap; return g != null ? string.Join(",", g.Ids.ToArray()) : ""; } }
        public bool TestSwapEmptyShown { get { return swEmpty.Visible; } }
        public void TestShowSwaps() { tabs.SelectedTab = tabSwap; }
#endif
    }
}
