using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace FocusSearch.Tests
{
    internal static class LauncherKeyboardTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(EnterCommandKeyOpensSelectedResult); }

        private static void EnterCommandKeyOpensSelectedResult()
        {
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings()))
            {
                var input = (TextBox)typeof(LauncherWindow).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                var expected = new SearchItem { Name = "Cursor", Path = @"C:\Users\76004\Desktop", Kind = ResultKind.Folder };
                SearchItem opened = null;
                window.LaunchRequested = delegate(SearchItem item) { opened = item; };
                canvas.SetItems(new List<SearchItem> { expected });

                var handle = input.Handle;
                var message = Message.Create(handle, 0x0100, new IntPtr((int)Keys.Enter), IntPtr.Zero);
                bool handled = input.PreProcessMessage(ref message);

                TestRunner.Equal(true, handled, "Enter command key must be consumed before the native edit control can beep.");
                TestRunner.Equal(expected, opened, "Enter command key must activate the selected result.");
            }
        }
    }
}
