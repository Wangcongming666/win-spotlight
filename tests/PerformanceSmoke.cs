using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class PerformanceSmoke
    {
        public static void Run()
        {
            Application.EnableVisualStyles();
            using (var index = new AppIndex(AppIndex.DefaultRoots()))
            using (var canvas = new LauncherCanvas { Width = 752, Height = 430, GridMode = true })
            {
                index.Refresh(); canvas.SetItems(index.Items);
                var timings = new List<double>();
                for (int i = 0; i < 12; i++)
                {
                    var watch = Stopwatch.StartNew(); canvas.Navigate(Keys.PageDown); watch.Stop(); timings.Add(watch.Elapsed.TotalMilliseconds);
                }
                Console.WriteLine("PAGE_NAV apps=" + index.Items.Count + " averageMs=" + timings.Average().ToString("F2") + " maxMs=" + timings.Max().ToString("F2"));
            }
        }
    }
}
