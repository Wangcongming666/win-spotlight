using System;
using System.Drawing;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class SettingsWindow : DesktopDialog
    {
        public AppSettings Value { get; private set; }
        public SettingsWindow(AppSettings current) : base("聚焦搜索设置", new Size(500, 560))
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 16, 24, 12) };
            var body = Stack();
            var title = Copy("按你的习惯使用聚焦搜索", false);
            title.Font = OwnFont(16, FontStyle.Bold); body.Controls.Add(title);
            body.Controls.Add(Copy("调整显示方式、搜索范围与唤起快捷键。", true));
            AddHeading(body, "显示与交互");
            var combined = Check(body, "CombinedMode", "搜索框与应用程序一起显示", current.CombinedMode);
            var grid = Check(body, "AppGridEnabled", "启用应用程序网格", current.AppGridEnabled);
            var hide = Check(body, "HideOnDeactivate", "失去焦点时自动收起", current.HideOnDeactivate);
            var animation = Check(body, "AnimationsEnabled", "启用展开与收起动画", current.AnimationsEnabled);
            AddHeading(body, "搜索与启动");
            var files = Check(body, "FileSearchEnabled", "启用本机文件搜索", current.FileSearchEnabled);
            var startup = Check(body, "StartWithWindows", "登录 Windows 时自动启动", current.StartWithWindows);
            AddHeading(body, "快捷键");
            var search = HotkeyBox(body, "搜索面板", "SearchHotkey", current.SearchHotkey);
            var apps = HotkeyBox(body, "独立应用网格", "GridHotkey", current.GridHotkey);
            body.Controls.Add(Copy("点击输入框后按下组合键，保存后生效。", true));
            grid.CheckedChanged += delegate { combined.Enabled = grid.Checked; apps.Enabled = grid.Checked; };
            combined.Enabled = grid.Checked; apps.Enabled = grid.Checked;
            scroll.Controls.Add(body); Controls.Add(scroll);
            Footer("保存", delegate {
                Value = new AppSettings { CombinedMode = combined.Checked, AppGridEnabled = grid.Checked,
                    FileSearchEnabled = files.Checked, StartWithWindows = startup.Checked, HideOnDeactivate = hide.Checked,
                    AnimationsEnabled = animation.Checked, SearchHotkey = ((HotkeySpec)search.Tag).Clone(), GridHotkey = ((HotkeySpec)apps.Tag).Clone() };
                DialogResult = DialogResult.OK; Close();
            }, true);
        }
        private CheckBox Check(TableLayoutPanel parent, string name, string text, bool value)
        {
            var control = new CheckBox { Name = name, AccessibleName = text, Text = text, Checked = value, AutoSize = true,
                Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 2), Margin = new Padding(0, 0, 0, 2), UseVisualStyleBackColor = true };
            parent.Controls.Add(control); return control;
        }
        private TextBox HotkeyBox(TableLayoutPanel parent, string label, string name, HotkeySpec hotkey)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
            var field = new HotkeyField { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var box = new TextBox { Name = name, AccessibleName = label, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                BackColor = Color.White, ForeColor = Ink, ReadOnly = true, Tag = hotkey.Clone(), Text = Format(hotkey) };
            box.GotFocus += delegate { field.Invalidate(); }; box.LostFocus += delegate { field.Invalidate(); };
            box.KeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.KeyCode == Keys.Escape || args.KeyCode == Keys.Tab) return;
                uint modifiers = (args.Alt ? 1u : 0u) | (args.Control ? 2u : 0u) | (args.Shift ? 4u : 0u);
                if (modifiers == 0) return;
                args.SuppressKeyPress = true;
                if (args.KeyCode == Keys.ShiftKey || args.KeyCode == Keys.ControlKey || args.KeyCode == Keys.Menu) return;
                var next = new HotkeySpec(modifiers, (uint)args.KeyCode); box.Tag = next; box.Text = Format(next);
            };
            field.Controls.Add(box); row.Controls.Add(field, 1, 0); parent.Controls.Add(row); return box;
        }
        private static string Format(HotkeySpec key)
        {
            return ((key.Modifiers & 1) != 0 ? "Alt + " : "") + ((key.Modifiers & 2) != 0 ? "Ctrl + " : "")
                + ((key.Modifiers & 4) != 0 ? "Shift + " : "") + ((key.Modifiers & 8) != 0 ? "Win + " : "")
                + (key.VirtualKey == 32 ? "Space" : ((Keys)key.VirtualKey).ToString());
        }
    }
}
