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
        private static readonly Guid PackagedClsid1 = new Guid("976D43D8-907F-46AF-B47F-07084C71A2F0");
        private static readonly Guid PackagedClsid2 = new Guid("3EE2A0FD-EA1B-4630-9972-02F51EC786FF");
        private static readonly Guid PackagedClsid3 = new Guid("3FA37C0D-64B9-4E51-A9DF-411647E9DE79");

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        public static void _DllMain()
        {
            try
            {
                var folder = XiaomiPcManagerHelper.GetInstallPath();
                if (Directory.Exists(folder))
                {
                    ShellExtensions.ShellExtensionsClassFactory.RegisterInProcess(
                        PackagedClsid1,
                        () => new ContextMenu(
                            "XiaomiShare",
                            XiaomiPcManagerHelper.GetIconString(),
                            "使用小米互传发送"));
                }
            }
            catch { }

            try
            {
                var folder = HonorPCManagerHelper.GetInstallPath();
                if (Directory.Exists(folder))
                {
                    ShellExtensions.ShellExtensionsClassFactory.RegisterInProcess(
                        PackagedClsid2,
                        () => new ContextMenu(
                            "HonorShare",
                            Path.GetFullPath(Path.Combine(DllModule.BaseDirectory, "..", "HonorImages", "Share.ico")),
                            "使用荣耀分享发送"));
                }
            }
            catch { }

            try
            {
                var folder = HuaweiPCManagerHelper.GetInstallPath();
                if (Directory.Exists(folder))
                {
                    ShellExtensions.ShellExtensionsClassFactory.RegisterInProcess(
                        PackagedClsid3,
                        () => new ContextMenu(
                            "HuaweiShare",
                            Path.GetFullPath(Path.Combine(DllModule.BaseDirectory, "..", "HuaweiImages", "Share.ico")),
                            "使用华为分享发送"));
                }
            }
            catch { }
        }

        [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
        private static int DllCanUnloadNow() => ShellExtensions.ShellExtensionsClassFactory.DllCanUnloadNow();

        [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
        private unsafe static int DllGetClassObject(Guid* clsid, Guid* riid, void** ppv) => ShellExtensions.ShellExtensionsClassFactory.DllGetClassObject(clsid, riid, ppv);

        public static unsafe void StartShare(string target, string[] files)
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
                            Process.Start(helperExecute, $"--target {target} --share-files {key}");
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
