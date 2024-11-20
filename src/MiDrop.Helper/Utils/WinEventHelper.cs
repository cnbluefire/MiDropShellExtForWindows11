using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace MiDrop.Helper.Utils
{
    internal class WinEventHelper : IDisposable
    {
        private static ConcurrentDictionary<nint, WeakReference> winEventHelpers = new();
        private static DispatcherQueueController? dispatcherQueueController;
        private static Lock locker = new Lock();

        public static DispatcherQueue DispatcherQueue
        {
            get
            {
                if (dispatcherQueueController == null)
                {
                    lock (locker)
                    {
                        if (dispatcherQueueController == null)
                        {
                            dispatcherQueueController = DispatcherQueueInterop.CreateDispatcherQueueController(false);
                        }
                    }
                }
                return dispatcherQueueController.DispatcherQueue;
            }
        }

        private bool disposedValue;
        private nint hWinEventHook;

        public WinEventHelper(uint @event) : this(@event, @event) { }

        public unsafe WinEventHelper(uint minEvent, uint maxEvent)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (disposedValue) return;

                hWinEventHook = (nint)Windows.Win32.PInvoke.SetWinEventHook(
                    minEvent,
                    maxEvent,
                    default(Windows.Win32.Foundation.HMODULE),
                    &OnWinEventProc,
                    0,
                    0,
                    Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT | Windows.Win32.PInvoke.WINEVENT_SKIPOWNTHREAD);

                if (disposedValue)
                {
                    Windows.Win32.PInvoke.UnhookWinEvent((HWINEVENTHOOK)hWinEventHook);
                    Interlocked.Exchange(ref hWinEventHook, 0);
                }

                if (Enabled)
                {
                    winEventHelpers[hWinEventHook] = new WeakReference(this);
                }
            });
        }

        public bool Enabled => hWinEventHook != 0;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void OnWinEventProc(HWINEVENTHOOK hWinEventHook, uint @event, HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
        {
            if (winEventHelpers.TryGetValue(hWinEventHook, out var weakRef))
            {
                if (weakRef.Target is WinEventHelper strongThis)
                {
                    strongThis.OnWinEventReceived(@event, hwnd, idObject, idChild, idEventThread, dwmsEventTime);
                }
                else
                {
                    winEventHelpers.TryRemove(hWinEventHook.Value, out _);
                }
            }
        }

        private void OnWinEventReceived(uint @event, HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
        {
            WinEventReceived?.Invoke(this, new WinEventReceivedEventArgs(@event, hwnd.Value, idObject, idChild, idEventThread, dwmsEventTime));
        }

        public event WinEventReceivedEventHandler? WinEventReceived;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                var _hWinEventHook = Interlocked.Exchange(ref hWinEventHook, 0);

                if (_hWinEventHook != 0)
                {
                    Windows.Win32.PInvoke.UnhookWinEvent((HWINEVENTHOOK)_hWinEventHook);
                    WinEventReceived = null;
                    winEventHelpers.TryRemove(_hWinEventHook, out _);
                }

                disposedValue = true;
            }
        }

        ~WinEventHelper()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class WinEventReceivedEventArgs
    {
        public WinEventReceivedEventArgs(uint eventId, nint hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
        {
            EventId = eventId;
            Hwnd = hwnd;
            IdObject = idObject;
            IdChild = idChild;
            IdEventThread = idEventThread;
            DwmsEventTime = dwmsEventTime;
        }

        public uint EventId { get; }

        public nint Hwnd { get; }

        public int IdObject { get; }

        public int IdChild { get; }

        public uint IdEventThread { get; }

        public uint DwmsEventTime { get; }
    }

    public delegate void WinEventReceivedEventHandler(object? sender, WinEventReceivedEventArgs args);
}
