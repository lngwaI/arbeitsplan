using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Zeile fuer eine Equipment-Position im Paket-Editor: Position | Geraet | abwechselnd | Ausweich-Geraet | Notiz | entfernen.
    partial class EquipRow : Panel
    {
        const int Gap = 6, AltW = 132, DelW = 24, PlusW = 24, ArrowW = 17, GripW = 12;
        public const int Lead = GripW + 2 * ArrowW + 4;   // linke Spalte: Griff, ▲, ▼ (UX-5b)
        EquipAssign a;
        EquipPosition pos;
        Catalog cat;
        string product;    // Produktcode des Pakets: erlaubte Geraete/Austauschgruppen aus dem Produktprofil
        Action changed;
        Action<EquipAssign> remove;
        bool allowNone;
        readonly Label name = new Label();
        readonly ComboBox cbId = new ComboBox(), cbFb = new ComboBox();
        readonly CheckBox chkAlt = new CheckBox();
        readonly Button btnAlt = new Button(), del = new Button(), plus = new Button(), up = new Button(), down = new Button();
        readonly RowGrip grip;
        readonly ToolTip tip = new ToolTip();
        bool hot, canUp, canDown;
        internal RowReorder Reorder;   // Umsortieren (Container setzt es; null = Zeile nicht verschiebbar)
        public int Slot = -1;          // Platz der Zeile im Container (Reihenfolge im Modell)
        readonly TextBox note = new TextBox();
        bool loading;
        int arrW = -1; bool arrWide, arrRot;   // letzte Anordnung (Arrange tut nichts, wenn sich nichts geaendert hat)
        bool itemsStale = true, fbStale = true;   // Auswahllisten werden erst beim Aufklappen gefuellt (ComboBox-Zugriffe sind teuer, UX-5e)

        public EquipAssign Assign { get { return a; } }
        Action<EquipAssign> addBelow;
        public Action<EquipAssign> AddBelow { get { return addBelow; } set { addBelow = value; plus.Visible = value != null; } }   // "+": dieselbe Position direkt darunter einfuegen (null = Knopf fehlt); der Container setzt es nach Get/Rebind

        bool NoList { get { return pos == null || pos.Ids.Count == 0; } }   // Position ohne Geraeteliste (z. B. Schlauch)

        // Zeile fuer "+" unter src: ohne Geraeteliste uebernimmt sie das Geraet, sonst bleibt sie leer
        public static EquipAssign Below(EquipAssign src, Catalog cat)
        {
            EquipAssign n = new EquipAssign(); n.Short = src.Short;
            EquipPosition p = cat.Position(src.Short);
            if (p == null || p.Ids.Count == 0) foreach (EquipChoice c in src.Choices) n.Choices.Add(c.Clone());
            return n;
        }

        // Fokus der neuen "+"-Zeile: Notiz (Geraet schon uebernommen) bzw. Geraet
        public void FocusNew() { if (NoList) note.Focus(); else cbId.Focus(); }

        // x-Positionen der Spalten (Position, Geraet, abwechselnd, Ausweich, Notiz, Entfernen, Ende) fuer die Breite w; auch fuer die Spaltenkoepfe.
        // wide = false ("Mehr Optionen" zu): abwechselnd und Ausweich ohne Breite.
        public static int[] Cols(int w, bool wide)
        {
            int alt = wide ? AltW : 0, gaps = wide ? 5 * Gap : 3 * Gap;
            int rest = Math.Max(wide ? 420 : 300, w - Lead - alt - DelW - PlusW - 2 - gaps);
            int p = rest * (wide ? 27 : 34) / 100, id = rest * (wide ? 25 : 33) / 100, fb = wide ? rest * 23 / 100 : 0, nt = rest - p - id - fb;
            int[] x = new int[7];
            x[0] = Lead; x[1] = Lead + p + Gap; x[2] = x[1] + id + Gap; x[3] = x[2] + alt + (wide ? Gap : 0); x[4] = x[3] + fb + (wide ? Gap : 0); x[5] = x[4] + nt + Gap; x[6] = x[5] + DelW + 2 + PlusW;
            return x;
        }

        public const string NoneText = "nicht benötigt";   // Eintrag oben in der Liste (nur mit allowNone): Zeile entfernen

        public EquipRow(EquipAssign a, Catalog cat, string product, Action changed, Action<EquipAssign> remove) : this(a, cat, product, changed, remove, false) { }

        // Das Steuerelement wird einmal gebaut und kann mit Rebind auf eine andere Zeile umgestellt werden (Handles anlegen ist teuer, UX-5e)
        public EquipRow(EquipAssign a, Catalog cat, string product, Action changed, Action<EquipAssign> remove, bool allowNone)
        {
            name.AutoEllipsis = true;
            cbId.DropDownStyle = ComboBoxStyle.DropDown;   // frei eintippbar (Notfall-/Ausweichgeraet)
            cbFb.DropDownStyle = ComboBoxStyle.DropDown;
            chkAlt.Text = "abwechselnd";
            btnAlt.Text = "…";
            Ui.SimpleMenu(note);
            plus.Text = "+"; plus.Click += delegate { if (addBelow != null) addBelow(this.a); };
            del.Text = "✕"; del.Click += delegate { this.remove(this.a); };
            grip = new RowGrip(this); up.Text = "▲"; down.Text = "▼"; up.Visible = down.Visible = false;
            up.Click += delegate { if (Reorder != null) Reorder.Step(this, -1); }; down.Click += delegate { if (Reorder != null) Reorder.Step(this, 1); };
            Controls.AddRange(new Control[] { name, cbId, chkAlt, btnAlt, cbFb, note, del, plus, grip, up, down });
            foreach (Button b in new[] { btnAlt, del, plus, up, down }) Theme.Current.StyleButton(b);
            Enter += delegate { ShowArrows(); }; Leave += delegate { ShowArrows(); };
            ContextMenuStrip cm = new ContextMenuStrip(); ToolStripMenuItem para = new ToolStripMenuItem("Leerzeile danach");   // Absatz im Plantext (EquipAssign.ParaAfter)
            para.Click += delegate { this.a.ParaAfter = !this.a.ParaAfter; SetName(); this.changed(); };
            cm.Opening += delegate { para.Checked = this.a.ParaAfter; };
            cm.Items.Add(para); ContextMenuStrip = cm; name.ContextMenuStrip = cm;
            tip.SetToolTip(btnAlt, "Geräte für den Wechsel auswählen"); tip.SetToolTip(del, "Zeile entfernen"); tip.SetToolTip(plus, "Dieselbe Position direkt darunter noch einmal einfügen (z. B. weiterer Schlauch)");
            tip.SetToolTip(cbFb, "Ausweich-Gerät (Backup), falls das geplante ausfällt." + UndoTip);
            tip.SetToolTip(note, "Notiz/Bezeichnung zu diesem Gerät (steht im Plan hinter der Nummer).");
            tip.SetToolTip(grip, "Zeile verschieben: am Griff nach oben oder unten ziehen (oder ▲▼ nutzen). Die Reihenfolge steht so im Plan.");
            tip.SetToolTip(up, "Eine Zeile nach oben"); tip.SetToolTip(down, "Eine Zeile nach unten");

            cbId.TextChanged += delegate
            {
                if (loading || chkAlt.Checked) return;
                if (this.allowNone && cbId.Text == NoneText)   // Zeile weglassen (nach dem Auswahlereignis, damit das Feld nicht mitten im Ereignis verschwindet)
                {
                    EquipAssign x = this.a; Action<EquipAssign> rm = this.remove;
                    if (IsHandleCreated) BeginInvoke(new Action(delegate { rm(x); })); else rm(x);
                    return;
                }
                this.a.Choices.Clear();
                if (cbId.Text.Trim().Length > 0) this.a.Choices.Add(new EquipChoice(this.a.Short, cbId.Text.Trim()));
                Warn(); this.changed();
            };
            cbFb.TextChanged += delegate { if (!loading) { this.a.Fallback = cbFb.Text.Trim(); this.changed(); } };
            // Stammdaten: Ersatzgeraete des gewaehlten Geraets zuerst vorschlagen; Hinweise (Reihen-Notiz, Wartung, Regeln) als Tooltip
            cbFb.DropDown += delegate { EnsureFallbacks(); SuggestFallbacks(); DropMain(); };
            cbId.DropDown += delegate { EnsureItems(); }; cbId.Enter += delegate { EnsureItems(); };   // Liste beim Aufklappen/Fokus fuellen (Pfeiltasten bleiben nutzbar)
            cbFb.Enter += delegate { EnsureFallbacks(); };
            cbId.TextChanged += delegate { ShowInfo(); };
            note.TextChanged += delegate { if (!loading) { this.a.Note = note.Text.Trim(); this.changed(); } };
            chkAlt.CheckedChanged += delegate
            {
                if (loading) return;
                this.a.Alternate = chkAlt.Checked;
                if (this.a.Alternate && this.a.Choices.Count < 2) PickAlternatives();
                if (!this.a.Alternate && this.a.Choices.Count > 1) this.a.Choices.RemoveRange(1, this.a.Choices.Count - 1);
                loading = true; Refill(); loading = false; this.changed();
            };
            btnAlt.Click += delegate { PickAlternatives(); loading = true; Refill(); loading = false; this.changed(); };
            this.cat = cat;
            Load(a, product, changed, remove, allowNone);
        }

        // Auf eine andere Zeile umstellen (aus dem Pool): Inhalt, Listen und Zustand wie neu, Steuerelemente und Handles bleiben
        public void Rebind(EquipAssign a, Catalog cat, string product, Action changed, Action<EquipAssign> remove, bool allowNone)
        {
            this.cat = cat; Reorder = null; AddBelow = null; Slot = -1; canUp = canDown = false; hot = false; grip.Hot = false;
            up.Visible = down.Visible = false;
            Load(a, product, changed, remove, allowNone);
        }

        void Load(EquipAssign a, string product, Action changed, Action<EquipAssign> remove, bool allowNone)
        {
            loading = true;
            this.a = a; this.product = product; this.pos = cat.Position(a.Short); this.changed = changed; this.remove = remove; this.allowNone = allowNone;
            SetName();
            itemsStale = fbStale = true;   // Listen erst beim Aufklappen fuellen (EnsureItems/EnsureFallbacks)
            bool fix = cat.IsFixed(product, a.Short);          // festes Geraet je Kampagne: nur Abschalten einer vorhandenen Rotation erlaubt
            chkAlt.Enabled = !fix || a.Alternate;   // Sichtbarkeit von abwechselnd/Ausweich setzt Arrange (nur bei Mehr Optionen oder gesetztem Wert)
            tip.SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange)." + UndoTip);
            SetFlag(false);
            tip.SetToolTip(chkAlt, fix ? "Für dieses Produkt ist ein festes Gerät je Kampagne vorgegeben – kein Wechsel." : "Läufe wechseln reihum zwischen den gewählten Geräten (Lauf 1 → A, Lauf 2 → B, Lauf 3 → wieder A …). Zur Auswahl stehen alle Geräte, die laut Admin gleich eingesetzt werden können.");
            tip.SetToolTip(del, allowNone ? "Position entfernen (wie „" + NoneText + "“)" : "Position aus diesem Schritt entfernen");
            chkAlt.Checked = a.Alternate; cbFb.Text = a.Fallback; note.Text = a.Note;
            Refill();
            ShowInfo();
            loading = false;
        }

        // Positionsname; "¶" = Leerzeile danach im Plantext
        void SetName()
        {
            name.Text = (a.ParaAfter ? "¶ " : "") + (pos != null && pos.Name != a.Short ? pos.Name + " (" + a.Short + ")" : a.Short);
            tip.SetToolTip(name, name.Text + (a.ParaAfter ? "\nLeerzeile danach (Rechtsklick ändert das)" : "\nRechtsklick: Leerzeile danach"));
        }

        public void Arrange(int w, bool wide)
        {
            bool rot = (pos != null && pos.AllowRotation) || a.Alternate;
            if (arrW == w && arrWide == wide && arrRot == rot) return;   // nichts zu tun: keine Fensternachrichten
            bool resized = arrW != w || arrWide != wide;
            arrW = w; arrWide = wide; arrRot = rot;
            int[] x = Cols(w, wide);
            int h = cbId.Height;
            Size = new Size(x[6], h + 4);
            grip.SetBounds(0, 2, GripW, h);
            Font small = FontCache.Get(Font.Name, Math.Max(6.5f, Font.SizeInPoints - 2f), FontStyle.Regular, Font);
            up.Font = down.Font = small; up.Padding = down.Padding = Padding.Empty;
            up.SetBounds(GripW + 2, 2, ArrowW, h); down.SetBounds(GripW + 2 + ArrowW, 2, ArrowW, h);
            name.SetBounds(x[0] + 2, 2, x[1] - Gap - x[0] - 2, h);
            name.TextAlign = ContentAlignment.MiddleLeft;
            cbId.SetBounds(x[1], 2, x[2] - x[1] - Gap, h);
            chkAlt.Visible = btnAlt.Visible = wide && rot; cbFb.Visible = wide;
            chkAlt.SetBounds(x[2], 2, AltW - 30, h);
            btnAlt.SetBounds(x[2] + AltW - 28, 2, 28, h);
            cbFb.SetBounds(x[3], 2, Math.Max(0, x[4] - x[3] - Gap), h);
            note.SetBounds(x[4], 2, x[5] - x[4] - Gap, h);
            del.SetBounds(x[5], 2, DelW, h); plus.SetBounds(x[5] + DelW + 2, 2, PlusW, h);
            if (resized) foreach (ComboBox c in new[] { cbId, cbFb }) if (!c.Focused) c.SelectionLength = 0;   // Groessenaenderung markiert sonst den ganzen Text
        }

        // Knoepfe nur bei Hover/Fokus und nur, wenn es etwas zu verschieben gibt (Rand der Gruppe: der Knopf ist deaktiviert)
        public void SetOrder(int slot, bool canUp, bool canDown)
        {
            Slot = slot; this.canUp = canUp; this.canDown = canDown;
            grip.Visible = Reorder != null && (canUp || canDown);
            up.Enabled = canUp; down.Enabled = canDown;
            ShowArrows();
        }

        internal void SetHot(bool on) { if (hot == on) return; hot = on; grip.Hot = on; grip.Invalidate(); ShowArrows(); }

        void ShowArrows() { up.Visible = down.Visible = Reorder != null && (canUp || canDown) && (hot || ContainsFocus); }

