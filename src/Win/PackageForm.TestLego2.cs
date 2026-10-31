#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttests "Lego 2": Pfeil hoch/runter bei Hauptlauf neben "zu jedem Lauf"-Schritten (letzter Schritt geht hoch). Nur erfundene Daten.
    partial class PackageForm
    {
        public static string TestLego2(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            StringBuilder sb = new StringBuilder(); int seen = Package.Seen.Count;
            try { sb.AppendLine(Lego2Order(store, equip, start)); }
            catch (Exception ex) { sb.AppendLine("Lego2 Reihenfolge: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]); }
            sb.AppendLine(Lego2Palette(store, equip, start));
            sb.AppendLine(Lego2Camp(store, start));
            sb.AppendLine(Lego2Columns(store, equip, start));
            sb.AppendLine(Lego2Layout(store, equip, start, 1600, 1000)); sb.AppendLine(Lego2Layout(store, equip, start, 1024, 768));
            try { sb.AppendLine(Lego2Para()); }
            catch (Exception ex) { sb.AppendLine("Lego2 Absatz: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]); }
            if (Package.Seen.Count > seen) Package.Seen.RemoveRange(seen, Package.Seen.Count - seen);
            return sb.ToString();
        }

        // Jeder Schritt ausser Back-Up und erstem geht per MoveStep(-1) eine Position hoch; Hauptlauf neben Puffer (-1) tauscht die Seite
        static string Lego2Order(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            bool ok = true; string why = "";
            for (int variant = 0; variant < 3; variant++)
            {
                int n = variant == 0 ? Package.SampleCampaign("L2").Steps.Count : variant == 1 ? Package.DefaultCampaign(null).Steps.Count : Package.SampleBuffers("L2").Steps.Count;
                for (int k = 1; k < n; k++)
                {
                    Package p = variant == 0 ? Package.SampleCampaign("L2") : variant == 1 ? Package.DefaultCampaign(null) : Package.SampleBuffers("L2");
                    PackageForm f = new PackageForm(store, equip, start, p, true, true);
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, 0);
                    try
                    {
                        f.Show(); Application.DoEvents();
                        PackageStep s = f.Ordered()[k]; f.Bind(s);
                        if (s.IsBackup) { f.MoveStep(-1); ok &= W(ref why, variant * 100 + k, f.Ordered().IndexOf(s) == k && f.stripMsg.Text.Length > 0); continue; }
                        f.MoveStep(-1);
                        ok &= W(ref why, variant * 100 + k, f.Ordered().IndexOf(s) == k - 1);
                        if (s == f.StepsView().Head) { f.MoveStep(-1); f.MoveStep(1); f.MoveStep(1); ok &= W(ref why, variant * 100 + k + 50, f.Ordered().IndexOf(s) == k); }   // mehrere Nachbarn nacheinander, auch zurueck
                    }
                    finally { f.dirty = false; f.Close(); f.Dispose(); }
                }
            }
            return "Lego2 Reihenfolge: Pfeil hoch bewegt jeden Schritt außer Back-Up, Hauptlauf tauscht mit Puffer " + T(ok) + (ok ? "" : " [" + why + "]");
        }

        // Absatz (Leerzeile danach) im Plantext: Rundreise, Spans bleiben richtig versetzt, Leerzeile am Ende faellt weg, Notizen mit Leerzeile, Puffer-Text
        static string Lego2Para()
        {
            Package p = Package.SampleCampaign("L2a"); PackageStep lf = p.Steps[2];
            string xml0 = p.ToXml(); Entry e0 = lf.EntryFor(0, p.RunPrefix);
            string why = "";
            bool ok = W(ref why, 0, !xml0.Contains("absatz"));   // alte Dateien bleiben unveraendert
            int vs0 = e0.Content.IndexOf("(nur eins vorhanden)");
            lf.Equipment[6].ParaAfter = true;     // Leerzeile nach RT, danach VS mit kursiver Notiz
            Entry e1 = lf.EntryFor(0, p.RunPrefix);
            int vs1 = e1.Content.IndexOf("(nur eins vorhanden)");
            bool italic = e1.Spans.Exists(delegate(FormatSpan s) { return s.Italic && s.Start == vs1 && s.Length == "(nur eins vorhanden)".Length; });
            ok &= W(ref why, 1, vs0 > 0 && vs1 == vs0 + 1 && italic);
            ok &= W(ref why, 2, e1.Content.Contains("RT: H1-T-0112\n\nVS:") && e1.Content.Split('\n').Length == e0.Content.Split('\n').Length + 1);
            Package back = Package.FromXml(p.ToXml());
            ok &= W(ref why, 3, back.Steps[2].Equipment[6].ParaAfter && !back.Steps[2].Equipment[5].ParaAfter && back.ToXml() == p.ToXml() && p.ToXml().Contains("absatz=\"1\""));
            ok &= W(ref why, 4, back.Steps[2].EntryFor(0, p.RunPrefix).Content == e1.Content);
            PackageStep pk = p.Steps[0]; Entry p0 = pk.EntryFor(0, p.RunPrefix);
            pk.Equipment[pk.Equipment.Count - 1].ParaAfter = true;
            ok &= W(ref why, 5, pk.EntryFor(0, p.RunPrefix).Content == p0.Content);   // Leerzeile ganz am Ende faellt weg
            PackageStep nt = p.Steps[1]; nt.Note = "Eins\n\nZwei\n\n";
            Entry en = nt.EntryFor(0, p.RunPrefix);
            ok &= W(ref why, 6, nt.NoteTexts().Count == 3 && en.Content.EndsWith("Eins\n\nZwei") && Notes.NoteTexts(en).Count == 2);
            Package pb = Package.SampleBuffers("L2b"); PackageStep pu = pb.Steps[pb.Steps.Count - 1]; pu.Equipment[0].ParaAfter = true;
            ok &= W(ref why, 7, pu.BuffersFor(0, "")[0].Text.Contains("Tank 1\n\nMobT:"));
            Package pb2 = Package.FromXml(pb.ToXml()); ok &= W(ref why, 8, pb2.ToXml() == pb.ToXml());
            return "Lego2 Absatz: Rundreise, Spans versetzt, Ende ohne Leerzeile, Notizen und Puffer-Text mit Leerzeile " + T(ok) + (ok ? "" : " [" + why + "] " + e1.Content.Replace("\n", "|"));
        }

        // Kampagnenstart: Knopf "Puffer-Rhythmus…" gilt nur fuer die Kopie, die Paketdatei bleibt, der Hinweis "pufrhythmus" verschwindet
        static string Lego2Camp(IDataStore store, DateTime start)
        {
            Package p = Package.SampleBuffers("L2c"); PackageStep pu = p.Steps[p.Steps.Count - 1];
            pu.Bind = StepBind.Frei; pu.Repeat = RepeatMode.DayList; pu.DayList = "2, 3";   // deckt die Laeufe nicht ab
            string xml0 = p.ToXml(); bool was = CampaignForm.Quiet; CampaignForm.Quiet = true;
            CampaignForm cf = new CampaignForm(p, store, start, ApplyMode.FillEmpty);
            cf.StartPosition = FormStartPosition.Manual; cf.Location = new Point(-4000, 0);
            try
            {
                cf.Show(); Application.DoEvents();
                bool ok = cf.TestRhythmButton && P1Any(cf.Problems, "pufrhythmus");
                PufferRhythmForm rf = cf.TestBuildRhythm();
                try
                {
                    ok &= rf.Rows.Count == 1 && rf.Rows[0].Step == cf.Result.Steps[cf.Result.Steps.Count - 1] && rf.Rows[0].Days.SetEquals(new[] { 2, 3 });
                    rf.Rows[0].Days.Clear(); foreach (int d in new[] { 2, 8, 14, 20 }) rf.Rows[0].Days.Add(d);
                    cf.TestApplyRhythm(rf.Rows); Application.DoEvents();
                }
                finally { rf.Dispose(); }
                List<int> got = new List<int>(); PackageStep cs = cf.Result.Steps[cf.Result.Steps.Count - 1];
                foreach (RunDate d in cf.Schedule) if (cf.Result.Steps[d.Step] == cs) got.Add(d.Day);
                ok &= LegoJoin(got) == "2,8,14,20" && cs.DayList == "2, 8, 14, 20" && !P1Any(cf.Problems, "pufrhythmus") && p.ToXml() == xml0 && pu.DayList == "2, 3";
                return "Lego2 Kampagne: Puffer-Rhythmus nur auf der Kopie, Hinweis weg, Paketdatei unverändert " + T(ok);
            }
            catch (Exception ex) { return "Lego2 Kampagne: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { CampaignForm.Quiet = was; cf.Close(); cf.Dispose(); }
        }

        // Umschalter "Prüfung (n)" / "Im Plan": Standard zu, Sperre klappt Prüfung auf, gemerkt je Sitzung, Kalender hat danach die volle Breite
        static string Lego2Columns(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm.TestColumnPrefs(null); string why = "";
            PackageForm f = new PackageForm(store, equip, start, Package.DefaultCampaign(null), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, 0);
            PackageForm g = null;
            try
            {
                f.Show(); Application.DoEvents();
                bool ok = W(ref why, 0, !f.hintsCol.Visible && !f.pagerCol.Visible && f.tgHints.Text.StartsWith("Prüfung") && !f.tgHints.Checked && f.stripScroll.Width >= f.ClientSize.Width - 40);
                f.steps[2].Day = 2; f.Bind(f.steps[2]); Application.DoEvents();   // Lauf am Tag des Back-Ups = Sperre (packen-tag)
                ok &= W(ref why, 1, Package.HasBlock(f.StepsView().Check()) || f.hintList.Exists(delegate(Hint h) { return h.Blocks; }));
                ok &= W(ref why, 2, f.hintsCol.Visible && f.tgHints.Checked && f.tgHints.ForeColor == Color.Firebrick && f.tgHints.Text.Contains("("));
                f.tgHints.Checked = false; f.tgPlan.Checked = true; Application.DoEvents();   // von Hand: Prüfung zu, Im Plan auf
                ok &= W(ref why, 3, !f.hintsCol.Visible && f.pagerCol.Visible && f.blockPreview.Visible);
                g = new PackageForm(store, equip, start, Package.SampleCampaign("L2s2"), true, true);   // neues Fenster derselben Sitzung: gleiche Stellung
                g.StartPosition = FormStartPosition.Manual; g.Location = new Point(-4000, 0); g.Show(); Application.DoEvents();
                ok &= W(ref why, 4, !g.hintsCol.Visible && g.pagerCol.Visible && g.tgPlan.Checked);
                return "Lego2 Spalten: Prüfung/Im Plan zu, Sperre klappt auf, gemerkt je Sitzung " + T(ok) + (ok ? "" : " [" + why + "]");
            }
            catch (Exception ex) { return "Lego2 Spalten: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally
            {
                PackageForm.TestColumnPrefs(true);
                f.dirty = false; f.Close(); f.Dispose();
                if (g != null) { g.dirty = false; g.Close(); g.Dispose(); }
            }
        }

        // Anordnung bei 1600x1000 und 1024x768: Kopf hoechstens 1 bzw. 2 Zeilen, Kalender >= 3 Zeilen, Equipment ohne Scrollen, "Groß" sichtbar
        static string Lego2Layout(IDataStore store, IEquipmentStatusProvider equip, DateTime start, int cw, int ch)
        {
            string oldPrefs = PrefsFile; PrefsFile = Path.Combine(Path.GetTempPath(), "lego2_prefs_" + Guid.NewGuid().ToString("N") + ".ini");   // Standardhoehe, nicht die des Nutzers
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("L2l"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0); string why = "";
            try
            {
                f.ClientSize = new Size(cw, ch); f.Show(); Application.DoEvents();
                Rectangle wa = Screen.FromControl(f).WorkingArea; bool fits = f.ClientSize.Width >= cw - 4 && f.ClientSize.Height >= ch - 4 && wa.Height >= ch;
                PackageStep pk = f.steps[0]; f.Bind(pk); Application.DoEvents();   // Packen: 3 Equipment-Zeilen
                int rowH = f.headH + 4, maxRows = cw >= 1500 ? 1 : 2;
                bool ok = W(ref why, 0, f.headPanel.Height <= maxRows * rowH + 14 && f.lStatus.Visible && f.lStatus.Bottom <= f.headPanel.Height);
                ok &= W(ref why, 1, f.paneWrap.Height >= f.PaneMin && f.strip.Height >= 3 * (2 * f.Font.Height + 13) + f.Font.Height + 6 - 2);
                ok &= W(ref why, 2, f.bBig.Visible && f.bBig.Left >= 0 && f.bBig.Right <= f.stripBar.ClientSize.Width && f.bBig.Text == "Groß" && f.bRhythm.Right <= f.stripBar.ClientSize.Width);
                // gross: das ganze Equipment ohne Scrollen; 1024x768: der Equipment-Kopf und die erste Zeile liegen im sichtbaren Teil (der Rest scrollt im Detailbereich)
                ok &= W(ref why, 3, f.eqRows.Count == 3 && (ch >= 900 ? f.gEquip.Bottom <= f.scroll.ClientSize.Height + 2 : f.gEquip.Top + f.gEquip.ContentTop + f.eqRows[0].Height <= f.scroll.ClientSize.Height));
                f.Bind(f.steps[2]); Application.DoEvents();   // Lauf: 10 Zeilen, bei grossem Fenster ohne Scrollen
                if (fits && ch >= 900) ok &= W(ref why, 4, f.gEquip.Bottom <= f.scroll.ClientSize.Height + 2);
                ok &= W(ref why, 5, f.lv.Columns.Count == 3 && f.stripBar.Controls.Contains(f.tgHints) && f.tgHints.Right <= f.stripBar.ClientSize.Width);
                return "Lego2 Anordnung " + cw + "x" + ch + ": Kopf, Kalender, Equipment ohne Scrollen, Groß sichtbar " + T(ok) + (ok ? "" : " [" + why + " head=" + f.headPanel.Height + " strip=" + f.strip.Height + " body=" + f.body.Height + "/" + f.scroll.ClientSize.Height + " eq=" + f.gEquip.Bottom + "]") + (fits ? "" : " (Bildschirm kleiner)");
            }
            catch (Exception ex) { return "Lego2 Anordnung: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { PrefsFile = oldPrefs; f.dirty = false; f.Close(); f.Dispose(); }
        }

        // Palette der grossen Vorschau: Termin setzen/entfernen ueber die Tagesliste, letzter Termin bleibt, neuer Puffer, gleiche Tagesbreite, Rueckgaengig
        static string Lego2Palette(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleBuffers("L2p"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, 0); string why = "";
            try
            {
                f.Show(); Application.DoEvents();
                string xml0 = f.StepsView().ToXml(); int n0 = f.steps.Count;
                PackageStep pu = f.steps[n0 - 1];
                Form big = f.TestBig(true); Application.DoEvents();
                bool ok = W(ref why, 0, big != null && f.palette != null && f.palette.Controls.Count >= 4 && f.strip.EqualDays && f.palette.Parent == big);
                List<int> d0 = f.DaysOf(pu); int free = d0[d0.Count - 1] + 1;
                f.PufferAt(new PalItem { Step = pu }, free);
                List<int> d1 = f.DaysOf(pu);
                ok &= W(ref why, 1, pu.Repeat == RepeatMode.DayList && d1.Count == d0.Count + 1 && d1.Contains(free) && f.dirty);
                f.PufferAt(new PalItem { Step = pu }, free);
                ok &= W(ref why, 2, f.DaysOf(pu).Count == d1.Count && f.stripMsg.Text.Length > 0);
                f.PufferOff(pu, free);
                ok &= W(ref why, 3, LegoJoin(f.DaysOf(pu)) == LegoJoin(d0));
                for (int i = 1; i < d0.Count; i++) f.PufferOff(pu, d0[i]);
                f.PufferOff(pu, d0[0]);
                ok &= W(ref why, 4, f.DaysOf(pu).Count == 1 && f.stripMsg.Text.Contains("Mindestens ein Termin"));
                f.PufferAt(new PalItem { NewName = Package.PufferStationaerName }, 4);
                PackageStep ns = f.steps[f.steps.Count - 1];
                ok &= W(ref why, 5, f.steps.Count == n0 + 1 && ns.Variant.Contains("stationaer") && LegoJoin(f.DaysOf(ns)) == "4");
                Application.DoEvents();
                ok &= W(ref why, 6, f.palette.Controls.Count >= 5);   // neuer Schritt steht in der Palette
                f.TestBig(false); Application.DoEvents();
                ok &= W(ref why, 7, f.palette == null && !f.strip.EqualDays);
                while (f.UndoAt > 0 && f.steps.Count != n0) f.TestUndo(-1);
                while (f.UndoAt > 0 && f.StepsView().ToXml() != xml0) f.TestUndo(-1);
                ok &= W(ref why, 8, f.StepsView().ToXml() == xml0);   // Rueckgaengig stellt den Ausgangsstand her
                return "Lego2 Palette: Termin setzen/entfernen, letzter bleibt, neuer Puffer, Rückgängig " + T(ok) + (ok ? "" : " [" + why + "]");
            }
            catch (Exception ex) { return "Lego2 Palette: Ausnahme " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { f.TestBig(false); f.dirty = false; f.Close(); f.Dispose(); }
        }
    }
}
#endif
