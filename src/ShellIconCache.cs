using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace FocusSearch
{
    internal sealed class ShellIconCache : IDisposable
    {
        private readonly Dictionary<string, Bitmap> _images = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly HashSet<string> _loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _workerGate = new object();
        private readonly Queue<LoadedImage> _ready = new Queue<LoadedImage>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly Bitmap _placeholder = Placeholder();
        private readonly Thread _worker;
        private SearchItem[] _desired = new SearchItem[0];
        private int _generation;
        private bool _closed;
        private readonly Func<SearchItem, Bitmap> _loader;
        private IImageList _jumbo;
        private bool _initialized;
        private int _genericIndex = -1;
        private sealed class LoadedImage { public string Key; public Bitmap Image; }
        public event EventHandler Ready;
        public ShellIconCache() : this(null) { }
        public ShellIconCache(Func<SearchItem, Bitmap> loader)
        {
            _loader = loader;
            _worker = new Thread(LoadWorker) { IsBackground = true, Name = "FocusSearch.IconLoader" };
            _worker.SetApartmentState(ApartmentState.STA); _worker.Start();
        }
        public static string Key(SearchItem item) { return (string.IsNullOrEmpty(item.IconKey) ? item.Path : item.IconKey) + "|" + item.Name; }

        public Bitmap Get(SearchItem item)
        {
            string key = Key(item);
            Bitmap bitmap; if (_images.TryGetValue(key, out bitmap)) return bitmap;
            return _placeholder;
        }
        public void Prefetch(IEnumerable<SearchItem> items)
        { Prefetch(items, new SearchItem[0]); }
        public void Prefetch(IEnumerable<SearchItem> items, IEnumerable<SearchItem> visible)
        {
            var ordered = new List<SearchItem>(); var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in visible) { string key = Key(item); if (keys.Add(key)) { ordered.Add(item); pins.Add(key); } if (ordered.Count >= 96) break; }
            foreach (var item in items) { if (ordered.Count >= 96) break; if (keys.Add(Key(item))) ordered.Add(item); }
            _protected = pins; _wanted = keys; DrainReady();
            var requested = new List<SearchItem>(); foreach (var item in ordered) if (!_loaded.Contains(Key(item))) requested.Add(item);
            lock (_workerGate) { if (_closed) return; _desired = requested.ToArray(); _generation++; _signal.Set(); }
        }
        public void Pause() { lock (_workerGate) { if (_closed) return; _desired = new SearchItem[0]; _generation++; _signal.Set(); } }
        public int DrainReady()
        {
            var images = new List<LoadedImage>();
            lock (_workerGate) { while (_ready.Count > 0) images.Add(_ready.Dequeue()); }
            foreach (var item in images)
            {
                if (!_wanted.Contains(item.Key)) { item.Image.Dispose(); continue; }
                Bitmap previous;
                if (_images.TryGetValue(item.Key, out previous)) previous.Dispose();
                else
                {
                    if (_images.Count >= 96)
                    {
                        string old = null; int candidates = _order.Count;
                        for (int i = 0; i < candidates; i++) { string candidate = _order.Dequeue(); if (_protected.Contains(candidate)) _order.Enqueue(candidate); else { old = candidate; break; } }
                        if (old == null) { item.Image.Dispose(); continue; }
                        _images[old].Dispose(); _images.Remove(old); _loaded.Remove(old);
                    }
                    _order.Enqueue(item.Key);
                }
                _images[item.Key] = item.Image; _loaded.Add(item.Key);
            }
            return images.Count;
        }
        private void LoadWorker()
        {
            try
            {
                while (true)
                {
                    _signal.WaitOne(); SearchItem[] desired; int version;
                    lock (_workerGate) { if (_closed) break; desired = _desired; version = _generation; }
                    foreach (var item in desired)
                    {
                        lock (_workerGate) { if (_closed || version != _generation) break; }
                        string path = string.IsNullOrEmpty(item.IconKey) ? item.Path : item.IconKey;
                        Bitmap image = (_loader == null ? Load(path) : _loader(item)) ?? Badge(item.Name);
                        lock (_workerGate) { if (_closed) { image.Dispose(); break; } _ready.Enqueue(new LoadedImage { Key = Key(item), Image = image }); }
                        EventHandler handler = Ready; if (handler != null) handler(this, EventArgs.Empty);
                    }
                }
            }
            finally { if (_jumbo != null) { Marshal.ReleaseComObject(_jumbo); _jumbo = null; } _signal.Dispose(); }
        }
        private static Bitmap Placeholder()
        {
            var image = new Bitmap(80, 80);
            using (var graphics = Graphics.FromImage(image))
            using (var shape = RoundedShapes.Path(new RectangleF(4, 4, 72, 72), 18))
            using (var brush = new SolidBrush(Color.FromArgb(35, 219, 232, 250)))
            { graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.FillPath(brush, shape); }
            return image;
        }
        private Bitmap Load(string path)
        {
            if (!_initialized)
            {
                _initialized = true;
                try { Guid iid = typeof(IImageList).GUID; NativeMethods.SHGetImageList(4, ref iid, out _jumbo); }
                catch (Exception) { }
                var generic = new NativeMethods.ShellFileInfo();
                NativeMethods.SHGetFileInfo("unknown.focus-file", 0x80, ref generic, (uint)Marshal.SizeOf(generic), 0x4000 | 0x10);
                _genericIndex = generic.Index;
            }
            var info = new NativeMethods.ShellFileInfo();
            if (NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), 0x4000) == IntPtr.Zero || info.Index == _genericIndex) return null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                if (_jumbo != null) _jumbo.GetIcon(info.Index, 1, out handle);
                if (handle == IntPtr.Zero) { NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), 0x100); handle = info.Icon; }
                if (handle == IntPtr.Zero) return null;
                using (var icon = (Icon)Icon.FromHandle(handle).Clone())
                using (var source = icon.ToBitmap())
                {
                    Rectangle content = ContentBounds(source);
                    var bitmap = new Bitmap(80, 80, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        double ratio = Math.Min(76.0 / content.Width, 76.0 / content.Height);
                        int width = (int)(content.Width * ratio), height = (int)(content.Height * ratio);
                        graphics.DrawImage(source, new Rectangle((80 - width) / 2, (80 - height) / 2, width, height), content, GraphicsUnit.Pixel);
                    }
                    return bitmap;
                }
            }
            catch (Exception) { return null; }
            finally { if (handle != IntPtr.Zero) NativeMethods.DestroyIcon(handle); }
        }
        private static Rectangle ContentBounds(Bitmap image)
        {
            var area = new Rectangle(0, 0, image.Width, image.Height);
            var data = image.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[data.Stride * data.Height]; Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                int left = image.Width, top = image.Height, right = -1, bottom = -1;
                for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                {
                    if (pixels[y * data.Stride + x * 4 + 3] < 12) continue;
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
                return right < left ? area : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            }
            finally { image.UnlockBits(data); }
        }
        private static Bitmap Badge(string name)
        {
            var colors = new[] { Color.FromArgb(63, 116, 180), Color.FromArgb(70, 139, 130), Color.FromArgb(134, 104, 167), Color.FromArgb(173, 117, 72), Color.FromArgb(174, 92, 108) };
            int hash = 0; foreach (char letter in name) hash = (hash * 31 + letter) % 997;
            var image = new Bitmap(80, 80);
            using (var graphics = Graphics.FromImage(image))
            using (var shape = RoundedShapes.Path(new RectangleF(4, 4, 72, 72), 18))
            using (var fill = new SolidBrush(colors[hash % colors.Length]))
            using (var font = new Font("Segoe UI", 25, FontStyle.Bold))
            using (var white = new SolidBrush(Color.White))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.FillPath(fill, shape);
                string initial = string.IsNullOrWhiteSpace(name) ? "·" : name.Trim().Substring(0, 1).ToUpperInvariant();
                graphics.DrawString(initial, font, white, new RectangleF(4, 4, 72, 72), format);
            }
            return image;
        }
        public void Dispose()
        {
            lock (_workerGate) { if (_closed) return; _closed = true; while (_ready.Count > 0) _ready.Dequeue().Image.Dispose(); _signal.Set(); }
            foreach (var image in _images.Values) image.Dispose(); _images.Clear();
            _placeholder.Dispose();
        }
    }
}
