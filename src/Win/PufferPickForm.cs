using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // UX-6 B1 "Pufferpaket übernehmen": Quelle wählen (Pufferpaket eines Produkts oder Standardpuffer), Puffer anhaken, bisherige ersetzen oder hinten
    // anhängen. Ohne Vorlagen der Quelle: Leerzustand mit "Pufferpaket für X anlegen…" (Ergebnis Retry, CreateFor = Produktcode; der Editor ruft
    // PufferPackAdmin und öffnet den Dialog erneut). Ein im Paket nicht gesetzter Produktcode wird nie geändert.
    class PufferPickForm : Form
    {
        class Source
        {
            public string Code = "", Text = ""; public List<PufferTemplate> List = new List<PufferTemplate>();
            public override string ToString() { return Text; }
        }

        public List<PufferTemplate> Result = new List<PufferTemplate>();   // Klone der angehakten Vorlagen (höchstens PackageStep.MaxPuffer)
        public string ResultCode = "";   // Quelle: Produktcode, leer = Standardpuffer
        public bool Append;              // true = hinten anhängen, false = bisherige ersetzen
        public string CreateFor = "";    // bei Retry: Produkt, dessen Pufferpaket angelegt werden soll

        readonly Catalog cat; readonly int existing, free; readonly bool canAdmin;
        internal readonly ComboBox cbSrc = new ComboBox();
        internal readonly ListView lv = new ListView();
        internal readonly Label lblHint = new Label(), lblCount = new Label(), lblEmpty = new Label();
        internal readonly LinkLabel link = new LinkLabel();
        internal readonly RadioButton rbReplace = new RadioButton(), rbAppend = new RadioButton();
        internal readonly Button bOk = new Button(), bAll = new Button(), bNone = new Button();
        readonly ToolTip tip = new ToolTip();
        bool loading;
        Source linkTo;   // Ziel des Links im Leerzustand (Standardpuffer fehlt: Quelle mit Vorlagen); null = Pufferpaket anlegen
        const string WarnText = "Nicht in der Equipment-Liste";

        public static string TypText(string typ) { return typ == "stationaer" ? "stationär" : typ; }

        // "Ansatztank H1-T-0101, VT, WFI": Position, bei vorbelegten Geräten mit Nummer
        public static string Devices(PufferTemplate t)
        {
            List<string> p = new List<string>();
            foreach (PufferTemplatePos x in t.Positions) p.Add(x.Ids.Count > 0 ? x.Short + " " + string.Join("/", x.Ids.ToArray()) : x.Short);
            return string.Join(", ", p.ToArray());
        }

        static bool Unknown(Catalog cat, PufferTemplate t)
        {
            foreach (PufferTemplatePos x in t.Positions) foreach (string id in x.Ids) if (id.Length > 0 && cat.Master.Device(id) == null) return true;
            return false;
        }

        // packageProduct = Produktcode des Pakets (kann leer sein); pre = Vorauswahl der Quelle (null = automatisch)
        public PufferPickForm(Catalog cat, string packageProduct, int existing, bool canAdmin, bool append, string pre)
        {
            this.cat = cat; this.existing = existing; this.canAdmin = canAdmin; free = Math.Max(0, PackageStep.MaxPuffer - existing);
            Text = "Pufferpaket übernehmen";
            Ui.Dialog(this, 660, 478);
            Label intro = new Label(); intro.SetBounds(12, 10, 636, 38); intro.UseMnemonic = false;
            intro.Text = "Hake die Puffer an, die dieser Schritt bekommen soll. Geräte und Reihenfolge kannst du danach in den Karten ändern.";
            Label ls = new Label(); ls.Text = "Pufferpaket von:"; ls.SetBounds(12, 54, 112, 24); ls.TextAlign = ContentAlignment.MiddleLeft;
            cbSrc.DropDownStyle = ComboBoxStyle.DropDownList; cbSrc.SetBounds(128, 54, 520, 24);
            lblHint.SetBounds(128, 78, 520, 34); lblHint.ForeColor = Color.DarkOrange; lblHint.UseMnemonic = false;
            lblHint.Text = "Das Paket hat noch keinen Produktcode. Hier wählst du nur, woher die Puffer kommen; den Produktcode trägst du oben im Paket ein."; lblHint.Visible = packageProduct.Length == 0;
            lv.SetBounds(12, 114, 636, 192); lv.View = View.Details; lv.CheckBoxes = true; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.ShowItemToolTips = false;
            lv.Columns.Add("Bezeichnung", 190); lv.Columns.Add("Typ", 80); lv.Columns.Add("Positionen und Geräte", 340);
            lv.ItemChecked += delegate { if (!loading) Recount(); };
            lv.MouseMove += OnMove;
            lblEmpty.SetBounds(20, 122, 620, 38); lblEmpty.ForeColor = SystemColors.GrayText; lblEmpty.UseMnemonic = false;
            link.SetBounds(20, 164, 620, 20); link.UseMnemonic = false; link.LinkClicked += delegate { if (linkTo != null) cbSrc.SelectedItem = linkTo; else CreatePack(); };
            lblCount.SetBounds(12, 312, 400, 20); lblCount.UseMnemonic = false;
            bAll.Text = "Alle"; bAll.SetBounds(482, 308, 80, 26); bAll.Click += delegate { CheckAll(true); };
            bNone.Text = "Keine"; bNone.SetBounds(568, 308, 80, 26); bNone.Click += delegate { CheckAll(false); };
            rbReplace.Text = existing == 1 ? "Bisherigen Puffer ersetzen" : "Bisherige " + existing + " Puffer ersetzen"; rbReplace.SetBounds(12, 346, 636, 22); rbReplace.UseMnemonic = false;
            rbAppend.Text = free > 0 ? "Hinten anhängen (noch " + free + " frei)" : "Hinten anhängen (kein Platz frei, höchstens " + PackageStep.MaxPuffer + " Puffer)"; rbAppend.SetBounds(12, 370, 636, 22); rbAppend.UseMnemonic = false;
            rbAppend.Enabled = free > 0; rbAppend.Checked = append && free > 0; rbReplace.Checked = !rbAppend.Checked;
            rbReplace.CheckedChanged += delegate { if (!loading) Recount(); };
            bOk.SetBounds(400, 432, 160, 30); bOk.Click += delegate { Commit(); };
            Button cancel = new Button(); cancel.Text = "Abbrechen"; cancel.SetBounds(568, 432, 80, 30); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = bOk; CancelButton = cancel;
            Controls.AddRange(new Control[] { intro, ls, cbSrc, lblHint, lv, lblEmpty, link, lblCount, bAll, bNone, rbReplace, rbAppend, bOk, cancel });

            foreach (ProductProfile p in cat.Products)
            {
                Source s = new Source(); s.Code = p.Code; s.List = p.Puffer;
                s.Text = p.Code + (p.Name.Length > 0 ? " · " + p.Name : "") + (p.Puffer.Count > 0 ? " (" + p.Puffer.Count + " Puffer)" : " (kein Pufferpaket)");
                cbSrc.Items.Add(s);
            }
            if (Package.IsProductCode(packageProduct) && cat.Product(packageProduct) == null)   // Code des Pakets ohne Produktprofil: als Quelle anbieten (Leerzustand mit "anlegen…")
            {
                Source u = new Source(); u.Code = packageProduct; u.Text = packageProduct + " (kein Pufferpaket)"; cbSrc.Items.Add(u);
            }
            Source std = new Source(); std.Text = "Standardpuffer (ohne Geräte)"; PufferTemplate st = cat.StandardPuffer(); if (st != null) std.List.Add(st);
            cbSrc.Items.Add(std);
            cbSrc.SelectedIndex = cbSrc.Items.Count - 1;
            string want = pre;
            if (want == null)
            {
                int withPack = 0; string one = "";
                foreach (ProductProfile p in cat.Products) if (p.Puffer.Count > 0) { withPack++; one = p.Code; }
                want = packageProduct.Length > 0 ? packageProduct : withPack == 1 ? one : "";
            }
            for (int i = 0; i < cbSrc.Items.Count; i++) if (((Source)cbSrc.Items[i]).Code == want && want.Length > 0) cbSrc.SelectedIndex = i;
            cbSrc.SelectedIndexChanged += delegate { Fill(); };
            Fill();
        }

        Source Cur { get { return (Source)cbSrc.SelectedItem; } }

        void Fill()
        {
            loading = true; lv.Items.Clear(); Source s = Cur; linkTo = null;
            foreach (PufferTemplate t in s.List)
            {
                ListViewItem it = new ListViewItem(t.Label); it.Checked = true; it.UseItemStyleForSubItems = false; it.Tag = t;
                it.SubItems.Add(TypText(t.Typ));
                bool warn = Unknown(cat, t);
                ListViewItem.ListViewSubItem d = it.SubItems.Add((warn ? "⚠ " : "") + Devices(t));
                if (warn) d.ForeColor = Color.DarkOrange;
                lv.Items.Add(it);
            }
            bool empty = s.List.Count == 0;
            lv.Visible = !empty; bAll.Enabled = bNone.Enabled = !empty; lblEmpty.Visible = empty; link.Visible = false;
            if (empty)
            {
                if (s.Code.Length == 0)
                {
                    lblEmpty.Text = "Der Standardpuffer ist noch nicht eingerichtet" + (canAdmin ? " (Baustein „" + Catalog.StandardPufferName + "“ unter Einstellungen → Equipment & Bausteine)." : ". Der Admin richtet ihn ein.");
                    foreach (object o in cbSrc.Items) { Source x = (Source)o; if (x.List.Count > 0) { linkTo = x; link.Text = "Stattdessen Pufferpaket von " + x.Code + " wählen"; link.Visible = true; break; } }
                }
                else if (canAdmin) { lblEmpty.Text = "Für " + s.Code + " ist kein Pufferpaket hinterlegt."; link.Text = "Pufferpaket für " + s.Code + " anlegen…"; link.Visible = true; }
                else lblEmpty.Text = "Für " + s.Code + " ist kein Pufferpaket hinterlegt. Der Admin legt Pufferpakete an.";
            }
            loading = false; Recount();
        }

        void CheckAll(bool on) { loading = true; foreach (ListViewItem it in lv.Items) it.Checked = on; loading = false; Recount(); }

        int Checked() { int n = 0; foreach (ListViewItem it in lv.Items) if (it.Checked) n++; return n; }

        // Zähler, Knopftext (Anzahl wie sie wirklich übernommen wird) und Knopf aus bei 0
        void Recount()
        {
            int c = Checked(), n = lv.Items.Count, room = rbAppend.Checked ? free : PackageStep.MaxPuffer, eff = Math.Min(c, room);
            lblCount.Text = lv.Visible ? c + " von " + n + (n == 1 ? " Puffer" : " Puffern") + " angehakt" + (eff < c ? " – es passen nur " + eff : "") : "";
            lblCount.ForeColor = eff < c ? Color.DarkOrange : SystemColors.ControlText;
            bOk.Text = eff > 0 ? eff + " Puffer übernehmen" : "Puffer übernehmen"; bOk.Enabled = eff > 0;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo h = lv.HitTest(e.Location);
            string t = h.Item != null && h.SubItem != null && h.SubItem.Text.StartsWith("⚠") ? WarnText : "";
            if (tip.GetToolTip(lv) != t) tip.SetToolTip(lv, t);
        }

        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }

        void CreatePack() { CreateFor = Cur.Code; Append = rbAppend.Checked; DialogResult = DialogResult.Retry; }

        void Commit()
        {
            Result = new List<PufferTemplate>();
            int room = rbAppend.Checked ? free : PackageStep.MaxPuffer;
            foreach (ListViewItem it in lv.Items) if (it.Checked && Result.Count < room) Result.Add(((PufferTemplate)it.Tag).Clone());
            if (Result.Count == 0) return;
            ResultCode = Cur.Code; Append = rbAppend.Checked;
            DialogResult = DialogResult.OK;
        }

#if TESTHOOKS
        internal string TestSourceText { get { return Cur.Text; } }
        internal int TestSourceCount { get { return cbSrc.Items.Count; } }
        internal void TestPickSource(string code) { for (int i = 0; i < cbSrc.Items.Count; i++) if (((Source)cbSrc.Items[i]).Code == code) { cbSrc.SelectedIndex = i; return; } }
        internal void TestCommit() { Commit(); }
        internal void TestCreate() { CreatePack(); }
#endif
    }
}
