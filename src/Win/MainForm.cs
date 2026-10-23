using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

// Nur .NET-4.0-APIs und Systemschrift verwenden (Windows-XP-Kompatibilitaet).
namespace Arbeitsplan.Win
{
    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        const int MinRows = 3, MinRowHeight = 170;

        readonly IDataStore store;
        DateTime start;                       // erster angezeigter Tag (linke Spalte)
        int rows = MinRows;
        TableLayoutPanel grid;
        readonly Label weekLabel = new Label();
        readonly Panel scroll = new Panel();
        readonly Panel bar = new Panel();
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();

        public MainForm()
        {
            Text = "Arbeitsplan (Prototyp, lokale Testdaten)";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(900, 600);
            Font = SystemFonts.MessageBoxFont;    // Segoe UI ab Vista, Tahoma auf XP

            start = MondayOf(DateTime.Today);
            store = MemoryStore.Sample(start);

            weekLabel.Dock = DockStyle.Top;
            weekLabel.Height = 34;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.Resize += delegate { ApplyRowHeights(); };

            bar.Dock = DockStyle.Bottom;
            bar.Height = 48;
            nav.AutoSize = true;
            nav.WrapContents = false;
            nav.Top = 6;
            AddButton("«  Woche", delegate { Go(-7); });
            AddButton("‹  Tag", delegate { Go(-1); });
            AddButton("Heute", delegate { start = MondayOf(DateTime.Today); Rebuild(); });
            AddButton("Tag  ›", delegate { Go(1); });
            AddButton("Woche  »", delegate { Go(7); });
            AddButton("+ Zeile", delegate { rows++; Rebuild(); });
            bar.Controls.Add(nav);
            bar.Resize += delegate { nav.Left = Math.Max(0, (bar.ClientSize.Width - nav.Width) / 2); };
            Shown += delegate { nav.Left = Math.Max(0, (bar.ClientSize.Width - nav.Width) / 2); };

            Controls.Add(scroll);
            Controls.Add(weekLabel);
            Controls.Add(bar);
            Rebuild();
        }

        void AddButton(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.MinimumSize = new Size(90, 34);
            b.Margin = new Padding(4, 0, 4, 0);
            b.Click += click;
            nav.Controls.Add(b);
        }

        static DateTime MondayOf(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

        void Go(int days) { start = start.AddDays(days); Rebuild(); }

        // Baut die Ansicht neu auf (bei jeder UI-Aenderung: Oeffnen, Heute, Pfeile).
        void Rebuild()
        {
            weekLabel.Text = "KW " + De.Calendar.GetWeekOfYear(start, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + "   " + start.ToString("dd.MM.yyyy", De) + " – " + start.AddDays(6).ToString("dd.MM.yyyy", De);
            for (int d = 0; d < 7; d++) rows = Math.Max(rows, store.MaxRow(start.AddDays(d)) + 2);

            scroll.SuspendLayout();
            scroll.Controls.Clear();
            grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.ColumnCount = 7;
            grid.RowCount = rows;
            grid.Padding = new Padding(4);
            for (int c = 0; c < 7; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            for (int r = 0; r < rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, MinRowHeight));
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 7; c++) grid.Controls.Add(MakeCell(start.AddDays(c), r), c, r);
            scroll.Controls.Add(grid);
            ApplyRowHeights();
            scroll.ResumeLayout(true);
            ActiveControl = nav.Controls[2];      // "Heute": keine Zelle beim Start markiert
        }

        // Zeilen fuellen die verfuegbare Hoehe, mindestens MinRowHeight; darueber scrollt die Ansicht.
        void ApplyRowHeights()
        {
            if (grid == null) return;
            int h = Math.Max(MinRowHeight, (scroll.ClientSize.Height - grid.Padding.Vertical - 4) / rows);
            for (int r = 0; r < grid.RowStyles.Count; r++) grid.RowStyles[r].Height = h;
        }

        Control MakeCell(DateTime day, int row)
        {
            bool today = day.Date == DateTime.Today;
            bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            Color headBack = today ? Color.FromArgb(255, 224, 130) : Color.FromArgb(205, 218, 234);
            Color body = today ? Color.FromArgb(255, 250, 230) : (weekend ? Color.FromArgb(244, 246, 250) : Color.White);
            Color body2 = Color.FromArgb(body.R - 8, body.G - 8, body.B - 8);

            Entry e = store.Get(day, row) ?? new Entry();
            Entry cur = e;                        // lokaler Stand dieser Zelle inkl. Version

            TableLayoutPanel cell = new TableLayoutPanel();
            cell.Dock = DockStyle.Fill;
            cell.ColumnCount = 1;
            cell.RowCount = 3;
            cell.Margin = new Padding(2);
            cell.Padding = new Padding(1);
            cell.BackColor = Color.FromArgb(150, 160, 175);
            cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            cell.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            cell.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            TextBox head = Box(headBack, false);
            head.Font = new Font(Font, FontStyle.Bold);
            head.Text = cur.Heading ?? (row == 0 ? day.ToString("dddd, dd.MM.yyyy", De) : "");
            TextBox content = Box(body, true); content.Text = cur.Content;
            TextBox assign = Box(body2, true); assign.Text = cur.Assign;

            // Handler erst nach dem Befuellen anhaengen. Konfliktbehandlung (Save == false): spaeter.
            EventHandler save = delegate
            {
                cur.Heading = head.Text; cur.Content = content.Text; cur.Assign = assign.Text;
                store.Save(day, row, cur);
            };
            head.TextChanged += save; content.TextChanged += save; assign.TextChanged += save;

            cell.Controls.Add(head, 0, 0);
            cell.Controls.Add(content, 0, 1);
            cell.Controls.Add(assign, 0, 2);
            return cell;
        }

        static TextBox Box(Color back, bool multi)
        {
            TextBox t = new TextBox();
            t.Dock = DockStyle.Fill;
            t.Multiline = multi;
            t.AutoSize = false;
            t.AcceptsReturn = multi;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = back;
            t.Margin = new Padding(0);
            return t;
        }
    }
}
