using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class WindowAnimator : IDisposable
    {
        private readonly Form _window;
        private readonly Timer _timer = new Timer { Interval = 15 };
        private readonly Stopwatch _clock = new Stopwatch();
        private Point _anchor;
        private double _from;
        private bool _translate;
        private Action _completed;
        public bool IsClosing { get; private set; }
        public WindowAnimator(Form window) { _window = window; _timer.Tick += Tick; }
        public void Open(bool enabled, bool translate)
        {
            _timer.Stop(); IsClosing = false; _completed = null; _anchor = _window.Location; _translate = translate;
            if (!enabled) { _window.Opacity = 1; return; }
            _window.Opacity = 0.04; if (_translate) _window.Top = _anchor.Y + 10;
            _clock.Restart(); _timer.Start();
        }
        public void Close(bool enabled, Action completed)
        {
            if (IsClosing) return;
            _timer.Stop(); IsClosing = true; _anchor = _window.Location; _from = _window.Opacity; _completed = completed;
            if (!enabled) { IsClosing = false; _completed = null; completed(); return; }
            _clock.Restart(); _timer.Start();
        }
        private void Tick(object sender, EventArgs args)
        {
            if (_window.IsDisposed) { _timer.Stop(); return; }
            double progress = Math.Min(1, _clock.Elapsed.TotalMilliseconds / (IsClosing ? 120 : 180));
            double eased = Motion.EaseOut(progress);
            _window.Opacity = IsClosing ? Math.Max(0.02, _from * (1 - eased)) : 0.04 + 0.96 * eased;
            if (_translate) _window.Top = _anchor.Y + (int)Math.Round(IsClosing ? 6 * eased : 10 * (1 - eased));
            if (progress < 1) return;
            _timer.Stop();
            if (IsClosing) { IsClosing = false; Action completed = _completed; _completed = null; if (completed != null) completed(); }
            else { _window.Location = _anchor; _window.Opacity = 1; }
        }
        public void Stop()
        {
            bool running = _timer.Enabled; _timer.Stop(); _completed = null; IsClosing = false;
            if (running && !_window.IsDisposed) _window.Location = _anchor;
            if (running && !_window.IsDisposed) _window.Opacity = 1;
        }
        public void Dispose() { _timer.Stop(); _timer.Dispose(); }
    }
}
