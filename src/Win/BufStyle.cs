using System;
using System.Drawing;

namespace Arbeitsplan.Win
{
    // UX-5b: gemeinsames Aussehen der Puffer in Wochenansicht und Vorschau: je Puffer ein eigener Hintergrundton (wechselt), Innenabstand, deutlicher
    // Abstand mit Trennlinie zwischen den Puffern. Der Typ bleibt als Seitenstrich (Theme.BufColors) sichtbar.
    static class BufStyle
    {
        public const int Pad = 3, Gap = 10;
        static readonly Color[] tints = { Color.FromArgb(232, 240, 250), Color.FromArgb(251, 243, 226) };

        public static Color Tint(int i) { return tints[i % tints.Length]; }
        public static readonly Color Line = Color.FromArgb(188, 196, 208);
    }
}
