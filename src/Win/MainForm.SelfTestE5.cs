#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Text;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests E5/E6 (Kalender-Vorschau im Paket-Editor): Core hier (Move/Shift/Zuruecksetzen, Hauptlauf mit JeLauf-Folgern, Schnappschuss-XML),
    // Fenster in PackageForm.TestE5.cs (Ziehen, Umschalt, Ablehnung, feste Laeufe, Rueckgaengig/Wiederholen, Zoom, "nur Auswahl").
    partial class MainForm
    {
        static string DaysOf(PackageStep s) { return string.Join(",", s.Days().ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()); }

        void SelfTestE5(StringBuilder sb)
        {
            try { E5Core(sb); } catch (Exception ex) { sb.AppendLine("E5 Kern: Ausnahme " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine(PackageForm.TestE5(new MemoryStore(), equip, start));
        }

        void E5Core(StringBuilder sb)
        {
            Package p = Package.SampleCampaign("E5");
            PackageStep lauf = p.Steps[2], misch = p.Steps[3];
            string base0 = DaysOf(lauf);
            // Move ab Lauf 2 (Index 1): dieser und alle folgenden; Lauf 1 bleibt; Schloss (Pinned) nur am verschobenen Lauf
            bool a = p.DragRun(lauf, 1, 2, false) && DaysOf(lauf) == "3,11,17,23" && lauf.Move.Count == 1 && lauf.Shift.Count == 0 && lauf.Pinned(1) && !lauf.Pinned(0) && !lauf.Pinned(2);
            List<RunDate> sch = p.Schedule();
            RunDate m1 = sch.Find(delegate(RunDate d) { return d.Step == 3 && d.Run == 1; }), m0 = sch.Find(delegate(RunDate d) { return d.Step == 3 && d.Run == 0; });
            bool follow = m0 != null && m1 != null && m0.Day == 3 + misch.RunOffset && m1.Day == 11 + misch.RunOffset;   // JeLauf-Schritt folgt dem Hauptlauf
            // Umschalt: nur dieser Lauf (Shift), die anderen bleiben
            bool b = p.DragRun(lauf, 3, -1, true) && DaysOf(lauf) == "3,11,17,22" && lauf.Shift.Count == 1 && lauf.Shifted(3) && lauf.Pinned(3);
            // zweiter Zug am selben Lauf addiert
            bool c = p.DragRun(lauf, 1, 1, false) && DaysOf(lauf) == "3,12,18,23" && lauf.Move[1] == 3;
            // Rundreise: Datei-XML und Klon behalten Move/Shift
            Package back = Package.FromXml(p.ToXml());
            bool rt = DaysOf(back.Steps[2]) == DaysOf(lauf) && back.Steps[2].Move.Count == 1 && back.Steps[2].Shift.Count == 1 && back.ToXml() == p.ToXml() && DaysOf(p.Clone().Steps[2]) == DaysOf(lauf);
            // Zuruecksetzen: ein Lauf, alle
            bool d1 = lauf.ResetRun(3) && DaysOf(lauf) == "3,12,18,24" && !lauf.ResetRun(3) && lauf.ResetRun(1) && DaysOf(lauf) == base0 && !lauf.Pinned(1);
            p.DragRun(lauf, 2, 1, false); p.DragRun(lauf, 1, 1, true);
            bool d2 = lauf.Pinned(1) && lauf.Pinned(2) && lauf.ResetMoves() && DaysOf(lauf) == base0 && !lauf.ResetMoves();
            // Grenzen: vor Tag 1, erster Lauf = ganzer Schritt, einzelner Lauf; JeLauf nie einzeln
            bool e = !p.DragRun(lauf, 1, -20, false) && DaysOf(lauf) == base0 && p.DragRun(lauf, 0, 1, false) && DaysOf(lauf) == "4,10,16,22" && lauf.Move.Count == 0 && lauf.Shift.Count == 0;
            bool f = !p.DragRun(misch, 1, 1, true) && misch.Shift.Count == 0 && p.DragRun(misch, 1, 1, false) && misch.RunOffset == 2;
            sb.AppendLine("E5 Kern: Move ab Lauf " + K(a) + " JeLauf folgt " + K(follow) + " Shift nur dieser " + K(b) + " addiert " + K(c) + " XML-Rundreise " + K(rt)
                + " Zurücksetzen " + K(d1 && d2) + " Grenzen " + K(e) + " JeLauf nur ganz " + K(f));
        }
    }
}
#endif
