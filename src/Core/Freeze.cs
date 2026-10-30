using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    public enum FreezeArt { Tank, Bag }

    // Standardwerte fuer neue Freeze-Vorgaenge (im Katalog, Catalog.FreezeDefaults).
    public class FreezeConfig
    {
        public int GwkAnlagen = 2, Kreislaeufe = 2;   // Tank-Freeze: GWK-Anlage 1..2, Kreislauf 1..2
        public int FreezeAnlagen = 3;                 // Bag-Freeze: Anlagen reihum (Rechenmodell FreezeCalc.Anlagen ist derzeit fest 3)
        public int DauerMin = 360, AbstandMin = 420, Cycles = 14;

        public FreezeConfig Clone() { return (FreezeConfig)MemberwiseClone(); }

        public static void Write(XmlWriter w, FreezeConfig c)
        {
            w.WriteStartElement("Freeze");
            w.WriteAttributeString("gwk", c.GwkAnlagen.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("kreislaeufe", c.Kreislaeufe.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("anlagen", c.FreezeAnlagen.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("dauer", c.DauerMin.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("abstand", c.AbstandMin.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("cycles", c.Cycles.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        // fehlendes Element/Attribut = Standard (alte Dateien)
        public static void Read(XmlElement katalog, FreezeConfig c)
        {
            foreach (XmlNode n in katalog.ChildNodes)
            {
                XmlElement e = n as XmlElement;
                if (e == null || e.Name != "Freeze") continue;
                c.GwkAnlagen = FreezeCalc.Num(e, "gwk", c.GwkAnlagen, 1, 9); c.Kreislaeufe = FreezeCalc.Num(e, "kreislaeufe", c.Kreislaeufe, 1, 9);
                c.FreezeAnlagen = FreezeCalc.Num(e, "anlagen", c.FreezeAnlagen, 1, 9); c.DauerMin = FreezeCalc.Num(e, "dauer", c.DauerMin, 1, FreezeCalc.MaxMin);
                c.AbstandMin = FreezeCalc.Num(e, "abstand", c.AbstandMin, 1, FreezeCalc.MaxMin); c.Cycles = FreezeCalc.Num(e, "cycles", c.Cycles, 1, FreezeCalc.MaxCycles);
                return;
            }
        }
    }

    public partial class Catalog { public FreezeConfig FreezeDefaults = new FreezeConfig(); }

    // Ein Freeze-Vorgang eines Blocks (Tank-Freeze oder eine ganze Bag-Serie), gespeichert im Block am Tag von Cycle 1.
    // Gespeichert werden nur Eingaben (Starts Cycle 1-3, Dauer, Abstand, Anzahl) und korrigierte tatsaechliche Starts (Ist); alles andere rechnet FreezeCalc.
    // Zeiten sind Ortszeit ohne Zone; DateTime.MinValue = nicht gesetzt.
    public class FreezeItem
    {
        public string Id = "";                                         // f1, f2 ... je Block
        public FreezeArt Art = FreezeArt.Bag;
        public int Anlage = 1, Kreislauf = 1;                          // nur Tank: GWK-Anlage, Kreislauf
        public DateTime[] Start1to3 = new DateTime[3];                 // Bag: Cycle 1..3; Tank: [0]; Cycle 1 Pflicht, leere 2/3 werden geschaetzt
        public int DauerMin = 360, AbstandMin = 420, Cycles = 14;      // Abstand = Zeit zwischen zwei Cycles je Anlage
        public SortedDictionary<int, DateTime> Ist = new SortedDictionary<int, DateTime>();   // Cycle -> tatsaechlicher Start
        public string MainRef = "", SubNo = "", Tanks = "", Note = ""; // Hauptlauf (8 Stellen), Unterlauf (8 Stellen oder leer), Tanks (Komma-Liste)

        public FreezeItem Clone()
        {
            FreezeItem f = (FreezeItem)MemberwiseClone();
            f.Start1to3 = (DateTime[])Start1to3.Clone();
            f.Ist = new SortedDictionary<int, DateTime>(Ist);
            return f;
        }

        public bool Valid { get { return Start1to3[0] != DateTime.MinValue; } }

        // Alle Werte ohne Id; Grundlage von SameValue und Key
        public string Core()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Art).Append('|').Append(Anlage).Append('|').Append(Kreislauf).Append('|');
            foreach (DateTime d in Start1to3) sb.Append(FreezeCalc.Fmt(d)).Append(',');
            sb.Append('|').Append(DauerMin).Append('|').Append(AbstandMin).Append('|').Append(Cycles).Append('|');
            foreach (KeyValuePair<int, DateTime> kv in Ist) sb.Append(kv.Key).Append('=').Append(FreezeCalc.Fmt(kv.Value)).Append(',');
            sb.Append('|').Append(MainRef).Append('|').Append(SubNo).Append('|').Append(Tanks).Append('|').Append(Note);
            return sb.ToString();
        }
        public bool SameValue(FreezeItem o) { return o != null && Core() == o.Core(); }
        public string Key() { return Id + "|" + Core() + ";"; }
    }

    // Berechneter Cycle (nie gespeichert)
    public class FreezeCycle
    {
        public FreezeItem Item;
        public int N;                    // 1..14
        public int Anlage;               // Freeze-Anlage 1..3 (Bag); Tank: 1
        public DateTime Planned, Actual, End;
        public int DelayMin;             // Actual - Planned
        public bool HasIst, Estimated;   // tatsaechlicher Start eingetragen / Start aus Cycle 1 geschaetzt (C2/C3 ohne Eingabe)
        public DateTime OriginDay = DateTime.MinValue;   // nur Fortsetzungen: Tag des Blocks mit dem Vorgang (FreezeSet.Continuations)
        public DateTime Day { get { return Actual.Date; } }   // ein Cycle gehoert zum Kalendertag seines Starts
        public string Label { get { return "C" + N; } }
    }

    public static class FreezeCalc
    {
        public const int Anlagen = 3, MaxCycles = 14, MaxMin = 10080;
        const string TimeFmt = "yyyy-MM-ddTHH:mm";

        public static int Anlage(int n) { return ((n - 1) % Anlagen) + 1; }
        public static int Count(FreezeItem f) { return f.Art == FreezeArt.Tank ? 1 : Math.Max(1, Math.Min(MaxCycles, f.Cycles)); }

        public static string Fmt(DateTime d) { return d == DateTime.MinValue ? "" : d.ToString(TimeFmt, CultureInfo.InvariantCulture); }
        public static bool TryParse(string s, out DateTime d) { return DateTime.TryParseExact(s, TimeFmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out d); }
        internal static int Num(XmlElement e, string a, int def, int min, int max)
        {
            int v; return int.TryParse(e.GetAttribute(a), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? Math.Max(min, Math.Min(max, v)) : def;
        }

        // Start von Cycle 1..3: Eingabe, sonst C1 + 1 h (C2) bzw. + 2 h (C3)
        static DateTime StartOf(FreezeItem f, int n)
        {
            DateTime s = f.Start1to3[n - 1];
            if (s != DateTime.MinValue || n == 1) return s;
            DateTime c1 = f.Start1to3[0];
            return c1 == DateTime.MinValue ? c1 : c1.AddHours(n - 1);
        }

        public static bool IsEstimated(FreezeItem f, int n) { return f.Art == FreezeArt.Bag && n >= 2 && n <= 3 && f.Start1to3[n - 1] == DateTime.MinValue && f.Start1to3[0] != DateTime.MinValue; }

        // Plan-Start P(n) ohne tatsaechliche Starts
        public static DateTime Planned(FreezeItem f, int n)
        {
            if (n <= 3) return StartOf(f, n);
            DateTime p = Planned(f, n - 3);
            return p == DateTime.MinValue ? p : p.AddMinutes(f.AbstandMin);
        }

        // E(n) = Ist[n], sonst n <= 3: Start[n], sonst E(n-3) + Abstand (nur Folge-Cycles derselben Anlage ziehen nach)
        public static DateTime Actual(FreezeItem f, int n)
        {
            DateTime d;
            if (f.Ist.TryGetValue(n, out d)) return d;
            if (n <= 3) return StartOf(f, n);
            DateTime p = Actual(f, n - 3);
            return p == DateTime.MinValue ? p : p.AddMinutes(f.AbstandMin);
        }

        public static List<FreezeCycle> Cycles(FreezeItem f)
        {
            List<FreezeCycle> l = new List<FreezeCycle>();
            if (!f.Valid) return l;
            for (int n = 1; n <= Count(f); n++)
            {
                FreezeCycle c = new FreezeCycle(); c.Item = f; c.N = n;
                c.Anlage = f.Art == FreezeArt.Tank ? 1 : Anlage(n);
                c.Planned = Planned(f, n); c.Actual = Actual(f, n); c.End = c.Actual.AddMinutes(f.DauerMin);
                c.DelayMin = (int)Math.Round((c.Actual - c.Planned).TotalMinutes);
                c.HasIst = f.Ist.ContainsKey(n); c.Estimated = IsEstimated(f, n);
                l.Add(c);
            }
            return l;
        }

        // Hinweise (nie Sperre): Abstand kleiner als Dauer; Ist-Start vor dem Ende des vorigen Cycles derselben Anlage
        public static List<string> Hints(FreezeItem f)
        {
            List<string> h = new List<string>();
            if (!f.Valid) return h;
            if (f.Art == FreezeArt.Bag && Count(f) > Anlagen && f.AbstandMin < f.DauerMin)
                h.Add("Abstand (" + Dur(f.AbstandMin) + ") ist kleiner als die Dauer (" + Dur(f.DauerMin) + "): die Anlage ist beim nächsten Cycle noch belegt.");
            if (f.Art == FreezeArt.Bag)
                for (int n = Anlagen + 1; n <= Count(f); n++)
                {
                    DateTime e;
                    if (f.Ist.TryGetValue(n, out e) && e < Actual(f, n - Anlagen).AddMinutes(f.DauerMin))
                        h.Add("C" + n + " startet vor dem Ende von C" + (n - Anlagen) + " (gleiche Anlage).");
                }
            return h;
        }

        // "6 h", "1 h 30 min", "45 min"
        public static string Dur(int min)
        {
            int a = Math.Abs(min), h = a / 60, m = a % 60;
            return (h > 0 ? h + " h" : "") + (h > 0 && m > 0 ? " " : "") + (m > 0 || h == 0 ? m + " min" : "");
        }

        // "+7 h", "-30 min", "" bei 0
        public static string DelayText(int min) { return min == 0 ? "" : (min > 0 ? "+" : "-") + Dur(min); }

        // "12.10. 12:00"
        public static string Short(DateTime d) { return d.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture); }
    }
}
