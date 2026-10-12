using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class ApplicationLaunchTests
    {
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMouse(IntPtr window, int message, IntPtr keys, IntPtr point);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        public static void Register(ICollection<Action> tests) { tests.Add(NativeDoubleClickReachesLaunchCallback); tests.Add(ShortcutStartsApplicationWithArgumentsAndWorkingDirectory); }
        private static void NativeDoubleClickReachesLaunchCallback()
        {
            foreach (bool grid in new[] { true, false })
                using (var window = new Form { ClientSize = new Size(400, 360), TopMost = true })
                using (var canvas = new LauncherCanvas { Size = new Size(320, 320), GridMode = grid })
                {
                    window.Controls.Add(canvas); window.Show();
                    var expected = new SearchItem { Name = "Test application", Path = "test.exe" };
                    canvas.SetItems(new List<SearchItem> { expected });
                    int launches = 0; SearchItem actual = null; canvas.ActivateItem = delegate(SearchItem item) { launches++; actual = item; };
                    TestRunner.Equal(canvas.Handle, WindowFromPoint(canvas.PointToScreen(new Point(40, 30))), "Native test clicks must hit the actual canvas window.");
                    IntPtr point = new IntPtr((30 << 16) | 40);
                    SendMouse(canvas.Handle, 0x201, new IntPtr(1), point); SendMouse(canvas.Handle, 0x202, IntPtr.Zero, point);
                    TestRunner.Equal(0, launches, "Native single-click must not launch.");
                    SendMouse(canvas.Handle, 0x203, new IntPtr(1), point); SendMouse(canvas.Handle, 0x202, IntPtr.Zero, point);
                    TestRunner.Equal(1, launches, "Native double-click must reach the launch callback once.");
                    TestRunner.Equal(expected, actual, "Native double-click must launch the hit-tested item.");
                    window.Close();
                }
        }
        private static void ShortcutStartsApplicationWithArgumentsAndWorkingDirectory()
        {
            Type launcher = typeof(SearchItem).Assembly.GetType("FocusSearch.ApplicationLauncher");
            TestRunner.Equal(true, launcher != null, "Application launch must handle unavailable shell execution.");
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launch-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); object shell = null, shortcut = null;
            try
            {
                string link = Path.Combine(root, "probe.lnk");
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                foreach (var pair in new[] { new[] { "TargetPath", Path.Combine(Environment.SystemDirectory, "cmd.exe") }, new[] { "Arguments", "/c exit 37" }, new[] { "WorkingDirectory", root } })
                    shortcut.GetType().InvokeMember(pair[0], BindingFlags.PutDispProperty, null, shortcut, new object[] { pair[1] });
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                Marshal.FinalReleaseComObject(shortcut); shortcut = null;
                Marshal.FinalReleaseComObject(shell); shell = null;
                using (var resolver = new ShortcutResolver())
                {
                    string target, arguments, working;
                    TestRunner.Equal(true, resolver.TryResolve(link, out target, out arguments, out working), "Saved shortcut must resolve its executable target.");
                    TestRunner.Equal(true, File.Exists(target), "Resolved shortcut target must exist.");
                    TestRunner.Equal(root, working, "Resolved shortcut must retain its working directory.");
                }
                using (var child = (Process)launcher.GetMethod("Start", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { new SearchItem { Name = "Probe", Path = link, Kind = ResultKind.Application } }))
                {
                    TestRunner.Equal(true, child != null && child.WaitForExit(5000), "Launching a real shortcut must start and finish the probe.");
                    TestRunner.Equal(37, child.ExitCode, "Child application must execute the shortcut's arguments.");
                }
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
                Directory.Delete(root, true);
            }
        }
    }
}
