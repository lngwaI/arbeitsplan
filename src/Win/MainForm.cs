using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Arbeitsplan.Core;

// Nur .NET-4.0-APIs, C# 5 und Systemschrift (Windows-XP-Kompatibilitaet).
namespace Arbeitsplan.Win
{
    class Cell
    {
        public DateTime Day;
        public int Row;
        public Entry Cur;                 // zuletzt gespeicherter Stand inkl. Version
        public Panel Panel;
        public TextBox Head;              // Blockname (zeilenweit)
        public RichTextBox Content;
        public TextBox[] Lbl = new TextBox[3];       // F / S / N (zeilenweit)
        public RichTextBox[] Ppl = new RichTextBox[3];
        public PictureBox Pencil;
        public Button Minus, Plus;        // Blockhoehe (nur im Bearbeitungsmodus mit Recht)
        public bool Editing;
        public int Lines;                 // Arbeitswert Mindesthoehe waehrend der Bearbeitung
    }

    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        static readonly Regex Token = new Regex(@"[^\s,;/]+", RegexOptions.Compiled);
        const int HeadH = 24, Pad = 4, Gap = 4, LW = 24, WM_SETREDRAW = 0xB;
        static readonly Color[] ShiftLabel = { Color.FromArgb(255, 225, 130), Color.FromArgb(255, 190, 130), Color.FromArgb(150, 175, 225) };
        static readonly Color[] ShiftBack = { Color.FromArgb(255, 248, 225), Color.FromArgb(255, 240, 225), Color.FromArgb(232, 238, 250) };
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

        readonly IDataStore store;
        readonly ILockService locks = new MemoryLockService();
        readonly UserDb users = UserDb.Sample();
        readonly IEquipmentStatusProvider equip = new MockEquipmentProvider();
        readonly StatusColors colors;
        readonly User me;
        readonly List<Cell> cells = new List<Cell>();
        readonly List<RowInfo> rowInfo = new List<RowInfo>();
        readonly Label weekLabel = new Label(), userLabel = new Label(), searchLabel = new Label();
        readonly TextBox search = new TextBox();
        readonly Panel topBar = new Panel(), dateBar = new Panel(), canvas = new Panel(), body = new Panel(), bar = new Panel();
        readonly Label[] dateLabels = new Label[7];
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Button btnRow, btnUsers = new Button();
        readonly Bitmap pencilIcon = MakePencil();
        DateTime start;
        int rows;
        Cell editCell;
        bool inReflow, coloring;

