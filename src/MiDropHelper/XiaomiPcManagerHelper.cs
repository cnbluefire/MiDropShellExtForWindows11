using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MiDropHelper;

public class XiaomiPcManagerHelper
{
    public static string GetXiaomiPcManagerInstallPath()
    {
        try
        {
            using (var subKey = Registry.ClassesRoot.OpenSubKey("CLSID\\{1bca9901-05c3-4d01-8ad4-78da2eac9b3f}\\InprocServer32"))
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
        catch { }

        return string.Empty;
    }

    public static string GetXiaomiPcManagerExecuteFile()
    {
        var installPath = GetXiaomiPcManagerInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var filePath = Path.Combine(installPath, "XiaomiPcManager.exe");
            if (File.Exists(filePath))
            {
                return filePath;
            }
        }
        return string.Empty;
    }

    public static Task<bool> LaunchAsync(CancellationToken cancellationToken)
    {
        var messageWindow = FindMessageWindow();
        if (messageWindow != 0) return Task.FromResult(true);

        var executeFile = GetXiaomiPcManagerExecuteFile();
        if (!string.IsNullOrEmpty(executeFile))
        {
            return LaunchAsyncCore(executeFile, cancellationToken);
        }
        return Task.FromResult(false);
    }

    public static Task<bool> SendFilesAsync(string[] files, TimeSpan timeout)
    {
        if (files == null || files.Length == 0) return Task.FromResult(false);

        var messageWindow = FindMessageWindow();
        if (messageWindow == 0) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
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

                unsafe
                {
                    fixed (char* ptr = file)
                    {
                        var s = default(COPYDATASTRUCT);
                        s.dwData = 0;
                        s.cbData = (file.Length) * 2;
                        s.lpData = (nint)ptr;

                        var timeout2 = unchecked((uint)timeout.TotalMilliseconds);
                        if (timeout2 > 15 * 1000) timeout2 = 15 * 1000;
                        if (timeout2 < 0) timeout2 = 0;

                        var res = SendMessageTimeoutW(messageWindow, 74, (IntPtr)1, (nint)(&s), 0, timeout2, out var lpdwResult);
                        tcs.SetResult(res != 0);
                    }
                }
            }
            catch { }
            tcs.SetResult(false);
        });
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private static Task<bool> LaunchAsyncCore(string executeFile, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(executeFile)
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

    private unsafe static nint FindMessageWindow()
    {
        const string WindowClassName = "XiaomiPCManager";

        fixed (char* pClassName = WindowClassName)
        {
            return FindWindowW(pClassName, null);
        }
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
