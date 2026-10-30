using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor (UX-1b): Puffer-Schritt = eine Karte je Puffer (PufferCard) statt einer gemeinsamen Equipment-Liste. Karten sind zugeklappt
    // kompakt; "+ Puffer" hängt einen Puffer aus dem Standardpuffer an, "Puffer für Produkt…" setzt die Puffer des Produktprofils (sonst
    // Standardpuffer). Beim Öffnen eines Puffer-Schritts wird er auf eigene Puffer umgestellt (MigratePuffers, verlustfrei), nach jeder
    // Änderung SyncPuffers + Rückgängig-Schnappschuss. Hinweise (Doppelbelegung, Produkt) markieren die betroffene Karte rot.
    partial class PackageForm
    {
        Section gPuf;
        readonly Panel pufCards = new Panel();
        readonly Button bPufAdd = new Button(), bPufProd = new Button(), bPufMgr = new Button();
        readonly List<PufferCard> cards = new List<PufferCard>();
        readonly HashSet<PufferSpec> openSpecs = new HashSet<PufferSpec>();   // aufgeklappte Karten (nur Ansicht, nicht gespeichert)
        readonly Dictionary<PackageStep, HashSet<int>> pufBad = new Dictionary<PackageStep, HashSet<int>>();   // Karten mit Hinweis (rot)
        PackageStep pufStep;

        void BuildPuffer()
        {
            gPuf = Group("Puffer", "Je Puffer eigene Geräte; orange = nicht validiert.", 500);
            pufCards.Tag = "keep"; gPuf.Controls.Add(pufCards);
            Btn(bPufProd, "Puffer für Produkt…", 8, 0, 240, delegate { PufferForProduct(true); });
            Btn(bPufAdd, "+ Puffer", 0, 0, 110, delegate { AddPuffer(); });
            Btn(bPufMgr, "Pufferpakete verwalten…", 0, 0, 200, delegate { ManagePufferPacks(); });
            gPuf.Controls.AddRange(new Control[] { bPufProd, bPufAdd, bPufMgr });
            tip.SetToolTip(bPufMgr, canAdmin ? "Pufferpakete der Produkte anlegen und ändern (Einstellungen → Pufferpakete…)" : "Pufferpakete der Produkte ansehen (ändern darf nur, wer Pakete einrichten darf)");
            gPuf.TabIndex = 4; gPuf.Visible = false;
            tip.SetToolTip(bPufAdd, "Weiteren Puffer anhängen (aus dem Standardpuffer, höchstens " + PackageStep.MaxPuffer + ")");
        }

        // Karten werden wiederverwendet: nicht gebrauchte bleiben versteckt in spare (ohne Zeilen), BindPuffer bindet sie neu
        readonly List<PufferCard> spare = new List<PufferCard>();

        PufferCard TakeCard()
        {
            if (spare.Count > 0) { PufferCard c = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1); return c; }
            PufferCard n = new PufferCard(PufferChanged, PufferRebuild, PufferStructure, TogglePuffer, delegate(int i) { RemovePuffer(i, true); }, MovePuffer);
            pufCards.Controls.Add(n);
            return n;
        }

        void BindPuffer(PackageStep s)
        {
            foreach (PufferCard c in cards) { c.ReleaseRows(); c.Visible = false; spare.Add(c); }
            cards.Clear();
            if (s == null || !s.IsBuffer || s.Puffers.Count == 0) { pufStep = null; return; }
            if (s != pufStep) { pufStep = s; openSpecs.Clear(); openSpecs.Add(s.Puffers[0]); }
            for (int k = 0; k < s.Puffers.Count; k++)
            {
                PufferCard c = TakeCard();
                c.Bind(s, k, cat, Product); c.TabIndex = k; c.Visible = true;
                cards.Add(c);
            }
        }

        // Pufferpakete aller Produkte (Produktauswahl, dann Pufferpaket-Dialog); danach Katalog neu laden (PufferPackAdmin.cs)
        void ManagePufferPacks()
        {
            if (!PufferPackAdmin.Manage(this, store, Product, canAdmin)) return;
            cat = store.GetCatalog(); cat.AddStandardBausteine(); BuildPosMenu();
            if (cur != null) Bind(cur);
        }

        void PufferChanged() { if (loading || cur == null) return; cur.SyncPuffers(); StepsView().Normalize(false); Changed(); }   // N7: Karte "3 Tage vor" darf nie vor Tag 1 landen

        void PufferRebuild() { if (cur == null) return; cur.SyncPuffers(); dirty = true; Bind(cur); CommitUndo(); }

        // Zeilen einer Karte geaendert (Reihenfolge, eine Zeile mehr/weniger): die Karte hat sich selbst angepasst, nur Ansicht nachziehen
        void PufferStructure()
        {
            if (cur == null) return;
            cur.SyncPuffers(); dirty = true;
            HoldDraw();
            try { HookLeave(pufCards); UpdateView(false); } finally { ReleaseDraw(); }
            CommitUndo();
        }

        void TogglePuffer(int k)
        {
            if (cur == null || k < 0 || k >= cur.Puffers.Count) return;
            PufferSpec sp = cur.Puffers[k];
            if (!openSpecs.Remove(sp)) openSpecs.Add(sp);
            LayoutDetails();
        }

        int ArrangePuffer(int iw)
        {
            int y = gPuf.ContentTop;
            if (cur == null || pufStep != cur || cards.Count != cur.Puffers.Count) return y + 8;
            bPufProd.Text = Product.Length > 0 ? "Puffer für Produkt " + Product + "…" : "Standardpuffer verwenden…";
            bPufProd.Width = Math.Max(200, TextRenderer.MeasureText(bPufProd.Text, bPufProd.Font).Width + 28);
            tip.SetToolTip(bPufProd, "Puffer des Produkts übernehmen (Pufferpaket aus „Produkte & Equipment“, sonst Standardpuffer). Ersetzt die Puffer dieses Schritts.");
            bPufAdd.Left = 8 + bPufProd.Width + 8;
            bPufMgr.Width = Math.Max(190, TextRenderer.MeasureText(bPufMgr.Text, bPufMgr.Font).Width + 28);
            bool wrap = bPufAdd.Right + 8 + bPufMgr.Width > iw;   // schmal: eigene Zeile
            bPufMgr.Left = wrap ? 8 : bPufAdd.Right + 8;
            y = Row(y, true, bPufProd, bPufAdd, bPufMgr);
            if (wrap) { bPufMgr.Top = y; y += bPufMgr.Height + 6; }
            bool wide = WideEquip; int cy = 0;
            foreach (PufferCard c in cards)
            {
                c.Open = openSpecs.Contains(cur.Puffers[c.Index]); c.ShowTiming = IsJe(cur);
                c.Left = 0; c.Top = cy; cy += c.Arrange(iw, wide) + 6;
            }
            pufCards.SetBounds(8, y, iw, cy); y += cy;
            bPufAdd.Enabled = cur.Puffers.Count < PackageStep.MaxPuffer;
            return y + 4;
        }

        // Neuer Puffer: Positionen des Standardpuffers (ohne Geräte), Typ wie der vorige; neue Karte aufgeklappt, die anderen bleiben wie sie sind
        void AddPuffer()
        {
            if (cur == null || !cur.IsBuffer || cur.Puffers.Count == 0 || cur.Puffers.Count >= PackageStep.MaxPuffer) return;
            PufferTemplate t = cat.StandardPuffer();
            if (t == null)
            {
                t = new PufferTemplate();
                foreach (EquipAssign a in cur.Puffers[cur.Puffers.Count - 1].Lines) { PufferTemplatePos p = new PufferTemplatePos(); p.Short = a.Short; t.Positions.Add(p); }
            }
            t.Typ = cur.Puffers[cur.Puffers.Count - 1].Typ;
            PufferSpec sp = Package.ApplyPufferTemplate(cur, t);
            sp.NameNo = cur.Puffers.Count;   // ohne eigenen Namen "Titel 2", "Titel 3" …
            openSpecs.Add(sp);
            dirty = true; Bind(cur); CommitUndo();
        }

        internal void RemovePuffer(int k, bool ask)
        {
            if (cur == null || k < 0 || k >= cur.Puffers.Count || cur.Puffers.Count < 2) return;
            PufferSpec sp = cur.Puffers[k]; int devs = cur.PufferChoices(k, 0).Count;
            if (ask && devs > 0 && MessageBox.Show(this, "Puffer " + (k + 1) + " mit " + devs + " Gerät(en) entfernen?\nGemeinsame Zeilen, die nur für diesen Puffer galten, entfallen.", "Puffer entfernen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            cur.RemovePuffer(k); openSpecs.Remove(sp);
            if (!cur.Puffers.Exists(delegate(PufferSpec x) { return openSpecs.Contains(x); })) openSpecs.Add(cur.Puffers[Math.Min(k, cur.Puffers.Count - 1)]);
            dirty = true; Bind(cur); CommitUndo();
            Say("Puffer " + (k + 1) + " entfernt – Rückgängig mit Strg+Z oder ↶");   // U6: auch ohne Rückfrage (leere Karte) sichtbar und umkehrbar
        }

        // U4: Karte (Puffer) verschieben; Geltungsbereiche ziehen mit (PackageStep.MovePuffer), Rueckgaengig wie jede Aenderung
        internal void MovePuffer(int from, int to)
        {
            if (cur == null || !cur.MovePuffer(from, to)) return;
            dirty = true; Bind(cur); CommitUndo();
            Say("Puffer " + (from + 1) + " steht jetzt an Stelle " + (to + 1));
        }

        // ---------- Puffer für Produkt ----------

        void PufferForProduct(bool ask)
        {
            if (cur == null || !cur.IsBuffer) return;
            List<PufferTemplate> list = Package.PufferFor(cat, Product);
            if (list.Count == 0)
            {
                MessageBox.Show(this, "Weder für Produkt " + (Product.Length > 0 ? Product : "(noch ohne Code)") + " noch als Standardpuffer ist ein Puffer hinterlegt.\nDer Admin kann beides unter Einstellungen → „Produkte & Equipment“ bzw. „Equipment & Bausteine“ anlegen.",
                    "Puffer für Produkt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ProductProfile pr = cat.Product(Product);
            bool own = pr != null && pr.Puffer.Count > 0;
            using (PufferPickForm d = new PufferPickForm(Product, own, list, cur.Puffers.Count))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ApplyProductPuffer(d.Result);
            }
        }

        internal void ApplyProductPuffer(List<PufferTemplate> list)
        {
            if (cur == null || !cur.IsBuffer || list.Count == 0) return;
            if (list.Count > PackageStep.MaxPuffer) list = list.GetRange(0, PackageStep.MaxPuffer);
            Package.ApplyPufferTemplates(cur, list);
            for (int k = 1; k < cur.Puffers.Count; k++) if (cur.Puffers[k].Name.Length == 0) cur.Puffers[k].NameNo = k + 1;   // unbenannte: "Titel 2" …
            pufStep = null;   // Kartenzustand neu: die erste aufgeklappt
            dirty = true; Bind(cur); CommitUndo();
        }

        // ---------- Hinweise (PackageForm.Preview.cs: UpdateHints, PackageForm.Hints.cs: RuleHint) ----------

        void MarkPuffer(PackageStep s, int k)
        {
            HashSet<int> set;
            if (!pufBad.TryGetValue(s, out set)) { set = new HashSet<int>(); pufBad[s] = set; }
            set.Add(k);
        }

        void MarkDevice(PackageStep s, int run, string id)
        {
            for (int k = 0; k < s.Puffers.Count; k++)
                foreach (EquipChoice c in s.PufferChoices(k, Math.Max(0, run)))
                    if (string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) MarkPuffer(s, k);
        }

        // Regel "pufdoppelt": die Karten beider Beteiligten, die das doppelte Gerät tragen, werden rot
        void NoteDup(PackageProblem p, PackageStep ps, PackageStep ps2)
        {
            int i = p.Text.IndexOf(" ist in Puffer", StringComparison.Ordinal);
            if (i < 0) return;
            string id = p.Text.Substring(0, i); id = id.Substring(id.LastIndexOf(' ') + 1);
            if (ps != null) MarkDevice(ps, p.Run, id);
            if (ps2 != null) MarkDevice(ps2, p.Run2, id);
        }

        // Nicht validierte Geräte je Puffer (Klick: zur Liste hinzufügen) und Produktpassung (Catalog.CheckPufferProduct)
        void PufferHints(List<Hint> h, string n, PackageStep s)
        {
            HashSet<string> reported = new HashSet<string>();
            for (int k = 0; k < s.Puffers.Count; k++)
                foreach (EquipAssign a in s.Puffers[k].Lines)
                    foreach (string id in EquipRow.Unvalidated(a, cat, Product)) { h.Add(EquipHint(n + ", Puffer " + (k + 1), a, id, s)); reported.Add(id); }
            foreach (PufferShare g in s.PufferShared)
                foreach (string id in EquipRow.Unvalidated(g.Eq, cat, Product)) { h.Add(EquipHint(n + ", gemeinsame Zeile", g.Eq, id, s)); reported.Add(id); }
            if (s.UsesAnsatz && s.Ansatz.Length == 0)   // leere Tank-Zeile: Gerät wählen, "nicht benötigt" oder Ansatz ohne Tank
            {
                for (int k = 0; k < s.Puffers.Count; k++)
                    foreach (string t in EmptyTanks(s.Puffers[k].Lines)) h.Add(new Hint(n + ", Puffer " + (k + 1) + ": Tank (" + t + ") nicht gewählt – Gerät wählen, „" + EquipRow.NoneText + "“ wählen oder Ansatz „Flasche/Gebinde“.", s, cbAnsatz));
                List<EquipAssign> sh = new List<EquipAssign>(); foreach (PufferShare g in s.PufferShared) sh.Add(g.Eq);
                foreach (string t in EmptyTanks(sh)) h.Add(new Hint(n + ", gemeinsame Zeile: Tank (" + t + ") nicht gewählt – Gerät wählen oder Zeile entfernen.", s, cbAnsatz));
            }
            if (s.Puffers.Count == 0 || Product.Length == 0) return;
            Package one = new Package(); one.ProductCode = Product; one.RunPrefix = prefix; one.Steps.Add(s);
            foreach (string e in cat.CheckPufferProduct(one))
            {
                int k = PufferNoOf(e);
                if (k >= 0 && k < s.Puffers.Count) MarkPuffer(s, k);
                int a = e.IndexOf("Gerät ", StringComparison.Ordinal), b = e.IndexOf(" ist für Produkt", StringComparison.Ordinal);
                string id = a >= 0 && b > a ? e.Substring(a + 6, b - a - 6) : "";
                if (id.Length == 0 || !reported.Contains(id)) h.Add(new Hint(n + ": " + e, s, null));
            }
        }

        List<string> EmptyTanks(List<EquipAssign> lines)
        {
            List<string> r = new List<string>();
            foreach (EquipAssign a in lines) if (PackageStep.IsTank(cat, a.Short) && a.ChoiceFor(0) == null && !r.Contains(a.Short)) r.Add(a.Short);
            return r;
        }

        // "Puffer 2 (…): …" -> 1 (nullbasiert), sonst -1
        static int PufferNoOf(string text)
        {
            if (!text.StartsWith("Puffer ", StringComparison.Ordinal)) return -1;
            int i = 7, v = 0;
            while (i < text.Length && char.IsDigit(text[i])) v = v * 10 + (text[i++] - '0');
            return i > 7 ? v - 1 : -1;
        }

        void MarkCards()
        {
            HashSet<int> set = null;
            if (cur != null) pufBad.TryGetValue(cur, out set);
            foreach (PufferCard c in cards)
            {
                bool bad = set != null && set.Contains(c.Index);
                if (c.Bad != bad) { c.Bad = bad; c.Invalidate(); }
            }
        }
    }
}
