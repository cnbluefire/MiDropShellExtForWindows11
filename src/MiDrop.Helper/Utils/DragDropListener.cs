using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace MiDrop.Helper.Utils
{
    internal static class DragDropListener
    {
        private static readonly string[] DragVisualWindowClassNames =
            ["SysDragImage", "DragVisualWindow"];

        private static WinEventHelper? winEventHelper;
        private static DispatcherQueueTimer? timer;
        private static Lock locker = new Lock();

        private static EventHandler? dragDropStartedEventHandler;
        private static EventHandler? dragDropStoppedEventHandler;
        private static bool isDragging;

        public static bool IsDragging => isDragging;

        public static event EventHandler DragDropStarted
        {
            add
            {
                dragDropStartedEventHandler += value;
                UpdateHookState();
            }
            remove
            {
                dragDropStartedEventHandler -= value;
                UpdateHookState();
            }
        }

        public static event EventHandler DragDropStopped
        {
            add
            {
                dragDropStoppedEventHandler += value;
                UpdateHookState();
            }
            remove
            {
                dragDropStoppedEventHandler -= value;
                UpdateHookState();
            }
        }

        private static void StartTimer()
        {
            var timer = DragDropListener.timer;
            if (timer == null)
            {
                lock (locker)
                {
                    if (DragDropListener.timer == null)
                    {
                        timer = DragDropListener.timer = WinEventHelper.DispatcherQueue.CreateTimer();
                        timer.Tick += OnTimerTick;
                        timer.Interval = TimeSpan.FromSeconds(0.5);
                    }
                }
            }

            timer!.Stop();
            if (isDragging) timer.Start();
        }

        private static void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            const ushort VK_LBUTTON = 0x01;

            var flag = (Windows.Win32.PInvoke.GetAsyncKeyState(VK_LBUTTON) & 0x8000) == (0x8000);

            if (!flag)
            {
                for (int i = 0; i < DragVisualWindowClassNames.Length; i++)
                {
                    var hWnd = Windows.Win32.PInvoke.FindWindowEx(default, default, DragVisualWindowClassNames[i], null);
                    if (!hWnd.IsNull)
                    {
                        flag = true;
                        break;
                    }
                }
            }

            if (!flag)
            {
                isDragging = false;
                timer?.Stop();
                dragDropStoppedEventHandler?.Invoke(default, EventArgs.Empty);
            }
        }

        private static void WinEventHelper_WinEventReceived(object? sender, WinEventReceivedEventArgs args)
        {
            if (args.EventId == Windows.Win32.PInvoke.EVENT_OBJECT_CREATE
                && args.IdObject == (int)Windows.Win32.UI.WindowsAndMessaging.OBJECT_IDENTIFIER.OBJID_WINDOW)
            {
                if (Windows.Win32.PInvoke.IsWindow((HWND)args.Hwnd))
                {
                    var className = GetClassName(args.Hwnd);
                    if (DragVisualWindowClassNames.Contains(className))
                    {
                        isDragging = true;
                        StartTimer();
                        dragDropStartedEventHandler?.Invoke(default, EventArgs.Empty);
                    }
                }
            }
        }

        private static void UpdateHookState()
        {
            lock (locker)
            {
                var handler1 = dragDropStartedEventHandler;
                var handler2 = dragDropStoppedEventHandler;

                if (handler1 == null && handler2 == null)
                {
                    UninstallHook();
                }
                else
                {
                    InstallHook();
                }
            }
        }

        private static unsafe void InstallHook()
        {
            lock (locker)
            {
                if (winEventHelper == null)
                {
                    winEventHelper = new WinEventHelper(Windows.Win32.PInvoke.EVENT_OBJECT_CREATE, Windows.Win32.PInvoke.EVENT_OBJECT_CREATE);
                    winEventHelper.WinEventReceived += WinEventHelper_WinEventReceived;
                }
            }
        }

        private static void UninstallHook()
        {
            lock (locker)
            {
                if (winEventHelper != null)
                {
                    winEventHelper.WinEventReceived -= WinEventHelper_WinEventReceived;
                    winEventHelper.Dispose();
                    winEventHelper = null;

                    timer?.Stop();

                    if (isDragging)
                    {
                        isDragging = false;
                        dragDropStoppedEventHandler?.Invoke(default, EventArgs.Empty);
                    }
                }
            }
        }

        private static unsafe string? GetClassName(nint hWnd)
        {
            var buffer = stackalloc char[255];
            var count = Windows.Win32.PInvoke.GetClassName((HWND)hWnd, buffer, 255);
            if (count > 0) return new string(buffer, 0, count);
            return null;
        }
    }
}
