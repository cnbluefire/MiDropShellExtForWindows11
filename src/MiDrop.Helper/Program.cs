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
using WinRT;

namespace MiDrop.Helper;

public static class Program
{
    public static async Task Main(string[] args)
    {
        bool hasCommandLineArgs = false;

        var fileOption = new Option<string>("--share-files");
        var targetOption = new Option<string?>("--target");
        var rootCommand = new Command("MiDropShellExtHelper")
        {
            fileOption,
            targetOption,
        };
        rootCommand.SetHandler(async (string file, string? target) =>
        {
            if (!string.IsNullOrEmpty(file))
            {
                hasCommandLineArgs = true;

                if (target == "XiaomiShare")
                {
                    await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                    await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(file, TimeSpan.FromSeconds(5));
                }
                else if (target == "HuaweiShare" || target == "HonorShare")
                {
                    var xiaomiFileContent = await FilesHelper.GetXiaomiFileAsync(file, default);
                    var files = FilesHelper.GetFilesFromXiaomiFileContent(xiaomiFileContent);

                    Console.WriteLine("files:");
                    Console.WriteLine(string.Join(Environment.NewLine, files));

                    if (target == "HuaweiShare") await HuaweiPCManagerHelper.SendFilesAsync(files);
                    if (target == "HonorShare") await HonorPCManagerHelper.SendFilesAsync(files);
                }
            }
        }, fileOption, targetOption);
        await rootCommand.InvokeAsync(args);

        if (!hasCommandLineArgs)
        {
            var aumid = GetCurrentApplicationUserModelId();
            var appIdIndex = aumid?.LastIndexOf('!') ?? -1;
            var appId = appIdIndex != -1 ? aumid![(appIdIndex + 1)..] : null;

            var activatedEventArgs = AppInstance.GetActivatedEventArgs();
            if (activatedEventArgs.Kind == Windows.ApplicationModel.Activation.ActivationKind.ShareTarget)
            {
                if (appId == "XiaomiShare" || appId == "HuaweiShare" || appId == "HonorShare")
                {
                    var sharedTargetActivatedEventArgs = activatedEventArgs.As<ShareTargetActivatedEventArgs>();
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
                                if (appId == "XiaomiShare")
                                {
                                    var key = MiDrop.Core.FilesHelper.SaveFilesAsync(itemsPathList, default).Result;
                                    await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                                    await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(key, TimeSpan.FromSeconds(5));
                                }
                                else if (appId == "HuaweiShare")
                                {
                                    await HuaweiPCManagerHelper.SendFilesAsync(itemsPathList);
                                }
                                else if (appId == "HonorShare")
                                {
                                    await HonorPCManagerHelper.SendFilesAsync(itemsPathList);
                                }
                            }
                        }
                    }
                    finally
                    {
                        shareOperation.ReportCompleted();
                    }

                    return;
                }
            }

            if (appId == "XiaomiApp")
            {
                await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync("open_controlcenter", default);
            }
            else if (appId == "HonorApp")
            {

            }
            else if (appId == "HuaweiApp")
            {

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