#if TESTHOOKS
        internal bool PinHot;
        internal Button TestUp { get { return up; } }
        internal Button TestDown { get { return down; } }
        internal Button TestDel { get { return del; } }
        internal Control TestGrip { get { return grip; } }
        internal ComboBox TestIdBox { get { EnsureItems(); return cbId; } }
        internal CheckBox TestAltBox { get { return chkAlt; } }
#endif

        // Auswahlliste Geraet: erlaubte Geraete (Produktprofil, sonst global) und Platzhalter; der Text bleibt
        void EnsureItems()
        {
            if (!itemsStale) return;
            itemsStale = false;
            bool was = loading; loading = true; string text = cbId.Text;
            cbId.Items.Clear();
            List<string> own = cat.Allowed(product, a.Short);   // nur erlaubte Geraete (Produktprofil, sonst global)
            string ph = Placeholder(cat, product, a.Short);
            if (allowNone) cbId.Items.Add(NoneText);
            cbId.Items.AddRange(own.ToArray()); if (ph.Length > 0) cbId.Items.Add(ph);
            cbId.Text = text; loading = was;
        }

        // Auswahlliste Ausweich-Geraet: Austauschgruppe des Produkts zuerst, dann gleichwertige Positionen
        void EnsureFallbacks()
        {
            if (!fbStale) return;
            fbStale = false;
            bool was = loading; loading = true; string text = cbFb.Text;
            cbFb.Items.Clear();
            foreach (string id in cat.SwapsFor(product, a.Short, "")) cbFb.Items.Add(id);
            foreach (EquipChoice c in cat.Interchangeable(a.Short)) { string t = c.Short == a.Short ? c.Id : c.Short + " " + c.Id; if (!cbFb.Items.Contains(t)) cbFb.Items.Add(t); }
            cbFb.Text = text; loading = was;
        }

        string Display(EquipChoice c) { return c.Short == a.Short ? c.Id : c.ToString(); }

        void Refill()
        {
            cbId.Enabled = !chkAlt.Checked; btnAlt.Enabled = chkAlt.Checked;
            if (chkAlt.Checked) { List<string> d = new List<string>(); foreach (EquipChoice c in a.Choices) d.Add(Display(c)); cbId.Text = string.Join(" / ", d.ToArray()); }
            else cbId.Text = a.Choices.Count > 0 ? a.Choices[0].Id : "";
            Warn();
        }

        void Warn()
        {
            EquipDevice dev = a.Alternate || a.Choices.Count == 0 ? null : cat.Master.Device(a.Choices[0].Id);
            cbId.ForeColor = dev != null && !dev.Active ? Color.Firebrick : Known(a, cat, product) ? SystemColors.WindowText : Color.DarkOrange;   // rot = in Wartung
        }

        string CurrentId() { return a.Alternate || a.Choices.Count == 0 ? "" : a.Choices[0].Id; }

        // Das Hauptgeraet der Zeile (alle gewaehlten) gehoert nicht in die Ausweich-Liste.
        void DropMain()
        {
            bool was = loading; loading = true; string text = cbFb.Text;
            foreach (EquipChoice c in a.Choices)
                for (int i = cbFb.Items.Count - 1; i >= 0; i--)
                { string t = cbFb.Items[i].ToString(); if (t == c.Id || t.EndsWith(" " + c.Id)) cbFb.Items.RemoveAt(i); }
            cbFb.Text = text; loading = was;
        }

