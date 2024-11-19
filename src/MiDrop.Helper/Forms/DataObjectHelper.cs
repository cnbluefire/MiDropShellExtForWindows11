using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;

namespace MiDrop.Helper.Forms
{
    internal static class DataObjectHelper
    {
        private static HttpClient? httpClient;

        public static async ValueTask<DataValue[]?> ProcessDataObjectAsync(IDataObject dataObject, CancellationToken cancellationToken)
        {
            if (dataObject == null) return [];

            var result = ProcessDropFile(dataObject);
            if (result != null) return result;

            string[]? fileNames = null;

            try
            {
                if (dataObject.GetDataPresent("FileGroupDescriptorW")
                    && dataObject.GetData("FileGroupDescriptorW") is MemoryStream fileGroupDescriptorStream)
                {
                    using (fileGroupDescriptorStream)
                    {
                        fileNames = GetFileNames(fileGroupDescriptorStream);
                        if (fileNames != null) MakeFileNameUnique(fileNames);
                    }
                }
            }
            catch { }

            try
            {
                if (fileNames != null)
                {
                    result = await ProcessFileContentsAsync(dataObject, fileNames, cancellationToken);
                    if (result != null) return result;
                }
            }
            catch { }

            try
            {
                result = await ProcessXMozUrlAsync(dataObject, fileNames, cancellationToken);
                if (result != null) return result;
            }
            catch { }

            try
            {
                var textResult = await ProcessTextAsync(dataObject, cancellationToken);
                if (textResult != null) return [textResult];
            }
            catch { }

            return null;
        }

