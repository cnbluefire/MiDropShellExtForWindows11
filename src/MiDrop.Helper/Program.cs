// See https://aka.ms/new-console-template for more information
using System;
using System.CommandLine;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace MiDrop.Helper;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var fileOptions = new Option<string>("--share-files");
        var rootCommand = new Command("MiDropShellExtHelper");
        rootCommand.AddOption(fileOptions);
        rootCommand.SetHandler(async (string file) =>
        {
            await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
            await MiDrop.Core.XiaomiPcManagerHelper.SendCachedFilesAsync(file, TimeSpan.FromSeconds(5));
        }, fileOptions);
        await rootCommand.InvokeAsync(args);
    }
}