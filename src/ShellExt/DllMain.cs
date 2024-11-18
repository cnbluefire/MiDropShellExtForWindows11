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
            var launch = MiDropHelper.XiaomiPcManagerHelper.LaunchAsync(default).Result;
            if (launch)
            {
                MiDropHelper.XiaomiPcManagerHelper.SendFilesAsync(files, TimeSpan.FromSeconds(1)).Wait();
            }
        }
    }
}
