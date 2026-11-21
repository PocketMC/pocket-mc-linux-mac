using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PocketMC.Core.Models;

namespace PocketMC.Infrastructure.Services;

public class AddonUpdateService
{
    private readonly AddonManifestService _manifestService;
    private readonly ModrinthService _modrinth;
    private readonly CurseForgeService _curseForge;
    private readonly MarketplaceFileInstaller _fileInstaller;
    private readonly ILogger<AddonUpdateService> _logger;

    public AddonUpdateService(
        AddonManifestService manifestService,
        ModrinthService modrinth,
        CurseForgeService curseForge,
        MarketplaceFileInstaller fileInstaller,
        ILogger<AddonUpdateService>? logger = null)
    {
        _manifestService = manifestService;
        _modrinth = modrinth;
        _curseForge = curseForge;
        _fileInstaller = fileInstaller;
        _logger = logger ?? NullLogger<AddonUpdateService>.Instance;
    }

    public async Task<AddonUpdateCheckResult> CheckForUpdateAsync(
        string serverDir,
        string fileName,
        string mcVersion,
        string loader,
        EngineCompatibility compat)
    {
        var manifest = await _manifestService.LoadManifestAsync(serverDir);
        var entry = manifest.Entries.FirstOrDefault(e =>
            e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            return new AddonUpdateCheckResult
            {
                Error = "This add-on was not installed from a marketplace. Update checking is unavailable for manually imported files."
            };
        }

        return await CheckForUpdateFromEntryAsync(entry, mcVersion, loader, compat);
    }

    public virtual async Task<AddonUpdateCheckResult> CheckForUpdateFromEntryAsync(
        AddonManifestEntry entry,
        string mcVersion,
        string loader,
        EngineCompatibility compat)
    {
        if (string.IsNullOrWhiteSpace(entry.Provider) || string.IsNullOrWhiteSpace(entry.ProjectId))
        {
            return new AddonUpdateCheckResult { Error = "Incomplete marketplace metadata in addon manifest." };
        }

        try
        {
            if (entry.Provider.Equals("Modrinth", StringComparison.OrdinalIgnoreCase))
            {
                var latest = await _modrinth.GetLatestVersionAsync(entry.ProjectId, mcVersion, new[] { loader });
                if (latest == null)
                {
                    return new AddonUpdateCheckResult { Error = $"No compatible versions found for Minecraft {mcVersion} on {loader}." };
                }

                bool isNewer = !string.Equals(latest.Id, entry.VersionId, StringComparison.OrdinalIgnoreCase);

                return new AddonUpdateCheckResult
                {
                    IsUpdateAvailable = isNewer,
                    LatestVersionId = latest.Id,
                    LatestVersionName = latest.Name,
                    LatestFileName = latest.FileName,
                    LatestDownloadUrl = latest.DownloadUrl,
                    ProjectTitle = latest.ProjectTitle,
                    Hash = latest.Hash,
                    HashType = latest.HashType,
                    ReleaseType = latest.ReleaseType ?? "release",
                    Warnings = latest.Warnings ?? new()
                };
            }

            return new AddonUpdateCheckResult { Error = $"Unsupported addon provider: {entry.Provider}" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates for addon {ProjectId} ({Provider}).", entry.ProjectId, entry.Provider);
            return new AddonUpdateCheckResult { Error = ex.Message };
        }
    }

    public async Task ApplyUpdateAsync(
        string serverDir,
        string oldFileName,
        AddonUpdateCheckResult updateInfo,
        string providerName,
        string projectId,
        EngineCompatibility compat,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.LatestDownloadUrl) || string.IsNullOrWhiteSpace(updateInfo.LatestFileName))
        {
            throw new InvalidOperationException("Invalid update payload: missing download URL or file name.");
        }

        string safeLatestFileName = MarketplaceFileNameSanitizer.RequireSafeFileName(updateInfo.LatestFileName);
        string destDir = Path.Combine(serverDir, compat.PrimaryAddonSubDir);
        Directory.CreateDirectory(destDir);

        string newFilePath = Path.Combine(destDir, safeLatestFileName);

        await _fileInstaller.InstallAsync(
            updateInfo.LatestDownloadUrl,
            newFilePath,
            updateInfo.Hash,
            updateInfo.HashType,
            progress,
            cancellationToken);

        if (!string.Equals(oldFileName, safeLatestFileName, StringComparison.OrdinalIgnoreCase))
        {
            string oldFilePath = Path.Combine(destDir, oldFileName);
            if (File.Exists(oldFilePath))
            {
                try { File.Delete(oldFilePath); }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not delete old addon file {OldFilePath}.", oldFilePath);
                }
            }
        }

        await _manifestService.RegisterInstallAsync(
            serverDir,
            providerName,
            projectId,
            updateInfo.LatestVersionId ?? string.Empty,
            safeLatestFileName,
            updateInfo.ProjectTitle,
            null,
            null,
            fileHash: updateInfo.Hash,
            fileHashType: updateInfo.HashType,
            minecraftVersion: null,
            loader: compat.LoaderName,
            downloadUrl: updateInfo.LatestDownloadUrl);
    }
}
