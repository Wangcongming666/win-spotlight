using System;
using System.Windows.Forms;
namespace FocusSearch
{
    internal static class GridNavigation
    {
        public static int Move(int current, int count, int columns, int pageSize, Keys key, bool grid)
        {
            if (count <= 0) return 0;
            int delta = key == Keys.Down ? (grid ? columns : 1) : key == Keys.Up ? -(grid ? columns : 1) : key == Keys.Left ? -1 : key == Keys.Right ? 1 : key == Keys.PageDown ? pageSize : -pageSize;
            return Math.Max(0, Math.Min(count - 1, current + delta));
        }
    }
}
