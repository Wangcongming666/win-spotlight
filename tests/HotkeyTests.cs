using System;
using System.Collections.Generic;
using System.Linq;
namespace FocusSearch.Tests
{
    internal static class HotkeyTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(CombinedModeRegistersGridHotkey); tests.Add(ConflictingChangesKeepPreviousHotkey); tests.Add(UncommittedChangeKeepsOldRegistration); }
        private static void CombinedModeRegistersGridHotkey()
        {
            var active = new Dictionary<int, uint>();
            using (var manager = new HotkeyCoordinator(delegate(int id, HotkeySpec key) { active[id] = key.VirtualKey; return true; }, delegate(int id) { active.Remove(id); }))
            {
                TestRunner.Equal(true, manager.Apply(new AppSettings { CombinedMode = true, AppGridEnabled = true }), "Combined mode hotkeys must register successfully.");
                TestRunner.Equal(2, active.Count, "Alt+Space and Alt+A must both remain active in combined mode.");
                int gridId = active.Single(item => item.Value == 0x41).Key;
                TestRunner.Equal(true, manager.IsGrid(gridId), "Alt+A must be routed to the independent application grid.");
            }
        }
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
                TestRunner.Equal(2, active.Count, "Failed persistence rollback must release only new keys.");
                TestRunner.Equal(true, active.ContainsValue(32), "Old registration must never need re-registration.");
                TestRunner.Equal(true, active.ContainsValue(65), "Grid registration must remain active after rollback.");
            }
        }
        private static void ConflictingChangesKeepPreviousHotkey()
        {
            var active = new Dictionary<int, uint>();
            var manager = new HotkeyCoordinator(delegate(int id, HotkeySpec key) { if (key.VirtualKey == 75) return false; active[id] = key.VirtualKey; return true; }, delegate(int id) { active.Remove(id); });
            TestRunner.Equal(true, manager.Apply(new AppSettings()), "Initial hotkey registration must succeed.");
            var next = new AppSettings { SearchHotkey = new HotkeySpec(1, 75) };
            TestRunner.Equal(false, manager.Apply(next), "Conflict must be rejected.");
            TestRunner.Equal(2, active.Count, "Old registrations must stay active after conflict.");
            TestRunner.Equal(true, active.ContainsValue(32), "Alt+Space must remain registered.");
            TestRunner.Equal(true, active.ContainsValue(65), "Alt+A must remain registered.");
            manager.Dispose(); TestRunner.Equal(0, active.Count, "Disposal must release all registrations.");
        }
    }
}
