using System;
using System.Collections.Generic;
using System.Linq;
namespace FocusSearch
{
    internal sealed class HotkeyCoordinator : IDisposable
    {
        internal sealed class Binding { public int Id; public bool Grid; public HotkeySpec Key; }
        private readonly Func<int, HotkeySpec, bool> _register;
        private readonly Action<int> _release;
        private List<Binding> _bindings = new List<Binding>();
        private int _nextId = 100;
        public HotkeyCoordinator(Func<int, HotkeySpec, bool> register, Action<int> release) { _register = register; _release = release; }
        public bool IsGrid(int id) { return _bindings.Any(b => b.Id == id && b.Grid); }
        public bool Apply(AppSettings settings)
        {
            using (var change = Prepare(settings)) { if (change == null) return false; change.Commit(); return true; }
        }
        public PendingChange Prepare(AppSettings settings)
        {
            var keys = new List<HotkeySpec> { settings.SearchHotkey };
            if (!settings.CombinedMode && settings.AppGridEnabled) keys.Add(settings.GridHotkey);
            if (keys.Any(k => k == null || k.VirtualKey == 0 || k.VirtualKey > 254 || k.Modifiers == 0 || (k.Modifiers & ~15u) != 0)) return null;
            if (keys.Count == 2 && Same(keys[0], keys[1])) return null;
            var next = new List<Binding>();
            for (int index = 0; index < keys.Count; index++)
            {
                var old = _bindings.FirstOrDefault(b => Same(b.Key, keys[index]));
                if (old != null) { next.Add(new Binding { Id = old.Id, Key = old.Key, Grid = index == 1 }); continue; }
                int id = _nextId++;
                if (!_register(id, keys[index]))
                {
                    foreach (var item in next.Where(b => !_bindings.Any(o => o.Id == b.Id))) _release(item.Id);
                    return null;
                }
                next.Add(new Binding { Id = id, Key = keys[index].Clone(), Grid = index == 1 });
            }
            return new PendingChange(this, next);
        }
        internal sealed class PendingChange : IDisposable
        {
            private readonly HotkeyCoordinator _owner;
            private readonly List<Binding> _next;
            private bool _finished;
            internal PendingChange(HotkeyCoordinator owner, List<Binding> next) { _owner = owner; _next = next; }
            public void Commit()
            {
                if (_finished) throw new InvalidOperationException("Hotkey change already completed.");
                foreach (var item in _owner._bindings.Where(b => !_next.Any(n => n.Id == b.Id))) _owner._release(item.Id);
                _owner._bindings = _next; _finished = true;
            }
            public void Dispose()
            {
                if (_finished) return;
                foreach (var item in _next.Where(b => !_owner._bindings.Any(o => o.Id == b.Id))) _owner._release(item.Id);
                _finished = true;
            }
        }
        private static bool Same(HotkeySpec a, HotkeySpec b) { return a.Modifiers == b.Modifiers && a.VirtualKey == b.VirtualKey; }
        public void Dispose() { foreach (var item in _bindings) _release(item.Id); _bindings.Clear(); }
    }
}
