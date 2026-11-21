using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public sealed class MarketplaceFileInstaller
{
    private readonly DownloaderService _downloader;
    private readonly ILogger<MarketplaceFileInstaller> _logger;

    public MarketplaceFileInstaller(DownloaderService downloader, ILogger<MarketplaceFileInstaller>? logger = null)
    {
        _downloader = downloader;
        _logger = logger ?? NullLogger<MarketplaceFileInstaller>.Instance;
    }

    public async Task InstallAsync(
        string url,
        string destinationPath,
        string? expectedHash = null,
        string? expectedHashType = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Marketplace destination path must include a directory.");
        }

        Directory.CreateDirectory(directory);

        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            _logger.LogWarning("Installing marketplace file {DestinationPath} without hash verification.", destinationPath);
        }

        string stagingDir = Path.Combine(directory, ".pocketmc-marketplace-staging");
        Directory.CreateDirectory(stagingDir);
        string stagingPath = Path.Combine(stagingDir, $"{Guid.NewGuid():N}-{Path.GetFileName(destinationPath)}");

        try
        {
            await _downloader.DownloadFileAsync(url, stagingPath, expectedHash, expectedHashType, progress, cancellationToken);
            ValidateDownloadedFile(stagingPath);
            await PromoteAsync(stagingPath, destinationPath, _logger, cancellationToken);
        }
        finally
        {
            TryDeleteFile(stagingPath);
            TryDeleteFile(stagingPath + ".partial");
            TryDeleteDirectory(stagingDir);
        }
    }

    private static void ValidateDownloadedFile(string stagingPath)
    {
        var fileInfo = new FileInfo(stagingPath);
        if (!fileInfo.Exists || fileInfo.Length <= 0)
        {
            throw new InvalidDataException("Marketplace download completed without a non-empty staged file.");
        }
    }

    private static async Task PromoteAsync(
        string sourcePath,
        string destinationPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(destinationPath))
                {
                    string backupPath = destinationPath + $".replace-{Guid.NewGuid():N}.bak";
                    File.Replace(sourcePath, destinationPath, backupPath, ignoreMetadataErrors: true);
                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(sourcePath, destinationPath);
                }

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && (ex is IOException or UnauthorizedAccessException))
            {
                logger.LogDebug(ex, "Marketplace file promotion attempt {Attempt} failed for {DestinationPath}; retrying.", attempt, destinationPath);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)), cancellationToken);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch
        {
        }
    }
}
