using System;
using System.Runtime.InteropServices;
namespace FocusSearch
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
        [DllImport("shell32.dll", EntryPoint="SHGetImageList")] internal static extern int SHGetImageList(int kind, ref Guid iid, out IImageList list);
        [DllImport("user32.dll")] internal static extern int SetWindowCompositionAttribute(IntPtr window, ref CompositionData data);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [StructLayout(LayoutKind.Sequential)] internal struct Margins { public int Left, Right, Top, Bottom; }
        [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
        [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, string text);
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
        [StructLayout(LayoutKind.Sequential)] internal struct AccentPolicy { public int State, Flags, Color, Animation; }
        [StructLayout(LayoutKind.Sequential)] internal struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct ShellFileInfo
        { public IntPtr Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName; }
    }
    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IImageList
    {
        [PreserveSig] int Add(IntPtr bitmap, IntPtr mask, out int index);
        [PreserveSig] int ReplaceIcon(int index, IntPtr icon, out int output);
        [PreserveSig] int SetOverlayImage(int image, int overlay);
        [PreserveSig] int Replace(int index, IntPtr bitmap, IntPtr mask);
        [PreserveSig] int AddMasked(IntPtr bitmap, int mask, out int index);
        [PreserveSig] int Draw(IntPtr parameters);
        [PreserveSig] int Remove(int index);
        [PreserveSig] int GetIcon(int index, int flags, out IntPtr icon);
    }
}
