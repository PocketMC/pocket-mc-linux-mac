using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;
using PocketMC.Infrastructure.Utils;

namespace PocketMC.Infrastructure.Services;

/// <summary>
/// Manages world archive extraction (.zip / .mcworld), world folder discovery, size reporting, and wiping.
/// </summary>
public sealed class WorldManager
{
    private readonly ILogger<WorldManager> _logger;
    private readonly InstanceOperationCoordinator _operations;

    public WorldManager(ILogger<WorldManager>? logger = null, InstanceOperationCoordinator? operations = null)
    {
        _logger = logger ?? NullLogger<WorldManager>.Instance;
        _operations = operations ?? new InstanceOperationCoordinator();
    }

    public static string ResolveWorldDirectory(string serverDir, EngineType engineType, string? levelName = null)
    {
        if (string.IsNullOrWhiteSpace(serverDir))
        {
            return string.Empty;
        }

        if (engineType == EngineType.Bedrock)
        {
            string folderName = string.IsNullOrWhiteSpace(levelName) ? "Bedrock level" : levelName.Trim();
            string? contained = PathSafety.ValidateContainedPath(Path.Combine(serverDir, "worlds"), folderName);
            return contained ?? Path.Combine(serverDir, "worlds", "Bedrock level");
        }

        string javaName = string.IsNullOrWhiteSpace(levelName) ? "world" : levelName.Trim();
        return PathSafety.ValidateContainedPath(serverDir, javaName) ?? Path.Combine(serverDir, "world");
    }

    public static (bool Exists, string StatusText, string SizeText) InspectWorld(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath))
        {
            return (false, "Not generated yet", "0 B (will generate on start)");
        }

        try
        {
            long totalBytes = Directory.EnumerateFiles(worldPath, "*", SearchOption.AllDirectories)
                .Sum(path =>
                {
                    try { return new FileInfo(path).Length; }
                    catch { return 0L; }
                });

            string sizeText = totalBytes switch
            {
                >= 1024L * 1024L * 1024L => $"{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
                >= 1024L * 1024L => $"{totalBytes / (1024.0 * 1024.0):F1} MB",
                >= 1024L => $"{totalBytes / 1024.0:F1} KB",
                _ => $"{totalBytes} B"
            };

            return (true, "Active World Ready", $"Location: {Path.GetFileName(worldPath)} ({sizeText})");
        }
        catch
        {
            return (true, "Active World Ready", $"Location: {Path.GetFileName(worldPath)}");
        }
    }

    public async Task ImportWorldZipAsync(
        string archivePath,
        string targetWorldPath,
        Action<double, string>? onDetailedProgress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateArchiveExtension(archivePath);
        string instanceRoot = InstanceOperationCoordinator.FindInstanceRoot(targetWorldPath);
        using var operation = await _operations.AcquireAsync(instanceRoot, requiresStopped: true, cancellationToken);
        InstanceFileTransaction.Recover(instanceRoot);

        string tempDir = Path.Combine(Path.GetTempPath(), "PocketMC", $"WorldExtract-{Guid.NewGuid():N}");
        try
        {
            onDetailedProgress?.Invoke(10, "Extracting world archive...");
            await Task.Run(() => SafeZipExtractor.ExtractZip(archivePath, tempDir), cancellationToken);

            onDetailedProgress?.Invoke(65, "Scanning for level.dat...");
            string? worldRoot = await Task.Run(() => FindWorldRoot(tempDir), cancellationToken);
            if (worldRoot == null)
            {
                throw new InvalidOperationException(
                    "Could not find level.dat in the archive. This does not appear to be a valid Minecraft world.");
            }

            string? parentDir = Path.GetDirectoryName(targetWorldPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            string staged = Path.Combine(instanceRoot, $".world-import-{Guid.NewGuid():N}");
            try
            {
                onDetailedProgress?.Invoke(80, "Preparing replacement world...");
                await FileUtils.CopyDirectoryAsync(worldRoot, staged, cancellationToken);

                onDetailedProgress?.Invoke(90, "Installing world...");
                string relative = Path.GetRelativePath(instanceRoot, targetWorldPath);
                InstanceFileTransaction.Commit(
                    instanceRoot,
                    new[] { new InstanceFileTransaction.Replacement(staged, relative) },
                    cancellationToken);
            }
            finally
            {
                if (Directory.Exists(staged))
                {
                    await FileUtils.CleanDirectoryAsync(staged, cancellationToken);
                }
            }

            _logger.LogInformation("Imported world from {Archive} to {TargetWorld}.", archivePath, targetWorldPath);
            onDetailedProgress?.Invoke(100, "World imported successfully.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { }
            }
        }
    }

    public Task ImportWorldZipAsync(string archivePath, string targetWorldPath, Action<string>? onProgress)
        => ImportWorldZipAsync(archivePath, targetWorldPath, (_, msg) => onProgress?.Invoke(msg));

    public async Task WipeWorldAsync(string instanceRoot, string targetWorldPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetWorldPath) || !Directory.Exists(targetWorldPath))
        {
            return;
        }

        using var operation = await _operations.AcquireAsync(instanceRoot, requiresStopped: true, cancellationToken);
        await FileUtils.CleanDirectoryAsync(targetWorldPath, cancellationToken);
    }

    private static void ValidateArchiveExtension(string archivePath)
    {
        string ext = Path.GetExtension(archivePath);
        if (!ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".mcworld", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported archive format '{ext}'. Expected .zip or .mcworld.");
        }
    }

    private static string? FindWorldRoot(string searchDir)
    {
        if (File.Exists(Path.Combine(searchDir, "level.dat")))
        {
            return searchDir;
        }

        foreach (string subDir in Directory.GetDirectories(searchDir))
        {
            string? match = FindWorldRoot(subDir);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
