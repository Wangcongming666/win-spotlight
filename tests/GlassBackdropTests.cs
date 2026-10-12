using System;
using System.Collections.Generic;
using System.Drawing;

namespace FocusSearch.Tests
{
    internal static class GlassBackdropTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(GlassSoftensDetailAndPreservesWallpaperColor); tests.Add(DarkGlassRetainsHueAndSuppressesBrightDetail); }
        private static void DarkGlassRetainsHueAndSuppressesBrightDetail()
        {
            using (var source = new Bitmap(256, 128))
            {
                using (var graphics = Graphics.FromImage(source))
                { graphics.Clear(Color.FromArgb(30, 110, 210)); graphics.FillRectangle(Brushes.White, 126, 0, 4, 128); }
                using (var result = GlassBackdrop.Render(source, true))
                {
                    Color background = result.GetPixel(32, 64), detail = result.GetPixel(128, 64);
                    TestRunner.Equal(source.Size, result.Size, "Dark glass must retain capture dimensions.");
                    TestRunner.Greater(background.B, background.R, "Dark glass must preserve wallpaper hue.");
                    TestRunner.Equal(true, background.R < 85 && background.B < 110, "Dark material must suppress bright wallpaper for readable text.");
                    TestRunner.Equal(true, Math.Abs(detail.R - background.R) < 35, "Dark glass must blur desktop detail.");
                }
            }
        }
        private static void GlassSoftensDetailAndPreservesWallpaperColor()
        {
            using (var source = new Bitmap(256, 128))
            {
                using (var graphics = Graphics.FromImage(source))
                {
                    graphics.Clear(Color.FromArgb(30, 110, 210));
                    graphics.FillRectangle(Brushes.White, 126, 0, 4, 128);
                }
                using (var result = GlassBackdrop.Render(source))
                {
                    Color background = result.GetPixel(32, 64), detail = result.GetPixel(128, 64);
                    TestRunner.Equal(source.Size, result.Size, "Glass must retain the captured area dimensions.");
                    TestRunner.Greater(background.B, background.R, "Wallpaper hue must survive the material tint.");
                    TestRunner.Greater(background.R, 180, "Light glass must remain bright over a dark wallpaper.");
                    TestRunner.Equal(true, Math.Abs(detail.R - background.R) < 35, "Fine bright desktop details must soften instead of remaining legible through the glass.");
                }
            }
        }
    }
}
