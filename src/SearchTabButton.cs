using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class SearchTabButton : Button
    {
        public SearchScope Scope { get; set; }
        public bool Selected { get; set; }
        private bool _hover, _pressed;
        public SearchTabButton()
        { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null) { base.OnPaintBackground(e); return; }
            var state = e.Graphics.Save(); e.Graphics.TranslateTransform(-Left, -Top);
            using (var paint = new PaintEventArgs(e.Graphics, Bounds)) InvokePaintBackground(Parent, paint);
            e.Graphics.Restore(state);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float inset = _pressed ? 3 : 1.5f;
            RectangleF circle = new RectangleF(inset, inset, Width - inset * 2, Height - inset * 2);
            using (var fill = new SolidBrush(SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(_hover ? 235 : 205, GlassPalette.Surface))) g.FillEllipse(fill, circle);
            using (var pen = new Pen(Selected || Focused ? GlassPalette.Selection : Color.FromArgb(GlassPalette.IsDark ? 95 : 235, Color.White), Selected || Focused ? 2 : 1.3f)) g.DrawEllipse(pen, circle);
            using (var pen = new Pen(Color.FromArgb(45, GlassPalette.Secondary))) g.DrawEllipse(pen, inset + 1.5f, inset + 1.5f, Width - inset * 2 - 3, Height - inset * 2 - 3);
            var state = g.Save(); g.TranslateTransform((Width - 24) / 2f, (Height - 24) / 2f);
            using (var pen = new Pen(Enabled ? Selected ? GlassPalette.Selection : GlassPalette.Text : GlassPalette.Secondary, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (Scope == SearchScope.Applications) { g.DrawLine(pen, 7, 21, 16, 3); g.DrawLine(pen, 8, 3, 17, 21); g.DrawLine(pen, 3, 16, 21, 16); g.DrawLine(pen, 5, 20, 7, 16); }
                else if (Scope == SearchScope.Folders) { g.DrawLines(pen, new[] { new Point(2, 20), new Point(2, 4), new Point(9, 4), new Point(12, 7), new Point(22, 7), new Point(22, 20), new Point(2, 20) }); g.DrawLine(pen, 5, 10, 19, 10); }
                else if (Scope == SearchScope.All) { g.DrawPolygon(pen, new[] { new Point(12, 2), new Point(22, 8), new Point(12, 14), new Point(2, 8) }); g.DrawLines(pen, new[] { new Point(2, 13), new Point(12, 19), new Point(22, 13) }); g.DrawLines(pen, new[] { new Point(2, 18), new Point(12, 24), new Point(22, 18) }); }
                else { g.DrawLines(pen, new[] { new Point(7, 21), new Point(7, 2), new Point(16, 2), new Point(22, 8), new Point(22, 21), new Point(7, 21) }); g.DrawLines(pen, new[] { new Point(16, 2), new Point(16, 8), new Point(22, 8) }); g.DrawLines(pen, new[] { new Point(4, 6), new Point(2, 6), new Point(2, 24), new Point(16, 24) }); }
            }
            g.Restore(state);
        }
    }
}
