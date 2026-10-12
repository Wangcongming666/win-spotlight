using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace FocusSearch
{
    internal interface IAppIndex
    {
        IList<SearchItem> Items { get; }
    }

    internal sealed class AppIndex : IAppIndex, IDisposable
    {
        private readonly string[] _roots;
        private readonly object _gate = new object();
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly Timer _refreshTimer;
        private IList<SearchItem> _items = new List<SearchItem>();
        private bool _disposed;
        public event EventHandler Changed;
        public IList<SearchItem> Items { get { lock (_gate) return new List<SearchItem>(_items); } }

        public AppIndex(IEnumerable<string> roots)
        {
            _roots = roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _refreshTimer = new Timer(delegate { Refresh(); }, null, Timeout.Infinite, Timeout.Infinite);
            foreach (string root in _roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
                    watcher.Changed += OnDirectoryChanged;
                    watcher.Created += OnDirectoryChanged;
                    watcher.Deleted += OnDirectoryChanged;
                    watcher.Renamed += OnDirectoryChanged;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static string[] DefaultRoots()
        {
            return new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
        }

        public void Refresh()
        {
            var found = new Dictionary<string, SearchItem>(StringComparer.OrdinalIgnoreCase);
            using (var shortcuts = new ShortcutResolver()) foreach (string root in _roots) Scan(root, found, shortcuts);
            var items = found.Values.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            lock (_gate)
            {
                if (_disposed) return;
                _items = items;
            }
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static void Scan(string directory, IDictionary<string, SearchItem> found, ShortcutResolver shortcuts, bool includeExecutables = true)
        {
            try
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension != ".lnk" && extension != ".url" && extension != ".appref-ms" && extension != ".exe") continue;
                    // Nested executables are often project/build artifacts, not app entries.
                    if (extension == ".exe" && !includeExecutables) continue;
                    string iconPath, arguments;
                    string identity = shortcuts.Identity(file, out iconPath, out arguments);
                    ResultKind kind = extension == ".url" ? ResultKind.Url : ResultKind.Application;
                    if (extension == ".lnk")
                    {
                        string target = Environment.ExpandEnvironmentVariables(iconPath);
                        string targetExtension = Path.GetExtension(target).ToLowerInvariant();
                        bool executable = targetExtension == ".exe" || targetExtension == ".com" || targetExtension == ".bat" || targetExtension == ".cmd" || targetExtension == ".appref-ms";
                        kind = Directory.Exists(target) ? ResultKind.Folder
                            : executable && File.Exists(target) ? ResultKind.Application : ResultKind.File;
                        if (string.Equals(Path.GetFileName(target), "explorer.exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(arguments)
                            && arguments.IndexOf("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            string location = Environment.ExpandEnvironmentVariables(arguments.Trim().Trim('"'));
                            kind = File.Exists(location) ? ResultKind.File : ResultKind.Folder;
                        }
                        if (string.Equals(Path.GetFileName(target), "control.exe", StringComparison.OrdinalIgnoreCase)
                            && arguments.IndexOf("Microsoft.AdministrativeTools", StringComparison.OrdinalIgnoreCase) >= 0)
                            kind = ResultKind.Folder;
                        if (string.Equals(Path.GetFileName(target), "hh.exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(arguments))
                            kind = ResultKind.File;
                    }
                    found[identity] = new SearchItem { Name = Path.GetFileNameWithoutExtension(file), Path = file, Detail = directory, IconKey = iconPath, Kind = kind };
                }
                foreach (string child in Directory.GetDirectories(directory))
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) Scan(child, found, shortcuts, false);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public IList<SearchItem> Search(string query, int limit)
        {
            return Items.Select(i => new { Item = i, Score = FuzzyMatcher.Score(query, i.Name) })
                .Where(i => i.Score > 0).OrderByDescending(i => i.Score).ThenBy(i => i.Item.Name)
                .Take(Math.Max(0, limit)).Select(i => i.Item).ToList();
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs args)
        {
            lock (_gate) { if (!_disposed) _refreshTimer.Change(500, Timeout.Infinite); }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                foreach (var watcher in _watchers) watcher.Dispose();
                _refreshTimer.Dispose();
            }
        }
    }
}
