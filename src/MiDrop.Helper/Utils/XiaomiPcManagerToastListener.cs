using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32.Foundation;
using Windows.Win32.System.Variant;
using WinRT.Interop;

namespace MiDrop.Helper.Utils
{
    internal static class XiaomiPcManagerToastListener
    {
        private static int initialized;
        private static WinEventHelper? winEventHelper;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;

            WinEventHelper.DispatcherQueue.TryEnqueue(() =>
            {
                var toast = FindCameraErrorToast();
                if (toast != 0)
                {
                    WindowHelper.HideWindow(toast);
                }

                winEventHelper = new WinEventHelper(Windows.Win32.PInvoke.EVENT_OBJECT_CREATE);
                winEventHelper.WinEventReceived += WinEventHelper_WinEventReceived;
            });
        }

        private static void WinEventHelper_WinEventReceived(object? sender, WinEventReceivedEventArgs args)
        {
            if (args.EventId == Windows.Win32.PInvoke.EVENT_OBJECT_CREATE
                && args.IdObject == (int)Windows.Win32.UI.WindowsAndMessaging.OBJECT_IDENTIFIER.OBJID_WINDOW)
            {
                if (WindowHelper.GetClassName(args.Hwnd) == "WinUIDesktopWin32WindowClass")
                {
                    WinEventHelper.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(1000);
                        if (IsCameraErrorToast(args.Hwnd))
                        {
                            WindowHelper.HideWindow(args.Hwnd);
                        }
                    });
                }
            }
        }

        public unsafe static nint FindCameraErrorToast()
        {
            nint result = 0;
            Windows.Win32.PInvoke.EnumWindows(&EnumWindowProc, (nint)(&result));

            return result;

            [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
            static BOOL EnumWindowProc(HWND hWnd, LPARAM lParam)
            {
                if (IsCameraErrorToast(hWnd.Value))
                {
                    *((nint*)lParam.Value) = hWnd.Value;
                    return false;
                }
                return true;
            }
        }

        public unsafe static bool IsCameraErrorToast(nint hWnd)
        {
            return WindowHelper.GetClassName(hWnd) == "WinUIDesktopWin32WindowClass"
                && CheckProcessName((HWND)hWnd)
                && CheckWindowContent((HWND)hWnd);

            static unsafe bool CheckProcessName(HWND hWnd)
            {
                uint processId = 0;
                var threadId = Windows.Win32.PInvoke.GetWindowThreadProcessId(hWnd, &processId);
                if (processId != 0)
                {
                    using var handle = Windows.Win32.PInvoke.OpenProcess_SafeHandle(
                        Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION
                        | Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ,
                        false,
                        processId);

                    if (!handle.IsInvalid)
                    {
                        uint size = 65535;

                        using var memoryOwner = MemoryPool<char>.Shared.Rent((int)size);
                        using var memoryHandle = memoryOwner.Memory.Pin();

                        if (Windows.Win32.PInvoke.QueryFullProcessImageName(
                            handle,
                            Windows.Win32.System.Threading.PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32,
                            (char*)memoryHandle.Pointer,
                            ref size) && size > 0)
                        {
                            var exeFile = new string((char*)memoryHandle.Pointer, 0, (int)size);
                            try
                            {
                                var fileName = System.IO.Path.GetFileName(exeFile);
                                if (fileName == "XiaomiPcManager.exe")
                                {
                                    return true;
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            var err = Marshal.GetLastWin32Error();
                        }
                    }
                }

                return false;
            }

            static unsafe bool CheckWindowContent(HWND hWnd)
            {
                fixed (Guid* riid = &Windows.Win32.UI.Accessibility.IAccessible.IID_Guid)
                {
                    Windows.Win32.UI.Accessibility.IAccessible* root = null;
                    Windows.Win32.UI.Accessibility.IAccessible* text = null;

                    try
                    {

                        var hr = Windows.Win32.PInvoke.AccessibleObjectFromWindow(
                            hWnd,
                            (uint)Windows.Win32.UI.WindowsAndMessaging.OBJECT_IDENTIFIER.OBJID_WINDOW,
                            riid,
                            (void**)&root);

                        if (hr.Succeeded)
                        {
                            text = SearchTextElement(root, "相机协同异常", StringComparison.OrdinalIgnoreCase);
                            if (text != null)
                            {
                                return true;
                            }
                        }
                    }
                    finally
                    {
                        if (text != null) text->Release();
                        if (root != null) root->Release();
                    }
                }

                return false;
            }

            static unsafe Windows.Win32.UI.Accessibility.IAccessible* SearchTextElement(Windows.Win32.UI.Accessibility.IAccessible* root, string searchText, StringComparison stringComparison)
            {
                fixed (Guid* riid = &Windows.Win32.UI.Accessibility.IAccessible.IID_Guid)
                {
                    if (root != null && root->get_accChildCount(out var count).Succeeded)
                    {
                        Span<VARIANT> span = new VARIANT[count];
                        if (Windows.Win32.PInvoke.AccessibleChildren(root, 0, span, out var count2).Succeeded)
                        {
                            span = span.Slice(0, count2);

                            try
                            {


                                for (int i = 0; i < span.Length; i++)
                                {
                                    Windows.Win32.PInvoke.VariantInit(out var variant);
                                    Windows.Win32.SysFreeStringSafeHandle? name = null;
                                    Windows.Win32.System.Com.IDispatch* dispatch = null;
                                    Windows.Win32.UI.Accessibility.IAccessible* childAccessible = null;

                                    try
                                    {
                                        if (span[i].Anonymous.Anonymous.vt == VARENUM.VT_DISPATCH)
                                        {
                                            dispatch = span[i].Anonymous.Anonymous.Anonymous.pdispVal;
                                        }
                                        else
                                        {
                                            if (root->get_accChild(span[i], &dispatch).Failed)
                                            {
                                                dispatch = null;
                                            }
                                        }

                                        if (dispatch != null && dispatch->QueryInterface(riid, (void**)&childAccessible).Succeeded)
                                        {
                                            variant.Anonymous.Anonymous.vt = VARENUM.VT_I4;
                                            variant.Anonymous.Anonymous.Anonymous.lVal = i;

                                            variant.Anonymous.Anonymous.Anonymous.lVal = (int)Windows.Win32.PInvoke.CHILDID_SELF;
                                            if (childAccessible->get_accRole(variant, out var role).Succeeded)
                                            {
                                                var roleValue = role.Anonymous.Anonymous.Anonymous.lVal;

                                                if (roleValue == Windows.Win32.PInvoke.ROLE_SYSTEM_STATICTEXT
                                                    || roleValue == Windows.Win32.PInvoke.ROLE_SYSTEM_TEXT)
                                                {
                                                    variant.Anonymous.Anonymous.Anonymous.lVal = (int)Windows.Win32.PInvoke.CHILDID_SELF;

                                                    HRESULT hr = default;

                                                    if (roleValue == Windows.Win32.PInvoke.ROLE_SYSTEM_STATICTEXT)
                                                    {
                                                        hr = childAccessible->get_accName(variant, out name);
                                                    }
                                                    else if (roleValue == Windows.Win32.PInvoke.ROLE_SYSTEM_TEXT)
                                                    {
                                                        hr = childAccessible->get_accValue(variant, out name);
                                                    }

                                                    if (hr.Succeeded && name != null && !name.IsInvalid)
                                                    {
                                                        var bstr = (BSTR)name.DangerousGetHandle();
                                                        var name2 = bstr.ToString();
                                                        if (name2.Contains(searchText, stringComparison))
                                                        {
                                                            childAccessible->AddRef();
                                                            return childAccessible;
                                                        }
                                                    }
                                                }
                                            }
                                            var v = SearchTextElement(childAccessible, searchText, stringComparison);
                                            if (v != null)
                                            {
                                                return v;
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        name?.Dispose();
                                        if (childAccessible != null) childAccessible->Release();
                                        Windows.Win32.PInvoke.VariantClear(&variant);
                                    }
                                }
                            }
                            finally
                            {
                                for (int i = 0; i < span.Length; i++)
                                {
                                    if (span[i].Anonymous.Anonymous.vt == VARENUM.VT_DISPATCH)
                                    {
                                        span[i].Anonymous.Anonymous.Anonymous.pdispVal->Release();
                                    }
                                }
                            }
                        }
                    }
                }
                return null;
            }
        }
    }
}
