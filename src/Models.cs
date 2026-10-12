using System;

namespace FocusSearch
{
    internal enum SearchScope { All, Applications, Folders, Files }
    internal enum ResultKind
    {
        Application,
        File,
        Folder,
        Url,
        Status
    }

    internal sealed class SearchItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Detail { get; set; }
        public string IconKey { get; set; }
        public ResultKind Kind { get; set; }
        public int Score { get; set; }

        public SearchItem()
        {
            Name = string.Empty;
            Path = string.Empty;
            Detail = string.Empty;
            IconKey = string.Empty;
        }
    }

    internal sealed class HotkeySpec
    {
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }

        public HotkeySpec()
        {
        }

        public HotkeySpec(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public HotkeySpec Clone()
        {
            return new HotkeySpec(Modifiers, VirtualKey);
        }
    }

    internal sealed class AppSettings
    {
        public bool CombinedMode { get; set; }
        public bool AppGridEnabled { get; set; }
        public bool FileSearchEnabled { get; set; }
        public bool StartWithWindows { get; set; }
        public bool HideOnDeactivate { get; set; }
        public bool AnimationsEnabled { get; set; }
        public HotkeySpec SearchHotkey { get; set; }
        public HotkeySpec GridHotkey { get; set; }

        public AppSettings()
        {
            CombinedMode = true;
            AppGridEnabled = true;
            FileSearchEnabled = true;
            HideOnDeactivate = true;
            AnimationsEnabled = true;
            SearchHotkey = new HotkeySpec(0x0001, 0x20);
            GridHotkey = new HotkeySpec(0x0001, 0x41);
        }
    }

    internal sealed class UsageRecord
    {
        public string Path { get; set; }
        public int LaunchCount { get; set; }
        public DateTime LastLaunchedUtc { get; set; }

        public UsageRecord()
        {
            Path = string.Empty;
        }
    }
}
