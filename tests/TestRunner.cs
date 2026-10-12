using System;
using System.Collections.Generic;

namespace FocusSearch.Tests
{
    internal static class TestRunner
    {
        private static int _passed;
        private static int _failed;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--ui-smoke")
            {
                UiSmoke.Run(); return;
            }
            if (args.Length > 0 && args[0] == "--perf-smoke") { PerformanceSmoke.Run(); return; }
            var tests = new List<Action>();
            FuzzyMatcherTests.Register(tests);
            SettingsStoreTests.Register(tests);
            AppIndexTests.Register(tests);
            SearchEngineTests.Register(tests);
            HotkeyTests.Register(tests);
            LocalFileTests.Register(tests);
            GridNavigationTests.Register(tests);
            MotionTests.Register(tests);
            SearchInputTests.Register(tests);
            IconCacheTests.Register(tests);

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
