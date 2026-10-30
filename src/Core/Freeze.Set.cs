using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Hilfsfunktionen fuer Entry.Freezes (Muster BufferSet): Ids, XML, Rechte, Anzeige, Suche, Dreiwege-Merge.
    public static class FreezeSet
    {
        public const int LookBack = 7;   // Fortsetzungen: je Tag so viele Tage zurueck nach Vorgaengen suchen

        static int Num(string id)
        {
            int n;
            return id != null && id.Length > 1 && id[0] == 'f' && int.TryParse(id.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        public static string NewId(params List<FreezeItem>[] lists)
        {
            int m = 0;
            foreach (List<FreezeItem> l in lists) if (l != null) foreach (FreezeItem f in l) m = Math.Max(m, Num(f.Id));
            return "f" + (m + 1).ToString(CultureInfo.InvariantCulture);
        }

        public static FreezeItem Add(List<FreezeItem> l, FreezeItem f) { f.Id = NewId(l); l.Add(f); return f; }

        public static List<FreezeItem> CloneAll(List<FreezeItem> l)
        {
            List<FreezeItem> r = new List<FreezeItem>();
            foreach (FreezeItem f in l) r.Add(f.Clone());
            return r;
        }

        // Kopien hinten anhaengen (neue Ids)
        public static void AddRange(List<FreezeItem> l, List<FreezeItem> add)
        {
            foreach (FreezeItem a in add) { FreezeItem f = a.Clone(); f.Id = NewId(l); l.Add(f); }
        }

        static void Normalize(List<FreezeItem> l)
        {
            List<string> seen = new List<string>();
            foreach (FreezeItem f in l)
            {
                if (f.Id.Length == 0 || seen.Contains(f.Id)) f.Id = NewId(l);
                seen.Add(f.Id);
            }
        }

        public static string Key(List<FreezeItem> l) { StringBuilder sb = new StringBuilder(); foreach (FreezeItem f in l) sb.Append(f.Key()); return sb.ToString(); }

        static FreezeItem ById(List<FreezeItem> l, string id) { foreach (FreezeItem f in l) if (f.Id == id) return f; return null; }

        // ---------- XML: <freeze id="f1" art="bag" dauer="360" abstand="420" cycles="14" haupt="" unter="" tanks=""><s c="1" t="2031-03-03T12:00"/><ist c="4" t="..."/>Notiz</freeze> ----------

        public static void WriteXml(XmlWriter w, List<FreezeItem> l)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            foreach (FreezeItem f in l)
            {
                w.WriteStartElement("freeze");
                w.WriteAttributeString("id", f.Id); w.WriteAttributeString("art", f.Art == FreezeArt.Tank ? "tank" : "bag");
                if (f.Art == FreezeArt.Tank) { w.WriteAttributeString("anlage", f.Anlage.ToString(inv)); w.WriteAttributeString("kreislauf", f.Kreislauf.ToString(inv)); }
                w.WriteAttributeString("dauer", f.DauerMin.ToString(inv)); w.WriteAttributeString("abstand", f.AbstandMin.ToString(inv)); w.WriteAttributeString("cycles", f.Cycles.ToString(inv));
                if (f.MainRef.Length > 0) w.WriteAttributeString("haupt", f.MainRef);
                if (f.SubNo.Length > 0) w.WriteAttributeString("unter", f.SubNo);
                if (f.Tanks.Length > 0) w.WriteAttributeString("tanks", f.Tanks);
                for (int i = 0; i < 3; i++)
                    if (f.Start1to3[i] != DateTime.MinValue) { w.WriteStartElement("s"); w.WriteAttributeString("c", (i + 1).ToString(inv)); w.WriteAttributeString("t", FreezeCalc.Fmt(f.Start1to3[i])); w.WriteEndElement(); }
                foreach (KeyValuePair<int, DateTime> kv in f.Ist) { w.WriteStartElement("ist"); w.WriteAttributeString("c", kv.Key.ToString(inv)); w.WriteAttributeString("t", FreezeCalc.Fmt(kv.Value)); w.WriteEndElement(); }
                if (f.Note.Length > 0) w.WriteString(f.Note);
                w.WriteEndElement();
            }
        }

        // Liest die <freeze>-Elemente unter e; ohne solche (alte Dateien) leere Liste. Unlesbare Werte -> Standard bzw. Eintrag ohne Zeit.
        public static List<FreezeItem> ReadXml(XmlElement e)
        {
            List<FreezeItem> l = new List<FreezeItem>();
            foreach (XmlNode n in e.ChildNodes)
            {
                XmlElement c = n as XmlElement;
                if (c == null || c.Name != "freeze") continue;
                FreezeItem f = new FreezeItem(); f.Id = c.GetAttribute("id");
                f.Art = c.GetAttribute("art") == "tank" ? FreezeArt.Tank : FreezeArt.Bag;
                f.Anlage = FreezeCalc.Num(c, "anlage", 1, 1, 9); f.Kreislauf = FreezeCalc.Num(c, "kreislauf", 1, 1, 9);
                f.DauerMin = FreezeCalc.Num(c, "dauer", 360, 1, FreezeCalc.MaxMin); f.AbstandMin = FreezeCalc.Num(c, "abstand", 420, 1, FreezeCalc.MaxMin);
                f.Cycles = FreezeCalc.Num(c, "cycles", 14, 1, FreezeCalc.MaxCycles);
                f.MainRef = c.GetAttribute("haupt"); f.SubNo = c.GetAttribute("unter"); f.Tanks = c.GetAttribute("tanks");
                StringBuilder note = new StringBuilder();
                foreach (XmlNode k in c.ChildNodes)
                {
                    XmlElement ke = k as XmlElement;
                    if (ke == null) { if (k.NodeType == XmlNodeType.Text || k.NodeType == XmlNodeType.CDATA) note.Append(k.Value); continue; }
                    int cn = FreezeCalc.Num(ke, "c", 0, 0, FreezeCalc.MaxCycles); DateTime t;
                    if (cn == 0 || !FreezeCalc.TryParse(ke.GetAttribute("t"), out t)) continue;
                    if (ke.Name == "s" && cn <= 3) f.Start1to3[cn - 1] = t;
                    else if (ke.Name == "ist") f.Ist[cn] = t;
                }
                f.Note = note.ToString().Replace("\r\n", "\n");
                l.Add(f);
            }
            Normalize(l);
            return l;
        }

        // ---------- Anzeige, Suche ----------

        // Protokoll/Vorschau: je Vorgang eine Zeile "Freeze 1: Bag · Unterlauf X (aus Y) · Z1 12.10. 12:00 · 14 Zyklen"; detail: darunter je Ist-Zyklus "Z4 Ist 12.10. 02:00 (+7 h)"
        public static List<string> DisplayLines(List<FreezeItem> l, bool detail)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < l.Count; i++)
            {
                FreezeItem f = l[i];
                string head = "Freeze " + (i + 1) + ": " + (f.Art == FreezeArt.Tank ? "Tank GWK" + f.Anlage + " K" + f.Kreislauf : "Bag");
                if (f.SubNo.Length > 0) head += " · Unterlauf " + f.SubNo + (f.MainRef.Length > 0 ? " (aus " + f.MainRef + ")" : "");
                else if (f.MainRef.Length > 0) head += " · Lauf " + f.MainRef;
                if (f.Valid) head += " · Z1 " + FreezeCalc.Short(f.Start1to3[0]) + (f.Art == FreezeArt.Bag ? " · " + FreezeCalc.Count(f) + " Zyklen" : " · " + FreezeCalc.Dur(f.DauerMin));
                if (f.Tanks.Length > 0) head += " · Tanks " + f.Tanks;
                r.Add(head);
                if (!detail) continue;
                foreach (FreezeCycle c in FreezeCalc.Cycles(f))
                    if (c.HasIst) r.Add("    " + c.Label + " Ist " + FreezeCalc.Short(c.Actual) + (c.DelayMin != 0 ? " (" + FreezeCalc.DelayText(c.DelayMin) + ")" : ""));
                if (f.Note.Length > 0) foreach (string t in f.Note.Split('\n')) r.Add("    " + t);
            }
            return r;
        }

        public static bool Matches(List<FreezeItem> l, string q)
        {
            foreach (FreezeItem f in l)
                if (f.MainRef.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || f.SubNo.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || f.Tanks.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || f.Note.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Cycles der Vorgaenge dieses Blocks, die am Kalendertag day starten (nach tatsaechlichem Start)
        public static List<FreezeCycle> CyclesOn(Entry e, DateTime day)
        {
            List<FreezeCycle> r = new List<FreezeCycle>();
            if (e == null) return r;
            foreach (FreezeItem f in e.Freezes) foreach (FreezeCycle c in FreezeCalc.Cycles(f)) if (c.Day == day.Date) r.Add(c);
            return r;
        }

        // Fortsetzungen am Tag day in der Zeile rowId: Cycles aus Bloecken der letzten LookBack Tage (OriginDay = Tag dieses Blocks)
        public static List<FreezeCycle> Continuations(IDataStore store, DateTime day, int rowId)
        {
            List<FreezeCycle> r = new List<FreezeCycle>();
            for (int k = 1; k <= LookBack; k++)
            {
                DateTime o = day.Date.AddDays(-k);
                foreach (FreezeCycle c in CyclesOn(store.Get(o, rowId), day)) { c.OriginDay = o; r.Add(c); }
            }
            return r;
        }

        // ---------- Rechte, Dreiwege-Merge ----------

        // Neu = AddText, Entfernen = DeleteText, Aendern (auch Ist-Starts, Reihenfolge) = EditText
        public static void Diff(List<FreezeItem> old, List<FreezeItem> n, out bool added, out bool removed, out bool changed)
        {
            added = removed = changed = false;
            foreach (FreezeItem f in n) { FreezeItem o = ById(old, f.Id); if (o == null) added = true; else if (!o.SameValue(f)) changed = true; }
            foreach (FreezeItem o in old) if (ById(n, o.Id) == null) removed = true;
            if (!added && !removed && !changed)
                for (int i = 0; i < n.Count && i < old.Count; i++) if (n[i].Id != old[i].Id) changed = true;
        }

        static T Pick<T>(T b, T m, T t, bool preferMine, ref bool clash)
        {
            EqualityComparer<T> c = EqualityComparer<T>.Default;
            if (c.Equals(m, b) || c.Equals(m, t)) return t;
            if (c.Equals(t, b)) return m;
            clash = true;
            return preferMine ? m : t;
        }

        // Ein Vorgang, beidseitig geaendert: je Feld, Ist je Cycle. Verschiedene Felder/Cycles still; dieselbe Stelle verschieden = Konflikt (gespeicherte Fassung bleibt).
        static FreezeItem MergeItem(FreezeItem b, FreezeItem m, FreezeItem t, bool pm, ref bool clash)
        {
            FreezeItem r = t.Clone();
            r.Art = Pick(b.Art, m.Art, t.Art, pm, ref clash);
            r.Anlage = Pick(b.Anlage, m.Anlage, t.Anlage, pm, ref clash); r.Kreislauf = Pick(b.Kreislauf, m.Kreislauf, t.Kreislauf, pm, ref clash);
            for (int i = 0; i < 3; i++) r.Start1to3[i] = Pick(b.Start1to3[i], m.Start1to3[i], t.Start1to3[i], pm, ref clash);
            r.DauerMin = Pick(b.DauerMin, m.DauerMin, t.DauerMin, pm, ref clash); r.AbstandMin = Pick(b.AbstandMin, m.AbstandMin, t.AbstandMin, pm, ref clash);
            r.Cycles = Pick(b.Cycles, m.Cycles, t.Cycles, pm, ref clash);
            r.MainRef = Pick(b.MainRef, m.MainRef, t.MainRef, pm, ref clash); r.SubNo = Pick(b.SubNo, m.SubNo, t.SubNo, pm, ref clash);
            r.Tanks = Pick(b.Tanks, m.Tanks, t.Tanks, pm, ref clash); r.Note = Pick(b.Note, m.Note, t.Note, pm, ref clash);
            List<int> keys = new List<int>(b.Ist.Keys);
            foreach (int k in m.Ist.Keys) if (!keys.Contains(k)) keys.Add(k);
            foreach (int k in t.Ist.Keys) if (!keys.Contains(k)) keys.Add(k);
            r.Ist = new SortedDictionary<int, DateTime>();
            foreach (int k in keys)
            {
                DateTime v;
                DateTime? bv = b.Ist.TryGetValue(k, out v) ? v : (DateTime?)null, mv = m.Ist.TryGetValue(k, out v) ? v : (DateTime?)null, tv = t.Ist.TryGetValue(k, out v) ? v : (DateTime?)null;
                DateTime? pick = Pick(bv, mv, tv, pm, ref clash);
                if (pick.HasValue) r.Ist[k] = pick.Value;
            }
            return r;
        }

        // Dreiwege je Id (Basis b, eigene Fassung mine, gespeicherter Stand theirs). Neu = dazu, einseitig geloescht = weg (hat die andere Seite ihn geaendert, bleibt er),
        // beidseitig geaendert = MergeItem. Gleichzeitig angelegte Vorgaenge mit gleicher Id aber anderem Inhalt: der eigene bekommt eine neue Id.
        public static List<FreezeItem> Merge(List<FreezeItem> b, List<FreezeItem> mine, List<FreezeItem> theirs, bool preferMine, out bool clash)
        {
            clash = false;
            List<FreezeItem> r = new List<FreezeItem>(), extra = new List<FreezeItem>();
            foreach (FreezeItem t in theirs)
            {
                FreezeItem bb = ById(b, t.Id), mm = ById(mine, t.Id);
                if (bb == null) { r.Add(t.Clone()); if (mm != null && !mm.SameValue(t)) extra.Add(mm.Clone()); continue; }
                if (mm == null) { if (!t.SameValue(bb)) r.Add(t.Clone()); continue; }
                r.Add(MergeItem(bb, mm, t, preferMine, ref clash));
            }
            foreach (FreezeItem m in mine)
            {
                if (ById(theirs, m.Id) != null) continue;
                FreezeItem bb = ById(b, m.Id);
                if (bb == null || !m.SameValue(bb)) extra.Add(m.Clone());   // von mir neu angelegt / von theirs geloescht, von mir geaendert
            }
            foreach (FreezeItem x in extra)
            {
                if (ById(r, x.Id) != null) x.Id = NewId(b, mine, theirs, r, extra);
                r.Add(x);
            }
            return r;
        }
    }
}
