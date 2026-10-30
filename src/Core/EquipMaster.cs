using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // Equipment-Stammdaten: Klassen (mit Eigenschaftsfeldern), Reihen (Nummernbereich/Liste mit Reihen-Notiz), Geraete,
    // "verwendbar mit"-Regeln und Ersatz-Beziehungen. Liegt im Catalog (wird mit gespeichert und gesichert).
    public class EquipProp
    {
        public string Name = "", Value = "";
        public EquipProp() { }
        public EquipProp(string n, string v) { Name = n; Value = v; }
    }

    public class EquipClass
    {
        public string Name = "";
        public List<string> Fields = new List<string>();     // Eigenschaftsfelder dieser Klasse (Vorlage fuer Geraete), z. B. Volumen, Hoehe
        public List<StatusRule> Rules = new List<StatusRule>();   // erlaubte/verbotene Statusuebergaenge der Art (Hintergrundlogik, EquipMaster.IsLegal)
        public EquipClass Clone() { EquipClass c = (EquipClass)MemberwiseClone(); c.Fields = new List<string>(Fields); c.Rules = new List<StatusRule>(Rules); return c; }
        // Feld mit Namensende " (Link)": Wert ist eine Adresse (http/https, Laufwerk, UNC) und in der Tabelle/Karte anklickbar
        public static bool IsLink(string field) { return field != null && field.EndsWith("(Link)", StringComparison.OrdinalIgnoreCase); }
        public override string ToString() { return Name; }
    }

    public class EquipDevice
    {
        public string Id = "", Class = "", Note = "";
        public string Parent = "";                           // Hauptgeraet (Skid/Tank), zu dem dieses Zubehoerteil gehoert; leer = eigenstaendig
        public bool Active = true;                          // false = in Wartung / nicht verfuegbar
        public List<EquipProp> Props = new List<EquipProp>();
        public List<StatusRule> Rules = new List<StatusRule>();   // gerätespezifische Statusuebergaenge (vor denen der Art)

        public EquipDevice Clone()
        {
            EquipDevice d = (EquipDevice)MemberwiseClone(); d.Props = new List<EquipProp>(); d.Rules = new List<StatusRule>(Rules);
            foreach (EquipProp p in Props) d.Props.Add(new EquipProp(p.Name, p.Value));
            return d;
        }
        public override string ToString() { return Active ? Id : Id + " (Wartung)"; }
    }

    // Reihe: Geraete einer Klasse, die zusammengehoeren (Nummernbereich von..bis und/oder Einzel-IDs) mit gemeinsamer Notiz.
    public class EquipSeries
    {
        public string Name = "", Class = "", From = "", To = "", Note = "";
        public List<string> Ids = new List<string>();

        public EquipSeries Clone() { EquipSeries s = (EquipSeries)MemberwiseClone(); s.Ids = new List<string>(Ids); return s; }
        public override string ToString() { return Name; }

        // Nummer = letzte Ziffernfolge der ID, Praefix = alles davor (T-0007 -> "T-", 7)
        static bool Split(string id, out string prefix, out long num)
        {
            prefix = ""; num = 0;
            int e = id.Length; int s = e;
            while (s > 0 && id[s - 1] >= '0' && id[s - 1] <= '9') s--;
            if (s == e || e - s > 12) return false;
            prefix = id.Substring(0, s); num = long.Parse(id.Substring(s));
            return true;
        }

        public bool Contains(string id)
        {
            if (Ids.Contains(id)) return true;
            string pf, pt, p; long nf, nt, n;
            if (From.Length == 0 || To.Length == 0) return false;
            if (!Split(From, out pf, out nf) || !Split(To, out pt, out nt) || !Split(id, out p, out n)) return false;
            return pf == pt && p == pf && n >= Math.Min(nf, nt) && n <= Math.Max(nf, nt);
        }
    }

    // "Verwendbar mit": From (z. B. Skid) kann mit den Geraeten in To (z. B. Filtergehaeuse) eingesetzt werden. Gilt in beide Richtungen.
    public class EquipLink
    {
        public string From = "", Note = "";
        public List<string> To = new List<string>();
        public EquipLink Clone() { EquipLink l = (EquipLink)MemberwiseClone(); l.To = new List<string>(To); return l; }
        public override string ToString() { return From + " → " + string.Join(", ", To.ToArray()); }
    }

    // Ersatz: Id kann Replaces vertreten (z. B. bei Wartung); Both = auch umgekehrt. Note = entscheidender Unterschied ("5 Zoll statt 10 Zoll").
    public class EquipSubst
    {
        public string Id = "", Replaces = "", Note = "";
        public bool Both;
        public EquipSubst Clone() { return (EquipSubst)MemberwiseClone(); }
        public override string ToString() { return Id + (Both ? " ↔ " : " ersetzt ") + Replaces; }
    }

    public partial class EquipMaster
    {
        public List<EquipClass> Classes = new List<EquipClass>();
        public List<EquipSeries> Series = new List<EquipSeries>();
        public List<EquipDevice> Devices = new List<EquipDevice>();
        public List<EquipLink> Links = new List<EquipLink>();
        public List<EquipSubst> Substs = new List<EquipSubst>();

        public EquipMaster Clone()
        {
            EquipMaster m = new EquipMaster();
            foreach (EquipClass c in Classes) m.Classes.Add(c.Clone());
            foreach (EquipSeries s in Series) m.Series.Add(s.Clone());
            foreach (EquipDevice d in Devices) m.Devices.Add(d.Clone());
            foreach (EquipLink l in Links) m.Links.Add(l.Clone());
            foreach (EquipSubst s in Substs) m.Substs.Add(s.Clone());
            foreach (EquipPending p in Pending) m.Pending.Add(p.Clone());
            return m;
        }

        public EquipDevice Device(string id)
        {
            foreach (EquipDevice d in Devices) if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        public EquipClass Class(string name)
        {
            foreach (EquipClass c in Classes) if (c.Name == name) return c;
            return null;
        }

        public List<EquipSeries> SeriesOf(string id)
        {
            List<EquipSeries> r = new List<EquipSeries>();
            foreach (EquipSeries s in Series) if (s.Contains(id)) r.Add(s);
            return r;
        }

        // Geraete, die zur Reihe gehoeren (Bereich oder Einzel-ID, passende Klasse)
        public List<EquipDevice> Members(EquipSeries s)
        {
            List<EquipDevice> r = new List<EquipDevice>();
            foreach (EquipDevice d in Devices) if ((s.Class.Length == 0 || d.Class == s.Class) && s.Contains(d.Id)) r.Add(d);
            return r;
        }

        // Alle Geraete, die mit id verwendbar sind (beide Richtungen der Regeln; Regelseiten duerfen "@Geraeteart" oder "#Gruppe" sein, siehe EquipExtra.cs)
        public List<string> CompatibleWith(string id)
        {
            List<string> r = new List<string>();
            foreach (EquipLink l in Links)
            {
                if (TokenHas(l.From, id)) { foreach (string t in l.To) AddIds(r, t, id); }
                else if (l.To.Exists(delegate(string t) { return TokenHas(t, id); })) AddIds(r, l.From, id);
            }
            return r;
        }

        // Geraete, die id vertreten koennen (Ersatz-Beziehungen), jeweils mit Unterschieds-Notiz
        public List<EquipSubst> SubstitutesFor(string id)
        {
            List<EquipSubst> r = new List<EquipSubst>();
            foreach (EquipSubst s in Substs)
            {
                if (string.Equals(s.Replaces, id, StringComparison.OrdinalIgnoreCase)) r.Add(s);
                else if (s.Both && string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                { EquipSubst x = s.Clone(); x.Id = s.Replaces; x.Replaces = s.Id; r.Add(x); }
            }
            return r;
        }

        // Zugehoerigkeit in einem gemeinsam eingesetzten Geraetesatz: Fehler, wenn ein Zubehoerteil dabei ist, sein Hauptgeraet fehlt,
        // aber ein anderes Geraet derselben Klasse wie das Hauptgeraet (Filter von Tank A mit Tank B). Mit A oder allein = ok.
        public List<string> CheckBelongs(IEnumerable<string> ids)
        {
            List<string> set = new List<string>(), err = new List<string>();
            foreach (string id in ids) if (!string.IsNullOrEmpty(id) && !Has(set, id)) set.Add(id);
            foreach (string id in set)
            {
                EquipDevice d = Device(id), main = d != null && d.Parent.Length > 0 ? Device(d.Parent) : null;
                if (main == null || main.Class.Length == 0 || Has(set, d.Parent)) continue;
                foreach (string o in set)
                {
                    EquipDevice x = Device(o);
                    if (x == null || x == d || x.Class != main.Class) continue;
                    err.Add("Gerät " + d.Id + " gehört zu " + main.Id + ", eingeplant ist aber " + x.Id + ".");
                    break;
                }
            }
            return err;
        }

        static bool Has(List<string> l, string id) { return l.Exists(delegate(string s) { return string.Equals(s, id, StringComparison.OrdinalIgnoreCase); }); }

        // Hinweistext zu einem Geraet (Klasse, Eigenschaften, Notiz, Reihen-Notizen, Wartung) fuer Tooltips; leer, wenn unbekannt.
        public string Info(string id)
        {
            EquipDevice d = Device(id);
            if (d == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append(d.Id); if (d.Class.Length > 0) sb.Append(" (").Append(d.Class).Append(')');
            if (!d.Active) sb.Append("  – IN WARTUNG");
            if (d.Parent.Length > 0) sb.Append("\ngehört zu ").Append(d.Parent);
            foreach (EquipProp p in d.Props) if (p.Value.Length > 0) sb.Append("\n").Append(p.Name).Append(": ").Append(p.Value);
            if (d.Note.Length > 0) sb.Append("\n").Append(d.Note);
            foreach (EquipSeries s in SeriesOf(id)) if (s.Note.Length > 0) sb.Append("\n").Append(s.Name).Append(": ").Append(s.Note);
            return sb.ToString();
        }

        // Erfundene Beispieldaten (Repo ist oeffentlich), passend zu den Positionen im Standardkatalog.
        public static EquipMaster Default()
        {
            EquipMaster m = new EquipMaster();
            m.Classes.Add(Cls("Tank", "Volumen", "Material"));
            m.Classes.Add(Cls("Skid", "Typ"));
            m.Classes.Add(Cls("Filtergehäuse", "Größe", "Höhe"));
            m.Classes.Add(Cls("Pumpe", "Typ"));

            m.Devices.Add(Dev("H1-T-0102", "Tank", "Volumen", "250 L"));
            m.Devices.Add(Dev("H1-T-0103", "Tank", "Volumen", "250 L"));
            m.Devices.Add(Dev("H1-T-0104", "Tank", "Volumen", "250 L"));
            m.Devices.Add(Dev("H1-X-0101", "Skid", "Typ", "TFF"));
            m.Devices.Add(Dev("H1-X-0102", "Skid", "Typ", "TFF"));
            m.Devices.Add(Dev("H1-F-0101-01", "Filtergehäuse", "Größe", "10 Zoll"));
            m.Devices.Add(Dev("H1-F-0101-02", "Filtergehäuse", "Größe", "5 Zoll"));
            m.Devices.Add(Dev("H1-F-0101-04", "Filtergehäuse", "Größe", "5 Zoll"));
            foreach (EquipDevice f in m.Devices) if (f.Class == "Filtergehäuse") f.Parent = "H1-X-0101";   // Gehaeuse gehoeren zu diesem Skid

            EquipSeries s = new EquipSeries(); s.Name = "Tanks 0103 bis 0104"; s.Class = "Tank"; s.From = "H1-T-0103"; s.To = "H1-T-0104";
            s.Note = "Probenahmeport neben dem Bodenablass anbauen"; m.Series.Add(s);

            EquipLink l = new EquipLink(); l.From = "H1-X-0101"; l.To.AddRange(new[] { "H1-F-0101-01", "H1-F-0101-02", "H1-F-0101-04" });
            l.Note = "Gehäuse dieses Skids"; m.Links.Add(l);

            EquipSubst x = new EquipSubst(); x.Id = "H1-F-0101-02"; x.Replaces = "H1-F-0101-01"; x.Note = "5 Zoll statt 10 Zoll"; m.Substs.Add(x);
            return m;
        }

        static EquipClass Cls(string name, params string[] fields) { EquipClass c = new EquipClass(); c.Name = name; c.Fields.AddRange(fields); return c; }

        static EquipDevice Dev(string id, string cls, string prop, string val)
        {
            EquipDevice d = new EquipDevice(); d.Id = id; d.Class = cls; d.Props.Add(new EquipProp(prop, val)); return d;
        }
    }
}
