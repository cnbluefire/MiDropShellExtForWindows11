using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace MiDrop.Helper.Utils
{
    internal static class DispatcherQueueInterop
    {
        internal static DispatcherQueueController CreateDispatcherQueueController(bool currentThread)
        {
            var options = new Windows.Win32.System.WinRT.DispatcherQueueOptions()
            {
                apartmentType = Windows.Win32.System.WinRT.DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                threadType = currentThread ? Windows.Win32.System.WinRT.DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT :
                    Windows.Win32.System.WinRT.DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_DEDICATED,
                dwSize = (uint)Marshal.SizeOf<Windows.Win32.System.WinRT.DispatcherQueueOptions>()
            };
            CreateDispatcherQueueController(options, out var result).ThrowOnFailure();
            return DispatcherQueueController.FromAbi(result);

            [DllImport("CoreMessaging.dll", ExactSpelling = true)]
            static extern Windows.Win32.Foundation.HRESULT CreateDispatcherQueueController(
                Windows.Win32.System.WinRT.DispatcherQueueOptions options,
                out nint dispatcherQueueController);

        }
    }
}
