using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arbeitsplan.Core
{
    // Produktcode und Laufnummern: der Produktcode ist frei (IsProductCode), die 8-stellige Laufnummer XXYYZZZZ davon unabhaengig.
    // Das Paket merkt sich das Laufnummern-Praefix XXYY (RunPrefix); alte Pakete (Produktcode = 4 Ziffern) nutzen den Code als Praefix.
    public partial class Package
    {
        public string RunPrefix = "";       // XXYY = erste 4 Stellen der Laufnummer; "" = alte Pakete (dann Produktcode, falls 4 Ziffern)
        public const string CodeChars = "-_./+";   // erlaubte Sonderzeichen im Produktcode
        public DayOfWeek Day1 = DayOfWeek.Monday;  // Wochentag von Tag 1 fuer Schedule/Overflow (Kampagnenstart setzt ihn), nicht gespeichert

        // Wirksames Praefix der Laufnummern ("" = Laeufe ohne 8-stellige Nummer)
        public string Prefix { get { return IsCode4(RunPrefix) ? RunPrefix : (IsCode4(ProductCode) ? ProductCode : ""); } }

        public static bool IsCode4(string s)
        {
            if (s == null || s.Length != 4) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        // Produktcode: 2-12 Zeichen aus Buchstaben, Ziffern und - _ . / + (keine Leerzeichen), mind. ein Buchstabe oder eine Ziffer,
        // z. B. "AB1", "AB12345678-", alt "1357". Gross-/Kleinschreibung bleibt erhalten.
        public static bool IsProductCode(string s)
        {
            if (s == null || s.Length < 2 || s.Length > 12) return false;
            bool any = false;
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) any = true;
                else if (CodeChars.IndexOf(c) < 0) return false;
            return any;
        }

        // Laufnummer als Text: mit Praefix 8-stellig (z. B. "13570030"), sonst die Zahl.
        public static string FormatRun(string prefix, int no)
        {
            return IsCode4(prefix) ? prefix + no.ToString("0000", CultureInfo.InvariantCulture) : no.ToString(CultureInfo.InvariantCulture);
        }

        // Eingabe "Erste Laufnummer": 8 Ziffern (XXYY + ZZZZ) oder 1-4 Ziffern ohne Praefix; false = ungueltig.
        public static bool ParseRun(string text, out string prefix, out int no)
        {
            prefix = ""; no = -1;
            string s = (text ?? "").Trim();
            if (s.Length == 0) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            if (s.Length == 8) { prefix = s.Substring(0, 4); no = int.Parse(s.Substring(4), CultureInfo.InvariantCulture); return true; }
            if (s.Length > 4) return false;
            no = int.Parse(s, CultureInfo.InvariantCulture);
            return true;
        }

        // Erste Laufnummer (z. B. "13570030"); "" = keine Lauf-Schritte.
        public string FirstRun { get { return FirstRunNo < 0 ? "" : FormatRun(Prefix, FirstRunNo); } }

        // Erste Laufnummer setzen: Praefix merken, Lauf-Schritte umnummerieren (Folgelaeufe zaehlen ZZZZ hoch). false = ungueltige Eingabe.
        public bool SetFirstRun(string text)
        {
            string p; int no;
            if (!ParseRun(text, out p, out no)) return false;
            RunPrefix = p;
            Renumber(no);
            return true;
        }

        // Lauf, dessen hintere 4 Stellen ueber 9999 hinauslaufen wuerden; null = alles passt.
        public string Overflow()
        {
            foreach (PackageStep s in Steps)
                if (s.Numbered && !s.IsEmpty && s.StartNo + s.RunCountFor(Day1) - 1 > 9999)
                    return (s.Title.Length > 0 ? s.Title : "Schritt") + ": " + s.RunCountFor(Day1) + " Läufe ab " + FormatRun(Prefix, s.StartNo)
                        + " – die letzten 4 Stellen laufen über 9999. Bitte eine kleinere erste Laufnummer wählen.";
            return null;
        }

        // ---------- Kampagnenstart: Laufnummern und Termine ----------

        // Kleinste Startnummer der Haupt-/Unterlauf-Schritte, -1 = keine.
        public int FirstRunNo
        {
            get
            {
                int m = -1;
                foreach (PackageStep s in Steps) if (s.Kind != StepKind.Normal && (m < 0 || s.StartNo < m)) m = s.StartNo;
                return m;
            }
        }

        // Laufnummern so verschieben, dass der erste Lauf "first" hat (Abstaende zwischen den Schritten bleiben).
        public void Renumber(int first)
        {
            int d = first - FirstRunNo;
            if (FirstRunNo < 0 || d == 0) return;
            foreach (PackageStep s in Steps) if (s.Kind != StepKind.Normal) s.StartNo = Clamp(s.StartNo + d, 0, 99999);   // ueber 9999: Overflow() meldet es
        }

        // Alle Laeufe aller Schritte mit Tag laut Paket-Rhythmus (Tag, Abstand).
        public List<RunDate> Schedule() { return Schedule(null); }

        // Wie Schedule(); gaps = Abstaende zwischen den Hauptlaeufen (z. B. aus einer frueheren Kampagne), fehlende = Paket-Abstand.
        // Alles, was zu einem Hauptlauf gehoert (Schritte ab seinem Tag bis zum naechsten), wandert mit ihm.
        public List<RunDate> Schedule(List<int> gaps)
        {
            int main = -1;   // Lauf-Schritt: erster Hauptlauf-Schritt mit den meisten Wiederholungen
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Kind == StepKind.Run && !Steps[i].IsEmpty && (main < 0 || Steps[i].RunCountFor(Day1) > Steps[main].RunCountFor(Day1))) main = i;
            List<int> mainDay = new List<int>(), shift = new List<int>();
            if (main >= 0)
            {
                mainDay = Steps[main].Days(Day1);
                for (int r = 0; r < mainDay.Count; r++)
                {
                    int plan = r == 0 ? 0 : mainDay[r] - mainDay[r - 1];   // geplanter Abstand (alle x Tage, Wochentage, Tagesliste)
                    shift.Add(r == 0 ? 0 : shift[r - 1] + (gaps != null && r - 1 < gaps.Count ? gaps[r - 1] : plan) - plan);
                }
            }
            List<RunDate> res = new List<RunDate>();
            for (int i = 0; i < Steps.Count; i++)
            {
                PackageStep s = Steps[i];
                if (s.IsEmpty) continue;
                List<int> days = s.Days(Day1);
                for (int r = 0; r < days.Count; r++)
                {
                    RunDate d = new RunDate(); d.Step = i; d.Run = r; d.Day = days[r];
                    d.Label = s.Label(Prefix, r); d.Row = s.Row;
                    for (int k = 0; k < mainDay.Count; k++) if (mainDay[k] <= d.Day) d.Group = k;
                    if (i == main) d.Group = r;
                    if (d.Group >= 0) d.Day += shift[d.Group];
                    res.Add(d);
                }
            }
            return res;
        }

        // Hauptlauf-Nummern (ZZZZ) mit diesem Laufnummern-Praefix (product = XXYY, siehe Prefix) im Plan mit erstem Datum, nach Nummer
        // sortiert. Unterlauf-Zeilen zaehlen nicht. before: nur Laeufe vor diesem Tag (Kampagnenstart); default = ganzer Bestand. Parser: RunNumbers.Find.
        public static List<KeyValuePair<int, DateTime>> PastRuns(IDataStore store, string product, DateTime before = default(DateTime))
        {
            List<KeyValuePair<int, DateTime>> res = new List<KeyValuePair<int, DateTime>>();
            if (!IsCode4(product)) return res;
            Dictionary<int, DateTime> seen = new Dictionary<int, DateTime>();
            foreach (SearchHit h in store.Find(product))
            {
                if (before != default(DateTime) && h.Day >= before.Date) continue;
                Entry e = store.Get(h.Day, h.RowId);
                if (e == null) continue;
                foreach (RunRef r in RunNumbers.Find(e, product))
                {
                    int n = int.Parse(r.Run, CultureInfo.InvariantCulture);
                    DateTime t;
                    if (!seen.TryGetValue(n, out t) || h.Day < t) seen[n] = h.Day;
                }
            }
            res.AddRange(seen);
            res.Sort(delegate(KeyValuePair<int, DateTime> a, KeyValuePair<int, DateTime> b) { return a.Key.CompareTo(b.Key); });
            return res;
        }

        // Frueherer Kampagnen-Stand eines Produkts mit EINER Suche im Bestand (fuer LastRun und PastGaps zugleich).
        public class RunHistory
        {
            public bool Found;
            public int No = -1, Interval;           // hoechste Hauptlauf-Nummer (ZZZZ), haeufigster Abstand (0 = unbekannt)
            public DateTime Day = DateTime.MinValue;
            public List<int> Gaps = new List<int>();
        }

        public static RunHistory History(IDataStore store, string product, DateTime before = default(DateTime))
        {
            List<KeyValuePair<int, DateTime>> runs = PastRuns(store, product, before);
            RunHistory h = new RunHistory();
            h.Gaps = GapsOf(runs);
            h.Found = LastOf(runs, out h.No, out h.Day, out h.Interval);
            return h;
        }

        // Abstaende (Tage) zwischen aufeinanderfolgenden Laeufen der letzten Kampagne (fortlaufende Nummern bis zur hoechsten,
        // hoechstens 30 Tage auseinander), aeltester zuerst. Leer = keine frueheren Laeufe.
        public static List<int> PastGaps(IDataStore store, string product, DateTime before = default(DateTime))
        {
            return GapsOf(PastRuns(store, product, before));
        }

        static List<int> GapsOf(List<KeyValuePair<int, DateTime>> runs)
        {
            List<int> gaps = new List<int>();
            for (int i = runs.Count - 1; i > 0; i--)
            {
                int g = (runs[i].Value - runs[i - 1].Value).Days;
                if (runs[i - 1].Key != runs[i].Key - 1 || g < 1 || g > 30) break;
                gaps.Insert(0, g);
            }
            return gaps;
        }

        // Fruehere Kampagnen desselben Produkts im Plan: hoechste Hauptlauf-Nummer (ZZZZ) mit Datum und haeufigster Abstand
        // zwischen aufeinanderfolgenden Nummern (0 = unbekannt). false = nichts gefunden.
        public static bool LastRun(IDataStore store, string product, out int no, out DateTime day, out int interval, DateTime before = default(DateTime))
        {
            return LastOf(PastRuns(store, product, before), out no, out day, out interval);
        }

        static bool LastOf(List<KeyValuePair<int, DateTime>> runs, out int no, out DateTime day, out int interval)
        {
            no = -1; day = DateTime.MinValue; interval = 0;
            if (runs.Count == 0) return false;
            no = runs[runs.Count - 1].Key; day = runs[runs.Count - 1].Value;
            Dictionary<int, int> gaps = new Dictionary<int, int>();
            for (int i = 1; i < runs.Count; i++)
                if (runs[i].Key == runs[i - 1].Key + 1 && runs[i].Value > runs[i - 1].Value)
                {
                    int g = (runs[i].Value - runs[i - 1].Value).Days, c;
                    gaps.TryGetValue(g, out c); gaps[g] = c + 1;
                }
            int best = 0;
            foreach (KeyValuePair<int, int> kv in gaps) if (kv.Value > best) { best = kv.Value; interval = kv.Key; }
            return true;
        }
    }
}
