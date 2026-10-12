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
                int dark = 1;
                NativeMethods.DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                // Acrylic composition and layered WinForms child EDIT windows produce
                // opaque strips and alpha artifacts. GlassBackdrop already provides
                // the blur; keep native DWM corners without a second blur compositor.
            }
            catch (EntryPointNotFoundException) { form.Opacity = 0.96; }
            catch (DllNotFoundException) { form.Opacity = 0.96; }
        }
    }
}
