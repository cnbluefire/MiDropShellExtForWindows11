using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace MiDrop.Core;

public class XiaomiPcManagerHelper
{
    private const string ContextMenuMappingPrefix = @"Local\MiDropFileMappingObject_";
    private static readonly TimeSpan ContextMenuMappingLifetime = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyList<string> PcManagerRegKeys = [
        // 5.x
        "{504d69c0-cb52-48df-b5b5-7161829fabc8}",
        
        // 4.x
        "{1bca9901-05c3-4d01-8ad4-78da2eac9b3f}",
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
                        while (!string.IsNullOrEmpty(folder))
                        {
                            if (File.Exists(Path.Combine(folder, "hyperConnect.exe"))
                                || File.Exists(Path.Combine(folder, "XiaomiPcManager.exe"))
                                || File.Exists(Path.Combine(folder, "MiPcContinuity.exe"))
                                || File.Exists(Path.Combine(folder, "Launch.exe")))
                            {
                                return folder;
                            }

                            folder = Path.GetDirectoryName(folder);
                        }
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    public static bool UsesContextMenuFileMapping
    {
        get
        {
            var installPath = GetInstallPath();
            return !string.IsNullOrEmpty(installPath)
                && !string.IsNullOrEmpty(GetLaunchFile(installPath))
                && File.Exists(Path.Combine(installPath, "MiSmartShare", "MiDropShellExt.dll"));
        }
    }

    public static string GetExecuteFile()
    {
        var installPath = GetInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            if (UsesContextMenuFileMapping)
            {
                return GetLaunchFile(installPath);
            }

            var filePath = Path.Combine(installPath, "hyperConnect.exe");
            if (File.Exists(filePath)) return filePath;

            filePath = Path.Combine(installPath, "XiaomiPcManager.exe");
            if (File.Exists(filePath)) return filePath;

            filePath = Path.Combine(installPath, "MiPcContinuity.exe");
            if (File.Exists(filePath)) return filePath;
        }
        return string.Empty;
    }

    private static string GetLaunchFile(string installPath)
    {
        var launchFile = Path.Combine(installPath, "Launch.exe");
        if (File.Exists(launchFile)) return launchFile;

        var parentFolder = Path.GetDirectoryName(installPath);
        if (!string.IsNullOrEmpty(parentFolder))
        {
            launchFile = Path.Combine(parentFolder, "Launch.exe");
            if (File.Exists(launchFile)) return launchFile;
        }

        return string.Empty;
    }

    public static string GetIconString()
    {
        var installPath = GetInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var icoFilePath = Path.Combine(installPath, "MiSmartShare", "Assets", "midrop_logo.ico");
            if (File.Exists(icoFilePath)) return icoFilePath;

            icoFilePath = Path.Combine(installPath, "Assets", "midrop_logo.ico");
            if (File.Exists(icoFilePath)) return icoFilePath;

            var exeFilePath = GetExecuteFile();
            if (File.Exists(exeFilePath)) return $"{exeFilePath},0";
        }

