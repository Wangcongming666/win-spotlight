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
                using (var source = new Bitmap(bounds.Width, bounds.Height))
                using (var thumbnail = new Bitmap(Math.Max(1, bounds.Width / 24), Math.Max(1, bounds.Height / 24)))
                {
                    using (var graphics = Graphics.FromImage(source)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    using (var graphics = Graphics.FromImage(thumbnail)) { graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; graphics.DrawImage(source, new Rectangle(Point.Empty, thumbnail.Size)); }
                    var backdrop = new Bitmap(bounds.Width, bounds.Height);
                    using (var graphics = Graphics.FromImage(backdrop))
                    using (var tint = new SolidBrush(Color.FromArgb(172, 27, 31, 39)))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(thumbnail, new Rectangle(Point.Empty, bounds.Size));
                        graphics.FillRectangle(tint, new Rectangle(Point.Empty, bounds.Size));
                    }
                    return backdrop;
                }
            }
            catch (Exception) { return null; }
        }
    }
}
