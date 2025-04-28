using Microsoft.Win32;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace MiDrop.Core
{
    public static class HuaweiPCManagerHelper
    {
        private static Dictionary<string, HuaweiShareModule> modules = new Dictionary<string, HuaweiShareModule>();

        private static readonly IReadOnlyList<string> PcManagerRegKeys = [
            // 5.x
            "{9557F42F-BD61-4E26-9752-33A8A20FC9F9}",
        ];

        public static string GetInstallPath()
        {
            try
            {
                for (int i = 0; i < PcManagerRegKeys.Count; i++)
                {
                    using (var subKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{PcManagerRegKeys[i]}\\InprocServer32"))
                    {
                        if (subKey != null && subKey.GetValue(null) is string path && !string.IsNullOrEmpty(path))
                        {
                            var folder = Path.GetDirectoryName(path);
                            if (Directory.Exists(folder))
                            {
                                return folder;
                            }
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        public static async Task LaunchApp()
        {
            var path = GetInstallPath();
            var appPath = Path.Combine(path, "PCManager.exe");
            await Task.Run(() =>
            {
                Process.Start(appPath);
            });
        }

        public static async Task<bool> SendFilesAsync(IReadOnlyList<string> items)
        {
            var path = GetInstallPath();
            var dllPath = Path.Combine(path, "ShareMenuControl.dll");

            if (!File.Exists(dllPath)) return false;

            return await Task.Run(() => SendToHuaweiShareDllCore(dllPath, items));
        }

        internal static unsafe bool SendToHuaweiShareDllCore(string dllPath, IReadOnlyList<string> items)
        {
            HuaweiShareModule? module;
            lock (modules)
            {
                if (!modules.TryGetValue(dllPath, out module))
                {
                    module = new HuaweiShareModule(dllPath);
                    modules[dllPath] = module;
                }
            }

            Console.WriteLine($"module is null: {module.IsNull}");

            if (!module.IsNull)
            {
                lock (module)
                {
                    var dataObject = CreateDataObject(items);
                    Console.WriteLine($"dataObject: {(nint)dataObject}");

                    if (dataObject != null)
                    {
                        var hr = module.Initialize(null, dataObject, default);
                        Console.WriteLine($"Initialize: {hr}");
                        if (hr.Failed) return false;

                        var hMenu = PInvoke.CreatePopupMenu();
                        try
                        {
                            const int SCRATCH_QCM_FIRST = 1;
                            const int SCRATCH_QCM_LAST = 0x7FFF;
                            const int CMF_NORMAL = 0;

                            hr = module.QueryContextMenu(hMenu, 0, SCRATCH_QCM_FIRST, SCRATCH_QCM_LAST, CMF_NORMAL);
                            Console.WriteLine($"QueryContextMenu: {hr}");
                            if (hr.Failed) return false;

                            CMINVOKECOMMANDINFO info = default;
                            info.cbSize = (uint)sizeof(CMINVOKECOMMANDINFO);
                            info.hwnd = default;
                            info.lpVerb = default;
                            hr = module.InvokeCommand(&info);
                            Console.WriteLine($"InvokeCommand: {hr}");
                            return hr.Succeeded;
                        }
                        finally
                        {
                            PInvoke.DestroyMenu(hMenu);
                        }
                    }
                }
            }

            return false;
        }

        private static unsafe IDataObject* CreateDataObject(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0) return null;

            var maxCount = Math.Min(items.Count, 100);
            int count = 0;
            ITEMIDLIST** pidls = stackalloc ITEMIDLIST*[maxCount];
            IShellItemArray* array = null;
            try
            {
                for (int i = 0; i < maxCount; i++)
                {
                    var pidl = PInvoke.ILCreateFromPath(items[i]);
                    if (pidl != null) pidls[count++] = pidl;
                }

                if (count > 0)
                {
                    var hr = PInvoke.SHCreateShellItemArrayFromIDLists((uint)count, pidls, &array);
                    if (hr.Succeeded)
                    {
                        hr = array->BindToHandler(null, PInvoke.BHID_DataObject, IDataObject.IID_Guid, out var ppv);
                        if (hr.Succeeded)
                        {
                            return (IDataObject*)ppv;
                        }
                    }
                }

                return null;
            }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    PInvoke.ILFree(pidls[i]);
                }
                if (array != null) array->Release();
            }
        }

        private class HuaweiShareModule
        {
            private HMODULE module;
            private nint pInitialize;
            private nint pQueryContextMenu;
            private nint pInvokeCommand;

            public HuaweiShareModule(string dllPath)
            {
                module = PInvoke.LoadLibraryEx(dllPath, 0);
                if (!module.IsNull)
                {
                    pInitialize = PInvoke.GetProcAddress(module, "Initialize");
                    pQueryContextMenu = PInvoke.GetProcAddress(module, "QueryContextMenu");
                    pInvokeCommand = PInvoke.GetProcAddress(module, "InvokeCommand");
                }
            }

            public bool IsNull => pInitialize == 0 || pQueryContextMenu == 0 || pInvokeCommand == 0;

            public unsafe HRESULT Initialize(ITEMIDLIST* pidlFolder, IDataObject* pdtobj, Windows.Win32.System.Registry.HKEY hkeyProgID)
            {
                if (pInitialize == 0) return (HRESULT)(unchecked((int)0x80004001));
                return ((delegate* unmanaged[Stdcall]<ITEMIDLIST*, IDataObject*, Windows.Win32.System.Registry.HKEY, HRESULT>)pInitialize)(pidlFolder, pdtobj, hkeyProgID);
            }

            public unsafe HRESULT QueryContextMenu(Windows.Win32.UI.WindowsAndMessaging.HMENU hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
            {
                if (pQueryContextMenu == 0) return (HRESULT)(unchecked((int)0x80004001));
                return ((delegate* unmanaged[Stdcall]<Windows.Win32.UI.WindowsAndMessaging.HMENU, uint, uint, uint, uint, HRESULT>)pQueryContextMenu)(hmenu, indexMenu, idCmdFirst, idCmdLast, uFlags);
            }

            public unsafe HRESULT InvokeCommand(CMINVOKECOMMANDINFO* pici)
            {
                if (pInvokeCommand == 0) return (HRESULT)(unchecked((int)0x80004001));
                return ((delegate* unmanaged[Stdcall]<CMINVOKECOMMANDINFO*, HRESULT>)pInvokeCommand)(pici);
            }
        }
    }
}
