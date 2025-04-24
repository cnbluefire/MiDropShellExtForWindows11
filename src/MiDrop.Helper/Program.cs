// See https://aka.ms/new-console-template for more information
using Microsoft.Win32;
using MiDrop.Core;
using System;
using System.CommandLine;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;

namespace MiDrop.Helper;

public static class Program
{
    public static async Task Main(string[] args)
    {
        bool hasCommandLineArgs = false;

        var fileOptions = new Option<string>("--share-files");
        var rootCommand = new Command("MiDropShellExtHelper");
        rootCommand.AddOption(fileOptions);
        rootCommand.SetHandler(async (string file) =>
        {
            if (!string.IsNullOrEmpty(file))
            {
                hasCommandLineArgs = true;

                await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(file, TimeSpan.FromSeconds(5));
            }
        }, fileOptions);
        await rootCommand.InvokeAsync(args);

        if (!hasCommandLineArgs)
        {
            var activatedEventArgs = AppInstance.GetActivatedEventArgs();
            if (activatedEventArgs.Kind == Windows.ApplicationModel.Activation.ActivationKind.ShareTarget)
            {
                var sharedTargetActivatedEventArgs = (ShareTargetActivatedEventArgs)activatedEventArgs;
                var shareOperation = sharedTargetActivatedEventArgs.ShareOperation;

                shareOperation.ReportStarted();
                try
                {
                    var dataPackageView = shareOperation.Data;

                    if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                    {
                        var items = await dataPackageView.GetStorageItemsAsync();
                        var itemsPathList = items
                            .Select(c => c.Path)
                            .Where(c => File.Exists(c) || Directory.Exists(c))
                            .ToArray();

                        if (itemsPathList != null && itemsPathList.Length > 0)
                        {
                            var key = MiDrop.Core.FilesHelper.SaveFilesAsync(itemsPathList, default).Result;
                            await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                            await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(key, TimeSpan.FromSeconds(5));
                        }
                    }
                }
                finally
                {
                    shareOperation.ReportCompleted();
                }

                return;
            }

            var aumid = GetCurrentApplicationUserModelId();
            if (aumid?.EndsWith("!Placeholder") is true)
            {
                await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync("open_controlcenter", default);
            }
        }

    }


    [DllImport("Kernel32.dll")]
    private unsafe static extern int GetCurrentApplicationUserModelId(uint* applicationUserModelIdLength, char* applicationUserModelId);

    private unsafe static string? GetCurrentApplicationUserModelId()
    {
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        uint length = 0;

        var error = GetCurrentApplicationUserModelId(&length, null);
        if (error == ERROR_INSUFFICIENT_BUFFER && length > 0)
        {
            char* buffer = stackalloc char[(int)length];
            error = GetCurrentApplicationUserModelId(&length, buffer);

            if (error == 0 && length > 1)
            {
                return new string(buffer, 0, (int)length - 1);
            }
        }
        return null;
    }
}