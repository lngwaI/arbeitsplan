using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Paketformat 7: neue Attribute und die aufgeloesten alten Attribute (Packages.Xml.cs ruft diese Teile auf).
    public partial class Package
    {
#if TESTHOOKS
        public static readonly List<Package> Seen = new List<Package>();   // alle geladenen Pakete (Golden-Test)
#endif
        static readonly string[] TypeKeys = { "kampagne", "einzel", "ereignis" };

        void WritePackage7(XmlWriter w)
        {
            if (typeSet.HasValue) w.WriteAttributeString("typ", TypeKeys[(int)typeSet.Value]);
            if (Lauf1.HasValue) w.WriteAttributeString("lauf1", Lauf1.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (Day1 != DayOfWeek.Monday) w.WriteAttributeString("tag1wochentag", PackageStep.DayNames[((int)Day1 + 6) % 7]);   // Wochentage der Wiederholung haengen davon ab (Normalize verschiebt den Ursprung)
        }

        static void ReadPackage7(XmlElement root, Package p)
        {
            int t = Array.IndexOf(TypeKeys, root.GetAttribute("typ"));
            if (t >= 0) p.typeSet = (PackageType)t;
            int wd = Array.IndexOf(PackageStep.DayNames, root.GetAttribute("tag1wochentag"));
            if (wd >= 0) p.Day1 = (DayOfWeek)((wd + 1) % 7);
            DateTime d;
            if (DateTime.TryParseExact(root.GetAttribute("lauf1"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d.Year >= 2000 && d.Year <= 2100) p.Lauf1 = d;   // unsinnige Jahre ignorieren (DateTimePicker waere ausserhalb)
        }

        // Fuer aeltere exe aufgeloestes Raster: JeLauf-Schritte als Tagesliste, Move in den Versatz eingerechnet. Sonst der Schritt selbst.
        PackageStep Resolved(PackageStep s, int si, List<RunDate> plan)
        {
            bool je = s.Bind == StepBind.JeLauf && Head != null && s != Head;
            if (!je && s.Move.Count == 0) return s;
            PackageStep c = s.Clone();
            if (je)
            {
                List<int> days = new List<int>();
                foreach (RunDate d in plan) if (d.Step == si) days.Add(Math.Max(1, d.Day));
                c.DayList = PackageStep.JoinDays(days);
                List<int> parsed = PackageStep.ParseDays(c.DayList);
                c.Repeat = parsed.Count > 0 ? RepeatMode.DayList : RepeatMode.Interval;
                c.Day = parsed.Count > 0 ? parsed[0] : 1; c.Count = Math.Max(1, parsed.Count); c.Interval = 1; c.Until = 0; c.WeekDays = 0;
                c.Shift.Clear(); c.Move.Clear();
            }
            else
            {
                Dictionary<int, int> merged = new Dictionary<int, int>();
                int cum = 0, n = s.Days(Day1).Count;
                for (int i = 0; i < n; i++)
                {
                    int m, sh; if (s.Move.TryGetValue(i, out m)) cum += m;
                    s.Shift.TryGetValue(i, out sh);
                    if (cum + sh != 0) merged[i] = cum + sh;
                }
                c.Move.Clear(); c.Shift = merged;
            }
            return c;
        }

        void WriteStep7(XmlWriter w, PackageStep s, int si, List<RunDate> plan)
        {
            if (s.Bind == StepBind.JeLauf)
            {
                w.WriteAttributeString("bezug", "lauf"); w.WriteAttributeString("laufversatz", I(s.RunOffset));
                if (s.RunEvery > 1) w.WriteAttributeString("jelaeufe", I(s.RunEvery));
                if (s.NumberRuns) w.WriteAttributeString("nummeriert", "1");
            }
            if (s.ManualNo.Length > 0) w.WriteAttributeString("nr", s.ManualNo);
            if (s.Skip.Count > 0) w.WriteAttributeString("ausfall", string.Join(",", s.Skip.ConvertAll<string>(delegate(int r) { return I(r + 1); }).ToArray()));
            if (s.Move.Count > 0) { w.WriteAttributeString("versatzab", PackageStep.MapText(s.Move)); w.WriteAttributeString("versatznur", s.ShiftText()); }
            if (s.IdentStart.Length > 0) w.WriteAttributeString("kennungstart", s.IdentStart);
            if (s.IdentOver.Count > 0)
            {
                List<int> keys = new List<int>(s.IdentOver.Keys); keys.Sort();
                List<string> parts = new List<string>();
                foreach (int k in keys) parts.Add(I(k + 1) + "=" + s.IdentOver[k].Replace(';', ','));
                w.WriteAttributeString("kennungje", string.Join(";", parts.ToArray()));
            }
            // alte Kommaliste (aufgeloest), fehlt bei aelteren Paketen
            if (s.IdentStart.Length == 0 && s.IdentOver.Count == 0) { if (s.Ident.Length > 0) w.WriteAttributeString("kennung", s.Ident); return; }
            List<string> ids = new List<string>();
            foreach (RunDate d in plan) if (d.Step == si) ids.Add(s.IdentFor(d.Run));
            string joined = string.Join(", ", ids.ToArray()).TrimEnd(' ', ',');
            if (joined.Length > 0) w.WriteAttributeString("kennung", joined);
        }

        static void ReadStep7(XmlElement e, PackageStep s)
        {
            s.ParseShift(e.HasAttribute("versatznur") ? e.GetAttribute("versatznur") : e.GetAttribute("versatz"));
            PackageStep.ParseMap(e.GetAttribute("versatzab"), s.Move);
            s.Bind = e.GetAttribute("bezug") == "lauf" ? StepBind.JeLauf : StepBind.Frei;
            s.RunOffset = Clamp(ToInt(e.GetAttribute("laufversatz"), 0), -PackageStep.MaxOffset, PackageStep.MaxOffset);
            s.RunEvery = Clamp(ToInt(e.GetAttribute("jelaeufe"), 1), 1, 99);
            s.NumberRuns = e.GetAttribute("nummeriert") == "1";
            s.ManualNo = PackageStep.IsManualNo(e.GetAttribute("nr")) ? e.GetAttribute("nr") : "";
            foreach (string t in e.GetAttribute("ausfall").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int r; if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) && r >= 1 && r <= PackageStep.MaxRuns) s.SetSkip(r - 1, true);
            }
            bool neu = e.HasAttribute("kennungstart") || e.HasAttribute("kennungje");
            s.IdentStart = e.GetAttribute("kennungstart").Trim();
            foreach (string part in e.GetAttribute("kennungje").Split(';'))
            {
                int eq = part.IndexOf('='), r;
                if (eq > 0 && int.TryParse(part.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out r) && r >= 1 && r <= PackageStep.MaxRuns && part.Length > eq + 1)
                    s.IdentOver[r - 1] = part.Substring(eq + 1).Trim();
            }
            s.Ident = neu ? "" : e.GetAttribute("kennung");
        }
    }
}
