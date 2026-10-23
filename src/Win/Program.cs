using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    static class Program
    {
        static string Arg(string[] a, string name)
        {
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        // Aufruf: Arbeitsplan.exe [--user <login>] [--nosave]
        //   Test:  --shot <png> [--size BxH] [--edit spalte,zeile]  (rendert die Ansicht als Bild und beendet)
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string login = Arg(args, "--user") ?? Environment.UserName;
            bool nosave = Array.IndexOf(args, "--nosave") >= 0;
            string shot = Arg(args, "--shot");
            if (shot == null) { Application.Run(new MainForm(login, nosave)); return; }

            MainForm f = new MainForm(login, nosave);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(0, 0);
            string size = Arg(args, "--size");
            f.ClientSize = size != null ? new Size(int.Parse(size.Split('x')[0]), int.Parse(size.Split('x')[1])) : new Size(1280, 720);
            f.TopMost = true;
            f.Show();
            string edit = Arg(args, "--edit");
            if (edit != null) f.TestEdit(int.Parse(edit.Split(',')[0]), int.Parse(edit.Split(',')[1]));
            Form target = Array.IndexOf(args, "--users") >= 0 ? f.TestUsers() : f;
            for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
            using (Bitmap bmp = new Bitmap(target.Width, target.Height))   // Bildschirmabzug (DrawToBitmap zeichnet RichTextBox nicht)
            {
                using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(target.Location, Point.Empty, target.Size);
                bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
