using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FocusSearch
{
    internal sealed class LauncherWindow : Form
    {
        private readonly AppIndex _index;
        private readonly SearchEngine _engine;
        private readonly TextBox _input;
        private readonly LauncherCanvas _canvas;
        private readonly Label _footer;
        private readonly Label _placeholder;
        private readonly System.Windows.Forms.Timer _debounce;
        private CancellationTokenSource _query;
        private bool _fullScreen, _includeGrid;
        private int _version;
        private Bitmap _backdrop;
        public AppSettings Settings { get; set; }
        public Action<SearchItem> LaunchRequested;
        public LauncherWindow(AppIndex index, SearchEngine engine, AppSettings settings)
        {
            _index = index; _engine = engine; Settings = settings;
            Text = "聚焦搜索"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; TopMost = true; KeyPreview = true;
            BackColor = Color.FromArgb(37, 39, 44); ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10);
            _input = new TextBox { BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 21), Left = 58, Top = 24, Width = 580 };
            _canvas = new LauncherCanvas { Left = 20, Top = 100, Width = 640, Height = 370, BackColor = Color.Transparent };
            _footer = new Label { Left = 24, Top = 480, Height = 28, Width = 640, BackColor = Color.Transparent, ForeColor = Color.FromArgb(190, 199, 213), Text = "输入以搜索应用和文件     ↑ ↓ 选择   Enter 打开   Esc 收起" };
            Controls.Add(_input); Controls.Add(_canvas); Controls.Add(_footer);
            _placeholder = new Label { Text = "搜索应用和文件", AutoSize = false, BackColor = BackColor, ForeColor = Color.FromArgb(175, 185, 202), Font = _input.Font, Height = 42 };
            _placeholder.Click += delegate { _input.Focus(); };
            Controls.Add(_placeholder); _placeholder.BringToFront();
            _canvas.ActivateItem = delegate(SearchItem item) { if (LaunchRequested != null) LaunchRequested(item); };
            _debounce = new System.Windows.Forms.Timer { Interval = 130 };
            _debounce.Tick += delegate { _debounce.Stop(); RunQuery(); };
            _input.TextChanged += delegate {
                _version++; if (_query != null) _query.Cancel();
                _placeholder.Visible = _input.TextLength == 0;
                _debounce.Stop();
                if (_input.TextLength == 0) { RefreshEmpty(); if (Visible && !_fullScreen) { Height = _includeGrid ? Math.Min(560, Screen.FromControl(this).WorkingArea.Height - 40) : 125; _canvas.Height = Math.Max(120, Height - 150); _footer.Top = Height - 32; } }
                else
                {
                    _canvas.GridMode = false; _canvas.Visible = true;
                    var immediate = _engine.SearchApplications(_input.Text, 8); _canvas.SetItems(immediate);
                    LayoutResults(immediate.Count); _footer.Text = "正在搜索文件…   Enter 打开当前应用   Esc 收起";
                    _debounce.Start();
                }
                Invalidate();
            };
            _input.KeyDown += OnInputKey;
            Deactivate += delegate { if (Settings.HideOnDeactivate) HideLauncher(); };
            _index.Changed += IndexChanged;
            _input.HandleCreated += delegate { NativeMethods.SendMessage(_input.Handle, 0x1501, new IntPtr(1), "搜索应用和文件"); };
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; HideLauncher(); } };
        }
        private void IndexChanged(object sender, EventArgs args)
        {
            if (IsHandleCreated && !IsDisposed)
                try { BeginInvoke((Action)delegate { if (_input.TextLength == 0) RefreshEmpty(); }); } catch (InvalidOperationException) { }
        }
        public void ShowLauncher(bool includeGrid, bool fullScreen)
        {
            if (Visible) HideLauncher();
            _includeGrid = includeGrid; _fullScreen = fullScreen;
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (fullScreen)
            {
                Bounds = Screen.FromPoint(Cursor.Position).Bounds;
                _input.Left = (Width - 620) / 2;
                _input.Top = 62;
                _canvas.SetBounds(Math.Max(40, (Width - 1120) / 2), 145, Math.Min(1120, Width - 80), Math.Max(140, Height - 240));
            }
            else
            {
                Width = Math.Min(740, work.Width - 30); Height = includeGrid ? Math.Min(560, work.Height - 40) : 125;
                Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + Math.Min(150, Math.Max(20, (work.Height - Height) / 4)));
                _input.SetBounds(58, 25, Width - 145, 44);
                _canvas.SetBounds(22, 100, Width - 44, Math.Max(120, Height - 150));
            }
            _footer.SetBounds(24, Height - 32, Width - 48, 25);
            _placeholder.SetBounds(_input.Left, _input.Top - 2, _input.Width, 44);
            if (_backdrop != null) _backdrop.Dispose();
            _backdrop = GlassBackdrop.Capture(Bounds);
            _input.Clear(); RefreshEmpty(); Show(); GlassEffects.Apply(this); Activate(); NativeMethods.SetForegroundWindow(Handle); _input.Focus();
        }
        private void RefreshEmpty()
        {
            _canvas.GridMode = _includeGrid || _fullScreen;
            _canvas.Visible = _canvas.GridMode;
            _canvas.SetItems(_engine.Recent(10000));
            _footer.Text = _canvas.GridMode ? "应用程序     ← ↑ ↓ → 选择   Enter 打开   滚轮翻页   Esc 收起" : "搜索应用和文件     Enter 打开   Esc 收起";
        }
        private async void RunQuery()
        {
            int version = _version; string query = _input.Text;
            var source = new CancellationTokenSource(); _query = source; var token = source.Token;
            _engine.FileSearchEnabled = Settings.FileSearchEnabled;
            try
            {
                var results = await _engine.SearchAsync(query, 8, token);
                if (token.IsCancellationRequested || version != _version || IsDisposed) return;
                _canvas.GridMode = false; _canvas.Visible = true; _canvas.SetItems(results);
                LayoutResults(results.Count);
                _footer.Text = results.Count == 0 ? "没有找到结果 · 文件搜索范围取决于 Windows 索引" : "↑ ↓ 选择   Enter 打开   Esc 收起";
                if (!string.IsNullOrEmpty(_engine.FileSearchStatus)) _footer.Text = _engine.FileSearchStatus + "   ·   Enter 打开   Esc 收起";
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!IsDisposed && version == _version) _footer.Text = "搜索暂不可用，请重试"; }
            finally { if (object.ReferenceEquals(_query, source)) _query = null; source.Dispose(); }
        }
        private void LayoutResults(int count)
        {
            if (_fullScreen) return;
            Rectangle work = Screen.FromControl(this).WorkingArea;
            Height = Math.Min(work.Height - 24, 146 + Math.Max(1, count) * 62);
            Top = Math.Max(work.Top + 12, Math.Min(Top, work.Bottom - Height - 12));
            _canvas.Height = Math.Max(62, Height - 145); _footer.Top = Height - 32;
        }
        public void HideLauncher() { _debounce.Stop(); if (_query != null) _query.Cancel(); Hide(); if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; } }
        private void OnInputKey(object sender, KeyEventArgs args)
        {
            if (args.KeyCode == Keys.Escape) { HideLauncher(); args.SuppressKeyPress = true; }
            else if (args.KeyCode == Keys.Enter) { _canvas.OpenSelected(); args.SuppressKeyPress = true; }
            else if (args.KeyCode == Keys.Down || args.KeyCode == Keys.Up || ((_input.TextLength == 0) && (args.KeyCode == Keys.Left || args.KeyCode == Keys.Right)) || args.KeyCode == Keys.PageDown || args.KeyCode == Keys.PageUp)
            { _canvas.Navigate(args.KeyCode); args.SuppressKeyPress = true; }
        }
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            using (var pen = new Pen(Color.FromArgb(160, 171, 190), 2)) { args.Graphics.DrawEllipse(pen, _input.Left - 31, _input.Top + 9, 16, 16); args.Graphics.DrawLine(pen, _input.Left - 17, _input.Top + 24, _input.Left - 10, _input.Top + 31); }
            using (var pen = new Pen(Color.FromArgb(67, 72, 82))) args.Graphics.DrawLine(pen, 22, _fullScreen ? 125 : 84, Width - 22, _fullScreen ? 125 : 84);
        }
        protected override void OnPaintBackground(PaintEventArgs args)
        {
            if (_backdrop == null) base.OnPaintBackground(args);
            else args.Graphics.DrawImage(_backdrop, ClientRectangle);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (_backdrop != null) _backdrop.Dispose(); _index.Changed -= IndexChanged; _debounce.Dispose(); if (_query != null) _query.Cancel(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class LauncherCanvas : Control
    {
        private IList<SearchItem> _items = new List<SearchItem>();
        private readonly Dictionary<string, Icon> _icons = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);
        private int _selected, _page;
        public bool GridMode { get; set; }
        public Action<SearchItem> ActivateItem;
        private int Columns { get { return Math.Max(1, Width / 120); } }
        private int Rows { get { return Math.Max(1, (Height - 28) / 116); } }
        private int PageSize { get { return Columns * Rows; } }
        private int ListStart { get { return Math.Max(0, _selected - Math.Max(1, Height / 62) + 1); } }
        public LauncherCanvas() { DoubleBuffered = true; ForeColor = Color.White; SetStyle(ControlStyles.SupportsTransparentBackColor, true); SetStyle(ControlStyles.Selectable, false); }
        public void SetItems(IList<SearchItem> items) { _items = items; _selected = 0; _page = 0; Invalidate(); }
        public void OpenSelected() { if (_items.Count > 0 && _selected < _items.Count && ActivateItem != null) ActivateItem(_items[_selected]); }
        public void Navigate(Keys key)
        {
            _selected = GridNavigation.Move(_selected, _items.Count, Columns, PageSize, key, GridMode); _page = _selected / PageSize; Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        { Navigate(e.Delta < 0 ? (GridMode ? Keys.PageDown : Keys.Down) : (GridMode ? Keys.PageUp : Keys.Up)); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (GridMode && e.Y >= Rows * 116) return;
            int index = GridMode ? _page * PageSize + (e.Y / 116) * Columns + Math.Min(Columns - 1, e.X / Math.Max(1, Width / Columns)) : ListStart + e.Y / 62;
            if (index >= _items.Count || index < 0) return;
            _selected = index; Invalidate(); if (GridMode) OpenSelected();
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { if (!GridMode) OpenSelected(); base.OnMouseDoubleClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int start = GridMode ? _page * PageSize : ListStart;
            int count = GridMode ? PageSize : Math.Min(Math.Max(1, Height / 62), _items.Count);
            for (int offset = 0; offset < count && start + offset < _items.Count; offset++)
            {
                int index = start + offset; SearchItem item = _items[index];
                Rectangle tile = GridMode ? new Rectangle((offset % Columns) * (Width / Columns), (offset / Columns) * 116, Width / Columns, 110) : new Rectangle(0, offset * 62, Width, 60);
                if (index == _selected)
                {
                    var rect = new Rectangle(tile.X + 5, tile.Y + 3, tile.Width - 10, tile.Height - 6);
                    using (var path = new GraphicsPath())
                    using (var brush = new SolidBrush(Color.FromArgb(66, 73, 88)))
                    using (var pen = new Pen(Color.FromArgb(102, 117, 139)))
                    {
                        path.AddArc(rect.Left, rect.Top, 20, 20, 180, 90); path.AddArc(rect.Right - 20, rect.Top, 20, 20, 270, 90);
                        path.AddArc(rect.Right - 20, rect.Bottom - 20, 20, 20, 0, 90); path.AddArc(rect.Left, rect.Bottom - 20, 20, 20, 90, 90); path.CloseFigure();
                        e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path);
                    }
                }
                Icon icon = GetIcon(string.IsNullOrEmpty(item.IconKey) ? item.Path : item.IconKey);
                int size = GridMode ? 52 : 32;
                Rectangle iconRect = new Rectangle(GridMode ? tile.X + (tile.Width - size) / 2 : 16, tile.Y + (GridMode ? 10 : 12), size, size);
                e.Graphics.DrawIcon(icon, iconRect);
                Rectangle textRect = GridMode ? new Rectangle(tile.X + 4, tile.Y + 69, tile.Width - 8, 30) : new Rectangle(62, tile.Y + 7, tile.Width - 76, 26);
                TextRenderer.DrawText(e.Graphics, item.Name, Font, textRect, Color.White, TextFormatFlags.EndEllipsis | (GridMode ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
                if (!GridMode) TextRenderer.DrawText(e.Graphics, item.Detail, Font, new Rectangle(62, tile.Y + 33, tile.Width - 76, 22), Color.FromArgb(176, 185, 199), TextFormatFlags.EndEllipsis);
            }
            if (GridMode && _items.Count > PageSize)
                TextRenderer.DrawText(e.Graphics, (_page + 1) + " / " + ((_items.Count + PageSize - 1) / PageSize), Font, new Rectangle(0, Height - 24, Width, 24), Color.Silver, TextFormatFlags.HorizontalCenter);
        }
        private Icon GetIcon(string path)
        {
            Icon icon; if (_icons.TryGetValue(path, out icon)) return icon;
            if (_icons.Count >= 96) { foreach (var cached in _icons.Values) cached.Dispose(); _icons.Clear(); }
            var info = new NativeMethods.ShellFileInfo();
            NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), 0x100);
            if (info.Icon != IntPtr.Zero) { icon = (Icon)Icon.FromHandle(info.Icon).Clone(); NativeMethods.DestroyIcon(info.Icon); }
            else icon = (Icon)SystemIcons.Application.Clone();
            _icons[path] = icon; return icon;
        }
        protected override void Dispose(bool disposing) { if (disposing) foreach (var icon in _icons.Values) icon.Dispose(); base.Dispose(disposing); }
    }
}
