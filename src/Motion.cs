using System;
namespace FocusSearch
{
    internal static class Motion
    {
        public static double EaseOut(double progress)
        { progress = Math.Max(0, Math.Min(1, progress)); return 1 - Math.Pow(1 - progress, 3); }
        public static bool SystemAllowsAnimation()
        { bool enabled; return !NativeMethods.SystemParametersInfo(0x1042, 0, out enabled, 0) || enabled; }
    }
}
