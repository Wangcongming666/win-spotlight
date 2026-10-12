using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FocusSearch
{
    // Keep native menu navigation and dismissal; customize only its presentation.
    internal sealed class TrayMenu : ContextMenuStrip
    {
        private readonly Func<AppSettings> _settings;
        private readonly Font _menuFont;
        private readonly float _scale;
        public TrayMenu(Func<AppSettings> settings, Action<int> invoke)
        {
            _settings = settings;
            using (var graphics = CreateGraphics()) _scale = graphics.DpiX / 96f;
            _menuFont = new Font("Microsoft YaHei UI", 9f);
            Font = _menuFont;
            BackColor = Color.FromArgb(246, 248, 251);
            ForeColor = Color.FromArgb(43, 51, 64);
            Padding = new Padding(S(4));
            ShowImageMargin = true;
            ShowCheckMargin = false;
            Renderer = new TrayMenuRenderer(_scale);
            string[] labels = { "显示搜索", "应用程序", "设置…", "关于", "退出" };
            for (int index = 0; index < labels.Length; index++)
            {
                if (index == 4) Items.Add(new ToolStripSeparator());
                int action = index;
                var item = new ToolStripMenuItem(labels[index]) { Tag = index, AutoSize = false, Size = new Size(S(210), S(29)) };
                item.Click += delegate { invoke(action); };
                Items.Add(item);
            }
        }
        private int S(int value) { return (int)Math.Round(value * _scale); }
        protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
        {
            var settings = _settings();
            Items[1].Enabled = settings.AppGridEnabled;
            ((ToolStripMenuItem)Items[0]).ShortcutKeyDisplayString = Format(settings.SearchHotkey);
            ((ToolStripMenuItem)Items[1]).ShortcutKeyDisplayString = Format(settings.GridHotkey);
            base.OnOpening(e);
        }
        private static string Format(HotkeySpec key)
        {
            return ((key.Modifiers & 2) != 0 ? "Ctrl + " : "") + ((key.Modifiers & 1) != 0 ? "Alt + " : "")
                + ((key.Modifiers & 4) != 0 ? "Shift + " : "") + ((key.Modifiers & 8) != 0 ? "Win + " : "")
                + (key.VirtualKey == 32 ? "Space" : ((Keys)key.VirtualKey).ToString());
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0) return;
            using (var path = RoundedShapes.Path(new RectangleF(0, 0, Width, Height), S(9)))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _menuFont != null) _menuFont.Dispose();
        }
    }

    internal sealed class TrayMenuRenderer : ToolStripRenderer
    {
        private readonly float _scale;
        private static readonly Color Surface = Color.FromArgb(246, 248, 251);
        public TrayMenuRenderer(float scale) { _scale = scale; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        { e.Graphics.Clear(Surface); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedShapes.Path(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 9 * _scale))
            using (var pen = new Pen(Color.FromArgb(210, 218, 228))) e.Graphics.DrawPath(pen, path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.Item.Enabled && e.Item.Selected)
            {
                using (var path = RoundedShapes.Path(new RectangleF(2 * _scale, 1 * _scale, e.Item.Width - 4 * _scale, e.Item.Height - 2 * _scale), 7 * _scale))
                using (var brush = new SolidBrush(Color.FromArgb(225, 235, 248))) graphics.FillPath(brush, path);
            }
            if (!(e.Item.Tag is int)) return;
            int kind = (int)e.Item.Tag;
            Color ink = !e.Item.Enabled ? Color.FromArgb(151, 159, 173) : kind == 4 && e.Item.Selected
                ? Color.FromArgb(181, 70, 80) : Color.FromArgb(87, 108, 136);
            var state = graphics.Save();
            graphics.TranslateTransform(10 * _scale, (e.Item.Height - 16 * _scale) / 2);
            graphics.ScaleTransform(_scale * 16f / 18f, _scale * 16f / 18f);
            using (var pen = new Pen(ink, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (kind == 0) { graphics.DrawEllipse(pen, 2, 2, 10, 10); graphics.DrawLine(pen, 11, 11, 16, 16); }
                else if (kind == 1)
                {
                    for (int row = 0; row < 2; row++) for (int col = 0; col < 2; col++)
                        graphics.DrawRectangle(pen, 2 + col * 8, 2 + row * 8, 5, 5);
                }
                else if (kind == 2)
                {
                    graphics.DrawEllipse(pen, 4, 4, 10, 10); graphics.DrawEllipse(pen, 7, 7, 4, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4;
                        graphics.DrawLine(pen, 9 + (float)Math.Cos(angle) * 5, 9 + (float)Math.Sin(angle) * 5,
                            9 + (float)Math.Cos(angle) * 7.5f, 9 + (float)Math.Sin(angle) * 7.5f);
                    }
                }
                else if (kind == 3) { graphics.DrawEllipse(pen, 2, 2, 14, 14); graphics.DrawLine(pen, 9, 8, 9, 12); graphics.DrawEllipse(pen, 8.5f, 5, 1, 1); }
                else { graphics.DrawArc(pen, 2, 2, 14, 14, -55, 290); graphics.DrawLine(pen, 9, 1, 9, 8); }
            }
            graphics.Restore(state);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            bool shortcut = item != null && e.Text == item.ShortcutKeyDisplayString;
            Color color = !e.Item.Enabled ? Color.FromArgb(151, 159, 173) : shortcut
                ? Color.FromArgb(100, 113, 132) : Color.FromArgb(43, 51, 64);
            // Native text rectangles can follow image-margin padding rather than
            // the row center. Use the same full-height centerline as the icons.
            Rectangle bounds = e.TextRectangle;
            bounds.Y = 0;
            bounds.Height = e.Item.Height;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
                | (shortcut ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(220, 226, 235)))
                e.Graphics.DrawLine(pen, 9 * _scale, e.Item.Height / 2, e.Item.Width - 9 * _scale, e.Item.Height / 2);
        }
    }
}
