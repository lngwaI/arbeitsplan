using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Paket-Editor, Leistung (UX-5e): Zeichnen aussetzen waehrend ein Bereich neu aufgebaut wird (WM_SETREDRAW), Zeitmessung fuer Selbsttests
    // (nur TESTHOOKS; im Release sind die Aufrufe PerfB/PerfE entfernt).
    partial class PackageForm
    {
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMsg(IntPtr h, int msg, IntPtr w, IntPtr l);
        const int WM_SETREDRAW = 0x000B;

        // Zeichnen der Steuerelemente unterhalb von c aussetzen; Resume zeichnet einmal alles neu (kein Zwischenstand sichtbar)
        static void SuspendDraw(Control c) { if (c.IsHandleCreated) SendMsg(c.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero); }

        static void ResumeDraw(Control c)
        {
            if (!c.IsHandleCreated) return;
            SendMsg(c.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            c.Invalidate(true);
        }

        // Bildlaufbereich der Details: Zeichnen ruht, solange mindestens ein Aufrufer es haelt (Bind -> Layout -> Resize kann verschachtelt sein)
        int holdDepth;

        void HoldDraw() { if (holdDepth++ == 0) SuspendDraw(scroll); }

        void ReleaseDraw() { if (holdDepth > 0 && --holdDepth == 0) ResumeDraw(scroll); }

        internal const int PfBind = 0, PfView = 1, PfPreview = 2, PfHints = 3, PfStrip = 4, PfUndo = 5, PfLayout = 6, PfN = 7;

#if TESTHOOKS
        internal readonly long[] PfTicks = new long[PfN];
        internal readonly int[] PfCount = new int[PfN];
        readonly long[] pfStart = new long[PfN];

        [Conditional("TESTHOOKS")] void PerfB(int k) { pfStart[k] = Stopwatch.GetTimestamp(); }
        [Conditional("TESTHOOKS")] void PerfE(int k) { PfTicks[k] += Stopwatch.GetTimestamp() - pfStart[k]; PfCount[k]++; }

        internal double PfMs(int k) { return PfTicks[k] * 1000.0 / Stopwatch.Frequency; }

        internal void PfReset() { Array.Clear(PfTicks, 0, PfN); Array.Clear(PfCount, 0, PfN); }
#else
        [Conditional("TESTHOOKS")] void PerfB(int k) { }
        [Conditional("TESTHOOKS")] void PerfE(int k) { }
#endif
    }
}