        return string.Empty;
    }

    public static Task<bool> LaunchAsync(CancellationToken cancellationToken)
    {
        return LaunchAsync(null, cancellationToken);
    }

    public static Task<bool> LaunchAsync(string? arguments, CancellationToken cancellationToken)
    {
        var messageWindow = FindMessageWindow();
        if (messageWindow != 0)
        {
            if (string.IsNullOrEmpty(arguments)) return Task.FromResult(true);
        }

        var executeFile = GetExecuteFile();
        if (!string.IsNullOrEmpty(executeFile))
        {
            return LaunchAsyncCore(executeFile, arguments, cancellationToken);
        }
        return Task.FromResult(false);
    }

    public static async Task<bool> SendCachedFilesAsync(string cacheKey, TimeSpan timeout)
    {
        var xiaomiFile = await FilesHelper.GetXiaomiFileAsync(cacheKey, default).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(xiaomiFile))
        {
            if (UsesContextMenuFileMapping
                && await SendFilesViaContextMenuMappingAsync(xiaomiFile).ConfigureAwait(false))
            {
                return true;
            }

            return await SendFilesAsyncCore(xiaomiFile, timeout);
        }
        return false;
    }

    public static async Task<bool> SendFilesAsync(string[] files, TimeSpan timeout)
    {
        var xiaomiFile = CreateXiaomiFile(files);
        if (!string.IsNullOrEmpty(xiaomiFile))
        {
            if (UsesContextMenuFileMapping
                && await SendFilesViaContextMenuMappingAsync(xiaomiFile).ConfigureAwait(false))
            {
                return true;
            }

            return await SendFilesAsyncCore(xiaomiFile, timeout);
        }

        return false;
    }

    private static Task<bool> LaunchAsyncCore(string executeFile, string? arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(executeFile, arguments ?? "")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executeFile) ?? string.Empty
        };

        var tcs = new TaskCompletionSource<bool>();

        var registration = cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled(cancellationToken);
        });

        var thread = new Thread(() =>
        {
            try
            {
                var process = Process.Start(psi);
                if (process == null)
                {
                    return;
                }

                while (!cancellationToken.IsCancellationRequested && !process.HasExited)
                {
                    var messageWindow = FindMessageWindow();
                    if (messageWindow != 0)
                    {
                        tcs.TrySetResult(true);
                        break;
                    }

                    Thread.Sleep(500);
                }
            }
            catch { }
            finally
            {
                registration.Dispose();
                tcs.TrySetResult(false);
            }
        });
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private static async Task<bool> SendFilesViaContextMenuMappingAsync(string xiaomiFiles)
    {
        var executeFile = GetExecuteFile();
        if (string.IsNullOrEmpty(executeFile)) return false;

        var mappingName = ContextMenuMappingPrefix + Guid.NewGuid().ToString("N");
        var payload = Encoding.Unicode.GetBytes(xiaomiFiles + '\0');

        try
        {
            using var mapping = MemoryMappedFile.CreateNew(mappingName, payload.Length);
            using var view = mapping.CreateViewAccessor(0, payload.Length);
            view.WriteArray(0, payload, 0, payload.Length);

            var psi = new ProcessStartInfo(
                executeFile,
                $"--contextmenu_dropfile=mapping:{mappingName}")
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executeFile) ?? string.Empty
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var lifetime = FindMessageWindow() != 0
                ? TimeSpan.FromSeconds(3)
                : ContextMenuMappingLifetime;
            await Task.Delay(lifetime).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static Task<bool> SendFilesAsyncCore(string xiaomiFile, TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(xiaomiFile)) return Task.FromResult(false);
        //if (files == null || files.Length == 0) return Task.FromResult(false);

        var messageWindow = FindMessageWindow();
        if (messageWindow == 0) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                tcs.TrySetResult(SendFilesCore(messageWindow, xiaomiFile, timeout));
            }
            catch { }
            tcs.TrySetResult(false);

            unsafe static bool SendFilesCore(nint messageWindow, string xiaomiFiles, TimeSpan timeout)
            {
                fixed (char* ptr = xiaomiFiles)
                {
                    var s = default(COPYDATASTRUCT);
                    s.dwData = 0;
                    s.cbData = (xiaomiFiles.Length) * 2;
                    s.lpData = (nint)ptr;

                    var timeout2 = unchecked((uint)timeout.TotalMilliseconds);
                    if (timeout2 > 15 * 1000) timeout2 = 15 * 1000;
                    if (timeout2 < 0) timeout2 = 0;

                    var res = SendMessageTimeoutW(messageWindow, 74, (IntPtr)1, (nint)(&s), 0, timeout2, out var lpdwResult);
                    return res != 0;
                }
            }
        });
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    internal static string CreateXiaomiFile(string[] files)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < files.Length; i++)
        {
            var path = files[i];

            if (!string.IsNullOrEmpty(path)
                && Path.IsPathRooted(path))
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(path);
            }
        }

        return sb.ToString();
    }


    private unsafe static nint FindMessageWindow()
    {
        const string WindowClassName1 = "XiaomiPCManager";
        const string WindowClassName2 = "MiPcContinuity";

        fixed (char* pClassName = WindowClassName1)
        {
            var hwnd = FindWindowW(pClassName, null);
            if (hwnd > 0) return hwnd;
        }
        fixed (char* pClassName = WindowClassName2)
        {
            var hwnd = FindWindowW(pClassName, null);
            if (hwnd > 0) return hwnd;
        }
        return 0;
    }

    [DllImport("user32.dll")]
    private static unsafe extern nint SendMessageW(nint hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static unsafe extern int SendMessageTimeoutW(nint hWnd, int Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

    [DllImport("user32.dll")]
    private static unsafe extern nint FindWindowW(char* lpClassName, char* lpWindowName);

    private struct COPYDATASTRUCT
    {
        public nint dwData;

        public int cbData;

        public nint lpData;
    }

}
