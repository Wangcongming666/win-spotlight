using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace FocusSearch
{
    internal sealed class LocalFileProvider : IFileSearchProvider
    {
        private readonly string[] _roots;
        public LocalFileProvider(IEnumerable<string> roots) { _roots = roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
        public static LocalFileProvider ForUser()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new LocalFileProvider(new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Path.Combine(profile, "Downloads") });
        }
        public IList<SearchItem> Search(string query, int limit)
        { return Search(query, limit, CancellationToken.None); }
        public IList<SearchItem> Search(string query, int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var matches = new List<SearchItem>();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2 || limit <= 0) return matches;
            var watch = Stopwatch.StartNew(); int inspected = 0;
            foreach (string root in _roots) Scan(root, 0, query, Math.Min(32, limit), matches, watch, ref inspected, token);
            return matches.GroupBy(i => i.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(limit).ToList();
        }
        private static void Scan(string root, int depth, string query, int limit, IList<SearchItem> matches, Stopwatch watch, ref int inspected, CancellationToken token)
        {
            if (depth > 2 || matches.Count >= limit || inspected >= 5000 || watch.ElapsedMilliseconds > 250) return;
            try
            {
                foreach (string file in Directory.EnumerateFiles(root))
                {
                    token.ThrowIfCancellationRequested();
                    if (++inspected > 5000 || watch.ElapsedMilliseconds > 250 || matches.Count >= limit) break;
                    string name = Path.GetFileName(file);
                    if (file.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) matches.Add(new SearchItem { Name = name, Path = file, Detail = file, IconKey = file, Kind = ResultKind.File });
                }
                foreach (string child in Directory.EnumerateDirectories(root))
                {
                    if (matches.Count >= limit || inspected >= 5000 || watch.ElapsedMilliseconds > 250) break;
                    token.ThrowIfCancellationRequested();
                    if (child.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) matches.Add(new SearchItem { Name = Path.GetFileName(child), Path = child, Detail = child, IconKey = child, Kind = ResultKind.Folder });
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) Scan(child, depth + 1, query, limit, matches, watch, ref inspected, token);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
