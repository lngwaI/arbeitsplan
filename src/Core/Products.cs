using System;
using System.Collections.Generic;
using System.Xml;

namespace Arbeitsplan.Core
{
    // Position im Produktprofil. Own = false: es gilt die globale Geraeteliste der Position (EquipPosition), sonst Ids.
    // Fixed = festes Geraet je Kampagne (kein "abwechselnd").
    public class ProfilePos
    {
        public string Short = "";
        public bool Own, Fixed;
        public List<string> Ids = new List<string>();
        public ProfilePos Clone() { ProfilePos p = (ProfilePos)MemberwiseClone(); p.Ids = new List<string>(Ids); return p; }
    }

    // Austauschgruppe eines Produkts: diese Geraete vertreten sich gegenseitig (Ausweich-Vorschlag zuerst).
    public class SwapGroup
    {
        public string Name = "";
        public List<string> Ids = new List<string>();
        public SwapGroup Clone() { SwapGroup g = (SwapGroup)MemberwiseClone(); g.Ids = new List<string>(Ids); return g; }
    }

    // Equipment je Produkt (Produktcode XXYY): ueberschreibt je Position den globalen Standard; ohne Profil gilt global.
    public class ProductProfile
    {
        public string Code = "", Name = "";
        public List<ProfilePos> Positions = new List<ProfilePos>();
        public List<SwapGroup> Swaps = new List<SwapGroup>();

        public ProductProfile Clone()
        {
            ProductProfile p = (ProductProfile)MemberwiseClone();
            p.Positions = new List<ProfilePos>(); foreach (ProfilePos x in Positions) p.Positions.Add(x.Clone());
            p.Swaps = new List<SwapGroup>(); foreach (SwapGroup g in Swaps) p.Swaps.Add(g.Clone());
            return p;
        }

