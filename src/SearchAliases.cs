using System;
using System.Collections.Concurrent;
using System.Text;

namespace FocusSearch
{
    internal static class SearchAliases
    {
        private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();
        private static readonly Encoding Chinese = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        // GB2312 level-one characters are ordered by Mandarin pronunciation.
        private static readonly int[] Boundaries = { -20319, -20283, -19775, -19218, -18710, -18526, -18239, -17922, -17417, -16474, -16212, -15640, -15165, -14922, -14914, -14630, -14149, -14090, -13318, -12838, -12556, -11847, -11055 };
        private const string Letters = "abcdefghjklmnopqrstwxyz";
        public static string Initials(string name)
        {
            if (Cache.Count > 4096) Cache.Clear();
            return Cache.GetOrAdd(name, Build);
        }
        private static string Build(string name)
        {
            var text = new StringBuilder(); bool start = true;
            foreach (char ch in name.Normalize(NormalizationForm.FormKC))
            {
                if (ch >= 0x4e00 && ch <= 0x9fff)
                {
                    try
                    {
                        byte[] bytes = Chinese.GetBytes(new[] { ch });
                        int code = bytes.Length == 2 ? bytes[0] * 256 + bytes[1] - 65536 : 0;
                        char initial = ch;
                        if (code >= Boundaries[0] && code <= -10247)
                            for (int i = Boundaries.Length - 1; i >= 0; i--) if (code >= Boundaries[i]) { initial = Letters[i]; break; }
                        text.Append(initial);
                    }
                    catch (EncoderFallbackException) { text.Append(ch); }
                    start = true;
                }
                else if (char.IsLetterOrDigit(ch))
                {
                    if (start || (char.IsUpper(ch) && text.Length > 0)) text.Append(char.ToLowerInvariant(ch));
                    start = false;
                }
                else start = true;
            }
            return text.ToString();
        }
        public static int Score(string query, string candidate)
        {
            if (query.Length < 2) return 0;
            foreach (char ch in query) if (!char.IsLetterOrDigit(ch) || ch > 127) return 0;
            string alias = Initials(candidate);
            if (string.Equals(query, alias, StringComparison.OrdinalIgnoreCase)) return 5600;
            if (alias.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 5200 - Math.Min(500, alias.Length - query.Length);
            int position = alias.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            return position >= 0 ? 3800 - Math.Min(500, position * 8 + alias.Length) : 0;
        }
    }
}
