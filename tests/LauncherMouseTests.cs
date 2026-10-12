using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FocusSearch.Tests
{
    internal static class LauncherMouseTests
    {
        public static void Register(ICollection<Action> tests)
        {
            tests.Add(SingleClickSelectsAndDoubleClickOpensInBothLayouts);
            tests.Add(RightClickTargetsClickedItemAndUsesCustomMenu);
            tests.Add(MenuDoesNotDeactivateLauncher);
            tests.Add(RevealLocationWaitsForResultAndReportsFailure);
            tests.Add(StandardGridUsesSixColumnsForNavigationAndHitTesting);
        }
        private static void StandardGridUsesSixColumnsForNavigationAndHitTesting()
        {
            foreach (int width in new[] { 684, 1120 })
                using (var canvas = new LauncherCanvas { Width = width, Height = 320, GridMode = true })
                {
                    var items = new List<SearchItem>();
                    for (int i = 0; i < 20; i++) items.Add(new SearchItem { Name = "App " + i, Path = "app" + i });
                    SearchItem opened = null;
                    canvas.ActivateItem = delegate(SearchItem item) { opened = item; };
                    canvas.SetItems(items);
                    canvas.Navigate(Keys.Down); canvas.OpenSelected();
                    TestRunner.Equal(items[6], opened, "Down must move six apps in both preview and full-size grids.");
                    canvas.SetItems(items);
                    Mouse(canvas, "OnMouseDoubleClick", MouseButtons.Left, new Point(width / 6 * 5 + 10, 30));
                    TestRunner.Equal(items[5], opened, "The sixth tile in the first row must open its own app.");
                    canvas.SetItems(items);
                    canvas.Navigate(Keys.PageDown); canvas.OpenSelected();
                    TestRunner.Equal(items[12], opened, "Paging must account for two rows of six apps.");
                }
        }
        private static void RevealLocationWaitsForResultAndReportsFailure()
        {
            var constructor = typeof(LauncherWindow).GetConstructor(new[] { typeof(AppIndex), typeof(SearchEngine), typeof(AppSettings), typeof(Func<string, Task>) });
            TestRunner.Equal(true, constructor != null, "Location opening must expose an asynchronous shell boundary so failure cannot be mistaken for success.");
            foreach (bool success in new[] { false, true })
                using (var index = new AppIndex(new string[0]))
                {
                    var completion = new TaskCompletionSource<bool>();
                    string received = null;
                    Func<string, Task> reveal = delegate(string path) { received = path; return completion.Task; };
                    using (var window = (LauncherWindow)constructor.Invoke(new object[] { index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { AnimationsEnabled = false, HideOnDeactivate = true }, reveal }))
                    {
                        window.ShowLauncher(true, true);
                        var action = typeof(LauncherWindow).GetMethod("InvokeContextAction", BindingFlags.Instance | BindingFlags.NonPublic);
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FocusSearch.Tests.exe");
                        action.Invoke(window, new object[] { new SearchItem { Name = "Probe", Path = path }, ItemAction.ShowInFolder });
                        TestRunner.Equal(path, received, "Reveal must receive the selected item's full path.");
                        typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { EventArgs.Empty });
                        Application.DoEvents();
                        TestRunner.Equal(true, window.Visible && !window.IsHiding, "Launcher must stay open while Explorer's result is pending.");
                        if (success) completion.SetResult(true); else completion.SetException(new IOException("probe reveal failed"));
                        var footer = (Label)typeof(LauncherWindow).GetField("_footer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                        while (DateTime.UtcNow < deadline && (success ? window.Visible : !footer.Text.Contains("probe reveal failed")))
                        { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                        if (success) TestRunner.Equal(false, window.Visible, "Launcher must close only after a confirmed successful reveal.");
                        else
                        {
                            TestRunner.Equal(true, window.Visible && !window.IsHiding && footer.Visible, "Failed reveal must preserve the launcher and show its error.");
                            TestRunner.Equal(true, footer.Text.Contains("probe reveal failed"), "The actual failure must be visible instead of silently returning to desktop.");
                        }
                        window.HideLauncher();
                    }
                }
        }
        private static void Mouse(LauncherCanvas canvas, string method, MouseButtons button, Point point)
        { typeof(LauncherCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new MouseEventArgs(button, method == "OnMouseDoubleClick" ? 2 : 1, point.X, point.Y, 0) }); }
        private static IList<SearchItem> Items()
        { return new List<SearchItem> { new SearchItem { Name = "第一个应用", Path = @"C:\first.exe" }, new SearchItem { Name = "第二个应用", Path = @"C:\second.exe" } }; }
        private static void SingleClickSelectsAndDoubleClickOpensInBothLayouts()
        {
            foreach (bool grid in new[] { true, false })
                using (var canvas = new LauncherCanvas { Width = 320, Height = 320, GridMode = grid })
                {
                    var items = Items(); canvas.SetItems(items); SearchItem opened = null; int count = 0;
                    canvas.ActivateItem = delegate(SearchItem item) { opened = item; count++; };
                    Point point = grid ? new Point(220, 40) : new Point(100, 75);
                    Mouse(canvas, "OnMouseClick", MouseButtons.Left, point);
                    TestRunner.Equal(0, count, "Single-click must only select in grid and list layouts.");
                    canvas.OpenSelected(); TestRunner.Equal(items[1], opened, "Single-click must select the clicked item.");
                    Mouse(canvas, "OnMouseClick", MouseButtons.Left, new Point(40, 30));
                    Mouse(canvas, "OnMouseDoubleClick", MouseButtons.Left, point);
                    TestRunner.Equal(items[1], opened, "Double-click must open its own hit-tested item.");
                    TestRunner.Equal(2, count, "Double-click must launch exactly once.");
                    Mouse(canvas, "OnMouseDoubleClick", MouseButtons.Left, new Point(10, 319));
                    TestRunner.Equal(2, count, "Empty space must not open the previous selection.");
                }
        }
        private static void RightClickTargetsClickedItemAndUsesCustomMenu()
        {
            using (var owner = new Form())
            using (var canvas = new LauncherCanvas { Width = 320, Height = 320, GridMode = true })
            {
                owner.Controls.Add(canvas); owner.Show(); var items = Items(); canvas.SetItems(items);
                SearchItem target = null; ItemAction selected = ItemAction.Open; int launches = 0;
                canvas.ActivateItem = delegate(SearchItem item) { launches++; target = item; };
                canvas.ContextActionRequested = delegate(SearchItem item, ItemAction action) { target = item; selected = action; };
                Mouse(canvas, "OnMouseClick", MouseButtons.Right, new Point(220, 40));
                TestRunner.Equal(0, launches, "Right-click must not launch an application.");
                TestRunner.Equal(true, canvas.IsContextMenuOpen, "Right-click must expand the action menu.");
                var menu = (ItemContextMenu)typeof(LauncherCanvas).GetField("_itemMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(canvas);
                TestRunner.Equal(true, menu.Renderer is ItemMenuRenderer, "Popup must use the custom glass renderer.");
                menu.Items[2].Select();
                using (var image = new Bitmap(menu.Width, menu.Height)) { menu.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "item-context-menu.png")); }
                menu.Items[5].PerformClick();
                TestRunner.Equal(items[1], target, "Context action must use the right-clicked item.");
                TestRunner.Equal(ItemAction.CopyPath, selected, "Copy path must dispatch its own action.");
                canvas.CloseItemMenu();
                Mouse(canvas, "OnMouseClick", MouseButtons.Right, new Point(20, 300));
                TestRunner.Equal(false, canvas.IsContextMenuOpen, "Blank grid space must not show an item menu.");
                owner.Close();
            }
        }
        private static void MenuDoesNotDeactivateLauncher()
        {
            using (var index = new AppIndex(new string[0]))
            using (var window = new LauncherWindow(index, new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()), new AppSettings { AnimationsEnabled = false }))
            {
                window.ShowLauncher(true, false);
                var canvas = (LauncherCanvas)typeof(LauncherWindow).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                canvas.SetItems(Items());
                Mouse(canvas, "OnMouseClick", MouseButtons.Right, new Point(40, 30));
                typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { EventArgs.Empty });
                Application.DoEvents();
                TestRunner.Equal(true, window.Visible && canvas.IsContextMenuOpen, "Opening a menu must not trigger hide-on-deactivate.");
                window.HideLauncher(); TestRunner.Equal(false, canvas.IsContextMenuOpen, "Hiding launcher must close its popup.");
            }
        }
    }
}
