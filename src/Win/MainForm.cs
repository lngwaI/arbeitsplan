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
        public TextBox Head, Assign;
        public RichTextBox Content;
        public PictureBox Pencil;
        public bool Editing;
        public int ContentNeed, AssignNeed, Height;
    }

    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        const int MinRows = 3, HeadH = 24, Pad = 4, Gap = 4, WM_SETREDRAW = 0xB;
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

        readonly IDataStore store;
        readonly ILockService locks = new MemoryLockService();
        readonly UserDb users = UserDb.Sample();
        readonly IEquipmentStatusProvider equip = new MockEquipmentProvider();
        readonly StatusColors colors;
        readonly User me;
        readonly List<Cell> cells = new List<Cell>();
        readonly Label weekLabel = new Label(), userLabel = new Label();
        readonly Panel dateBar = new Panel(), canvas = new Panel(), body = new Panel(), bar = new Panel();
        readonly Label[] dateLabels = new Label[7];
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Button btnRow, btnUsers = new Button();
        readonly Bitmap pencilIcon = MakePencil();
        DateTime start;
        int rows = MinRows;
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
            locks.Acquire(start.AddDays(1), 0, "Paula Werk");   // Mock: Dienstag Raum A ist "in Bearbeitung"

            me = users.FindByLogin(login);
            if (me == null)
            {
                // Annahme (Prototyp): unbekannter Windows-Nutzer = Produktion mit Windows-Namen.
                me = new User(); me.Login = login;
                UserDb.ApplyPackage(me.Rights, users.Package("Produktion"));
            }

            weekLabel.Dock = DockStyle.Top;
            weekLabel.Height = 34;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

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
            btnRow = AddNav("+ Zeile", delegate { if (me.Has(Perm.AddText) && EndEdit(true)) { rows++; Rebuild(); } });
            btnRow.Enabled = me.Has(Perm.AddText);
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
            Controls.Add(weekLabel);
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
            b.MinimumSize = new Size(90, 34);
            b.Margin = new Padding(4, 0, 4, 0);
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

        static string Norm(string s) { return s.Replace("\r\n", "\n").Replace("\n", "\r\n"); }

        // Baut die Ansicht neu auf (bei jeder UI-Aenderung: Oeffnen, Heute, Pfeile).
        void Rebuild()
        {
            DateTime end = start.AddDays(6);
            weekLabel.Text = "KW" + De.Calendar.GetWeekOfYear(start, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + " (" + start.ToString("dd.MM.yyyy", De) + " - " + end.ToString("dd.MM.yyyy", De) + ")";
            for (int c = 0; c < 7; c++)
            {
                DateTime d = start.AddDays(c);
                dateLabels[c].Text = d.ToString("ddd, dd.MM.yyyy", De);
                dateLabels[c].BackColor = d == DateTime.Today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            }
            for (int c = 0; c < 7; c++) rows = Math.Max(rows, store.MaxRow(start.AddDays(c)) + 2);

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

            Cell c = new Cell();
            c.Day = day; c.Row = row;
            c.Cur = store.Get(day, row) ?? new Entry();

            c.Panel = new Panel();
            c.Panel.BackColor = Color.FromArgb(150, 160, 175);
            c.Head = Box(headBack, false); c.Head.Font = new Font(Font, FontStyle.Bold); c.Head.Text = c.Cur.Heading ?? "";
            c.Content = new RichTextBox();
            c.Content.BorderStyle = BorderStyle.None; c.Content.BackColor = back; c.Content.ScrollBars = RichTextBoxScrollBars.None;
            c.Content.DetectUrls = false; c.Content.ReadOnly = true; c.Content.Text = c.Cur.Content;
            c.Assign = Box(Color.FromArgb(back.R - 8, back.G - 8, back.B - 8), true); c.Assign.Text = c.Cur.Assign;
            c.Pencil = new PictureBox();
            c.Pencil.Image = pencilIcon; c.Pencil.Size = new Size(18, 18); c.Pencil.BackColor = headBack; c.Pencil.Visible = false;

            // Farben erst nach dem Erzeugen des Handles und Laden des Textes wirksam -> verzoegert ausfuehren
            c.Content.HandleCreated += delegate { BeginInvoke(new MethodInvoker(delegate { if (!c.Content.IsDisposed) Colorize(c.Content); })); };
            EventHandler changed = delegate { if (c.Editing) Reflow(); };
            c.Head.TextChanged += changed;
            c.Assign.TextChanged += changed;
            c.Content.TextChanged += delegate { if (c.Editing) { Colorize(c.Content); Reflow(); } };

            EventHandler dbl = delegate { BeginEdit(c); };
            foreach (Control ctl in new Control[] { c.Panel, c.Head, c.Content, c.Assign, c.Pencil }) ctl.DoubleClick += dbl;

            c.Panel.Controls.Add(c.Head); c.Panel.Controls.Add(c.Pencil); c.Panel.Controls.Add(c.Content); c.Panel.Controls.Add(c.Assign);
            body.Controls.Add(c.Panel);
            return c;
        }

        static TextBox Box(Color back, bool multi)
        {
            TextBox t = new TextBox();
            t.Multiline = multi;
            t.AutoSize = false;
            t.AcceptsReturn = multi;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = back;
            t.ReadOnly = true;
            return t;
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

        // Positioniert alles neu: Zeilenhoehe = hoechster Block der Zeile; Einteilung nur so hoch wie noetig.
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

            int[] rowH = new int[rows];
            foreach (Cell k in cells)
            {
                k.ContentNeed = Need(k.Content.Text, iw, 3);
                k.AssignNeed = Need(k.Assign.Text, iw, 3);
                k.Height = 2 + HeadH + k.ContentNeed + k.AssignNeed;
                rowH[k.Row] = Math.Max(rowH[k.Row], k.Height);
            }
            int[] rowY = new int[rows];
            int y = Pad;
            for (int r = 0; r < rows; r++) { rowY[r] = y; y += rowH[r] + Gap; }

            foreach (Cell k in cells)
            {
                int col = (int)(k.Day - start).TotalDays, h = rowH[k.Row];
                k.Panel.SetBounds(Pad + col * (colW + Gap), rowY[k.Row], colW, h);
                k.Head.SetBounds(1, 1, k.Editing ? iw - 20 : iw, HeadH - 1);
                k.Pencil.Location = new Point(iw - 17, 4);
                k.Assign.SetBounds(1, h - 1 - k.AssignNeed, iw, k.AssignNeed);
                k.Content.SetBounds(1, 1 + HeadH, iw, h - 2 - HeadH - k.AssignNeed);
            }
            body.SetBounds(0, 0, cw, y + Pad);
        }

        // ---------- Statusfarben ----------

        void Colorize(RichTextBox rtb)
        {
            if (coloring) return;
            coloring = true;
            int s = rtb.SelectionStart, l = rtb.SelectionLength;
            if (rtb.IsHandleCreated) SendMessage(rtb.Handle, WM_SETREDRAW, 0, 0);
            rtb.SelectAll();
            rtb.SelectionColor = Color.Black;     // Annahme: Statusfarbe nur fuer die Nummer, Rest bleibt Standard
            foreach (Match m in EquipmentMatcher.Pattern.Matches(rtb.Text))
            {
                EquipmentInfo info = equip.Get(m.Value);
                if (info == null) continue;       // nur Nummern, die in der Tabelle existieren
                int rgb = colors.Get(info.Status);
                rtb.Select(m.Index, m.Length);
                rtb.SelectionColor = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            }
            rtb.Select(s, l);
            if (rtb.IsHandleCreated) { SendMessage(rtb.Handle, WM_SETREDRAW, 1, 0); rtb.Invalidate(); }
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
            SetEditing(c, true);
            c.Head.Focus();
        }

        void SetEditing(Cell c, bool on)
        {
            c.Editing = on;
            c.Head.ReadOnly = c.Content.ReadOnly = c.Assign.ReadOnly = !on;
            c.Pencil.Visible = on;
            c.Panel.BackColor = on ? Color.FromArgb(255, 140, 0) : Color.FromArgb(150, 160, 175);
            Reflow();
        }

        bool IsDirty(Cell c)
        {
            return c.Head.Text != (c.Cur.Heading ?? "") || Norm(c.Content.Text) != c.Cur.Content || Norm(c.Assign.Text) != c.Cur.Assign;
        }

        // false = Speichern fehlgeschlagen; Block bleibt im Bearbeitungsmodus, Text bleibt erhalten.
        bool EndEdit(bool save)
        {
            Cell c = editCell;
            if (c == null) return true;
            if (save && IsDirty(c))
            {
                Entry n = c.Cur.Clone();
                n.Heading = c.Head.Text; n.Content = Norm(c.Content.Text); n.Assign = Norm(c.Assign.Text);
                if (!TrySave(c, n)) return false;
                c.Cur = n;
            }
            else if (!save)
            {
                c.Head.Text = c.Cur.Heading ?? ""; c.Content.Text = c.Cur.Content; c.Assign.Text = c.Cur.Assign;
                Colorize(c.Content);
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
            catch (Exception ex)
            {
                MessageBox.Show("Konnte nicht gespeichert werden.\n" + ex.Message +
                    "\n\nIhre Änderung ist NICHT verloren und bleibt im Editor sichtbar.\nSpeichern später erneut versuchen (Strg+S) oder mit Esc verwerfen.",
                    "Speichern fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
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
