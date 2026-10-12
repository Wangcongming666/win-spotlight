using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace FocusSearch
{
    internal sealed class ShortcutResolver : IDisposable
    {
        private object _shell;
        public ShortcutResolver()
        {
            try { var type = Type.GetTypeFromProgID("WScript.Shell"); if (type != null) _shell = Activator.CreateInstance(type); } catch (Exception) { }
        }
        public string Identity(string path, out string iconPath, out string arguments)
        {
            iconPath = path;
            string target, working;
            if (!TryResolve(path, out target, out arguments, out working)) return path;
            iconPath = target;
            return target + "|" + arguments + "|" + working;
        }
        public bool TryResolve(string path, out string target, out string arguments, out string working)
        {
            target = arguments = working = string.Empty;
            if (_shell == null || !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)) return false;
            object shortcut = null;
            try
            {
                shortcut = _shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, _shell, new object[] { path });
                target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                arguments = Convert.ToString(shortcut.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null));
                working = Convert.ToString(shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, shortcut, null));
                return !string.IsNullOrWhiteSpace(target);
            }
            catch (Exception) { return false; }
            finally { if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); }
        }
        public void Dispose() { if (_shell != null && Marshal.IsComObject(_shell)) Marshal.FinalReleaseComObject(_shell); _shell = null; }
    }
}
