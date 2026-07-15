using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocketMC.Infrastructure.Services
{
    public static class FileUtils
    {
        public static void AtomicWriteAllText(string filePath, string contents, Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(false);
            string targetPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(tempPath, contents, encoding);
                ReplaceWithTempFile(tempPath, targetPath);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }

        public static async Task AtomicWriteAllTextAsync(
            string filePath,
            string contents,
            Encoding? encoding = null,
            CancellationToken cancellationToken = default)
        {
            encoding ??= new UTF8Encoding(false);
            string targetPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await File.WriteAllTextAsync(tempPath, contents, encoding, cancellationToken);
                ReplaceWithTempFile(tempPath, targetPath);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }

        private static void ReplaceWithTempFile(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                string backupPath = $"{targetPath}.{Guid.NewGuid():N}.bak";
                File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
                TryDeleteFile(backupPath);
                return;
            }

            File.Move(tempPath, targetPath);
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        public static async Task CleanDirectoryAsync(string dirPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(dirPath)) return;

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                StripReadOnly(dirPath);
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(dirPath, recursive: true);
            }, cancellationToken);
        }

        private static void StripReadOnly(string dirPath)
        {
            foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }

            foreach (var directory in Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(directory);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(directory, attrs & ~FileAttributes.ReadOnly);
            }

            var rootAttrs = File.GetAttributes(dirPath);
            if ((rootAttrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(dirPath, rootAttrs & ~FileAttributes.ReadOnly);
        }
    }
}

