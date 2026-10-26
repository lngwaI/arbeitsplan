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
            View.Rows = rows;
            View.LoadCells(store, start);
            title.Text = "Woche 2: " + PlanView.WeekTitle(start) + " - Testansicht, nur lesend";
            View.Collapsed = View.DefaultFold();
            for (int i = 0; i < 7; i++) { days[i].Text = start.AddDays(i).ToString("ddd, dd.MM.yyyy", Ui.De); days[i].Today = start.AddDays(i) == DateTime.Today; days[i].Invalidate(); }
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
