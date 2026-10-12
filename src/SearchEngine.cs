using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace FocusSearch
{
    internal sealed class SearchEngine
    {
        private readonly IAppIndex _index;
        private readonly IFileSearchProvider _files;
        private readonly IDictionary<string, UsageRecord> _history;
        private readonly object _historyGate = new object();
        private readonly SemaphoreSlim _fileGate = new SemaphoreSlim(1, 1);
        public bool FileSearchEnabled { get; set; }
        public string FileSearchStatus { get { var provider = _files as WindowsSearchProvider; return provider == null || !FileSearchEnabled ? string.Empty : provider.Status; } }
        public SearchEngine(IAppIndex index, IFileSearchProvider files, IDictionary<string, UsageRecord> history)
        { _index = index; _files = files; _history = history; FileSearchEnabled = true; }
        public IList<SearchItem> Recent(int limit)
        { return _index.Items.OrderByDescending(Usage).ThenBy(i => i.Name).Take(Math.Max(0, limit)).ToList(); }
        private int Usage(SearchItem item)
        { lock (_historyGate) { UsageRecord record; return _history.TryGetValue(item.Path, out record) ? Math.Min(30, record.LaunchCount) * 10 : 0; } }
        public void RecordLaunch(string path)
        {
            lock (_historyGate)
            {
                UsageRecord record;
                if (!_history.TryGetValue(path, out record)) { record = new UsageRecord { Path = path }; _history[path] = record; }
                record.LaunchCount = Math.Min(int.MaxValue - 1, record.LaunchCount) + 1; record.LastLaunchedUtc = DateTime.UtcNow;
            }
        }
        public async Task<IList<SearchItem>> SearchAsync(string query, int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            query = (query ?? string.Empty).Trim();
            if (query.Length == 0) return Recent(limit);
            var candidates = _index.Items.Where(i => Match(query, i) > 0).ToList();
            if (FileSearchEnabled && query.Length >= 2)
            {
                await _fileGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    candidates.AddRange(await Task.Run(() => _files.Search(query, 32, token), token).ConfigureAwait(false));
                }
                finally { _fileGate.Release(); }
            }
            token.ThrowIfCancellationRequested();
            return candidates.GroupBy(i => i.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .OrderByDescending(i => Match(query, i) + Usage(i) + (i.Kind == ResultKind.Application ? 200 : 0))
                .ThenBy(i => i.Name).Take(Math.Max(0, Math.Min(8, limit))).ToList();
        }
        private static int Match(string query, SearchItem item) { return Math.Max(FuzzyMatcher.Score(query, item.Name), FuzzyMatcher.Score(query, item.Path) / 2); }
        public IList<SearchItem> SearchApplications(string query, int limit)
        {
            return _index.Items.Where(i => Match(query, i) > 0).OrderByDescending(i => Match(query, i) + Usage(i)).ThenBy(i => i.Name).Take(Math.Min(8, Math.Max(0, limit))).ToList();
        }
    }
}
