using System;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Forms;

namespace FocusSearch
{
    internal static class GlassPalette
    {
        public static bool IsDark { get; private set; }
        public static Color Surface { get { return IsDark ? Color.FromArgb(38, 40, 45) : Color.FromArgb(242, 243, 246); } }
        public static Color Text { get { return SystemInformation.HighContrast ? SystemColors.WindowText : IsDark ? Color.FromArgb(244, 245, 248) : Color.FromArgb(36, 38, 43); } }
        public static Color Secondary { get { return SystemInformation.HighContrast ? SystemColors.WindowText : IsDark ? Color.FromArgb(181, 184, 194) : Color.FromArgb(100, 104, 114); } }
        public static Color Selection { get { return SystemInformation.HighContrast ? SystemColors.Highlight : IsDark ? Color.FromArgb(67, 130, 221) : Color.FromArgb(38, 112, 222); } }
        public static void Refresh()
        {
            IsDark = false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    IsDark = key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.IO.IOException) { }
        }
        public static Font CreateFont(float size)
        {
            foreach (string name in new[] { "SF Pro Display", "SF Pro Text", "Segoe UI", "Microsoft YaHei UI" })
            {
                var font = new Font(name, size, FontStyle.Regular, GraphicsUnit.Point);
                if (font.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return font;
                font.Dispose();
            }
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size);
        }
    }
}
