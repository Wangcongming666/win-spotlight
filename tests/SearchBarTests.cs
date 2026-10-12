using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class SearchBarTests
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        public static void Register(ICollection<Action> tests) { tests.Add(InitialSearchBarAndHoverCategories); tests.Add(InstalledDouyinMatchesInitials); tests.Add(ApplicationPreviewCollapsesOnLeaveAndSearchStaysOpen); tests.Add(IndependentApplicationsViewHasNoSearchAndDoubleClickOpens); tests.Add(CategoryRevealAnimatesAndReversesWithoutJumping); tests.Add(ApplicationPanelExpandsAndReversesSmoothly); }
        private static void ApplicationPanelExpandsAndReversesSmoothly()
        {
            if (!Motion.SystemAllowsAnimation()) return;
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { AnimationsEnabled = false, HideOnDeactivate = false }))
            {
                window.ShowLauncher(true, false);
                window.Settings.AnimationsEnabled = true;
                var select = typeof(LauncherWindow).GetMethod("SelectScope", BindingFlags.NonPublic | BindingFlags.Instance);
                var hover = typeof(LauncherWindow).GetMethod("UpdateHoverState", BindingFlags.NonPublic | BindingFlags.Instance);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var input = (TextBox)typeof(LauncherWindow).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                int target = Math.Min(600, Screen.FromControl(window).WorkingArea.Height - 40);
                select.Invoke(window, new object[] { SearchScope.Applications }); tabs[0].Focus();
                TestRunner.Equal(true, window.Height < target, "App preview must not jump directly to its final size.");
                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                while (window.Height == 76 && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                int partial = window.Height;
                TestRunner.Equal(true, partial > 76 && partial < target, "App panel must display an intermediate height.");
                Capture(window, "application-panel-mid.png");
                input.Focus(); hover.Invoke(window, new object[] { new Point(20, 20) });
                TestRunner.Equal(partial, window.Height, "Reversing to close must continue from the current height.");
                TestRunner.Equal(true, canvas.Visible && canvas.GridMode, "The app grid must remain visible during panel collapse.");
                select.Invoke(window, new object[] { SearchScope.Applications }); tabs[0].Focus();
                TestRunner.Equal(partial, window.Height, "Reopening midway must not reset panel size.");
                deadline = DateTime.UtcNow.AddSeconds(2);
                while (window.Height != target && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                TestRunner.Equal(target, window.Height, "Expanded panel must settle at its final size.");
                input.Focus(); hover.Invoke(window, new object[] { new Point(20, 20) });
                deadline = DateTime.UtcNow.AddSeconds(2);
                while (window.Height != 76 && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                TestRunner.Equal(76, window.Height, "Collapsed panel must return to the compact search bar.");
                TestRunner.Equal(false, canvas.Visible, "The app grid must disappear only after collapse finishes.");
                window.HideLauncher();
            }
        }
        private static void CategoryRevealAnimatesAndReversesWithoutJumping()
        {
            var progress = typeof(SearchTabButton).GetProperty("RevealProgress");
            TestRunner.Equal(true, progress != null, "Category buttons must support progressive reveal rather than abrupt visibility changes.");
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { HideOnDeactivate = false, AnimationsEnabled = false }))
            {
                window.ShowLauncher(true, false);
                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var expand = typeof(LauncherWindow).GetMethod("ExpandTabs", BindingFlags.NonPublic | BindingFlags.Instance);
                if (Motion.SystemAllowsAnimation())
                {
                    window.Settings.AnimationsEnabled = true;
                    expand.Invoke(window, new object[] { true }); tabs[0].Focus();
                    TestRunner.Equal(true, (double)progress.GetValue(tabs[0], null) < 1, "Hover reveal must begin before the fully visible state.");
                    DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                    while ((double)progress.GetValue(tabs[0], null) == 0 && DateTime.UtcNow < deadline)
                    { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                    double partial = (double)progress.GetValue(tabs[0], null);
                    TestRunner.Equal(true, partial > 0 && partial < 1, "Reveal must render an intermediate state.");
                    TestRunner.Equal(true, partial > (double)progress.GetValue(tabs[3], null), "Buttons must enter with a short stagger.");
                    Capture(window, "category-reveal-mid.png");
                    expand.Invoke(window, new object[] { false });
                    TestRunner.Equal(partial, (double)progress.GetValue(tabs[0], null), "Closing must continue from current progress without snapping.");
                    TestRunner.Equal(true, tabs[0].Visible, "Closing animation must retain the button until fading finishes.");
                    expand.Invoke(window, new object[] { true });
                    TestRunner.Equal(partial, (double)progress.GetValue(tabs[0], null), "Reopening midway must not reset the animation.");
                    deadline = DateTime.UtcNow.AddSeconds(2);
                    while ((double)progress.GetValue(tabs[3], null) < 1 && DateTime.UtcNow < deadline)
                    { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                    TestRunner.Equal(1.0, (double)progress.GetValue(tabs[3], null), "All buttons must settle fully visible.");
                    Capture(window, "category-reveal-final.png");
                    expand.Invoke(window, new object[] { false });
                    deadline = DateTime.UtcNow.AddSeconds(2);
                    while (tabs[0].Visible && DateTime.UtcNow < deadline)
                    { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                    foreach (var tab in tabs) TestRunner.Equal(false, tab.Visible, "Animated closing must eventually remove every category button.");
                    TestRunner.Equal(false, window.Region.IsVisible(tabs[3].Bounds.Location + new Size(27, 27)), "Closed categories must no longer leave a native hit region.");
                }
                window.Settings.AnimationsEnabled = false;
                expand.Invoke(window, new object[] { false });
                foreach (var tab in tabs) TestRunner.Equal(false, tab.Visible, "Disabled animations must close immediately.");
                expand.Invoke(window, new object[] { true });
                foreach (var tab in tabs) TestRunner.Equal(1.0, (double)progress.GetValue(tab, null), "Disabled animations must reveal all buttons immediately.");
                window.HideLauncher();
            }
        }
        private static void IndependentApplicationsViewHasNoSearchAndDoubleClickOpens()
        {
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { HideOnDeactivate = false, AnimationsEnabled = false }))
            {
                window.ShowLauncher(true, true);
                var input = (TextBox)typeof(LauncherWindow).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                TestRunner.Equal(false, input.Visible, "Alt+A applications view must not include a search input.");
                TestRunner.Equal(true, canvas.Visible && canvas.GridMode, "Independent applications view must show the app grid immediately.");
                typeof(LauncherWindow).GetMethod("ExpandTabs", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { true });
                foreach (var tab in tabs) TestRunner.Equal(false, tab.Visible, "Independent applications view must not reveal search categories.");
                var expected = new SearchItem { Name = "Test app", Path = "test.exe", Kind = ResultKind.Application };
                SearchItem opened = null;
                canvas.SetItems(new List<SearchItem> { expected }); window.LaunchRequested = delegate(SearchItem item) { opened = item; };
                typeof(LauncherCanvas).GetMethod("OnMouseDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 40, 30, 0) });
                TestRunner.Equal(expected, opened, "Double-click must open an app without any search query.");
                window.ShowLauncher(true, false);
                TestRunner.Equal(true, input.Visible, "Returning to Alt+Space search must restore its input.");
                TestRunner.Equal(false, canvas.Visible, "Returning to search must restore the compact initial state.");
                window.HideLauncher();
            }
        }
        private static void ApplicationPreviewCollapsesOnLeaveAndSearchStaysOpen()
        {
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { HideOnDeactivate = false, AnimationsEnabled = false, FileSearchEnabled = false }))
            {
                window.ShowLauncher(true, false);
                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var input = (TextBox)typeof(LauncherWindow).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var expand = typeof(LauncherWindow).GetMethod("ExpandTabs", BindingFlags.NonPublic | BindingFlags.Instance);
                var hover = typeof(LauncherWindow).GetMethod("UpdateHoverState", BindingFlags.NonPublic | BindingFlags.Instance);
                TestRunner.Equal(true, hover != null, "Hover preview must handle pointer departure, not only category selection.");
                expand.Invoke(window, new object[] { true });
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tabs[0], new object[] { EventArgs.Empty });
                TestRunner.Equal(true, canvas.Visible, "Hover must reveal app preview.");
                hover.Invoke(window, new object[] { new Point(tabs[0].Left + 27, 72) });
                TestRunner.Equal(true, canvas.Visible, "Crossing the gap below the app tab must not collapse the grid.");
                hover.Invoke(window, new object[] { new Point(100, 180) });
                TestRunner.Equal(true, canvas.Visible, "Moving into the app grid must keep it available for double-click and right-click.");
                hover.Invoke(window, new object[] { new Point(100, 30) });
                TestRunner.Equal(false, canvas.Visible, "Moving back to input must collapse empty app preview.");
                TestRunner.Equal(76, window.Height, "Collapsed preview must restore a single bar.");
                expand.Invoke(window, new object[] { true });
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tabs[0], new object[] { EventArgs.Empty });
                TestRunner.Equal(true, canvas.Visible, "Same app category must reopen after departure.");
                input.Text = "missing";
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (window.Height != 204 && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                hover.Invoke(window, new object[] { new Point(-20, -20) });
                TestRunner.Equal(true, canvas.Visible && window.Height > 76, "Nonempty search must stay expanded when pointer leaves.");
                window.HideLauncher();
            }
        }
        private static void InitialSearchBarAndHoverCategories()
        {
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { HideOnDeactivate = false, AnimationsEnabled = false }))
            {
                window.ShowLauncher(true, false);
                var tabs = (SearchTabButton[])typeof(LauncherWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var reveal = (Panel)typeof(LauncherWindow).GetField("_tabReveal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                TestRunner.Equal(76, window.Height, "Initial window must be one search bar.");
                TestRunner.Equal(false, canvas.Visible, "Initial bar must not expose an app grid.");
                foreach (var tab in tabs) TestRunner.Equal(false, tab.Visible, "Categories must start collapsed.");
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reveal, new object[] { EventArgs.Empty });
                foreach (var tab in tabs) TestRunner.Equal(true, tab.Visible, "Hovering right edge must reveal all four categories.");
                TestRunner.Equal(true, window.Region.IsVisible(tabs[3].Bounds.Location + new Size(27, 27)), "Expanded circular tab must be clickable through the native window region.");
                using (var graphics = window.CreateGraphics())
                using (var blank = new Region(new Rectangle(0, 0, 1, 1)))
                {
                    IntPtr handle = blank.GetHrgn(graphics);
                    try
                    {
                        TestRunner.Greater(GetWindowRgn(window.Handle, handle), 0, "Native HWND must have a clipping region.");
                        using (var native = Region.FromHrgn(handle)) TestRunner.Equal(true, native.IsVisible(tabs[3].Bounds.Location + new Size(27, 27)), "The native region, not just Form.Region, must include expanded tabs.");
                    }
                    finally { DeleteObject(handle); }
                }
                Capture(window, "search-bar-expanded.png");
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tabs[0], new object[] { EventArgs.Empty });
                TestRunner.Equal(true, canvas.GridMode && canvas.Visible, "Hovering Applications, without clicking, must expose the grid.");
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tabs[2], new object[] { EventArgs.Empty });
                TestRunner.Equal(76, window.Height, "Hovering All must restore compact empty search.");
                typeof(LauncherWindow).GetMethod("ExpandTabs", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { false });
                Capture(window, "search-bar-collapsed.png");
                window.HideLauncher();
            }
        }
        private static void Capture(Form window, string name)
        {
            using (var image = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name)); }
        }
        private static void InstalledDouyinMatchesInitials()
        {
            using (var index = new AppIndex(AppIndex.DefaultRoots()))
            {
                index.Refresh(); var engine = new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()) { FileSearchEnabled = false };
                bool installed = false; foreach (var item in index.Items) installed |= item.Name.Contains("抖音");
                if (!installed) return;
                bool found = false; foreach (var item in engine.SearchApplications("dy", 8)) found |= item.Name.Contains("抖音");
                TestRunner.Equal(true, found, "Installed Douyin must be in dy's top eight application results.");
                found = false; foreach (var item in engine.SearchAsync("dy", 8, System.Threading.CancellationToken.None).GetAwaiter().GetResult()) found |= item.Name.Contains("抖音");
                TestRunner.Equal(true, found, "Async search must retain the Chinese initial match.");
            }
        }
    }
}
