using Microsoft.Win32;
using ShellExtensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ShellExt
{
    public static class DllMain
    {
        private static readonly Guid PackagedClsid = new Guid("976D43D8-907F-46AF-B47F-07084C71A2F0");

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        public static void _DllMain()
        {
            try
            {
                using (var subKey = Registry.ClassesRoot.OpenSubKey("CLSID\\{1bca9901-05c3-4d01-8ad4-78da2eac9b3f}\\InprocServer32"))
                {
                    if (subKey != null && subKey.GetValue(null) is string path && !string.IsNullOrEmpty(path))
                    {
                        var folder = Path.GetDirectoryName(path);
                        ShellExtensions.ShellExtensionsClassFactory.RegisterInProcess(PackagedClsid, () => new ContextMenu(folder!));
                    }
                }
            }
            catch { }
        }

        [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
        private static int DllCanUnloadNow() => ShellExtensions.ShellExtensionsClassFactory.DllCanUnloadNow();

        [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
        private unsafe static int DllGetClassObject(Guid* clsid, Guid* riid, void** ppv) => ShellExtensions.ShellExtensionsClassFactory.DllGetClassObject(clsid, riid, ppv);

        public static unsafe void SendToXiaomiPcManager(string[] files)
        {
            const string WindowClassName = "XiaomiPCManager";

            if (files != null && files.Length > 0)
            {
                fixed (char* pClassName = WindowClassName)
                {
                    var hWnd = FindWindowW(pClassName, null);

                    if (hWnd != 0)
                    {
                        var sb = new StringBuilder();
                        for (int i = 0; i < files.Length; i++)
                        {
                            var path = files[i];

                            if (!string.IsNullOrEmpty(path)
                                && Path.IsPathRooted(path))
                            {
                                if (i != 0) sb.Append('|');
                                sb.Append(path);
                            }
                        }

                        sb.Append('\0');
                        var file = sb.ToString();

                        fixed (char* ptr = file)
                        {
                            var s = default(COPYDATASTRUCT);
                            s.dwData = 0;
                            s.cbData = (file.Length) * 2;
                            s.lpData = (nint)ptr;

                            SendMessageW(hWnd, 74, (IntPtr)1, (nint)(&s));
                        }
                    }
                }
            }
        }


        [DllImport("user32.dll")]
        private static unsafe extern nint SendMessageW(nint hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static unsafe extern nint FindWindowW(char* lpClassName, char* lpWindowName);

        private struct COPYDATASTRUCT
        {
            public nint dwData;

            public int cbData;

            public nint lpData;
        }

    }
}
