using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class LauncherCloseButton : Button
    {
        private bool _hover;
        public LauncherCloseButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand; TabStop = false;
            AccessibleName = "收起面板（Esc）"; Text = "关闭";
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null) { base.OnPaintBackground(e); return; }
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(-Left, -Top);
            using (var parentPaint = new PaintEventArgs(e.Graphics, Bounds))
            { InvokePaintBackground(Parent, parentPaint); InvokePaint(Parent, parentPaint); }
            e.Graphics.Restore(state);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_hover || Focused) using (var path = RoundedShapes.Path(new RectangleF(0, 0, Width, Height), 6))
                using (var brush = new SolidBrush(Color.FromArgb(30, 221, 233, 250))) e.Graphics.FillPath(brush, path);
            float cx = Width / 2f, cy = Height / 2f;
            using (var pen = new Pen(ForeColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            { e.Graphics.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4); e.Graphics.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4); }
        }
    }
}
