using System;
using System.IO;

namespace FocusSearch
{
    internal static class WindowsShellEnvironment
    {
        public static void Prepare()
        {
            // Some launchers omit these inherited variables. Shell COM registrations
            // use them to locate Windows modules; repair only this process's omissions.
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            SetIfMissing("SystemRoot", windows);
            SetIfMissing("windir", windows);
            SetIfMissing("SystemDrive", Path.GetPathRoot(windows).TrimEnd(Path.DirectorySeparatorChar));
            SetIfMissing("ComSpec", Path.Combine(Environment.SystemDirectory, "cmd.exe"));
        }

        private static void SetIfMissing(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }
    }
}