#if TESTHOOKS
        public List<string> FallbackItems()   // Selbsttest: Inhalt des Ausweich-Dropdowns nach dem Aufklappen
        {
            EnsureFallbacks(); SuggestFallbacks(); DropMain();
            List<string> l = new List<string>(); foreach (object o in cbFb.Items) l.Add(o.ToString()); return l;
        }
#endif

        void SuggestFallbacks()
        {
            string id = CurrentId(); if (id.Length == 0) return;
            string text = cbFb.Text;
            List<string> first = cat.SwapsFor(product, a.Short, id);   // zuerst Austauschgruppe des Produkts, dann Ersatz
            foreach (EquipSubst s in cat.Master.SubstitutesFor(id)) if (!first.Contains(s.Id)) first.Add(s.Id);
            foreach (string c in cat.Master.CompatibleWith(id)) { EquipDevice d = cat.Master.Device(c); if (d != null && d.Class == (cat.Master.Device(id) != null ? cat.Master.Device(id).Class : d.Class) && !first.Contains(c)) first.Add(c); }
            if (first.Count == 0) return;
            bool was = loading; loading = true;   // N7: vorherigen Zustand wiederherstellen (Aufruf auch waehrend Load)
            for (int i = cbFb.Items.Count - 1; i >= 0; i--) if (first.Contains(cbFb.Items[i].ToString())) cbFb.Items.RemoveAt(i);
            for (int i = first.Count - 1; i >= 0; i--) cbFb.Items.Insert(0, first[i]);
            cbFb.Text = text; loading = was;
        }

        void ShowInfo()
        {
            string id = CurrentId(), info = id.Length > 0 ? cat.Master.Info(id) : "";
            foreach (EquipSubst s in id.Length > 0 ? cat.Master.SubstitutesFor(id) : new List<EquipSubst>())
                info += (info.Length > 0 ? "\n" : "") + "Ersatz: " + s.Id + (s.Note.Length > 0 ? " (" + s.Note + ")" : "");
            if (!Known(a, cat, product)) info = "ACHTUNG: " + id + " ist " + (cat.Product(product) != null ? "für Produkt " + product + " nicht vorgesehen." : "nicht validiert.") + (info.Length > 0 ? "\n" + info : "");
            tip.SetToolTip(cbId, "Gerät aus der validierten Liste wählen oder eigenes eintippen (nicht validierte Eingabe wird orange)." + UndoTip + (info.Length > 0 ? "\n\n" + info : ""));
        }

        void PickAlternatives()
        {
            List<EquipChoice> offer = new List<EquipChoice>();   // erlaubte Geraete des Produkts + Positionen der Austausch-Gruppe
            foreach (string id in cat.Allowed(product, a.Short)) offer.Add(new EquipChoice(a.Short, id));
            foreach (EquipChoice c in cat.Interchangeable(a.Short)) if (!string.Equals(c.Short, a.Short, StringComparison.OrdinalIgnoreCase)) offer.Add(c);
            using (AlternativesPicker p = new AlternativesPicker(offer, a.Choices, a.Short))
                if (p.ShowDialog(this) == DialogResult.OK) a.Choices = p.Result;
            if (a.Choices.Count < 2) { a.Alternate = false; loading = true; chkAlt.Checked = false; loading = false; }
        }
    }
}
