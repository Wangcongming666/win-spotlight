using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace FocusSearch
{
    internal static class GlassEffects
    {
        public static void Apply(Form form, bool compact = false)
        {
            try
            {
                int rounded = compact ? 1 : 2;
                NativeMethods.DwmSetWindowAttribute(form.Handle, 33, ref rounded, 4);
                int dark = GlassPalette.IsDark ? 1 : 0;
                NativeMethods.DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                int border = -2;
                NativeMethods.DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
                int inset = compact ? 0 : 1;
                var margins = new NativeMethods.Margins { Left = inset, Right = inset, Top = inset, Bottom = inset };
                NativeMethods.DwmExtendFrameIntoClientArea(form.Handle, ref margins);
                // Acrylic composition and layered WinForms child EDIT windows produce
                // opaque strips and alpha artifacts. GlassBackdrop already provides
                // the blur; keep native DWM corners without a second blur compositor.
            }
            catch (EntryPointNotFoundException) { form.Opacity = 0.96; }
            catch (DllNotFoundException) { form.Opacity = 0.96; }
        }
    }
}
