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
    // Selbsttests E2: Kennung Start +1, Ausfall = Luecke, Ueberschreiben/Aussetzen/Zuruecksetzen (Tabelle im Paket-Editor),
    // XML-Rundweg, Katalog-Rundweg der Notiz-Vorlage, Vorlage einfuegen, Notizen und Kennungszeile im Plantext.
    partial class MainForm
    {
        static string K(bool b) { return b ? "OK" : "FEHLER"; }

        void SelfTestE2(StringBuilder sb)
        {
            string tmp = Path.GetTempFileName();
            try
            {
                // Katalog-Rundweg: Notiz-Vorlage (mehrzeilig) in der Sicherung
                MemoryStore ms = new MemoryStore(); StoreData d = ms.Snapshot();
                Baustein pb = new Baustein(); pb.Name = "Puffer E2"; pb.Row = "Puffer"; pb.NoteTemplate = "Schläuche DN __ · Dichtungen DN __ · Tüllen DN __\nZweite Zeile";
                d.Catalog.Bausteine.Add(pb);
                Backup.Write(d, tmp, Backup.KeyOf(ms));
                StoreData back = Backup.Read(tmp, Backup.KeyOf(ms));
                Baustein rb = back.Catalog.Baustein_("Puffer E2");
                bool okCat = rb != null && rb.NoteTemplate == pb.NoteTemplate && d.Catalog.Clone().Baustein_("Puffer E2").NoteTemplate == pb.NoteTemplate;
                Baustein plain = new Baustein(); plain.Name = "Ohne Vorlage";
                okCat &= plain.NoteTemplate == "";

                // Paket-XML: Start + Ueberschreibungen + Ausfall
                Package p = new Package(); p.ProductCode = "1357"; p.RunPrefix = "1357";
                PackageStep h = new PackageStep(); h.Title = "Lauf"; h.Row = "Suite 1"; h.Day = 1; h.Kind = StepKind.Run; h.StartNo = 30; h.RunCount = 5; h.Interval = 2;
                h.IdentStart = "PA 40010001"; h.SetSkip(1, true); h.IdentOver[3] = "PA 40010099"; h.IdentOver[4] = PackageStep.IdentOff; h.Note = "Eins\nZwei";
                p.Steps.Add(h);
                bool okAuto = h.IdentFor(0) == "PA 40010001" && h.IdentFor(1) == "PA 40010002" && h.IdentFor(2) == "PA 40010003" && h.IdentFor(3) == "PA 40010099" && h.IdentFor(4) == "";
                p.Save(tmp); Package pl = Package.Load(tmp); Package.Seen.Remove(pl);
                PackageStep hl = pl.Steps[0];
                bool okXml = hl.IdentStart == "PA 40010001" && hl.Skips(1) && !hl.Skips(0) && hl.IdentFor(2) == "PA 40010003" && hl.IdentFor(3) == "PA 40010099" && hl.IdentFor(4) == "" && hl.IdentOver.Count == 2;
                Entry e = h.EntryFor(2, p.Prefix);
                string[] lines = e.Content.Split('\n');
                bool okOut = lines.Length == 4 && lines[1] == "Eins" && lines[2] == "Zwei" && lines[3] == "PA 40010003" && Notes.NoteTexts(e).Count == 2
                    && e.Spans.Exists(delegate(FormatSpan s) { return s.Start == e.Content.Length - 11 && s.Length == 11; });
                sb.AppendLine("E2 Kennung: Start+1=" + K(okAuto) + " Ausfall/Aussetzen im XML=" + K(okXml) + " Notizen+Kennungszeile=" + K(okOut) + " Katalog NoteTemplate=" + K(okCat));
            }
            catch (Exception ex) { sb.AppendLine("E2 Kennung: FEHLER " + ex.GetType().Name + ": " + ex.Message); }
            finally { try { File.Delete(tmp); } catch { } }
            sb.AppendLine(PackageForm.TestE2(store, equip, start));
        }
    }

    partial class PackageForm
    {
        public static string TestE2(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("E2"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep lauf = f.pk.Head; int n = f.pk.RunCountOf(lauf);
                lauf.IdentOver.Clear(); lauf.IdentStart = ""; lauf.Ident = "";
                f.Bind(lauf); f.TestMore(); Application.DoEvents();
                string dbg = "vis=" + f.gridId.Visible + " rows=" + f.gridId.Rows.Count + " n=" + n;
                bool okTab = f.gridId.Visible && f.gridId.Columns.Count == 5 && f.gridId.Columns[ColSkip].HeaderText == "fällt aus" && f.gridId.Rows.Count == n && n >= 4;
                f.tIdent.Text = "PA 40010001"; Application.DoEvents();
                okTab &= lauf.IdentStart == "PA 40010001" && Cell(f, 0) == "PA 40010001" && Cell(f, 3) == "PA 40010004" && !f.HasHint("Endziffern");
                // Ausfall = Luecke: Lauf 2 faellt aus (Kennung bleibt reserviert, Lauf 3 behaelt PA ...03)
                dbg += " c0=" + Cell(f, 0) + " c3=" + Cell(f, 3) + " hint=" + f.HasHint("Endziffern");
                f.gridId[ColSkip, 1].Value = true; Application.DoEvents();
                dbg += " skip=" + lauf.Skips(1) + " c1=" + Cell(f, 1) + " fc=" + f.gridId[ColId, 1].Style.ForeColor + " fnt=" + (f.gridId[ColNo, 1].Style.Font != null) + " end=" + lauf.EntryFor(2, f.prefix).Content.Replace("\n", "|");
                bool okSkip = lauf.Skips(1) && Cell(f, 1) == "PA 40010002" && Cell(f, 2) == "PA 40010003" && f.gridId[ColId, 1].Style.ForeColor == Section.HelpColor
                    && f.gridId[ColNo, 1].Style.Font != null && (f.gridId[ColNo, 1].Style.Font.Strikeout) && lauf.EntryFor(2, f.prefix).Content.EndsWith("PA 40010003");
                // Ueberschreiben (orange+fett), Aussetzen (—), Zuruecksetzen
                f.SetIdent(2, "PA 40010099"); Application.DoEvents();
                bool okOver = lauf.IdentFor(2) == "PA 40010099" && Cell(f, 2) == "PA 40010099" && f.gridId[ColId, 2].Style.ForeColor == Color.DarkOrange && f.gridId[ColId, 2].Style.Font.Bold && Cell(f, 3) == "PA 40010004";
                f.SetIdent(3, ""); Application.DoEvents();
                okOver &= lauf.IdentFor(3) == "" && Cell(f, 3) == "—" && lauf.IdentOver[3] == PackageStep.IdentOff;
                f.SetIdent(2, "PA 40010003"); Application.DoEvents();   // gleich der Automatik = Ueberschreibung weg
                okOver &= !lauf.IdentOver.ContainsKey(2);
                f.ResetIdent(3); Application.DoEvents();
                okOver &= !lauf.IdentOver.ContainsKey(3) && Cell(f, 3) == "PA 40010004" && lauf.IdentOver.Count == 0;
                f.gridId[ColSkip, 1].Value = false; Application.DoEvents();
                okSkip &= !lauf.Skips(1);
                // Start ohne Endziffern: Hinweis
                f.tIdent.Text = "PA X"; Application.DoEvents();
                bool okHint = f.HasHint("keine Endziffern");
                f.tIdent.Text = ""; Application.DoEvents();
                okHint &= !f.HasHint("Endziffern");
                // Materialvorlage nur bei Puffer-Schritten mit Vorlage
                Baustein pb = new Baustein(); pb.Name = "Puffer E2"; pb.Row = "Puffer"; pb.NoteTemplate = "Schläuche DN __ · Dichtungen DN __ · Tüllen DN __\nZweite Zeile";
                f.cat.Bausteine.Add(pb);
                PackageStep pu = new PackageStep(); pu.Title = "Puffer"; pu.Source = "Puffer E2"; pu.Variant = "mobil"; pu.Row = "Puffer"; pu.Day = f.NextDay();
                f.AddStep(pu); f.Bind(pu); f.TestMore(); Application.DoEvents();
                bool okTpl = f.bTemplate.Visible;
                f.InsertTemplate(); f.InsertTemplate(); Application.DoEvents();
                List<string> nt = pu.NoteTexts();
                okTpl &= nt.Count == 2 && nt[0] == "Schläuche DN __ · Dichtungen DN __ · Tüllen DN __" && nt[1] == "Zweite Zeile";
                Entry pe = pu.EntryFor(0, f.prefix);
                okTpl &= pe.Buffers.Count > 0 && pe.Buffers[0].Text.Contains("Tüllen DN __");
                f.Bind(lauf); f.TestMore(); Application.DoEvents();
                okTpl &= !f.bTemplate.Visible;
                f.dirty = false;
                return "E2 Paket-Editor: Kennungstabelle=" + K(okTab) + " Ausfall=" + K(okSkip) + " Ueberschreiben/Aussetzen/Zuruecksetzen=" + K(okOver) + " Start ohne Ziffern=" + K(okHint) + " Materialvorlage=" + K(okTpl) + (okTab && okSkip && okOver ? "" : " [" + dbg + "]");
            }
            catch (Exception ex) { return "E2 Paket-Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace.Split('\n')[0]; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }

        internal void TestScrollEnd() { Application.DoEvents(); scroll.AutoScrollPosition = new Point(0, 100000); Application.DoEvents(); }

        static string K(bool b) { return b ? "OK" : "FEHLER"; }
        static string Cell(PackageForm f, int run) { return Convert.ToString(f.gridId[ColId, run].Value); }
    }
}
#endif
