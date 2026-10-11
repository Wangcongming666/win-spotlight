using System;
using System.Collections.Generic;

namespace FocusSearch.Tests
{
    internal static class TestRunner
    {
        private static int _passed;
        private static int _failed;

        private static void Main()
        {
            var tests = new List<Action>();
            FuzzyMatcherTests.Register(tests);
            SettingsStoreTests.Register(tests);

            foreach (var test in tests)
            {
                try
                {
                    test();
                    _passed++;
                    Console.WriteLine("PASS " + test.Method.Name);
                }
                catch (Exception exception)
                {
                    _failed++;
                    Console.WriteLine("FAIL " + test.Method.Name + ": " + exception.Message);
                }
            }

            Console.WriteLine("RESULT " + _passed + " passed, " + _failed + " failed");
            Environment.ExitCode = _failed == 0 ? 0 : 1;
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + ", actual: " + actual);
            }
        }

        public static void Greater(int higher, int lower, string message)
        {
            if (higher <= lower)
            {
                throw new InvalidOperationException(message + " Expected " + higher + " > " + lower);
            }
        }
    }
}
