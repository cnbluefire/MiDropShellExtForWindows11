// See https://aka.ms/new-console-template for more information
using MiDrop.Helper.Forms;
using MiDrop.Helper.Utils;
using System;
using System.CommandLine;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace MiDrop.Helper;

public static class Program
{
    private static Mutex? mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        var fileOptions = new Option<string>("--share-files");
        var rootCommand = new Command("MiDropShellExtHelper");
        rootCommand.AddOption(fileOptions);
        rootCommand.SetHandler(async (string file) =>
        {
            if (!string.IsNullOrEmpty(file))
            {
                await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(file, TimeSpan.FromSeconds(5));
            }
        }, fileOptions);
        rootCommand.Invoke(args);

        using (mutex = new Mutex(true, "395FE0E2-7A4C-4A17-A59F-FF99BBC55390", out var createdNew))
        {
            if (!createdNew)
            {
                // not first instance, return
                return;
            }

            var installPath = MiDrop.Core.XiaomiPcManagerHelper.GetXiaomiPcManagerInstallPath();
            if (string.IsNullOrEmpty(installPath)) return;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            XiaomiPcManagerToastListener.Initialize();

            Application.Run(new MainForm());
        }
    }
}