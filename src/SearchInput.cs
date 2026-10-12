using System;
using System.Drawing;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class SearchInput : TextBox
    {
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            // TextLength sends WM_GETTEXTLENGTH; inspect it only for paint messages.
            // Reading it for every message recursively re-enters this WndProc.
            if (message.Msg != 0x000F && message.Msg != 0x0317 && message.Msg != 0x0318) return;
            if (TextLength != 0) return;
            if (message.Msg == 0x000F)
                using (var graphics = Graphics.FromHwnd(Handle)) DrawCue(graphics);
            else if ((message.Msg == 0x0317 || message.Msg == 0x0318) && message.WParam != IntPtr.Zero)
                using (var graphics = Graphics.FromHdc(message.WParam)) DrawCue(graphics);
        }
        private void DrawCue(Graphics graphics)
        {
            TextRenderer.DrawText(graphics, "搜索应用和文件", Font, ClientRectangle, Color.FromArgb(187, 200, 222), BackColor,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
