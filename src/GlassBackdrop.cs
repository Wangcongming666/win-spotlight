using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace FocusSearch
{
    internal static class GlassBackdrop
    {
        // Capture only on invocation: no repaint polling or persistent screen capture.
        public static Bitmap Capture(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;
            try
            {
                // Wait for a hidden previous panel to leave the composed desktop.
                try { NativeMethods.DwmFlush(); }
                catch (EntryPointNotFoundException) { }
                catch (DllNotFoundException) { }
                using (var source = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                {
                    using (var graphics = Graphics.FromImage(source)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    if (SystemInformation.HighContrast) { var solid = new Bitmap(source.Width, source.Height); using (var g = Graphics.FromImage(solid)) g.Clear(SystemColors.Window); return solid; }
                    return Render(source, GlassPalette.IsDark);
                }
            }
            catch (Exception) { return null; }
        }
        // Work on a small image, then blur repeatedly so desktop text disappears
        // while broad wallpaper colors still pass through the glass material.
        public static Bitmap Render(Bitmap source)
        {
            return Render(source, false);
        }
        public static Bitmap Render(Bitmap source, bool dark)
        {
            int width = Math.Max(1, source.Width / 4), height = Math.Max(1, source.Height / 4);
            using (var reduced = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(reduced))
                { graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; graphics.DrawImage(source, new Rectangle(0, 0, width, height)); }
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = reduced.GetPixel(x, y);
                for (int pass = 0; pass < 3; pass++)
                { pixels = Blur(pixels, width, height, true); pixels = Blur(pixels, width, height, false); }
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) reduced.SetPixel(x, y, pixels[y * width + x]);
                var backdrop = new Bitmap(source.Width, source.Height);
                using (var graphics = Graphics.FromImage(backdrop))
                using (var tint = new SolidBrush(dark ? Color.FromArgb(205, 28, 30, 35) : Color.FromArgb(174, 245, 246, 249)))
                using (var sheen = new LinearGradientBrush(new Rectangle(0, 0, source.Width, source.Height), dark ? Color.FromArgb(16, 255, 255, 255) : Color.FromArgb(36, 255, 255, 255), Color.FromArgb(3, 225, 232, 244), LinearGradientMode.Vertical))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(reduced, new Rectangle(0, 0, source.Width, source.Height));
                    graphics.FillRectangle(tint, 0, 0, source.Width, source.Height);
                    graphics.FillRectangle(sheen, 0, 0, source.Width, source.Height);
                }
                return backdrop;
            }
        }
        private static Color[] Blur(Color[] pixels, int width, int height, bool horizontal)
        {
            const int radius = 8;
            const int count = radius * 2 + 1;
            var output = new Color[pixels.Length];
            int length = horizontal ? width : height, lines = horizontal ? height : width;
            for (int line = 0; line < lines; line++)
            {
                int red = 0, green = 0, blue = 0;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    int position = Math.Max(0, Math.Min(length - 1, offset));
                    Color color = pixels[horizontal ? line * width + position : position * width + line];
                    red += color.R; green += color.G; blue += color.B;
                }
                for (int position = 0; position < length; position++)
                {
                    output[horizontal ? line * width + position : position * width + line] = Color.FromArgb(red / count, green / count, blue / count);
                    int remove = Math.Max(0, position - radius), add = Math.Min(length - 1, position + radius + 1);
                    Color before = pixels[horizontal ? line * width + remove : remove * width + line];
                    Color after = pixels[horizontal ? line * width + add : add * width + line];
                    red += after.R - before.R; green += after.G - before.G; blue += after.B - before.B;
                }
            }
            return output;
        }
    }
}
