using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace FocusSearch
{
    internal sealed class SearchInput : TextBox
    {
        private string _hint = "聚焦搜索";
        public string Hint { get { return _hint; } set { _hint = value; if (IsHandleCreated) SendMessage(Handle, 0x1501, new IntPtr(1), _hint); } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, string text);
        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            SendMessage(Handle, 0x1501, new IntPtr(1), _hint);
        }
    }
}
