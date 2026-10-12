using System;
using System.Collections.Generic;
using System.IO;

namespace FocusSearch.Tests
{
    internal static class SettingsStoreTests
    {
        public static void Register(ICollection<Action> tests)
        {
            tests.Add(DefaultSettingsUseCombinedModeAndExpectedHotkeys);
            tests.Add(SettingsRoundTripPreservesChoices);
            tests.Add(CorruptSettingsFallBackToDefaults);
            tests.Add(LaunchHistoryAccumulatesCaseInsensitivePaths);
        }

        private static void DefaultSettingsUseCombinedModeAndExpectedHotkeys()
        {
            WithTemporaryDirectory(delegate(string directory)
            {
                var settings = new SettingsStore(directory).LoadSettings();
                TestRunner.Equal(true, settings.CombinedMode, "Combined mode must be the default.");
                TestRunner.Equal((uint)0x0001, settings.SearchHotkey.Modifiers, "Search hotkey must use Alt.");
                TestRunner.Equal((uint)0x20, settings.SearchHotkey.VirtualKey, "Search hotkey must use Space.");
                TestRunner.Equal((uint)0x41, settings.GridHotkey.VirtualKey, "Grid hotkey must use A.");
            });
        }

        private static void SettingsRoundTripPreservesChoices()
        {
            WithTemporaryDirectory(delegate(string directory)
            {
                var store = new SettingsStore(directory);
                var settings = new AppSettings();
                settings.CombinedMode = false;
                settings.FileSearchEnabled = false;
                settings.AnimationsEnabled = false;
                settings.SearchHotkey = new HotkeySpec(0x0002, 0x4B);
                store.SaveSettings(settings);

                var loaded = store.LoadSettings();
                if (loaded.CombinedMode)
                {
                    throw new InvalidOperationException("Saved XML: " + File.ReadAllText(Path.Combine(directory, "settings.xml")) + " Log: " + ReadLog(directory));
                }
                TestRunner.Equal(false, loaded.CombinedMode, "Display mode must round-trip.");
                TestRunner.Equal(false, loaded.FileSearchEnabled, "File search choice must round-trip.");
                TestRunner.Equal(false, loaded.AnimationsEnabled, "Animation preference must round-trip.");
                TestRunner.Equal((uint)0x0002, loaded.SearchHotkey.Modifiers, "Hotkey modifiers must round-trip.");
                TestRunner.Equal((uint)0x4B, loaded.SearchHotkey.VirtualKey, "Hotkey key must round-trip.");
            });
        }

        private static void CorruptSettingsFallBackToDefaults()
        {
            WithTemporaryDirectory(delegate(string directory)
            {
                File.WriteAllText(Path.Combine(directory, "settings.xml"), "<settings");
                var loaded = new SettingsStore(directory).LoadSettings();
                TestRunner.Equal(true, loaded.CombinedMode, "Corrupt settings must use defaults.");
                TestRunner.Equal((uint)0x20, loaded.SearchHotkey.VirtualKey, "Corrupt settings must restore Alt+Space.");
            });
        }

        private static void LaunchHistoryAccumulatesCaseInsensitivePaths()
        {
            WithTemporaryDirectory(delegate(string directory)
            {
                var store = new SettingsStore(directory);
                store.RecordLaunch(@"C:\\Apps\\Note.exe");
                store.RecordLaunch(@"c:\\apps\\NOTE.exe");
                var history = store.LoadHistory();

                if (history.Count == 0)
                {
                    throw new InvalidOperationException("Saved XML: " + File.ReadAllText(Path.Combine(directory, "history.xml")) + " Log: " + ReadLog(directory));
                }

                TestRunner.Equal(1, history.Count, "Path casing must not create duplicate history records.");
                TestRunner.Equal(2, history[@"C:\\Apps\\Note.exe"].LaunchCount, "Launch count must accumulate.");
            });
        }

        private static string ReadLog(string directory)
        {
            string path = Path.Combine(directory, "errors.log");
            return File.Exists(path) ? File.ReadAllText(path) : "<none>";
        }

        private static void WithTemporaryDirectory(Action<string> test)
        {
            string directory = Path.Combine(Path.GetTempPath(), "FocusSearchTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                test(directory);
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch { }
            }
        }
    }
}
