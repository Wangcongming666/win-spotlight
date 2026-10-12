using System;
using System.Collections.Generic;
using System.IO;
namespace FocusSearch.Tests
{
    internal static class AppIndexTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(IndexFiltersAndDeduplicates); }
        private static void IndexFiltersAndDeduplicates()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusIndex-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "Notes.lnk"), "broken shortcut");
                File.WriteAllText(Path.Combine(root, "ignored.txt"), "text");
                Directory.CreateDirectory(Path.Combine(root, "copy"));
                File.WriteAllText(Path.Combine(root, "copy", "Notes.lnk"), "broken shortcut");
                using (var index = new AppIndex(new[] { root, root, Path.Combine(root, "missing") }))
                {
                    index.Refresh();
                    TestRunner.Equal(2, index.Items.Count, "Distinct same-named shortcuts must be preserved; duplicate roots must not duplicate paths.");
                    TestRunner.Equal(2, index.Search("notes", 8).Count, "Name search must retain both distinct paths.");
                    TestRunner.Equal(0, index.Search("calculator", 8).Count, "Unmatched items must be excluded.");
                }
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
