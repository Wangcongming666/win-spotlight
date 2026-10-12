using System;
using System.Collections.Generic;
namespace FocusSearch.Tests
{
    internal static class HotkeyTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(ConflictingChangesKeepPreviousHotkey); tests.Add(UncommittedChangeKeepsOldRegistration); }
        private static void UncommittedChangeKeepsOldRegistration()
        {
            var active = new Dictionary<int, uint>();
            using (var manager = new HotkeyCoordinator(delegate(int id, HotkeySpec key) { active[id] = key.VirtualKey; return true; }, delegate(int id) { active.Remove(id); }))
            {
                manager.Apply(new AppSettings());
                using (var change = manager.Prepare(new AppSettings { SearchHotkey = new HotkeySpec(1, 75) }))
                {
                    TestRunner.Equal(true, active.ContainsValue(32), "Old hotkey must remain held until persistence succeeds.");
                    TestRunner.Equal(true, active.ContainsValue(75), "New hotkey must be reserved before commit.");
                }
                TestRunner.Equal(1, active.Count, "Failed persistence rollback must release only new keys.");
                TestRunner.Equal(true, active.ContainsValue(32), "Old registration must never need re-registration.");
            }
        }
        private static void ConflictingChangesKeepPreviousHotkey()
        {
            var active = new Dictionary<int, uint>();
            var manager = new HotkeyCoordinator(delegate(int id, HotkeySpec key) { if (key.VirtualKey == 75) return false; active[id] = key.VirtualKey; return true; }, delegate(int id) { active.Remove(id); });
            TestRunner.Equal(true, manager.Apply(new AppSettings()), "Initial hotkey registration must succeed.");
            var next = new AppSettings { SearchHotkey = new HotkeySpec(1, 75) };
            TestRunner.Equal(false, manager.Apply(next), "Conflict must be rejected.");
            TestRunner.Equal(1, active.Count, "Old registration must stay active after conflict.");
            TestRunner.Equal(true, active.ContainsValue(32), "Alt+Space must remain registered.");
            manager.Dispose(); TestRunner.Equal(0, active.Count, "Disposal must release all registrations.");
        }
    }
}