        public ProfilePos Pos(string shortName)
        {
            foreach (ProfilePos p in Positions) if (string.Equals(p.Short, shortName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
        public override string ToString() { return Code + (Name.Length > 0 ? " " + Name : ""); }
    }

    public partial class Catalog
    {
        public List<ProductProfile> Products = new List<ProductProfile>();

        public ProductProfile Product(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (ProductProfile p in Products) if (p.Code == code) return p;
            return null;
        }

        // Erlaubte Geraete einer Position fuer das Produkt (Kopie): eigene Liste des Profils, sonst globaler Standard.
        public List<string> Allowed(string code, string shortName)
        {
            ProductProfile pr = Product(code);
            ProfilePos pp = pr != null ? pr.Pos(shortName) : null;
            if (pp != null && pp.Own) return new List<string>(pp.Ids);
            EquipPosition g = Position(shortName);
            return g != null ? new List<string>(g.Ids) : new List<string>();
        }

        public bool IsFixed(string code, string shortName)
        {
            ProductProfile pr = Product(code);
            ProfilePos pp = pr != null ? pr.Pos(shortName) : null;
            return pp != null && pp.Fixed;
        }

        // Austauschgeraete fuer id (ohne id selbst) aus den Gruppen des Produkts; id leer = Gruppen der erlaubten Geraete der Position.
        public List<string> SwapsFor(string code, string shortName, string id)
        {
            List<string> r = new List<string>();
            ProductProfile pr = Product(code);
            if (pr == null) return r;
            List<string> keys = string.IsNullOrEmpty(id) ? Allowed(code, shortName) : new List<string>(new[] { id });
            foreach (SwapGroup g in pr.Swaps)
                if (g.Ids.Exists(delegate(string x) { return keys.Contains(x); }))
                    foreach (string x in g.Ids) if (x != id && !r.Contains(x)) r.Add(x);
            return r;
        }

        // "Wie Produkt … kopieren": Profil von fromCode als toCode anlegen (vorhandenes toCode wird ersetzt); null = Quelle fehlt.
        public ProductProfile CopyProduct(string fromCode, string toCode, string name)
        {
            ProductProfile src = Product(fromCode);
            if (src == null || string.IsNullOrEmpty(toCode)) return null;
            ProductProfile c = src.Clone(); c.Code = toCode; c.Name = name ?? "";
            int i = Products.IndexOf(Product(toCode));
            if (i >= 0) Products[i] = c; else Products.Add(c);
            return c;
        }

        // Zugehoerigkeit je Schritt und Lauf (bei "abwechselnd" das Geraet dieses Laufs); Ausweichgeraete zaehlen nicht.
        public List<string> CheckPackage(Package p)
        {
            List<string> err = new List<string>();
            foreach (PackageStep s in p.Steps)
            {
                if (s.IsEmpty) continue;
                for (int run = 0; run < Math.Max(1, s.Count); run++)
                {
                    List<string> ids = new List<string>();
                    foreach (EquipAssign a in s.Equipment) { EquipChoice c = a.ChoiceFor(run); if (c != null) ids.Add(c.Id); }
                    foreach (string e in Master.CheckBelongs(ids)) { string m = s.Label(p.ProductCode, run) + ": " + e; if (!err.Contains(m)) err.Add(m); }
                }
            }
            return err;
        }

        // Blocktext: Fehler = Zugehoerigkeit der erkannten Equipment-Nummern; warnings = Geraete, die fuer das Produkt
        // der Laufnummer(n) im Text nicht erlaubt sind (nur Hinweis).
        public List<string> CheckText(string content, List<string> warnings)
        {
            List<string> ids = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in EquipmentMatcher.Pattern.Matches(content ?? "")) if (!ids.Contains(m.Value)) ids.Add(m.Value);
            foreach (RunRef r in RunNumbers.Find(content))
            {
                ProductProfile pr = Product(r.Product);
                if (pr == null) continue;
                List<string> ok = new List<string>();
                foreach (EquipPosition g in Positions) ok.AddRange(Allowed(pr.Code, g.Short));
                foreach (ProfilePos pp in pr.Positions) ok.AddRange(Allowed(pr.Code, pp.Short));
                foreach (string id in ids)
                {
                    string w = "Gerät " + id + " ist für Produkt " + pr.Code + " nicht vorgesehen.";
                    if (!ok.Contains(id) && !warnings.Contains(w)) warnings.Add(w);
                }
            }
            return Master.CheckBelongs(ids);
        }
    }

    // XML im Katalog: <Produkt code name><Pos kurz eigen fest><Id v/></Pos><Tausch name><Id v/></Tausch></Produkt>
    static class ProductXml
    {
        public static void Write(XmlWriter w, List<ProductProfile> list)
        {
            foreach (ProductProfile p in list)
            {
                w.WriteStartElement("Produkt"); w.WriteAttributeString("code", p.Code); w.WriteAttributeString("name", p.Name);
                foreach (ProfilePos x in p.Positions)
                {
                    w.WriteStartElement("Pos"); w.WriteAttributeString("kurz", x.Short);
                    w.WriteAttributeString("eigen", x.Own ? "1" : "0"); w.WriteAttributeString("fest", x.Fixed ? "1" : "0");
                    Ids(w, x.Ids); w.WriteEndElement();
                }
                foreach (SwapGroup g in p.Swaps) { w.WriteStartElement("Tausch"); w.WriteAttributeString("name", g.Name); Ids(w, g.Ids); w.WriteEndElement(); }
                w.WriteEndElement();
            }
        }

        static void Ids(XmlWriter w, List<string> ids) { foreach (string id in ids) { w.WriteStartElement("Id"); w.WriteAttributeString("v", id); w.WriteEndElement(); } }

        public static void Read(XmlElement katalog, List<ProductProfile> list)
        {
            foreach (XmlElement e in Kids(katalog, "Produkt"))
            {
                ProductProfile p = new ProductProfile(); p.Code = e.GetAttribute("code"); p.Name = e.GetAttribute("name");
                foreach (XmlElement x in Kids(e, "Pos"))
                {
                    ProfilePos pp = new ProfilePos(); pp.Short = x.GetAttribute("kurz"); pp.Own = x.GetAttribute("eigen") == "1"; pp.Fixed = x.GetAttribute("fest") == "1";
                    foreach (XmlElement i in Kids(x, "Id")) pp.Ids.Add(i.GetAttribute("v"));
                    p.Positions.Add(pp);
                }
                foreach (XmlElement x in Kids(e, "Tausch"))
                {
                    SwapGroup g = new SwapGroup(); g.Name = x.GetAttribute("name");
                    foreach (XmlElement i in Kids(x, "Id")) g.Ids.Add(i.GetAttribute("v"));
                    p.Swaps.Add(g);
                }
                list.Add(p);
            }
        }

        static IEnumerable<XmlElement> Kids(XmlNode n, string name)
        {
            foreach (XmlNode c in n.ChildNodes) { XmlElement e = c as XmlElement; if (e != null && e.Name == name) yield return e; }
        }
    }
}
