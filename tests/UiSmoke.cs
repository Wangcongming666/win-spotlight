using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class UiSmoke
    {
        public static void Run()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var first = new HotkeyWindow())
            using (var second = new HotkeyWindow())
            {
                bool registered = NativeMethods.RegisterHotKey(first.Handle, 901, 0x0007, 0x87);
                if (!registered) throw new InvalidOperationException("Native test hotkey registration failed.");
                try
                {
                    TestRunner.Equal(false, NativeMethods.RegisterHotKey(second.Handle, 902, 0x0007, 0x87), "Native hotkey conflict must be detected.");
                }
                finally { NativeMethods.UnregisterHotKey(first.Handle, 901); }
                bool reclaimed = NativeMethods.RegisterHotKey(second.Handle, 902, 0x0007, 0x87);
                try { TestRunner.Equal(true, reclaimed, "Released native hotkey must be registerable again."); }
                finally { NativeMethods.UnregisterHotKey(second.Handle, 902); }
                Console.WriteLine("Native global hotkey registration/conflict/release: PASS");
            }
            using (var index = new AppIndex(AppIndex.DefaultRoots()))
            {
                index.Refresh();
                var engine = new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>());
                using (var window = new LauncherWindow(index, engine, new AppSettings { HideOnDeactivate = false, FileSearchEnabled = false }))
                using (var timer = new Timer { Interval = 800 })
                {
                    int phase = 0;
                    timer.Tick += delegate {
                        if (phase < 5)
                        {
                            if (phase == 1)
                            {
                                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                                TestRunner.Equal(true, tabs[3].Visible, "Categories must remain open during keyboard navigation.");
                                Console.WriteLine("Expanded circular categories and keyboard focus: PASS");
                            }
                            string name = new[] { "search-only", "categories-expanded", "search-results", "launchpad", "search-only-final" }[phase];
                            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".png");
                            using (var image = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(path); }
                            using (var image = new Bitmap(window.Width, window.Height))
                            {
                                using (var graphics = Graphics.FromImage(image)) graphics.CopyFromScreen(window.Location, Point.Empty, window.Size);
                                image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + "-live.png"));
                            }
                        }
                        if (phase == 0)
                        {
                            var reveal = (Panel)typeof(LauncherWindow).GetField("_tabReveal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                            typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reveal, new object[] { EventArgs.Empty });
                            var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                            tabs[0].Focus();
                        }
                        else if (phase == 1)
                        {
                            var input = (TextBox)typeof(LauncherWindow).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                            input.Focus(); input.Text = "dy";
                        }
                        else if (phase == 2) window.ShowLauncher(true, true);
                        else if (phase == 3) { window.HideLauncher(); window.ShowLauncher(true, false); }
                        else if (phase == 4)
                        {
                            if (!window.Visible) throw new InvalidOperationException("Reopened launcher must stay visible.");
                            TestRunner.Equal(true, window.Visible, "Reopening during close must remain visible.");
                            TestRunner.Equal(false, window.IsHiding, "Reopening must cancel pending close.");
                            TestRunner.Equal(true, window.Opacity >= 0.95, "Entrance must reach final opacity.");
                            window.HideLauncher();
                        }
                        else
                        {
                            TestRunner.Equal(false, window.Visible, "Exit must finish and hide the window.");
                            Console.WriteLine("Window animation open/interruption/close: PASS");
                            timer.Stop(); Application.Exit();
                        }
                        phase++;
                    };
                    window.Shown += delegate { timer.Start(); };
                    window.ShowLauncher(true, false); Application.Run(window);
                    Console.WriteLine("UI smoke: indexed " + index.Items.Count + " apps; saved collapsed bar, expanded categories, dy results and launchpad screenshots");
                }
                var provider = new WindowsSearchProvider();
                Console.WriteLine("Windows Search results: " + provider.Search("note", 8).Count + "; status: " + provider.Status);
                Console.WriteLine(provider.Diagnostic);
            }
        }
    }
}