        private static DataValue[]? ProcessDropFile(IDataObject dataObject)
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                if (dataObject.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string[] fileNames = [.. files.Select(c => System.IO.Path.GetFileName(c))];
                    MakeFileNameUnique(fileNames);

                    return [.. files.Select((c, i) => new DataValue(DataType.FilePath, fileNames[i], c))];
                }
            }
            return null;
        }

        private static async ValueTask<DataValue[]?> ProcessFileContentsAsync(IDataObject dataObject, string[] fileNames, CancellationToken cancellationToken)
        {
            if (dataObject.GetDataPresent("FileContents"))
            {
                MemoryStream[]? fileContents = null;
                var data = dataObject.GetData("FileContents");
                if (data is MemoryStream[] tmp1) fileContents = tmp1;
                else if (data is MemoryStream tmp2) fileContents = [tmp2];

                if (fileContents != null && fileContents.Length > 0)
                {
                    try
                    {
                        if (fileContents.Length == fileNames.Length)
                        {
                            var list = new List<DataValue>();

                            for (int i = 0; i < fileNames.Length; i++)
                            {
                                var filePath = await WriteToTempFileAsync(fileNames[i], fileContents[i], cancellationToken);
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    list.Add(new DataValue(DataType.FilePath, fileNames[i], filePath));
                                }
                            }

                            if (list.Count > 0)
                            {
                                return [.. list];
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        for (int i = 0; i < fileContents.Length; i++)
                        {
                            fileContents[i].Dispose();
                        }
                    }
                }
            }
            return null;
        }

        private static async ValueTask<DataValue[]?> ProcessXMozUrlAsync(IDataObject dataObject, string[]? fileNames, CancellationToken cancellationToken)
        {
            if (dataObject.GetDataPresent("text/x-moz-url")
                && dataObject.GetData("text/x-moz-url") is MemoryStream memoryStream)
            {
                using (memoryStream)
                {
                    var parts = Encoding.Unicode.GetString(memoryStream.ToArray())
                        .Split((char)10);

                    var list = new List<DataValue>();

                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (Uri.TryCreate(parts[i], UriKind.Absolute, out var uri))
                        {
                            if (!uri.Host.Equals("loc.dingtalk.com", StringComparison.OrdinalIgnoreCase))
                            {
                                var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                                if (fileNames != null && fileNames.Length > i && !string.IsNullOrEmpty(fileNames[i]))
                                {
                                    fileName = fileNames[i];
                                }

                                Stream? stream = null;
                                try
                                {
                                    if (uri.IsFile)
                                    {
                                        stream = new FileStream(uri.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    }
                                    else
                                    {
                                        if (httpClient == null) httpClient = new HttpClient();
                                        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                                        stream = await httpClient.GetStreamAsync(uri, cts.Token);
                                    }

                                    var filePath = await WriteToTempFileAsync(fileName, stream, cancellationToken);
                                    if (!string.IsNullOrEmpty(filePath))
                                    {
                                        list.Add(new DataValue(DataType.FilePath, fileName, filePath));
                                    }
                                }
                                catch { }
                                finally
                                {
                                    stream?.Dispose();
                                }
                            }
                        }
                    }

                    if (list.Count > 0)
                    {
                        return [.. list];
                    }
                }
            }
            return null;
        }

        private static async ValueTask<DataValue?> ProcessTextAsync(IDataObject dataObject, CancellationToken cancellationToken)
        {
            if (dataObject.GetDataPresent(DataFormats.Text)
                && dataObject.GetData(DataFormats.Text) is string text
                && !string.IsNullOrEmpty(text))
            {
                if (text.Length >= 500000)
                {
                    try
                    {
                        var tempFolder = GetTempFolder();
                        var fileName = CreateTempFileName(".txt");
                        var fullPath = System.IO.Path.Combine(tempFolder, fileName);

                        await System.IO.File.WriteAllTextAsync(fullPath, text, cancellationToken);
                        return new DataValue(DataType.FilePath, fileName, fullPath);
                    }
                    catch { }
                }
                else
                {
                    return new DataValue(DataType.Text, null, text);
                }
            }

            return null;
        }

        private static string CreateTempFileName(string ext)
        {
            if (!string.IsNullOrEmpty(ext) && ext[0] != '.') ext = $".{ext}";
            return $"{DateTime.Now:yyyyMMddHHmmss}_{$"{Guid.NewGuid():N}"[..8]}{ext}";
        }

        private static FileStream CreateTempFile(string fileName, out string filePath)
        {
            var tempFolder = GetTempFolder();
            filePath = System.IO.Path.Combine(tempFolder, fileName);

            try
            {
                return new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            }
            catch
            {
                try { System.IO.File.Delete(filePath); } catch { }
                throw;
            }
        }

        private static async Task<string?> WriteToTempFileAsync(string fileName, Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                using (var fs = CreateTempFile(fileName, out var filePath))
                {
                    await stream.CopyToAsync(fs, cancellationToken);
                    return filePath;
                }
            }
            catch { }
            return null;
        }

        private static string GetTempFolder()
        {
            var tmpFolder = System.IO.Path.Combine(Path.GetTempPath(), "MiDrop.Helper");
            if (!Directory.Exists(tmpFolder))
            {
                Directory.CreateDirectory(tmpFolder);
            }
            else
            {
                try
                {
                    var folders = Directory.GetDirectories(tmpFolder);
                    if (folders != null)
                    {
                        var now = DateTime.Now;
                        for (int i = 0; i < folders.Length; i++)
                        {
                            var createTime = Directory.GetCreationTime(folders[i]);
                            if ((now - createTime).TotalDays > 0.5)
                            {
                                try { Directory.Delete(folders[i], true); }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            var subFolder = "";

            do
            {
                subFolder = System.IO.Path.Combine(tmpFolder, $"{Guid.NewGuid():N}"[..8]);
            } while (Directory.Exists(subFolder));
            Directory.CreateDirectory(subFolder);

            return subFolder;
        }

        private static void MakeFileNameUnique(string[] names)
        {
            var nameDict = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++)
            {
                if (nameDict.TryGetValue(names[i], out var v)) v++;
                else v = 0;
                nameDict[names[i]] = v;
                if (v > 0)
                {
                    names[i] = $"{names[i]} ({v})";
                }
            }
        }

        private unsafe static string[]? GetFileNames(MemoryStream fileGroupDescriptorStream)
        {
            var fileGroupDescriptorBytes = fileGroupDescriptorStream.ToArray();

            fixed (void* _pFileGroupDescriptor = fileGroupDescriptorBytes)
            {
                var pFileGroupDescriptor = (Windows.Win32.UI.Shell.FILEGROUPDESCRIPTORW*)_pFileGroupDescriptor;

                if (pFileGroupDescriptor != null)
                {
                    var count = (int)(pFileGroupDescriptor->cItems);

                    var names = new string[count];

                    var span = pFileGroupDescriptor->fgd.AsSpan(count);
                    for (int i = 0; i < count; i++)
                    {
                        names[i] = span[i].cFileName.ToString();
                    }

                    return names;
                }
            }
            return null;
        }

        internal record class DataValue(DataType DataType, string? FileName, string? Value);

        internal enum DataType
        {
            None,
            FilePath,
            DownloadUrl,
            Text,
        }
    }
}
