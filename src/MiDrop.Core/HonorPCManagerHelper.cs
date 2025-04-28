using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MiDrop.Core
{
    public static class HonorPCManagerHelper
    {
        private static readonly IReadOnlyList<string> PcManagerRegKeys = [
            // 5.x
            "{9557F42F-BD61-4E26-9752-33A8A20FC9FA}",
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
            var dllPath = Path.Combine(path, "HonorFileShareMenuControl.dll");

            Console.WriteLine($"dllPath: {dllPath}");

            if (!File.Exists(dllPath)) return false;

            return await Task.Run(() => HuaweiPCManagerHelper.SendToHuaweiShareDllCore(dllPath, items));
        }
    }
}
