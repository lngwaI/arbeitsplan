using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor: Freeze-Vorlage im Schritt-Detail, nur beim Baustein "Freeze / Thaw". Ohne Haken bleibt der Schritt normaler Text (z. B. Thaw).
    // Beim Uebernehmen entsteht je Termin ein Vorgang im Block (Package.FreezeFor); ohne Vorgabe-Uhrzeit "Zeit offen".
    partial class PackageForm
    {
        Section gFz;
        readonly CheckBox kFz = new CheckBox(), kFzTime = new CheckBox();
        readonly RadioButton rFzBag = new RadioButton(), rFzTank = new RadioButton();
        readonly NumericUpDown nFzDur = new NumericUpDown(), nFzGap = new NumericUpDown(), nFzCnt = new NumericUpDown();
        readonly TextBox tFzNames = new TextBox(), tFzTanks = new TextBox();
        readonly DateTimePicker dFzTime = new DateTimePicker();
        Label lFzArt, lFzDur, lFzGap, lFzCnt, lFzNames, lFzTanks, lFzHint;

        bool RowIsFreeze(PackageStep s) { return store.GetRows().Exists(delegate(RowInfo r) { return r.Freeze && string.Equals(r.Name, s.Row, StringComparison.OrdinalIgnoreCase); }); }
        static bool FreezeStep(PackageStep s) { return s.Freeze != null || RowInfo.IsFreezeName(s.Source); }

        void BuildFreeze()
        {
            gFz = Group("Freeze-Vorlage", "Legt zu jedem Termin einen Freeze-Vorgang im Plan an; Tag und Lauf stehen fest.", 500);
            gFz.TabIndex = 4;
            int lw = LabelW("Zeit zwischen zwei Zyklen:", "Zyklen-Namen:"), x = 8 + lw, w = 500 - 8 - x;
            kFz.Text = "Freeze-Vorgang im Plan anlegen (ohne Haken nur Text, z. B. Thaw)"; kFz.AutoSize = false; kFz.SetBounds(8, 0, 480, IH); kFz.Anchor = TLR;
            lFzArt = Lbl(gFz, "Art:", 8, lw);
            rFzBag.Text = "Bag-Freeze"; rFzBag.AutoSize = false; rFzBag.SetBounds(x, 0, 110, IH);
            rFzTank.Text = "Tank-Freeze"; rFzTank.AutoSize = false; rFzTank.SetBounds(x + 114, 0, 110, IH);
            lFzDur = Lbl(gFz, "Dauer (Stunden):", 8, lw); FzHours(nFzDur, x);
            lFzGap = Lbl(gFz, "Zeit zwischen zwei Zyklen:", 8, lw); FzHours(nFzGap, x);
            lFzCnt = Lbl(gFz, "Anzahl Zyklen:", 8, lw); nFzCnt.Minimum = 1; nFzCnt.Maximum = FreezeCalc.MaxCycles; nFzCnt.SetBounds(x, 0, 80, IH);
            lFzNames = Lbl(gFz, "Zyklen-Namen:", 8, lw); tFzNames.SetBounds(x, 0, w, IH);
            tip.SetToolTip(tFzNames, "Optional, mit | getrennt (z. B. A|B|C). Leer = Z1, Z2, Z3 …");
            lFzTanks = Lbl(gFz, "Tanks:", 8, lw); tFzTanks.SetBounds(x, 0, w, IH);
            tip.SetToolTip(tFzTanks, "Optional, mit Komma getrennt. Im Dialog „Freeze“ im Plan änderbar.");
            kFzTime.Text = "Uhrzeit vorgeben:"; kFzTime.AutoSize = false; kFzTime.SetBounds(8, 0, lw, IH);
            dFzTime.Format = DateTimePickerFormat.Custom; dFzTime.CustomFormat = "HH:mm"; dFzTime.ShowUpDown = true; dFzTime.SetBounds(x, 0, 80, IH); dFzTime.Value = DateTime.Today.AddHours(6);
            tip.SetToolTip(kFzTime, "Ohne Vorgabe steht der Vorgang mit „Zeit offen“ im Plan und unter den offenen Pflichten, bis die Startzeit im Dialog eingetragen ist. Mit Vorgabe bleibt die Zeit dort änderbar.");
            lFzHint = Lbl(gFz, "", 8, 480); lFzHint.ForeColor = Color.DarkOrange; lFzHint.AutoEllipsis = true;
            gFz.Controls.AddRange(new Control[] { kFz, rFzBag, rFzTank, nFzDur, nFzGap, nFzCnt, tFzNames, tFzTanks, kFzTime, dFzTime });
            Grow(tFzNames, tFzTanks, lFzHint);
            Ui.SimpleMenu(tFzNames); Ui.SimpleMenu(tFzTanks);

            kFz.CheckedChanged += delegate
            {
                if (!Edit()) return;
                cur.Freeze = kFz.Checked ? NewFreezeTemplate() : null;
                loading = true; BindFreeze(cur); loading = false;
                Changed();
            };
            EventHandler art = delegate { if (FzEdit()) { cur.Freeze.Art = rFzTank.Checked ? FreezeArt.Tank : FreezeArt.Bag; Changed(); } };
            rFzBag.CheckedChanged += art; rFzTank.CheckedChanged += art;
            nFzDur.ValueChanged += delegate { if (FzEdit()) { cur.Freeze.DauerMin = FzMins(nFzDur); Changed(); } };
            nFzGap.ValueChanged += delegate { if (FzEdit()) { cur.Freeze.AbstandMin = FzMins(nFzGap); Changed(); } };
            nFzCnt.ValueChanged += delegate { if (FzEdit()) { cur.Freeze.Cycles = (int)nFzCnt.Value; Changed(); } };
            tFzNames.TextChanged += delegate { if (FzEdit()) { cur.Freeze.CycleNames = tFzNames.Text.Trim(); Changed(); } };
            tFzTanks.TextChanged += delegate { if (FzEdit()) { cur.Freeze.Tanks = tFzTanks.Text.Trim(); Changed(); } };
            EventHandler time = delegate { if (FzEdit()) { dFzTime.Enabled = kFzTime.Checked; StoreFzTime(); Changed(); } };
            kFzTime.CheckedChanged += time; dFzTime.ValueChanged += time;
        }

        bool FzEdit() { return Edit() && cur.Freeze != null; }
        void FzHours(NumericUpDown n, int x) { n.DecimalPlaces = 2; n.Increment = 0.25m; n.Minimum = 0.25m; n.Maximum = 168; n.SetBounds(x, 0, 80, IH); }
        static int FzMins(NumericUpDown n) { return Math.Max(1, (int)Math.Round(n.Value * 60m)); }

        FreezeItem NewFreezeTemplate()
        {
            FreezeConfig c = cat.FreezeDefaults;
            FreezeItem f = new FreezeItem(); f.Id = "f1"; f.DauerMin = c.DauerMin; f.AbstandMin = c.AbstandMin; f.Cycles = c.Cycles; f.Offset2Min = c.Offset2Min; f.Offset3Min = c.Offset3Min; f.TimeOpen = true;
            return f;
        }

        // Vorgabe-Uhrzeit in der Vorlage: Start1to3[0] (Datum ohne Bedeutung)
        void StoreFzTime()
        {
            cur.Freeze.TimeOpen = !kFzTime.Checked;
            cur.Freeze.Start1to3[0] = kFzTime.Checked ? new DateTime(2000, 1, 1, dFzTime.Value.Hour, dFzTime.Value.Minute, 0) : DateTime.MinValue;
        }

        void BindFreeze(PackageStep s)
        {
            FreezeItem f = s.Freeze;
            kFz.Checked = f != null;
            if (f == null) return;
            rFzBag.Checked = f.Art == FreezeArt.Bag; rFzTank.Checked = f.Art == FreezeArt.Tank;
            nFzDur.Value = Math.Max(nFzDur.Minimum, Math.Min(nFzDur.Maximum, Math.Round(f.DauerMin / 60m, 2)));
            nFzGap.Value = Math.Max(nFzGap.Minimum, Math.Min(nFzGap.Maximum, Math.Round(f.AbstandMin / 60m, 2)));
            nFzCnt.Value = Math.Max(1, Math.Min(FreezeCalc.MaxCycles, f.Cycles));
            tFzNames.Text = f.CycleNames; tFzTanks.Text = f.Tanks;
            bool preset = f.Start1to3[0] != DateTime.MinValue;
            kFzTime.Checked = dFzTime.Enabled = preset;
            if (preset) dFzTime.Value = DateTime.Today + f.Start1to3[0].TimeOfDay;
        }

        int LayoutFreeze(PackageStep s, int w, int y)
        {
            bool show = FreezeStep(s);
            gFz.Visible = show;
            if (!show) return y;
            gFz.Width = w;
            bool on = s.Freeze != null, bag = on && s.Freeze.Art == FreezeArt.Bag;
            lFzHint.Text = on && s.Row.Length > 0 && !RowIsFreeze(s) ? "„" + s.Row + "“ ist keine Freeze-Zeile: Der Schritt kommt dort nur als Text in den Plan." : "";
            tip.SetToolTip(lFzHint, lFzHint.Text);
            int fy = gFz.ContentTop;
            fy = Row(fy, true, kFz); fy = Row(fy, lFzHint.Text.Length > 0, lFzHint);
            fy = Row(fy, on, lFzArt, rFzBag, rFzTank); fy = Row(fy, on, lFzDur, nFzDur);
            fy = Row(fy, bag, lFzGap, nFzGap); fy = Row(fy, bag, lFzCnt, nFzCnt); fy = Row(fy, bag, lFzNames, tFzNames);
            fy = Row(fy, on, lFzTanks, tFzTanks); fy = Row(fy, on, kFzTime, dFzTime);
            gFz.SetBounds(0, y, w, fy + 2);
            return gFz.Bottom + 8;
        }

#if TESTHOOKS
        public bool TestFzVisible(int step) { return FreezeStep(StepsView().Steps[step]); }
        public PackageStep TestFzOn(int step, bool on) { SelectStep(step); kFz.Checked = on; return cur; }
        public void TestFzSet(bool tank, decimal hours, string names, string tanks) { rFzTank.Checked = tank; rFzBag.Checked = !tank; nFzDur.Value = hours; tFzNames.Text = names; tFzTanks.Text = tanks; }
        public void TestFzClean() { dirty = false; }
        public void TestFzTime(int hour) { kFzTime.Checked = true; dFzTime.Value = DateTime.Today.AddHours(hour); }

        // Testfenster --form freezepkg: Kampagne mit Freeze-Schritt, Vorlage eingeschaltet
        public static PackageForm P5bForm(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            Package p = new Package(); p.Name = "Freeze-Test"; p.ProductCode = "1357";
            PackageStep lf = new PackageStep(); lf.Title = "Lauf"; lf.Source = "Lauf"; lf.Row = "Suite 1"; lf.Kind = StepKind.Run; lf.Day = 1; lf.Count = 3; lf.Interval = 6; lf.StartNo = 30; p.Steps.Add(lf);
            PackageStep fz = new PackageStep(); fz.Title = "Freeze / Thaw"; fz.Source = "Freeze / Thaw"; fz.Row = "Freeze / Thaw"; fz.Bind = StepBind.JeLauf; fz.RunOffset = 1; p.Steps.Add(fz);
            PackageForm f = new PackageForm(store, equip, start, p, true, true);
            f.Shown += delegate { f.Size = new Size(1500, 1000); f.TestFzOn(1, true); f.dirty = false; f.scroll.AutoScrollPosition = new Point(0, f.gFz.Top); };
            return f;
        }
#endif
    }
}
