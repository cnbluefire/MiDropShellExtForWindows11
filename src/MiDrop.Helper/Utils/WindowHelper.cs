using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32.Foundation;

namespace MiDrop.Helper.Utils
{
    internal static class WindowHelper
    {
        internal static unsafe string? GetClassName(nint hWnd)
        {
            var buffer = stackalloc char[255];
            var count = Windows.Win32.PInvoke.GetClassName((HWND)hWnd, buffer, 255);
            if (count > 0) return new string(buffer, 0, count);
            return null;
        }

        internal static bool HideWindow(nint hWnd)
        {
            return Windows.Win32.PInvoke.SetWindowPos(
                (HWND)hWnd,
                default,
                0, 0, 0, 0,
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE
                | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE
                | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW);
        }
    }
}
