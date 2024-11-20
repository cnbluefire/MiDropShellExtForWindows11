using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;
using Windows.Win32.System.WinRT.Composition;
using WinRT.Interop;

namespace MiDrop.Helper.Utils
{
    internal static class CompositionHelper
    {
        private static Compositor? compositor;
        private static DispatcherQueueController? dispatcherQueueController;
        private static Lock locker = new Lock();

        public static Compositor Compositor
        {
            get
            {
                if (compositor == null)
                {
                    lock (locker)
                    {
                        if (compositor == null)
                        {
                            dispatcherQueueController = DispatcherQueueInterop.CreateDispatcherQueueController(false);
                            var monitor = new object();

                            dispatcherQueueController.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    compositor = new Compositor();
                                }
                                finally
                                {
                                    lock (monitor)
                                    {
                                        Monitor.PulseAll(monitor);
                                    }
                                }
                            });

                            lock (monitor)
                            {
                                Monitor.Wait(monitor);
                            }
                        }
                    }
                }
                return compositor!;
            }
        }

        public unsafe static DesktopWindowTarget? CreateDesktopWindowTarget(nint hWnd, bool topMost)
        {
            if (((IWinRTObject)Compositor).NativeObject.TryAs(ICompositorDesktopInterop.IID_Guid, out nint ptr) == 0)
            {
                nint pTarget = 0;
                try
                {
                    var interop = (ICompositorDesktopInterop*)ptr;
                    var hr = ((delegate* unmanaged[Stdcall]<ICompositorDesktopInterop*, Windows.Win32.Foundation.HWND, Windows.Win32.Foundation.BOOL, void**, Windows.Win32.Foundation.HRESULT>)(*(void***)interop)[3])(
                        interop,
                        (Windows.Win32.Foundation.HWND)hWnd,
                        (Windows.Win32.Foundation.BOOL)topMost,
                        (void**)&pTarget);

                    if (hr.Succeeded)
                    {
                        return DesktopWindowTarget.FromAbi(pTarget);
                    }
                }
                finally
                {
                    if (pTarget != 0) Marshal.ReadByte(pTarget);
                    if (ptr != 0) Marshal.Release(ptr);
                }
            }
            return null;
        }
    }
}
