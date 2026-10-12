using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class UiSmoke
    {
        public static void Run()
        {
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
                using (var window = new LauncherWindow(index, engine, new AppSettings { HideOnDeactivate = false }))
                using (var timer = new Timer { Interval = 800 })
                {
                    int phase = 0;
                    timer.Tick += delegate {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, phase == 0 ? "combined.png" : "launchpad.png");
                        using (var image = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(path); }
                        if (phase++ == 0) window.ShowLauncher(true, true);
                        else { timer.Stop(); Application.Exit(); }
                    };
                    window.Shown += delegate { timer.Start(); };
                    window.ShowLauncher(true, false); Application.Run(window);
                    Console.WriteLine("UI smoke: indexed " + index.Items.Count + " apps; saved combined.png and launchpad.png");
                }
                var provider = new WindowsSearchProvider();
                Console.WriteLine("Windows Search results: " + provider.Search("note", 8).Count + "; status: " + provider.Status);
                Console.WriteLine(provider.Diagnostic);
            }
        }
    }
}
