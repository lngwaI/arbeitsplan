using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    public static partial class Backup
    {
        internal static byte[] ToXml(StoreData d)
        {
            using (MemoryStream ms = new MemoryStream()) { ToXml(ms, d, true); return ms.ToArray(); }
        }

        // indent=false: kompakt (Datendatei); Lesen kommt mit beiden Formen zurecht. output bleibt offen.
        internal static void ToXml(Stream output, StoreData d, bool indent)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = indent; st.Encoding = Utf8;
            {
                using (XmlWriter w = XmlWriter.Create(output, st))
                {
                    w.WriteStartElement("Arbeitsplan"); w.WriteAttributeString("version", FormatVersion.ToString(Inv)); w.WriteAttributeString("erstellt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv));
                    w.WriteStartElement("Zeilen");
                    foreach (RowInfo r in d.Rows)
                    {
                        w.WriteStartElement("Zeile"); w.WriteAttributeString("id", I(r.Id)); w.WriteAttributeString("name", r.Name);
                        for (int i = 0; i < 3; i++) w.WriteAttributeString("l" + i, r.Labels[i]);
                        w.WriteAttributeString("min", I(r.MinLines)); w.WriteAttributeString("lkz", B(r.HasLkz));
                        if (r.Mode != ShiftMode.FSN) w.WriteAttributeString("modus", r.Mode.ToString());
                        if (r.Variants.Length > 0) w.WriteAttributeString("varianten", r.Variants);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Bloecke");
                    foreach (StoredEntry s in d.Entries) { w.WriteStartElement("Block"); w.WriteAttributeString("tag", s.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(s.RowId)); WriteEntry(w, s.Data); w.WriteEndElement(); }
                    w.WriteEndElement();
                    WriteCatalog(w, d.Catalog);
                    WriteDuties(w, d.Duties);
                    w.WriteStartElement("Wochen");
                    foreach (WeekMark m in d.WeekMarks)
                    {
                        w.WriteStartElement("Woche"); w.WriteAttributeString("montag", m.Monday.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("k", m.Key);
                        w.WriteAttributeString("erledigt", B(m.Done)); w.WriteAttributeString("ver", I(m.Version));
                        if (m.At != DateTime.MinValue) w.WriteAttributeString("am", m.At.ToString("yyyy-MM-ddTHH:mm:ss", Inv));   // optional, aeltere Programme ignorieren es
                        if (m.By.Length > 0) w.WriteAttributeString("von", m.By);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Status");
                    foreach (DeviceStatus ds in d.Statuses)
                    {
                        w.WriteStartElement("Geraet"); w.WriteAttributeString("id", ds.Id); w.WriteAttributeString("status", ds.Status.ToString());
                        w.WriteAttributeString("am", ds.At.ToString("yyyy-MM-ddTHH:mm:ss", Inv)); w.WriteAttributeString("von", ds.By); w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Protokoll");
                    foreach (AuditEntry a in d.Audit)
                    {
                        w.WriteStartElement("Eintrag"); w.WriteAttributeString("zeit", I(a.Time.Ticks)); w.WriteAttributeString("login", a.Login); w.WriteAttributeString("kuerzel", a.Kuerzel);
                        if (a.Role.Length > 0) w.WriteAttributeString("rolle", a.Role);
                        w.WriteAttributeString("tag", a.Day.ToString("yyyy-MM-dd", Inv)); w.WriteAttributeString("zeile", I(a.RowId)); w.WriteAttributeString("name", a.RowName); w.WriteAttributeString("ver", I(a.Version));
                        if (a.Mark.Length > 0) { w.WriteAttributeString("pflicht", a.Mark); w.WriteAttributeString("wert", a.Value); }
                        if (a.Before != null) { w.WriteStartElement("Vorher"); WriteEntry(w, a.Before); w.WriteEndElement(); }
                        if (a.After != null) { w.WriteStartElement("Nachher"); WriteEntry(w, a.After); w.WriteEndElement(); }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    if (d.Users != null) d.Users.WriteXml(w);
                    w.WriteEndElement();
                }
            }
        }

        static void WriteEntry(XmlWriter w, Entry e)
        {
            w.WriteAttributeString("ver", I(e.Version)); w.WriteAttributeString("lkz", B(e.Lkz));
            w.WriteAttributeString("inhalt", e.Content);
            for (int i = 0; i < 3; i++) w.WriteAttributeString("p" + i, e.People[i]);
            foreach (string m in e.Marks) { w.WriteStartElement("Mark"); w.WriteAttributeString("k", m); w.WriteEndElement(); }
            BufferSet.WriteXml(w, e.Buffers);
            foreach (FormatSpan f in e.Spans)
            {
                w.WriteStartElement("Fmt"); w.WriteAttributeString("start", I(f.Start)); w.WriteAttributeString("len", I(f.Length)); w.WriteAttributeString("rgb", I(f.Rgb));
                w.WriteAttributeString("size", f.Size.ToString("R", Inv)); w.WriteAttributeString("b", B(f.Bold)); w.WriteAttributeString("i", B(f.Italic));
                if (f.Note) w.WriteAttributeString("n", "1");
                if (f.Note && f.NoteWasItalic) w.WriteAttributeString("oi", "1");
                if (f.Font != null) w.WriteAttributeString("font", f.Font);
                w.WriteEndElement();
            }
        }

        static void WriteCatalog(XmlWriter w, Catalog c)
        {
            w.WriteStartElement("Katalog");
            foreach (EquipPosition p in c.Positions)
            {
                w.WriteStartElement("Position"); w.WriteAttributeString("name", p.Name); w.WriteAttributeString("kurz", p.Short); w.WriteAttributeString("abwechselnd", B(p.AllowRotation));
                foreach (string id in p.Ids) { w.WriteStartElement("Id"); w.WriteAttributeString("v", id); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipGroup g in c.Groups)
            {
                w.WriteStartElement("Gruppe"); w.WriteAttributeString("name", g.Name);
                foreach (string s in g.Shorts) { w.WriteStartElement("K"); w.WriteAttributeString("v", s); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (Baustein b in c.Bausteine)
            {
                w.WriteStartElement("Baustein"); w.WriteAttributeString("name", b.Name); w.WriteAttributeString("block", b.Row); w.WriteAttributeString("text", b.Text);
                if (b.NoteTemplate.Length > 0) w.WriteAttributeString("notizvorlage", b.NoteTemplate);
                if (b.Variant.Length > 0) w.WriteAttributeString("puffertyp", b.Variant);
                w.WriteAttributeString("einmal", B(b.Unique)); w.WriteAttributeString("standard", B(b.DefaultInCampaign)); w.WriteAttributeString("sonder", B(b.Special));
                foreach (string s in b.Positions) { w.WriteStartElement("P"); w.WriteAttributeString("v", s); if (b.DefaultCount(s) > 1) w.WriteAttributeString("n", b.DefaultCount(s).ToString(CultureInfo.InvariantCulture)); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            WriteMaster(w, c.Master);
            ProductXml.Write(w, c.Products);
            w.WriteEndElement();
        }

        static void WriteDuties(XmlWriter w, DutyConfig c)
        {
            w.WriteStartElement("Pflichten"); w.WriteAttributeString("land", c.Land);
            foreach (CheckDef d in c.Defs)
            {
                w.WriteStartElement("Def"); w.WriteAttributeString("k", d.Key); w.WriteAttributeString("name", d.Name); w.WriteAttributeString("kurz", d.Short);
                w.WriteAttributeString("zeile", I(d.RowId)); w.WriteAttributeString("regel", d.Rule.ToString()); w.WriteAttributeString("bed", d.Cond);
                w.WriteAttributeString("woche", B(d.Weekly)); w.WriteEndElement();
            }
            foreach (RoundDef r in c.Rounds)
            {
                w.WriteStartElement("Rundgang"); w.WriteAttributeString("k", r.Key); w.WriteAttributeString("name", r.Name); w.WriteAttributeString("kurz", r.Short);
                w.WriteAttributeString("aktiv", B(r.Active)); w.WriteEndElement();
            }
            foreach (DateTime x in c.ExtraHolidays) { w.WriteStartElement("Feiertag"); w.WriteAttributeString("tag", x.ToString("yyyy-MM-dd", Inv)); w.WriteEndElement(); }
            foreach (DateTime x in c.RemovedHolidays) { w.WriteStartElement("KeinFeiertag"); w.WriteAttributeString("tag", x.ToString("yyyy-MM-dd", Inv)); w.WriteEndElement(); }
            w.WriteEndElement();
        }

        static DutyConfig ReadDuties(XmlElement p)
        {
            DutyConfig c = new DutyConfig();
            if (p.GetAttribute("land").Length > 0) c.Land = p.GetAttribute("land");
            foreach (XmlElement e in Kids(p, "Def"))
            {
                CheckDef d = new CheckDef(); d.Key = e.GetAttribute("k"); d.Name = e.GetAttribute("name"); d.Short = e.GetAttribute("kurz");
                d.RowId = Int(e, "zeile"); d.Rule = En(e, "regel", CheckRule.IfOccupied); d.Cond = e.GetAttribute("bed"); d.Weekly = Bool(e, "woche");
                c.Defs.Add(d);
            }
            foreach (XmlElement e in Kids(p, "Rundgang"))
            {
                RoundDef r = new RoundDef(); r.Key = e.GetAttribute("k"); r.Name = e.GetAttribute("name"); r.Short = e.GetAttribute("kurz"); r.Active = Bool(e, "aktiv");
                c.Rounds.Add(r);
            }
            foreach (XmlElement e in Kids(p, "Feiertag")) c.ExtraHolidays.Add(Day(e));
            foreach (XmlElement e in Kids(p, "KeinFeiertag")) c.RemovedHolidays.Add(Day(e));
            return c;
        }

        static void WriteMaster(XmlWriter w, EquipMaster m)
        {
            w.WriteStartElement("Stammdaten");
            foreach (EquipClass c in m.Classes)
            {
                w.WriteStartElement("Klasse"); w.WriteAttributeString("name", c.Name);
                foreach (string f in c.Fields) { w.WriteStartElement("F"); w.WriteAttributeString("v", f); w.WriteEndElement(); }
                WriteRules(w, c.Rules);
                w.WriteEndElement();
            }
            foreach (EquipSeries s in m.Series)
            {
                w.WriteStartElement("Reihe"); w.WriteAttributeString("name", s.Name); w.WriteAttributeString("klasse", s.Class);
                w.WriteAttributeString("von", s.From); w.WriteAttributeString("bis", s.To); w.WriteAttributeString("notiz", s.Note);
                foreach (string id in s.Ids) { w.WriteStartElement("Id"); w.WriteAttributeString("v", id); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipDevice d in m.Devices)
            {
                w.WriteStartElement("Geraet"); w.WriteAttributeString("id", d.Id); w.WriteAttributeString("klasse", d.Class);
                w.WriteAttributeString("notiz", d.Note); w.WriteAttributeString("aktiv", B(d.Active));
                if (d.Parent.Length > 0) w.WriteAttributeString("gehoertzu", d.Parent);
                foreach (EquipProp p in d.Props) { w.WriteStartElement("E"); w.WriteAttributeString("name", p.Name); w.WriteAttributeString("v", p.Value); w.WriteEndElement(); }
                WriteRules(w, d.Rules);
                w.WriteEndElement();
            }
            foreach (EquipLink l in m.Links)
            {
                w.WriteStartElement("Verwendbar"); w.WriteAttributeString("von", l.From); w.WriteAttributeString("notiz", l.Note);
                foreach (string t in l.To) { w.WriteStartElement("Z"); w.WriteAttributeString("v", t); w.WriteEndElement(); }
                w.WriteEndElement();
            }
            foreach (EquipSubst s in m.Substs)
            {
                w.WriteStartElement("Ersatz"); w.WriteAttributeString("id", s.Id); w.WriteAttributeString("ersetzt", s.Replaces);
                w.WriteAttributeString("notiz", s.Note); w.WriteAttributeString("beide", B(s.Both)); w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        static void WriteRules(XmlWriter w, List<StatusRule> rules)
        {
            foreach (StatusRule r in rules) { w.WriteStartElement("Uebergang"); w.WriteAttributeString("von", r.From.ToString()); w.WriteAttributeString("nach", r.To.ToString()); w.WriteAttributeString("erlaubt", B(r.Legal)); w.WriteEndElement(); }
        }

        static void ReadRules(XmlElement e, List<StatusRule> rules)
        {
            foreach (XmlElement x in Kids(e, "Uebergang")) rules.Add(new StatusRule(En(x, "von", EquipStatus.NichtGereinigt), En(x, "nach", EquipStatus.NichtGereinigt), Bool(x, "erlaubt")));
        }

        static void ReadMaster(XmlElement sd, EquipMaster m)
        {
            foreach (XmlElement e in Kids(sd, "Klasse"))
            {
                EquipClass c = new EquipClass(); c.Name = e.GetAttribute("name");
                foreach (XmlElement x in Kids(e, "F")) c.Fields.Add(x.GetAttribute("v"));
                ReadRules(e, c.Rules);
                m.Classes.Add(c);
            }
            foreach (XmlElement e in Kids(sd, "Reihe"))
            {
                EquipSeries s = new EquipSeries(); s.Name = e.GetAttribute("name"); s.Class = e.GetAttribute("klasse");
                s.From = e.GetAttribute("von"); s.To = e.GetAttribute("bis"); s.Note = e.GetAttribute("notiz");
                foreach (XmlElement x in Kids(e, "Id")) s.Ids.Add(x.GetAttribute("v"));
                m.Series.Add(s);
            }
            foreach (XmlElement e in Kids(sd, "Geraet"))
            {
                EquipDevice d = new EquipDevice(); d.Id = e.GetAttribute("id"); d.Class = e.GetAttribute("klasse");
                d.Note = e.GetAttribute("notiz"); d.Active = Bool(e, "aktiv"); d.Parent = e.GetAttribute("gehoertzu");
                foreach (XmlElement x in Kids(e, "E")) d.Props.Add(new EquipProp(x.GetAttribute("name"), x.GetAttribute("v")));
                ReadRules(e, d.Rules);
                m.Devices.Add(d);
            }
            foreach (XmlElement e in Kids(sd, "Verwendbar"))
            {
                EquipLink l = new EquipLink(); l.From = e.GetAttribute("von"); l.Note = e.GetAttribute("notiz");
                foreach (XmlElement x in Kids(e, "Z")) l.To.Add(x.GetAttribute("v"));
                m.Links.Add(l);
            }
            foreach (XmlElement e in Kids(sd, "Ersatz"))
            {
                EquipSubst s = new EquipSubst(); s.Id = e.GetAttribute("id"); s.Replaces = e.GetAttribute("ersetzt");
                s.Note = e.GetAttribute("notiz"); s.Both = Bool(e, "beide"); m.Substs.Add(s);
            }
        }
    }
}
