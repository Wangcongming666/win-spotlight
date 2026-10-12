using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
namespace FocusSearch.Tests
{
    internal static class IconCacheTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(PrefetchNeverEvictsVisibleIcons); }
        private static void PrefetchNeverEvictsVisibleIcons()
        {
            var items = new List<SearchItem>(); for (int i = 0; i < 136; i++) items.Add(new SearchItem { Name = "Item " + i, Path = "icon" + i });
            int loaded = 0; int goal = 96;
            using (var ready = new ManualResetEvent(false))
            using (var cache = new ShellIconCache(delegate(SearchItem item) { return new Bitmap(1, 1); }))
            {
                cache.Ready += delegate { if (Interlocked.Increment(ref loaded) >= Volatile.Read(ref goal)) ready.Set(); };
                var visible = items.GetRange(0, 56);
                cache.Prefetch(items.GetRange(0, 96), visible);
                TestRunner.Equal(true, ready.WaitOne(5000), "Initial icons must load."); cache.DrainReady();
                ready.Reset(); Volatile.Write(ref goal, 136);
                var next = new List<SearchItem>(visible); next.AddRange(items.GetRange(96, 40));
                cache.Prefetch(next, visible);
                TestRunner.Equal(true, ready.WaitOne(5000), "Neighbor icons must load."); cache.DrainReady();
                foreach (var item in visible) TestRunner.Equal(1, cache.Get(item).Width, "Visible icon must remain a loaded image, not a placeholder.");
            }
        }
    }
}
