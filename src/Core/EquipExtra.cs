using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arbeitsplan.Core
{
    // Erlaubter/unerlaubter Statusuebergang (von -> nach) einer Geraeteart oder eines Geraets. Nur Hintergrundlogik (EquipMaster.IsLegal).
    public class StatusRule
    {
        public EquipStatus From, To;
        public bool Legal;
        public StatusRule() { }
        public StatusRule(EquipStatus from, EquipStatus to, bool legal) { From = from; To = to; Legal = legal; }
    }

    // Erweiterungen der Stammdaten: Regeln auf Art-/Gruppenebene, Matrix-Paare, Serienanlage, Vorbelegung, Vertraeglichkeitspruefung, Statusuebergaenge.
    public partial class EquipMaster
    {
        // ---------- "Passt zu" auf Art-/Gruppenebene ----------
        // Eine Regelseite (EquipLink.From / .To) ist eine Geraete-ID, "@Geraeteart" (alle Geraete der Art) oder "#Gruppenname" (alle Mitglieder).

        public static bool IsToken(string t) { return t.Length > 0 && (t[0] == '@' || t[0] == '#'); }

        public bool TokenHas(string token, string id)
        {
            if (token.Length == 0) return false;
            if (token[0] == '@') { EquipDevice d = Device(id); return d != null && d.Class == token.Substring(1); }
            if (token[0] == '#')
            {
                foreach (EquipSeries s in Series)
                    if (s.Name == token.Substring(1)) { EquipDevice d = Device(id); if (d != null && (s.Class.Length == 0 || d.Class == s.Class) && s.Contains(d.Id)) return true; }
                return false;
            }
            return string.Equals(token, id, StringComparison.OrdinalIgnoreCase);
        }

        // Geraete-IDs hinter einer Regelseite (ohne self)
        public List<string> Expand(string token, string self)
        {
            List<string> r = new List<string>();
            AddIds(r, token, self);
            return r;
        }

        void AddIds(List<string> r, string token, string self)
        {
            if (token.Length == 0) return;
            if (!IsToken(token)) { if (!string.Equals(token, self, StringComparison.OrdinalIgnoreCase) && !r.Contains(token)) r.Add(token); return; }
            foreach (EquipDevice d in Devices)
                if (!string.Equals(d.Id, self, StringComparison.OrdinalIgnoreCase) && TokenHas(token, d.Id) && !r.Contains(d.Id)) r.Add(d.Id);
        }

        // Regelseite umbenannt (Gruppe/Art): Verweise mitziehen
        public void RenameToken(string old, string now)
        {
            if (old == now || old.Length == 0) return;
            foreach (EquipLink l in Links) { if (l.From == old) l.From = now; for (int i = 0; i < l.To.Count; i++) if (l.To[i] == old) l.To[i] = now; }
        }

        public void RemoveToken(string token)
        {
            Links.RemoveAll(delegate(EquipLink l) { return l.From == token; });
            foreach (EquipLink l in Links) l.To.RemoveAll(delegate(string t) { return t == token; });
            Links.RemoveAll(delegate(EquipLink l) { return l.To.Count == 0; });
        }

        // Warnung (kein harter Fehler), wenn im Geraetesatz zwei Geraete zusammenstehen, die laut "Passt zu" nicht zusammengehoeren:
        // fuer ein Geraet gibt es Regeln zur Geraeteart des anderen, das andere steht aber nicht darunter.
        public List<string> CheckCompat(IEnumerable<string> ids)
        {
            List<string> set = new List<string>(), warn = new List<string>();
            foreach (string id in ids) if (!string.IsNullOrEmpty(id) && Device(id) != null && !Has(set, id)) set.Add(id);
            for (int i = 0; i < set.Count; i++)
                for (int j = i + 1; j < set.Count; j++)
                {
                    EquipDevice a = Device(set[i]), b = Device(set[j]);
                    if (a.Class.Length == 0 || b.Class.Length == 0 || a.Class == b.Class) continue;
                    List<string> ca = CompatibleWith(a.Id), cb = CompatibleWith(b.Id);
                    if (Has(ca, b.Id) || Has(cb, a.Id)) continue;
                    if (HasClass(ca, b.Class) || HasClass(cb, a.Class)) warn.Add("Gerät " + b.Id + " passt laut Stammdaten nicht zu " + a.Id + ".");
                }
            return warn;
        }

        bool HasClass(List<string> ids, string cls)
        {
            foreach (string id in ids) { EquipDevice d = Device(id); if (d != null && d.Class == cls) return true; }
            return false;
        }

        // ---------- Matrix (Paar a/b, nur Geraete-IDs) ----------

        public bool HasPair(string a, string b) { return Has(CompatibleWith(a), b); }

        // Link, der genau dieses Paar direkt (ohne Art/Gruppe) enthaelt; swapped = b steht links
        EquipLink PairLink(string a, string b, out bool swapped)
        {
            swapped = false;
            foreach (EquipLink l in Links)
            {
                if (string.Equals(l.From, a, StringComparison.OrdinalIgnoreCase) && Has(l.To, b)) return l;
                if (string.Equals(l.From, b, StringComparison.OrdinalIgnoreCase) && Has(l.To, a)) { swapped = true; return l; }
            }
            return null;
        }

        public string PairNote(string a, string b) { bool sw; EquipLink l = PairLink(a, b, out sw); return l != null ? l.Note : ""; }

        public void SetPair(string a, string b, bool on)
        {
            bool sw; EquipLink l = PairLink(a, b, out sw);
            if (on)
            {
                if (HasPair(a, b)) return;
                l = Links.Find(delegate(EquipLink x) { return string.Equals(x.From, a, StringComparison.OrdinalIgnoreCase) && x.Note.Length == 0; });
                if (l == null) { l = new EquipLink(); l.From = a; Links.Add(l); }
                l.To.Add(b);
                return;
            }
            while (l != null)
            {
                string other = sw ? a : b;
                l.To.RemoveAll(delegate(string t) { return string.Equals(t, other, StringComparison.OrdinalIgnoreCase); });
                Links.RemoveAll(delegate(EquipLink x) { return x.To.Count == 0; });
                l = PairLink(a, b, out sw);
            }
        }

        public void SetPairNote(string a, string b, string note)
        {
            SetPair(a, b, true);
            bool sw; EquipLink l = PairLink(a, b, out sw);
            if (l == null) return;   // nur ueber Art/Gruppe verbunden: keine Notiz je Paar
            string other = sw ? a : b;
            if (l.To.Count > 1)
            {   // Notiz gilt nur fuer dieses Paar: eigener Eintrag
                l.To.RemoveAll(delegate(string t) { return string.Equals(t, other, StringComparison.OrdinalIgnoreCase); });
                EquipLink n = new EquipLink(); n.From = l.From; n.To.Add(other); Links.Add(n); l = n;
            }
            l.Note = note;
        }

        // ---------- Serienanlage und Vorbelegung ----------

        // Letzte Ziffernfolge: Praefix, Zahl, Breite (T-0007 -> "T-", 7, 4)
        static bool SplitNum(string id, out string prefix, out long num, out int width)
        {
            prefix = ""; num = 0; width = 0;
            int e = id.Length, s = e;
            while (s > 0 && id[s - 1] >= '0' && id[s - 1] <= '9') s--;
            if (s == e || e - s > 12) return false;
            prefix = id.Substring(0, s); num = long.Parse(id.Substring(s), CultureInfo.InvariantCulture); width = e - s;
            return true;
        }

        public const int MaxSerial = 200;

        // "H1-F-0101" bis "0110" oder "H1-F-0110": alle IDs dazwischen (Breite der Nummer bleibt). null = Eingabe unbrauchbar (error).
        public static List<string> SerialIds(string from, string to, out string error)
        {
            error = null; from = (from ?? "").Trim(); to = (to ?? "").Trim();
            string pf, pt; long nf, nt; int wf, wt;
            if (!SplitNum(from, out pf, out nf, out wf)) { error = "Die erste ID muss mit einer Nummer enden (z. B. H1-F-0101)."; return null; }
            if (to.Length > 0 && to.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').Length == 0) to = pf + to;   // nur die Nummer eingegeben
            if (!SplitNum(to, out pt, out nt, out wt) || pt != pf) { error = "Die letzte ID muss denselben Anfang haben wie die erste (" + pf + "…)."; return null; }
            if (nt < nf) { error = "Die letzte Nummer ist kleiner als die erste."; return null; }
            if (nt - nf + 1 > MaxSerial) { error = "Höchstens " + MaxSerial + " Geräte auf einmal."; return null; }
            List<string> r = new List<string>();
            for (long n = nf; n <= nt; n++) r.Add(pf + n.ToString(CultureInfo.InvariantCulture).PadLeft(wf, '0'));
            return r;
        }

        // Naechste freie ID nach id (letzte Nummer +1, Breite bleibt); "" wenn keine Nummer am Ende
        public string NextId(string id)
        {
            string p; long n; int w;
            if (!SplitNum(id ?? "", out p, out n, out w)) return "";
            for (int i = 0; i < 1000; i++)
            {
                n++;
                string c = p + n.ToString(CultureInfo.InvariantCulture).PadLeft(w, '0');
                if (Device(c) == null) return c;
            }
            return "";
        }

        // Vorschlag fuer ein neues Geraet der Art: die Nummer nach dem letzten Geraet dieser Art
        public string SuggestId(string cls)
        {
            for (int i = Devices.Count - 1; i >= 0; i--) if (Devices[i].Class == cls) { string n = NextId(Devices[i].Id); if (n.Length > 0) return n; }
            return "";
        }

        // Serienanlage: legt fuer jede noch freie ID ein Geraet der Art an (Eigenschaften "Name=Wert", Hauptgeraet optional) und,
        // wenn groupName gesetzt ist, eine Gerätegruppe ueber den Nummernbereich. Rueckgabe: neu angelegte IDs (vorhandene bleiben unberuehrt).
        public List<string> AddSerial(string cls, List<string> ids, string parent, List<EquipProp> props, string groupName)
        {
            List<string> added = new List<string>();
            foreach (string id in ids)
            {
                if (Device(id) != null) continue;
                EquipDevice d = new EquipDevice(); d.Id = id; d.Class = cls; d.Parent = parent ?? "";
                foreach (EquipProp p in props) d.Props.Add(new EquipProp(p.Name, p.Value));
                Devices.Add(d); added.Add(id);
            }
            if (!string.IsNullOrEmpty(groupName) && ids.Count > 0)
            {
                EquipSeries s = new EquipSeries(); s.Name = groupName; s.Class = cls; s.From = ids[0]; s.To = ids[ids.Count - 1];
                Series.Add(s);
            }
            return added;
        }

        // Kopie eines Geraets mit neuer ID (Art, Eigenschaften, Notiz, Hauptgeraet, Regeln); in dieselbe Gruppe, wenn das Original dort einzeln steht
        public EquipDevice Duplicate(EquipDevice d, string newId)
        {
            EquipDevice c = d.Clone(); c.Id = newId; c.Active = true;
            Devices.Insert(Devices.IndexOf(d) + 1, c);
            foreach (EquipSeries s in Series) if (s.Ids.Contains(d.Id) && !s.Contains(newId)) s.Ids.Add(newId);
            return c;
        }

        // ---------- Statusuebergaenge (Hintergrund, keine Oberflaeche) ----------

        // Darf das Geraet von "from" nach "to" wechseln? Regel des Geraets vor der der Art; ohne Regel erlaubt.
        public bool IsLegal(string id, EquipStatus from, EquipStatus to)
        {
            if (from == to) return true;
            EquipDevice d = Device(id);
            if (d == null) return true;
            StatusRule r = Rule(d.Rules, from, to);
            if (r == null) { EquipClass c = Class(d.Class); if (c != null) r = Rule(c.Rules, from, to); }
            return r == null || r.Legal;
        }

        static StatusRule Rule(List<StatusRule> l, EquipStatus from, EquipStatus to)
        {
            foreach (StatusRule r in l) if (r.From == from && r.To == to) return r;
            return null;
        }
    }

    public partial class Catalog
    {
        // Produkte, fuer die das Geraet an irgendeiner Position erlaubt ist (globaler Standard oder eigene Liste des Profils)
        public List<string> ProductsAllowing(string id)
        {
            List<string> r = new List<string>();
            foreach (ProductProfile p in Products)
            {
                bool ok = false;
                foreach (EquipPosition g in Positions) if (Allowed(p.Code, g.Short).Contains(id)) ok = true;
                foreach (ProfilePos pp in p.Positions) if (Allowed(p.Code, pp.Short).Contains(id)) ok = true;
                if (ok) r.Add(p.Code);
            }
            return r;
        }

        // Hinweise (kein Fehler) zu "Passt zu" je Schritt und Lauf; Gegenstueck zu CheckPackage
        public List<string> CheckPackageWarn(Package p)
        {
            List<string> warn = new List<string>();
            foreach (PackageStep s in p.Steps)
            {
                if (s.IsEmpty) continue;
                for (int run = 0; run < p.RunCountOf(s); run++)
                {
                    foreach (List<string> ids in s.DeviceGroups(run))
                        foreach (string e in Master.CheckCompat(ids)) { string m = s.Label(p.Prefix, run) + ": " + e; if (!warn.Contains(m)) warn.Add(m); }
                }
            }
            return warn;
        }
    }
}
