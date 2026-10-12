using System;
using System.Collections.Generic;
using System.Threading;
namespace FocusSearch.Tests
{
    internal static class SearchEngineTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(QueryEscapesQuotes); tests.Add(CancelledSearchDoesNotReturnResults); tests.Add(SearchLimitsAndDeduplicates); tests.Add(UsageUpdatesRecentOrdering); tests.Add(ApplicationSearchMatchesPath); tests.Add(ExecutingFileSearchCanBeCancelled); }
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
