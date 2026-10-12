using System;
using System.Runtime.InteropServices;
namespace FocusSearch
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern int SetWindowCompositionAttribute(IntPtr window, ref CompositionData data);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, string text);
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
        [StructLayout(LayoutKind.Sequential)] internal struct AccentPolicy { public int State, Flags, Color, Animation; }
        [StructLayout(LayoutKind.Sequential)] internal struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct ShellFileInfo
        { public IntPtr Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName; }
    }
}
