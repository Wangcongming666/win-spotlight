using System;
using System.Collections.Generic;
namespace FocusSearch.Tests
{
    internal static class MotionTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(EasingEndsExactlyAndDecelerates); }
        private static void EasingEndsExactlyAndDecelerates()
        {
            TestRunner.Equal(0.0, Motion.EaseOut(-1), "Animation must start at zero.");
            TestRunner.Equal(1.0, Motion.EaseOut(2), "Animation must finish exactly at one.");
            TestRunner.Equal(0.875, Motion.EaseOut(0.5), "Arrival must cover most distance early and decelerate.");
        }
    }
}
