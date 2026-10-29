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
            return s;
        }

        public bool IsEmpty
        {
            get
            {
                if (Title.Length > 0 || Text.Length > 0 || Note.Length > 0 || Ident.Trim().Length > 0 || IdentStart.Length > 0 || IdentOver.Count > 0 || ManualNo.Length > 0 || Kind == StepKind.SubRun) return false;
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

        // Kurzbezeichnung fuer Tabellen (z. B. "Lauf 13570030").
        public string Label(string prefix, int run)
        {
            string t = Title.Length > 0 ? Title : (Kind == StepKind.SubRun ? "Unterlauf" : "Schritt");
            return Numbered ? t + " " + RunNo(prefix, run) : t;
        }

        public string Compose(int run, string prefix) { return string.Join("\n", Lines(run, prefix).ToArray()); }

        // Puffer-Schritt (Variante gesetzt): erzeugt Puffer statt Inhaltstext
        public bool IsBuffer { get { return Variant.Trim().Length > 0 && VariantSet.Of(Variant).Count > 0; } }

        // Puffer dieses Laufs (Ids vergibt der Block beim Anhaengen): Name = Titelzeile, Typ = Variante, Text = die uebrigen Zeilen
        // (Ansatz, Equipment, Notiz). "mobil|stationaer" = zwei Puffer gleichen Namens, "mobil:2" = zwei mobile.
        // PufferCount (1..9) Puffer gleichen Typs am selben Tag je Vorkommen. Namen: PufferNames (je Puffer einer, "|"-getrennt);
        // fehlt einer: Titelzeile, ab dem 2. Puffer "Titel 2" usw. Equipment: die Zeilen des Schritts gelten fuer jeden Puffer; bei
        // "abwechselnd" wird reihum verteilt (Puffer k im Lauf r nimmt Geraet r*n+k).
        public List<BufferItem> BuffersFor(int run, string prefix)
        {
            int n = Math.Max(1, PufferCount);
            List<BufferItem> res = new List<BufferItem>();
            string[] names = BufferNames();
            int k = 0;
            foreach (KeyValuePair<string, int> kv in VariantSet.Counts(Variant))
                for (int i = 0; i < kv.Value; i++)
                    for (int j = 0; j < n; j++, k++)
                    {
                        List<string> lines = Lines(run, prefix, run * n + j);
                        string name = "";
                        if (Title.Length > 0 && lines.Count > 0) { name = lines[0]; lines.RemoveAt(0); }
                        if (k < names.Length && names[k].Length > 0) name = names[k];
                        else if (n > 1 && k > 0 && name.Length > 0) name += Numbered ? " (" + (k + 1) + ")" : " " + (k + 1);   // "Titel 2"; bei "Titel 1, 2, 3" (Wiederholung) "Titel 1 (2)"
                        BufferSet.Add(res, name, kv.Key, string.Join("\n", lines.ToArray()));
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
            foreach (EquipAssign a in Equipment) { string l = a.Line(alt); if (l.Length > 0) lines.Add(l); }
            lines.AddRange(NoteTexts());   // Notizzeilen ohne Praefix (im Plan kursiv, siehe EntryFor)
            if (IdentFor(run).Length > 0) lines.Add(IdentFor(run));   // immer die letzte Zeile (Format in BuildTargets)
            return lines;
        }

        // Block, wie ihn dieser Lauf allein im Plan ergibt (Plantext, rote Kennung, Herstellungsvariante), z. B. fuer die Vorschau.
        // Notizen des Schritts: je Zeile (Enter im Notizfeld) eine Notiz
        public List<string> NoteTexts()
        {
            List<string> res = new List<string>();
            foreach (string n in Note.Replace("\r", "").Split('\n')) if (n.Trim().Length > 0) res.Add(n.Trim());
            return res;
        }

        public Entry EntryFor(int run, string prefix)
        {
            Entry e = new Entry();
            if (IsBuffer) { e.Buffers = BuffersFor(run, prefix); return e; }
            List<string> lines = Lines(run, prefix);
            e.Content = string.Join("\n", lines.ToArray());
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
