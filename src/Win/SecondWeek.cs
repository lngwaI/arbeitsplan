#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Testansicht "2 Wochen": zweite, schreibgeschuetzte PlanView mit eigener Kopfzeile unter der ersten Woche.
    class SecondWeek
    {
        static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
        public readonly PlanView View;
        readonly Panel head = new Panel();
        readonly Label title = new Label();
        readonly DayLabel[] days = new DayLabel[7];
        readonly Control parent;

        public SecondWeek(Control parent, Font font, PlanView first, List<RowInfo> rows)
        {
            this.parent = parent;
            View = new PlanView(font);
            View.Equip = first.Equip; View.Colors = first.Colors; View.MyKuerzel = first.MyKuerzel; View.Rows = rows;   // keine Callbacks = nur lesen
            title.SetBounds(0, 0, 900, 20); title.Font = new Font(font, FontStyle.Bold);
            head.Height = 48; head.Controls.Add(title);
            for (int i = 0; i < 7; i++) { days[i] = new DayLabel(font); head.Controls.Add(days[i]); }
            parent.Controls.Add(head); parent.Controls.Add(View);
        }

        public void Remove() { parent.Controls.Remove(head); parent.Controls.Remove(View); View.Dispose(); head.Dispose(); }

        public void Load(IDataStore store, List<RowInfo> rows, DateTime start)
        {
            View.Start = start; View.Rows = rows; View.Cells.Clear();
            title.Text = "Woche 2: KW" + De.Calendar.GetWeekOfYear(start, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
                + " (" + start.ToString("dd.MM.yyyy", De) + " - " + start.AddDays(6).ToString("dd.MM.yyyy", De) + ") - Testansicht, nur lesend";
            bool[] fold = new bool[rows.Count];
            for (int r = 0; r < rows.Count; r++)
            {
                fold[r] = true;
                for (int c = 0; c < 7; c++)
                {
                    CellData d = new CellData();
                    d.Day = start.AddDays(c); d.Col = c; d.RowIndex = r;
                    d.Cur = store.Get(d.Day, rows[r].Id) ?? new Entry();
                    d.Content = d.Cur.Content; d.People = (string[])d.Cur.People.Clone(); d.Lkz = d.Cur.Lkz;
                    View.Cells.Add(d);
                    if (!d.Cur.IsEmpty || d.Cur.Lkz) fold[r] = false;
                }
            }
            View.Collapsed = fold;
            for (int i = 0; i < 7; i++) { days[i].Text = start.AddDays(i).ToString("ddd, dd.MM.yyyy", De); days[i].Today = start.AddDays(i) == DateTime.Today; days[i].Invalidate(); }
        }

        // unter die erste Woche setzen, gleiche Breite
        public void Place(PlanView first)
        {
            head.SetBounds(first.Left, first.Top + first.Height, first.Width, 48);
            View.SetBounds(first.Left, head.Bottom, first.Width, View.Height);
            View.MarkAllDirty(); View.Relayout();
            for (int i = 0; i < 7; i++) days[i].SetBounds(View.ColLeft(i), 22, View.ColW, 22);
        }
    }
}
#endif
