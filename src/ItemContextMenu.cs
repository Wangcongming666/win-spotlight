using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FocusSearch
{
    internal enum ItemAction { Open, ShowInFolder, CopyPath }
    internal sealed class ItemContextMenu : ContextMenuStrip
    {
        private readonly Font _menuFont;
        private readonly ToolStripMenuItem _title, _open, _folder, _copy;
        private SearchItem _target;
        private readonly Action<SearchItem, ItemAction> _invoke;
        public ItemContextMenu(Action<SearchItem, ItemAction> invoke)
        {
            _invoke = invoke; _menuFont = GlassPalette.CreateFont(10); Font = _menuFont;
            float scale; using (var graphics = CreateGraphics()) scale = graphics.DpiX / 96f;
            Padding = new Padding((int)(6 * scale)); ShowImageMargin = false; ShowCheckMargin = false;
            MinimumSize = new Size((int)(252 * scale), 0);
            Renderer = new ItemMenuRenderer(scale);
            _title = new ToolStripMenuItem { Enabled = false, Tag = "title", AutoSize = false, Size = new Size((int)(240 * scale), (int)(32 * scale)) };
            Items.Add(_title); Items.Add(new ToolStripSeparator());
            _open = Add("打开", "Enter", ItemAction.Open, scale);
            _folder = Add("打开所在位置", "", ItemAction.ShowInFolder, scale);
            Items.Add(new ToolStripSeparator()); _copy = Add("复制路径", "", ItemAction.CopyPath, scale);
        }
        private ToolStripMenuItem Add(string label, string hint, ItemAction action, float scale)
        {
            var row = new ToolStripMenuItem(label) { Tag = action, ShortcutKeyDisplayString = hint, AutoSize = false, Size = new Size((int)(240 * scale), (int)(36 * scale)) };
            row.Click += delegate { if (_target != null) _invoke(_target, action); };
            Items.Add(row); return row;
        }
        public void ShowFor(Control owner, Point location, SearchItem target)
        {
            _target = target; _title.Text = target.Name;
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : GlassPalette.Surface; ForeColor = GlassPalette.Text;
            _open.Enabled = target.Kind != ResultKind.Status && !string.IsNullOrWhiteSpace(target.Path);
            _folder.Enabled = _open.Enabled && target.Kind != ResultKind.Url;
            _copy.Enabled = !string.IsNullOrWhiteSpace(target.Path);
            Show(owner, location);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e); if (Width < 2 || Height < 2) return;
            using (var path = RoundedShapes.Path(new RectangleF(0, 0, Width, Height), Padding.Left * 2))
            { Region previous = Region; Region = new Region(path); if (previous != null) previous.Dispose(); }
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _menuFont.Dispose(); }
    }
    internal sealed class ItemMenuRenderer : ToolStripRenderer
    {
        private readonly float _scale;
        public ItemMenuRenderer(float scale) { _scale = scale; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (SystemInformation.HighContrast) { e.Graphics.Clear(SystemColors.Window); return; }
            using (var fill = new LinearGradientBrush(e.ToolStrip.ClientRectangle, e.ToolStrip.BackColor, GlassPalette.IsDark ? Color.FromArgb(31, 33, 38) : Color.FromArgb(250, 251, 253), LinearGradientMode.Vertical)) e.Graphics.FillRectangle(fill, e.ToolStrip.ClientRectangle);
        }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = RoundedShapes.Path(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 12 * _scale))
            using (var pen = new Pen(Color.FromArgb(GlassPalette.IsDark ? 60 : 180, 255, 255, 255))) e.Graphics.DrawPath(pen, shape);
            using (var shape = RoundedShapes.Path(new RectangleF(1.5f, 1.5f, e.ToolStrip.Width - 3, e.ToolStrip.Height - 3), 11 * _scale))
            using (var pen = new Pen(Color.FromArgb(25, GlassPalette.Secondary))) e.Graphics.DrawPath(pen, shape);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.Item.Enabled && e.Item.Selected)
                using (var shape = RoundedShapes.Path(new RectangleF(0, 1, e.Item.Width, e.Item.Height - 2), 7 * _scale))
                using (var fill = new SolidBrush(GlassPalette.Selection)) e.Graphics.FillPath(fill, shape);
            if (!(e.Item.Tag is ItemAction)) return;
            Color ink = !e.Item.Enabled ? GlassPalette.Secondary : e.Item.Selected ? SystemColors.HighlightText : GlassPalette.Secondary;
            var state = e.Graphics.Save(); e.Graphics.TranslateTransform(12 * _scale, (e.Item.Height - 16 * _scale) / 2); e.Graphics.ScaleTransform(_scale, _scale);
            using (var pen = new Pen(ink, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                ItemAction action = (ItemAction)e.Item.Tag;
                if (action == ItemAction.Open) { e.Graphics.DrawLine(pen, 2, 8, 14, 8); e.Graphics.DrawLine(pen, 9, 3, 14, 8); e.Graphics.DrawLine(pen, 9, 13, 14, 8); }
                else if (action == ItemAction.ShowInFolder) e.Graphics.DrawLines(pen, new[] { new Point(1, 13), new Point(1, 3), new Point(6, 3), new Point(8, 5), new Point(15, 5), new Point(15, 13), new Point(1, 13) });
                else { e.Graphics.DrawRectangle(pen, 5, 5, 9, 10); e.Graphics.DrawLines(pen, new[] { new Point(3, 11), new Point(1, 11), new Point(1, 1), new Point(10, 1), new Point(10, 3) }); }
            }
            e.Graphics.Restore(state);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool title = e.Item.Tag is string;
            var item = e.Item as ToolStripMenuItem;
            bool hint = item != null && !string.IsNullOrEmpty(item.ShortcutKeyDisplayString) && e.Text == item.ShortcutKeyDisplayString;
            Color color = e.Item.Selected && e.Item.Enabled ? SystemColors.HighlightText : title || !e.Item.Enabled || hint ? GlassPalette.Secondary : GlassPalette.Text;
            Rectangle rect = hint ? new Rectangle(e.Item.Width - (int)(65 * _scale), 0, (int)(53 * _scale), e.Item.Height)
                : new Rectangle((int)((title ? 12 : 40) * _scale), 0, e.Item.Width - (int)((title ? 24 : 106) * _scale), e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, color, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (hint ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        { using (var pen = new Pen(Color.FromArgb(32, GlassPalette.Secondary))) e.Graphics.DrawLine(pen, 12 * _scale, e.Item.Height / 2, e.Item.Width - 12 * _scale, e.Item.Height / 2); }
    }
}
