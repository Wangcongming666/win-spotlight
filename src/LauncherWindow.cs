using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FocusSearch
{
    internal sealed class LauncherWindow : Form
    {
        private readonly AppIndex _index;
        private readonly SearchEngine _engine;
        private readonly Func<string, Task> _openFileLocation;
        private bool _contextActionPending;
        private readonly TextBox _input;
        private readonly LauncherCanvas _canvas;
        private readonly Label _footer;
        private readonly LauncherCloseButton _dismiss;
        private readonly System.Windows.Forms.Timer _debounce;
        private CancellationTokenSource _query;
        private bool _fullScreen;
        private int _version;
        private Bitmap _backdrop;
        private readonly WindowAnimator _animator;
        private bool _presenting;
        private bool _tabsExpanded, _browseApps;
        private SearchScope _scope = SearchScope.All;
        private readonly SearchTabButton[] _tabs;
        private readonly Panel _tabReveal;
        private readonly ToolTip _tabTips;
        private readonly System.Windows.Forms.Timer _tabTimer;
        private int SearchBarWidth { get { return _fullScreen ? 600 : Math.Max(200, Width - 256); } }
        private bool Compact { get { return !_fullScreen && !_browseApps && _input.TextLength == 0; } }
        public bool IsHiding { get { return _animator.IsClosing; } }
        public bool IsApplicationsView { get { return _fullScreen; } }
        public AppSettings Settings { get; set; }
        public Action<SearchItem> LaunchRequested;
        public LauncherWindow(AppIndex index, SearchEngine engine, AppSettings settings, Func<string, Task> openFileLocation = null)
        {
            _index = index; _engine = engine; Settings = settings;
            _openFileLocation = openFileLocation ?? FileLocationOpener.OpenAsync;
            Text = "聚焦搜索"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; TopMost = true; KeyPreview = true;
            BackColor = Color.FromArgb(242, 246, 250); ForeColor = Color.FromArgb(35, 48, 65);
            DoubleBuffered = true;
            _animator = new WindowAnimator(this);
            Font = GlassPalette.CreateFont(10);
            _input = new SearchInput { BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = ForeColor, Font = GlassPalette.CreateFont(20), Left = 70, Top = 34, Width = 580, AccessibleName = "搜索应用和文件" };
            _canvas = new LauncherCanvas { Left = 20, Top = 100, Width = 640, Height = 370, BackColor = Color.Transparent };
            _footer = new Label { Left = 24, Top = 480, Height = 28, Width = 640, BackColor = Color.Transparent, ForeColor = Color.FromArgb(190, 199, 213), Text = "输入以搜索应用和文件     ↑ ↓ 选择   Enter 打开   Esc 收起" };
            _footer.TextAlign = ContentAlignment.MiddleLeft;
            _footer.ForeColor = Color.FromArgb(69, 86, 107);
            _footer.AutoEllipsis = true;
            _dismiss = new LauncherCloseButton { Width = 28, Height = 28, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _dismiss.Click += delegate { HideLauncher(); };
            Controls.Add(_input); Controls.Add(_canvas); Controls.Add(_footer); Controls.Add(_dismiss);
            _tabTips = new ToolTip { InitialDelay = 350, ReshowDelay = 100 };
            _tabs = new SearchTabButton[4];
            var scopes = new[] { SearchScope.Applications, SearchScope.Folders, SearchScope.All, SearchScope.Files };
            var labels = new[] { "应用", "文件夹", "全部", "文件" };
            for (int i = 0; i < _tabs.Length; i++)
            {
                SearchScope scope = scopes[i];
                _tabs[i] = new SearchTabButton { Scope = scope, Visible = false, Size = new Size(54, 54), AccessibleName = labels[i], Selected = scope == SearchScope.All };
                _tabs[i].Click += delegate { SelectScope(scope); };
                _tabs[i].MouseEnter += delegate { if (!_presenting) SelectScope(scope); };
                _tabTips.SetToolTip(_tabs[i], labels[i]); Controls.Add(_tabs[i]);
            }
            _tabReveal = new Panel { BackColor = Color.Transparent, TabStop = false, AccessibleName = "展开搜索类别" };
            _tabReveal.MouseEnter += delegate { ExpandTabs(true); };
            Controls.Add(_tabReveal);
            _tabTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _tabTimer.Tick += delegate { UpdateHoverState(PointToClient(Cursor.Position)); };
            _canvas.ActivateItem = delegate(SearchItem item) { if (LaunchRequested != null) LaunchRequested(item); };
            _canvas.ContextActionRequested = InvokeContextAction;
            _canvas.ContextMenuClosed += delegate { CheckDeactivation(); };
            _debounce = new System.Windows.Forms.Timer { Interval = 130 };
            _debounce.Tick += delegate { _debounce.Stop(); if (_input.TextLength == 0) LayoutEmpty(); else RunQuery(); };
            _input.TextChanged += delegate {
                _version++; if (_query != null) _query.Cancel();
                _animator.Stop();
                _debounce.Stop();
                if (_input.TextLength == 0) {
                    _browseApps = _scope == SearchScope.Applications && Settings.AppGridEnabled && InApplicationPreview(PointToClient(Cursor.Position));
                    RefreshEmpty();
                }
                else
                {
                    _canvas.GridMode = false; _canvas.Visible = true;
                    var immediate = _scope == SearchScope.All || _scope == SearchScope.Applications ? _engine.SearchApplications(_input.Text, 8) : new List<SearchItem>(); _canvas.SetItems(immediate);
                    _footer.Text = "正在搜索文件…   Enter 打开当前应用   Esc 收起";
                }
                if (!_presenting) _debounce.Start();
            };
            _input.KeyDown += OnInputKey;
            Deactivate += delegate { CheckDeactivation(); };
            _index.Changed += IndexChanged;
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; HideLauncher(); } };
        }
        private void IndexChanged(object sender, EventArgs args)
        {
            if (IsHandleCreated && !IsDisposed)
                try { BeginInvoke((Action)delegate { if (_input.TextLength == 0) RefreshEmpty(); }); } catch (InvalidOperationException) { }
        }
        private void CheckDeactivation()
        {
            if (_presenting || _contextActionPending || IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate {
                if (!IsDisposed && Visible && !_presenting && !_contextActionPending && Settings.HideOnDeactivate && !_canvas.IsContextMenuOpen && NativeMethods.GetForegroundWindow() != Handle) HideLauncher();
            });
        }
        private void ExpandTabs(bool expanded)
        {
            expanded = expanded && !_fullScreen;
            _tabsExpanded = expanded;
            foreach (var tab in _tabs) tab.Visible = expanded;
            if (expanded || _browseApps) _tabTimer.Start(); else _tabTimer.Stop();
            UpdateWindowRegion(); Invalidate(true);
        }
        private void SelectScope(SearchScope scope)
        {
            if (_scope == scope && (scope != SearchScope.Applications || _browseApps || _input.TextLength > 0)) return;
            _scope = scope; _browseApps = scope == SearchScope.Applications && Settings.AppGridEnabled;
            foreach (var tab in _tabs) { tab.Selected = tab.Scope == scope; tab.Invalidate(); }
            ((SearchInput)_input).Hint = scope == SearchScope.All ? "聚焦搜索" : scope == SearchScope.Applications ? "搜索应用" : scope == SearchScope.Folders ? "搜索文件夹" : "搜索文件";
            _version++; if (_query != null) _query.Cancel(); _debounce.Stop();
            if (_input.TextLength == 0) { RefreshEmpty(); LayoutEmpty(); } else RunQuery();
            _input.Focus();
        }
        private bool InApplicationPreview(Point mouse)
        {
            return (_tabs[0].Visible && _tabs[0].Bounds.Contains(mouse))
                || (_canvas.Visible && new Rectangle(0, 64, Width, Math.Max(0, Height - 64)).Contains(mouse));
        }
        private void UpdateHoverState(Point mouse)
        {
            if (_presenting || !Visible) return;
            bool focused = false; foreach (var tab in _tabs) focused |= tab.Focused;
            if (_browseApps && !_fullScreen && _input.TextLength == 0 && !focused && !_canvas.IsContextMenuOpen && !InApplicationPreview(mouse))
            {
                _browseApps = false;
                RefreshEmpty(); LayoutEmpty();
            }
            int left = _fullScreen ? _input.Right - 40 : SearchBarWidth - 48;
            bool inHeader = new Rectangle(left, _fullScreen ? 54 : 0, Width - left, 82).Contains(mouse);
            if (_tabsExpanded && !focused && !inHeader) ExpandTabs(false);
            else if (!_tabsExpanded && !_browseApps) _tabTimer.Stop();
        }
        private async void InvokeContextAction(SearchItem item, ItemAction action)
        {
            if (_contextActionPending) return;
            try
            {
                if (action == ItemAction.CopyPath)
                {
                    Clipboard.SetText(item.Path);
                    _footer.Text = "已复制路径   ·   双击打开   ·   右键更多操作";
                }
                else if (action == ItemAction.ShowInFolder)
                {
                    string path = Path.GetFullPath(item.Path);
                    if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException();
                    _contextActionPending = true;
                    _footer.Text = "正在打开所在位置…";
                    _footer.Visible = true;
                    await _openFileLocation(path);
                    if (!IsDisposed) HideLauncher();
                }
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    _footer.Text = action == ItemAction.CopyPath ? "暂时无法复制路径，请重试" : "无法打开所在位置：" + error.Message;
                    _footer.AccessibleDescription = _footer.Text;
                    _footer.Visible = true;
                    Activate(); NativeMethods.SetForegroundWindow(Handle);
                    if (_input.Visible) _input.Focus();
                }
            }
            finally { _contextActionPending = false; }
        }
        public void ReportLaunchError(string message)
        {
            _footer.Text = "无法打开：" + message;
            _footer.AccessibleDescription = _footer.Text;
            _footer.Visible = true;
            _input.Focus();
        }
        public void ShowLauncher(bool includeGrid, bool fullScreen)
        {
            _animator.Stop();
            if (Visible) FinishHide();
            _presenting = true;
            GlassPalette.Refresh();
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : GlassPalette.Surface;
            ForeColor = _input.ForeColor = _canvas.ForeColor = GlassPalette.Text;
            _input.BackColor = BackColor;
            _footer.ForeColor = _dismiss.ForeColor = GlassPalette.Secondary;
            _fullScreen = fullScreen;
            _scope = fullScreen ? SearchScope.Applications : SearchScope.All; _browseApps = false; ((SearchInput)_input).Hint = "聚焦搜索";
            _input.Visible = _tabReveal.Visible = !fullScreen;
            foreach (var tab in _tabs) { tab.Selected = tab.Scope == SearchScope.All; tab.Enabled = tab.Scope != SearchScope.Applications || Settings.AppGridEnabled; }
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (fullScreen)
            {
                Bounds = Screen.FromPoint(Cursor.Position).Bounds;
                _input.SetBounds((Width - 600) / 2, 72, 600, 38);
                _canvas.SetBounds(Math.Max(40, (Width - 1120) / 2), 72, Math.Min(1120, Width - 80), Math.Max(140, Height - 172));
            }
            else
            {
                Width = Math.Min(720, work.Width - 30); Height = 76;
                Location = new Point(Math.Min(work.Right - Width - 12, work.Left + (work.Width - SearchBarWidth) / 2), work.Top + Math.Max(20, (work.Height - Height) / 3));
                _input.SetBounds(54, 21, SearchBarWidth - 100, 34);
                _canvas.SetBounds(18, 98, Width - 36, 120);
            }
            int tabLeft = _fullScreen ? _input.Right + 22 : SearchBarWidth + 10;
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].SetBounds(tabLeft + i * 60, _fullScreen ? 58 : 10, 54, 54);
            _tabReveal.SetBounds(_fullScreen ? _input.Right - 20 : SearchBarWidth - 42, _fullScreen ? 58 : 10, 36, 54);
            ExpandTabs(false);
            _footer.SetBounds(24, Height - 34, Width - 88, 25);
            _dismiss.SetBounds(Width - 52, Height - 36, 28, 28);
            if (_backdrop != null) _backdrop.Dispose();
            Rectangle capture = Bounds;
            if (!fullScreen) capture.Height = Math.Max(Height, Math.Min(work.Height, 174 + 8 * 62));
            _backdrop = GlassBackdrop.Capture(capture);
            if (_backdrop != null) _input.BackColor = _backdrop.GetPixel(Math.Min(_backdrop.Width - 1, _input.Left + _input.Width / 2), Math.Min(_backdrop.Height - 1, _input.Top + _input.Height / 2));
            _input.Clear(); RefreshEmpty(); _canvas.PrepareIcons();
            UpdateWindowRegion();
            _canvas.AnimationsEnabled = Settings.AnimationsEnabled && Motion.SystemAllowsAnimation();
            Opacity = 0.04;
            _animator.Open(Settings.AnimationsEnabled && Motion.SystemAllowsAnimation(), !fullScreen);
            Show(); UpdateWindowRegion(); Activate(); NativeMethods.SetForegroundWindow(Handle); if (fullScreen) Focus(); else _input.Focus(); _presenting = false;
        }
        private void RefreshEmpty()
        {
            _canvas.GridMode = _browseApps || _fullScreen;
            _canvas.Visible = _canvas.GridMode;
            _canvas.SetItems(_engine.Recent(10000));
            _footer.Visible = _dismiss.Visible = !Compact;
            _footer.Text = _canvas.GridMode ? "双击打开   ·   右键更多   ·   滚轮翻页   ·   Esc 收起" : "应用、文件与文件夹   ·   Esc 收起";
        }
        private void LayoutEmpty()
        {
            if (!Visible || _fullScreen) return;
            SuspendLayout();
            try
            {
                Height = _browseApps ? Math.Min(600, Screen.FromControl(this).WorkingArea.Height - 40) : 76;
                _canvas.Height = Math.Max(120, Height - 142); _footer.Top = Height - 32;
                _footer.Visible = _dismiss.Visible = !Compact; UpdateWindowRegion();
            }
            finally { ResumeLayout(); }
        }
        private async void RunQuery()
        {
            int version = _version; string query = _input.Text;
            var source = new CancellationTokenSource(); _query = source; var token = source.Token;
            _engine.FileSearchEnabled = Settings.FileSearchEnabled;
            try
            {
                SearchScope scope = _scope;
                var results = await _engine.SearchAsync(query, 8, token, scope);
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
            SuspendLayout();
            try
            {
                int height = Math.Min(work.Height - 24, 142 + Math.Max(1, count) * 62);
                int top = Math.Max(work.Top + 12, Math.Min(Top, work.Bottom - height - 12));
                SetBounds(Left, top, Width, height);
                _canvas.Height = Math.Max(62, Height - 142); _footer.Top = Height - 32;
                _footer.Visible = _dismiss.Visible = true; UpdateWindowRegion();
            }
            finally { ResumeLayout(); }
        }
        public void HideLauncher()
        {
            _canvas.CloseItemMenu();
            ExpandTabs(false);
            _debounce.Stop(); if (_query != null) _query.Cancel();
            _tabTimer.Stop();
            if (!Visible) return;
            _animator.Close(Settings.AnimationsEnabled && Motion.SystemAllowsAnimation(), FinishHide);
        }
        private void FinishHide() { Hide(); if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; } }
        private void OnInputKey(object sender, KeyEventArgs args)
        {
            if (HandleLauncherKey(args.KeyCode)) args.SuppressKeyPress = true;
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            return HandleLauncherKey(keyData & Keys.KeyCode) || base.ProcessCmdKey(ref message, keyData);
        }
        private bool HandleLauncherKey(Keys key)
        {
            if (_canvas.IsContextMenuOpen) return false;
            if (key == Keys.F6 && !_fullScreen) { ExpandTabs(true); foreach (var tab in _tabs) if (tab.Enabled) { tab.Focus(); break; } return true; }
            if (key == Keys.Escape && _tabsExpanded) { ExpandTabs(false); _input.Focus(); return true; }
            foreach (var tab in _tabs) if (tab.Focused) return false;
            if (key == Keys.Escape) { HideLauncher(); return true; }
            if (key == Keys.Enter) { _canvas.OpenSelected(); return true; }
            if (key == Keys.Down || key == Keys.Up || ((_input.TextLength == 0) && (key == Keys.Left || key == Keys.Right)) || key == Keys.PageDown || key == Keys.PageUp)
            { _canvas.Navigate(key); return true; }
            return false;
        }
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            if (_fullScreen) return;
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF search = _fullScreen ? new RectangleF(_input.Left - 48, _input.Top - 14, _input.Width + 64, 64) : new RectangleF(1, 7, SearchBarWidth - 2, 60);
            using (var path = RoundedShapes.Path(search, 30))
            using (var fill = new SolidBrush(_input.BackColor)) args.Graphics.FillPath(fill, path);
            using (var path = RoundedShapes.Path(search, 30))
            using (var rim = new Pen(Color.FromArgb(GlassPalette.IsDark ? 115 : 235, Color.White), 1.4f)) args.Graphics.DrawPath(rim, path);
            using (var path = RoundedShapes.Path(new RectangleF(search.X + 2, search.Y + 2, search.Width - 4, search.Height - 4), 28))
            using (var rim = new Pen(Color.FromArgb(36, GlassPalette.Secondary))) args.Graphics.DrawPath(rim, path);
            using (var fill = new SolidBrush(_input.BackColor)) args.Graphics.FillRectangle(fill, _input.Left - 1, _input.Top - 1, _input.Width + 2, _input.Height + 2);
            using (var pen = new Pen(GlassPalette.Secondary, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { args.Graphics.DrawEllipse(pen, _input.Left - 31, _input.Top + 7, 17, 17); args.Graphics.DrawLine(pen, _input.Left - 16, _input.Top + 22, _input.Left - 10, _input.Top + 28); }
            if (_canvas.Visible)
                using (var pen = new Pen(Color.FromArgb(GlassPalette.IsDark ? 22 : 18, GlassPalette.Text))) args.Graphics.DrawLine(pen, 24, _fullScreen ? 144 : 82, Width - 24, _fullScreen ? 144 : 82);
            if (!_fullScreen && !Compact)
            {
                using (var border = new LinearGradientBrush(ClientRectangle, Color.FromArgb(GlassPalette.IsDark ? 95 : 220, 255, 255, 255), Color.FromArgb(GlassPalette.IsDark ? 25 : 75, 193, 201, 215), LinearGradientMode.Vertical))
                using (var pen = new Pen(border, 1))
                using (var outline = RoundedShapes.Path(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 24)) args.Graphics.DrawPath(pen, outline);
                using (var pen = new Pen(Color.FromArgb(24, 255, 255, 255)))
                using (var outline = RoundedShapes.Path(new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), 23)) args.Graphics.DrawPath(pen, outline);
            }
        }
        protected override void OnSizeChanged(EventArgs args)
        {
            base.OnSizeChanged(args);
            UpdateWindowRegion();
        }
        private void UpdateWindowRegion()
        {
            if (Width < 48 || Height < 48) return;
            if (IsHandleCreated && _input != null) GlassEffects.Apply(this, Compact);
            Region previous = Region;
            if (_fullScreen) Region = null;
            else if (_input != null && Compact)
            {
                Region expanded;
                using (var path = RoundedShapes.Path(new RectangleF(0, 6, SearchBarWidth, 62), 31)) expanded = new Region(path);
                if (_tabsExpanded && _tabs != null) foreach (var tab in _tabs) using (var path = new GraphicsPath()) { path.AddEllipse(tab.Bounds); expanded.Union(path); }
                // Assign after union: mutating Form.Region afterwards does not
                // update the native HWND clipping region or its mouse hit area.
                Region = expanded;
            }
            else using (var path = RoundedShapes.Path(new RectangleF(0, 0, Width, Height), 24)) Region = new Region(path);
            if (previous != null) previous.Dispose();
        }
        protected override void OnPaintBackground(PaintEventArgs args)
        {
            if (_backdrop == null) base.OnPaintBackground(args);
            else args.Graphics.DrawImageUnscaled(_backdrop, Point.Empty);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _tabTimer.Dispose(); _tabTips.Dispose(); _animator.Dispose(); if (_backdrop != null) _backdrop.Dispose(); _index.Changed -= IndexChanged; _debounce.Dispose(); if (_query != null) _query.Cancel(); }
            base.Dispose(disposing);
        }
    }

}
