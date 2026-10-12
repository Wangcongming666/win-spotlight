using System;
using System.Collections.Generic;

namespace FocusSearch
{
    internal static class FileExtensionQuery
    {
        private static readonly HashSet<string> Common = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "md", "markdown", "txt", "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "csv", "json", "yaml", "yml", "png", "jpg", "jpeg", "gif", "webp", "zip", "rar", "7z", "mp3", "mp4"
        };
        public static bool TryParse(string query, out string extension)
        {
            extension = null;
            string value = (query ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormKC);
            bool explicitExtension = value.StartsWith("*.", StringComparison.Ordinal) || value.StartsWith(".", StringComparison.Ordinal);
            if (value.StartsWith("*.", StringComparison.Ordinal)) value = value.Substring(2);
            else if (value.StartsWith(".", StringComparison.Ordinal)) value = value.Substring(1);
            if (!explicitExtension && !Common.Contains(value)) return false;
            if (value.Length == 0 || value.Length > 16) return false;
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            extension = "." + value.ToLowerInvariant();
            return true;
        }
        public static bool Matches(SearchItem item, string extension)
        {
            return item.Kind == ResultKind.File && (EndsWith(item.Path, extension) || EndsWith(item.Name, extension)
                || (EndsWith(item.Path, ".lnk") && EndsWith(item.IconKey, extension)));
        }
        private static bool EndsWith(string value, string extension)
        { return !string.IsNullOrEmpty(value) && value.EndsWith(extension, StringComparison.OrdinalIgnoreCase); }
    }
}
