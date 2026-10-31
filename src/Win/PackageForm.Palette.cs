using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Baustein der Puffer-Palette (grosse Vorschau): vorhandener Puffer-Schritt (Step) oder neuer ("Puffer mobil" / "Puffer stationär")
    sealed class PalItem { public PackageStep Step; public string NewName = ""; }

    // Puffer-Termine ohne Dialog setzen: Baustein aus der Palette auf einen Tag ziehen, Rechtsklick auf leeren Tag der Puffer-Zeile,
    // Rechtsklick auf einen Puffer-Block = Termin entfernen. Alles ueber dieselbe Tagesliste wie das Rhythmus-Fenster (SetPufferDays).
    partial class PackageForm
    {
        Panel palette;   // nur solange die grosse Vorschau offen ist

        void InitPalette()
        {
            strip.PaletteDropped += delegate(int day, object it) { PufferAt((PalItem)it, day); };
            strip.EmptyMenu += EmptyDayMenu;
        }

        static int TypIndex(PackageStep s) { List<string> v = VariantSet.Of(s.PufferVariant()); bool st = v.Contains("stationaer"); return v.Contains("mobil") && st ? 2 : (st ? 1 : 0); }

        // Palette neu aufbauen (Schritte kommen und gehen); y = naechste freie Zeile
        void BuildPalette()
        {
            if (palette == null) return;
            while (palette.Controls.Count > 0) { Control c = palette.Controls[0]; palette.Controls.RemoveAt(0); c.Dispose(); }
            int y = 4, w = palette.ClientSize.Width - 8;
            Label head = new Label(); head.Text = "Puffer-Bausteine – auf einen Tag ziehen"; head.SetBounds(4, y, w, Font.Height * 3); palette.Controls.Add(head); y += head.Height + 6;
            foreach (PackageStep s in Ordered())
                if (!s.IsEmpty && IsPuffer(s)) { AddChip(TitleOf(s), Theme.Current.BufColors[TypIndex(s)], new PalItem { Step = s }, ref y, w); }
            if (cat.Baustein_(Package.PufferMobilName) != null) AddChip("+ Puffer mobil", Theme.Current.BufColors[0], new PalItem { NewName = Package.PufferMobilName }, ref y, w);
            if (cat.Baustein_(Package.PufferStationaerName) != null) AddChip("+ Puffer stationär", Theme.Current.BufColors[1], new PalItem { NewName = Package.PufferStationaerName }, ref y, w);
        }

        void AddChip(string text, Color typ, PalItem item, ref int y, int w)
        {
            Label l = new Label(); l.Text = text; l.AutoEllipsis = true; l.TextAlign = ContentAlignment.MiddleLeft; l.Padding = new Padding(10, 0, 0, 0);
            l.BackColor = Color.FromArgb((typ.R + 3 * 255) / 4, (typ.G + 3 * 255) / 4, (typ.B + 3 * 255) / 4); l.Cursor = Cursors.Hand;
            l.SetBounds(4, y, w, Font.Height + 12); y += l.Height + 4;
            l.Paint += delegate(object o, PaintEventArgs e) { using (SolidBrush b = new SolidBrush(typ)) e.Graphics.FillRectangle(b, 0, 0, 4, l.Height); };
            l.MouseMove += delegate(object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) l.DoDragDrop(item, DragDropEffects.Copy); };
            tip.SetToolTip(l, "Auf einen Tag im Kalender ziehen: setzt dort einen Termin" + (item.Step == null ? " (neuer Puffer-Schritt)" : ""));
            palette.Controls.Add(l);
        }

        // Tage des Schritts im Plan (ohne Doppelte, sortiert)
        List<int> DaysOf(PackageStep s)
        {
            int si = steps.IndexOf(s); List<int> l = new List<int>();
            foreach (RunDate d in Plan()) if (d.Step == si && !l.Contains(d.Day)) l.Add(d.Day);
            l.Sort(); return l;
        }

        // Neuer Termin fuer einen Puffer (oder neuer Puffer-Schritt) an day
        void PufferAt(PalItem p, int day)
        {
            CommitUndo();
            PackageStep s = p.Step; List<int> days;
            if (s == null)
            {
                Baustein b = cat.Baustein_(p.NewName);
                if (b == null) return;
                s = FromBaustein(b); if (s.Row.Length == 0) s.Row = b.Row;
                steps.Add(s); days = new List<int>();
            }
            else days = DaysOf(s);
            if (days.Contains(day)) { Say("Dort liegt schon ein Termin von " + TitleOf(s) + "."); return; }
            days.Add(day); SetPufferDays(s, days);
            Say(""); AfterMove(s);
        }

        // Termin entfernen; der letzte bleibt (ohne Termin gaebe es keinen Puffer mehr)
        void PufferOff(PackageStep s, int day)
        {
            CommitUndo();
            List<int> days = DaysOf(s); days.Remove(day);
            if (days.Count == 0) { Say("Nicht entfernt: Mindestens ein Termin bleibt – den Puffer-Schritt löschen, wenn er nicht gebraucht wird."); return; }
            SetPufferDays(s, days);
            Say(""); AfterMove(s);
        }

        // Rechtsklick auf einen leeren Tag der Puffer-Zeile
        void EmptyDayMenu(int day, string row, Point at)
        {
            List<PackageStep> pu = Ordered().FindAll(delegate(PackageStep s) { return !s.IsEmpty && IsPuffer(s) && s.Row == row && row.Length > 0; });
            if (pu.Count == 0) return;
            stripMenu.Items.Clear();
            foreach (PackageStep s in pu) { PackageStep x = s; stripMenu.Items.Add("Termin für " + TitleOf(x) + " hier setzen", null, delegate { PufferAt(new PalItem { Step = x }, day); }); }
            foreach (string n in new[] { Package.PufferMobilName, Package.PufferStationaerName })
            {
                string nn = n;
                if (cat.Baustein_(nn) != null) stripMenu.Items.Add("+ " + nn + " hier", null, delegate { PufferAt(new PalItem { NewName = nn }, day); });
            }
            stripMenu.Show(strip, at);
        }
    }
}
