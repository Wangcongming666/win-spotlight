using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace FocusSearch
{
    internal sealed class AboutWindow : DesktopDialog
    {
        private readonly Bitmap _brand;
        public AboutWindow() : base("关于聚焦搜索", new Size(420, 320))
        {
            _brand = ApplicationIcon.Render(64);
            var body = Stack(); body.Padding = new Padding(28, 24, 28, 12);
            var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 16) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.Controls.Add(new PictureBox { Image = _brand, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64), Margin = Padding.Empty }, 0, 0);
            var identity = Stack();
            var title = Copy("FocusSearch", false); title.Font = OwnFont(18, FontStyle.Bold); identity.Controls.Add(title);
            identity.Controls.Add(Copy("聚焦搜索 · Windows 桌面启动器", true)); header.Controls.Add(identity, 1, 0);
            body.Controls.Add(header);
            var version = Copy("版本 " + typeof(AboutWindow).Assembly.GetName().Version.ToString(3), true); version.Name = "Version"; body.Controls.Add(version);
            body.Controls.Add(Copy("快速查找应用与本机文件。\n轻量运行，搜索数据仅保存在本机。", false));
            var link = new LinkLabel { Text = "访问 GitHub 项目", AutoSize = true, Margin = new Padding(0, 10, 0, 0),
                LinkColor = Color.FromArgb(49, 108, 186), ActiveLinkColor = Color.FromArgb(35, 85, 153), VisitedLinkColor = Color.FromArgb(49, 108, 186) };
            link.LinkClicked += delegate {
                try { var process = Process.Start(new ProcessStartInfo("https://github.com/Wangcongming666/win-spotlight") { UseShellExecute = true }); if (process != null) process.Dispose(); }
                catch (Exception) { MessageBox.Show(this, "无法打开浏览器，请访问 github.com/Wangcongming666/win-spotlight。", "打开项目链接失败"); }
            };
            body.Controls.Add(link); Controls.Add(body);
            Footer("关闭", delegate { Close(); }, false);
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _brand.Dispose(); }
    }
}
