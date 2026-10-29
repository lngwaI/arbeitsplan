using System;
using System.Collections.Generic;
using System.Xml;

namespace Arbeitsplan.Core
{
    // UX-1: Pufferpaket je Produkt. Position einer Puffer-Vorlage mit vorbelegten Geraeten (Ids leer = Geraet im Schritt waehlen).
    // Shared = Zeile gilt fuer mehrere Puffer gemeinsam (z. B. WFI-Vorlagetank), siehe PufferShare.
    public class PufferTemplatePos
    {
        public string Short = "", Note = "";
        public List<string> Ids = new List<string>();
        public bool Alternate, Shared;

        public PufferTemplatePos Clone() { PufferTemplatePos p = (PufferTemplatePos)MemberwiseClone(); p.Ids = new List<string>(Ids); return p; }

        public EquipAssign ToAssign()
        {
            EquipAssign a = new EquipAssign(); a.Short = Short; a.Note = Note;
            foreach (string id in Ids) a.Choices.Add(new EquipChoice(Short, id));
            a.Alternate = Alternate && Ids.Count > 1;
            return a;
        }
    }

    // Vorlage fuer einen Puffer: Name (leer = aus dem Schritt-Titel), Typ (mobil/stationaer), Positionen.
    public class PufferTemplate
    {
        public string Name = "", Typ = "mobil";
        public List<PufferTemplatePos> Positions = new List<PufferTemplatePos>();

        public PufferTemplate Clone()
        {
            PufferTemplate t = (PufferTemplate)MemberwiseClone();
            t.Positions = new List<PufferTemplatePos>(); foreach (PufferTemplatePos p in Positions) t.Positions.Add(p.Clone());
            return t;
        }
    }

    public partial class ProductProfile
    {
        public List<PufferTemplate> Puffer = new List<PufferTemplate>();   // Pufferpaket des Produkts; leer = Standardpuffer des Katalogs

        void ClonePuffer(ProductProfile c) { c.Puffer = new List<PufferTemplate>(); foreach (PufferTemplate t in Puffer) c.Puffer.Add(t.Clone()); }
    }

    public partial class Catalog
    {
        public const string StandardPufferName = "Standardpuffer";   // Baustein: Positionen + Standardanzahl des Standardpuffers (kein Schritt-Baustein)

        // Standardpuffer als Vorlage ohne Geraete (je Position so viele Zeilen wie die Standard-Anzahl); null = Baustein fehlt
        public PufferTemplate StandardPuffer()
        {
            Baustein b = Baustein_(StandardPufferName);
            if (b == null) return null;
            PufferTemplate t = new PufferTemplate(); t.Typ = b.Variant.Length > 0 ? b.Variant : "mobil";
            foreach (string sh in b.Positions)
                for (int i = 0; i < b.DefaultCount(sh); i++)
                {
                    PufferTemplatePos p = new PufferTemplatePos(); p.Short = sh;
                    t.Positions.Add(p);
                }
            return t;
        }

        // Hinweise (keine Sperre): Puffer-Geraete, die fuer das Produkt des Pakets an dieser Position nicht vorgesehen sind.
        // Zugehoerigkeit (Zubehoer ohne Hauptgeraet) prueft CheckPackage je Puffer. Erlaubt = Allowed(code, kurz); leere Liste = frei.
        public List<string> CheckPufferProduct(Package p)
        {
            List<string> warn = new List<string>();
            if (string.IsNullOrEmpty(p.ProductCode)) return warn;
            foreach (PackageStep s in p.Steps)
            {
                if (s.IsEmpty || !s.IsBuffer) continue;
                int n = s.Puffers.Count > 0 ? s.Puffers.Count : s.LegacyPuffers().Count;
                for (int run = 0; run < p.RunCountOf(s); run++)
                    for (int k = 0; k < n; k++)
                        foreach (EquipChoice c in s.PufferChoices(k, run))
                        {
                            List<string> ok = Allowed(p.ProductCode, c.Short);
                            string m = "Puffer " + (k + 1) + " (" + s.Label(p.Prefix, run) + "): Gerät " + c.Id + " ist für Produkt " + p.ProductCode + " nicht vorgesehen.";
                            if (c.Id.Length > 0 && ok.Count > 0 && !ok.Contains(c.Id) && !warn.Contains(m)) warn.Add(m);
                        }
            }
            return warn;
        }
    }

    // XML im Produktprofil: <Puffer name typ><P kurz notiz abwechselnd gemeinsam><Id v/></P></Puffer> (aeltere exe ignoriert die Elemente)
    static partial class ProductXml
    {
        public static void WritePuffer(XmlWriter w, List<PufferTemplate> list)
        {
            foreach (PufferTemplate t in list)
            {
                w.WriteStartElement("Puffer"); w.WriteAttributeString("name", t.Name); w.WriteAttributeString("typ", t.Typ);
                foreach (PufferTemplatePos x in t.Positions)
                {
                    w.WriteStartElement("P"); w.WriteAttributeString("kurz", x.Short); w.WriteAttributeString("notiz", x.Note);
                    if (x.Alternate) w.WriteAttributeString("abwechselnd", "1");
                    if (x.Shared) w.WriteAttributeString("gemeinsam", "1");
                    Ids(w, x.Ids); w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }

        public static void ReadPuffer(XmlElement produkt, List<PufferTemplate> list)
        {
            foreach (XmlElement e in Kids(produkt, "Puffer"))
            {
                PufferTemplate t = new PufferTemplate(); t.Name = e.GetAttribute("name"); t.Typ = e.GetAttribute("typ");
                foreach (XmlElement x in Kids(e, "P"))
                {
                    PufferTemplatePos p = new PufferTemplatePos(); p.Short = x.GetAttribute("kurz"); p.Note = x.GetAttribute("notiz");
                    p.Alternate = x.GetAttribute("abwechselnd") == "1"; p.Shared = x.GetAttribute("gemeinsam") == "1";
                    foreach (XmlElement i in Kids(x, "Id")) p.Ids.Add(i.GetAttribute("v"));
                    t.Positions.Add(p);
                }
                list.Add(t);
            }
        }
    }
}
