using System;
using System.Drawing;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class SettingsWindow : Form
    {
        public AppSettings Value { get; private set; }
        public SettingsWindow(AppSettings current)
        {
            Text = "聚焦搜索设置"; ClientSize = new Size(480, 360); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 10);
            var combined = Check("搜索框与应用程序一起显示", current.CombinedMode, 28);
            var grid = Check("启用应用程序网格", current.AppGridEnabled, 68);
            var files = Check("搜索 Windows 已索引文件", current.FileSearchEnabled, 108);
            var startup = Check("登录 Windows 时自动启动", current.StartWithWindows, 148);
            var hide = Check("失去焦点时自动收起", current.HideOnDeactivate, 188);
            var search = HotkeyBox("搜索快捷键", current.SearchHotkey, 235);
            var apps = HotkeyBox("独立应用页", current.GridHotkey, 273);
            var save = new Button { Text = "保存", Left = 370, Top = 318, Width = 85 };
            save.Click += delegate {
                Value = new AppSettings { CombinedMode = combined.Checked, AppGridEnabled = grid.Checked, FileSearchEnabled = files.Checked, StartWithWindows = startup.Checked, HideOnDeactivate = hide.Checked, SearchHotkey = ((HotkeySpec)search.Tag).Clone(), GridHotkey = ((HotkeySpec)apps.Tag).Clone() };
                DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(save);
        }
        private CheckBox Check(string text, bool value, int top)
        { var control = new CheckBox { Text = text, Checked = value, Left = 25, Top = top, Width = 420 }; Controls.Add(control); return control; }
        private TextBox HotkeyBox(string label, HotkeySpec hotkey, int top)
        {
            Controls.Add(new Label { Text = label, Left = 25, Top = top + 3, Width = 130 });
            var box = new TextBox { Left = 170, Top = top, Width = 275, ReadOnly = true, Tag = hotkey.Clone(), Text = Format(hotkey) };
            box.KeyDown += delegate(object sender, KeyEventArgs args) {
                args.SuppressKeyPress = true;
                if (args.KeyCode == Keys.ShiftKey || args.KeyCode == Keys.ControlKey || args.KeyCode == Keys.Menu) return;
                uint modifiers = (args.Alt ? 1u : 0u) | (args.Control ? 2u : 0u) | (args.Shift ? 4u : 0u);
                if (modifiers == 0) return;
                var next = new HotkeySpec(modifiers, (uint)args.KeyCode); box.Tag = next; box.Text = Format(next);
            };
            Controls.Add(box); return box;
        }
        private static string Format(HotkeySpec key)
        { return ((key.Modifiers & 1) != 0 ? "Alt + " : "") + ((key.Modifiers & 2) != 0 ? "Ctrl + " : "") + ((key.Modifiers & 4) != 0 ? "Shift + " : "") + ((Keys)key.VirtualKey).ToString(); }
    }
}
