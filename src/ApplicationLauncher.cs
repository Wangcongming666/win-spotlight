using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
namespace FocusSearch
{
    internal static class ApplicationLauncher
    {
        public static Process Start(SearchItem item)
        {
            try { return Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
            catch (Win32Exception)
            {
                // Keep normal shell semantics (including shortcut flags) when
                // available. Resolve executable shortcuts only if the shell fails.
                if (item.Kind != ResultKind.Application) throw;
                string target = item.Path, arguments = string.Empty, working = string.Empty;
                if (string.Equals(Path.GetExtension(target), ".lnk", StringComparison.OrdinalIgnoreCase))
                    using (var resolver = new ShortcutResolver())
                        if (!resolver.TryResolve(item.Path, out target, out arguments, out working)) throw;
                target = Environment.ExpandEnvironmentVariables(target);
                string extension = Path.GetExtension(target);
                if ((!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".com", StringComparison.OrdinalIgnoreCase)) || !File.Exists(target)) throw;
                if (string.IsNullOrWhiteSpace(working)) working = Path.GetDirectoryName(Path.GetFullPath(target));
                else working = Environment.ExpandEnvironmentVariables(working);
                return Process.Start(new ProcessStartInfo(target, arguments) { UseShellExecute = false, WorkingDirectory = working });
            }
        }
    }
}
