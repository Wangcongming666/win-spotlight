using System;

namespace FocusSearch
{
    internal static class FuzzyMatcher
    {
        public static int Score(string query, string candidate)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(candidate))
            {
                return 0;
            }

            query = query.Trim().Normalize(System.Text.NormalizationForm.FormKC);
            candidate = candidate.Trim().Normalize(System.Text.NormalizationForm.FormKC);

            if (string.Equals(query, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return 10000;
            }

            if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 8000 - Math.Min(1000, candidate.Length - query.Length);
            }

            int substringIndex = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (substringIndex >= 0)
            {
                return 6000 - Math.Min(1000, substringIndex * 8 + candidate.Length - query.Length);
            }

            int aliasScore = SearchAliases.Score(query, candidate);
            int queryIndex = 0;
            int firstMatch = -1;
            int lastMatch = -1;
            for (int candidateIndex = 0; candidateIndex < candidate.Length && queryIndex < query.Length; candidateIndex++)
            {
                if (char.ToUpperInvariant(candidate[candidateIndex]) == char.ToUpperInvariant(query[queryIndex]))
                {
                    if (firstMatch < 0)
                    {
                        firstMatch = candidateIndex;
                    }

                    lastMatch = candidateIndex;
                    queryIndex++;
                }
            }

            if (queryIndex != query.Length)
            {
                return aliasScore;
            }

            int span = lastMatch - firstMatch + 1;
            int gaps = span - query.Length;
            return Math.Max(aliasScore, Math.Max(1, 3000 - firstMatch * 10 - gaps * 20 - (candidate.Length - query.Length)));
        }
    }
}
