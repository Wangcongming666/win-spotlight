using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FocusSearch
{
    internal static class FileLocationOpener
    {
        public static async Task OpenAsync(string path)
        {
            WindowsShellEnvironment.Prepare();
            path = Path.GetFullPath(path);
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("文件或文件夹已移动，无法打开所在位置。", path);

            var completion = new TaskCompletionSource<bool>();
            var worker = new Thread(delegate()
            {
                IntPtr item = IntPtr.Zero;
                bool initialized = false;
                try
                {
                    Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 2));
                    initialized = true;
                    item = ILCreateFromPathW(path);
                    if (item == IntPtr.Zero) throw new IOException("无法解析文件所在位置。");
                    // A zero selection count opens the parent and selects this item.
                    Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0));
                    completion.SetResult(true);
                }
                catch (Exception error) { completion.SetException(error); }
                finally
                {
                    if (item != IntPtr.Zero) ILFree(item);
                    if (initialized) CoUninitialize();
                }
            }) { IsBackground = true, Name = "FocusSearch.RevealLocation" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            if (await Task.WhenAny(completion.Task, Task.Delay(5000)).ConfigureAwait(false) != completion.Task)
            {
                // The native call cannot be cancelled; observe any later failure.
                var observation = completion.Task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("资源管理器没有响应，请稍后重试。");
            }
            await completion.Task.ConfigureAwait(false);
        }

        [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")] private static extern void CoUninitialize();
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern IntPtr ILCreateFromPathW(string path);
        [DllImport("shell32.dll", ExactSpelling = true)] private static extern int SHOpenFolderAndSelectItems(IntPtr item, uint count, IntPtr children, uint flags);
        [DllImport("shell32.dll", ExactSpelling = true)] private static extern void ILFree(IntPtr item);
    }
}
