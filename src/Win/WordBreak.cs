using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Umbruch im Editor nur an Leerraum (wie die Ansicht), nicht an Bindestrichen: "H1-F-0103-04" bleibt ein Wort.
    static class WordBreak
    {
        delegate int BreakProc(IntPtr text, int cur, int cch, int code);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        const int EM_SETWORDBREAKPROC = 0x00D0;
        static readonly BreakProc proc = Proc;   // am Leben halten

        public static void Apply(RichTextBox box)
        {
            if (box.IsHandleCreated) Set(box);
            box.HandleCreated += delegate { Set(box); };
        }

        static void Set(RichTextBox box)
        {
            SendMessage(box.Handle, EM_SETWORDBREAKPROC, IntPtr.Zero, Marshal.GetFunctionPointerForDelegate(proc));
        }

        static bool White(IntPtr p, int i) { char c = (char)Marshal.ReadInt16(p, i * 2); return c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\v' || c == '\f' || c == '\0'; }

        static int Proc(IntPtr text, int cur, int cch, int code)
        {
            if (cch < 0) cch = 0;
            if (cur < 0) cur = 0;
            if (cur > cch) cur = cch;
            switch (code)
            {
                case 2: return cur < cch && White(text, cur) ? 1 : 0;                       // WB_ISDELIMITER
                case 0: case 4: case 6:                                                     // links: Wortanfang
                    while (cur > 0 && !White(text, cur - 1)) cur--;
                    return cur;
                default:                                                                    // rechts: Anfang des naechsten Worts
                    while (cur < cch && !White(text, cur)) cur++;
                    while (cur < cch && White(text, cur)) cur++;
                    return cur;
            }
        }
    }
}
