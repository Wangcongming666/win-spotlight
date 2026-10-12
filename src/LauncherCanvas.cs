using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;
namespace FocusSearch
{
    internal sealed class LauncherCanvas : Control
    {
        private IList<SearchItem> _items = new List<SearchItem>();
        private readonly ShellIconCache _icons = new ShellIconCache();
        private readonly System.Windows.Forms.Timer _hoverTimer = new System.Windows.Forms.Timer { Interval = 15 };
        private readonly System.Windows.Forms.Timer _iconTimer = new System.Windows.Forms.Timer { Interval = 32 };
        private readonly System.Windows.Forms.Timer _wheelTimer = new System.Windows.Forms.Timer { Interval = 40 };
        private int _iconDispatchPending, _wheelDelta;
        private readonly Stopwatch _hoverClock = new Stopwatch();
        private int _selected, _page, _hovered = -1, _previousHover = -1;
        private double _hoverProgress = 1;
        public bool GridMode { get; set; }
        public bool AnimationsEnabled { get; set; }
        public Action<SearchItem> ActivateItem;
        private int Columns { get { return Math.Max(1, Width / 144); } }
        private int Rows { get { return Math.Min(Math.Max(1, (Height - 30) / 132), Math.Max(1, 96 / Columns)); } }
        private int PageSize { get { return Columns * Rows; } }
        private int ListStart { get { return Math.Max(0, _selected - Math.Max(1, Height / 62) + 1); } }
        public LauncherCanvas()
        {
            DoubleBuffered = true; ForeColor = Color.White; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true); SetStyle(ControlStyles.Selectable, false);
            _hoverTimer.Tick += delegate {
                _hoverProgress = Motion.EaseOut(_hoverClock.Elapsed.TotalMilliseconds / 130);
                InvalidateHover(); if (_hoverProgress >= 1) _hoverTimer.Stop();
            };
            _icons.Ready += IconsReady;
            _iconTimer.Tick += delegate { _iconTimer.Stop(); _icons.DrainReady(); Interlocked.Exchange(ref _iconDispatchPending, 0); Invalidate(); };
            _wheelTimer.Tick += delegate {
                _wheelTimer.Stop(); int steps = _wheelDelta / 120; _wheelDelta %= 120;
                if (steps == 0) return;
                if (GridMode) { _page = Math.Max(0, Math.Min(Math.Max(0, _items.Count - 1) / PageSize, _page - steps)); _selected = _page * PageSize; PrepareIcons(); Invalidate(); }
                else Navigate(steps < 0 ? Keys.Down : Keys.Up);
            };
        }
        private void IconsReady(object sender, EventArgs args)
        {
            if (IsDisposed || !IsHandleCreated || Interlocked.CompareExchange(ref _iconDispatchPending, 1, 0) != 0) return;
            try { BeginInvoke((Action)delegate { if (!IsDisposed) { if (Visible) _iconTimer.Start(); else { _icons.DrainReady(); Interlocked.Exchange(ref _iconDispatchPending, 0); } } }); }
            catch (InvalidOperationException) { Interlocked.Exchange(ref _iconDispatchPending, 0); }
        }
        public void SetItems(IList<SearchItem> items) { _items = items; _selected = 0; _page = 0; _hovered = -1; _previousHover = -1; _hoverTimer.Stop(); _wheelTimer.Stop(); _wheelDelta = 0; PrepareIcons(); Invalidate(); }
        public void PrepareIcons()
        {
            if (!Visible && IsHandleCreated) return;
            var wanted = new List<SearchItem>(); var visible = new List<SearchItem>(); int start = GridMode ? _page * PageSize : ListStart; int count = GridMode ? PageSize : 8;
            for (int i = start; i < Math.Min(_items.Count, start + count); i++) { wanted.Add(_items[i]); visible.Add(_items[i]); }
            if (GridMode)
            {
                for (int i = start + count; i < Math.Min(_items.Count, start + count * 2); i++) wanted.Add(_items[i]);
                for (int i = Math.Max(0, start - count); i < start; i++) wanted.Add(_items[i]);
            }
            _icons.Prefetch(wanted, visible);
        }
        public void OpenSelected() { if (_items.Count > 0 && _selected < _items.Count && ActivateItem != null) ActivateItem(_items[_selected]); }
        public void Navigate(Keys key)
        {
            _selected = GridNavigation.Move(_selected, _items.Count, Columns, PageSize, key, GridMode); _page = _selected / PageSize; PrepareIcons(); Invalidate();
        }
        protected override void OnVisibleChanged(EventArgs args) { if (!Visible) { _hoverTimer.Stop(); _wheelTimer.Stop(); _iconTimer.Stop(); _icons.Pause(); Interlocked.Exchange(ref _iconDispatchPending, 0); } base.OnVisibleChanged(args); if (Visible) PrepareIcons(); }
        protected override void OnMouseWheel(MouseEventArgs e) { _wheelDelta += e.Delta; if (!_wheelTimer.Enabled) _wheelTimer.Start(); }
        private void InvalidateHover()
        {
            int start = GridMode ? _page * PageSize : ListStart;
            foreach (int index in new[] { _hovered, _previousHover })
            {
                int offset = index - start; if (offset < 0) continue;
                if (GridMode && offset < PageSize) Invalidate(new Rectangle((offset % Columns) * (Width / Columns), (offset / Columns) * 132, Width / Columns, 132));
                else if (!GridMode && offset < 8) Invalidate(new Rectangle(0, offset * 62, Width, 62));
            }
        }
        private int HitTest(Point point)
        {
            if (point.X < 0 || point.X >= Width || point.Y < 0) return -1;
            if (GridMode && point.Y >= Rows * 132) return -1;
            int index = GridMode ? _page * PageSize + (point.Y / 132) * Columns + Math.Min(Columns - 1, point.X / Math.Max(1, Width / Columns)) : ListStart + point.Y / 62;
            return index >= _items.Count ? -1 : index;
        }
        private void Hover(int index)
        {
            if (index == _hovered) return;
            _previousHover = _hovered; _hovered = index; _hoverProgress = 0;
            if (!AnimationsEnabled) { _hoverProgress = 1; _hoverTimer.Stop(); Invalidate(); return; }
            _hoverClock.Restart(); _hoverTimer.Start();
        }
        protected override void OnMouseMove(MouseEventArgs e) { Hover(HitTest(e.Location)); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs args) { Hover(-1); base.OnMouseLeave(args); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int index = HitTest(e.Location); if (index < 0) return;
            _selected = index; Invalidate(); if (GridMode) OpenSelected();
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { if (!GridMode && e.Button == MouseButtons.Left && HitTest(e.Location) >= 0) OpenSelected(); base.OnMouseDoubleClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            _icons.DrainReady();
            int start = GridMode ? _page * PageSize : ListStart;
            int count = GridMode ? PageSize : Math.Min(Math.Max(1, Height / 62), _items.Count);
            for (int offset = 0; offset < count && start + offset < _items.Count; offset++)
            {
                int index = start + offset; SearchItem item = _items[index];
                Rectangle tile = GridMode ? new Rectangle((offset % Columns) * (Width / Columns), (offset / Columns) * 132, Width / Columns, 128) : new Rectangle(0, offset * 62, Width, 60);
                if (!e.ClipRectangle.IntersectsWith(tile)) continue;
                double hover = index == _hovered ? _hoverProgress : index == _previousHover ? 1 - _hoverProgress : 0;
                int size = GridMode ? 72 + (int)(hover * 5) : 36;
                Rectangle iconRect = new Rectangle(GridMode ? tile.X + (tile.Width - size) / 2 : 14, tile.Y + (GridMode ? 8 - (int)(hover * 2) : 10), size, size);
                if (GridMode)
                {
                    RectangleF focus = new RectangleF(tile.X + (tile.Width - 88) / 2, tile.Y, 88, 88);
                    if (index == _selected || hover > 0)
                        using (var shape = RoundedShapes.Path(focus, 22))
                        using (var fill = new SolidBrush(Color.FromArgb(index == _selected ? 26 : (int)(hover * 22), 224, 237, 255)))
                        using (var outline = new Pen(Color.FromArgb(index == _selected ? 96 : (int)(hover * 55), 206, 223, 248), 1))
                        { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(outline, shape); }
                }
                else if (index == _selected || hover > 0)
                    using (var shape = RoundedShapes.Path(new RectangleF(2, tile.Y + 1, tile.Width - 4, 58), 13))
                    using (var fill = new SolidBrush(Color.FromArgb(index == _selected ? 42 : (int)(hover * 25), 198, 218, 248))) e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawImage(_icons.Get(item), iconRect);
                Rectangle text = GridMode ? new Rectangle(tile.X + 4, tile.Y + 94, tile.Width - 8, 28) : new Rectangle(65, tile.Y + 6, tile.Width - 80, 26);
                TextRenderer.DrawText(e.Graphics, item.Name, Font, text, Color.FromArgb(244, 247, 254), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (GridMode ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
                if (!GridMode) TextRenderer.DrawText(e.Graphics, item.Detail, Font, new Rectangle(65, tile.Y + 32, tile.Width - 80, 22), Color.FromArgb(178, 191, 215), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (GridMode && _items.Count > PageSize)
            {
                int pages = (_items.Count + PageSize - 1) / PageSize;
                if (pages <= 14)
                {
                    int left = (Width - pages * 16) / 2;
                    for (int i = 0; i < pages; i++) using (var dot = new SolidBrush(Color.FromArgb(i == _page ? 225 : 72, 228, 237, 255))) e.Graphics.FillEllipse(dot, left + i * 16, Height - 20, 5, 5);
                }
                else TextRenderer.DrawText(e.Graphics, (_page + 1) + " / " + pages, Font, new Rectangle(0, Height - 26, Width, 24), Color.FromArgb(178, 191, 215), TextFormatFlags.HorizontalCenter);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { _icons.Ready -= IconsReady; _hoverTimer.Dispose(); _wheelTimer.Dispose(); _iconTimer.Dispose(); _icons.Dispose(); } base.Dispose(disposing); }
    }
}
