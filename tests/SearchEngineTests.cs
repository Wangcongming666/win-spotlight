using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace FocusSearch.Tests
{
    internal static class SearchEngineTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(QueryEscapesQuotes); tests.Add(CancelledSearchDoesNotReturnResults); tests.Add(SearchLimitsAndDeduplicates); tests.Add(UsageUpdatesRecentOrdering); tests.Add(ApplicationSearchMatchesPath); tests.Add(ExecutingFileSearchCanBeCancelled); tests.Add(CategoriesFilterResultsAndSkipUnneededFileSearch); tests.Add(ApplicationViewsExcludeNonApplications); tests.Add(SearchOrdersApplicationsBeforeFoldersBeforeFiles); tests.Add(MarkdownExtensionQueriesReturnDocumentsInsteadOfApplicationAliases); }
        private static void MarkdownExtensionQueriesReturnDocumentsInsteadOfApplicationAliases()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusMdExtension-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "KNN算法.md"), "fixture");
                File.WriteAllText(Path.Combine(root, "集成学习.MD"), "fixture");
                File.WriteAllText(Path.Combine(root, "普通文档.txt"), "fixture");
                Directory.CreateDirectory(Path.Combine(root, "md-folder"));
                var apps = new List<SearchItem>();
                for (int i = 0; i < 12; i++) apps.Add(new SearchItem { Name = "MD App " + i, Path = "app" + i, Kind = ResultKind.Application });
                var engine = new SearchEngine(new FixtureIndex { Items = apps }, new LocalFileProvider(new[] { root }), new Dictionary<string, UsageRecord>());
                foreach (string query in new[] { "md", ".md", "*.md" })
                {
                    var results = engine.SearchAsync(query, 8, CancellationToken.None).GetAwaiter().GetResult();
                    TestRunner.Equal(2, results.Count, "Extension queries must return both Markdown documents even when more than eight application aliases match.");
                    foreach (var result in results)
                    {
                        TestRunner.Equal(ResultKind.File, result.Kind, "Extension search must exclude apps and folders.");
                        TestRunner.Equal(".md", Path.GetExtension(result.Path).ToLowerInvariant(), "Matching a parent directory must not include unrelated file extensions.");
                    }
                }
                TestRunner.Equal(8, engine.SearchAsync("md", 8, CancellationToken.None, SearchScope.Applications).GetAwaiter().GetResult().Count, "An explicitly selected application category must still search application names.");
                var folders = engine.SearchAsync("md", 8, CancellationToken.None, SearchScope.Folders).GetAwaiter().GetResult();
                TestRunner.Equal(1, folders.Count, "An explicitly selected folder category must still find folders named md, without converting to extension search.");
                TestRunner.Equal(ResultKind.Folder, folders[0].Kind, "Folder intent must remain independent of file-type intent.");
            }
            finally { Directory.Delete(root, true); }
        }
        private static void SearchOrdersApplicationsBeforeFoldersBeforeFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusSearchOrder-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string document = Path.Combine(root, "wx");
                File.WriteAllText(document, "fixture");
                Directory.CreateDirectory(Path.Combine(root, "WXWork"));
                Directory.CreateDirectory(Path.Combine(root, "WXWorkCefCache"));
                var index = new FixtureIndex { Items = new List<SearchItem> {
                    new SearchItem { Name = "微信", Path = "wechat.lnk", Kind = ResultKind.Application },
                    new SearchItem { Name = "wx", Path = "exact-app.lnk", Kind = ResultKind.Application }
                } };
                var engine = new SearchEngine(index, new LocalFileProvider(new[] { root }), new Dictionary<string, UsageRecord>());
                for (int i = 0; i < 30; i++) engine.RecordLaunch(document);
                var results = engine.SearchAsync("wx", 8, CancellationToken.None).GetAwaiter().GetResult();
                TestRunner.Equal(5, results.Count, "Combined search must retain both apps, both folders and the document.");
                TestRunner.Equal("exact-app.lnk", results[0].Path, "Within applications, exact matches must rank above initials.");
                TestRunner.Equal("wechat.lnk", results[1].Path, "WeChat initials must rank above better-matching folders and frequently used files.");
                TestRunner.Equal(ResultKind.Folder, results[2].Kind, "Folders must follow applications.");
                TestRunner.Equal(ResultKind.Folder, results[3].Kind, "All folders must precede files.");
                TestRunner.Equal(document, results[4].Path, "An exact and frequently used file must still follow folders.");
                TestRunner.Equal(ResultKind.Application, engine.SearchAsync("wx", 1, CancellationToken.None).GetAwaiter().GetResult()[0].Kind, "Type priority must apply before truncating results.");
            }
            finally { Directory.Delete(root, true); }
        }
        private static void ApplicationViewsExcludeNonApplications()
        {
            var items = new List<SearchItem> {
                new SearchItem { Name = "Note app", Path = "app", Kind = ResultKind.Application },
                new SearchItem { Name = "Note document", Path = "document.lnk", Kind = ResultKind.File },
                new SearchItem { Name = "Note folder", Path = "folder.lnk", Kind = ResultKind.Folder },
                new SearchItem { Name = "Note website", Path = "website.url", Kind = ResultKind.Url }
            };
            var files = new CategoryFiles();
            var engine = new SearchEngine(new FixtureIndex { Items = items }, files, new Dictionary<string, UsageRecord>()) { FileSearchEnabled = false };
            engine.RecordLaunch("folder.lnk");
            TestRunner.Equal(1, engine.Recent(10000).Count, "Application grid must exclude non-apps even when frequently opened.");
            TestRunner.Equal("app", engine.Recent(10000)[0].Path, "The remaining grid entry must be the application.");
            TestRunner.Equal(1, engine.SearchApplications("note", 8).Count, "Immediate application results must exclude files, folders and websites.");
            TestRunner.Equal(1, engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Applications).GetAwaiter().GetResult().Count, "Application category must exclude non-app shortcuts.");
            TestRunner.Equal(4, engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.All).GetAwaiter().GetResult().Count, "Spotlight must retain indexed non-app shortcuts.");
            engine.FileSearchEnabled = true;
            TestRunner.Equal(2, engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Folders).GetAwaiter().GetResult().Count, "Folder search must include both folder shortcuts and file-provider folders.");
            TestRunner.Equal(2, engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Files).GetAwaiter().GetResult().Count, "File search must include both document shortcuts and file-provider files.");
        }
        private sealed class CategoryFiles : IFileSearchProvider
        {
            public int Calls;
            public IList<SearchItem> Search(string query, int limit, CancellationToken token)
            {
                Calls++; return new List<SearchItem> { new SearchItem { Name = "note.txt", Path = "file", Kind = ResultKind.File }, new SearchItem { Name = "notes", Path = "folder", Kind = ResultKind.Folder } };
            }
        }
        private static void CategoriesFilterResultsAndSkipUnneededFileSearch()
        {
            var files = new CategoryFiles();
            var engine = new SearchEngine(new FixtureIndex { Items = new List<SearchItem> { new SearchItem { Name = "Notepad", Path = "app", Kind = ResultKind.Application } } }, files, new Dictionary<string, UsageRecord>());
            var apps = engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Applications).GetAwaiter().GetResult();
            TestRunner.Equal(1, apps.Count, "Applications category must show only apps.");
            TestRunner.Equal(0, files.Calls, "Applications category must not query the file provider.");
            var folders = engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Folders).GetAwaiter().GetResult();
            TestRunner.Equal(1, folders.Count, "Folders category must exclude files and apps.");
            TestRunner.Equal(ResultKind.Folder, folders[0].Kind, "Folders category must return actual folders.");
            var documents = engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.Files).GetAwaiter().GetResult();
            TestRunner.Equal(1, documents.Count, "Files category must exclude folders and apps.");
            TestRunner.Equal(ResultKind.File, documents[0].Kind, "Files category must return files.");
            TestRunner.Equal(3, engine.SearchAsync("note", 8, CancellationToken.None, SearchScope.All).GetAwaiter().GetResult().Count, "All category must combine app and file results.");
            TestRunner.Equal(0, engine.SearchAsync("", 8, CancellationToken.None, SearchScope.Folders).GetAwaiter().GetResult().Count, "Empty folder queries must not expose application history.");
        }
        private sealed class BlockingFiles : IFileSearchProvider
        {
            public readonly ManualResetEvent Entered = new ManualResetEvent(false);
            public IList<SearchItem> Search(string query, int limit, CancellationToken token)
            { Entered.Set(); token.WaitHandle.WaitOne(3000); token.ThrowIfCancellationRequested(); return new List<SearchItem>(); }
        }
        private static void ExecutingFileSearchCanBeCancelled()
        {
            var index = new FixtureIndex { Items = new List<SearchItem> { new SearchItem { Name = "Note", Path = "note.exe" } } };
            var files = new BlockingFiles();
            var engine = new SearchEngine(index, files, new Dictionary<string, UsageRecord>());
            using (var cancellation = new CancellationTokenSource())
            {
                var task = engine.SearchAsync("note", 8, cancellation.Token);
                TestRunner.Equal(true, files.Entered.WaitOne(3000), "Fixture must enter actual file-search task.");
                TestRunner.Equal(1, engine.SearchApplications("note", 8).Count, "Applications must remain available while files are pending.");
                cancellation.Cancel(); bool cancelled = false;
                try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                TestRunner.Equal(true, cancelled, "Cancellation during provider execution must propagate.");
            }
            files.Entered.Dispose();
        }
        private static void ApplicationSearchMatchesPath()
        {
            var index = new FixtureIndex { Items = new List<SearchItem> { new SearchItem { Name = "Editor", Path = @"C:\Tools\Creative\Editor.exe" } } };
            var engine = new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()) { FileSearchEnabled = false };
            TestRunner.Equal(1, engine.SearchAsync("Creative", 8, CancellationToken.None).GetAwaiter().GetResult().Count, "Path fragment must match application.");
        }
        private sealed class FixtureIndex : IAppIndex { public IList<SearchItem> Items { get; set; } }
        private static void SearchLimitsAndDeduplicates()
        {
            var items = new List<SearchItem>();
            for (int i = 0; i < 12; i++) items.Add(new SearchItem { Name = "Note " + i, Path = "path" + i, Kind = ResultKind.Application });
            items.Add(new SearchItem { Name = "Note duplicate", Path = "path0", Kind = ResultKind.Application });
            var engine = new SearchEngine(new FixtureIndex { Items = items }, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>()) { FileSearchEnabled = false };
            var results = engine.SearchAsync("Note", 100, CancellationToken.None).GetAwaiter().GetResult();
            TestRunner.Equal(8, results.Count, "Result cap must hold even when caller asks for more.");
            var paths = new HashSet<string>(); foreach (var item in results) paths.Add(item.Path);
            TestRunner.Equal(8, paths.Count, "Results must contain unique paths.");
        }
        private static void UsageUpdatesRecentOrdering()
        {
            var items = new List<SearchItem> { new SearchItem { Name = "Alpha", Path = "alpha" }, new SearchItem { Name = "Zeta", Path = "zeta" } };
            var engine = new SearchEngine(new FixtureIndex { Items = items }, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>());
            engine.RecordLaunch("zeta");
            TestRunner.Equal("zeta", engine.Recent(1)[0].Path, "Launch must update ordering without restarting.");
        }
        private static void QueryEscapesQuotes()
        {
            TestRunner.Equal("a''b[%][_][[]", WindowsSearchProvider.EscapeContainsValue("a'b%_["), "LIKE input must escape SQL quotes and wildcard characters.");
        }
        private static void CancelledSearchDoesNotReturnResults()
        {
            using (var index = new AppIndex(new string[0]))
            {
                var engine = new SearchEngine(index, new WindowsSearchProvider(), new Dictionary<string, UsageRecord>());
                var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                bool cancelled = false;
                try { engine.SearchAsync("note", 8, cancellation.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
                TestRunner.Equal(true, cancelled, "Cancellation must propagate before querying.");
            }
        }
    }
}
