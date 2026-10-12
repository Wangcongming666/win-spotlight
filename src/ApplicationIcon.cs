using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace FocusSearch
{
    internal static class ApplicationIcon
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        public static Icon Create()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FocusSearch.app.ico"))
            {
                if (stream != null) using (var icon = new Icon(stream, new Size(32, 32))) return (Icon)icon.Clone();
            }
            using (var image = Render(64))
            {
                IntPtr handle = image.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        public static Bitmap Render(int size)
        {
            var image = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.ScaleTransform(size / 128f, size / 128f);
                using (var shape = RoundedShapes.Path(new RectangleF(4, 4, 120, 120), 30))
                using (var gradient = new LinearGradientBrush(new RectangleF(0, 0, 128, 128), Color.FromArgb(63, 146, 244), Color.FromArgb(37, 70, 160), 60)) graphics.FillPath(gradient, shape);
                using (var lens = new Pen(Color.White, 9) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { graphics.DrawEllipse(lens, 29, 26, 51, 51); graphics.DrawLine(lens, 74, 72, 99, 98); }
                using (var highlight = new Pen(Color.FromArgb(100, Color.White), 2)) graphics.DrawArc(highlight, 36, 33, 37, 37, 195, 65);
            }
            return image;
        }
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 }; var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) using (var image = Render(sizes[i])) using (var stream = new MemoryStream()) { image.Save(stream, System.Drawing.Imaging.ImageFormat.Png); images[i] = stream.ToArray(); }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
                }
                foreach (byte[] image in images) writer.Write(image);
            }
        }
    }
}
