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
        private readonly System.Windows.Forms.Timer _debounce;
        private CancellationTokenSource _query;
        private bool _fullScreen, _includeGrid;
        private int _version;
        private Bitmap _backdrop;
        private readonly WindowAnimator _animator;
        private bool _presenting;
        public bool IsHiding { get { return _animator.IsClosing; } }
        public AppSettings Settings { get; set; }
        public Action<SearchItem> LaunchRequested;
        public LauncherWindow(AppIndex index, SearchEngine engine, AppSettings settings)
        {
            _index = index; _engine = engine; Settings = settings;
            Text = "聚焦搜索"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; TopMost = true; KeyPreview = true;
            BackColor = Color.FromArgb(35, 39, 49); ForeColor = Color.White;
            DoubleBuffered = true;
            _animator = new WindowAnimator(this);
            Font = new Font("Microsoft YaHei UI", 10);
            _input = new SearchInput { BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 18), Left = 70, Top = 34, Width = 580 };
            _canvas = new LauncherCanvas { Left = 20, Top = 100, Width = 640, Height = 370, BackColor = Color.Transparent };
            _footer = new Label { Left = 24, Top = 480, Height = 28, Width = 640, BackColor = Color.Transparent, ForeColor = Color.FromArgb(190, 199, 213), Text = "输入以搜索应用和文件     ↑ ↓ 选择   Enter 打开   Esc 收起" };
            Controls.Add(_input); Controls.Add(_canvas); Controls.Add(_footer);
            _canvas.ActivateItem = delegate(SearchItem item) { if (LaunchRequested != null) LaunchRequested(item); };
            _debounce = new System.Windows.Forms.Timer { Interval = 130 };
            _debounce.Tick += delegate { _debounce.Stop(); RunQuery(); };
            _input.TextChanged += delegate {
                _version++; if (_query != null) _query.Cancel();
                _animator.Stop();
                _debounce.Stop();
                if (_input.TextLength == 0) { RefreshEmpty(); if (Visible && !_fullScreen) { Height = _includeGrid ? Math.Min(600, Screen.FromControl(this).WorkingArea.Height - 40) : 125; _canvas.Height = Math.Max(120, Height - 170); _footer.Top = Height - 32; } }
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
            Deactivate += delegate { if (!_presenting && Settings.HideOnDeactivate) HideLauncher(); };
            _index.Changed += IndexChanged;
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; HideLauncher(); } };
        }
        private void IndexChanged(object sender, EventArgs args)
        {
            if (IsHandleCreated && !IsDisposed)
                try { BeginInvoke((Action)delegate { if (_input.TextLength == 0) RefreshEmpty(); }); } catch (InvalidOperationException) { }
        }
        public void ShowLauncher(bool includeGrid, bool fullScreen)
        {
            _animator.Stop();
            if (Visible) FinishHide();
            _presenting = true;
            _includeGrid = includeGrid; _fullScreen = fullScreen;
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (fullScreen)
            {
                Bounds = Screen.FromPoint(Cursor.Position).Bounds;
                _input.SetBounds((Width - 600) / 2, 72, 600, 38);
                _canvas.SetBounds(Math.Max(40, (Width - 1120) / 2), 170, Math.Min(1120, Width - 80), Math.Max(140, Height - 270));
            }
            else
            {
                Width = Math.Min(800, work.Width - 30); Height = includeGrid ? Math.Min(600, work.Height - 40) : 125;
                Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + Math.Min(150, Math.Max(20, (work.Height - Height) / 4)));
                _input.SetBounds(70, 34, Width - 115, 38);
                _canvas.SetBounds(24, 126, Width - 48, Math.Max(120, Height - 170));
            }
            _footer.SetBounds(24, Height - 32, Width - 48, 25);
            if (_backdrop != null) _backdrop.Dispose();
            _backdrop = GlassBackdrop.Capture(Bounds);
            _input.Clear(); RefreshEmpty(); _canvas.PrepareIcons();
            _canvas.AnimationsEnabled = Settings.AnimationsEnabled && Motion.SystemAllowsAnimation();
            GlassEffects.Apply(this); Opacity = 0.04;
            _animator.Open(Settings.AnimationsEnabled && Motion.SystemAllowsAnimation(), !fullScreen);
            Show(); Activate(); NativeMethods.SetForegroundWindow(Handle); _input.Focus(); _presenting = false;
        }
        private void RefreshEmpty()
        {
            _canvas.GridMode = _includeGrid || _fullScreen;
            _canvas.Visible = _canvas.GridMode;
            _canvas.SetItems(_engine.Recent(10000));
            _footer.Text = _canvas.GridMode ? "方向键选择     回车打开     滚轮翻页" : "应用、文件与文件夹";
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
            Height = Math.Min(work.Height - 24, 174 + Math.Max(1, count) * 62);
            Top = Math.Max(work.Top + 12, Math.Min(Top, work.Bottom - Height - 12));
            _canvas.Height = Math.Max(62, Height - 173); _footer.Top = Height - 32;
        }
        public void HideLauncher()
        {
            _debounce.Stop(); if (_query != null) _query.Cancel();
            if (!Visible) return;
            _animator.Close(Settings.AnimationsEnabled && Motion.SystemAllowsAnimation(), FinishHide);
        }
        private void FinishHide() { Hide(); if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; } }
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
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedShapes.Path(new RectangleF(_input.Left - 48, _input.Top - 14, _input.Width + 64, 64), 18))
            using (var fill = new SolidBrush(BackColor)) args.Graphics.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(183, 198, 218), 1.8f)) { args.Graphics.DrawEllipse(pen, _input.Left - 29, _input.Top + 6, 15, 15); args.Graphics.DrawLine(pen, _input.Left - 16, _input.Top + 20, _input.Left - 9, _input.Top + 27); }
            if (!_fullScreen) using (var border = new Pen(Color.FromArgb(75, 205, 218, 238))) using (var outline = RoundedShapes.Path(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 24)) args.Graphics.DrawPath(border, outline);
            using (var hintFont = new Font("Segoe UI", 9)) TextRenderer.DrawText(args.Graphics, "esc", hintFont, new Rectangle(Width - 60, Height - 33, 38, 24), Color.FromArgb(176, 188, 208), TextFormatFlags.HorizontalCenter);
        }
        protected override void OnSizeChanged(EventArgs args)
        {
            base.OnSizeChanged(args);
            if (Width < 48 || Height < 48) return;
            Region previous = Region;
            if (_fullScreen) Region = null;
            else using (var path = RoundedShapes.Path(new RectangleF(0, 0, Width, Height), 24)) Region = new Region(path);
            if (previous != null) previous.Dispose();
        }
        protected override void OnPaintBackground(PaintEventArgs args)
        {
            if (_backdrop == null) base.OnPaintBackground(args);
            else args.Graphics.DrawImage(_backdrop, ClientRectangle);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _animator.Dispose(); if (_backdrop != null) _backdrop.Dispose(); _index.Changed -= IndexChanged; _debounce.Dispose(); if (_query != null) _query.Cancel(); }
            base.Dispose(disposing);
        }
    }

}
