using IELDiscordBot.Classes.Models;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Utilities
{
    class CaptchaGenerator
    {
        const string LETTERS = "2346789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const int CAPTCHA_LENGTH = 5;

        public static MemoryStream Generate(ref Captcha captcha)
        {
            captcha.CaptchaCode = GenerateCode();

            return GenerateImage(captcha.CaptchaCode);
        }

        private static string GenerateCode()
        {
            Random random = new Random();
            int maxIndex = LETTERS.Length - 1;

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < CAPTCHA_LENGTH; i++)
                builder.Append(LETTERS[random.Next(maxIndex)]);

            return builder.ToString();
        }

        private static Color GetBackgroundColor(Random r)
        {
            int low = 100, high = 255;

            int red = r.Next(high) % (high - low) + low;
            int green = r.Next(high) % (high - low) + low;
            int blue = r.Next(high) % (high - low) + low;

            return Color.FromArgb(red, green, blue);
        }

        private static Color GetForegroundColour(Random r)
        {
            int redLow = 150, greenLow = 100, blueLow = 150;

            return Color.FromArgb(r.Next(redLow), r.Next(greenLow), r.Next(blueLow));
        }

        private static MemoryStream GenerateImage(string captchaCode)
        {
            int width = 400, height = 200;

            using (Bitmap bmp = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    Random r = new Random();

                    g.Clear(GetBackgroundColor(r));

                    MemoryStream ms = new MemoryStream();

                    SolidBrush brush = new SolidBrush(Color.Black);
                    int fontSize = GetFontSize(width, captchaCode.Length);
                    Font font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

                    for (int i = 0; i < captchaCode.Length; i++)
                    {
                        brush.Color = GetForegroundColour(r);
                        int shiftPx = fontSize / 6;

                        float x = i * fontSize + r.Next(-shiftPx, shiftPx) + r.Next(-shiftPx, shiftPx);
                        int maxY = height - (fontSize);
                        if (maxY < 0) maxY = 0;
                        float y = r.Next(0, maxY);

                        g.DrawString(captchaCode[i].ToString(), font, brush, x, y);
                    }

                    Pen linePen = new Pen(new SolidBrush(Color.Black), 3);
                    for (int i = 0; i < r.Next(10, 20); i++)
                    {
                        linePen.Color = GetForegroundColour(r);

                        Point start = new Point(r.Next(0, width), r.Next(0, height));
                        Point end = new Point(r.Next(0, width), r.Next(0, height));
                        g.DrawLine(linePen, start, end);
                    }

                    //LIVE
                    bmp.Save(ms, ImageFormat.Jpeg);

                    //DEV
                    //bmp.Save(@"C:\Users\Kian\Desktop\CaptchaTest.jpeg", ImageFormat.Jpeg);
                    return ms;

                }
            }
        }

        private static int GetFontSize(int width, int length)
        {
            double size = (width / length);
            return Convert.ToInt32(Math.Round(size));
        }
    }
}
