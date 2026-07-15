using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using PocketMC.Infrastructure.Services;

namespace PocketMC.Infrastructure.Utils
{
    public static class SafeZipExtractor
    {
        public static void ExtractZip(string zipFilePath, string destinationDirectory)
        {
            destinationDirectory = Path.GetFullPath(destinationDirectory);
            if (!destinationDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                destinationDirectory += Path.DirectorySeparatorChar;
            }

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (var entry in archive.Entries)
                {
                    var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                    if (!targetPath.StartsWith(destinationDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Zip Slip detected: Entry '{entry.FullName}' resolves outside destination directory.");
                    }

                    if (targetPath.EndsWith(Path.DirectorySeparatorChar.ToString()) || targetPath.EndsWith("/"))
                    {
                        Directory.CreateDirectory(targetPath);
                    }
                    else
                    {
                        var parentDir = Path.GetDirectoryName(targetPath);
                        if (parentDir != null)
                        {
                            Directory.CreateDirectory(parentDir);
                        }
                        entry.ExtractToFile(targetPath, overwrite: true);
                    }
                }
            }
        }

        public static void ExtractTarGz(string tarGzFilePath, string destinationDirectory)
        {
            destinationDirectory = Path.GetFullPath(destinationDirectory);
            if (!destinationDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                destinationDirectory += Path.DirectorySeparatorChar;
            }

            using (var fileStream = File.OpenRead(tarGzFilePath))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var tarReader = new TarReader(gzipStream))
            {
                TarEntry? entry;
                while ((entry = tarReader.GetNextEntry()) != null)
                {
                    var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.Name));
                    if (!targetPath.StartsWith(destinationDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Zip Slip detected: Entry '{entry.Name}' resolves outside destination directory.");
                    }

                    if (entry.EntryType == TarEntryType.Directory)
                    {
                        Directory.CreateDirectory(targetPath);
                    }
                    else if (entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile)
                    {
                        var parentDir = Path.GetDirectoryName(targetPath);
                        if (parentDir != null)
                        {
                            Directory.CreateDirectory(parentDir);
                        }
                        entry.ExtractToFile(targetPath, true);
                    }
                }
            }
        }

        public static Task ExtractAsync(string zipPath, string extractPath, Action<long, long>? onProgress = null)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(extractPath);

                string extractRoot = Path.GetFullPath(extractPath);
                if (!extractRoot.EndsWith(Path.DirectorySeparatorChar))
                {
                    extractRoot += Path.DirectorySeparatorChar;
                }

                using var archive = ZipFile.OpenRead(zipPath);
                long totalEntries = archive.Entries.Count;
                long entriesExtracted = 0;

                foreach (var entry in archive.Entries)
                {
                    string? destinationPath = PathSafety.ValidateContainedPath(extractRoot, entry.FullName);
                    if (destinationPath == null)
                    {
                        throw new InvalidDataException($"ZIP entry '{entry.FullName}' would extract outside the destination directory.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }

                    entriesExtracted++;
                    onProgress?.Invoke(entriesExtracted, totalEntries);
                }
            });
        }
    }
}

