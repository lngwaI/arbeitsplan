using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Schritt-Details: Anordnung (LayoutDetails, Equipment-Spalten, "Mehr Optionen"); Aufbau und Bind: PackageForm.Details.cs.
    partial class PackageForm
    {
        // Zeile in einer Gruppe: setzt Sichtbarkeit und Hoehe; liefert die naechste y-Position.
        int Row(int y, bool show, params Control[] cs)
        {
            int h = 0;
            foreach (Control c in cs) { c.Visible = show; c.Top = y; h = Math.Max(h, c.Height); }
            return show ? y + Math.Max(h, IH) + 6 : y;
        }

        // Equipment mit Spalten abwechselnd/Ausweich: aufgeklappt oder wenn ein Wert gesetzt ist (nichts verschwindet still)
        bool WideEquip { get { return more || (cur != null && cur.AllEquip().Exists(delegate(EquipAssign a) { return a.Alternate || a.Fallback.Length > 0; })); } }

        const string MoreTip = "Mehr Optionen: eigene Laufnummer, abwechselnde Geräte, Ausweich-Gerät, Kennung und Notizen";
        string moreTip = "";

        // Zusammenfassung der eingeklappten Optionen auf der Schaltflaeche
        string MoreSummary(PackageStep s)
        {
            List<string> p = new List<string>();
            List<EquipAssign> all = s.AllEquip();
            if (all.Exists(delegate(EquipAssign a) { return a.Alternate; })) p.Add("abwechselnd");
            if (all.Exists(delegate(EquipAssign a) { return a.Fallback.Length > 0; })) p.Add("Ausweich");
            if (s.IdentList(StepsView().RunCountOf(s)).Length > 0) p.Add("Kennung");
            if (s.Note.Length > 0) p.Add(s.NoteTexts().Count > 1 ? "Notizen" : "Notiz");
            if (s.RunEvery > 1) p.Add("reicht für " + s.RunEvery + " Läufe");
            return p.Count > 0 ? "  –  gesetzt: " + string.Join(", ", p.ToArray()) : "";
        }

        void LayoutDetails()
        {
            if (laying || cur == null) return;
            PerfB(PfLayout);
            laying = true;
            try { LayoutCore(); }
            finally { laying = false; PerfE(PfLayout); }   // N7: Ausnahme beim Anordnen sperrt die Anordnung sonst dauerhaft
        }

        void LayoutCore()
        {
            PackageStep s = cur;
            bool sub = s.Kind == StepKind.SubRun;
            bool var = s.IsBuffer && s.Puffers.Count == 0;   // Typ steht je Puffer in der Karte (PackageForm.PufferCards.cs)
            int w = body.ClientSize.Width, g = 8;
            int a = Math.Max((w - g) / 2, Math.Min(w - g - 250, (w - g) * 55 / 100));   // Breite Was? Wo? | Rest Wann?
            gWhat.Width = a; gWhen.Width = w - a - g;   // Feldbreiten (Anker) stehen vor dem Anordnen fest

            Baustein b = cat.Baustein_(s.Source);
            bool off = b != null && b.Row.Length > 0 && s.Row.Length > 0 && s.Row != b.Row;   // z. B. Packen in Suite 2
            rowHint.Text = off ? "Standard-Raum ist „" + b.Row + "“ (Sonderaktion?)" : "";
            tip.SetToolTip(rowHint, rowHint.Text);
            int y = gWhat.ContentTop;
            y = Row(y, true, lTitle, tTitle); y = Row(y, true, lRow, cbRow); y = Row(y, off, rowHint) - (off ? 4 : 0);
            y = Row(y, true, lKind, cbKind); y = LayoutVariants(y, var); y = LayoutAnsatz(y, s);
            bool packen = s.Source == Package.PackenName && !s.IsBackup;
            y = Row(y, packen, lManual, tManualNo); y = Row(y, packen && lManualHint.Text.Length > 0, lManualHint) - (packen && lManualHint.Text.Length > 0 ? 2 : 0);
            y = Row(y, sub, lSub, tSubNos); y = Row(y, sub, lSubHelp);
            int hWhat = y + 2, hWhen = LayoutWhen(s);   // Wann?: PackageForm.When.cs

            gExtra.Width = w;   // vor dem Anordnen, damit die Anker-Breiten (Tabelle) stimmen
            y = gExtra.ContentTop;
            bool ident = IdentVisible(s);   // Kennung/PA nur bei Lauf-/Unterlauf-Schritten, nie bei Puffern (ausser es steht schon eine drin)
            y = Row(y, ident, lIdent, tIdent); y = Row(y, ident, lIdentHelp) - (ident ? 2 : 0);
            gridId.SetBounds(8, gridId.Top, w - 16, gridId.Height); y = Row(y, showIdents, gridId);
            y = Row(y, true, lNote); y = Row(y, true, tNote) - 2;
            y = Row(y, cat.NoteTemplateFor(s).Length > 0, bTemplate);
            int hExtra = y + 2;

            int h = Math.Max(hWhat, hWhen);
            gWhat.SetBounds(0, 0, a, h); gWhen.SetBounds(a + g, 0, w - a - g, h);
            y = h + 4;
            string sum = MoreSummary(s);   // U7: kurzer Knopftext, Zusammenfassung dahinter und vollstaendig im Tooltip (bei 1024 px nicht abgeschnitten)
            bMore.Text = more ? "▾ Weniger Optionen" : "▸ Mehr Optionen" + sum;
            string mt = MoreTip + (sum.Length > 0 ? "\n" + sum.Replace("  –  ", "") : "");
            if (mt != moreTip) { moreTip = mt; tip.SetToolTip(bMore, mt); }
            bMore.SetBounds(0, y, w, bMore.Height); y = bMore.Bottom + 4;
            gExtra.Visible = more;   // Zusaetze direkt unter "Mehr Optionen", vor den Puffer-/Equipment-Karten
            if (more) { gExtra.SetBounds(0, y, w, hExtra); y = gExtra.Bottom + g; }
            gEquip.Visible = !s.IsBuffer; gPuf.Visible = s.IsBuffer;   // Puffer-Schritt: je Puffer eine Karte statt der gemeinsamen Equipment-Liste
            if (s.IsBuffer) { gPuf.Width = w; gPuf.SetBounds(0, y, w, ArrangePuffer(w - 16)); y = gPuf.Bottom; }
            else { gEquip.SetBounds(0, y, w, ArrangeEquip(w - 16)); y = gEquip.Bottom; }
            body.Height = y + 4;
        }

        int ArrangeEquip(int iw)
        {
            bool any = eqRows.Count > 0, wide = WideEquip;
            int[] x = EquipRow.Cols(iw, wide);
            int y = gEquip.ContentTop;
            for (int i = 0; i < 5; i++) equipHead[i].SetBounds(8 + x[i], 0, Math.Max(0, x[i + 1] - x[i] - 6), equipHead[i].Height);
            if (!wide) equipHead[2].Visible = equipHead[3].Visible = false;   // Sichtbarkeit je Kopf einmal setzen (nicht erst ein-, dann ausblenden)
            y = Row(y, any, wide ? (Control[])equipHead : new Control[] { equipHead[0], equipHead[1], equipHead[4] }) - 2;
            int ry = 0;
            int n = eqRows.Count, si = 0;
            foreach (EquipRow r in eqRows) { r.TabIndex = si; r.SetOrder(si, si > 0, si < n - 1); si++; r.Arrange(iw, wide); r.Location = new Point(0, ry); ry += r.Height; }
            equipRows.SetBounds(8, y, x[6], ry);
            y += ry + 4;
            y = Row(y, !any, lNoEquip);
            y = Row(y, true, bPos);
            return y + 4;
        }
    }
}
