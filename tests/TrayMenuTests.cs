using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FocusSearch.Tests
{
    internal static class TrayMenuTests
    {
        public static void Register(List<Action> tests) { tests.Add(MenuPreservesActionsAndCurrentSettings); }
        private static void MenuPreservesActionsAndCurrentSettings()
        {
            Type type = typeof(AppSettings).Assembly.GetType("FocusSearch.TrayMenu");
            TestRunner.Equal(true, type != null, "Styled native tray menu should exist.");
            var settings = new AppSettings { AppGridEnabled = false, SearchHotkey = new HotkeySpec(2, 75) };
            int action = -1;
            using (var menu = (ContextMenuStrip)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { (Func<AppSettings>)delegate { return settings; }, (Action<int>)delegate(int value) { action = value; } }, null))
            {
                menu.Show(new Point(40, 40));
                TestRunner.Equal(false, menu.Items[1].Enabled, "Disabled application grid must remain disabled.");
                TestRunner.Equal("Ctrl + K", ((ToolStripMenuItem)menu.Items[0]).ShortcutKeyDisplayString, "Show configured shortcut, not hard-coded defaults.");
                menu.Items[2].PerformClick();
                TestRunner.Equal(2, action, "Settings action should still dispatch.");
                menu.Close();
                settings.AppGridEnabled = true;
                menu.Show(new Point(40, 40));
                TestRunner.Equal(true, menu.Items[1].Enabled, "Read current settings on each opening.");
                TestRunner.Equal(true, menu.Region != null, "Rounded menu region should be applied.");
                using (var image = new Bitmap(menu.Width, menu.Height))
                {
                    menu.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    TestRunner.Equal(true, image.GetPixel(menu.Width / 2, 3).R > 230, "Background should be light.");
                    image.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray-menu.png"));
                }
                menu.Close();
            }
        }
    }
}
