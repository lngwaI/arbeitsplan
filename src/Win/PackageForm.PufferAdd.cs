using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // UX-6 B1/B2 im Paket-Editor: "Pufferpaket übernehmen…" (PufferPickForm, ersetzt oder hängt an) und "+ Puffer ▾" (leer, Kopie, Vorlage).
    // Jede Aktion: neue Karte aufgeklappt, dirty, Bind, CommitUndo, Rückmeldung in der Leiste. Der Produktcode des Pakets wird nie geändert.
    partial class PackageForm
    {
        ContextMenuStrip addMenu;
        const string UndoHint = " – Rückgängig mit Strg+Z";

        bool CanAddPuffer() { return cur != null && cur.IsBuffer && cur.Puffers.Count > 0 && cur.Puffers.Count < PackageStep.MaxPuffer; }

        // Name des Puffers k wie im Plan (ohne Produktcode)
        string PufferName(int k)
        {
            List<BufferItem> l = cur.BuffersFor(0, prefix);
            return k < l.Count ? l[k].Name : "";
        }

        static string Amp(string s) { return s.Replace("&", "&&"); }

        void ShowAddMenu()
        {
            if (!CanAddPuffer()) return;
            if (addMenu != null) addMenu.Dispose();
            addMenu = new ContextMenuStrip(); addMenu.Font = Font;
            addMenu.Items.Add("Leerer Puffer (Positionen des Standardpuffers)", null, delegate { AddPuffer(); });
            for (int k = 0; k < cur.Puffers.Count; k++)
            {
                int kk = k; string n = PufferName(k);
                addMenu.Items.Add(Amp("Kopie von Puffer " + (k + 1) + (n.Length > 0 ? ": " + n : "")), null, delegate { AddPufferCopy(kk); });
            }
            addMenu.Items.Add(new ToolStripSeparator());
            ProductProfile pr = cat.Product(Product);
            List<PufferTemplate> tpls = pr != null ? pr.Puffer : new List<PufferTemplate>();
            string head = Product.Length == 0 ? "Kein Produktcode im Paket" : tpls.Count == 0 ? "Kein Pufferpaket für " + Product : "Vorlage aus Pufferpaket " + Product + ":";
            ToolStripLabel hl = new ToolStripLabel(Amp(head)); hl.ForeColor = SystemColors.GrayText; addMenu.Items.Add(hl);
            foreach (PufferTemplate t in tpls)
            {
                PufferTemplate tt = t;
                addMenu.Items.Add(Amp(t.Label + " · " + PufferPickForm.TypText(t.Typ)), null, delegate { AddPufferFromTemplate(tt); });
            }
            addMenu.Items.Add("Andere Vorlage…", null, delegate { PickPufferPack(true, null); });
            addMenu.Show(bPufAdd, new Point(0, bPufAdd.Height));
        }

        // Neue Karte aufgeklappt, Bind, Rückgängig-Schnappschuss, Meldung
        void PufferAdded(PufferSpec first, string msg)
        {
            openSpecs.Add(first);
            dirty = true; Bind(cur); CommitUndo();
            Say(msg + UndoHint);
        }

        // Leerer Puffer: Positionen des Standardpuffers (ohne Geräte), Typ wie der vorige
        void AddPuffer()
        {
            if (!CanAddPuffer()) return;
            PufferTemplate t = cat.StandardPuffer();
            if (t == null)
            {
                t = new PufferTemplate();
                foreach (EquipAssign a in cur.Puffers[cur.Puffers.Count - 1].Lines) { PufferTemplatePos p = new PufferTemplatePos(); p.Short = a.Short; t.Positions.Add(p); }
            }
            t.Typ = cur.Puffers[cur.Puffers.Count - 1].Typ;
            PufferSpec sp = Package.ApplyPufferTemplate(cur, t);
            sp.NameNo = cur.Puffers.Count;   // ohne eigenen Namen "Titel 2", "Titel 3" …
            PufferAdded(sp, "Puffer " + cur.Puffers.Count + " angehängt");
        }

        // Unabhängige Kopie von Puffer k: Zeilen geklont; gemeinsame Zeilen, die Puffer k abdecken, decken auch die Kopie ab
        internal void AddPufferCopy(int k)
        {
            if (!CanAddPuffer() || k < 0 || k >= cur.Puffers.Count) return;
            PufferSpec sp = cur.Puffers[k].Clone();
            int nk = cur.Puffers.Count;
            cur.Puffers.Add(sp);
            foreach (PufferShare g in cur.PufferShared) if (g.Scope != 0 && g.Covers(k) && nk < 31) g.Scope |= 1 << nk;
            if (sp.Name.Length == 0) sp.NameNo = cur.Puffers.Count;
            cur.SyncPuffers();
            PufferAdded(sp, "Puffer " + cur.Puffers.Count + " als Kopie von Puffer " + (k + 1) + " angehängt");
        }

        internal void AddPufferFromTemplate(PufferTemplate t)
        {
            if (!CanAddPuffer()) return;
            PufferSpec sp = Package.ApplyPufferTemplate(cur, t.Clone());
            if (sp.Name.Length == 0) sp.NameNo = cur.Puffers.Count;
            PufferAdded(sp, "Puffer " + cur.Puffers.Count + " aus Vorlage „" + t.Label + "“ angehängt");
        }

        // ---------- Pufferpaket übernehmen ----------

        // Dialog (PufferPickForm); "Pufferpaket anlegen…" im Leerzustand: Pufferpakete verwalten (dieses Produkt) und den Dialog erneut öffnen
        void PickPufferPack(bool append, string pre)
        {
            if (cur == null || !cur.IsBuffer) return;
            while (true)
            {
                using (PufferPickForm d = new PufferPickForm(cat, Product, cur.Puffers.Count, canAdmin, append, pre))
                {
                    DialogResult r = d.ShowDialog(this);
                    if (r == DialogResult.Retry)
                    {
                        pre = d.CreateFor; append = d.Append;
                        if (PufferPackAdmin.Manage(this, store, pre, canAdmin)) { cat = store.GetCatalog(); cat.AddStandardBausteine(); BuildPosMenu(); }
                        continue;
                    }
                    if (r == DialogResult.OK) ApplyPicked(d.Result, d.ResultCode, d.Append);
                    return;
                }
            }
        }

        // Ersetzen: Puffer des Schritts = Vorlagen (ApplyProductPuffer); Anhängen: hinten dran bis MaxPuffer, Reihenfolge erhalten
        internal void ApplyPicked(List<PufferTemplate> list, string code, bool append)
        {
            if (cur == null || !cur.IsBuffer || list.Count == 0) return;
            string src = code.Length > 0 ? "Pufferpaket " + code : "Standardpuffer";
            if (!append)
            {
                ApplyProductPuffer(list);
                Say(Math.Min(list.Count, PackageStep.MaxPuffer) + " Puffer aus " + src + " übernommen (bisherige ersetzt)" + UndoHint);
                return;
            }
            int n = Math.Min(list.Count, PackageStep.MaxPuffer - cur.Puffers.Count);
            if (n <= 0) return;
            PufferSpec first = null;
            for (int i = 0; i < n; i++)
            {
                PufferSpec sp = Package.ApplyPufferTemplate(cur, list[i].Clone());
                if (sp.Name.Length == 0) sp.NameNo = cur.Puffers.Count;
                if (first == null) first = sp;
            }
            PufferAdded(first, n + " Puffer aus " + src + " übernommen (hinten angehängt)");
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
    }
}
