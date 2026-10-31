using System;
using System.Collections.Generic;

namespace Arbeitsplan.Core
{
    // Puffer-Rhythmus als Muster: Eintraege durch Komma/Leerzeichen, je Tag mob | stat | mob/stat | frei (auch m, s, f, m/s, stat/mob; Gross/Klein egal).
    // Das Muster wiederholt sich ab dem Starttag; es wird nicht gespeichert, nur die daraus entstehenden festen Tage.
    public static class PufferPattern
    {
        public const int Mob = 1, Stat = 2;   // Bits; 0 = frei, 3 = beide am selben Tag

        // null + error = Muster ungueltig (leer oder unbekannter Eintrag)
        public static List<int> Parse(string text, out string error)
        {
            error = "";
            List<int> res = new List<int>();
            foreach (string raw in (text ?? "").Split(new[] { ',', ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.ToLowerInvariant();
                int v;
                if (t == "mob" || t == "m") v = Mob;
                else if (t == "stat" || t == "s") v = Stat;
                else if (t == "mob/stat" || t == "stat/mob" || t == "m/s" || t == "s/m") v = Mob | Stat;
                else if (t == "frei" || t == "f") v = 0;
                else { error = "Unbekannter Eintrag „" + raw + "“ – erlaubt: mob, stat, mob/stat, frei."; return null; }
                res.Add(v);
            }
            if (res.Count == 0) error = "Das Muster ist leer – z. B. „mob, mob/stat, frei“.";
            return res.Count == 0 ? null : res;
        }

        // Tage (1-basiert) je Typ: Muster ab abTag bis einschliesslich bisTag, Muster wiederholt sich
        public static void Fill(List<int> muster, int abTag, int bisTag, List<int> mob, List<int> stat)
        {
            mob.Clear(); stat.Clear();
            if (muster == null || muster.Count == 0) return;
            for (int d = Math.Max(1, abTag); d <= bisTag; d++)
            {
                int v = muster[(d - abTag) % muster.Count];
                if ((v & Mob) != 0) mob.Add(d);
                if ((v & Stat) != 0) stat.Add(d);
            }
        }
    }
}
