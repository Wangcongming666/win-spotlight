using System.Drawing;
using System.Drawing.Drawing2D;
namespace FocusSearch
{
    internal static class RoundedShapes
    {
        public static GraphicsPath Path(RectangleF rectangle, float radius)
        {
            var path = new GraphicsPath(); float diameter = System.Math.Min(radius * 2, System.Math.Min(rectangle.Width, rectangle.Height));
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
    }
}
