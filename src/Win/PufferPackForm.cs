using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Im Pufferpaket-Dialog in die Equipment-Liste aufgenommenes Geraet (Stammdaten + Position). Der Dialog wendet es sofort auf den Katalog an,
    // der ihm uebergeben wurde (ProductForm: Arbeitskopie, wird mit "Speichern" gesichert); PufferPackAdmin.Merged setzt es auf den frisch
    // geladenen Katalog erneut ein, damit Aenderungen anderer Admins erhalten bleiben.
    class DevAdd
    {
        public EquipDevice D; public string Short = "";
        public void ApplyTo(Catalog c)
        {
            if (c.Master.Device(D.Id) == null) c.Master.Devices.Add(D.Clone());
            EquipPosition p = c.Position(Short);
            if (p != null && !p.Ids.Contains(D.Id)) p.Ids.Add(D.Id);
            c.Master.RemovePending(D.Id);   // eine Vormerkung ist damit erledigt
        }
    }

    // Pufferpaket eines Produkts (UX-6 B4; aus Einstellungen -> Pufferpakete oder Produkte & Equipment -> "Pufferpaket…", Recht PackageAdmin):
    // Liste von Vorlagen (Bezeichnung zur Auswahl, Name im Plan, Typ, Positionen mit vorbelegtem Geraet, "gemeinsam", Notiz). Der Paket-Editor
    // bietet sie unter "Pufferpaket uebernehmen" und "+ Puffer" an. Ohne Pufferpaket gilt der Standardpuffer (Equipment & Bausteine).
    // Zeilen (PufferPackForm.Rows.cs) lassen sich mit ▲▼ bzw. Alt+↑/↓ umsortieren; die Reihenfolge ist die im Plantext.
    partial class PufferPackForm : Form
    {
        readonly Catalog cat; readonly ProductProfile prod; readonly bool readOnly;   // readOnly: nur ansehen (ohne Recht PackageAdmin)
        public List<PufferTemplate> Result = new List<PufferTemplate>();
        public readonly List<DevAdd> Added = new List<DevAdd>();   // in diesem Dialog aufgenommene Geraete (siehe DevAdd)
        readonly List<PufferTemplate> list = new List<PufferTemplate>();
        PufferTemplate cur;
        bool loading;
        readonly ListBox lb = new ListBox();
        readonly TextBox tArt = new TextBox(), tName = new TextBox(), tPreview = new TextBox();
        readonly ComboBox cbTyp = new ComboBox();
        readonly Button bPos = new Button(), bTpl = new Button();
        readonly ToolTip tip = new ToolTip();
        readonly ContextMenuStrip posMenu = new ContextMenuStrip(), tplMenu = new ContextMenuStrip();

        public PufferPackForm(Catalog cat, ProductProfile prod, bool readOnly = false)
        {
            this.cat = cat; this.prod = prod; this.readOnly = readOnly;
            foreach (PufferTemplate t in prod.Puffer) list.Add(t.Clone());
            Text = "Pufferpaket für Produkt " + prod.Code;
            Ui.Dialog(this, 860, 560);
            Label info = L("Vorlagen, die der Paket-Editor unter „Pufferpaket übernehmen“ und „+ Puffer ▾“ anbietet. Ohne Vorlagen gilt der Standardpuffer.", 8, 6);
            Controls.Add(info);
            Controls.Add(L("Vorlagen:", 8, 30));
            lb.SetBounds(8, 48, 250, 230); lb.IntegralHeight = false; Controls.Add(lb);
            lb.SelectedIndexChanged += delegate { if (!loading) Choose(lb.SelectedIndex >= 0 ? list[lb.SelectedIndex] : null); };
            Controls.Add(Btn(bTpl, "+ Vorlage ▾", 8, 284, 120, delegate { tplMenu.Show(bTpl, new Point(0, bTpl.Height)); }));
            Controls.Add(Btn("Entfernen", 134, 284, 124, delegate { RemoveTemplate(); }));
            Controls.Add(Btn("▲", 8, 316, 58, delegate { MoveTemplate(-1); }));
            Controls.Add(Btn("▼", 70, 316, 58, delegate { MoveTemplate(1); }));

            Controls.Add(L("Bezeichnung (zur Auswahl):", 272, 30)); tArt.SetBounds(272, 48, 230, 22); Ui.SimpleMenu(tArt); Controls.Add(tArt);
            Controls.Add(L("Name im Plan:", 512, 30)); tName.SetBounds(512, 48, 220, 22); Ui.SimpleMenu(tName); Controls.Add(tName);
            Label help = L("leer = Titel des Schritts · im Plan: " + prod.Code + " - <Name>", 512, 72); help.ForeColor = SystemColors.GrayText; Controls.Add(help);
            Controls.Add(L("Typ:", 742, 30)); cbTyp.SetBounds(742, 48, 110, 22); cbTyp.DropDownStyle = ComboBoxStyle.DropDownList; cbTyp.Items.AddRange(new object[] { "mobil", "stationär" }); Controls.Add(cbTyp);
            tArt.TextChanged += delegate { if (!loading && cur != null) { cur.Art = tArt.Text.Trim(); RefreshItem(); } };
            tName.TextChanged += delegate { if (!loading && cur != null) { cur.Name = tName.Text.Replace('|', '/').Trim(); RefreshItem(); Preview(); } };
            cbTyp.SelectedIndexChanged += delegate { if (!loading && cur != null) { cur.Typ = cbTyp.SelectedIndex == 1 ? "stationaer" : "mobil"; RefreshItem(); Preview(); } };
            BuildRows();
            BuildMenus();
            Controls.Add(L("So erscheint der Puffer im Plan:", 8, 372));
            tPreview.SetBounds(8, 390, 844, 118); tPreview.Multiline = true; tPreview.ReadOnly = true; tPreview.ScrollBars = ScrollBars.Vertical; Controls.Add(tPreview);
            Button ok = Btn("Speichern", 656, 520, 96, delegate { Commit(); }); ok.Height = 30;
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(758, 520, 94, 30); cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel; Controls.AddRange(new Control[] { ok, cancel });
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
        static string M(string s) { return s.Replace("&", "&&"); }   // Menuetext ohne Kurztaste

        // Menues: "+ Vorlage ▾" (leer / Kopie / aus anderem Produkt) und "+ Position ▾" (Standardpuffer-Positionen, dazu alle)
        void BuildMenus()
        {
            tplMenu.Items.Add("Leer (Positionen des Standardpuffers)", null, delegate { AddTemplate(); });
            ToolStripItem copy = tplMenu.Items.Add("Kopie der markierten", null, delegate { CopyTemplate(); });
            ToolStripMenuItem from = new ToolStripMenuItem("Aus Produkt");
            foreach (ProductProfile p in cat.Products)
            {
                if (p == prod || p.Puffer.Count == 0) continue;
                ToolStripMenuItem pm = new ToolStripMenuItem(M(p.Code + (p.Name.Length > 0 ? " · " + p.Name : "")));
                foreach (PufferTemplate t in p.Puffer) { PufferTemplate src = t; pm.DropDownItems.Add(M(t.Label), null, delegate { AddFromProduct(src); }); }
                from.DropDownItems.Add(pm);
            }
            if (from.DropDownItems.Count == 0) { from.Text = "Aus Produkt (keine Vorlagen anderer Produkte)"; from.Enabled = false; }
            tplMenu.Items.Add(from);
            tplMenu.Opening += delegate { copy.Enabled = cur != null; };

            Baustein b = cat.Baustein_(Catalog.StandardPufferName);
            if (b != null) foreach (string sh in b.Positions) AddPosItem(posMenu.Items, sh);
            ToolStripMenuItem more = new ToolStripMenuItem("Weitere Positionen");
            foreach (EquipPosition ep in cat.Positions) AddPosItem(more.DropDownItems, ep.Short);
            if (posMenu.Items.Count > 0) posMenu.Items.Add(new ToolStripSeparator());
            posMenu.Items.Add(more);
        }

        void AddPosItem(ToolStripItemCollection to, string sh)
        {
            EquipPosition ep = cat.Position(sh); string s = sh;
            to.Add(M(ep != null ? ep.Name + "  (" + sh + ")" : sh), null, delegate { AddPos(s); });
        }

        string ItemText(PufferTemplate t)
        {
            int n = t.Positions.Count;
            return t.Label + " · " + (t.Typ == "stationaer" ? "stationär" : t.Typ) + " · " + n + (n == 1 ? " Position" : " Positionen");
        }

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
            tArt.Text = t != null ? t.Art : ""; tName.Text = t != null ? t.Name : ""; cbTyp.SelectedIndex = t != null && t.Typ == "stationaer" ? 1 : 0;
            tArt.Enabled = tName.Enabled = cbTyp.Enabled = bPos.Enabled = t != null && !readOnly;
            ClearRows();
            if (t != null) foreach (PufferTemplatePos p in t.Positions) AddRow(p);
            LayoutRows();
            loading = false;
            Preview(); UpdateStatus();
        }

        void AddTemplate()
        {
            PufferTemplate t = cat.StandardPuffer();
            if (t == null) { t = new PufferTemplate(); t.Positions.Add(new PufferTemplatePos()); t.Positions[0].Short = "Ansatztank"; }
            if (list.Count > 0) t.Typ = list[list.Count - 1].Typ;
            list.Add(t); Fill(t);
        }

        // Kopie der markierten Vorlage direkt dahinter; Bezeichnung bekommt "(Kopie)", damit beide in der Liste unterscheidbar sind
        void CopyTemplate()
        {
            if (cur == null) return;
            PufferTemplate t = cur.Clone();
            string l = cur.Art.Length > 0 ? cur.Art : cur.Name;
            t.Art = l.Length > 0 ? l + " (Kopie)" : "Kopie";
            list.Insert(list.IndexOf(cur) + 1, t); Fill(t);
        }

        // Vorlage eines anderen Produkts als unabhaengige Kopie hinten anhaengen
        void AddFromProduct(PufferTemplate src) { PufferTemplate t = src.Clone(); list.Add(t); Fill(t); }

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
            lb.Focus();
        }

        // Vorschau: die Vorlagen als Puffer-Schritt (Plan-Ausgabe des Kerns), Namen mit Produktcode wie im Plan (Package.PlanBufferName)
        void Preview()
        {
            PackageStep s = new PackageStep(); s.Title = "Puffer"; s.Source = "Puffer"; s.Row = "Puffer"; s.Day = 1;
            List<PufferTemplate> copy = new List<PufferTemplate>(); foreach (PufferTemplate t in list) copy.Add(t.Clone());
            if (copy.Count > 0) Package.ApplyPufferTemplates(s, copy);
            if (!s.IsBuffer) { tPreview.Text = "Kein Puffer – für das Produkt gilt der Standardpuffer."; return; }
            List<BufferItem> bl = s.BuffersFor(0, "");
            foreach (BufferItem b in bl) b.Name = Package.PlanBufferName(prod.Code, b.Name);
            tPreview.Text = string.Join("\r\n", BufferSet.DisplayLines(bl, true).ToArray()).Replace("(stationaer)", "(stationär)");
        }

        void Commit()
        {
            Result = new List<PufferTemplate>(list);
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        public List<PufferTemplate> TestList { get { return list; } }
        public string TestPreview { get { return tPreview.Text; } }
        public string TestItemText(int i) { return lb.Items[i].ToString(); }
        public List<string> TestTplMenu() { List<string> r = new List<string>(); foreach (ToolStripItem i in tplMenu.Items) r.Add(i.Text); return r; }
        public void TestAdd() { AddTemplate(); }
        public void TestCopy() { CopyTemplate(); }
        public void TestFromProduct(PufferTemplate src) { AddFromProduct(src); }
        public void TestSelect(int i) { Fill(list[i]); }
        public bool TestCommit() { Commit(); return Result.Count == list.Count; }
        public void TestName(string n) { tName.Text = n; }
        public void TestArt(string a) { tArt.Text = a; }
#endif
    }
}
