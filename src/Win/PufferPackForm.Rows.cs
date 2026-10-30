using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Positionszeilen des Pufferpaket-Dialogs (UX-6 B4): Position | Gerät | gemeinsam | Notiz | ▲ ▼ ✕, Geräteauswahl (erst für das Produkt freigegebene,
    // dann übrige bekannte Geräte der Position), orange Markierung unbekannter/nicht freigegebener Geräte, Statuszeile mit "Zur Liste hinzufügen…".
    partial class PufferPackForm
    {
        const int RowH = 26, ColDev = 124, ColShared = 290, ColNote = 382, ColBtn = 484;   // Spalten-x in der Zeile (Kopfbeschriftung richtet sich danach)
        readonly Panel rows = new Panel();
        readonly List<PRow> rowList = new List<PRow>();
        readonly Label stUnk = new Label(), stNa = new Label();
        readonly LinkLabel stLink = new LinkLabel();
        bool linkOn;   // Sichtbarkeit des Links (Visible ist ohne angezeigtes Fenster immer false)

        enum DevState { Ok, Unknown, NotAllowed }

        class PRow
        {
            public PufferTemplatePos P; public Label N; public ComboBox D; public CheckBox S; public TextBox No; public Button Up, Dn, X;
            public Control[] All { get { return new Control[] { N, D, S, No, Up, Dn, X }; } }
        }

        // Geräte-Eintrag der Auswahl: ToString = ID (wird übernommen); der Zusatz "(nicht für … freigegeben)" wird nur gezeichnet
        class DevOpt { public string Id = ""; public bool NotAllowed; public override string ToString() { return Id; } }

        void BuildRows()
        {
            string[] heads = { "Position", "Gerät", "gemeinsam", "Notiz" }; int[] xs = { 0, ColDev, ColShared, ColNote };
            for (int i = 0; i < heads.Length; i++) Controls.Add(L(heads[i], 273 + xs[i] + 3, 98));
            rows.SetBounds(272, 116, 580, 172); rows.AutoScroll = true; rows.BorderStyle = BorderStyle.FixedSingle; Controls.Add(rows);
            Btn(bPos, "+ Position ▾", 272, 294, 120, delegate { posMenu.Show(bPos, new Point(0, bPos.Height)); }); Controls.Add(bPos);
            stUnk.AutoSize = true; stUnk.Location = new Point(272, 328); stUnk.UseMnemonic = false; stUnk.ForeColor = Color.DarkOrange; Controls.Add(stUnk);
            stLink.Text = "Zur Liste hinzufügen…"; stLink.AutoSize = true; stLink.Visible = false; Controls.Add(stLink);
            stLink.LinkClicked += delegate { AddUnknown(); };
            stNa.AutoSize = true; stNa.Location = new Point(272, 348); stNa.UseMnemonic = false; stNa.ForeColor = Color.DarkOrange; Controls.Add(stNa);
        }

        void ClearRows()
        {
            foreach (PRow r in rowList) Drop(r);
            rowList.Clear();
        }

        void Drop(PRow r) { foreach (Control c in r.All) { tip.SetToolTip(c, null); rows.Controls.Remove(c); c.Dispose(); } }

        void LayoutRows()
        {
            int off = rows.AutoScrollPosition.Y;   // negativ, wenn gescrollt
            int ti = 0;
            for (int i = 0; i < rowList.Count; i++) foreach (Control c in rowList[i].All) { c.Top = 4 + i * RowH + off; c.TabIndex = ti++; }   // Tab-Reihenfolge = sichtbare Reihenfolge
        }

        // ---------- Prüfung und Auswahl ----------

        DevState Check(string sh, string id)
        {
            if (id.Length == 0) return DevState.Ok;
            if (cat.Master.Device(id) == null) return DevState.Unknown;
            List<string> al = cat.Allowed(prod.Code, sh);
            return al.Count > 0 && !al.Contains(id) ? DevState.NotAllowed : DevState.Ok;
        }

        // Auswahl: freigegebene Geräte der Position, dann übrige bekannte Geräte der Position (Positionsliste und Geräte derselben Geräteart)
        List<DevOpt> Options(string sh)
        {
            List<string> allowed = cat.Allowed(prod.Code, sh), more = new List<string>(), cls = new List<string>();
            EquipPosition ep = cat.Position(sh);
            List<string> known = new List<string>(allowed); if (ep != null) foreach (string id in ep.Ids) if (!known.Contains(id)) known.Add(id);
            foreach (string id in known) { EquipDevice d = cat.Master.Device(id); if (d != null && d.Class.Length > 0 && !cls.Contains(d.Class)) cls.Add(d.Class); }
            foreach (string id in known) if (!allowed.Contains(id) && !more.Contains(id)) more.Add(id);
            foreach (EquipDevice d in cat.Master.Devices) if (cls.Contains(d.Class) && !allowed.Contains(d.Id) && !more.Contains(d.Id)) more.Add(d.Id);
            List<DevOpt> r = new List<DevOpt>();
            r.Add(new DevOpt());
            foreach (string id in allowed) { DevOpt o = new DevOpt(); o.Id = id; r.Add(o); }
            foreach (string id in more) { DevOpt o = new DevOpt(); o.Id = id; o.NotAllowed = allowed.Count > 0; r.Add(o); }
            return r;
        }

        void DrawOpt(object sender, DrawItemEventArgs e)
        {
            ComboBox d = (ComboBox)sender; if (e.Index < 0) return;
            e.DrawBackground();
            DevOpt o = d.Items[e.Index] as DevOpt; if (o == null) return;
            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            Rectangle b = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 2, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, o.Id, e.Font, b, e.ForeColor, f);
            if (o.NotAllowed)
            {
                int w = TextRenderer.MeasureText(e.Graphics, o.Id + " ", e.Font, new Size(1000, 100), f).Width;
                b.X += w; b.Width -= w;
                TextRenderer.DrawText(e.Graphics, "(nicht für " + prod.Code + " freigegeben)", e.Font, b, (e.State & DrawItemState.Selected) != 0 ? e.ForeColor : SystemColors.GrayText, f);
            }
            e.DrawFocusRectangle();
        }

        // Farbe und Tooltip eines Geräte-Felds; Wechsel-Geräte (mehrere IDs) werden einzeln geprüft
        void Mark(PRow r)
        {
            string msg = "";
            foreach (string id in r.P.Ids)
            {
                DevState s = Check(r.P.Short, id);
                if (s == DevState.Unknown) msg += (msg.Length > 0 ? "\n" : "") + id + ": nicht in der Equipment-Liste (nicht validiert).";
                else if (s == DevState.NotAllowed) msg += (msg.Length > 0 ? "\n" : "") + id + ": für Produkt " + prod.Code + " nicht freigegeben.";
            }
            r.D.ForeColor = msg.Length > 0 ? Color.DarkOrange : SystemColors.WindowText;
            tip.SetToolTip(r.D, msg);
        }

        void UpdateStatus()
        {
            List<string> unk = new List<string>(), na = new List<string>();
            if (cur != null)
                foreach (PufferTemplatePos p in cur.Positions)
                    foreach (string id in p.Ids)
                    {
                        DevState s = Check(p.Short, id);
                        if (s == DevState.Unknown && !unk.Contains(id)) unk.Add(id);
                        else if (s == DevState.NotAllowed && !na.Contains(id)) na.Add(id);
                    }
            stUnk.Text = unk.Count == 0 ? "" : unk.Count + (unk.Count == 1 ? " Gerät nicht" : " Geräte nicht") + " in der Equipment-Liste: " + string.Join(", ", unk.ToArray())
                + (readOnly ? " – der Admin nimmt sie auf." : "");
            stNa.Text = na.Count == 0 ? "" : na.Count + (na.Count == 1 ? " Gerät nicht" : " Geräte nicht") + " für " + prod.Code + " freigegeben: " + string.Join(", ", na.ToArray());
            linkOn = unk.Count > 0 && !readOnly; stLink.Visible = linkOn;
            stLink.Location = new Point(stUnk.Right + 8, stUnk.Top);
        }

        // Erstes unbekanntes Gerät über den vorhandenen Dialog in die Equipment-Liste aufnehmen (nur mit Recht)
        void AddUnknown()
        {
            if (cur == null || readOnly) return;
            foreach (PufferTemplatePos p in cur.Positions)
                foreach (string id in p.Ids)
                    if (Check(p.Short, id) == DevState.Unknown)
                    {
                        using (EquipAddForm f = new EquipAddForm(id, cat, p.Short))
                        {
                            if (f.ShowDialog(this) != DialogResult.OK) return;
                            AddDevice(f.Device, p.Short);
                        }
                        return;
                    }
        }

        internal void AddDevice(EquipDevice d, string shortName)
        {
            if (readOnly || d == null || d.Id.Length == 0) return;
            DevAdd a = new DevAdd(); a.D = d; a.Short = shortName; a.ApplyTo(cat); Added.Add(a);
            Choose(cur);   // Auswahllisten und Markierungen neu aufbauen
        }

        // ---------- Zeilen ----------

        void AddRow(PufferTemplatePos p)
        {
            PRow r = new PRow(); r.P = p;
            EquipPosition ep = cat.Position(p.Short);
            r.N = new Label(); r.N.Text = ep != null ? ep.Name : p.Short; r.N.AutoEllipsis = true; r.N.UseMnemonic = false; r.N.TextAlign = ContentAlignment.MiddleLeft; r.N.SetBounds(4, 0, ColDev - 8, 22);
            r.D = new ComboBox(); r.D.DropDownStyle = ComboBoxStyle.DropDown; r.D.SetBounds(ColDev, 0, ColShared - ColDev - 6, 22);
            r.D.DrawMode = DrawMode.OwnerDrawFixed; r.D.DropDownWidth = 300; r.D.DrawItem += DrawOpt;
            foreach (DevOpt o in Options(p.Short)) r.D.Items.Add(o);
            if (p.Ids.Count > 1) { r.D.Text = string.Join(" / ", p.Ids.ToArray()); r.D.Enabled = false; }   // Wechsel-Geräte bleiben unverändert
            else r.D.Text = p.Ids.Count == 1 ? p.Ids[0] : "";
            r.S = new CheckBox(); r.S.Text = "gemeinsam"; r.S.Checked = p.Shared; r.S.SetBounds(ColShared, 0, ColNote - ColShared - 4, 22);
            r.No = new TextBox(); r.No.Text = p.Note; r.No.SetBounds(ColNote, 0, ColBtn - ColNote - 4, 22); Ui.SimpleMenu(r.No);
            r.Up = RowBtn("▲", ColBtn, "Zeile nach oben (Alt+↑)"); r.Dn = RowBtn("▼", ColBtn + 24, "Zeile nach unten (Alt+↓)"); r.X = RowBtn("✕", ColBtn + 48, "Zeile entfernen");
            tip.SetToolTip(r.S, "Das Gerät gilt für mehrere Puffer gemeinsam (z. B. WFI-Anschluss)");
            r.D.TextChanged += delegate { if (loading || p.Ids.Count > 1) return; p.Ids.Clear(); if (r.D.Text.Trim().Length > 0) p.Ids.Add(r.D.Text.Trim()); Mark(r); Chg("Gerät geändert"); UpdateStatus(); };
            r.S.CheckedChanged += delegate { if (!loading) { p.Shared = r.S.Checked; Chg("„gemeinsam“ geändert"); } };
            r.No.TextChanged += delegate { if (!loading) { p.Note = r.No.Text.Trim(); Chg("Notiz geändert"); } };
            r.Up.Click += delegate { MoveRow(r, -1); }; r.Dn.Click += delegate { MoveRow(r, 1); }; r.X.Click += delegate { RemoveRow(r); };
            if (readOnly) { r.D.Enabled = r.S.Enabled = r.No.Enabled = false; r.Up.Visible = r.Dn.Visible = r.X.Visible = false; }
            rowList.Add(r); rows.Controls.AddRange(r.All);
            Mark(r);
        }

        Button RowBtn(string text, int x, string tipText)
        {
            Button b = new Button(); b.Text = text; b.SetBounds(x, 0, 22, 22);
            tip.SetToolTip(b, tipText);
            return b;
        }

        void AddPos(string shortName)
        {
            if (cur == null) return;
            PufferTemplatePos p = new PufferTemplatePos(); p.Short = shortName; cur.Positions.Add(p);
            loading = true; AddRow(p); loading = false;
            LayoutRows(); RefreshItem(); UpdateStatus(); Chg("Position " + shortName + " hinzugefügt");
            rows.ScrollControlIntoView(rowList[rowList.Count - 1].D);
        }

        void RemoveRow(PRow r)
        {
            if (cur == null) return;
            cur.Positions.Remove(r.P); rowList.Remove(r);
            Drop(r);
            LayoutRows(); RefreshItem(); UpdateStatus(); Chg("Position " + r.P.Short + " entfernt");
        }

        // Zeile um d (-1/+1) verschieben: Reihenfolge im Plantext; Fokus bleibt im Feld
        void MoveRow(PRow r, int d)
        {
            if (cur == null || readOnly) return;
            int i = rowList.IndexOf(r), j = i + d;
            if (i < 0 || j < 0 || j >= rowList.Count) return;
            cur.Positions.RemoveAt(i); cur.Positions.Insert(j, r.P);
            rowList.RemoveAt(i); rowList.Insert(j, r);
            LayoutRows(); Chg("Reihenfolge der Zeilen geändert");
            Control f = ActiveControl; if (f != null && f.Parent == rows) rows.ScrollControlIntoView(f);
        }

        PRow RowOf(Control c) { return c == null ? null : rowList.Find(delegate(PRow r) { return Array.IndexOf(r.All, c) >= 0; }); }

        // Alt+↑/↓ verschiebt die Zeile mit Fokus (bzw. die markierte Vorlage), auch im Geräte-Feld (dort öffnet Alt+↓ sonst die Liste)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            int d = keyData == (Keys.Alt | Keys.Up) ? -1 : keyData == (Keys.Alt | Keys.Down) ? 1 : 0;
            if (d != 0 && !readOnly)
            {
                PRow r = RowOf(ActiveControl);
                if (r != null) { MoveRow(r, d); return true; }
                if (ActiveControl == lb) { MoveTemplate(d); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

#if TESTHOOKS
        public void TestSetDevice(int row, string id) { rowList[row].D.Text = id; }
        public void TestShared(int row, bool on) { rowList[row].S.Checked = on; }
        public int TestTab(int row) { return rowList[row].N.TabIndex; }
        public int TestRows { get { return rowList.Count; } }
        public void TestMove(int row, int d) { MoveRow(rowList[row], d); }
        public void TestKey(int row, Keys k) { ActiveControl = rowList[row].No; Message m = new Message(); ProcessCmdKey(ref m, k); }
        public string TestShort(int row) { return rowList[row].P.Short; }
        public bool TestOrange(int row) { return rowList[row].D.ForeColor == Color.DarkOrange; }
        public string TestTip(int row) { return tip.GetToolTip(rowList[row].D); }
        public string TestStatus { get { return stUnk.Text + "|" + stNa.Text; } }
        public bool TestLinkVisible { get { return linkOn; } }
        public List<string> TestOptions(int row) { List<string> r = new List<string>(); foreach (object o in rowList[row].D.Items) { DevOpt d = (DevOpt)o; r.Add(d.Id + (d.NotAllowed ? "*" : "")); } return r; }
        public void TestAddPos(string sh) { AddPos(sh); }
        public List<string> TestPosMenu() { List<string> r = new List<string>(); foreach (ToolStripItem i in posMenu.Items) r.Add(i.Text); return r; }
#endif
    }
}
