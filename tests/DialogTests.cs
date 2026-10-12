using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FocusSearch.Tests
{
    internal static class DialogTests
    {
        public static void Register(List<Action> tests)
        {
            tests.Add(SettingsCancelKeepsOriginalValues);
            tests.Add(SettingsSaveProducesIndependentValues);
            tests.Add(AboutShowsCurrentVersion);
            tests.Add(CloseButtonPreservesParentBackground);
        }
        private static T Find<T>(Control parent, string name) where T : Control
        {
            Control[] matches = parent.Controls.Find(name, true);
            TestRunner.Equal(1, matches.Length, "Expected accessible control: " + name);
            return (T)matches[0];
        }
        private static void SettingsCancelKeepsOriginalValues()
        {
            var current = new AppSettings();
            using (var dialog = new SettingsWindow(current))
            {
                dialog.Show();
                Find<CheckBox>(dialog, "CombinedMode").Checked = false;
                ((Button)dialog.CancelButton).PerformClick();
                TestRunner.Equal(true, current.CombinedMode, "Cancel must not mutate live settings.");
                TestRunner.Equal(true, dialog.Value == null, "Cancel must not publish settings.");
                TestRunner.Equal(DialogResult.Cancel, dialog.DialogResult, "Escape/cancel must be explicit.");
            }
        }
        private static void SettingsSaveProducesIndependentValues()
        {
            var current = new AppSettings();
            using (var dialog = new SettingsWindow(current))
            {
                dialog.Show();
                Find<CheckBox>(dialog, "CombinedMode").Checked = false;
                ((Button)dialog.AcceptButton).PerformClick();
                TestRunner.Equal(DialogResult.OK, dialog.DialogResult, "Save must accept settings.");
                TestRunner.Equal(false, dialog.Value.CombinedMode, "Save changed value.");
                TestRunner.Equal(true, current.CombinedMode, "Original settings remain unchanged.");
                dialog.Value.SearchHotkey.VirtualKey = 75;
                TestRunner.Equal(32u, current.SearchHotkey.VirtualKey, "Hotkey objects must be cloned.");
            }
            using (var dialog = new SettingsWindow(current)) { dialog.Show(); Capture(dialog, "settings-window.png"); dialog.Close(); }
        }
        private static void AboutShowsCurrentVersion()
        {
            Type type = typeof(AppSettings).Assembly.GetType("FocusSearch.AboutWindow");
            TestRunner.Equal(true, type != null, "About must have a dedicated window.");
            using (var dialog = (Form)Activator.CreateInstance(type, true))
            {
                dialog.Show();
                TestRunner.Equal(true, Find<Label>(dialog, "Version").Text.Contains(typeof(AppSettings).Assembly.GetName().Version.ToString(3)), "Show running assembly version.");
                Capture(dialog, "about-window.png");
                ((Button)dialog.CancelButton).PerformClick();
                TestRunner.Equal(DialogResult.Cancel, dialog.DialogResult, "About should close with Escape.");
            }
        }
        private static void Capture(Form dialog, string name)
        {
            using (var image = new Bitmap(dialog.Width, dialog.Height))
            {
                dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name));
            }
        }
        private static void CloseButtonPreservesParentBackground()
        {
            using (var parent = new Form { BackColor = Color.FromArgb(80, 90, 100) })
            using (var button = new LauncherCloseButton { Bounds = new Rectangle(10, 10, 28, 28) })
            {
                parent.Controls.Add(button); parent.Show();
                using (var image = new Bitmap(28, 28))
                {
                    button.DrawToBitmap(image, new Rectangle(0, 0, 28, 28));
                    TestRunner.Equal(parent.BackColor.ToArgb(), image.GetPixel(2, 2).ToArgb(), "Close button must not render a black rectangle over glass.");
                }
                parent.Close();
            }
        }
    }
}
