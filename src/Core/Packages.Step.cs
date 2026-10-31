using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Ablaufschritt einer Kampagne: wann (Tag relativ zum Start), in welchem Block, was, mit welchem Equipment; optional wiederholt
    // (Wiederholmodi, Versatz je Lauf, Ansatz: Packages.Repeat.cs).
    public partial class PackageStep
    {
        public string Title = "";           // z. B. "Lauf", "Packen"
        public string Source = "";          // Name des Bausteins, aus dem der Schritt stammt
        public string Row = "";
        public int Day = 1;                 // 1 = erster Tag
        public int Count = 1;               // Wiederholungen (z. B. 6 Laeufe)
        public int Interval = 1;            // Abstand in Tagen
        public int StartNo = 1;             // Nummer des ersten Laufs ("Lauf 1"); mit Produktcode die 4 hinteren Stellen (ZZZZ)
        public StepKind Kind;
        public string SubNos = "";          // nur SubRun: Unterlauf-Nummern je Lauf, Komma-getrennt; mehrere je Lauf mit "/"
        public string Text = "";
        public string Note = "";
        public string Ident = "";           // alte Kennung je Lauf, Komma-getrennt (z. B. "PA 40010001, PA 40010017"); neu: IdentStart/IdentOver (Packages.Bind.cs); ganz unten rot, gross, fett
        public string Variant = "";         // Puffer-Schritt: Typ(en) der erzeugten Puffer (VariantSet, z. B. "mobil"), "" = kein Puffer
        public int PufferCount = 1;         // Puffer-Schritt: so viele Puffer des Typs je Vorkommen am selben Tag (1..9, XML "puffer")
        public string PufferNames = "";     // Namen der Puffer, "|"-getrennt (XML "pufnamen"); leer = Titel (+ Nummer)
        public List<EquipAssign> Equipment = new List<EquipAssign>();
        public FreezeItem Freeze;           // Vorlage (Baustein "Freeze / Thaw"), null = nur Text (z. B. Thaw). Start1to3[0] = Vorgabe-Uhrzeit (Datum egal), nicht gesetzt = Zeit offen

        // Format der Kennungszeile (rot, gross, fett).
        public static FormatSpan IdentSpan(int start, int length)
        {
            FormatSpan f = new FormatSpan(); f.Start = start; f.Length = length; f.Rgb = 0xFF0000; f.Size = 14f; f.Bold = true;
            return f;
        }

        public PackageStep Clone()
        {
            PackageStep s = (PackageStep)MemberwiseClone();
            s.Shift = new Dictionary<int, int>(Shift);
            s.CopyBind(this);
            s.Equipment = new List<EquipAssign>();
            foreach (EquipAssign a in Equipment) s.Equipment.Add(a.Clone());
            ClonePuffers(s);
            if (Freeze != null) s.Freeze = Freeze.Clone();
            return s;
        }

        public bool IsEmpty
        {
            get
            {
                if (Title.Length > 0 || Text.Length > 0 || Note.Length > 0 || Ident.Trim().Length > 0 || IdentStart.Length > 0 || IdentOver.Count > 0 || ManualNo.Length > 0 || Kind == StepKind.SubRun || Puffers.Count > 0 || Freeze != null) return false;
                foreach (EquipAssign a in Equipment) if (a.Line(0).Length > 0) return false;
                return true;
            }
        }

        public bool Numbered { get { return Kind != StepKind.Normal || (Bind == StepBind.JeLauf ? NumberRuns : RunCount > 1); } }

        // Laufnummer: mit Laufnummern-Praefix (XXYY, Package.Prefix) 8-stellig XXYYZZZZ (z. B. 13570030), sonst wie bisher "3".
        public string RunNo(string prefix, int run)
        {
            return Kind != StepKind.Normal ? Package.FormatRun(prefix, StartNo + run) : (StartNo + run).ToString(CultureInfo.InvariantCulture);
        }

        // Erste Unterlauf-Nummer dieses Laufs als 8 Ziffern; "" = keine. shortcut: je Vorgang nur ein Unterlauf, weitere ("/") bleiben als Textzeile im Block; bei Bedarf je Unterlauf ein Vorgang.
        public string SubNoFor(int run, string prefix)
        {
            string[] per = SubNos.Split(',');
            if (Kind != StepKind.SubRun || run >= per.Length) return "";
            string n = per[run].Split('/')[0].Trim();
            if (Package.IsCode4(n) && Package.IsCode4(prefix)) n = prefix + n;
            return System.Text.RegularExpressions.Regex.IsMatch(n, @"^\d{8}$") ? n : "";
        }

        // Kurzbezeichnung fuer Tabellen (z. B. "Lauf 13570030").
        public string Label(string prefix, int run)
        {
            string t = Title.Length > 0 ? Title : (Kind == StepKind.SubRun ? "Unterlauf" : "Schritt");
            return Numbered ? t + " " + RunNo(prefix, run) : t;
        }

        public string Compose(int run, string prefix) { return BufferSet.Plain(string.Join("\n", Lines(run, prefix).ToArray())); }

        // Puffer-Schritt (Variante gesetzt): erzeugt Puffer statt Inhaltstext
        public bool IsBuffer { get { return Puffers.Count > 0 || (Variant.Trim().Length > 0 && VariantSet.Of(Variant).Count > 0); } }

        // Puffer dieses Laufs (Ids vergibt der Block beim Anhaengen), je PufferSpec einer: Name = Titelzeile, Typ = Typ des Puffers, Text = die
        // uebrigen Zeilen (Ansatz, EIGENE Equipment-Zeilen + gemeinsame im Geltungsbereich, Notiz). Name: PufferSpec.Name, sonst Titelzeile (+ NameNo).
        // Alter Modus (Puffers leer): LegacyPuffers() = PufferCount je Typ ("mobil|stationaer" = zwei, "mobil:2" = zwei mobile), PufferNames,
        // alle Zeilen in jedem Puffer, "abwechselnd" reihum (Puffer k im Lauf r nimmt Geraet r*n+k); ab dem 2. Puffer "Titel 2" ("Titel 1 (2)" bei Nummern).
        public List<BufferItem> BuffersFor(int run, string prefix) { return BuffersFor(run, prefix, 0); }

        // lauf: Nummer des bedienten Laufs (BufferItem.LaufRef), 0 = ohne Angabe
        public List<BufferItem> BuffersFor(int run, string prefix, int lauf)
        {
            List<PufferSpec> specs = Puffers.Count > 0 ? Puffers : LegacyPuffers();
            List<BufferItem> res = new List<BufferItem>();
            for (int k = 0; k < specs.Count; k++)
            {
                PufferSpec sp = specs[k];
                List<string> lines = Lines(run, prefix, PufferEquipLines(sp, k, run));
                string name = "";
                if (Title.Length > 0 && lines.Count > 0) { name = lines[0]; lines.RemoveAt(0); }
                if (sp.Name.Length > 0) name = sp.Name;
                else if (sp.NameNo > 1 && name.Length > 0) name += Numbered ? " (" + sp.NameNo + ")" : " " + sp.NameNo;
                BufferSet.Add(res, name, sp.Typ, string.Join("\n", lines.ToArray())).LaufRef = Math.Max(0, lauf);
            }
            return res;
        }

        // Namen aus PufferNames (getrimmt, leere bleiben als "" erhalten)
        public string[] BufferNames()
        {
            if (PufferNames.Length == 0) return new string[0];
            string[] a = PufferNames.Split('|');
            for (int i = 0; i < a.Length; i++) a[i] = a[i].Trim();
            return a;
        }

        List<string> Lines(int run, string prefix) { return Lines(run, prefix, run); }

        // alt: Index fuer "abwechselnd" (normal = run; bei mehreren Puffern je Lauf run*n+Puffer)
        List<string> Lines(int run, string prefix, int alt)
        {
            List<string> eq = new List<string>();
            foreach (EquipAssign a in Equipment) { string l = a.LineM(alt); if (l.Length > 0) { eq.Add(l); if (a.ParaAfter) eq.Add(""); } }
            return Lines(run, prefix, eq);
        }

        // eq: fertige Equipment-Zeilen (alle Zeilen des Schritts oder die eines Puffers)
        List<string> Lines(int run, string prefix, List<string> eq)
        {
            string product = prefix;
            List<string> lines = new List<string>();
            if (Kind == StepKind.SubRun)
            {
                string main = RunNo(product, run);
                if (Title.Length > 0) lines.Add(Title);
                string[] per = SubNos.Split(',');
                bool any = false;
                foreach (string part in (run < per.Length ? per[run] : "").Split('/'))
                {
                    string n = part.Trim();
                    if (n.Length == 0) continue;
                    if (Package.IsCode4(n) && Package.IsCode4(product)) n = product + n;   // "4186" -> "13574186"
                    lines.Add("Unterlauf " + n + " (aus " + main + ")"); any = true;
                }
                if (!any) lines.Add("Unterlauf (aus " + main + ")");
            }
            else if (Numbered && Title.Length > 0) lines.Add(Title + " " + RunNo(product, run));
            else if (ManualNo.Length > 0) lines.Add((Title.Length > 0 ? Title + " " : "") + "(Nr. " + ManualNo + ")");   // Klammer: zaehlt nicht als Kampagnenlauf (RunNumbers)
            else if (Title.Length > 0) lines.Add(Title);
            if (Text.Length > 0) lines.Add(Text);
            if (AnsatzLine().Length > 0) lines.Add(AnsatzLine());   // z. B. "Gebinde: Flasche 5 L"
            lines.AddRange(eq);
            lines.AddRange(NoteTexts());   // Notizzeilen ohne Praefix (im Plan kursiv, siehe EntryFor)
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);   // Leerzeile ganz am Ende faellt weg
            if (IdentFor(run).Length > 0) lines.Add(IdentFor(run));   // immer die letzte Zeile (Format in BuildTargets)
            return lines;
        }

        // Block, wie ihn dieser Lauf allein im Plan ergibt (Plantext, rote Kennung, Herstellungsvariante), z. B. fuer die Vorschau.
        // Notizen des Schritts: je Zeile (Enter im Notizfeld) eine Notiz
        public List<string> NoteTexts()
        {
            List<string> res = new List<string>();
            foreach (string n in Note.Replace("\r", "").Split('\n')) if (n.Trim().Length > 0 || res.Count > 0) res.Add(n.Trim());   // Leerzeilen zwischen Notizen bleiben
            while (res.Count > 0 && res[res.Count - 1].Length == 0) res.RemoveAt(res.Count - 1);
            return res;
        }

        public Entry EntryFor(int run, string prefix)
        {
            Entry e = new Entry();
            if (IsBuffer) { e.Buffers = BuffersFor(run, prefix); return e; }
            List<string> lines = Lines(run, prefix);
            e.Content = string.Join("\n", lines.ToArray());
            for (int i = e.Content.IndexOf(BufferSet.NoteMark); i >= 0; i = e.Content.IndexOf(BufferSet.NoteMark, i + 1))   // Notiz-Teil der Geraetezeile kursiv (UX-5f)
            {
                int end = e.Content.IndexOf('\n', i); if (end < 0) end = e.Content.Length;
                FormatSpan it = new FormatSpan(); it.Start = i + 1; it.Length = end - i - 1; it.Italic = true;
                if (it.Length > 0) e.Spans.Add(it);
            }
            e.Content = BufferSet.Plain(e.Content);   // Marker -> Leerzeichen, Positionen bleiben
            string idt = IdentFor(run);
            int nn = NoteTexts().Count;
            if (nn > 0)
            {
                int first = 0, before = lines.Count - nn - (idt.Length > 0 ? 1 : 0);   // Zeilen des Inhalts vor der ersten Notiz (Text kann mehrzeilig sein)
                for (int k = 0; k < before; k++) first += lines[k].Split('\n').Length;
                Notes.SetNote(e, first, first + nn - 1, true);
            }
            if (idt.Length > 0) e.Spans.Add(IdentSpan(e.Content.Length - idt.Length, idt.Length));
            return e;
        }
    }
}
