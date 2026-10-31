using System;
using System.Collections.Generic;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Paketformat 8: je Schritt <Puffer name typ nameno altmul altadd><Equipment .../></Puffer> und <Gemeinsam bereich="3"><Equipment .../></Gemeinsam>
    // (bereich = Bitmaske, fehlt = alle). Die Equipment-Elemente haben dasselbe Format wie direkt unter <Schritt>.
    public partial class Package
    {
        static void WriteEquip(XmlWriter w, EquipAssign a)
        {
            w.WriteStartElement("Equipment");
            w.WriteAttributeString("kurz", a.Short);
            w.WriteAttributeString("abwechselnd", a.Alternate ? "1" : "0"); w.WriteAttributeString("ausweich", a.Fallback); w.WriteAttributeString("notiz", a.Note);
            if (a.ParaAfter) w.WriteAttributeString("absatz", "1");   // optional: Leerzeile nach dieser Zeile im Plantext
            foreach (EquipChoice ch in a.Choices)   // je Geraet ein Element: beliebige Zeichen in Kurzname/ID moeglich
            {
                w.WriteStartElement("Geraet"); w.WriteAttributeString("kurz", ch.Short); w.WriteAttributeString("id", ch.Id); w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        static EquipAssign ReadEquip(XmlElement q)
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
            a.Alternate = q.GetAttribute("abwechselnd") == "1"; a.Fallback = q.GetAttribute("ausweich"); a.Note = q.GetAttribute("notiz"); a.ParaAfter = q.GetAttribute("absatz") == "1";
            return a;
        }

        static void WritePuffers(XmlWriter w, PackageStep s)
        {
            foreach (PufferSpec p in s.Puffers)
            {
                w.WriteStartElement("Puffer"); w.WriteAttributeString("name", p.Name); w.WriteAttributeString("typ", p.Typ);
                if (p.NameNo > 1) w.WriteAttributeString("nameno", I(p.NameNo));
                if (p.AltMul != 1) w.WriteAttributeString("altmul", I(p.AltMul));
                if (p.AltAdd != 0) w.WriteAttributeString("altadd", I(p.AltAdd));
                if (p.OffsetDays.HasValue) w.WriteAttributeString("versatz", I(p.OffsetDays.Value));
                foreach (EquipAssign a in p.Lines) WriteEquip(w, a);
                w.WriteEndElement();
            }
            foreach (PufferShare g in s.PufferShared)
            {
                w.WriteStartElement("Gemeinsam");
                if (g.Scope != 0) w.WriteAttributeString("bereich", I(g.Scope));
                if (g.After >= 0) w.WriteAttributeString("nach", I(g.After));
                WriteEquip(w, g.Eq);
                w.WriteEndElement();
            }
        }

        // Liest <Puffer>/<Gemeinsam>; mit eigenen Puffern ersetzen sie die gemeinsame Liste (die nur fuer aeltere exe mitgeschrieben wird)
        static void ReadPuffers(XmlElement e, PackageStep s)
        {
            foreach (XmlElement q in Elements(e))
            {
                if (q.Name == "Puffer" && s.Puffers.Count < 99)
                {
                    PufferSpec p = new PufferSpec(); p.Name = q.GetAttribute("name"); p.Typ = q.GetAttribute("typ");
                    p.NameNo = Clamp(ToInt(q.GetAttribute("nameno"), 0), 0, 99);
                    p.AltMul = Clamp(ToInt(q.GetAttribute("altmul"), 1), 1, 99); p.AltAdd = Clamp(ToInt(q.GetAttribute("altadd"), 0), 0, 98);
                    if (q.HasAttribute("versatz")) p.OffsetDays = Clamp(ToInt(q.GetAttribute("versatz"), 0), -PackageStep.MaxCardOffset, PackageStep.MaxCardOffset);
                    foreach (XmlElement x in Elements(q)) if (x.Name == "Equipment") p.Lines.Add(ReadEquip(x));
                    s.Puffers.Add(p);
                }
                else if (q.Name == "Gemeinsam")
                {
                    PufferShare g = new PufferShare(); g.Scope = Math.Max(0, ToInt(q.GetAttribute("bereich"), 0)); g.After = Clamp(ToInt(q.GetAttribute("nach"), -1), -1, 99);
                    foreach (XmlElement x in Elements(q)) if (x.Name == "Equipment") { g.Eq = ReadEquip(x); break; }
                    s.PufferShared.Add(g);
                }
            }
            if (s.Puffers.Count > 0) s.SyncPuffers();
            else s.PufferShared.Clear();
        }
    }
}
