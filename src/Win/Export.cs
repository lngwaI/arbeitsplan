using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Arbeitsplan.Win
{
    // Export der ganzen Woche (alle Zeilen, nicht nur der sichtbare Ausschnitt) als Bild oder PDF.
    static class Export
    {
        static readonly CultureInfo De = new CultureInfo("de-DE");

        public static Bitmap RenderWeek(PlanView view, string title, DateTime start)
        {
            const int top = 36, dates = 28;
            Bitmap bmp = new Bitmap(view.Width, top + dates + view.Height);
            try
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Bitmap body = new Bitmap(view.Width, view.Height))
                using (Font tf = new Font(view.Font.FontFamily, 12f, FontStyle.Bold))
                using (Font df = new Font(view.Font, FontStyle.Bold))
                {
                    g.Clear(Theme.Current.Window);
                    TextRenderer.DrawText(g, title, tf, new Rectangle(0, 0, bmp.Width, top), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    for (int c = 0; c < 7; c++)
                    {
                        DateTime d = start.AddDays(c);
                        Rectangle r = new Rectangle(view.ColLeft(c), top, view.ColW, dates - 4);
                        g.FillRectangle(Theme.Current.Brush(d == DateTime.Today ? Theme.Current.DayHeadToday : Theme.Current.DayHead), r);
                        TextRenderer.DrawText(g, d.ToString("ddd, dd.MM.yyyy", De), df, r, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                    view.DrawToBitmap(body, new Rectangle(0, 0, view.Width, view.Height));
                    g.DrawImageUnscaled(body, 0, top + dates);
                }
            }
            catch (Exception) { bmp.Dispose(); throw; }   // z. B. zu wenig Speicher: kein halbes Bild liegen lassen
            return bmp;
        }

        public static void SavePng(Bitmap bmp, string path) { bmp.Save(path, ImageFormat.Png); }

        // Minimales PDF: eine Seite mit dem Bild (JPEG), Breite A4 quer (842 pt), Hoehe passend zum Bild.
        public static void SavePdf(Bitmap bmp, string path)
        {
            byte[] jpg;
            using (MemoryStream ms = new MemoryStream())
            {
                ImageCodecInfo codec = null;
                foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") codec = c;
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                    bmp.Save(ms, codec, ep);
                }
                jpg = ms.ToArray();
            }
            double pw = 842.0, ph = Math.Round(pw * bmp.Height / bmp.Width, 2);
            string W = pw.ToString("0.##", CultureInfo.InvariantCulture), H = ph.ToString("0.##", CultureInfo.InvariantCulture);
            string content = "q " + W + " 0 0 " + H + " 0 0 cm /Im0 Do Q";

            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                long[] pos = new long[6];
                Action<string> txt = delegate(string s) { byte[] b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); };
                txt("%PDF-1.4\n");
                pos[1] = fs.Position; txt("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
                pos[2] = fs.Position; txt("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
                pos[3] = fs.Position; txt("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + W + " " + H + "] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
                pos[4] = fs.Position;
                txt("4 0 obj\n<< /Type /XObject /Subtype /Image /Width " + bmp.Width + " /Height " + bmp.Height +
                    " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpg.Length + " >>\nstream\n");
                fs.Write(jpg, 0, jpg.Length);
                txt("\nendstream\nendobj\n");
                pos[5] = fs.Position; txt("5 0 obj\n<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream\nendobj\n");
                long xref = fs.Position;
                txt("xref\n0 6\n0000000000 65535 f \n");
                for (int i = 1; i <= 5; i++) txt(pos[i].ToString("0000000000") + " 00000 n \n");
                txt("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            }
        }
    }
}
