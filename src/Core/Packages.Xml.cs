using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Paketdatei .apkg. Version 4: Produktcode frei ("produkt"), Laufnummern-Praefix eigenes Attribut "praefix", Schritt-"variante";
    // alte Pakete (produkt = 4 Ziffern, ohne praefix) bleiben lesbar: Praefix = Produktcode.
    public partial class Package
    {
        // ---------- Datei (XML, UTF-8: Umlaute und Sonderzeichen bleiben erhalten) ----------

        public void Save(string path)
        {
            XmlWriterSettings st = new XmlWriterSettings(); st.Indent = true; st.Encoding = new UTF8Encoding(false);
            using (XmlWriter w = XmlWriter.Create(path, st))
            {
                w.WriteStartElement("Paket"); w.WriteAttributeString("name", Name); w.WriteAttributeString("version", "4");
                if (ProductCode.Length > 0) w.WriteAttributeString("produkt", ProductCode);
                if (Prefix.Length > 0) w.WriteAttributeString("praefix", Prefix);
                foreach (PackageStep s in Steps)
                {
                    w.WriteStartElement("Schritt");
                    w.WriteAttributeString("titel", s.Title); w.WriteAttributeString("quelle", s.Source); w.WriteAttributeString("block", s.Row);
                    w.WriteAttributeString("tag", I(s.Day)); w.WriteAttributeString("anzahl", I(s.Count)); w.WriteAttributeString("abstand", I(s.Interval)); w.WriteAttributeString("startnr", I(s.StartNo));
                    if (s.Kind != StepKind.Normal) w.WriteAttributeString("art", s.Kind == StepKind.Run ? "lauf" : "unterlauf");
                    if (s.SubNos.Length > 0) w.WriteAttributeString("unterlaeufe", s.SubNos);
                    w.WriteAttributeString("text", s.Text); w.WriteAttributeString("notiz", s.Note);
                    if (s.Ident.Length > 0) w.WriteAttributeString("kennung", s.Ident);   // fehlt bei aelteren Paketen
                    if (s.Variant.Length > 0) w.WriteAttributeString("variante", s.Variant);
                    foreach (EquipAssign a in s.Equipment)
                    {
                        w.WriteStartElement("Equipment");
                        w.WriteAttributeString("kurz", a.Short);
                        w.WriteAttributeString("abwechselnd", a.Alternate ? "1" : "0"); w.WriteAttributeString("ausweich", a.Fallback); w.WriteAttributeString("notiz", a.Note);
                        foreach (EquipChoice ch in a.Choices)   // je Geraet ein Element: beliebige Zeichen in Kurzname/ID moeglich
                        {
                            w.WriteStartElement("Geraet"); w.WriteAttributeString("kurz", ch.Short); w.WriteAttributeString("id", ch.Id); w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                foreach (PackageBlock b in Blocks)
                {
                    w.WriteStartElement("Block");
                    w.WriteAttributeString("tag", I(b.Day)); w.WriteAttributeString("zeile", b.Row); w.WriteAttributeString("lkz", b.Data.Lkz ? "1" : "0");
                    if (b.Data.Variant.Length > 0) w.WriteAttributeString("variante", b.Data.Variant);
                    w.WriteElementString("Inhalt", b.Data.Content);
                    w.WriteStartElement("Kuerzel");
                    w.WriteAttributeString("f", b.Data.People[0]); w.WriteAttributeString("s", b.Data.People[1]); w.WriteAttributeString("n", b.Data.People[2]);
                    w.WriteEndElement();
                    foreach (FormatSpan f in b.Data.Spans)
                    {
                        w.WriteStartElement("Format");
                        w.WriteAttributeString("start", I(f.Start)); w.WriteAttributeString("laenge", I(f.Length));
                        w.WriteAttributeString("farbe", I(f.Rgb)); w.WriteAttributeString("groesse", f.Size.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("fett", f.Bold ? "1" : "0"); w.WriteAttributeString("kursiv", f.Italic ? "1" : "0");
                        w.WriteAttributeString("schrift", f.Font ?? "");
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }

        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        static int ToInt(string s, int def) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def; }

        static IEnumerable<XmlElement> Elements(XmlNode n)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null) yield return e; }
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static Package Load(string path)
        {
            // Gehaertet: keine DTD/externen Entitaeten, keine Aufloesung externer Quellen, Groessenlimit (10 MB).
            if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("Paketdatei ist zu groß.");
            XmlReaderSettings rs = new XmlReaderSettings();
            rs.DtdProcessing = DtdProcessing.Prohibit;
            rs.XmlResolver = null;
            rs.MaxCharactersInDocument = 20 * 1000 * 1000;
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (XmlReader xr = XmlReader.Create(path, rs)) doc.Load(xr);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Paket") throw new InvalidDataException("Keine gültige Paketdatei.");
            Package p = new Package();
            p.Name = root.GetAttribute("name");
            p.ProductCode = IsProductCode(root.GetAttribute("produkt")) ? root.GetAttribute("produkt") : "";   // fehlt bei aelteren Paketen
            p.RunPrefix = IsCode4(root.GetAttribute("praefix")) ? root.GetAttribute("praefix") : "";        // fehlt bis Version 3: Prefix = Produktcode
            foreach (XmlElement e in Elements(root))
            {
                if (e.Name == "Schritt")
                {
                    PackageStep s = new PackageStep();
                    s.Title = e.GetAttribute("titel"); s.Source = e.GetAttribute("quelle"); s.Row = e.GetAttribute("block");
                    s.Day = Clamp(ToInt(e.GetAttribute("tag"), 1), 1, 366); s.Count = Clamp(ToInt(e.GetAttribute("anzahl"), 1), 1, 99);
                    s.Interval = Clamp(ToInt(e.GetAttribute("abstand"), 1), 1, 30); s.StartNo = Clamp(ToInt(e.GetAttribute("startnr"), 1), 0, 9999);
                    string art = e.GetAttribute("art");
                    s.Kind = art == "lauf" ? StepKind.Run : (art == "unterlauf" ? StepKind.SubRun : StepKind.Normal);
                    s.SubNos = e.GetAttribute("unterlaeufe");
                    s.Text = e.GetAttribute("text"); s.Note = e.GetAttribute("notiz"); s.Ident = e.GetAttribute("kennung");
                    s.Variant = e.GetAttribute("variante");
                    foreach (XmlElement q in Elements(e))
                        if (q.Name == "Equipment")
                        {
                            EquipAssign a = new EquipAssign();
                            a.Short = q.GetAttribute("kurz");
                            foreach (XmlElement g in Elements(q))
                                if (g.Name == "Geraet") a.Choices.Add(new EquipChoice(g.GetAttribute("kurz"), g.GetAttribute("id")));
                            if (a.Choices.Count == 0)   // aeltere Pakete: "wahl" (Kurz|Id;...) bzw. "ids" (Id|Id|...)
                            {
                                foreach (string w in q.GetAttribute("wahl").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = w.Split('|');
                                    a.Choices.Add(kv.Length > 1 ? new EquipChoice(kv[0], kv[1]) : new EquipChoice(a.Short, w));
                                }
                                if (a.Choices.Count == 0)
                                    foreach (string id in q.GetAttribute("ids").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) a.Choices.Add(new EquipChoice(a.Short, id));
                            }
                            a.Alternate = q.GetAttribute("abwechselnd") == "1"; a.Fallback = q.GetAttribute("ausweich"); a.Note = q.GetAttribute("notiz");
                            s.Equipment.Add(a);
                        }
                    p.Steps.Add(s);
                }
                else if (e.Name == "Block")
                {
                    PackageBlock b = new PackageBlock();
                    b.Day = Clamp(ToInt(e.GetAttribute("tag"), 1), 1, 366); b.Row = e.GetAttribute("zeile"); b.Data.Lkz = e.GetAttribute("lkz") == "1"; b.Data.Variant = e.GetAttribute("variante");
                    foreach (XmlElement c in Elements(e))
                    {
                        if (c.Name == "Inhalt") b.Data.Content = c.InnerText.Replace("\r\n", "\n");
                        else if (c.Name == "Kuerzel") { b.Data.People[0] = c.GetAttribute("f"); b.Data.People[1] = c.GetAttribute("s"); b.Data.People[2] = c.GetAttribute("n"); }
                        else if (c.Name == "Format")
                        {
                            FormatSpan f = new FormatSpan();
                            f.Start = Clamp(ToInt(c.GetAttribute("start"), 0), 0, 1000000); f.Length = Clamp(ToInt(c.GetAttribute("laenge"), 0), 0, 1000000); f.Rgb = ToInt(c.GetAttribute("farbe"), 0) & 0xFFFFFF;
                            float.TryParse(c.GetAttribute("groesse"), NumberStyles.Float, CultureInfo.InvariantCulture, out f.Size);
                            if (!(f.Size >= 6f && f.Size <= 72f)) f.Size = 0;   // NaN/unsinnig: Standardgroesse
                            f.Bold = c.GetAttribute("fett") == "1"; f.Italic = c.GetAttribute("kursiv") == "1";
                            f.Font = c.GetAttribute("schrift").Length > 0 ? c.GetAttribute("schrift") : null;
                            b.Data.Spans.Add(f);
                        }
                    }
                    int len = b.Data.Content.Length;   // Formatbereiche nie ueber den Text hinaus
                    b.Data.Spans.RemoveAll(delegate(FormatSpan f) { return f.Start > len; });
                    foreach (FormatSpan f in b.Data.Spans) f.Length = Math.Min(f.Length, len - f.Start);
                    p.Blocks.Add(b);
                }
            }
            return p;
        }
    }
}
