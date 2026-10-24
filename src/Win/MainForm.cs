using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

// Nur .NET-4.0-APIs, C# 5 und Systemschrift (Windows-XP-Kompatibilitaet).
namespace Arbeitsplan.Win
{
    // Panel, das beim Fokuswechsel nicht selbst scrollt (sonst springt die Ansicht nach oben).
    class QuietPanel : Panel
    {
        protected override Point ScrollToControl(Control activeControl) { return DisplayRectangle.Location; }
    }

    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        const int WM_SETREDRAW = 0xB;
        static readonly int[] Swatches = { 0x000000, 0xC00000, 0x0050C8, 0x008000, 0xE06000, 0x7030A0, 0x808080 };
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

        readonly IDataStore store;
        readonly ILockService locks = new MemoryLockService();
        readonly UserDb users = UserDb.Sample();
        readonly User me;
        readonly List<RowInfo> rowInfo = new List<RowInfo>();
        readonly PlanView view;
        readonly Label weekLabel = new Label(), userLabel = new Label(), searchLabel = new Label(), hitsLabel = new Label();
        readonly TextBox search = new TextBox();
        readonly Panel topBar = new Panel(), dateBar = new Panel(), bar = new Panel(), fmtPanel = new Panel(), rowTools = new Panel();
        readonly QuietPanel canvas = new QuietPanel();
        readonly Label[] dateLabels = new Label[7];
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Button btnUsers = new Button(), btnAdd = new Button(), btnUp = new Button(), btnDown = new Button();
        readonly Bitmap pencilIcon = MakePencil();

        // Eingabefelder, nur im Bearbeitungsmodus sichtbar (liegen ueber dem aktiven Block)
        readonly TextBox edHead = new TextBox();
        readonly TextBox[] edLbl = { new TextBox(), new TextBox(), new TextBox() };
        readonly TextBox[] edPpl = { new TextBox(), new TextBox(), new TextBox() };
        readonly RichTextBox edContent = new RichTextBox();
        readonly PictureBox edPencil = new PictureBox();
        readonly Button edMinus, edPlus;
        readonly List<Control> editors = new List<Control>();

        DateTime start;
        CellData editCell;
        RowInfo workRow;
        bool loadingEditor, inResize;
        int hitPos = -1;

