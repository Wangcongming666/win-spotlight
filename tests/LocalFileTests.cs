using System;
using System.Collections.Generic;
using System.IO;
namespace FocusSearch.Tests
{
    internal static class LocalFileTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(FallbackSearchFindsUserFile); }
        private static void FallbackSearchFindsUserFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusFiles-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "项目说明.txt"), "fixture");
                var search = new LocalFileProvider(new[] { root });
                TestRunner.Equal(1, search.Search("说明", 8).Count, "Fallback must search actual user filenames.");
                TestRunner.Equal(0, search.Search("不存在", 8).Count, "Fallback must exclude unmatched files.");
                Directory.CreateDirectory(Path.Combine(root, "项目资料"));
                var folders = search.Search("资料", 8);
                TestRunner.Equal(1, folders.Count, "Fallback must find folder names.");
                TestRunner.Equal(ResultKind.Folder, folders[0].Kind, "Folder results must be classified correctly.");
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
