using System;
using System.Collections.Generic;

namespace FocusSearch.Tests
{
    internal static class FuzzyMatcherTests
    {
        public static void Register(ICollection<Action> tests)
        {
            tests.Add(ExactMatchOutranksPrefix);
            tests.Add(PrefixOutranksSubstring);
            tests.Add(SubstringOutranksSparseMatch);
            tests.Add(SparseMatchOutranksNoMatch);
            tests.Add(MatchingIsOrdinalCaseInsensitive);
            tests.Add(ChineseCharactersMatchInOrder);
        }

        private static void ExactMatchOutranksPrefix()
        {
            TestRunner.Greater(FuzzyMatcher.Score("note", "note"), FuzzyMatcher.Score("note", "notepad"), "Exact match must rank first.");
        }

        private static void PrefixOutranksSubstring()
        {
            TestRunner.Greater(FuzzyMatcher.Score("pad", "pad editor"), FuzzyMatcher.Score("pad", "notepad"), "Prefix must outrank substring.");
        }

        private static void SubstringOutranksSparseMatch()
        {
            TestRunner.Greater(FuzzyMatcher.Score("note", "my notepad"), FuzzyMatcher.Score("note", "new online text editor"), "Contiguous substring must outrank sparse match.");
        }

        private static void SparseMatchOutranksNoMatch()
        {
            TestRunner.Greater(FuzzyMatcher.Score("npe", "new photo editor"), FuzzyMatcher.Score("npe", "calculator"), "Sparse ordered characters must outrank no match.");
        }

        private static void MatchingIsOrdinalCaseInsensitive()
        {
            TestRunner.Equal(FuzzyMatcher.Score("NOTE", "notepad"), FuzzyMatcher.Score("note", "NOTEPAD"), "Case must not affect score.");
        }

        private static void ChineseCharactersMatchInOrder()
        {
            TestRunner.Greater(FuzzyMatcher.Score("聚搜", "聚焦搜索"), 0, "Chinese characters must support sparse ordered matching.");
        }
    }
}
