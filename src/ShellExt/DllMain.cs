using Microsoft.Win32;
using MiDrop.Core;
using ShellExtensions;
using System.Diagnostics;
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
                var folder = XiaomiPcManagerHelper.GetXiaomiPcManagerInstallPath();
                if (Directory.Exists(folder))
                {
                    ShellExtensions.ShellExtensionsClassFactory.RegisterInProcess(PackagedClsid, () => new ContextMenu(folder!));
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
            var installLocation = PackageProperties.Current?.PackageInstallLocation;
            if (!string.IsNullOrEmpty(installLocation))
            {
                var helperExecute = System.IO.Path.Combine(installLocation, "MiDrop.Helper", "MiDrop.Helper.exe");
                if (System.IO.File.Exists(helperExecute))
                {
                    var key = MiDrop.Core.FilesHelper.SaveFilesAsync(files, default).Result;
                    if (!string.IsNullOrEmpty(key))
                    {
                        try
                        {
                            Process.Start(helperExecute, $"--share-files {key}");
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
