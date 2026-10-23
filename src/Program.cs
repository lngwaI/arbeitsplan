using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Arbeitsplan
{
    // Ein Eintrag = eine Zelle (Tag x Zeile): Ueberschrift, Inhalt, Einteilung.
    // Heading == null bedeutet: Standardueberschrift (Datum in Zeile 1).
    class Entry
    {
        public string Heading;
        public string Content = "";
        public string Assign = "";
    }

    // Prototyp-Datenhaltung: nur im Speicher. Spaeter durch Server-Client ersetzen (siehe CLAUDE.md).
    class DataStore
    {
        readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>();

        static string Key(DateTime day, int row) { return day.ToString("yyyy-MM-dd") + "|" + row; }

        public Entry Get(DateTime day, int row)
        {
            Entry e;
            items.TryGetValue(Key(day, row), out e);
            return e;
        }

        public Entry GetOrCreate(DateTime day, int row)
        {
            string k = Key(day, row);
            Entry e;
            if (!items.TryGetValue(k, out e)) { e = new Entry(); items[k] = e; }
            return e;
        }

        public int MaxRow(DateTime day)
        {
            int max = -1;
            for (int r = 0; r < 100; r++)
            {
                Entry e = Get(day, r);
                if (e != null && (e.Content.Length > 0 || e.Assign.Length > 0 || e.Heading != null)) max = r;
            }
            return max;
        }

        public static DataStore Sample(DateTime monday)
        {
            DataStore s = new DataStore();
            string[] contents = {
                "Raumreinigung im Laufe des Tages.\nProdukt XY Lauf Z.\nG2-F-1234-30, G2-F-1900-12",
                "Probenahme Charge 4711.\nEquipment: G2-F-1750-02",
                "Wartung Abfuellung (extern ab 08:00).",
                "Produkt AB Lauf 3.\nG2-F-2010-01",
                "Reinigung und Ruesten.",
                "Wochenendbetrieb: Ueberwachung Fermenter.",
                "Wochenendbetrieb: Ueberwachung Fermenter."
            };
            string[] assigns = {
                "F: ABCD EFGH IJKL\nS: MNOP QRST UVWX\nN: ",
                "F: abc def\nS: ghi jkl\nN: mno",
                "F: pqr stu\nS: \nN: ",
                "F: vwx yza\nS: bcd efg\nN: hij",
                "F: klm nop\nS: qrs tuv\nN: ",
                "F: wxy\nS: zab\nN: cde",
                "F: wxy\nS: zab\nN: cde"
            };
            for (int d = 0; d < 7; d++)
            {
                DateTime day = monday.AddDays(d);
                Entry e = s.GetOrCreate(day, 0);
                e.Content = contents[d].Replace("\n", "\r\n");
                e.Assign = assigns[d].Replace("\n", "\r\n");
            }
            Entry x = s.GetOrCreate(monday, 1);
            x.Heading = "Zusatz";
            x.Content = "Schulung GMP (Raum 2.14), 13:00";
            x.Assign = "F: alle";
            return s;
        }
    }

    class MainForm : Form
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");
        const int MinRows = 3;

        readonly DataStore store;
        DateTime start;     // erster angezeigter Tag (linke Spalte)
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
            Font = new Font("Segoe UI", 9f);

            start = MondayOf(DateTime.Today);
            store = DataStore.Sample(start);

            weekLabel.Dock = DockStyle.Top;
            weekLabel.Height = 34;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);

            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.Resize += delegate { ApplyRowHeights(); };

            bar.Dock = DockStyle.Bottom;
            bar.Height = 48;
            nav.AutoSize = true;
            nav.WrapContents = false;
            nav.Top = 6;
            nav.Controls.Add(MakeButton("«  Woche", delegate { Go(-7); }));
            nav.Controls.Add(MakeButton("‹  Tag", delegate { Go(-1); }));
            nav.Controls.Add(MakeButton("Heute", delegate { start = MondayOf(DateTime.Today); Rebuild(); }));
            nav.Controls.Add(MakeButton("Tag  ›", delegate { Go(1); }));
            nav.Controls.Add(MakeButton("Woche  »", delegate { Go(7); }));
            nav.Controls.Add(MakeButton("+ Zeile", delegate { rows++; Rebuild(); }));
            bar.Controls.Add(nav);
            bar.Resize += delegate { nav.Left = Math.Max(0, (bar.ClientSize.Width - nav.Width) / 2); };

            Controls.Add(scroll);
            Controls.Add(weekLabel);
            Controls.Add(bar);

            Rebuild();
            Shown += delegate { nav.Left = Math.Max(0, (bar.ClientSize.Width - nav.Width) / 2); };
        }

        static Button MakeButton(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.MinimumSize = new Size(90, 34);
            b.Margin = new Padding(4, 0, 4, 0);
            b.Click += click;
            return b;
        }

        static DateTime MondayOf(DateTime d)
        {
            int diff = ((int)d.DayOfWeek + 6) % 7;
            return d.Date.AddDays(-diff);
        }

        void Go(int days) { start = start.AddDays(days); Rebuild(); }

        static int WeekNo(DateTime d)
        {
            return De.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        // Baut die komplette Ansicht neu auf (bei jeder UI-Aenderung: Oeffnen, Heute, Pfeile).
        void Rebuild()
        {
            DateTime end = start.AddDays(6);
            weekLabel.Text = "KW " + WeekNo(start) + "   " + start.ToString("dd.MM.yyyy", De) + " – " + end.ToString("dd.MM.yyyy", De);

            for (int d = 0; d < 7; d++)
                rows = Math.Max(rows, store.MaxRow(start.AddDays(d)) + 2);

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
            for (int r = 0; r < rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 7; c++)
                    grid.Controls.Add(MakeCell(start.AddDays(c), r), c, r);

            scroll.Controls.Add(grid);
            ApplyRowHeights();
            scroll.ResumeLayout(true);
            ActiveControl = nav.Controls[2];   // "Heute", damit beim Start keine Zelle markiert ist
        }

        // Zeilen fuellen die verfuegbare Hoehe (mind. 170 px), danach scrollt die Ansicht.
        void ApplyRowHeights()
        {
            if (grid == null) return;
            int avail = scroll.ClientSize.Height - grid.Padding.Vertical - 4;
            int h = Math.Max(170, avail / rows);
            for (int r = 0; r < grid.RowStyles.Count; r++) grid.RowStyles[r].Height = h;
        }

        Control MakeCell(DateTime day, int row)
        {
            bool isToday = day.Date == DateTime.Today;
            bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            Entry e = store.Get(day, row);

            Color headBack = isToday ? Color.FromArgb(255, 224, 130) : (weekend ? Color.FromArgb(214, 224, 238) : Color.FromArgb(205, 218, 234));
            Color bodyBack = isToday ? Color.FromArgb(255, 250, 230) : (weekend ? Color.FromArgb(244, 246, 250) : Color.White);

            TableLayoutPanel cell = new TableLayoutPanel();
            cell.Dock = DockStyle.Fill;
            cell.ColumnCount = 1;
            cell.RowCount = 3;
            cell.Margin = new Padding(2);
            cell.BackColor = Color.FromArgb(150, 160, 175);
            cell.Padding = new Padding(1);
            cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            cell.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            cell.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            string defaultHeading = row == 0 ? day.ToString("dddd, dd.MM.yyyy", De) : "";
            TextBox head = MakeBox(bodyBack, false);
            head.AutoSize = false;
            head.BackColor = headBack;
            head.Font = new Font(Font, FontStyle.Bold);
            head.Text = (e != null && e.Heading != null) ? e.Heading : defaultHeading;
            head.TextChanged += delegate { store.GetOrCreate(day, row).Heading = head.Text; };

            TextBox content = MakeBox(bodyBack, true);
            content.Text = e != null ? e.Content : "";
            content.TextChanged += delegate { store.GetOrCreate(day, row).Content = content.Text; };

            TextBox assign = MakeBox(Color.FromArgb(bodyBack.R - 8, bodyBack.G - 8, bodyBack.B - 8), true);
            assign.Text = e != null ? e.Assign : "";
            assign.TextChanged += delegate { store.GetOrCreate(day, row).Assign = assign.Text; };

            cell.Controls.Add(head, 0, 0);
            cell.Controls.Add(content, 0, 1);
            cell.Controls.Add(assign, 0, 2);
            return cell;
        }

        static TextBox MakeBox(Color back, bool multiline)
        {
            TextBox t = new TextBox();
            t.Dock = DockStyle.Fill;
            t.Multiline = multiline;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = back;
            t.Margin = new Padding(0);
            t.AcceptsReturn = multiline;
            return t;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Testhilfe: "--shot <datei.png> [breite hoehe]" rendert die Ansicht in eine Bilddatei und beendet.
            if (args.Length >= 2 && args[0] == "--shot")
            {
                int w = args.Length >= 4 ? int.Parse(args[2]) : 1280;
                int h = args.Length >= 4 ? int.Parse(args[3]) : 720;
                MainForm f = new MainForm();
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(0, 0);
                f.ClientSize = new Size(w, h);
                f.Show();
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
