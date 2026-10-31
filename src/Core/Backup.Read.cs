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
        internal static StoreData FromXml(byte[] xml) { return FromXml(new MemoryStream(xml)); }
        internal static StoreData FromXml(Stream xml)
        {
            XmlDocument doc = new XmlDocument();
            XmlReaderSettings rs = new XmlReaderSettings(); rs.DtdProcessing = DtdProcessing.Prohibit; rs.XmlResolver = null; rs.IgnoreWhitespace = true;
            using (XmlReader r = XmlReader.Create(xml, rs)) doc.Load(r);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Arbeitsplan" || Kid(root, "Zeilen") == null || Kid(root, "Katalog") == null) throw new FormatException();
            // Neueres Datenformat (neuere exe auf dem Laufwerk): nie lesen und zurueckschreiben, sonst gingen neue Felder verloren
            int fv;
            if (int.TryParse(root.GetAttribute("version"), NumberStyles.Integer, Inv, out fv) && fv > FormatVersion) throw new NewerFormatException();
            StoreData d = new StoreData();
            foreach (XmlElement z in Kids(Kid(root, "Zeilen"), "Zeile"))
            {
                RowInfo r = new RowInfo(); r.Id = Int(z, "id"); r.Name = z.GetAttribute("name");
                for (int i = 0; i < 3; i++) r.Labels[i] = z.GetAttribute("l" + i);
                r.MinLines = Int(z, "min"); r.HasLkz = Bool(z, "lkz");
                r.Mode = En(z, "modus", ShiftMode.FSN); r.Variants = z.GetAttribute("varianten");
                r.Freeze = z.HasAttribute("freeze") ? Bool(z, "freeze") : RowInfo.IsFreezeName(r.Name);   // alte Dateien: am Namen erkannt
                d.Rows.Add(r);
            }
            if (d.Rows.Count == 0) throw new FormatException();
            XmlElement bl = Kid(root, "Bloecke");
            if (bl != null)
                foreach (XmlElement e in Kids(bl, "Block")) { StoredEntry s = new StoredEntry(); s.Day = Day(e); s.RowId = Int(e, "zeile"); s.Data = ReadEntry(e); d.Entries.Add(s); }
            XmlElement k = Kid(root, "Katalog");
            foreach (XmlElement e in Kids(k, "Position"))
            {
                EquipPosition p = new EquipPosition(); p.Name = e.GetAttribute("name"); p.Short = e.GetAttribute("kurz"); p.AllowRotation = Bool(e, "abwechselnd");
                foreach (XmlElement x in Kids(e, "Id")) p.Ids.Add(x.GetAttribute("v"));
                d.Catalog.Positions.Add(p);
            }
            foreach (XmlElement e in Kids(k, "Gruppe"))
            {
                EquipGroup g = new EquipGroup(); g.Name = e.GetAttribute("name");
                foreach (XmlElement x in Kids(e, "K")) g.Shorts.Add(x.GetAttribute("v"));
                d.Catalog.Groups.Add(g);
            }
            foreach (XmlElement e in Kids(k, "Baustein"))
            {
                Baustein b = new Baustein(); b.Name = e.GetAttribute("name"); b.Row = e.GetAttribute("block"); b.Text = e.GetAttribute("text"); b.NoteTemplate = e.GetAttribute("notizvorlage"); b.Variant = e.GetAttribute("puffertyp");
                b.Unique = Bool(e, "einmal"); b.DefaultInCampaign = Bool(e, "standard"); b.Special = Bool(e, "sonder");
                foreach (XmlElement x in Kids(e, "P"))
                {
                    b.Positions.Add(x.GetAttribute("v"));
                    int cn; if (int.TryParse(x.GetAttribute("n"), NumberStyles.Integer, CultureInfo.InvariantCulture, out cn)) b.SetDefaultCount(x.GetAttribute("v"), cn);   // fehlt bei aelteren Bestaenden = 1
                }
                d.Catalog.Bausteine.Add(b);
            }
            XmlElement sd = Kid(k, "Stammdaten");
            if (sd != null) ReadMaster(sd, d.Catalog.Master);
            FreezeConfig.Read(k, d.Catalog.FreezeDefaults);   // fehlt in aelteren Dateien = Standard
            ProductXml.Read(k, d.Catalog.Products);   // fehlt in aelteren Dateien = keine Produktprofile
            XmlElement pf = Kid(root, "Pflichten");
            if (pf != null) d.Duties = ReadDuties(pf);   // sonst Standard (v2-Dateien)
            XmlElement wo = Kid(root, "Wochen");
            if (wo != null)
                foreach (XmlElement e in Kids(wo, "Woche"))
                {
                    WeekMark m = new WeekMark(); m.Monday = DateTime.ParseExact(e.GetAttribute("montag"), "yyyy-MM-dd", Inv); m.Key = e.GetAttribute("k");
                    m.Done = Bool(e, "erledigt"); m.Version = Int(e, "ver"); m.By = e.GetAttribute("von");
                    DateTime at;
                    if (DateTime.TryParseExact(e.GetAttribute("am"), "yyyy-MM-ddTHH:mm:ss", Inv, DateTimeStyles.None, out at)) m.At = at;
                    d.WeekMarks.Add(m);
                }
            XmlElement stt = Kid(root, "Status");
            if (stt != null)
            {
                d.StatusesKnown = true;
                foreach (XmlElement e in Kids(stt, "Geraet"))
                {
                    DeviceStatus st = new DeviceStatus(); st.Id = e.GetAttribute("id"); st.Status = En(e, "status", EquipStatus.NichtGereinigt); st.By = e.GetAttribute("von");
                    DateTime at; if (DateTime.TryParseExact(e.GetAttribute("am"), "yyyy-MM-ddTHH:mm:ss", Inv, DateTimeStyles.None, out at)) st.At = at;
                    d.Statuses.Add(st);
                }
            }
            XmlElement pr = Kid(root, "Protokoll");
            if (pr != null)
                foreach (XmlElement e in Kids(pr, "Eintrag"))
                {
                    AuditEntry a = new AuditEntry(); a.Time = new DateTime(long.Parse(e.GetAttribute("zeit"), Inv), DateTimeKind.Local);
                    a.Login = e.GetAttribute("login"); a.Kuerzel = e.GetAttribute("kuerzel"); a.Role = e.GetAttribute("rolle"); a.Day = Day(e); a.RowId = Int(e, "zeile"); a.RowName = e.GetAttribute("name"); a.Version = Int(e, "ver");
                    a.Mark = e.GetAttribute("pflicht"); a.Value = e.GetAttribute("wert");
                    XmlElement v = Kid(e, "Vorher"), n = Kid(e, "Nachher");
                    if (v != null) a.Before = ReadEntry(v);
                    if (n != null) a.After = ReadEntry(n);
                    d.Audit.Add(a);
                }
            XmlElement nu = Kid(root, "Nutzer");
            if (nu != null) d.Users = UserDb.ReadXml(nu);
            return d;
        }

        static Entry ReadEntry(XmlElement e)
        {
            Entry x = new Entry(); x.Version = Int(e, "ver"); x.Lkz = Bool(e, "lkz"); x.Content = e.GetAttribute("inhalt");
            for (int i = 0; i < 3; i++) x.People[i] = e.GetAttribute("p" + i);
            x.Freezes = FreezeSet.ReadXml(e);   // alte Dateien: keine <freeze> = leer
            x.Buffers = BufferSet.ReadXml(e);   // alte Dateien: Attribut "variante" -> Puffer ohne Namen
            foreach (XmlElement m in Kids(e, "Mark")) x.Marks.Add(m.GetAttribute("k"));
            foreach (XmlElement f in Kids(e, "Fmt"))
            {
                FormatSpan s = new FormatSpan(); s.Start = Int(f, "start"); s.Length = Int(f, "len"); s.Rgb = Int(f, "rgb");
                s.Size = float.Parse(f.GetAttribute("size"), Inv); s.Bold = Bool(f, "b"); s.Italic = Bool(f, "i") || Bool(f, "n");   // "n" = alte Notiz-Markierung: gilt als kursiv (Kursiv = Notiz)
                s.Font = f.HasAttribute("font") ? f.GetAttribute("font") : null;
                x.Spans.Add(s);
            }
            return x;
        }
    }
}
