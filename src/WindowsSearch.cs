using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Threading;
namespace FocusSearch
{
    internal interface IFileSearchProvider { IList<SearchItem> Search(string query, int limit, CancellationToken token); }
    internal interface IFileExtensionSearchProvider { IList<SearchItem> SearchByExtension(string extension, int limit, CancellationToken token); }
    internal sealed class WindowsSearchProvider : IFileSearchProvider, IFileExtensionSearchProvider
    {
        public string Status { get; private set; }
        public string Diagnostic { get; private set; }
        private readonly LocalFileProvider _fallback = LocalFileProvider.ForUser();
        public static string EscapeContainsValue(string value)
        {
            return value.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }
        public IList<SearchItem> Search(string query, int limit)
        { return Search(query, limit, CancellationToken.None); }
        public IList<SearchItem> Search(string query, int limit, CancellationToken token)
        { return SearchCore(query, limit, token, null); }
        public IList<SearchItem> SearchByExtension(string extension, int limit, CancellationToken token)
        { return SearchCore(extension, limit, token, extension); }
        private IList<SearchItem> SearchCore(string query, int limit, CancellationToken token, string extension)
        {
            token.ThrowIfCancellationRequested();
            var items = new List<SearchItem>();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2 || limit <= 0) return items;
            try
            {
                using (var connection = new OleDbConnection("Provider=Search.CollatorDSO.1;Connect Timeout=2;Extended Properties='Application=Windows';"))
                {
                    connection.Open();
                    token.ThrowIfCancellationRequested();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 2;
                        string condition = extension == null ? "System.FileName LIKE '%" + EscapeContainsValue(query) + "%' OR System.ItemPathDisplay LIKE '%" + EscapeContainsValue(query) + "%'"
                            : "System.FileName LIKE '%" + EscapeContainsValue(extension) + "'";
                        command.CommandText = "SELECT TOP " + Math.Min(limit, 64) + " System.ItemNameDisplay, System.ItemPathDisplay FROM SYSTEMINDEX WHERE " + condition;
                        using (token.Register(delegate { try { command.Cancel(); } catch (Exception) { } }))
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                token.ThrowIfCancellationRequested();
                                string path = Convert.ToString(reader.GetValue(1));
                                if (string.IsNullOrWhiteSpace(path)) continue;
                                if (extension != null && Directory.Exists(path)) continue;
                                items.Add(new SearchItem { Name = Convert.ToString(reader.GetValue(0)), Path = path, Detail = path, IconKey = path, Kind = Directory.Exists(path) ? ResultKind.Folder : ResultKind.File });
                            }
                        }
                    }
                }
                Status = string.Empty;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { token.ThrowIfCancellationRequested(); Diagnostic = error.ToString(); Status = "文件搜索：桌面 / 文档 / 下载（有限范围）"; return extension == null ? _fallback.Search(query, limit, token) : _fallback.SearchByExtension(extension, limit, token); }
            return items;
        }
    }
}
