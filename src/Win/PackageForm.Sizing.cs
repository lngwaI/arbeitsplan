using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor beim Vergroessern/Verkleinern: Doppelpuffer gegen Flackern; waehrend des Ziehens am Fensterrand (ResizeBegin..ResizeEnd)
    // nur die Dock-Bereiche mitziehen, Schritt-Details und Pruefhinweise erst am Ende einmal neu anordnen. Listen werden nie neu aufgebaut.
    partial class PackageForm
    {
        bool sizing, sizePending;
        int hintsW;   // Breite, fuer die die Pruefhinweise zuletzt umbrochen wurden

        // Standard-Steuerelemente (Panel, ListView) puffern; die Eigenschaft ist protected
        static void Buffered(params Control[] cs)
        {
            PropertyInfo p = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Control c in cs) p.SetValue(c, true, null);
        }

        void InitSizing()
        {
            DoubleBuffered = true;
            InitSmooth();   // Tipp-Verzoegerung, Pool freigeben (PackageForm.Smooth.cs)
            Buffered(left, lv, scroll, body, equipRows, stripScroll);
            body.Resize += delegate
            {
                if (sizing) { sizePending = true; return; }
                HoldDraw();   // einmal anordnen und zeichnen, kein schrittweiser Aufbau
                try { LayoutDetails(); } finally { ReleaseDraw(); }
            };
            lbHints.Resize += delegate { if (sizing) sizePending = true; else RewrapHints(); };
        }

        void RewrapHints() { if (lbHints.Width != hintsW) { hintsW = lbHints.Width; UpdateHints(); } }

        protected override void OnResizeBegin(EventArgs e) { sizing = true; strip.Deferred = true; base.OnResizeBegin(e); }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            sizing = false;
            strip.Deferred = false;   // Kalender ordnet sich jetzt einmal neu an
            if (!sizePending) return;
            sizePending = false;
            HoldDraw();
            try { body.SuspendLayout(); LayoutDetails(); body.ResumeLayout(); }
            finally { ReleaseDraw(); }
            blockPreview.Relayout(PreviewW);
            RewrapHints();
        }

        // Kopfzeile: Kampagne aus dem Hauptlauf (Package.Head) aus Startnummer, Laufanzahl und Rhythmus:
        // "Lauf 12340001 bis 12340005 · alle 3 Tage"; null = kein Lauf im Ablauf
        string CampaignText()
        {
            PackageStep m = StepsView().Head;
            if (m == null) return null;
            int n = pk.RunCount;
            return "Lauf " + m.RunNo(prefix, 0) + (n > 1 ? " bis " + m.RunNo(prefix, n - 1) + " · " + RepeatPlain(m) : "");
        }

#if TESTHOOKS
        // Fenster ziehen (ResizeBegin, mehrere Groessen, ResizeEnd): Eintraege der Ablaufliste, Geraetezeilen und Pruefhinweise bleiben
        // dieselben Objekte (kein Neuaufbau); nach dem Loslassen passt die Equipment-Gruppe wieder genau in die neue Breite.
        public static string TestResize(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, Package.SampleCampaign("Groessentest"), true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                foreach (PackageStep s in f.Ordered()) if (s.Equipment.Count > 0) { f.Bind(s); break; }
                Application.DoEvents();
                ListViewItem it0 = f.lv.Items.Count > 0 ? f.lv.Items[0] : null;
                Control eq0 = f.eqRows.Count > 0 ? f.eqRows[0] : null;
                object h0 = f.lbHints.Items.Count > 0 ? f.lbHints.Items[0] : null;
                int w0 = f.gEquip.Width;
                f.OnResizeBegin(EventArgs.Empty);
                Size cs = f.ClientSize;   // OnLoad passt das Fenster an kleine Bildschirme an (CI: 1024x768): nur verkleinern
                for (int i = 1; i <= 5; i++) { f.ClientSize = new Size(cs.Width - 15 * i, cs.Height - 5 * i); Application.DoEvents(); }
                bool locked = f.gEquip.Width == w0 && f.sizePending;
                bool same = f.lv.Items.Count > 0 && f.lv.Items[0] == it0 && eq0 != null && f.eqRows.Count > 0 && f.eqRows[0] == eq0
                    && (h0 == null || (f.lbHints.Items.Count > 0 && f.lbHints.Items[0] == h0));
                f.OnResizeEnd(EventArgs.Empty); Application.DoEvents();
                // Toleranz: ein Bildlauf, der erst durch die neue Hoehe erscheint/verschwindet, aendert die Breite danach noch
                bool fit = f.gEquip.Width != w0 && Math.Abs(f.gEquip.Width - f.body.ClientSize.Width) <= SystemInformation.VerticalScrollBarWidth && !f.sizePending && f.eqRows.Count > 0 && f.eqRows[0] == eq0;
                string camp = f.CampaignText() ?? "";
                return "Paket-Editor Resize: Layoutsperre beim Ziehen " + (locked ? "ok" : "FEHLER") + ", kein Neuaufbau " + (same ? "ok" : "FEHLER")
                    + ", danach angepasst " + (fit ? "ok" : "FEHLER") + ", Kopfzeile Kampagne " + (camp.StartsWith("Lauf ") ? "ok (" + camp + ")" : "FEHLER");
            }
            catch (Exception ex) { return "Paket-Editor Resize: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
#endif
    }
}
