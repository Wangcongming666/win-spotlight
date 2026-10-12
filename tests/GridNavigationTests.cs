using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace FocusSearch.Tests
{
    internal static class GridNavigationTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(GridMovementClampsAtBoundaries); }
        private static void GridMovementClampsAtBoundaries()
        {
            TestRunner.Equal(5, GridNavigation.Move(0, 17, 5, 15, Keys.Down, true), "Down must move one grid row.");
            TestRunner.Equal(16, GridNavigation.Move(10, 17, 5, 15, Keys.PageDown, true), "Last page must clamp to final item.");
            TestRunner.Equal(0, GridNavigation.Move(0, 17, 5, 15, Keys.Left, true), "Left must not underflow.");
            TestRunner.Equal(0, GridNavigation.Move(0, 0, 5, 15, Keys.Down, true), "Empty grid must remain safe.");
            TestRunner.Equal(1, GridNavigation.Move(0, 17, 5, 15, Keys.Down, false), "List mode moves one item.");
        }
    }
}