        public MainForm(string login, bool failSaves)
        {
            Text = "Arbeitsplan (Prototyp, lokale Testdaten)";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = SystemFonts.MessageBoxFont;    // Segoe UI ab Vista, Tahoma auf XP
            KeyPreview = true;

            colors = StatusColors.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "statuscolors.ini"));
            start = MondayOf(DateTime.Today);
            MemoryStore ms = MemoryStore.Sample(start);
            ms.FailSaves = failSaves;
            store = ms;
            locks.Acquire(start.AddDays(1), 0, "Paula Werk");   // Mock: Dienstag Suite 1 ist "in Bearbeitung"

            me = users.FindByLogin(login);
            if (me == null)
            {
                // Annahme (Prototyp): unbekannter Windows-Nutzer = Produktion mit Windows-Namen.
                me = new User(); me.Login = login;
                UserDb.ApplyPackage(me.Rights, users.Package("Produktion"));
            }

            weekLabel.Dock = DockStyle.Fill;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
            searchLabel.Text = "Kürzel markieren:";
            searchLabel.AutoSize = true;
            searchLabel.Location = new Point(0, 9);
            search.Width = 90;
            search.Location = new Point(searchLabel.PreferredWidth + 6, 6);
            search.TextChanged += delegate { foreach (Cell c in cells) foreach (RichTextBox p in c.Ppl) Mark(p); };
            Panel searchPanel = new Panel();
            searchPanel.Dock = DockStyle.Right;
            searchPanel.Width = searchLabel.PreferredWidth + 6 + search.Width + 10;
            searchPanel.Controls.Add(searchLabel);
            searchPanel.Controls.Add(search);
            topBar.Dock = DockStyle.Top;
            topBar.Height = 34;
            topBar.Controls.Add(weekLabel);
            topBar.Controls.Add(searchPanel);

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
            canvas.Resize += delegate { Reflow(); };
            canvas.Controls.Add(body);

            bar.Dock = DockStyle.Bottom;
            bar.Height = 48;
            nav.AutoSize = true;
            nav.WrapContents = false;
            nav.Top = 6;
            AddNav("«  Woche", delegate { Go(-7); });
            AddNav("‹  Tag", delegate { Go(-1); });
            AddNav("Heute", delegate { Go(MondayOf(DateTime.Today) - start); });
            AddNav("Tag  ›", delegate { Go(1); });
            AddNav("Woche  »", delegate { Go(7); });
            btnRow = AddNav("+ Zeile", delegate { AddRow(); });
            btnRow.Enabled = me.Has(Perm.EditNames);
            userLabel.AutoSize = true;
            userLabel.Location = new Point(10, 16);
            userLabel.Text = "Angemeldet: " + me.Display + " (" + (users.PackageOf(me) != null ? users.PackageOf(me).Name : "benutzerdefiniert") + ")";
            btnUsers.Text = "Nutzer…";
            btnUsers.Size = new Size(90, 34);
            btnUsers.Visible = me.Has(Perm.UserCreate) || me.Has(Perm.UserEdit) || me.Has(Perm.UserDelete);
            btnUsers.Click += delegate { using (UserAdminForm f = new UserAdminForm(users, me)) f.ShowDialog(this); };
            bar.Controls.Add(nav);
            bar.Controls.Add(userLabel);
            bar.Controls.Add(btnUsers);
            bar.Resize += delegate { CenterBar(); };
            Shown += delegate { CenterBar(); };

            Controls.Add(canvas);
            Controls.Add(dateBar);
            Controls.Add(topBar);
            Controls.Add(bar);

            KeyDown += OnKey;
            FormClosing += OnClosing;
            Rebuild();
        }

        Button AddNav(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.MinimumSize = new Size(86, 34);
            b.Margin = new Padding(3, 0, 3, 0);
            b.Click += click;
            nav.Controls.Add(b);
            return b;
        }

        void CenterBar()
        {
            nav.Left = Math.Max(userLabel.Right + 10, (bar.ClientSize.Width - nav.Width) / 2);
            btnUsers.Location = new Point(bar.ClientSize.Width - btnUsers.Width - 10, 7);
        }

        static DateTime MondayOf(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

        void Go(int days) { Go(TimeSpan.FromDays(days)); }

        void Go(TimeSpan delta)
        {
            if (!EndEdit(true)) return;           // Speichern fehlgeschlagen: nicht wechseln, nichts geht verloren
            start = start.Add(delta);
            Rebuild();
        }

        void AddRow()
        {
            if (!me.Has(Perm.EditNames) || !EndEdit(true)) return;
            try { store.AddRow(); } catch (Exception ex) { ShowSaveError(ex); return; }
            Rebuild();
        }

        static string Norm(string s) { return s.Replace("\r\n", "\n").Replace("\n", "\r\n"); }

        // Baut die Ansicht neu auf (bei jeder UI-Aenderung: Oeffnen, Heute, Pfeile).
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
            rows = store.RowCount;
            rowInfo.Clear();
            for (int r = 0; r < rows; r++) rowInfo.Add(store.GetRow(r));

            body.SuspendLayout();
            foreach (Cell old in cells) old.Panel.Dispose();
            cells.Clear();
            body.Controls.Clear();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 7; c++) cells.Add(MakeCell(start.AddDays(c), r));
            body.ResumeLayout(false);
            Reflow();
            ActiveControl = nav.Controls[2];      // keine Zelle beim Start markiert
        }

        Cell MakeCell(DateTime day, int row)
        {
            bool today = day.Date == DateTime.Today;
            bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            Color headBack = today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            Color back = today ? Color.FromArgb(255, 250, 230) : (weekend ? Color.FromArgb(244, 246, 250) : Color.White);
            RowInfo info = rowInfo[row];

            Cell c = new Cell();
            c.Day = day; c.Row = row; c.Lines = info.MinLines;
            c.Cur = store.Get(day, row) ?? new Entry();

            c.Panel = new Panel();
            c.Panel.BackColor = Color.FromArgb(150, 160, 175);
            c.Head = Box(headBack); c.Head.Font = new Font(Font, FontStyle.Bold); c.Head.Text = info.Name;
            c.Pencil = new PictureBox();
            c.Pencil.Image = pencilIcon; c.Pencil.Size = new Size(18, 18); c.Pencil.BackColor = headBack; c.Pencil.Visible = false;
            c.Minus = SmallButton("-", headBack, delegate { if (c.Lines > 1) { c.Lines--; Reflow(); } });
            c.Plus = SmallButton("+", headBack, delegate { if (c.Lines < 30) { c.Lines++; Reflow(); } });

            c.Content = Rich(back); c.Content.Text = c.Cur.Content;
            Hook(c.Content, delegate { Colorize(c.Content); }, c);
            c.Panel.Controls.Add(c.Head); c.Panel.Controls.Add(c.Pencil); c.Panel.Controls.Add(c.Minus); c.Panel.Controls.Add(c.Plus);
            c.Panel.Controls.Add(c.Content);

            for (int s = 0; s < 3; s++)
            {
                int si = s;
                c.Lbl[s] = Box(ShiftLabel[s]); c.Lbl[s].Font = new Font(Font, FontStyle.Bold); c.Lbl[s].Text = info.Labels[s];
                c.Lbl[s].TextAlign = HorizontalAlignment.Center;
                c.Ppl[s] = Rich(ShiftBack[s]); c.Ppl[s].Text = c.Cur.People[s];
                RichTextBox box = c.Ppl[s];
                Hook(box, delegate { Mark(box); }, c);
                c.Lbl[s].TextChanged += delegate { if (c.Editing) Reflow(); };
                c.Panel.Controls.Add(c.Lbl[s]); c.Panel.Controls.Add(box);
            }

            c.Head.TextChanged += delegate { if (c.Editing) Reflow(); };
            EventHandler dbl = delegate { BeginEdit(c); };
            c.Panel.DoubleClick += dbl;
            foreach (Control ctl in c.Panel.Controls) if (!(ctl is Button)) ctl.DoubleClick += dbl;
            body.Controls.Add(c.Panel);
            return c;
        }

        // Dekoration (Farben/Markierung) erst nach Erzeugen des Handles und Laden des Textes wirksam -> verzoegert; danach bei Textaenderung im Bearbeitungsmodus.
        void Hook(RichTextBox rtb, MethodInvoker decorate, Cell c)
        {
            rtb.HandleCreated += delegate { BeginInvoke(new MethodInvoker(delegate { if (!rtb.IsDisposed) decorate(); })); };
            rtb.TextChanged += delegate { if (c.Editing) { decorate(); Reflow(); } };
        }

        static Button SmallButton(string text, Color back, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.Size = new Size(19, 19); b.FlatStyle = FlatStyle.Flat; b.BackColor = back;
            b.Font = new Font("Arial", 9f, FontStyle.Bold); b.UseCompatibleTextRendering = true;
            b.Padding = new Padding(0); b.Visible = false; b.TabStop = false;
            b.Click += click;
            return b;
        }

        static TextBox Box(Color back)
        {
            TextBox t = new TextBox();
            t.AutoSize = false;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = back;
            t.ReadOnly = true;
            return t;
        }

        static RichTextBox Rich(Color back)
        {
            RichTextBox r = new RichTextBox();
            r.BorderStyle = BorderStyle.None; r.BackColor = back; r.ScrollBars = RichTextBoxScrollBars.None;
            r.DetectUrls = false; r.ReadOnly = true;
            return r;
        }

        // Hoehe, die ein Text bei gegebener Breite braucht (mind. minLines Zeilen). Verhindert Abschneiden.
        int Need(string text, int width, int minLines)
        {
            string t = Norm(text);
            if (t.Length == 0 || t.EndsWith("\n")) t += " ";
            Size s = TextRenderer.MeasureText(t, Font, new Size(Math.Max(10, width - 10), 100000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            return Math.Max(minLines * Font.Height, s.Height) + 6;
        }

        // Positioniert alles neu. Je Zeile sind Inhaltsfeld und jedes Schichtfeld (F/S/N) in ALLEN Spalten gleich hoch
        // (Maximum der Spalten), jeweils nur so hoch wie noetig.
        void Reflow()
        {
            if (inReflow || cells.Count == 0) return;
            inReflow = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int cw = canvas.ClientSize.Width;
                    LayoutAll(cw);
                    if (canvas.ClientSize.Width == cw) break;   // Scrollbalken kann die Breite aendern
                }
            }
            finally { inReflow = false; }
        }

        void LayoutAll(int cw)
        {
            int colW = Math.Max(60, (cw - 2 * Pad - 6 * Gap) / 7);
            int iw = colW - 2;
            for (int c = 0; c < 7; c++) dateLabels[c].SetBounds(Pad + c * (colW + Gap), 2, colW, 22);

            int[] contentH = new int[rows];
            int[][] shiftH = new int[rows][];
            for (int r = 0; r < rows; r++)
            {
                int lines = (editCell != null && editCell.Row == r) ? editCell.Lines : rowInfo[r].MinLines;
                contentH[r] = lines * Font.Height + 6;
                shiftH[r] = new int[] { Need("", iw - LW, 1), Need("", iw - LW, 1), Need("", iw - LW, 1) };
            }
            foreach (Cell k in cells)
            {
                contentH[k.Row] = Math.Max(contentH[k.Row], Need(k.Content.Text, iw, 1));
                for (int s = 0; s < 3; s++) shiftH[k.Row][s] = Math.Max(shiftH[k.Row][s], Need(k.Ppl[s].Text, iw - LW, 1));
            }

            int[] rowY = new int[rows], rowH = new int[rows];
            int y = Pad;
            for (int r = 0; r < rows; r++)
            {
                rowH[r] = 1 + HeadH + contentH[r] + 3 + shiftH[r][0] + shiftH[r][1] + shiftH[r][2] + 1;
                rowY[r] = y; y += rowH[r] + Gap;
            }

            foreach (Cell k in cells)
            {
                int col = (int)(k.Day - start).TotalDays, r = k.Row;
                k.Panel.SetBounds(Pad + col * (colW + Gap), rowY[r], colW, rowH[r]);
                bool canH = k.Editing && me.Has(Perm.EditHeight);
                int right = iw - (k.Editing ? 19 : 0);
                k.Pencil.Location = new Point(iw - 17, 4);
                k.Plus.Location = new Point(right - 19, 3); k.Minus.Location = new Point(right - 39, 3);
                k.Plus.Visible = k.Minus.Visible = canH;
                k.Head.SetBounds(1, 1, k.Editing ? iw - 19 - (canH ? 40 : 0) : iw, HeadH - 1);
                k.Content.SetBounds(1, 1 + HeadH, iw, contentH[r]);
                int sy = 1 + HeadH + contentH[r] + 1;
                for (int s = 0; s < 3; s++)
                {
                    k.Lbl[s].SetBounds(1, sy, LW, shiftH[r][s]);
                    k.Ppl[s].SetBounds(1 + LW, sy, iw - LW, shiftH[r][s]);
                    sy += shiftH[r][s];
                }
            }
            body.SetBounds(0, 0, cw, y + Pad);
        }

        // ---------- Farben / Markierung ----------

        // Statusfarbe nur fuer erkannte Equipment-Nummern, die in der Tabelle existieren; Rest bleibt Standard.
        void Colorize(RichTextBox rtb)
        {
            Decorate(rtb, delegate
            {
                rtb.SelectAll();
                rtb.SelectionColor = Color.Black;
                foreach (Match m in EquipmentMatcher.Pattern.Matches(rtb.Text))
                {
                    EquipmentInfo info = equip.Get(m.Value);
                    if (info == null) continue;
                    int rgb = colors.Get(info.Status);
                    rtb.Select(m.Index, m.Length);
                    rtb.SelectionColor = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                }
            });
        }

        // Eigenes Kuerzel gruen, Treffer des Suchfelds gelb hinterlegt (Uebersicht bei vielen Mitarbeitern).
        void Mark(RichTextBox rtb)
        {
            string q = search.Text.Trim();
            Decorate(rtb, delegate
            {
                rtb.SelectAll();
                rtb.SelectionBackColor = rtb.BackColor;
                foreach (Match m in Token.Matches(rtb.Text))
                {
                    bool hit = q.Length > 0 && m.Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool mine = me.Kuerzel.Length > 0 && string.Equals(m.Value, me.Kuerzel, StringComparison.OrdinalIgnoreCase);
                    if (!hit && !mine) continue;
                    rtb.Select(m.Index, m.Length);
                    rtb.SelectionBackColor = hit ? Color.Yellow : Color.FromArgb(170, 235, 170);
                }
            });
        }

        void Decorate(RichTextBox rtb, MethodInvoker work)
        {
            if (coloring || !rtb.IsHandleCreated) return;
            coloring = true;
            int s = rtb.SelectionStart, l = rtb.SelectionLength;
            SendMessage(rtb.Handle, WM_SETREDRAW, 0, 0);
            work();
            rtb.Select(s, l);
            SendMessage(rtb.Handle, WM_SETREDRAW, 1, 0);
            rtb.Invalidate();
            coloring = false;
        }

        // ---------- Bearbeitungsmodus (Soft-Lock) ----------

        public void BeginEdit(Cell c)
        {
            if (editCell == c) return;
            bool allowed = me.Has(Perm.EditText) || (c.Cur.IsEmpty && me.Has(Perm.AddText));
            if (!allowed) { MessageBox.Show("Keine Berechtigung zum Bearbeiten.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!EndEdit(true)) return;

            string holder = locks.Holder(c.Day, c.Row, me.Display);
            if (holder != null)
            {
                string title = c.Head.Text.Length > 0 ? c.Head.Text : "Block " + (c.Row + 1);
                MessageBox.Show("Das Fenster " + title + " wird gerade von " + holder + " bearbeitet.", "Arbeitsplan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            locks.Acquire(c.Day, c.Row, me.Display);   // Soft-Lock: danach trotzdem bearbeiten und speichern
            editCell = c;
            c.Lines = rowInfo[c.Row].MinLines;
            SetEditing(c, true);
            c.Content.Focus();
        }

        // Inhalt/Schichten: wer bearbeiten darf. Blockname und F/S/N nur mit eigenem Recht.
        void SetEditing(Cell c, bool on)
        {
            c.Editing = on;
            bool names = on && me.Has(Perm.EditNames);
            c.Head.ReadOnly = !names;
            foreach (TextBox l in c.Lbl) l.ReadOnly = !names;
            c.Content.ReadOnly = !on;
            foreach (RichTextBox p in c.Ppl) p.ReadOnly = !on;
            c.Pencil.Visible = on;
            c.Panel.BackColor = on ? Color.FromArgb(255, 140, 0) : Color.FromArgb(150, 160, 175);
            Reflow();
        }

        RowInfo WorkRow(Cell c)
        {
            RowInfo r = rowInfo[c.Row].Clone();
            if (me.Has(Perm.EditNames)) { r.Name = c.Head.Text; for (int s = 0; s < 3; s++) r.Labels[s] = c.Lbl[s].Text; }
            if (me.Has(Perm.EditHeight)) r.MinLines = c.Lines;
            return r;
        }

        bool EntryDirty(Cell c)
        {
            if (Norm(c.Content.Text) != c.Cur.Content) return true;
            for (int s = 0; s < 3; s++) if (Norm(c.Ppl[s].Text) != c.Cur.People[s]) return true;
            return false;
        }

        bool IsDirty(Cell c) { return EntryDirty(c) || !WorkRow(c).SameAs(rowInfo[c.Row]); }

        // false = Speichern fehlgeschlagen; Block bleibt im Bearbeitungsmodus, Text bleibt erhalten.
        bool EndEdit(bool save)
        {
            Cell c = editCell;
            if (c == null) return true;
            if (save)
            {
                if (EntryDirty(c))
                {
                    Entry n = c.Cur.Clone();
                    n.Content = Norm(c.Content.Text);
                    for (int s = 0; s < 3; s++) n.People[s] = Norm(c.Ppl[s].Text);
                    if (!TrySave(c, n)) return false;
                    c.Cur = n;
                }
                RowInfo wr = WorkRow(c);
                if (!wr.SameAs(rowInfo[c.Row]))
                {
                    try { store.SaveRow(c.Row, wr); }
                    catch (Exception ex) { ShowSaveError(ex); return false; }
                    rowInfo[c.Row] = wr;
                    foreach (Cell k in cells)       // Name/Label/Hoehe gelten fuer die ganze Zeile
                        if (k.Row == c.Row && k != c)
                        {
                            k.Head.Text = wr.Name; k.Lines = wr.MinLines;
                            for (int s = 0; s < 3; s++) k.Lbl[s].Text = wr.Labels[s];
                        }
                }
            }
            else
            {
                RowInfo ri = rowInfo[c.Row];
                c.Head.Text = ri.Name; c.Lines = ri.MinLines;
                for (int s = 0; s < 3; s++) { c.Lbl[s].Text = ri.Labels[s]; c.Ppl[s].Text = c.Cur.People[s]; }
                c.Content.Text = c.Cur.Content;
            }
            locks.Release(c.Day, c.Row, me.Display);
            editCell = null;
            SetEditing(c, false);
            return true;
        }

        bool TrySave(Cell c, Entry n)
        {
            try
            {
                if (store.Save(c.Day, c.Row, n)) return true;
                Entry latest = store.Get(c.Day, c.Row);
                if (MessageBox.Show("Der Block wurde inzwischen von jemand anderem geändert.\n\nJa = meine Fassung speichern (überschreibt die andere)\nNein = im Editor bleiben",
                    "Konflikt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                n.Version = latest != null ? latest.Version : 0;
                if (store.Save(c.Day, c.Row, n)) return true;
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

        public Form TestUsers()
        {
            UserAdminForm u = new UserAdminForm(users, me);
            u.StartPosition = FormStartPosition.Manual; u.Location = new Point(0, 0); u.TopMost = true;
            u.Show();
            return u;
        }

        public void TestEdit(int col, int row)
        {
            foreach (Cell c in cells) if (c.Row == row && (int)(c.Day - start).TotalDays == col) BeginEdit(c);
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
