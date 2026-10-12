using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace FocusSearch
{
    internal static class GlassEffects
    {
        public static void Apply(Form form)
        {
            try
            {
                int rounded = 2;
                NativeMethods.DwmSetWindowAttribute(form.Handle, 33, ref rounded, 4);
                var policy = new NativeMethods.AccentPolicy { State = 4, Flags = 2, Color = unchecked((int)0xB82A2522) };
                IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(policy));
                try
                {
                    Marshal.StructureToPtr(policy, memory, false);
                    var data = new NativeMethods.CompositionData { Attribute = 19, Data = memory, Size = Marshal.SizeOf(policy) };
                    if (NativeMethods.SetWindowCompositionAttribute(form.Handle, ref data) == 0) form.Opacity = 0.96;
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
            catch (EntryPointNotFoundException) { form.Opacity = 0.96; }
            catch (DllNotFoundException) { form.Opacity = 0.96; }
        }
    }
}
