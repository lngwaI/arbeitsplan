using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // "+ Schritt hinzufuegen": 1. Art waehlen (haeufige zuerst: Prozess-Lauf, Puffer, Freeze / Thaw, CIP / SIP, Einwaage, dann alle
    // weiteren Bausteine, Sonderereignisse, "Sonstiges"), 2. "Wo im Plan?" – Raum (Block) aus der Struktur, vom Baustein vorbelegt.
    // Ohne Raum geht es nicht weiter; "Sonstiges" fragt zusaetzlich einen Titel.
    class StepPicker : Form
    {
        static readonly string[] Common = { "Lauf", "Puffer", "Freeze / Thaw", "CIP / SIP", "Einwaage" };
        readonly ListBox list = new ListBox();
        readonly ComboBox cbRoom = new ComboBox();
        readonly TextBox tTitle = new TextBox();
        readonly Label lTitle = new Label(), lRoomHint = new Label();
        readonly Button ok = new Button();
        readonly List<Baustein> items = new List<Baustein>();   // null = Sonstiges
        readonly List<string> names = new List<string>(), descs = new List<string>();
        public Baustein Result;
        public string Room = "", TitleText = "";

        public StepPicker(Catalog cat, List<PackageStep> steps, List<string> rows)
        {
            Text = "Schritt hinzufügen";
            Ui.Dialog(this, 520, 520, false);
            Font bold = new Font(Font, FontStyle.Bold);
            Label l1 = new Label(); l1.Text = "1.  Welche Art von Schritt?"; l1.Font = bold; l1.SetBounds(12, 10, 496, 20);
            list.SetBounds(12, 34, 496, 314); list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = Font.Height * 2 + 10; list.IntegralHeight = false;
            List<Baustein> rest = new List<Baustein>(cat.Bausteine);
            foreach (string n in Common) { Baustein b = cat.Baustein_(n); if (b != null) { Add(b, steps, n == "Lauf" ? "Prozess-Lauf" : n); rest.Remove(b); } }
            foreach (Baustein b in rest) if (!b.Special) Add(b, steps, b.Name);
            foreach (Baustein b in rest) if (b.Special) Add(b, steps, b.Name);
            items.Add(null); names.Add("Sonstiges"); descs.Add("Ohne Vorlage: Titel, Raum und Equipment selbst festlegen."); list.Items.Add("Sonstiges");
            list.DrawItem += DrawItem;
            list.DoubleClick += delegate { if (ok.Enabled) Commit(); };

            Label l2 = new Label(); l2.Text = "2.  Wo im Plan?  (Raum / Block)"; l2.Font = bold; l2.SetBounds(12, 360, 300, 20);
            cbRoom.DropDownStyle = ComboBoxStyle.DropDownList; cbRoom.SetBounds(12, 382, 240, 24); cbRoom.Items.AddRange(rows.ToArray());
            lRoomHint.SetBounds(260, 385, 248, 20); lRoomHint.ForeColor = Section.HelpColor; lRoomHint.AutoEllipsis = true;
            lTitle.Text = "Titel:"; lTitle.SetBounds(12, 418, 60, 20);
            tTitle.SetBounds(76, 415, 432, 24); Ui.SimpleMenu(tTitle);
            ok.Text = "Hinzufügen"; ok.SetBounds(308, 480, 100, 30); ok.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(414, 480, 94, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { l1, list, l2, cbRoom, lRoomHint, lTitle, tTitle, ok, cancel });
            list.SelectedIndexChanged += delegate { Preset(); };
            cbRoom.SelectedIndexChanged += delegate { Check(); };
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            Theme.Current.Style(this);
        }

        void Add(Baustein b, List<PackageStep> steps, string name)
        {
            List<string> d = new List<string>();
            if (b.Name == "Lauf") d.Add("Lauf mit Laufnummer"); else if (b.Name == "Splitten") d.Add("Unterläufe");
            d.Add(b.Row.Length > 0 ? "Raum: " + b.Row : "Raum frei wählbar");
            d.Add(b.Positions.Count > 0 ? "Equipment: " + string.Join(", ", b.Positions.ToArray()) : "ohne Equipment");
            if (b.Unique) d.Add("nur einmal je Kampagne");
            if (b.Special) d.Add("Sonderereignis");
            if (steps.Exists(delegate(PackageStep s) { return s.Source == b.Name; })) d.Add("schon im Ablauf");
            items.Add(b); names.Add(name); descs.Add(string.Join(" · ", d.ToArray())); list.Items.Add(name);
        }

        // Raum des Bausteins vorbelegen (falls es ihn im Plan gibt); "Sonstiges": Raum und Titel selbst waehlen
        void Preset()
        {
            int i = list.SelectedIndex;
            if (i < 0) return;
            Baustein b = items[i];
            int r = b != null ? cbRoom.Items.IndexOf(b.Row) : -1;
            cbRoom.SelectedIndex = r;
            lRoomHint.Text = b == null ? "bitte wählen" : (b.Row.Length == 0 ? "Baustein ohne festen Raum – bitte wählen" : (r < 0 ? "„" + b.Row + "“ gibt es im Plan nicht – bitte wählen" : "vorbelegt, änderbar"));
            lTitle.Visible = tTitle.Visible = b == null;
            Check();
        }

        void Check() { ok.Enabled = list.SelectedIndex >= 0 && cbRoom.SelectedIndex >= 0; }

        void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            e.DrawBackground();
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color main = sel ? SystemColors.HighlightText : SystemColors.WindowText, sub = sel ? SystemColors.HighlightText : Section.HelpColor;
            Rectangle r = e.Bounds;
            TextFormatFlags ff = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            using (Font b = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, names[e.Index], b, new Rectangle(r.X + 6, r.Y + 4, r.Width - 10, Font.Height + 1), main, ff);
            TextRenderer.DrawText(e.Graphics, descs[e.Index], Font, new Rectangle(r.X + 6, r.Y + 5 + Font.Height, r.Width - 10, Font.Height + 1), sub, ff);
            e.DrawFocusRectangle();
        }

        void Commit()
        {
            int i = list.SelectedIndex;
            if (i < 0) return;
            if (cbRoom.SelectedIndex < 0) { cbRoom.Focus(); cbRoom.DroppedDown = true; return; }
            Result = items[i]; Room = cbRoom.Text; TitleText = Result == null ? tTitle.Text.Trim() : "";
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        // Selbsttest: Eintrag mit diesem Anzeigenamen waehlen; liefert den vorbelegten Raum ("" = keiner)
        public string TestSelect(string name) { list.SelectedIndex = names.IndexOf(name); return cbRoom.SelectedIndex >= 0 ? cbRoom.Text : ""; }
        public bool TestOk { get { return ok.Enabled; } }
        public List<string> TestNames { get { return names; } }
#endif
    }
}