        public MainForm(string login, bool failSaves)
        {
            Text = "Arbeitsplan (Prototyp, lokale Testdaten)";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = SystemFonts.MessageBoxFont;    // Segoe UI ab Vista, Tahoma auf XP
            KeyPreview = true;

            start = MondayOf(DateTime.Today);
            MemoryStore ms = MemoryStore.Sample(start);
            ms.FailSaves = failSaves;
            store = ms;
            locks.Acquire(start.AddDays(1), 1, "Paula Werk");   // Mock: Dienstag, Zeile 1 ist "in Bearbeitung"

            me = users.FindByLogin(login);
            if (me == null)
            {
                // Annahme (Prototyp): unbekannter Windows-Nutzer = Produktion mit Windows-Namen.
                me = new User(); me.Login = login;
                UserDb.ApplyPackage(me.Rights, users.Package("Produktion"));
            }

            view = new PlanView(Font);
            view.Equip = new MockEquipmentProvider();
            view.Colors = StatusColors.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "statuscolors.ini"));
            view.MyKuerzel = me.Kuerzel;
            view.Rows = rowInfo;
            view.CellActivated = BeginEdit;
            view.SelectionChanged = UpdateTools;
            view.LayoutDone += delegate { PositionEditors(); hitsLabel.Text = view.Query.Length > 0 ? view.HitCount + " in dieser Woche" : ""; };
            view.MouseEnter += delegate { if (editCell == null && !(ActiveControl is TextBox)) view.Focus(); };   // Mausrad scrollt

            BuildTopBar();
            BuildEditors();
            edMinus = SmallButton("-", delegate { if (workRow != null && workRow.MinLines > 1) { workRow.MinLines--; view.Relayout(); } });
            edPlus = SmallButton("+", delegate { if (workRow != null && workRow.MinLines < 30) { workRow.MinLines++; view.Relayout(); } });
            view.Controls.Add(edMinus); view.Controls.Add(edPlus);
            editors.Add(edMinus); editors.Add(edPlus);
            foreach (Control c in editors) c.Visible = false;

            dateBar.Dock = DockStyle.Top;
            dateBar.Height = 26;
            for (int c = 0; c < 7; c++)
            {
                Label l = new Label();
                l.TextAlign = ContentAlignment.MiddleCenter;
                l.Font = new Font(Font, FontStyle.Bold);
                dateLabels[c] = l;
                dateBar.Controls.Add(l);
            }

            canvas.Dock = DockStyle.Fill;
            canvas.AutoScroll = true;
            canvas.Controls.Add(view);
            canvas.Resize += delegate { ResizeView(); };

            BuildBottomBar();
            Controls.Add(canvas);
            Controls.Add(dateBar);
            Controls.Add(topBar);
            Controls.Add(bar);

            KeyDown += OnKey;
            FormClosing += OnClosing;
            Rebuild();
        }

        // ---------- Aufbau der Leisten ----------

        void BuildTopBar()
        {
            weekLabel.Dock = DockStyle.Fill;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

            // links: Zeilenwerkzeuge und (im Bearbeitungsmodus) Schriftfarbe
            btnAdd.Text = "+ Zeile"; btnUp.Text = "Hoch"; btnDown.Text = "Runter";
            btnAdd.Click += delegate { AddRow(); };
            btnUp.Click += delegate { MoveSelected(-1); };
            btnDown.Click += delegate { MoveSelected(1); };
            int x = 4;
            foreach (Button b in new Button[] { btnAdd, btnUp, btnDown })
            {
                b.SetBounds(x, 4, b == btnAdd ? 66 : 52, 26); x += b.Width + 3;
                rowTools.Controls.Add(b);
            }
            rowTools.Dock = DockStyle.Left; rowTools.Width = x + 4;

            Label fl = new Label(); fl.Text = "Schriftfarbe:"; fl.AutoSize = true; fl.Location = new Point(4, 9);
            fmtPanel.Controls.Add(fl);
            int sx = fl.PreferredWidth + 10;
            foreach (int rgb in Swatches)
            {
                int col = rgb;
                Button b = new Button();
                b.SetBounds(sx, 6, 22, 22); sx += 24;
                b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb((col >> 16) & 255, (col >> 8) & 255, col & 255); b.TabStop = false;
                b.Click += delegate { ApplyColor(col); };
                fmtPanel.Controls.Add(b);
            }
            fmtPanel.Dock = DockStyle.Left; fmtPanel.Width = sx + 6; fmtPanel.Visible = false;

            // rechts: Suche in Inhalt/Equipment/Kuerzel (nicht Raumnamen)
            searchLabel.Text = "Suchen:"; searchLabel.AutoSize = true; searchLabel.Location = new Point(0, 9);
            search.Width = 130; search.Location = new Point(searchLabel.PreferredWidth + 6, 6);
            hitsLabel.AutoSize = true; hitsLabel.Location = new Point(search.Right + 6, 9);
            search.TextChanged += delegate { OnSearchChanged(); };
            search.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { NextHit(); e.SuppressKeyPress = true; } };
            Panel sp = new Panel();
            sp.Dock = DockStyle.Right; sp.Width = search.Right + 130;
            sp.Controls.Add(searchLabel); sp.Controls.Add(search); sp.Controls.Add(hitsLabel);

            topBar.Dock = DockStyle.Top; topBar.Height = 34;
            topBar.Controls.Add(weekLabel);
            topBar.Controls.Add(sp);
            topBar.Controls.Add(fmtPanel);
            topBar.Controls.Add(rowTools);
            btnAdd.Enabled = me.Has(Perm.EditNames);
            rowTools.Visible = me.Has(Perm.EditNames) || me.Has(Perm.SortRows);
            btnUp.Visible = btnDown.Visible = me.Has(Perm.SortRows);
            UpdateTools();
        }

        void BuildBottomBar()
        {
            bar.Dock = DockStyle.Bottom;
            bar.Height = 48;
            nav.AutoSize = true; nav.WrapContents = false; nav.Top = 6;
            AddNav("«  Woche", delegate { Go(-7); });
            AddNav("‹  Tag", delegate { Go(-1); });
            AddNav("Heute", delegate { Go(MondayOf(DateTime.Today) - start); });
            AddNav("Tag  ›", delegate { Go(1); });
            AddNav("Woche  »", delegate { Go(7); });
            userLabel.AutoSize = true;
            userLabel.Location = new Point(10, 16);
            userLabel.Text = "Angemeldet: " + me.Display + " (" + (users.PackageOf(me) != null ? users.PackageOf(me).Name : "benutzerdefiniert") + ")";
            btnUsers.Text = "Nutzer…";
            btnUsers.Size = new Size(90, 34);
            btnUsers.Visible = me.Has(Perm.UserCreate) || me.Has(Perm.UserEdit) || me.Has(Perm.UserDelete);
            btnUsers.Click += delegate { using (UserAdminForm f = new UserAdminForm(users, me)) f.ShowDialog(this); };
            bar.Controls.Add(nav); bar.Controls.Add(userLabel); bar.Controls.Add(btnUsers);
            bar.Resize += delegate { CenterBar(); };
            Shown += delegate { CenterBar(); };
        }

        void BuildEditors()
        {
            Color white = Color.White;
            edHead.Font = new Font(Font, FontStyle.Bold);
            edContent.BorderStyle = BorderStyle.None; edContent.ScrollBars = RichTextBoxScrollBars.None; edContent.DetectUrls = false;
            edContent.HideSelection = false; edContent.AcceptsTab = false;
            edPencil.Image = pencilIcon; edPencil.Size = new Size(18, 18);
            List<Control> all = new List<Control>();
            all.Add(edHead); all.Add(edContent); all.Add(edPencil);
            all.AddRange(edLbl); all.AddRange(edPpl);
            foreach (Control c in all)
            {
                TextBox t = c as TextBox;
                if (t != null) { t.BorderStyle = BorderStyle.None; t.AutoSize = false; t.Multiline = true; t.AcceptsReturn = true; }
                c.Visible = false; view.Controls.Add(c); editors.Add(c);
            }
            foreach (TextBox l in edLbl) { l.Font = new Font(Font, FontStyle.Bold); l.TextAlign = HorizontalAlignment.Center; l.Multiline = false; }
            edHead.Multiline = false;

            edContent.TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.Content = Norm(edContent.Text); editCell.Dirty = true; view.Relayout(); } };
            for (int s = 0; s < 3; s++)
            {
                int si = s;
                edPpl[s].TextChanged += delegate { if (!loadingEditor && editCell != null) { editCell.People[si] = Norm(edPpl[si].Text); editCell.Dirty = true; view.Relayout(); } };
            }
        }

        Button SmallButton(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.Size = new Size(19, 19); b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Arial", 9f, FontStyle.Bold); b.UseCompatibleTextRendering = true;
            b.Padding = new Padding(0); b.TabStop = false;
            b.Click += click;
            return b;
        }

        void AddNav(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.AutoSize = true; b.MinimumSize = new Size(86, 34); b.Margin = new Padding(3, 0, 3, 0);
            b.Click += click;
            nav.Controls.Add(b);
        }

        void CenterBar()
        {
            nav.Left = Math.Max(userLabel.Right + 10, (bar.ClientSize.Width - nav.Width) / 2);
            btnUsers.Location = new Point(bar.ClientSize.Width - btnUsers.Width - 10, 7);
        }

        // ---------- Navigation ----------

        static DateTime MondayOf(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }
        static string Norm(string s) { return s.Replace("\r\n", "\n"); }
        static string Win(string s) { return s.Replace("\r\n", "\n").Replace("\n", "\r\n"); }

        void Go(int days) { Go(TimeSpan.FromDays(days)); }

        void Go(TimeSpan delta)
        {
            if (!EndEdit(true)) return;           // Speichern fehlgeschlagen: nicht wechseln, nichts geht verloren
            start = start.Add(delta);
            Rebuild();
        }

        // Laedt die Woche neu und zeichnet nur neu (keine Steuerelemente werden erzeugt -> schnell).
        void Rebuild()
        {
            weekLabel.Text = "KW" + De.Calendar.GetWeekOfYear(start, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + " (" + start.ToString("dd.MM.yyyy", De) + " - " + start.AddDays(6).ToString("dd.MM.yyyy", De) + ")";
            for (int c = 0; c < 7; c++)
            {
                DateTime d = start.AddDays(c);
                dateLabels[c].Text = d.ToString("ddd, dd.MM.yyyy", De);
                dateLabels[c].BackColor = d == DateTime.Today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            }
            int selRow = -1; DateTime selDay = DateTime.MinValue;
            if (view.Selected != null) { selRow = rowInfo[view.Selected.RowIndex].Id; selDay = view.Selected.Day; }

            rowInfo.Clear(); rowInfo.AddRange(store.GetRows());
            view.Start = start;
            view.Cells.Clear();
            view.Selected = null;
            for (int r = 0; r < rowInfo.Count; r++)
                for (int c = 0; c < 7; c++)
                {
                    CellData d = new CellData();
                    d.Day = start.AddDays(c); d.Col = c; d.RowIndex = r;
                    d.Cur = store.Get(d.Day, rowInfo[r].Id) ?? new Entry();
                    d.Content = d.Cur.Content; d.People = (string[])d.Cur.People.Clone();
                    view.Cells.Add(d);
                    if (rowInfo[r].Id == selRow && d.Day == selDay) view.Selected = d;
                }
            ResizeView();
            UpdateTools();
        }

        void ResizeView()
        {
            if (inResize) return;
            inResize = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int cw = canvas.ClientSize.Width;
                    view.Width = cw;
                    view.MarkAllDirty();
                    view.Relayout();
                    if (canvas.ClientSize.Width == cw) break;   // Scrollbalken kann die Breite aendern
                }
                for (int c = 0; c < 7; c++) dateLabels[c].SetBounds(view.ColLeft(c), 2, view.ColW, 22);
            }
            finally { inResize = false; }
        }

        // ---------- Zeilen ----------

        void UpdateTools()
        {
            btnUp.Enabled = btnDown.Enabled = me.Has(Perm.SortRows) && view.Selected != null;
        }

        void AddRow()
        {
            if (!me.Has(Perm.EditNames) || !EndEdit(true)) return;
            try { store.AddRow(); } catch (Exception ex) { ShowSaveError(ex); return; }
            Rebuild();
        }

        void MoveSelected(int delta)
        {
            if (!me.Has(Perm.SortRows) || view.Selected == null || !EndEdit(true)) return;
            try { store.MoveRow(rowInfo[view.Selected.RowIndex].Id, delta); } catch (Exception ex) { ShowSaveError(ex); return; }
            Rebuild();
        }

        // ---------- Suche ----------

        void OnSearchChanged()
        {
            view.Query = search.Text.Trim();
            hitPos = -1;
            view.MarkAllDirty();
            view.Relayout();
        }

        // Enter: naechster Treffer (auch in anderen Wochen); Raumnamen werden nicht durchsucht.
        void NextHit()
        {
            if (view.Query.Length == 0) return;
            List<SearchHit> hits = store.Find(view.Query);
            if (hits.Count == 0) { hitsLabel.Text = "keine Treffer"; return; }
            hitPos = (hitPos + 1) % hits.Count;
            SearchHit h = hits[hitPos];
            if (h.Day < start || h.Day > start.AddDays(6))
            {
                if (!EndEdit(true)) return;
                start = MondayOf(h.Day);
                Rebuild();
            }
            foreach (CellData c in view.Cells)
                if (c.Day == h.Day && rowInfo[c.RowIndex].Id == h.RowId)
                {
                    view.Selected = c; view.Invalidate(); UpdateTools();
                    canvas.AutoScrollPosition = new Point(0, Math.Max(0, c.R.Y - 4));
                    break;
                }
            hitsLabel.Text = "Treffer " + (hitPos + 1) + " von " + hits.Count;
        }

        // ---------- Bearbeitungsmodus (Soft-Lock) ----------

        void BeginEdit(CellData c)
        {
            if (editCell == c) return;
            bool allowed = me.Has(Perm.EditText) || (c.Cur.IsEmpty && me.Has(Perm.AddText));
            if (!allowed) { MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!EndEdit(true)) return;

            RowInfo ri = rowInfo[c.RowIndex];
            string holder = locks.Holder(c.Day, ri.Id, me.Display);
            if (holder != null)
            {
                string title = ri.Name.Length > 0 ? ri.Name : "Block " + (c.RowIndex + 1);
                MessageBox.Show("Das Fenster " + title + " wird gerade von " + holder + " bearbeitet.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            locks.Acquire(c.Day, ri.Id, me.Display);   // Soft-Lock: danach trotzdem bearbeiten und speichern

            editCell = c;
            workRow = ri.Clone();
            view.Override = workRow;
            c.Editing = true; c.Dirty = true;
            view.Selected = c;
            LoadEditors(c);
            fmtPanel.Visible = me.Has(Perm.Format);
            view.Relayout();
            edContent.Focus();
        }

        void LoadEditors(CellData c)
        {
            loadingEditor = true;
            bool today = c.Day.Date == DateTime.Today;
            bool weekend = c.Day.DayOfWeek == DayOfWeek.Saturday || c.Day.DayOfWeek == DayOfWeek.Sunday;
            Color headBack = today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            Color back = today ? Color.FromArgb(255, 250, 230) : (weekend ? Color.FromArgb(244, 246, 250) : Color.White);
            bool names = me.Has(Perm.EditNames);

            edHead.Text = workRow.Name; edHead.BackColor = headBack; edHead.ReadOnly = !names;
            edPencil.BackColor = headBack;
            edMinus.BackColor = edPlus.BackColor = headBack;
            edContent.BackColor = back;
            edContent.Text = c.Cur.Content;
            edContent.SelectAll(); edContent.SelectionColor = Color.Black;
            foreach (ColorSpan sp in c.Cur.Spans)
            {
                edContent.Select(sp.Start, sp.Length);
                edContent.SelectionColor = Color.FromArgb((sp.Rgb >> 16) & 255, (sp.Rgb >> 8) & 255, sp.Rgb & 255);
            }
            edContent.Select(edContent.TextLength, 0);
            for (int s = 0; s < 3; s++)
            {
                edLbl[s].Text = workRow.Labels[s]; edLbl[s].BackColor = PlanView.ShiftLabel[s]; edLbl[s].ReadOnly = !names;
                edPpl[s].Text = Win(c.Cur.People[s]); edPpl[s].BackColor = PlanView.ShiftBack[s];
            }
            loadingEditor = false;
        }

        // Eingabefelder exakt ueber den Block legen (nach jedem Layout).
        void PositionEditors()
        {
            if (editCell == null) { foreach (Control c in editors) c.Visible = false; return; }
            CellData c0 = editCell;
            bool canH = me.Has(Perm.EditHeight);
            int right = c0.RHead.Right - 19;
            edPencil.Location = new Point(right, c0.RHead.Y + 3);
            edPlus.Location = new Point(right - 21, c0.RHead.Y + 2); edMinus.Location = new Point(right - 42, c0.RHead.Y + 2);
            edPlus.Visible = edMinus.Visible = canH;
            edHead.SetBounds(c0.RHead.X, c0.RHead.Y, right - c0.RHead.X - (canH ? 44 : 2), c0.RHead.Height + 1);
            edContent.SetBounds(c0.RContent.X + PlanView.TP - 3, c0.RContent.Y + PlanView.TV, c0.RContent.Width - 2 * (PlanView.TP - 3) - 6, c0.RContent.Height - 2 * PlanView.TV);
            for (int s = 0; s < 3; s++)
            {
                edLbl[s].SetBounds(c0.RLbl[s].X, c0.RLbl[s].Y, c0.RLbl[s].Width, c0.RLbl[s].Height);
                edPpl[s].SetBounds(c0.RPpl[s].X + PlanView.TP - 2, c0.RPpl[s].Y + PlanView.TV, c0.RPpl[s].Width - 2 * (PlanView.TP - 2), c0.RPpl[s].Height - 2 * PlanView.TV);
            }
            foreach (Control c in editors) if (c != edPlus && c != edMinus) c.Visible = true;
            edPencil.BringToFront();
        }

        void ApplyColor(int rgb)
        {
            if (editCell == null || !me.Has(Perm.Format)) return;
            edContent.SelectionColor = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            edContent.Focus();
        }

        // Manuelle Schriftfarben aus dem Editor lesen (Zeichen fuer Zeichen; schwarz = Standard = kein Bereich).
        List<ColorSpan> ExtractSpans()
        {
            List<ColorSpan> res = new List<ColorSpan>();
            int n = edContent.TextLength;
            if (n == 0) return res;
            int s0 = edContent.SelectionStart, l0 = edContent.SelectionLength;
            SendMessage(edContent.Handle, WM_SETREDRAW, 0, 0);
            ColorSpan open = null;
            for (int i = 0; i <= n; i++)
            {
                int rgb = 0;
                if (i < n) { edContent.Select(i, 1); Color c = edContent.SelectionColor; rgb = (c.R << 16) | (c.G << 8) | c.B; }
                if (open != null && rgb != open.Rgb) { open.Length = i - open.Start; res.Add(open); open = null; }
                if (open == null && rgb != 0 && i < n) { open = new ColorSpan(); open.Start = i; open.Rgb = rgb; }
            }
            edContent.Select(s0, l0);
            SendMessage(edContent.Handle, WM_SETREDRAW, 1, 0);
            return res;
        }

        RowInfo WorkRow(CellData c)
        {
            RowInfo r = rowInfo[c.RowIndex].Clone();
            if (me.Has(Perm.EditNames)) { r.Name = edHead.Text; for (int s = 0; s < 3; s++) r.Labels[s] = edLbl[s].Text; }
            if (me.Has(Perm.EditHeight)) r.MinLines = workRow.MinLines;
            return r;
        }

        Entry WorkEntry(CellData c)
        {
            Entry n = c.Cur.Clone();
            n.Content = Norm(edContent.Text);
            n.Spans = me.Has(Perm.Format) ? ExtractSpans() : n.Spans;
            for (int s = 0; s < 3; s++) n.People[s] = Norm(edPpl[s].Text);
            return n;
        }

        bool EntryDirty(CellData c, Entry n)
        {
            return n.Content != c.Cur.Content || n.People[0] != c.Cur.People[0] || n.People[1] != c.Cur.People[1] || n.People[2] != c.Cur.People[2]
                || n.SpanKey() != c.Cur.SpanKey();
        }

        bool IsDirty(CellData c) { return EntryDirty(c, WorkEntry(c)) || !WorkRow(c).SameAs(rowInfo[c.RowIndex]); }

        // false = Speichern fehlgeschlagen; Block bleibt im Bearbeitungsmodus, Text bleibt erhalten.
        bool EndEdit(bool save)
        {
            CellData c = editCell;
            if (c == null) return true;
            if (save)
            {
                Entry n = WorkEntry(c);
                if (EntryDirty(c, n))
                {
                    if (!TrySave(c, n)) return false;
                    c.Cur = n;
                }
                RowInfo wr = WorkRow(c);
                if (!wr.SameAs(rowInfo[c.RowIndex]))
                {
                    try { store.SaveRow(wr); }
                    catch (Exception ex) { ShowSaveError(ex); return false; }
                    rowInfo[c.RowIndex] = wr;       // Name/Label/Hoehe gelten fuer die ganze Zeile
                }
            }
            locks.Release(c.Day, rowInfo[c.RowIndex].Id, me.Display);
            c.Content = c.Cur.Content; c.People = (string[])c.Cur.People.Clone();
            c.Editing = false; c.Dirty = true;
            editCell = null; workRow = null; view.Override = null;
            fmtPanel.Visible = false;
            view.Relayout();
            return true;
        }

        bool TrySave(CellData c, Entry n)
        {
            int rowId = rowInfo[c.RowIndex].Id;
            try
            {
                if (store.Save(c.Day, rowId, n)) return true;
                Entry latest = store.Get(c.Day, rowId);
                if (MessageBox.Show("Der Block wurde inzwischen von jemand anderem geändert.\n\nJa = meine Fassung speichern (überschreibt die andere)\nNein = im Editor bleiben",
                    "Konflikt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                n.Version = latest != null ? latest.Version : 0;
                if (store.Save(c.Day, rowId, n)) return true;
                MessageBox.Show("Der Block wurde erneut geändert. Bitte noch einmal speichern.", "Konflikt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex) { ShowSaveError(ex); return false; }
        }

        static void ShowSaveError(Exception ex)
        {
            MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message +
                "\n\nIhre Änderung ist NICHT verloren und bleibt im Editor sichtbar.\nSpeichern später erneut versuchen (Strg+S) oder mit Esc verwerfen.",
                "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; return; }
            if (editCell == null) return;
            if (e.Control && e.KeyCode == Keys.S) { EndEdit(true); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape)
            {
                if (IsDirty(editCell) && MessageBox.Show("Änderungen verwerfen?", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                EndEdit(false);
                e.SuppressKeyPress = true;
            }
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (EndEdit(true)) return;
            if (MessageBox.Show("Nicht gespeicherte Änderung verwerfen und schließen?", "Arbeitsplan", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                EndEdit(false);
            else e.Cancel = true;
        }

        // ---------- Testhilfen (--shot) ----------

        public Form TestUsers()
        {
            UserAdminForm u = new UserAdminForm(users, me);
            u.StartPosition = FormStartPosition.Manual; u.Location = new Point(0, 0); u.TopMost = true;
            u.Show();
            return u;
        }

        public void TestEdit(int col, int row)
        {
            foreach (CellData c in view.Cells) if (c.RowIndex == row && c.Col == col) BeginEdit(c);
        }

        public void TestSearch(string q) { search.Text = q; }

        // Selbsttest (Admin): Bearbeiten, Farbe, Speichern, Zeilenname/F-S-N/Hoehe, Sortieren.
        public string SelfTest()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            CellData c = null;
            foreach (CellData x in view.Cells) if (x.RowIndex == 0 && x.Col == 4) c = x;
            int rowId = rowInfo[0].Id; DateTime day = c.Day;
            BeginEdit(c);
            sb.AppendLine("Editing=" + c.Editing);
            edContent.Text = "Neuer Text LKZ"; edContent.Select(11, 3); edContent.SelectionColor = Color.Red;
            edPpl[0].Text = "TEST, ABCD";
            edHead.Text = "Suite 1A"; edLbl[0].Text = "Fr"; workRow.MinLines = 5;
            sb.AppendLine("Dirty=" + IsDirty(c));
            bool ok = EndEdit(true);
            Entry e = store.Get(day, rowId);
            sb.AppendLine("Save=" + ok + " Content=[" + e.Content + "] Spans=" + e.SpanKey() + " F=[" + e.People[0] + "] Ver=" + e.Version);
            RowInfo r = store.GetRows()[0];
            sb.AppendLine("Row: " + r.Name + " / " + r.Labels[0] + " / Lines=" + r.MinLines);
            Rebuild();
            sb.AppendLine("Nach Rebuild: " + view.Cells[4].Content.Replace("\n", "|") + " Hits=" + view.HitCount);
            view.Selected = view.Cells[0]; MoveSelected(1);
            sb.AppendLine("Nach Sortieren: Zeile1=" + store.GetRows()[0].Name + ", Zeile2=" + store.GetRows()[1].Name);
            sb.AppendLine("Find 'TEST': " + store.Find("TEST").Count);
            return sb.ToString();
        }

        // Messung: n Tageswechsel inkl. Neuzeichnen (ms je Wechsel).
        public double Bench(int n)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) { Go(i % 2 == 0 ? 7 : -7); Refresh(); }
            return sw.ElapsedMilliseconds / (double)n;
        }

        // Notizblock mit Stift, per GDI+ gezeichnet (keine Bilddatei, keine Sonderzeichen-Schrift noetig).
        static Bitmap MakePencil()
        {
            Bitmap b = new Bitmap(18, 18);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillRectangle(Brushes.White, 1, 3, 11, 13);
                g.DrawRectangle(Pens.DimGray, 1, 3, 11, 13);
                g.FillRectangle(Brushes.SteelBlue, 1, 1, 11, 3);
                for (int i = 0; i < 3; i++) g.DrawLine(Pens.Gray, 3, 7 + i * 3, 10, 7 + i * 3);
                g.FillPolygon(Brushes.Orange, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
                g.DrawPolygon(Pens.Sienna, new Point[] { new Point(16, 4), new Point(17, 5), new Point(9, 15), new Point(7, 16), new Point(8, 13) });
            }
            return b;
        }
    }
}
