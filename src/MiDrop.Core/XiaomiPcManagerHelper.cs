using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MiDrop.Core;

public class XiaomiPcManagerHelper
{
    private static readonly IReadOnlyList<string> PcManagerRegKeys = [
        // 5.x / 6.x
        "{504d69c0-cb52-48df-b5b5-7161829fabc8}",

        // 4.x
        "{1bca9901-05c3-4d01-8ad4-78da2eac9b3f}",
        ];

    /// <summary>
    /// 小米电脑管家 6.x 之后右键菜单走"共享内存 + Launch.exe --contextmenu_dropfile"协议。
    /// 宿主程序从安装根目录的 Launch.exe 启动，载荷放在这个命名映射里。
    /// </summary>
    private const string ContextMenuMappingName = "Local\\MiDropFileMappingObject";

    private const string ContextMenuArgument = "--contextmenu_dropfile=1";

    private const uint PageReadWrite = 0x04;

    private const uint FileMapAllAccess = 0x0F001F;

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
                        if (folder?.Contains("\\native-interconnect\\") is true)
                        {
                            while (!string.IsNullOrEmpty(folder))
                            {
                                if (File.Exists(Path.Combine(folder, "hyperConnect.exe"))) break;
                                folder = Path.GetDirectoryName(folder);
                            }
                        }
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

    /// <summary>
    /// 从 shell 扩展所在的组件目录往上找到安装根目录（含 Launch.exe 的那一层）。
    /// 5.x 的目录结构是 &lt;root&gt;\&lt;version&gt;\MiSmartShare\，6.x 把主程序挪到了 &lt;root&gt;\&lt;version&gt;\，
    /// 所以这里靠"往上找 Launch.exe"来兼容两种布局。
    /// </summary>
    public static string GetRootPath()
    {
        var installPath = GetInstallPath();
        if (string.IsNullOrEmpty(installPath)) return string.Empty;

        var folder = installPath;
        for (int i = 0; i < 4 && !string.IsNullOrEmpty(folder); i++)
        {
            if (File.Exists(Path.Combine(folder, "Launch.exe"))) return folder;
            folder = Path.GetDirectoryName(folder);
        }

        return string.Empty;
    }

    /// <summary>
    /// 6.x 的右键菜单入口：安装根目录下的 Launch.exe。
    /// </summary>
    public static string GetLaunchFile()
    {
        var root = GetRootPath();
        if (string.IsNullOrEmpty(root)) return string.Empty;

        var launchFile = Path.Combine(root, "Launch.exe");
        return File.Exists(launchFile) ? launchFile : string.Empty;
    }

    public static string GetExecuteFile()
    {
        var root = GetRootPath();
        if (!string.IsNullOrEmpty(root))
        {
            var launchFile = Path.Combine(root, "Launch.exe");
            if (File.Exists(launchFile)) return launchFile;

            var rootExe = Path.Combine(root, "XiaomiPcManager.exe");
            if (File.Exists(rootExe)) return rootExe;
        }

        var installPath = GetInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var filePath = Path.Combine(installPath, "hyperConnect.exe");
            if (File.Exists(filePath)) return filePath;

            filePath = Path.Combine(installPath, "XiaomiPcManager.exe");
            if (File.Exists(filePath)) return filePath;

            filePath = Path.Combine(installPath, "MiPcContinuity.exe");
            if (File.Exists(filePath)) return filePath;
        }
        return string.Empty;
    }

    public static string GetIconString()
    {
        var installPath = GetInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var icoFilePath = Path.Combine(installPath, "Assets", "midrop_logo.ico");
            if (File.Exists(icoFilePath)) return icoFilePath;

            var root = GetRootPath();
            if (!string.IsNullOrEmpty(root))
            {
                icoFilePath = Path.Combine(root, "Assets", "midrop_logo.ico");
                if (File.Exists(icoFilePath)) return icoFilePath;
            }

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
        // 6.x：发送动作本身就是"建映射 + 拉起 Launch.exe"，不需要先把主程序单独拉起来
        if (string.IsNullOrEmpty(arguments) && !string.IsNullOrEmpty(GetLaunchFile()))
        {
            return Task.FromResult(true);
        }

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
            return await SendFilesAsyncCore(xiaomiFile, timeout);
        }
        return false;
    }

    public static Task<bool> SendFilesAsync(string[] files, TimeSpan timeout)
    {
        var xiaomiFile = CreateXiaomiFile(files);
        if (!string.IsNullOrEmpty(xiaomiFile))
        {
            return SendFilesAsyncCore(xiaomiFile, timeout);
        }

        return Task.FromResult(false);
    }

    private static Task<bool> LaunchAsyncCore(string executeFile, string? arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(executeFile, arguments ?? "")
        {
            UseShellExecute = true
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
                var process = Process.Start(executeFile);
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
            registration.Dispose();
            tcs.TrySetResult(false);
        });
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    internal static Task<bool> SendFilesAsyncCore(string xiaomiFile, TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(xiaomiFile)) return Task.FromResult(false);

        var launchFile = GetLaunchFile();
        if (!string.IsNullOrEmpty(launchFile))
        {
            return Task.Run(() => SendFilesViaContextMenuMapping(launchFile, xiaomiFile, timeout));
        }

        // 5.x 及更早版本：仍然用 WM_COPYDATA 把路径列表丢给主程序的隐藏窗口
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

    /// <summary>
    /// 6.x 的发送协议：
    /// 1) 把 "parent_dir:&lt;首个文件所在目录&gt;|文件1|文件2" 以 UTF-16 写进命名共享内存；
    /// 2) 启动 &lt;安装根目录&gt;\Launch.exe --contextmenu_dropfile=1，由主程序读取映射并弹出互传发送窗。
    /// </summary>
    private static bool SendFilesViaContextMenuMapping(string launchFile, string xiaomiFile, TimeSpan timeout)
    {
        string[] files = xiaomiFile.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (files.Length == 0) return false;

        var parentDir = Path.GetDirectoryName(files[0]) ?? string.Empty;
        var payload = "parent_dir:" + parentDir + "|" + xiaomiFile;

        var mapHandle = nint.Zero;
        try
        {
            var payloadBytes = Encoding.Unicode.GetBytes(payload + "\0");

            // 上一轮可能还留着一个同名映射，稍等重试几次
            for (int attempt = 0; attempt < 8 && mapHandle == nint.Zero; attempt++)
            {
                mapHandle = CreateFileMappingW(new nint(-1), nint.Zero, PageReadWrite, 0, (uint)payloadBytes.Length, ContextMenuMappingName);
                if (mapHandle == nint.Zero) Thread.Sleep(120);
            }

            if (mapHandle == nint.Zero) return false;

            var view = MapViewOfFile(mapHandle, FileMapAllAccess, 0, 0, (nuint)payloadBytes.Length);
            if (view == nint.Zero) return false;

            try
            {
                Marshal.Copy(payloadBytes, 0, view, payloadBytes.Length);
            }
            finally
            {
                UnmapViewOfFile(view);
            }

            var psi = new ProcessStartInfo(launchFile, ContextMenuArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(launchFile) ?? string.Empty
            };
            Process.Start(psi)?.Dispose();

            // 给主程序留出打开并读取映射的时间，之后释放
            var hold = timeout.TotalMilliseconds;
            if (hold < 800) hold = 800;
            if (hold > 3000) hold = 3000;
            Thread.Sleep((int)hold);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (mapHandle != nint.Zero)
            {
                CloseHandle(mapHandle);
            }
        }
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateFileMappingW(nint hFile, nint lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint MapViewOfFile(nint hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, nuint dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(nint lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

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
