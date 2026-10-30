using System;

namespace Arbeitsplan.Core
{
    // N2: Puffer "hergestellt". Mit DutyConfig.BufMade folgen Tages- und Wochenprobe aus den hergestellten Puffern
    // (Tagesprobe: EIN hergestellter mobiler Puffer des Tages genuegt; Wochenprobe: ein hergestellter stationaerer der KW).
    // Tagesprobe, Wochenprobe und WFI-Rundgang bleiben getrennte Dinge (eigene Namen, Tooltips); die Rundgaenge haengen nie an Puffern.
    public static class MadeRules
    {
        public const string DayKey = "PROBE", WeekKey = "WFIW", RoundKey = "WFI";
        const string DayOld = "Tagesprobe (täglich)", WeekOld = "Manuelle Probe (wöchentlich)", RoundOld = "WFI-Probenahme";
        public const string DayName = "Tagesprobe (mobiler Puffer)", WeekName = "Wochenprobe (stationärer Puffer)", RoundName = "WFI-Rundgang (Probenahme)";

        // Hat der Block einen hergestellten Puffer, dessen Typ in der Bedingung d mit Praefix p steht ("+" automatisch, "!" entfaellt)?
        public static bool AnyMade(Entry e, CheckDef d, char p)
        {
            foreach (BufferItem b in e.Buffers) if (b.Made && b.Typ.Length > 0 && CheckEval.Cond(d, p, b.Typ)) return true;
            return false;
        }

        // Anzeigefassung eines Pflichtpunkts (nur Namen der Standard-Pflichtpunkte, eigene Namen bleiben); Kopie, gespeichert wird nie
        public static CheckDef Display(CheckDef d, bool on)
        {
            if (!on) return d;
            CheckDef c = d.Clone();
            if (d.Key == DayKey && (d.Name == DayOld || d.Name == DayName)) { c.Name = DayName; c.Short = "Tagesprobe mobil"; }
            else if (d.Key == WeekKey && (d.Name == WeekOld || d.Name == WeekName)) { c.Name = WeekName; c.Short = "Wochenprobe stationär"; }
            return c;
        }

        public static RoundDef Display(RoundDef r, bool on)
        {
            if (!on || r.Key != RoundKey || r.Name != RoundOld) return r;
            RoundDef c = r.Clone(); c.Name = RoundName; return c;
        }

        // Erklaerung fuer den Tooltip (nur mit BufMade; "" = keine besondere)
        public static string Tip(CheckDef d, DayState st, bool on)
        {
            if (d.Key == DayKey)
                return on ? "Manuell abgehakt." : st == DayState.Auto ? "Gezogen: ein mobiler Puffer dieses Tages ist als hergestellt abgehakt."
                    : "Noch nicht gezogen: wird erledigt, sobald EIN mobiler Puffer dieses Tages als hergestellt abgehakt ist (oder hier von Hand abhaken). Unabhängig von Wochenprobe und WFI-Rundgang.";
            if (d.Key == WeekKey)
                return on ? "Manuell abgehakt." : st == DayState.Auto ? "Gezogen: ein stationärer Puffer dieser KW ist als hergestellt abgehakt."
                    : "Noch nicht gezogen: wird erledigt, sobald ein stationärer Puffer dieser KW als hergestellt abgehakt ist (oder hier von Hand abhaken). Unabhängig von Tagesprobe und WFI-Rundgang.";
            return "";
        }

        public const string RoundTipText = "Der WFI-Rundgang ist unabhängig von Tages- und Wochenprobe und hängt nicht an den Puffern.";
    }
}
