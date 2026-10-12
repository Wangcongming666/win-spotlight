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
            tests.Add(ChineseInitialsFindApplications);
            tests.Add(EnglishAcronymsAndFullWidthInputMatch);
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
        private static void ChineseInitialsFindApplications()
        {
            foreach (var pair in new[] { new[] { "dy", "抖音" }, new[] { "wx", "微信" }, new[] { "bdwp", "百度网盘" }, new[] { "wyy", "网易云音乐" }, new[] { "jjss", "聚焦搜索" }, new[] { "dy", "抖音电脑版" } })
                TestRunner.Greater(FuzzyMatcher.Score(pair[0], pair[1]), 0, "Chinese app must match pinyin initials: " + pair[1]);
            TestRunner.Equal(0, FuzzyMatcher.Score("dy", "微信"), "Unrelated Chinese apps must not match.");
            TestRunner.Greater(FuzzyMatcher.Score("抖音", "抖音"), FuzzyMatcher.Score("dy", "抖音"), "Literal name must outrank an alias.");
        }
        private static void EnglishAcronymsAndFullWidthInputMatch()
        {
            TestRunner.Greater(FuzzyMatcher.Score("vsc", "Visual Studio Code"), 0, "English acronym must remain searchable.");
            TestRunner.Equal(FuzzyMatcher.Score("dy", "抖音"), FuzzyMatcher.Score("ＤＹ", "抖音"), "Full-width and uppercase input must normalize.");
        }
    }
}
