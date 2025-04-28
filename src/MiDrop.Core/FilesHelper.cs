using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiDrop.Core
{
    public static class FilesHelper
    {
        private static readonly string FilesCacheFolder =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiDropHelper",
                "ShareFiles");

        public static async Task<string> SaveFilesAsync(string[] files, CancellationToken cancellationToken)
        {
            DeleteExpiredFiles();

            if (!System.IO.Directory.Exists(FilesCacheFolder))
            {
                System.IO.Directory.CreateDirectory(FilesCacheFolder);
            }

            var xiaomiFile = XiaomiPcManagerHelper.CreateXiaomiFile(files);
            if (!string.IsNullOrEmpty(xiaomiFile))
            {
                var key = Guid.NewGuid().ToString("N");

                try
                {
                    var filePath = System.IO.Path.Combine(FilesCacheFolder, key);
                    await System.IO.File.WriteAllTextAsync(filePath, xiaomiFile);
                    return key;
                }
                catch { }
            }
            return string.Empty;
        }

        public static async Task<string> GetXiaomiFileAsync(string cacheKey, CancellationToken cancellationToken)
        {
            DeleteExpiredFiles();

            try
            {
                var filePath = System.IO.Path.Combine(FilesCacheFolder, cacheKey);
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.DeleteOnClose))
                using (var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true))
                {
                    return await reader.ReadToEndAsync(cancellationToken);
                }
            }
            catch { }
            return string.Empty;
        }

        public static string[] GetFilesFromXiaomiFileContent(string fileContent)
        {
            return fileContent.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Where(c => File.Exists(c) || Directory.Exists(c))
                .ToArray();
        }

        private static void DeleteExpiredFiles()
        {
            try
            {
                var files = System.IO.Directory.GetFiles(FilesCacheFolder);
                if (files != null && files.Length > 0)
                {
                    var now = DateTime.Now;
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            var createTime = System.IO.File.GetCreationTime(files[i]);
                            if ((now - createTime).TotalDays > 0.5)
                            {
                                System.IO.File.Delete(files[i]);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
