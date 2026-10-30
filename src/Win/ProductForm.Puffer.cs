using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Pufferpaket eines Produkts (Einstellungen -> Produkte & Equipment -> "Pufferpaket…", Recht PackageAdmin): Liste von Puffer-Vorlagen
    // (Name, Typ, Positionen mit vorbelegtem Gerät aus der nach Produkt gefilterten Liste, "gemeinsam" für mehrere Puffer). Der Paket-Editor
    // übernimmt sie mit "Puffer für Produkt…". Ohne Pufferpaket gilt der Standardpuffer (Einstellungen -> Equipment & Bausteine).
    partial class ProductForm
    {
        void EditPuffer()
        {
            if (prod == null) return;
            using (PufferPackForm f = new PufferPackForm(cat, prod))
                if (f.ShowDialog(this) == DialogResult.OK) { prod.Puffer = f.Result; SelectProduct(prod); }
        }
    }

    class PufferPackForm : Form
    {
        readonly Catalog cat; readonly ProductProfile prod; readonly bool readOnly;   // readOnly: nur ansehen (ohne Recht PackageAdmin)
        public List<PufferTemplate> Result = new List<PufferTemplate>();
        readonly List<PufferTemplate> list = new List<PufferTemplate>();
        PufferTemplate cur;
        bool loading;
        readonly ListBox lb = new ListBox();
        readonly TextBox tName = new TextBox(), tPreview = new TextBox();
        readonly ComboBox cbTyp = new ComboBox();
        readonly Panel rows = new Panel();
        readonly Button bPos = new Button();
        readonly ContextMenuStrip posMenu = new ContextMenuStrip();

        public PufferPackForm(Catalog cat, ProductProfile prod, bool readOnly = false)
        {
            this.cat = cat; this.prod = prod; this.readOnly = readOnly;
            foreach (PufferTemplate t in prod.Puffer) list.Add(t.Clone());
            Text = "Pufferpaket für Produkt " + prod.Code;
            Ui.Dialog(this, 860, 560);
            Label info = Ui.Hint("Puffer, die der Paket-Editor mit „Puffer für Produkt…“ vorbelegt. Ohne Pufferpaket gilt der Standardpuffer (Equipment & Bausteine).", 8, 6, 844);
            info.UseMnemonic = false; Controls.Add(info);
            Controls.Add(L("Puffer des Produkts:", 8, 30));
            lb.SetBounds(8, 48, 210, 250); lb.IntegralHeight = false; Controls.Add(lb);
            lb.SelectedIndexChanged += delegate { if (!loading) Choose(lb.SelectedIndex >= 0 ? list[lb.SelectedIndex] : null); };
            Controls.Add(Btn("+ Puffer", 8, 304, 100, delegate { AddTemplate(); }));
            Controls.Add(Btn("Entfernen", 114, 304, 104, delegate { RemoveTemplate(); }));
            Controls.Add(Btn("▲", 8, 336, 48, delegate { MoveTemplate(-1); }));
            Controls.Add(Btn("▼", 60, 336, 48, delegate { MoveTemplate(1); }));

            Controls.Add(L("Name (leer = Titel des Schritts):", 232, 30)); tName.SetBounds(232, 48, 300, 22); Ui.SimpleMenu(tName); Controls.Add(tName);
            Controls.Add(L("Typ:", 546, 30)); cbTyp.SetBounds(546, 48, 110, 22); cbTyp.DropDownStyle = ComboBoxStyle.DropDownList; cbTyp.Items.AddRange(new object[] { "mobil", "stationär" }); Controls.Add(cbTyp);
            tName.TextChanged += delegate { if (!loading && cur != null) { cur.Name = tName.Text.Replace('|', '/').Trim(); RefreshItem(); Preview(); } };
            cbTyp.SelectedIndexChanged += delegate { if (!loading && cur != null) { cur.Typ = cbTyp.SelectedIndex == 1 ? "stationaer" : "mobil"; RefreshItem(); Preview(); } };
            Controls.Add(L("Position  |  Gerät (aus der Liste des Produkts)  |  gemeinsam für mehrere Puffer  |  Notiz", 232, 78));
            rows.SetBounds(232, 98, 620, 200); rows.AutoScroll = true; rows.BorderStyle = BorderStyle.FixedSingle; Controls.Add(rows);
            Btn(bPos, "+ Position ▾", 232, 304, 120, delegate { posMenu.Show(bPos, new Point(0, bPos.Height)); }); Controls.Add(bPos);
            Controls.Add(L("So erscheint der Puffer im Plan:", 8, 372));
            tPreview.SetBounds(8, 390, 844, 118); tPreview.Multiline = true; tPreview.ReadOnly = true; tPreview.ScrollBars = ScrollBars.Vertical; Controls.Add(tPreview);
            Button ok = Btn("Speichern", 656, 520, 96, delegate { Commit(); }); ok.Height = 30;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(758, 520, 94, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel; Controls.AddRange(new Control[] { ok, cancel });
            List<string> shorts = new List<string>();
            Baustein b = cat.Baustein_(Catalog.StandardPufferName);
            if (b != null) shorts.AddRange(b.Positions);
            foreach (string sh in shorts) { string s = sh; EquipPosition ep = cat.Position(sh); posMenu.Items.Add(ep != null ? ep.Name + "  (" + sh + ")" : sh, null, delegate { AddPos(s); }); }
            if (readOnly)   // nur ansehen: Liste und Vorschau bleiben bedienbar, alles Aendernde entfaellt
            {
                Text += " (nur ansehen)";
                foreach (Control c in Controls) if (c is Button && c != cancel) c.Visible = false;
                cancel.Text = "Schließen"; ok.Visible = false;
            }
            Fill(list.Count > 0 ? list[0] : null);
        }

        static Label L(string text, int x, int y) { Label l = new Label(); l.Text = text; l.AutoSize = true; l.UseMnemonic = false; l.Location = new Point(x, y); return l; }
        static Button Btn(string text, int x, int y, int w, EventHandler click) { return Btn(new Button(), text, x, y, w, click); }
        static Button Btn(Button b, string text, int x, int y, int w, EventHandler click) { b.Text = text; b.SetBounds(x, y, w, 26); b.Click += click; return b; }

        string ItemText(PufferTemplate t) { return (t.Name.Length > 0 ? t.Name : "(Name aus Titel)") + " · " + (t.Typ == "stationaer" ? "stationär" : t.Typ); }

        void Fill(PufferTemplate select)
        {
            loading = true;
            lb.Items.Clear();
            foreach (PufferTemplate t in list) lb.Items.Add(ItemText(t));
            lb.SelectedIndex = select != null ? list.IndexOf(select) : -1;
            loading = false;
            Choose(select);
        }

        void RefreshItem() { int i = lb.SelectedIndex; if (i >= 0) { loading = true; lb.Items[i] = ItemText(list[i]); loading = false; } }

        void Choose(PufferTemplate t)
        {
            cur = t; loading = true;
            tName.Text = t != null ? t.Name : ""; cbTyp.SelectedIndex = t != null && t.Typ == "stationaer" ? 1 : 0;
            tName.Enabled = cbTyp.Enabled = bPos.Enabled = t != null && !readOnly;
            while (rows.Controls.Count > 0) { Control c = rows.Controls[0]; rows.Controls.RemoveAt(0); c.Dispose(); }
            if (t != null) { int y = 4; foreach (PufferTemplatePos p in t.Positions) y = AddRow(p, y); }
            loading = false;
            Preview();
        }

        int AddRow(PufferTemplatePos p, int y)
        {
            EquipPosition ep = cat.Position(p.Short);
            Label n = new Label(); n.Text = ep != null ? ep.Name : p.Short; n.AutoEllipsis = true; n.TextAlign = ContentAlignment.MiddleLeft; n.SetBounds(4, y, 130, 22);
            ComboBox d = new ComboBox(); d.DropDownStyle = ComboBoxStyle.DropDown; d.SetBounds(140, y, 170, 22);
            d.Items.Add(""); foreach (string id in cat.Allowed(prod.Code, p.Short)) d.Items.Add(id);
            if (p.Ids.Count > 1) { d.Text = string.Join(" / ", p.Ids.ToArray()); d.Enabled = false; }   // Wechsel-Geräte bleiben unverändert
            else d.Text = p.Ids.Count == 1 ? p.Ids[0] : "";
            CheckBox s = new CheckBox(); s.Text = "gemeinsam"; s.Checked = p.Shared; s.SetBounds(318, y, 92, 22);
            TextBox no = new TextBox(); no.Text = p.Note; no.SetBounds(416, y, 150, 22); Ui.SimpleMenu(no);
            Button x = Btn("x", 572, y, 26, null);
            PufferTemplatePos pp = p;
            d.TextChanged += delegate { if (loading || pp.Ids.Count > 1) return; pp.Ids.Clear(); if (d.Text.Trim().Length > 0) pp.Ids.Add(d.Text.Trim()); Preview(); };
            s.CheckedChanged += delegate { if (!loading) { pp.Shared = s.Checked; Preview(); } };
            no.TextChanged += delegate { if (!loading) { pp.Note = no.Text.Trim(); Preview(); } };
            x.Click += delegate { cur.Positions.Remove(pp); Choose(cur); };
            if (readOnly) { d.Enabled = s.Enabled = no.Enabled = false; x.Visible = false; }
            rows.Controls.AddRange(new Control[] { n, d, s, no, x });
            return y + 26;
        }

        void AddPos(string shortName) { if (cur == null) return; PufferTemplatePos p = new PufferTemplatePos(); p.Short = shortName; cur.Positions.Add(p); Choose(cur); }

        void AddTemplate()
        {
            PufferTemplate t = cat.StandardPuffer();
            if (t == null) { t = new PufferTemplate(); t.Positions.Add(new PufferTemplatePos()); t.Positions[0].Short = "Ansatztank"; }
            if (list.Count > 0) t.Typ = list[list.Count - 1].Typ;
            list.Add(t); Fill(t);
        }

        void RemoveTemplate()
        {
            if (cur == null) return;
            int i = list.IndexOf(cur); list.Remove(cur);
            Fill(list.Count > 0 ? list[Math.Min(i, list.Count - 1)] : null);
        }

        void MoveTemplate(int d)
        {
            if (cur == null) return;
            int i = list.IndexOf(cur), j = i + d;
            if (j < 0 || j >= list.Count) return;
            list.RemoveAt(i); list.Insert(j, cur); Fill(cur);
        }

        // Vorschau: die Puffer-Vorlagen als Puffer-Schritt (Plan-Ausgabe des Kerns)
        void Preview()
        {
            PackageStep s = new PackageStep(); s.Title = "Puffer"; s.Source = "Puffer"; s.Row = "Puffer"; s.Day = 1;
            List<PufferTemplate> copy = new List<PufferTemplate>(); foreach (PufferTemplate t in list) copy.Add(t.Clone());
            if (copy.Count > 0) Package.ApplyPufferTemplates(s, copy);
            tPreview.Text = s.IsBuffer ? string.Join("\r\n", BufferSet.DisplayLines(s.BuffersFor(0, ""), true).ToArray()).Replace("(stationaer)", "(stationär)") : "Kein Puffer – für das Produkt gilt der Standardpuffer.";
        }

        void Commit()
        {
            Result = new List<PufferTemplate>(list);
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        public List<PufferTemplate> TestList { get { return list; } }
        public string TestPreview { get { return tPreview.Text; } }
        public void TestAdd() { AddTemplate(); }
        public void TestSelect(int i) { Fill(list[i]); }
        public void TestSetDevice(int row, string id) { ((ComboBox)rows.Controls[row * 5 + 1]).Text = id; }
        public void TestShared(int row, bool on) { ((CheckBox)rows.Controls[row * 5 + 2]).Checked = on; }
        public int TestRows { get { return rows.Controls.Count / 5; } }
        public bool TestCommit() { Commit(); return Result.Count == list.Count; }
        public void TestName(string n) { tName.Text = n; }
#endif
    }
}
