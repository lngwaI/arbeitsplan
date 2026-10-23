using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Testhilfe: --shot <datei.png> [breite hoehe] rendert die Ansicht als Bild und beendet.
            if (args.Length >= 2 && args[0] == "--shot")
            {
                MainForm f = new MainForm();
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(0, 0);
                f.ClientSize = args.Length >= 4 ? new Size(int.Parse(args[2]), int.Parse(args[3])) : new Size(1280, 720);
                f.Show();
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
                return;
            }
            Application.Run(new MainForm());
        }
    }
}
