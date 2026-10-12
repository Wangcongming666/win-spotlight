using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FocusSearch
{
    internal class DesktopDialog : Form
    {
        protected static readonly Color Ink = Color.FromArgb(43, 51, 64);
        protected static readonly Color Muted = Color.FromArgb(100, 113, 132);
        private readonly List<Font> _fonts = new List<Font>();
        private readonly Icon _icon;
        public DesktopDialog(string title, Size size)
        {
            Text = title; ClientSize = size;
            Font = OwnFont(9, FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6, 12);
            BackColor = Color.FromArgb(246, 248, 251); ForeColor = Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            _icon = ApplicationIcon.Create(); Icon = _icon;
        }
        protected Font OwnFont(float size, FontStyle style)
        { var font = new Font("Microsoft YaHei UI", size, style); _fonts.Add(font); return font; }
        protected Label Copy(string text, bool secondary)
        {
            return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill,
                ForeColor = secondary ? Muted : Ink, Margin = new Padding(0, 0, 0, 8) };
        }
        protected TableLayoutPanel Stack()
        {
            var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Dock = DockStyle.Top, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return panel;
        }
        protected void AddHeading(TableLayoutPanel parent, string title)
        {
            var label = Copy(title, false); label.Font = OwnFont(10, FontStyle.Bold);
            label.Margin = new Padding(0, 12, 0, 8); parent.Controls.Add(label);
        }
        protected Button Footer(string primaryText, EventHandler onPrimary, bool cancel)
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(24, 12, 24, 12), BackColor = Color.FromArgb(239, 243, 248) };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var primary = new DialogButton { Text = primaryText, Primary = true, Size = new Size(82, 34), Margin = Padding.Empty };
            primary.Click += onPrimary; actions.Controls.Add(primary);
            if (cancel)
            {
                var back = new DialogButton { Text = "取消", Size = new Size(82, 34), Margin = new Padding(0, 0, 10, 0), DialogResult = DialogResult.Cancel };
                actions.Controls.Add(back); CancelButton = back;
            }
            else { primary.DialogResult = DialogResult.Cancel; CancelButton = primary; }
            AcceptButton = primary; footer.Controls.Add(actions); Controls.Add(footer); return primary;
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int round = 2; NativeMethods.DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { _icon.Dispose(); foreach (var font in _fonts) font.Dispose(); }
        }
    }
    internal sealed class DialogButton : Button
    {
        public bool Primary { get; set; }
        private bool _hover, _pressed;
        public DialogButton()
        { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (_pressed ? Color.FromArgb(35, 85, 153) : _hover ? Color.FromArgb(42, 99, 174) : Color.FromArgb(49, 108, 186))
                : (_hover ? Color.FromArgb(225, 233, 245) : Color.White);
            using (var shape = RoundedShapes.Path(new RectangleF(.5f, .5f, Width - 1, Height - 1), 7))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Primary ? fill : Color.FromArgb(205, 215, 230)))
            { e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Primary ? Color.White : Color.FromArgb(43, 51, 64),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), Primary ? Color.White : Color.FromArgb(49, 108, 186), fill);
        }
    }
    internal sealed class HotkeyField : Panel
    {
        public HotkeyField()
        { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(10, 7, 10, 6); Height = 34; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedShapes.Path(new RectangleF(.5f, .5f, Width - 1, Height - 1), 6))
            using (var pen = new Pen(ContainsFocus ? Color.FromArgb(49, 108, 186) : Color.FromArgb(205, 215, 230))) e.Graphics.DrawPath(pen, path);
        }
    }
}
