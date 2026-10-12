using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
namespace FocusSearch
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "Local\\FocusSearch.Instance"))
            using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FocusSearch.Wake"))
            using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FocusSearch.Quit"))
            {
                if (Array.IndexOf(args, "--quit") >= 0) { quit.Set(); return; }
                bool acquired;
                try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { wake.Set(); return; }
                try
                {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayContext(wake, quit, Array.IndexOf(args, "--hidden") >= 0));
                }
                catch (Exception error) { MessageBox.Show(error.Message, "聚焦搜索启动失败"); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public Action<int> Pressed;
        public HotkeyWindow() { CreateHandle(new CreateParams { Caption = "FocusSearch.Hotkeys", Parent = new IntPtr(-3) }); }
        protected override void WndProc(ref Message message) { if (message.Msg == 0x0312 && Pressed != null) Pressed(message.WParam.ToInt32()); base.WndProc(ref message); }
        public void Dispose() { NativeMethods.UnregisterHotKey(Handle, 1); NativeMethods.UnregisterHotKey(Handle, 2); DestroyHandle(); }
    }
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly SettingsStore _store;
        private AppSettings _settings;
        private readonly AppIndex _index;
        private readonly SearchEngine _engine;
        private readonly LauncherWindow _launcher;
        private readonly HotkeyWindow _hotkeys;
        private readonly HotkeyCoordinator _coordinator;
        private readonly NotifyIcon _tray;
        private readonly RegisteredWaitHandle _wakeRegistration;
        private readonly RegisteredWaitHandle _quitRegistration;
        public TrayContext(EventWaitHandle wake, EventWaitHandle quit, bool hidden)
        {
            _store = new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusSearch"));
            _settings = _store.LoadSettings(); _index = new AppIndex(AppIndex.DefaultRoots());
            _engine = new SearchEngine(_index, new WindowsSearchProvider(), _store.LoadHistory());
            _launcher = new LauncherWindow(_index, _engine, _settings);
            _launcher.CreateControl(); var handle = _launcher.Handle;
            _launcher.LaunchRequested = Launch;
            _hotkeys = new HotkeyWindow();
            _coordinator = new HotkeyCoordinator(delegate(int id, HotkeySpec key) { return NativeMethods.RegisterHotKey(_hotkeys.Handle, id, key.Modifiers | 0x4000, key.VirtualKey); }, delegate(int id) { NativeMethods.UnregisterHotKey(_hotkeys.Handle, id); });
            _hotkeys.Pressed = delegate(int id) { Toggle(_coordinator.IsGrid(id)); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示搜索", null, delegate { Toggle(false); });
            var appsMenu = menu.Items.Add("应用程序", null, delegate { Toggle(true); });
            menu.Opening += delegate { appsMenu.Enabled = _settings.AppGridEnabled; };
            menu.Items.Add("设置…", null, delegate { OpenSettings(); });
            menu.Items.Add("关于", null, delegate { MessageBox.Show("聚焦搜索\n原生 Windows 应用启动与文件搜索工具\nAlt + Space 搜索 · Alt + A 应用程序", "聚焦搜索"); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitThread(); });
            _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "聚焦搜索 · Alt + Space", ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += delegate { Toggle(false); };
            if (!_coordinator.Apply(_settings)) _tray.ShowBalloonTip(5000, "快捷键注册失败", "可从托盘打开设置修改快捷键。", ToolTipIcon.Warning);
            _wakeRegistration = ThreadPool.RegisterWaitForSingleObject(wake, delegate { try { _launcher.BeginInvoke((Action)delegate { Show(false); }); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);
            _quitRegistration = ThreadPool.RegisterWaitForSingleObject(quit, delegate { try { _launcher.BeginInvoke((Action)delegate { ExitThread(); }); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);
            ThreadPool.QueueUserWorkItem(delegate { _index.Refresh(); });
            if (!hidden) Show(false);
        }
        private void Toggle(bool grid) { if (_launcher.Visible) _launcher.HideLauncher(); else Show(grid); }
        private void Show(bool grid) { _launcher.ShowLauncher(grid || (_settings.CombinedMode && _settings.AppGridEnabled), grid); }
        private void Launch(SearchItem item)
        {
            try { var launched = Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); if (launched != null) launched.Dispose(); }
            catch (Exception error) { _tray.ShowBalloonTip(4000, "无法打开", error.Message, ToolTipIcon.Warning); return; }
            _launcher.HideLauncher(); _engine.RecordLaunch(item.Path);
            try { _store.RecordLaunch(item.Path); }
            catch (Exception error) { _tray.ShowBalloonTip(4000, "历史记录未保存", error.Message, ToolTipIcon.Warning); }
        }
        private void OpenSettings()
        {
            _launcher.HideLauncher();
            using (var dialog = new SettingsWindow(_settings))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                var next = dialog.Value;
                using (var change = _coordinator.Prepare(next))
                {
                    if (change == null) { MessageBox.Show("快捷键已被占用，请选择其他组合。原设置已保留。", "无法保存"); return; }
                    try
                    {
                        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                        {
                            object previous = key.GetValue("FocusSearch");
                            RegistryValueKind previousKind = previous == null ? RegistryValueKind.String : key.GetValueKind("FocusSearch");
                            try
                            {
                                if (next.StartWithWindows) key.SetValue("FocusSearch", "\"" + Application.ExecutablePath + "\" --hidden"); else key.DeleteValue("FocusSearch", false);
                                _store.SaveSettings(next);
                            }
                            catch
                            {
                                if (previous == null) key.DeleteValue("FocusSearch", false); else key.SetValue("FocusSearch", previous, previousKind);
                                throw;
                            }
                        }
                        change.Commit(); _settings = next; _launcher.Settings = next;
                    }
                    catch (Exception error) { MessageBox.Show(error.Message + "\n原快捷键已保留。", "设置保存失败"); }
                }
            }
        }
        protected override void ExitThreadCore()
        {
            _quitRegistration.Unregister(null); _wakeRegistration.Unregister(null); _tray.Visible = false; _tray.Dispose(); _coordinator.Dispose(); _hotkeys.Dispose(); _launcher.Dispose(); _index.Dispose(); base.ExitThreadCore();
        }
    }
}
